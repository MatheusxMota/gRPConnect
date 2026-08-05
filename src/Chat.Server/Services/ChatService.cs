using System.Collections.Concurrent;
using Chat.Contracts;
using Chat.Server.Models;
using Grpc.Core;

namespace Chat.Server.Services;

public class ChatService : Contracts.ChatService.ChatServiceBase
{
    private static readonly ConcurrentDictionary<Guid, ClientConnection> _clients = new();

    public override async Task Connect(
        IAsyncStreamReader<ClientMessage> requestStream,
        IServerStreamWriter<ServerMessage> responseStream,
        ServerCallContext context)
    {
        var cancellationToken = context.CancellationToken;

        // 1. Primeira mensagem deve ser o JoinRequest
        if (!await requestStream.MoveNext(cancellationToken)) return;

        var firstMessage = requestStream.Current;
        if (firstMessage.PayloadCase != ClientMessage.PayloadOneofCase.Join) return;

        var join = firstMessage.Join;
        var connection = new ClientConnection
        {
            Id = Guid.NewGuid(),
            Name = join.Name,
            Room = join.Room,
            ResponseStream = responseStream
        };

        _clients.TryAdd(connection.Id, connection);
        Console.WriteLine($"[Server] {connection.Name} entrou na sala '{connection.Room}'. Total online: {_clients.Count}");

        // 2. Broadcast de ENTRADA para os demais membros da sala
        await BroadcastNotificationToRoomAsync(
            connection.Room, 
            $"[Sistema] {connection.Name} entrou na sala.", 
            excludeClientId: connection.Id);

        try
        {
            await foreach (var clientMessage in requestStream.ReadAllAsync(cancellationToken))
            {
                if (clientMessage.PayloadCase == ClientMessage.PayloadOneofCase.ChatMessage)
                {
                    var incomingMsg = clientMessage.ChatMessage;

                    // Trata comandos iniciados com '/'
                    if (incomingMsg.Content.StartsWith("/"))
                    {
                        var fullText = incomingMsg.Content.Trim();

                        if (fullText.Equals("/users", StringComparison.OrdinalIgnoreCase))
                        {
                            var usersInRoom = _clients.Values
                                .Where(c => c.Room == connection.Room)
                                .Select(c => c.Name)
                                .OrderBy(n => n)
                                .ToList();

                            var userListFormatted = string.Join(", ", usersInRoom);
                            var responseText = $"[Sistema] Usuários online na sala '{connection.Room}' ({usersInRoom.Count}): {userListFormatted}";

                            var systemNotification = new ServerMessage
                            {
                                Notification = new SystemNotification
                                {
                                    Text = responseText,
                                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                                }
                            };

                            await responseStream.WriteAsync(systemNotification, CancellationToken.None);
                        }
                        else if (fullText.StartsWith("/private ", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = fullText.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);

                            if (parts.Length < 3)
                            {
                                var usageMsg = new ServerMessage
                                {
                                    Notification = new SystemNotification
                                    {
                                        Text = "[Sistema] Uso correto: /private <nome> <mensagem>",
                                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                                    }
                                };
                                await responseStream.WriteAsync(usageMsg, CancellationToken.None);
                            }
                            else
                            {
                                var targetName = parts[1];
                                var privateContent = parts[2];

                                var targetClient = _clients.Values.FirstOrDefault(c => 
                                    c.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));

                                if (targetClient != null)
                                {
                                    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                                    var msgToTarget = new ServerMessage
                                    {
                                        Notification = new SystemNotification
                                        {
                                            Text = $"[Privado de {connection.Name}]: {privateContent}",
                                            Timestamp = timestamp
                                        }
                                    };
                                    await targetClient.ResponseStream.WriteAsync(msgToTarget, CancellationToken.None);

                                    if (targetClient.Id != connection.Id)
                                    {
                                        var msgToSender = new ServerMessage
                                        {
                                            Notification = new SystemNotification
                                            {
                                                Text = $"[Privado para {targetClient.Name}]: {privateContent}",
                                                Timestamp = timestamp
                                            }
                                        };
                                        await responseStream.WriteAsync(msgToSender, CancellationToken.None);
                                    }

                                    Console.WriteLine($"[Private] {connection.Name} -> {targetClient.Name}: {privateContent}");
                                }
                                else
                                {
                                    var errorMsg = new ServerMessage
                                    {
                                        Notification = new SystemNotification
                                        {
                                            Text = $"[Sistema] Usuário '{targetName}' não foi encontrado.",
                                            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                                        }
                                    };
                                    await responseStream.WriteAsync(errorMsg, CancellationToken.None);
                                }
                            }
                        }
                        else
                        {
                            var unknownCmd = new ServerMessage
                            {
                                Notification = new SystemNotification
                                {
                                    Text = $"[Sistema] Comando desconhecido: '{incomingMsg.Content}'",
                                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                                }
                            };
                            await responseStream.WriteAsync(unknownCmd, CancellationToken.None);
                        }

                        continue;
                    }

                    // Broadcast normal para mensagens comuns
                    Console.WriteLine($"[Broadcast] [{incomingMsg.Room}] {connection.Name}: {incomingMsg.Content}");

                    var serverMessage = new ServerMessage
                    {
                        ChatMessage = new ChatMessage
                        {
                            Id = Guid.NewGuid().ToString(),
                            Sender = new User
                            {
                                Id = connection.Id.ToString(),
                                Name = connection.Name,
                                Room = connection.Room
                            },
                            Room = incomingMsg.Room,
                            Content = incomingMsg.Content,
                            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                        }
                    };

                    var roomClients = _clients.Values.Where(c => c.Room == incomingMsg.Room);

                    foreach (var client in roomClients)
                    {
                        try
                        {
                            await client.ResponseStream.WriteAsync(serverMessage, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Server] Erro ao enviar para {client.Name}: {ex.Message}");
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[Server] Conexão cancelada por {connection.Name}.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Server] Erro na conexão de {connection.Name}: {ex.Message}");
        }
        finally
        {
            // 3. Remove o cliente do dicionário
            _clients.TryRemove(connection.Id, out _);
            Console.WriteLine($"[Server] {connection.Name} desconectou. Total online: {_clients.Count}");

            // 4. Broadcast de SAÍDA para os membros remanescentes da sala.
            // Nota: Usamos CancellationToken.None para garantir o envio mesmo que o token do cliente cancelado tenha expirado.
            await BroadcastNotificationToRoomAsync(
                connection.Room, 
                $"[Sistema] {connection.Name} saiu da sala.");
        }
    }

    /// <summary>
    /// Envia uma notificação de sistema para todos os membros de uma sala específica.
    /// </summary>
    private static async Task BroadcastNotificationToRoomAsync(string room, string text, Guid? excludeClientId = null)
    {
        var notification = new ServerMessage
        {
            Notification = new SystemNotification
            {
                Text = text,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }
        };

        var targets = _clients.Values.Where(c => c.Room == room);

        if (excludeClientId.HasValue)
        {
            targets = targets.Where(c => c.Id != excludeClientId.Value);
        }

        foreach (var client in targets)
        {
            try
            {
                // CancellationToken.None garante que o envio ocorra na finalização sem ser abortado
                await client.ResponseStream.WriteAsync(notification, CancellationToken.None);
            }
            catch
            {
                // Falhas pontuais no stream de envio são ignoradas silenciosamente
            }
        }
    }
}
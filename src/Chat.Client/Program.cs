using Chat.Contracts;
using Grpc.Core;
using Grpc.Net.Client;

Console.Write("Digite seu nome: ");
var name = Console.ReadLine() ?? "Anonimo";

Console.Write("Digite a sala: ");
var room = Console.ReadLine() ?? "Geral";

using var channel = GrpcChannel.ForAddress("https://localhost:7057");
var client = new ChatService.ChatServiceClient(channel);

using var call = client.Connect();

// Envia a mensagem de Join
await call.RequestStream.WriteAsync(new ClientMessage
{
    Join = new JoinRequest
    {
        Name = name,
        Room = room
    }
});

Console.WriteLine($"\n--- Conectado como '{name}' na sala '{room}' ---");
Console.WriteLine("Comandos disponíveis: /users, /private <nome> <mensagem>");
Console.WriteLine("Digite sua mensagem e pressione ENTER (or Ctrl+C para sair):\n");

// Task em segundo plano para LER respostas do servidor
var receiveTask = Task.Run(async () =>
{
    try
    {
        await foreach (var response in call.ResponseStream.ReadAllAsync())
        {
            if (response.PayloadCase == ServerMessage.PayloadOneofCase.ChatMessage)
            {
                var msg = response.ChatMessage;
                Console.WriteLine($"[{msg.Sender.Name}]: {msg.Content}");
            }
            else if (response.PayloadCase == ServerMessage.PayloadOneofCase.Notification)
            {
                var notification = response.Notification;
                Console.WriteLine($"{notification.Text}");
            }
        }
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
    {
        // Cancelamento normal ao fechar
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Erro na recepção]: {ex.Message}");
    }
});

// Loop principal de envio
while (true)
{
    var text = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(text)) continue;

    var chatMessage = new ClientMessage
    {
        ChatMessage = new ChatMessage
        {
            Content = text,
            Room = room
        }
    };

    await call.RequestStream.WriteAsync(chatMessage);
}
using Chat.Contracts;
using Grpc.Core;

namespace Chat.Server.Models;

public class ClientConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public IServerStreamWriter<ServerMessage> ResponseStream { get; set; } = null!;
}
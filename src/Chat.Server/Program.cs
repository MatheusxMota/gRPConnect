using Chat.Server.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<ChatService>();
app.MapGet("/", () => "gRPC Server rodando...");

app.Run();
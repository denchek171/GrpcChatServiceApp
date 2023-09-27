using GrpcChatServiceApp.Services.RpcServices;
using GrpcChatServiceApp.Services.СommunicationServices;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);
//builder.WebHost.ConfigureKestrel(options =>
//{
//    // Setup a HTTP/2 endpoint without TLS.
//    options.ListenLocalhost(15032, o => o.Protocols =
//        HttpProtocols.Http2);
//});

builder.WebHost.UseKestrel().ConfigureKestrel(
    options =>
    {
        options.ListenAnyIP(7125, listenOptions => { listenOptions.Protocols = HttpProtocols.Http2; });
    });
builder.Services.AddGrpc();

builder.Services.AddSingleton<ChatRoomService>();

var app = builder.Build();

app.MapGrpcService<ChatService>();

app.MapGet("/", () => "App started and running");

app.Run();

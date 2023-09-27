using Communication;
using Grpc.Core;
using Grpc.Net.Client;
using System.Threading.Channels;

Console.Write("Enter your username: ");
string username = Console.ReadLine();

Guid UserId = Guid.Empty;

if (string.IsNullOrWhiteSpace(username))
{
    Console.WriteLine("Username cannot be empty.");
    return;
}

using var channel = GrpcChannel.ForAddress("http://localhost:7135");
var client = new ChatCommunication.ChatCommunicationClient(channel);


try
{
    var call = client.HandleCommunication();

    if (UserId == Guid.Empty)
    {
        await call.RequestStream.WriteAsync(new UserMessage
        {
            Login = new UserMessageLogin
            {
                UserName = username
            }
        });

        await foreach (var response in call.ResponseStream.ReadAllAsync())
        {
            if (response.LoginSuccess != null)
            {
                UserId = new Guid(response.LoginSuccess.UserId);
                Console.WriteLine($"Login success! User ID: {UserId}");
                break;
            }
            else if (response.ServerError != null)
            {
                Console.WriteLine($"Server error: {response.ServerError.ErrorMessage}");
                break;
            }
        }
    }

    if (UserId != Guid.Empty)
    {
        Console.Write("Enter chat Id: ");
        string chatId = Console.ReadLine();

        await call.RequestStream.WriteAsync(new UserMessage
        {
            ChatConnect = new UserMessageChatConnect()
            {
                UserId = UserId.ToString(),
                ChatRoomId = chatId
            }
        });

        await foreach (var response in call.ResponseStream.ReadAllAsync())
        {
            if (response.SuccessMessage != null)
            {

                Console.WriteLine($"{response.SuccessMessage.SuccessMessage}");
                break;
            }
            else if (response.ServerError != null)
            {
                Console.WriteLine($"Server error: {response.ServerError.ErrorMessage}");
                break;
            }
        }

        var readTask = Task.Run(async () =>
        {
            while (await call.ResponseStream.MoveNext())
            {
                var serverMessage = call.ResponseStream.Current;
                if (serverMessage.SuccessMessage != null)
                {
                    Console.WriteLine($"{serverMessage.SuccessMessage}");
                }
                else if (serverMessage.Chat != null)
                {
                    Console.WriteLine($"{serverMessage.Chat.UserName}: {serverMessage.Chat.Text}");
                }
            }
        });

        var writeTask = Task.Run(async () =>
        {
            while (true)
            {
                var text = Console.ReadLine();
                if (text == "exit")
                {
                    break;
                }

                await call.RequestStream.WriteAsync(new UserMessage
                {
                    Chat = new UserMessageChat
                    {
                        Text = text
                    }
                });
            }
        });

        await Task.WhenAny(readTask, writeTask);

        await call.RequestStream.CompleteAsync();
    }

}
catch (RpcException ex)
{
    Console.WriteLine($"Error: {ex.Status}");
}

Console.WriteLine("Press any key to exit...");
Console.ReadKey();

void Logout(AsyncDuplexStreamingCall<UserMessage, ServerMessage> call)
{
    Console.CancelKeyPress += async (sender, e) =>
    {
        if (e.SpecialKey == ConsoleSpecialKey.ControlC)
        {
            //LOGOUT
            await call.RequestStream.WriteAsync(new UserMessage
            {
                Logout = new UserMessageLogout()
                {
                    UserId = UserId.ToString()
                }
            });
            await foreach (var response in call.ResponseStream.ReadAllAsync())
            {
                if (response.SuccessMessage != null)
                {
                    var successMessage = response.SuccessMessage.SuccessMessage;
                    Console.WriteLine($"{successMessage}");
                    break;
                }
                else if (response.ServerError != null)
                {
                    Console.WriteLine($"Server error: {response.ServerError.ErrorMessage}");
                }
            }
        }
    };
}
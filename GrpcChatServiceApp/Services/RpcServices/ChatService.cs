using Communication;
using Google.Protobuf;
using Grpc.Core;
using GrpcChatServiceApp.Services.СommunicationServices;

namespace GrpcChatServiceApp.Services.RpcServices
{
    public class ChatService : ChatCommunication.ChatCommunicationBase
    {
        private readonly ILogger<ChatService> _logger;
        private readonly ChatRoomService _chatRoomService;


        public ChatService(ILogger<ChatService> logger, ChatRoomService chatRoomService)
        {
            _logger = logger;
            _chatRoomService = chatRoomService;
        }

        //TODO : Add validation
        public override async Task HandleCommunication(IAsyncStreamReader<UserMessage> requestStream,
            IServerStreamWriter<ServerMessage> responseStream, ServerCallContext context)
        {
            var userSessionId = string.Empty;
            var chatSessionRoomId = string.Empty;

            while (true)
            {
                var userMessage =
                    await _chatRoomService.ReadMessageWithTimeoutAsync(requestStream, Timeout.InfiniteTimeSpan);

                switch (userMessage.ContentCase)
                {
                    case UserMessage.ContentOneofCase.Login:
                        var loginMessage = userMessage.Login;
                        if (string.IsNullOrEmpty(loginMessage.UserName))
                        {
                            var failureMessage = new ServerMessage
                            {
                                ServerError = new ServerMessageError
                                {
                                    ErrorMessage = "UserName cannot be empty."
                                }
                            };
                            await responseStream.WriteAsync(failureMessage);
                        }
                        else
                        {
                            userSessionId = await _chatRoomService.AddUserToChatUsers(loginMessage.UserName);

                            var successMessage = new ServerMessage { LoginSuccess = new ServerMessageLoginSuccess { UserId = userSessionId } };

                            await responseStream.WriteAsync(successMessage);
                        }

                        break;
                    case UserMessage.ContentOneofCase.Logout:
                        var logoutMessage = userMessage.Logout;
                        if (string.IsNullOrEmpty(logoutMessage.UserId) && !string.IsNullOrWhiteSpace(userSessionId))
                        {
                            await _chatRoomService.DeleteUserToChatUsers(new Guid(logoutMessage.UserId));
                            userSessionId = string.Empty;
                            chatSessionRoomId = string.Empty;

                            var successMessage = new ServerMessage
                            {
                                SuccessMessage = new ServerMessageSuccess
                                {
                                    SuccessMessage = "User logout success."
                                }
                            };

                            await responseStream.WriteAsync(successMessage);

                        }
                        else
                        {
                            var failureMessage = new ServerMessage
                            {
                                ServerError = new ServerMessageError
                                {
                                    ErrorMessage = "UserId cannot be empty."
                                }
                            };
                            await responseStream.WriteAsync(failureMessage);
                        }

                        break;
                    case UserMessage.ContentOneofCase.ChatConnect:
                        var chatConnectMessage = userMessage.ChatConnect;

                        if (string.IsNullOrEmpty(chatConnectMessage.UserId) ||
                            string.IsNullOrEmpty(chatConnectMessage.ChatRoomId))
                        {
                            var failureMessage = new ServerMessage
                            {
                                ServerError = new ServerMessageError
                                {
                                    ErrorMessage = "UserId and ChatRoomId cannot be empty."
                                }
                            };
                            await responseStream.WriteAsync(failureMessage);
                        }
                        else
                        {
                            var result = await _chatRoomService.AddUserToChatRoom(chatConnectMessage.ChatRoomId,
                                  new Guid(chatConnectMessage.UserId), responseStream);
                            chatSessionRoomId = result.Item2;

                            var successMessage = new ServerMessage
                            {
                                SuccessMessage = new ServerMessageSuccess
                                {
                                    SuccessMessage = $"{result.Item1.UserName} joined to chat."
                                }
                            };

                            await responseStream.WriteAsync(successMessage);
                        }
                        break;
                    case UserMessage.ContentOneofCase.Chat:
                        var chatMessage = userMessage.Chat;
                        if (!string.IsNullOrWhiteSpace(userSessionId) && !string.IsNullOrWhiteSpace(chatSessionRoomId))
                        {
                            await _chatRoomService.BroadcastMessageToChatRoom(chatSessionRoomId, new Guid(userSessionId),
                                chatMessage.Text);
                        }
                        break;
                    case UserMessage.ContentOneofCase.ChatDisconnect:
                        var chatDisconnectMessage = userMessage.ChatDisconnect;

                        if (!string.IsNullOrWhiteSpace(userSessionId) && !string.IsNullOrWhiteSpace(chatSessionRoomId))
                        {
                            await _chatRoomService.DeleteUserToChatRoom(new Guid(userSessionId), chatSessionRoomId);
                            chatSessionRoomId = string.Empty;
                        }
                        break;
                }
            }
        }
    }
}

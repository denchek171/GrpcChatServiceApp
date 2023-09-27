using Grpc.Core;
using System.Collections.Concurrent;
using Communication;
using Google.Protobuf;
using System.IO;
using System.Threading.Tasks;

namespace GrpcChatServiceApp.Services.СommunicationServices
{
    public class ChatRoomService
    {
        private static readonly ConcurrentDictionary<Guid, User> _chatUsers = new ConcurrentDictionary<Guid, User>();
        private static readonly ConcurrentDictionary<string, List<ChatUser>> _chatRooms = new ConcurrentDictionary<string, List<ChatUser>>();

        public async Task<UserMessage> ReadMessageWithTimeoutAsync(IAsyncStreamReader<UserMessage> requestStream, TimeSpan timeout)
        {
            CancellationTokenSource cancellationTokenSource = new();

            cancellationTokenSource.CancelAfter(timeout);

            try
            {
                bool moveNext = await requestStream.MoveNext(cancellationTokenSource.Token);

                if (moveNext == false)
                {
                    throw new Exception("connection dropped exception");
                }

                return requestStream.Current;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                throw new TimeoutException();
            }
        }

        public async Task<string> AddUserToChatUsers(string userName)
        {
            Guid userId = Guid.NewGuid();
            _chatUsers.TryAdd(userId, new User
            {
                UserName = userName
            });

            Console.WriteLine($"User with ID {userId} added to user list");

            await Task.CompletedTask;

            return userId.ToString();
        }

        public async Task DeleteUserToChatUsers(Guid userId)
        {
            if (userId != Guid.Empty)
            {
                _chatUsers.TryRemove(userId, out _);

                Console.WriteLine($"User with ID {userId} deleted from user list");
            }

            await Task.CompletedTask;
        }

        public async Task<(ChatUser, string)> AddUserToChatRoom(string chatRoomId, Guid userId, IServerStreamWriter<ServerMessage> stream)
        {

            var chatUser = new ChatUser
            {
                Id = userId,
                StreamWriter = stream,
                UserName = _chatUsers[userId].UserName
            };

            if (!_chatRooms.ContainsKey(chatRoomId))
            {

                _chatRooms[chatRoomId] = new List<ChatUser>
                {
                    chatUser
                };
            }
            else
            {
                var existingUser = _chatRooms[chatRoomId].FirstOrDefault(c => c.Id == userId);

                if (existingUser != null)
                {
                    throw new InvalidOperationException("User with the same name already exists in the chat room");
                }

                _chatRooms[chatRoomId].Add(chatUser);
            }

            await Task.CompletedTask;

            return (chatUser, chatRoomId);
        }

        public async Task DeleteUserToChatRoom(Guid userId, string chatRoomId)
        {
            if (userId != Guid.Empty)
            {
                if (_chatRooms.TryGetValue(chatRoomId, out List<ChatUser> chatRoomUsers))
                {
                 
                    var userToRemove = chatRoomUsers.FirstOrDefault(user => user.Id == userId);
                    if (userToRemove != null)
                    {
                        chatRoomUsers.Remove(userToRemove);
                        Console.WriteLine($"User with ID {userId} deleted from user list in chat room {chatRoomId}");

                        if (chatRoomUsers.Count == 0)
                        {
                            _chatRooms.TryRemove(chatRoomId, out _);
                            Console.WriteLine($"Chat room {chatRoomId} has been deleted as it became empty.");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"User with ID {userId} not found in chat room {chatRoomId}");
                    }
                }
                else
                {
                    Console.WriteLine($"Chat room with ID {chatRoomId} not found");
                }
            }

            await Task.CompletedTask;
        }

        public async Task BroadcastMessageToChatRoom(string chatRoomId, Guid userId, string text)
        {
            if (_chatRooms.TryGetValue(chatRoomId, out var room))
            {
                
                var chatMessage = new ServerMessageChat
                {
                    UserName = _chatUsers[userId].UserName,
                    Text = text
                };

                var message = new ServerMessage
                {
                    Chat = chatMessage
                };

                var tasks = new List<Task>();

                foreach (var stream in _chatRooms[chatRoomId])
                {
                    if (stream != null && stream != default)
                    {
                        tasks.Add(stream.StreamWriter.WriteAsync(message));
                    }
                }

                await Task.WhenAll(tasks);
            }
        }
    }

    public class User
    {
        public string UserName { get; set; }
    }

    public class ChatUser
    {
        public Guid Id { get; set; }
        public IServerStreamWriter<ServerMessage> StreamWriter { get; set; }
        public string UserName { get; set; }
    }
}

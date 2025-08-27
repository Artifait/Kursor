using KursorServer.Services;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace KursorServer.Hubs
{
    public class KursorHub : Hub
    {
        private readonly RoomManager _rooms;
        private readonly CoordsDispatcherService _dispatcher;

        public KursorHub(RoomManager rooms, CoordsDispatcherService dispatcher)
        {
            _rooms = rooms;
            _dispatcher = dispatcher;
        }

        public async Task JoinRoom(string token, string role)
        {
            if (!_rooms.TryGetByToken(token, out var room))
            {
                await Clients.Caller.SendAsync("RoomNotFound");
                return;
            }

            if (role == "teacher")
            {
                _rooms.SetTeacher(token, Context.ConnectionId);
                await Groups.AddToGroupAsync(Context.ConnectionId, token);
                await Clients.Caller.SendAsync("JoinedAsTeacher", new
                {
                    token = token,
                    aspectW = room.AspectW,
                    aspectH = room.AspectH
                });
            }
            else if (role == "student")
            {
                _rooms.SetStudent(token, Context.ConnectionId);
                await Groups.AddToGroupAsync(Context.ConnectionId, token);
                await Clients.Caller.SendAsync("JoinedAsStudent", new { token = token });

                if (!string.IsNullOrEmpty(room.TeacherConnectionId))
                {
                    await Clients.Client(room.TeacherConnectionId).SendAsync("StudentConnected");
                }
            }
        }

        public Task SendCoords(string token, float nx, float ny)
        {
            if (_rooms.TryGetByToken(token, out var r))
            {
                r.LastActivityUtc = System.DateTime.UtcNow;
                int qx = (int)Math.Clamp(nx * 65535f, 0, 65535);
                int qy = (int)Math.Clamp(ny * 65535f, 0, 65535);
                _dispatcher.Push(token, (ushort)qx, (ushort)qy);
            }
            return Task.CompletedTask;
        }

        public Task Heartbeat()
        {
            _rooms.UpdateActivityByConnection(Context.ConnectionId);
            return Task.CompletedTask;
        }

        public async Task<bool> RemoveRoom(string token)
        {
            if (!_rooms.TryGetByToken(token, out var room)) return false;
            if (room.TeacherConnectionId != Context.ConnectionId) return false;

            if (!string.IsNullOrEmpty(room.StudentConnectionId))
            {
                await Clients.Client(room.StudentConnectionId).SendAsync("RoomRemoved");
            }
            _rooms.RemoveByToken(token);
            return true;
        }

        // SignalR forwarding for WebRTC signaling
        public Task SendOffer(string token, string sdp) => ForwardToPeer(token, "ReceiveOffer", sdp);
        public Task SendAnswer(string token, string sdp) => ForwardToPeer(token, "ReceiveAnswer", sdp);
        public Task SendIceCandidate(string token, string candidate) => ForwardToPeer(token, "ReceiveIce", candidate);

        private Task ForwardToPeer(string token, string method, object data)
        {
            if (!_rooms.TryGetByToken(token, out var room)) return Task.CompletedTask;
            var peer = (Context.ConnectionId == room.TeacherConnectionId) ? room.StudentConnectionId : room.TeacherConnectionId;
            if (string.IsNullOrEmpty(peer)) return Task.CompletedTask;
            return Clients.Client(peer).SendAsync(method, data);
        }

        public override async Task OnDisconnectedAsync(System.Exception? exception)
        {
            var disconnectedConn = Context.ConnectionId;
            if (_rooms.TryGetByConnection(disconnectedConn, out var found, out var role))
            {
                var peerId = role == "teacher" ? found.StudentConnectionId : found.TeacherConnectionId;
                if (!string.IsNullOrEmpty(peerId))
                {
                    try { await Clients.Client(peerId).SendAsync("PeerDisconnected"); }
                    catch { }
                }
                _rooms.RemoveByConnection(disconnectedConn);
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}

using KursorServer.Services;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
namespace KursorServer.Hubs
{
    public class KursorHub : Hub
    {
        private readonly RoomManager _rooms;
        public KursorHub(RoomManager rooms) { _rooms = rooms; }
        /// <summary>
        /// Клиент присоединяется к комнате. role: "teacher" или "student".
        /// </summary>
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
                await Clients.Caller.SendAsync("JoinedAsStudent", new
                {
                    token = token
                });
                // Уведомляем учителя если он онлайн
                
            if (!string.IsNullOrEmpty(room.TeacherConnectionId))
                {
                    await
                    Clients.Client(room.TeacherConnectionId).SendAsync("StudentConnected");
                }
            }
        }
        /// <summary>
        /// Отправка координат от учителя. Ожидаются нормализованные координаты в диапазоне[0..1].
        /// Передаём только студенту (если он подключён) чтобы избежать эха.
        /// Используем float для уменьшения размера пакета (MessagePack поддерживает float).
        /// </summary>
        public Task SendCoords(string token, float nx, float ny)
        {
            if (_rooms.TryGetByToken(token, out var r)) r.LastActivityUtc =
            System.DateTime.UtcNow;
            // Отправляем только студенту — если его нет, ничего не делаем
            if (_rooms.TryGetByToken(token, out var room) && !
            string.IsNullOrEmpty(room.StudentConnectionId))
            {
                return
                Clients.Client(room.StudentConnectionId).SendAsync("CoordsUpdated", nx, ny);
            }
            return Task.CompletedTask;
        }
        /// <summary>
        /// Периодический heartbeat/keepalive от клиентов — используется для отметки активности комнаты.
        /// </summary>
        public Task Heartbeat()
        {
            _rooms.UpdateActivityByConnection(Context.ConnectionId);
            return Task.CompletedTask;
        }
        /// <summary>
        /// Запрашивает удаление комнаты (только учитель может удалить свою комнату).
        /// После удаления уведомляем студента (при подключении) и очищаем состояние.
        /// </summary>
        public async Task<bool> RemoveRoom(string token)
        {
            if (!_rooms.TryGetByToken(token, out var room)) return false;
            // проверяем, что текущий connectionId соответствует учителю
            if (room.TeacherConnectionId != Context.ConnectionId) 
                return false;

            // уведомляем студента, если он онлайн
            if (!string.IsNullOrEmpty(room.StudentConnectionId))
            {
                await
                Clients.Client(room.StudentConnectionId).SendAsync("RoomRemoved");
            }
            _rooms.RemoveByToken(token);
            return true;
        }
        /// <summary>
        /// При отключении клиента — находим его комнату, уведомляем пира и очищаем состояние.
        /// </summary>
        public override async Task OnDisconnectedAsync(System.Exception? exception)
        {
            // Найдём комнату, где connectionId совпадает с учителем или студентом
            var disconnectedConn = Context.ConnectionId;
            KursorServer.Models.Room? found = null;
            string? role = null;
            foreach (var r in _rooms.AllRooms)
            {
                if (r.TeacherConnectionId == disconnectedConn)
                {
                    found = r; role = "teacher"; break;
                }
                if (r.StudentConnectionId == disconnectedConn)
                {
                    found = r; role = "student"; break;
                }
            }
            if (found != null)
            {
                // идентифицируем peer (другую сторону) и уведомим её
                var peerId = role == "teacher" ? found.StudentConnectionId :
                found.TeacherConnectionId;
                if (!string.IsNullOrEmpty(peerId))
                {
                    try
                    {
                        await
                        Clients.Client(peerId).SendAsync("PeerDisconnected");
                    }

                catch { /* Игнорируем ошибки при попытке уведомить отключенного клиента */
                    }
                }
                // очищаем mappings/комнату
                _rooms.RemoveByConnection(disconnectedConn);
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}

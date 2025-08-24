using KursorServer.Models;
using System.Collections.Concurrent;


namespace KursorServer.Services
{
    public class RoomManager
    {
        private readonly ConcurrentDictionary<string, Room> _roomsByToken = new();
        private readonly ConcurrentDictionary<string, (string token, string role)> _connToRoom = new();


        public Room CreateRoom(double aspectW = 16, double aspectH = 9)
        {
            var r = new Room { AspectW = aspectW, AspectH = aspectH };
            _roomsByToken[r.Token] = r;
            return r;
        }


        public bool TryGetByToken(string token, out Room? room) => _roomsByToken.TryGetValue(token, out room);


        public IEnumerable<Room> AllRooms => _roomsByToken.Values;


        public void SetTeacher(string token, string connectionId)
        {
            if (_roomsByToken.TryGetValue(token, out var r))
            {
                r.TeacherConnectionId = connectionId;
                r.LastActivityUtc = DateTime.UtcNow;
                _connToRoom[connectionId] = (token, "teacher");
            }
        }


        public void SetStudent(string token, string connectionId)
        {
            if (_roomsByToken.TryGetValue(token, out var r))
            {
                r.StudentConnectionId = connectionId;
                r.LastActivityUtc = DateTime.UtcNow;
                _connToRoom[connectionId] = (token, "student");
            }
        }


        public void UpdateActivityByConnection(string connectionId)
        {
            if (_connToRoom.TryGetValue(connectionId, out var info) && _roomsByToken.TryGetValue(info.token, out var r))
            {
                r.LastActivityUtc = DateTime.UtcNow;
            }
        }


        public void RemoveByToken(string token)
        {
            _roomsByToken.TryRemove(token, out _);
        }


        public void RemoveByConnection(string connectionId)
        {
            if (_connToRoom.TryRemove(connectionId, out var info))
            {
                if (_roomsByToken.TryGetValue(info.token, out var r))
                {
                    if (info.role == "teacher") r.TeacherConnectionId = null;
                    if (info.role == "student") r.StudentConnectionId = null;
                    // если обе null -> удаляем комнату
                    if (r.TeacherConnectionId == null && r.StudentConnectionId == null)
                        _roomsByToken.TryRemove(info.token, out _);
                }
            }
        }
    }
}

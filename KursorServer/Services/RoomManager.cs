using KursorServer.Models;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace KursorServer.Services
{
    public class RoomManager
    {
        private readonly ConcurrentDictionary<string, Room> _roomsByToken = new();
        private readonly ConcurrentDictionary<string, (string token, string role)> _connToRoom = new();

        // Генерация URL-safe токена (base64url, без '='). 16 байт -> ~22 символа
        private static string GenerateToken(int bytes = 16)
        {
            var buffer = RandomNumberGenerator.GetBytes(bytes);
            var b64 = Convert.ToBase64String(buffer)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            return b64;
        }

        public Room CreateRoom(double aspectW = 16, double aspectH = 9)
        {
            string token;
            do
            {
                token = GenerateToken();
            } while (_roomsByToken.ContainsKey(token));

            var r = new Room(token) { AspectW = aspectW, AspectH = aspectH };
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
                    if (r.TeacherConnectionId == null && r.StudentConnectionId == null)
                        _roomsByToken.TryRemove(info.token, out _);
                }
            }
        }

        // Быстрый lookup по connectionId
        public bool TryGetByConnection(string connectionId, out Room? room, out string? role)
        {
            room = null; role = null;
            if (_connToRoom.TryGetValue(connectionId, out var info))
            {
                if (_roomsByToken.TryGetValue(info.token, out var r))
                {
                    room = r;
                    role = info.role;
                    return true;
                }
            }
            return false;
        }

        public List<string> GetInactiveTokens(DateTime threshold)
        {
            var list = new List<string>();
            foreach (var kv in _roomsByToken)
            {
                if (kv.Value.LastActivityUtc < threshold) list.Add(kv.Key);
            }
            return list;
        }
    }
}

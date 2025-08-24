using KursorServer.Models;
using KursorServer.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KursorServer.Services
{
    public class RoomManager
    {
        private readonly ConcurrentDictionary<string, Room> _roomsById = new();
        private readonly ConcurrentDictionary<Guid, string> _tokenToRoom = new();
        private readonly TimeSpan _roomTtl;
        private readonly Timer _cleanupTimer;

        public RoomManager(TimeSpan roomTtl)
        {
            _roomTtl = roomTtl;
            // Cleanup every 30s
            _cleanupTimer = new Timer(_ => Cleanup(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        public Room CreateRoom(string password)
        {
            var id = IdGenerator.ShortId(6);
            var salt = PasswordHasher.GenerateSalt();
            var hash = PasswordHasher.HashPassword(password, salt);
            var teacherToken = Guid.NewGuid();
            var studentToken = Guid.NewGuid();

            var room = new Room
            {
                Id = id,
                PasswordHash = hash,
                Salt = salt,
                Created = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                TeacherToken = teacherToken,
                StudentToken = studentToken
            };

            _roomsById[id] = room;
            _tokenToRoom[teacherToken] = id;
            _tokenToRoom[studentToken] = id;
            return room;
        }

        public (bool ok, Guid token, string? message) JoinRoomAsStudent(string id, string password)
        {
            if (!_roomsById.TryGetValue(id, out var room)) return (false, Guid.Empty, "Room not found");
            if (!PasswordHasher.VerifyPassword(password, room.PasswordHash, room.Salt)) return (false, Guid.Empty, "Invalid password");

            // Return existing student token
            room.LastActivity = DateTime.UtcNow;
            return (true, room.StudentToken, null);
        }

        public bool TryGetRoomByToken(Guid token, out Room? room)
        {
            room = null;
            if (!_tokenToRoom.TryGetValue(token, out var id)) return false;
            return _roomsById.TryGetValue(id, out room);
        }

        public bool TouchByToken(Guid token)
        {
            if (!TryGetRoomByToken(token, out var room) || room == null) return false;
            room.LastActivity = DateTime.UtcNow;
            return true;
        }

        public bool UpdateEndpoint(Guid token, System.Net.IPEndPoint endpoint, bool isTeacher)
        {
            if (!TryGetRoomByToken(token, out var room) || room == null) return false;
            if (isTeacher) room.TeacherEndpoint = endpoint;
            else room.StudentEndpoint = endpoint;
            room.LastActivity = DateTime.UtcNow;
            return true;
        }

        public void MarkActivity(string roomId)
        {
            if (_roomsById.TryGetValue(roomId, out var r)) r.LastActivity = DateTime.UtcNow;
        }

        private void Cleanup()
        {
            var now = DateTime.UtcNow;
            foreach (var kv in _roomsById)
            {
                var room = kv.Value;
                // 1) If created > TTL and nobody joined (teacher or student endpoint missing) -> delete
                if (now - room.Created > _roomTtl && (room.TeacherEndpoint == null || room.StudentEndpoint == null))
                {
                    RemoveRoom(room);
                    continue;
                }

                // 2) If completely idle (no activity) > TTL -> delete
                if (now - room.LastActivity > _roomTtl)
                {
                    RemoveRoom(room);
                }
            }
        }

        private void RemoveRoom(Room room)
        {
            _roomsById.TryRemove(room.Id, out _);
            _tokenToRoom.TryRemove(room.TeacherToken, out _);
            _tokenToRoom.TryRemove(room.StudentToken, out _);
        }

        // For debug
        public object GetSnapshot()
        {
            return _roomsById.Values.Select(r => new
            {
                r.Id,
                Created = r.Created,
                LastActivity = r.LastActivity,
                TeacherEndpoint = r.TeacherEndpoint?.ToString(),
                StudentEndpoint = r.StudentEndpoint?.ToString(),
                TeacherToken = r.TeacherToken,
                StudentToken = r.StudentToken
            }).ToArray();
        }
    }
}

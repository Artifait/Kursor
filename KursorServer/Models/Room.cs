using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace KursorServer.Models
{
    public class Room
    {
        public string Id { get; init; } = null!;
        public byte[] PasswordHash { get; set; } = null!;
        public byte[] Salt { get; set; } = null!;
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;

        // Tokens
        public Guid TeacherToken { get; set; }
        public Guid StudentToken { get; set; }

        // Last known external UDP endpoints (learned from incoming UDP packets)
        public IPEndPoint? TeacherEndpoint { get; set; }
        public IPEndPoint? StudentEndpoint { get; set; }
    }
}


namespace KursorServer.Models
{
    public class Room
    {
        public string Token { get; init; } = Guid.NewGuid().ToString("N").Substring(0, 8);
        public string? TeacherConnectionId { get; set; }
        public string? StudentConnectionId { get; set; }
        public double AspectW { get; set; } = 16;
        public double AspectH { get; set; } = 9;
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;
    }
}
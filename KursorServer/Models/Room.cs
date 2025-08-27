namespace KursorServer.Models
{
    public class Room
    {
        public string Token { get; init; }
        public string? TeacherConnectionId { get; set; }
        public string? StudentConnectionId { get; set; }
        public double AspectW { get; set; } = 16;
        public double AspectH { get; set; } = 9;
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;

        public Room(string token)
        {
            Token = token;
        }
    }
}

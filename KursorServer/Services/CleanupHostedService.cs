using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;


namespace KursorServer.Services
{
    public class CleanupHostedService : BackgroundService
    {
        private readonly RoomManager _rooms;
        public CleanupHostedService(RoomManager rooms) { _rooms = rooms; }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var threshold = System.DateTime.UtcNow.AddSeconds(-30); // комната неактивна 30s
                var toRemove = _rooms.AllRooms.Where(r => r.LastActivityUtc < threshold).Select(r => r.Token).ToList();
                foreach (var t in toRemove) _rooms.RemoveByToken(t);
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}
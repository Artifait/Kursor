using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace KursorServer.Services
{
    public class CleanupHostedService : BackgroundService
    {
        private readonly RoomManager _rooms;
        private readonly int _checkIntervalMs = 10_000; // каждые 10s
        private readonly int _inactivitySec = 600; // удалять комнаты неактивные > 600s

        public CleanupHostedService(RoomManager rooms) { _rooms = rooms; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var threshold = System.DateTime.UtcNow.AddSeconds(-_inactivitySec);
                var toRemove = _rooms.GetInactiveTokens(threshold);
                foreach (var t in toRemove) _rooms.RemoveByToken(t);
                try { await Task.Delay(_checkIntervalMs, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}

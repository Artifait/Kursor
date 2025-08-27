using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using KursorServer.Hubs;

namespace KursorServer.Services
{
    /// <summary>
    /// Буфер/диспетчер координат: хранит последний квантованный пакет по комнате и рассылает его с ограниченной частотой.
    /// Отправляет бинарное событие "CoordsBinary": 5 байт [seq][qx_hi][qx_lo][qy_hi][qy_lo].
    /// </summary>
    public class CoordsDispatcherService : BackgroundService
    {
        private readonly RoomManager _rooms;
        private readonly IHubContext<KursorHub> _hub;
        private readonly ConcurrentDictionary<string, CoordsBufferEntry> _buffers = new();

        private readonly int _intervalMs;

        public CoordsDispatcherService(RoomManager rooms, IHubContext<KursorHub> hub)
        {
            _rooms = rooms;
            _hub = hub;
            _intervalMs = 16; // ~60Hz
        }

        public void Push(string token, ushort x, ushort y)
        {
            var buf = _buffers.GetOrAdd(token, _ => new CoordsBufferEntry());
            buf.X = x;
            buf.Y = y;
            buf.Dirty = true;
            unchecked { buf.Sequence++; }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();

                foreach (var kv in _buffers)
                {
                    var token = kv.Key;
                    var entry = kv.Value;

                    if (!entry.Dirty) continue;

                    if (!_rooms.TryGetByToken(token, out var room))
                    {
                        _buffers.TryRemove(token, out _);
                        continue;
                    }

                    var student = room.StudentConnectionId;
                    if (string.IsNullOrEmpty(student)) continue;

                    var payload = new byte[5];
                    payload[0] = (byte)(entry.Sequence & 0xFF);
                    payload[1] = (byte)(entry.X >> 8);
                    payload[2] = (byte)(entry.X & 0xFF);
                    payload[3] = (byte)(entry.Y >> 8);
                    payload[4] = (byte)(entry.Y & 0xFF);

                    try
                    {
                        await _hub.Clients.Client(student).SendAsync("CoordsBinary", payload);
                    }
                    catch
                    {
                        // ignore transient
                    }

                    entry.Dirty = false;
                }

                var elapsed = (int)sw.ElapsedMilliseconds;
                var delay = Math.Max(0, _intervalMs - elapsed);
                try { await Task.Delay(delay, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        private class CoordsBufferEntry
        {
            public volatile ushort X;
            public volatile ushort Y;
            public volatile int Sequence;
            public volatile bool Dirty;
        }
    }
}

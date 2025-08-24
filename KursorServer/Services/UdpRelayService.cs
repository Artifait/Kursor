using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace KursorServer.Services
{
    public sealed class UdpRelayService : BackgroundService
    {
        private readonly RoomManager _roomManager;
        private readonly int _port;
        private readonly ILogger<UdpRelayService> _logger;
        private UdpClient? _udp;

        // Packet layout (binary, little-endian):
        // [0]   : byte type (1=keepalive, 2=cursor)
        // [1..16]: 16 bytes token GUID (Guid.ToByteArray)
        // [17..20]: uint32 seq
        // [21..28]: int64 timestamp ms
        // [29..32]: float x
        // [33..36]: float y
        private const int PacketSize = 37;

        public UdpRelayService(RoomManager roomManager, int port, ILogger<UdpRelayService> logger)
        {
            _roomManager = roomManager;
            _port = port;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _udp = new UdpClient(new IPEndPoint(IPAddress.Any, _port));
            _logger.LogInformation("UDP relay listening on {port}", _port);
            _ = Task.Run(() => LoopAsync(stoppingToken), stoppingToken);
            return Task.CompletedTask;
        }

        private async Task LoopAsync(CancellationToken ct)
        {
            if (_udp == null) return;
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var res = await _udp.ReceiveAsync(ct);
                    var buf = res.Buffer;
                    if (buf == null || buf.Length < 1) continue;

                    // Fast path: expect our compact packet; if smaller, ignore
                    if (buf.Length < 17) continue;

                    var type = buf[0];
                    // read token bytes [1..16]
                    var tokenBytes = new byte[16];
                    Array.Copy(buf, 1, tokenBytes, 0, 16);
                    var token = new Guid(tokenBytes);

                    _logger.LogDebug("Received UDP packet type={type} token={token}", type, token);
                    // Try map token to room
                    if (!_roomManager.TryGetRoomByToken(token, out var room) || room == null)
                    {
                        // unknown token -> ignore
                        continue;
                    }

                    // Update endpoint mapping when we receive any UDP from this token
                    var remoteEp = res.RemoteEndPoint;
                    bool isTeacher = token == room.TeacherToken;
                    _roomManager.UpdateEndpoint(token, remoteEp, isTeacher);

                    room.LastActivity = DateTime.UtcNow;

                    if (type == 1) // keepalive - nothing else to do
                    {
                        continue;
                    }
                    else if (type == 2)
                    {
                        // Cursor packet: forward to student (only teacher sends cursor)
                        if (token != room.TeacherToken) continue; // only teacher may send cursor packets

                        var dest = room.StudentEndpoint;
                        if (dest == null) continue; // no student yet
                                                    // Forward raw bytes as-is (fast)
                        try
                        {
                            await _udp.SendAsync(buf, buf.Length, dest);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to forward UDP to {dest}", dest);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UDP loop crashed");
            }
            finally
            {
                _udp?.Dispose();
            }
        }
    }
}

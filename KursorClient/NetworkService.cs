using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http.Json;

namespace KursorClient
{

    public sealed class NetworkService
    {
        private UdpClient? _udp;
        private IPEndPoint? _serverUdpEp;
        private readonly HttpClient _http = new();
        private bool _udpInitialized = false;
        private int _udpPort = 50000; // default, может быть перезаписан сервером

        // Binary packet lengths same as server: 37 bytes
        // [0] type (1 keepalive,2 cursor)
        // [1..16] token (16 bytes Guid)
        // [17..20] seq (uint32 LE)
        // [21..28] ts (int64 LE ms)
        // [29..32] float x
        // [33..36] float y

        // Возвращаем udpPort вместе с токеном/roomId
        public async Task<(string roomId, string teacherToken, int udpPort)> CreateRoomAsync(string serverUrl, string password)
        {
            var req = new { Password = password };
            var res = await _http.PostAsJsonAsync($"{serverUrl.TrimEnd('/')}/rooms", req);
            res.EnsureSuccessStatusCode();
            using var s = await res.Content.ReadAsStreamAsync();
            var doc = await JsonDocument.ParseAsync(s);
            var roomId = doc.RootElement.GetProperty("roomId").GetString()!;
            var teacherToken = doc.RootElement.GetProperty("teacherToken").GetString()!;
            var udpPort = doc.RootElement.TryGetProperty("udpPort", out var p) ? p.GetInt32() : 50000;
            _udpPort = udpPort;
            return (roomId, teacherToken, udpPort);
        }

        public async Task<(string studentToken, int udpPort)> JoinRoomAsync(string serverUrl, string roomId, string password)
        {
            var req = new { Password = password };
            var res = await _http.PostAsJsonAsync($"{serverUrl.TrimEnd('/')}/rooms/{roomId}/join", req);
            res.EnsureSuccessStatusCode();
            using var s = await res.Content.ReadAsStreamAsync();
            var doc = await JsonDocument.ParseAsync(s);
            var studentToken = doc.RootElement.GetProperty("studentToken").GetString()!;
            var udpPort = doc.RootElement.TryGetProperty("udpPort", out var p) ? p.GetInt32() : 50000;
            _udpPort = udpPort;
            return (studentToken, udpPort);
        }

        public async Task InitUdpAsync(string serverUrl)
        {
            if (_udpInitialized) return;

            var uri = new Uri(serverUrl);
            var host = uri.Host;

            // Попытка резолва IPv4 адреса
            IPAddress? addr = null;
            try
            {
                var addrs = await Dns.GetHostAddressesAsync(host);
                addr = Array.Find(addrs, a => a.AddressFamily == AddressFamily.InterNetwork) ?? addrs.FirstOrDefault();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"DNS lookup failed for host '{host}': {ex.Message}", ex);
            }

            if (addr == null)
            {
                throw new InvalidOperationException($"Cannot resolve host '{host}' to any IP address.");
            }

            _serverUdpEp = new IPEndPoint(addr, _udpPort);

            // Создаем UdpClient (без NoDelay). Используем привязку к ephemeral порту (0)
            _udp = new UdpClient(0);
            // Для производительности можно включить ReuseAddress на уровне сокета
            _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            _udpInitialized = true;
        }

        public async Task SendKeepaliveAsync(Guid token, bool isTeacher)
        {
            if (!_udpInitialized || _udp == null || _serverUdpEp == null) throw new InvalidOperationException("UDP not initialized. Call InitUdpAsync first.");
            var buf = new byte[37];
            buf[0] = 1; // keepalive
            var t = token.ToByteArray();
            Array.Copy(t, 0, buf, 1, 16);
            await _udp!.SendAsync(buf, buf.Length, _serverUdpEp);
        }

        public async Task SendCursorAsync(Guid teacherToken, uint seq, float x, float y)
        {
            if (!_udpInitialized || _udp == null || _serverUdpEp == null) throw new InvalidOperationException("UDP not initialized. Call InitUdpAsync first.");
            var buf = new byte[37];
            buf[0] = 2;
            var t = teacherToken.ToByteArray();
            Array.Copy(t, 0, buf, 1, 16);
            BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(17, 4), seq);
            BinaryPrimitives.WriteInt64LittleEndian(buf.AsSpan(21, 8), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            BinaryPrimitives.WriteSingleLittleEndian(buf.AsSpan(29, 4), x);
            BinaryPrimitives.WriteSingleLittleEndian(buf.AsSpan(33, 4), y);
            await _udp!.SendAsync(buf, buf.Length, _serverUdpEp);
        }

        public async IAsyncEnumerable<(float x, float y)> ReceiveCursorPacketsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            if (!_udpInitialized || _udp == null) throw new InvalidOperationException("UDP not initialized. Call InitUdpAsync first.");
            var client = _udp!;
            while (!ct.IsCancellationRequested)
            {
                UdpReceiveResult r;
                try
                {
                    r = await client.ReceiveAsync(ct);
                }
                catch (OperationCanceledException) { yield break; }
                catch (Exception) { continue; }

                var b = r.Buffer;
                if (b.Length < 37) continue;
                if (b[0] != 2) continue;
                var x = BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(29, 4));
                var y = BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(33, 4));
                if (x < 0f) x = 0f; if (x > 1f) x = 1f;
                if (y < 0f) y = 0f; if (y > 1f) y = 1f;
                yield return (x, y);
            }
        }
    }
}

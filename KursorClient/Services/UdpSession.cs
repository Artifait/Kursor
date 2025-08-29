using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KursorClient.Services;

public class UdpSession : IDisposable
{
    private readonly UdpClient _udp;
    public IPEndPoint ServerEndpoint { get; }
    private CancellationTokenSource? _recvCts;
    private CancellationTokenSource? _keepAliveCts;

    public UdpSession(IPEndPoint serverEndpoint)
    {
        ServerEndpoint = serverEndpoint;
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
    }

    // Если requestedToken == null -> обычное CREATE_ROOM, сервер вернёт сгенерированный токен
    // Если requestedToken != null -> пытаемся перепривязать к существующей комнате (rebind).
    public async Task<string?> CreateRoomAsync(string? requestedToken = null, int timeoutMs = 2000)
    {
        byte[] req;
        if (string.IsNullOrEmpty(requestedToken))
        {
            req = new byte[] { 0x10 };
        }
        else
        {
            var tb = Encoding.UTF8.GetBytes(requestedToken);
            req = new byte[1 + tb.Length];
            req[0] = 0x10;
            Buffer.BlockCopy(tb, 0, req, 1, tb.Length);
        }
        await _udp.SendAsync(req, req.Length, ServerEndpoint);

        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            var res = await _udp.ReceiveAsync();
            if (res.Buffer.Length > 0 && res.Buffer[0] == 0x11)
            {
                var token = Encoding.UTF8.GetString(res.Buffer, 1, res.Buffer.Length - 1);
                return token;
            }
        }
        return null;
    }

    public async Task<bool> JoinRoomAsync(string token, int timeoutMs = 2000)
    {
        var tb = Encoding.UTF8.GetBytes(token);
        var req = new byte[1 + tb.Length];
        req[0] = 0x20;
        Buffer.BlockCopy(tb, 0, req, 1, tb.Length);
        await _udp.SendAsync(req, req.Length, ServerEndpoint);

        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            var res = await _udp.ReceiveAsync();
            if (res.Buffer.Length >= 2 && res.Buffer[0] == 0x21)
            {
                return res.Buffer[1] == 0x01;
            }
        }
        return false;
    }

    public Task SendCursorAsync(ushort nx, ushort ny)
    {
        var payload = new byte[1 + 4];
        payload[0] = 0x30;
        payload[1] = (byte)(nx >> 8);
        payload[2] = (byte)(nx & 0xFF);
        payload[3] = (byte)(ny >> 8);
        payload[4] = (byte)(ny & 0xFF);
        return _udp.SendAsync(payload, payload.Length, ServerEndpoint);
    }

    public Task SendLeaveAsync() => _udp.SendAsync(new byte[] { 0x50 }, 1, ServerEndpoint);

    public Task SendPingAsync() => _udp.SendAsync(new byte[] { 0x40 }, 1, ServerEndpoint);

    public void StartKeepAlive(int intervalMs = 3000)
    {
        StopKeepAlive();
        _keepAliveCts = new CancellationTokenSource();
        var ct = _keepAliveCts.Token;
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try { await SendPingAsync(); }
                catch { /* ignore */ }
                await Task.Delay(intervalMs, ct).ContinueWith(_ => { });
            }
        }, ct);
    }

    public void StopKeepAlive()
    {
        try { _keepAliveCts?.Cancel(); } catch { }
        _keepAliveCts = null;
    }

    public void StartReceiving(Func<byte[], IPEndPoint, Task> onMessage)
    {
        _recvCts = new CancellationTokenSource();
        var ct = _recvCts.Token;
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var res = await _udp.ReceiveAsync();
                    _ = onMessage(res.Buffer, res.RemoteEndPoint);
                }
                catch (ObjectDisposedException) { break; }
                catch { await Task.Delay(10); }
            }
        }, ct);
    }

    public void StopReceiving()
    {
        try { _recvCts?.Cancel(); } catch { }
        _recvCts = null;
    }

    public void Dispose()
    {
        try { StopKeepAlive(); StopReceiving(); } catch { }
        _udp?.Dispose();
    }
}
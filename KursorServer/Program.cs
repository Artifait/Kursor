using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Net;
using System.Text.Json;
using System.Text;

class Room
{
    public IPEndPoint? TeacherEP;
    public IPEndPoint? StudentEP;
    public DateTime LastSeenTeacher = DateTime.UtcNow;
    public DateTime LastSeenStudent = DateTime.UtcNow;
}

class Program
{
    static readonly int UDP_PORT = ReadIntFromEnv("UDP_PORT", 55555);
    static readonly int MONITOR_PORT = ReadIntFromEnv("MONITOR_PORT", 8080);

    static readonly ConcurrentDictionary<string, Room> rooms = new();
    static readonly ConcurrentDictionary<string, string> teacherMap = new();

    static async Task Main()
    {
        Console.WriteLine($"Kursor UDP server starting on UDP port {UDP_PORT}, monitor HTTP port {MONITOR_PORT}...");
        using var udp = new UdpClient(UDP_PORT);

        _ = Task.Run(() => StartHttpMonitorAsync(MONITOR_PORT));

        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    foreach (var kv in rooms.ToArray())
                    {
                        var token = kv.Key;
                        var room = kv.Value;
                        if ((now - room.LastSeenTeacher).TotalSeconds > 60)
                        {
                            rooms.TryRemove(token, out _);
                            Console.WriteLine($"[Cleanup] removed room {token} (teacher timeout)");
                        }
                    }
                }
                catch { }
                await Task.Delay(5000);
            }
        });

        while (true)
        {
            UdpReceiveResult res;
            try { res = await udp.ReceiveAsync(); }
            catch (Exception ex) { Console.WriteLine("Receive error: " + ex.Message); continue; }
            _ = Task.Run(() => HandlePacket(res, udp));
        }
    }

    static async Task HandlePacket(UdpReceiveResult res, UdpClient udp)
    {
        var data = res.Buffer;
        var remote = res.RemoteEndPoint;
        if (data == null || data.Length == 0) return;
        var type = data[0];

        try
        {
            switch (type)
            {
                case 0x10: // CREATE_ROOM (optionally with requested token to rebind)
                    {
                        if (data.Length == 1)
                        {
                            var token = Guid.NewGuid().ToString("N");
                            var room = new Room { TeacherEP = remote, LastSeenTeacher = DateTime.UtcNow };
                            rooms[token] = room;
                            teacherMap[remote.ToString()] = token;
                            Console.WriteLine($"CREATE_ROOM from {remote} -> token {token}");
                            var payload = new byte[1 + Encoding.UTF8.GetByteCount(token)];
                            payload[0] = 0x11;
                            Buffer.BlockCopy(Encoding.UTF8.GetBytes(token), 0, payload, 1, payload.Length - 1);
                            await udp.SendAsync(payload, payload.Length, remote);
                        }
                        else
                        {
                            var tokenReq = Encoding.UTF8.GetString(data, 1, data.Length - 1);
                            if (rooms.TryGetValue(tokenReq, out var existing))
                            {
                                foreach (var kv in teacherMap)
                                {
                                    if (kv.Value == tokenReq)
                                        teacherMap.TryRemove(kv.Key, out _);
                                }
                                existing.TeacherEP = remote;
                                existing.LastSeenTeacher = DateTime.UtcNow;
                                teacherMap[remote.ToString()] = tokenReq;
                                Console.WriteLine($"REBOUND teacher for token {tokenReq} -> {remote}");
                                var payload = new byte[1 + Encoding.UTF8.GetByteCount(tokenReq)];
                                payload[0] = 0x11;
                                Buffer.BlockCopy(Encoding.UTF8.GetBytes(tokenReq), 0, payload, 1, payload.Length - 1);
                                await udp.SendAsync(payload, payload.Length, remote);
                            }
                            else
                            {
                                var payload = new byte[] { 0x11 };
                                await udp.SendAsync(payload, payload.Length, remote);
                            }
                        }
                        break;
                    }

                case 0x20: // JOIN_ROOM
                    {
                        var token = Encoding.UTF8.GetString(data, 1, data.Length - 1);
                        Console.WriteLine($"JOIN_ROOM token={token} from {remote}");
                        if (rooms.TryGetValue(token, out var room))
                        {
                            room.StudentEP = remote;
                            room.LastSeenStudent = DateTime.UtcNow;
                            var ack = new byte[] { 0x21, 0x01 };
                            await udp.SendAsync(ack, ack.Length, remote);
                            Console.WriteLine($" -> student bound for room {token} -> {remote}");
                        }
                        else
                        {
                            var nak = new byte[] { 0x21, 0x00 };
                            await udp.SendAsync(nak, nak.Length, remote);
                            Console.WriteLine($" -> join failed (no such token)");
                        }
                        break;
                    }

                case 0x30: // CURSOR
                    {
                        if (data.Length < 5) break;
                        teacherMap.TryGetValue(remote.ToString(), out var token);
                        if (token == null) break;
                        if (!rooms.TryGetValue(token, out var room)) break;
                        room.LastSeenTeacher = DateTime.UtcNow;
                        if (room.StudentEP != null)
                        {
                            await udp.SendAsync(data, data.Length, room.StudentEP);
                        }
                        break;
                    }

                case 0x40: // PING
                    {
                        if (teacherMap.TryGetValue(remote.ToString(), out var tkn))
                        {
                            if (rooms.TryGetValue(tkn, out var r)) r.LastSeenTeacher = DateTime.UtcNow;
                        }
                        else
                        {
                            foreach (var kv in rooms)
                            {
                                if (kv.Value.StudentEP?.ToString() == remote.ToString())
                                {
                                    kv.Value.LastSeenStudent = DateTime.UtcNow;
                                    break;
                                }
                            }
                        }
                        break;
                    }

                case 0x50: // LEAVE_ROOM
                    {
                        if (teacherMap.TryRemove(remote.ToString(), out var t))
                        {
                            if (rooms.TryGetValue(t, out var r))
                            {
                                if (r.StudentEP != null)
                                {
                                    var closed = new byte[] { 0x60 }; // ROOM_CLOSED
                                    await udp.SendAsync(closed, closed.Length, r.StudentEP);
                                }
                            }
                            rooms.TryRemove(t, out _);
                            Console.WriteLine($"Room {t} removed by teacher leave");
                        }
                        else
                        {
                            foreach (var kv in rooms)
                            {
                                if (kv.Value.StudentEP?.ToString() == remote.ToString())
                                {
                                    kv.Value.StudentEP = null;
                                    Console.WriteLine($"Student {remote} left room {kv.Key}");
                                    break;
                                }
                            }
                        }
                        break;
                    }

                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("HandlePacket error: " + ex);
        }
    }

    // ------------------------------
    // Lightweight HTTP monitor implemented over TcpListener (cross-platform)
    // Responds to GET /rooms and GET / with JSON: {"rooms":N}
    // ------------------------------
    static async Task StartHttpMonitorAsync(int requestedPort)
    {
        // Helper: try to start a TcpListener on given IP/port; return started listener or null.
        TcpListener? TryStart(IPAddress addr, int port)
        {
            try
            {
                var listener = new TcpListener(addr, port);
                // На Windows/Unix по умолчанию ExclusiveAddressUse = true; мы не меняем это.
                listener.Start();
                return listener;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Monitor] bind {addr}:{port} failed: {ex.Message}");
                return null;
            }
        }

        try
        {
            TcpListener? listener = null;

            // 1) попробуем любой интерфейс (0.0.0.0) — чтобы docker -p работал
            listener = TryStart(IPAddress.Any, requestedPort);

            // 2) если не получилось, пробуем loopback
            if (listener == null)
                listener = TryStart(IPAddress.Loopback, requestedPort);

            // 3) если и это не получилось — падаём на эпемерный порт на loopback
            if (listener == null)
            {
                listener = TryStart(IPAddress.Loopback, 0);
                if (listener != null)
                {
                    var localPort = ((IPEndPoint)listener.LocalEndpoint).Port;
                    Console.WriteLine($"[Monitor] original port {requestedPort} busy — started monitor on ephemeral port {localPort} (loopback).");
                }
            }
            else
            {
                var lp = ((IPEndPoint)listener.LocalEndpoint).Port;
                Console.WriteLine($"[Monitor] started on {((IPEndPoint)listener.LocalEndpoint).Address}:{lp}");
            }

            if (listener == null)
            {
                Console.WriteLine("[Monitor] failed to start on requested port and ephemeral fallback — HTTP monitor disabled.");
                return;
            }

            // Основной loop — принимаем соединения
            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var stream = client.GetStream();
                        using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);

                        var requestLine = await reader.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(requestLine))
                        {
                            client.Close();
                            return;
                        }

                        var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var path = parts.Length >= 2 ? parts[1] : "/";

                        if ((path.StartsWith("/rooms", StringComparison.OrdinalIgnoreCase) || path == "/" || path == "/metrics")
                            && parts[0].ToUpperInvariant() == "GET")
                        {
                            var payloadObj = new { rooms = rooms.Count };
                            var json = JsonSerializer.Serialize(payloadObj);
                            var body = Encoding.UTF8.GetBytes(json);
                            var header = $"HTTP/1.1 200 OK\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
                            var headerBytes = Encoding.ASCII.GetBytes(header);
                            await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
                            await stream.WriteAsync(body, 0, body.Length);
                        }
                        else
                        {
                            var msg = "Not found";
                            var body = Encoding.UTF8.GetBytes(msg);
                            var header = $"HTTP/1.1 404 Not Found\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
                            var headerBytes = Encoding.ASCII.GetBytes(header);
                            await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
                            await stream.WriteAsync(body, 0, body.Length);
                        }
                    }
                    catch { /* per-connection ignore */ }
                    finally
                    {
                        try { client.Close(); } catch { }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Monitor failed to start: " + ex.Message);
        }
    }

    static int ReadIntFromEnv(string name, int defaultValue)
    {
        try
        {
            var s = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(s)) return defaultValue;
            if (int.TryParse(s, out var v)) return v;
            return defaultValue;
        }
        catch { return defaultValue; }
    }
}
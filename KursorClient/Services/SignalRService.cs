using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.MixedReality.WebRTC;

namespace KursorClient.Services
{
    public class SignalRService : IDisposable
    {
        private readonly HubConnection _conn;
        private readonly string _serverUrl;
        private readonly CancellationTokenSource _cts = new();
        private Task? _heartbeatTask;
        private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(10);

        public event Action<double, double>? CoordsReceived;
        public event Action? StudentConnected;
        public event Action? RoomNotFound;
        public event Action? PeerDisconnected;
        public event Action? RoomRemoved;

        // signaling events
        public event Action<string>? OfferReceived;
        public event Action<string>? AnswerReceived;
        public event Action<IceCandidate>? IceCandidateReceived;

        public SignalRService(string serverUrl)
        {
            _serverUrl = serverUrl.TrimEnd('/');

            _conn = new HubConnectionBuilder()
                .WithUrl($"{_serverUrl}/kursorHub", options =>
                {
                    options.Transports = HttpTransportType.WebSockets;
                })
                .AddMessagePackProtocol()
                .WithAutomaticReconnect()
                .Build();

            _conn.On<double, double>("CoordsUpdated", (x, y) => CoordsReceived?.Invoke(x, y));

            _conn.On<byte[]>("CoordsBinary", bytes =>
            {
                try
                {
                    if (bytes == null) return;
                    int offset = 0;
                    if (bytes.Length == 5) offset = 1;
                    else if (bytes.Length != 4) return;
                    ushort qx = (ushort)((bytes[offset] << 8) | bytes[offset + 1]);
                    ushort qy = (ushort)((bytes[offset + 2] << 8) | bytes[offset + 3]);
                    double nx = qx / 65535.0;
                    double ny = qy / 65535.0;
                    CoordsReceived?.Invoke(nx, ny);
                }
                catch { }
            });

            _conn.On("StudentConnected", () => StudentConnected?.Invoke());
            _conn.On("RoomNotFound", () => RoomNotFound?.Invoke());
            _conn.On("PeerDisconnected", () => PeerDisconnected?.Invoke());
            _conn.On("RoomRemoved", () => RoomRemoved?.Invoke());

            // signaling
            _conn.On<string>("ReceiveOffer", sdp => OfferReceived?.Invoke(sdp));
            _conn.On<string>("ReceiveAnswer", sdp => AnswerReceived?.Invoke(sdp));
            _conn.On<IceCandidate>("ReceiveIce", candidate => IceCandidateReceived?.Invoke(candidate));

            _conn.Reconnecting += error => Task.CompletedTask;
            _conn.Reconnected += connectionId => Task.CompletedTask;
        }

        public async Task StartAsync()
        {
            await _conn.StartAsync();
            _heartbeatTask = Task.Run(HeartbeatLoopAsync, _cts.Token);
        }

        public async Task StopAsync()
        {
            try
            {
                _cts.Cancel();
                if (_heartbeatTask != null)
                    await Task.WhenAny(_heartbeatTask, Task.Delay(500));
                await _conn.StopAsync();
            }
            catch { }
        }

        public Task JoinRoomAsync(string token, string role) =>
            _conn.InvokeAsync("JoinRoom", token, role);

        public Task SendCoordsAsync(string token, float nx, float ny) =>
            _conn.InvokeAsync("SendCoords", token, nx, ny);

        public Task SendCoordsAsync(string token, double nx, double ny) =>
            _conn.InvokeAsync("SendCoords", token, nx, ny);

        public Task HeartbeatAsync() => _conn.InvokeAsync("Heartbeat");

        // signaling wrappers
        public Task SendOffer(string token, string sdp) => _conn.InvokeAsync("SendOffer", token, sdp);
        public Task SendAnswer(string token, string sdp) => _conn.InvokeAsync("SendAnswer", token, sdp);
        public Task SendIceCandidate(string token, string candidate) => _conn.InvokeAsync("SendIceCandidate", token, candidate);

        private async Task HeartbeatLoopAsync()
        {
            var ct = _cts.Token;
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        if (_conn.State == HubConnectionState.Connected)
                        {
                            await HeartbeatAsync();
                        }
                    }
                    catch { }
                    await Task.Delay(_heartbeatInterval, ct);
                }
            }
            catch (OperationCanceledException) { }
        }

        public void Dispose()
        {
            _cts.Cancel();
            try
            {
                _conn.StopAsync().Wait(500);
                _conn.DisposeAsync().AsTask().Wait(500);
            }
            catch { }
            _cts.Dispose();
        }
    }
}

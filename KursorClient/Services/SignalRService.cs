using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Protocol;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
namespace KursorClient.Services
{
    public class SignalRService : IDisposable
    {
        private readonly HubConnection _conn;
        private readonly string _serverUrl;
        private readonly CancellationTokenSource _cts = new();
        public event Action<double, double>? CoordsReceived;
        public event Action? StudentConnected;
        public event Action? RoomNotFound;
        public SignalRService(string serverUrl)
        {
            _serverUrl = serverUrl.TrimEnd('/');
            _conn = new HubConnectionBuilder()
            .WithUrl($"{_serverUrl}/kursorHub")
            .AddMessagePackProtocol()
            .WithAutomaticReconnect()
            .Build();
            // Message handlers
            _conn.On<double, double>("CoordsUpdated", (x, y) =>
            CoordsReceived?.Invoke(x, y));
            _conn.On("StudentConnected", () => StudentConnected?.Invoke());
            _conn.On("RoomNotFound", () => RoomNotFound?.Invoke());
        }
        public Task StartAsync() => _conn.StartAsync();
        public Task StopAsync() => _conn.StopAsync();
        public Task JoinRoomAsync(string token, string role) =>
        _conn.InvokeAsync("JoinRoom", token, role);
        public Task SendCoordsAsync(string token, double nx, double ny) =>
        _conn.InvokeAsync("SendCoords", token, nx, ny);
        public Task HeartbeatAsync() => _conn.InvokeAsync("Heartbeat");
        public void Dispose()
        {
            _cts.Cancel();
            _conn.DisposeAsync().AsTask().Wait(500);
            _cts.Dispose();
        }
    }
}
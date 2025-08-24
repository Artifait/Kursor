using System.Windows;
using KursorClient.Services;
using KursorClient.Utils;
using System;
namespace KursorClient.Windows
{
    public partial class StudentSetupWindow : Window
    {
        private SignalRService? _signalR;
        private OverlayWindow? _overlay;
        private StudentCenterWindow? _center;
        public StudentSetupWindow() { InitializeComponent(); }
        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            var tokenOrLink = TokenBox.Text.Trim();
            if (string.IsNullOrEmpty(tokenOrLink))
            {
                MessageBox.Show("Введите токен или ссылку"); return;
            }
            // извлечь токен из ссылки если нужно
            var token = tokenOrLink;
            if (tokenOrLink.Contains('/')) token =
            tokenOrLink.TrimEnd('/').Split('/')[^1];
            var server = ConfigReader.ReadServerUrl();
            _signalR = new SignalRService(server);
            _signalR.CoordsReceived += OnCoords;
            _signalR.StudentConnected += () => { /* not used */ };
            await _signalR.StartAsync();
            await _signalR.JoinRoomAsync(token, "student");
            // открыть центр окно и overlay
            _overlay = new OverlayWindow();
            _overlay.Show();
            _center = new StudentCenterWindow(_signalR, token, _overlay);
            _center.Show();
            this.Close();
        }
        private void OnCoords(double nx, double ny)
        {
            Application.Current.Dispatcher.Invoke(() =>
            _overlay?.SetTargetNormalized(nx, ny));
        }
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            var main = new MainWindow();
            main.Show();
            this.Close();
        }
    }
}
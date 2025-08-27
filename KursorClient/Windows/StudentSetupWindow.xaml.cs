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
        private WebrtcStudent? _webrtcStudent;

        public StudentSetupWindow() { InitializeComponent(); }

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            var tokenOrLink = TokenBox.Text.Trim();
            if (string.IsNullOrEmpty(tokenOrLink))
            {
                MessageBox.Show("Введите токен или ссылку"); return;
            }
            var token = tokenOrLink;
            if (tokenOrLink.Contains('/')) token = tokenOrLink.TrimEnd('/').Split('/')[^1];
            var server = ConfigReader.ReadServerUrl();
            _signalR = new SignalRService(server);

            _signalR.CoordsReceived += OnCoords;
            _signalR.StudentConnected += () => { };
            _signalR.PeerDisconnected += () => { };

            await _signalR.StartAsync();
            await _signalR.JoinRoomAsync(token, "student");

            _overlay = new OverlayWindow();
            _overlay.Show();
            _center = new StudentCenterWindow(_signalR, token, _overlay);
            _center.Show();

            try
            {
                throw new NotImplementedException();
                _webrtcStudent = new WebrtcStudent(_signalR, token, (nx, ny) => _overlay?.SetTargetNormalized(nx, ny));
                await _webrtcStudent.InitializeAsync();
            }
            catch
            {
                _webrtcStudent = null;
            }

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

using System.Windows;
using KursorClient.Services;
namespace KursorClient.Windows
{
    public partial class StudentCenterWindow : Window
    {
        private readonly SignalRService _signalR;
        private readonly string _token;
        private readonly OverlayWindow _overlay;
        public StudentCenterWindow(SignalRService signalR, string token, OverlayWindow overlay)
        {
            InitializeComponent();
            _signalR = signalR;
            _token = token;
            _overlay = overlay;
        }
        private async void OnExit(object sender, RoutedEventArgs e)
        {
            try { await _signalR.StopAsync(); } catch { }
            _overlay.Close();
            var main = new MainWindow();
            main.Show();
            this.Close();
        }
    }
}

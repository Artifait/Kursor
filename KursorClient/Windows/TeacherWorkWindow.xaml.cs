using KursorClient.Services;
using System;
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Threading;
namespace KursorClient.Windows
{
    public partial class TeacherWorkWindow : Window
    {
        private readonly SignalRService _signalR;
        private readonly string _token;
        // для rate-limiting отправки
        private double _lastNx = -1, _lastNy = -1;
        private double _pendingNx = -1, _pendingNy = -1;
        private readonly Timer _sendTimer;
        private readonly int _hz = 60; // target frequency
        public TeacherWorkWindow(SignalRService signalR, string token)
        {
            InitializeComponent();
            _signalR = signalR;
            _token = token;
            _sendTimer = new Timer(async _ => await FlushAsync(), null, Timeout.Infinite, Timeout.Infinite);
            // запускаем таймер при показе
            this.Loaded += (s, e) => _sendTimer.Change(0, 1000 / _hz);
            this.Closed += (s, e) => _sendTimer.Dispose();
        }
        private void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var p = e.GetPosition(PreviewCanvas);
            var w = PreviewCanvas.ActualWidth;
            var h = PreviewCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;
            var nx = Math.Clamp(p.X / w, 0.0, 1.0);
            var ny = Math.Clamp(p.Y / h, 0.0, 1.0);
            // заменяем pending координаты — таймер отправит их с частотой hz
            _pendingNx = nx; _pendingNy = ny;
        }

        private void PreviewCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private async Task FlushAsync()
        {
            try
            {
                var nx = _pendingNx; var ny = _pendingNy;
                if (nx < 0 || ny < 0) return; // нет новых
                                              // небольшая фильтрация: если почти не изменилось — можно неслать
                if (Math.Abs(nx - _lastNx) < 0.001 && Math.Abs(ny - _lastNy) < 0.001)
                    return;

                _lastNx = nx; _lastNy = ny;
                await _signalR.SendCoordsAsync(_token, nx, ny);
            }
            catch { /* ignore transient errors */ }
        }
        private void OnFinishClick(object sender, RoutedEventArgs e)
        {
            // можно вызвать серверный endpoint для удаления комнаты по токену
            var main = new MainWindow();
            main.Show();
            this.Close();
        }
    }
}

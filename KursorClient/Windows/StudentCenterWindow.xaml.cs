using System.Windows;
using KursorClient.Services;
namespace KursorClient.Windows
{
    public partial class StudentCenterWindow : Window
    {
        private readonly UdpSession _session;
        private readonly string _token;
        private OverlayWindow? _overlay;

        public StudentCenterWindow(UdpSession session, string token)
        {
            InitializeComponent();
            _session = session;
            _token = token;
            Loaded += StudentCenterWindow_Loaded;
            Closed += StudentCenterWindow_Closed;
        }

        private void StudentCenterWindow_Closed(object? sender, EventArgs e)
        {
            try { _session.SendLeaveAsync().Wait(300); } catch { }
            try { _session.Dispose(); } catch { }
            try { _overlay?.Close(); } catch { }
        }

        private void StudentCenterWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // show overlay
            _overlay = new OverlayWindow();
            _overlay.Show();

            // start keepalive so server knows we're alive
            _session.StartKeepAlive(3000);

            _session.StartReceiving(async (buffer, remote) =>
            {
                if (buffer == null || buffer.Length == 0) return;

                var type = buffer[0];
                if (type == 0x30 && buffer.Length >= 5)
                {
                    ushort nx = (ushort)((buffer[1] << 8) | buffer[2]);
                    ushort ny = (ushort)((buffer[3] << 8) | buffer[4]);
                    double ndx = nx / 65535.0;
                    double ndy = ny / 65535.0;
                    await _overlay.Dispatcher.InvokeAsync(() => _overlay.SetTargetNormalized(ndx, ndy));
                }
                else if (type == 0x60) // ROOM_CLOSED
                {
                    // уведомляем пользователя, закрываем overlay и возвращаем в главное меню
                    await Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("Комната завершена учителем.");
                        try { _overlay?.Close(); } catch { }
                        var main = new MainWindow();
                        main.Show();
                        Close();
                    });
                }
            });
        }

        private void OnExit(object sender, RoutedEventArgs e)
        {
            // пользователь вышел — сообщаем серверу и возвращаем в главное меню
            _ = _session.SendLeaveAsync();
            _session.StopKeepAlive();
            try { _overlay?.Close(); } catch { }
            var main = new MainWindow();
            main.Show();
            Close();
        }
    }
}

using KursorClient.Services;
using System;
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Interop;

namespace KursorClient.Windows
{
    public partial class TeacherWorkWindow : Window
    {
        #region AddFunctForResizeWindow
        // TODO: вынести всё в конфиги
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        private const int WM_NCHITTEST = 0x0084;

        private const int RESIZE_BORDER = 14; // толщина зоны resize в px

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_NCHITTEST)
            {
                Point p = PointFromLParam(lParam);

                double width = ActualWidth;
                double height = ActualHeight;

                // Левый верхний угол
                if (p.X <= RESIZE_BORDER && p.Y <= RESIZE_BORDER) { handled = true; return (IntPtr)HTTOPLEFT; }
                // Правый верхний угол
                if (p.X >= width - RESIZE_BORDER && p.Y <= RESIZE_BORDER) { handled = true; return (IntPtr)HTTOPRIGHT; }
                // Левый нижний угол
                if (p.X <= RESIZE_BORDER && p.Y >= height - RESIZE_BORDER) { handled = true; return (IntPtr)HTBOTTOMLEFT; }
                // Правый нижний угол
                if (p.X >= width - RESIZE_BORDER && p.Y >= height - RESIZE_BORDER) { handled = true; return (IntPtr)HTBOTTOMRIGHT; }

                // Верх
                if (p.Y <= RESIZE_BORDER) { handled = true; return (IntPtr)HTTOP; }
                // Низ
                if (p.Y >= height - RESIZE_BORDER) { handled = true; return (IntPtr)HTBOTTOM; }
                // Лево
                if (p.X <= RESIZE_BORDER) { handled = true; return (IntPtr)HTLEFT; }
                // Право
                if (p.X >= width - RESIZE_BORDER) { handled = true; return (IntPtr)HTRIGHT; }
            }

            // во всех остальных случаях — НЕ перехватываем, чтобы UI оставался кликабельным
            return IntPtr.Zero;
        }


        private Point PointFromLParam(IntPtr lParam)
        {
            int x = unchecked((short)(long)lParam);
            int y = unchecked((short)((long)lParam >> 16));

            return this.PointFromScreen(new Point(x, y));
        }
        #endregion

        private readonly SignalRService _signalR;
        private readonly string _token;
        private WebrtcTeacher? _webrtcTeacher;

        private double _lastNx = -1, _lastNy = -1;
        private double _pendingNx = -1, _pendingNy = -1;
        private readonly Timer _sendTimer;
        private readonly int _hz = 60;

        public TeacherWorkWindow(SignalRService signalR, string token)
        {
            InitializeComponent();
            _signalR = signalR;
            _token = token;
            _sendTimer = new Timer(async _ => await FlushAsync(), null, Timeout.Infinite, Timeout.Infinite);
            this.Loaded += async (s, e) =>
            {
                _sendTimer.Change(0, 1000 / _hz);
                try
                {
                    throw new InvalidOperationException();
                    _webrtcTeacher = new WebrtcTeacher(_signalR, _token);
                    await _webrtcTeacher.InitializeAsync();
                }
                catch
                {
                    _webrtcTeacher = null;
                }
            };
            this.Closed += (s, e) =>
            {
                _sendTimer.Dispose();
                _webrtcTeacher?.Dispose();
            };
        }

        private void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var p = e.GetPosition(PreviewCanvas);
            var w = PreviewCanvas.ActualWidth;
            var h = PreviewCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;
            var nx = Math.Clamp(p.X / w, 0.0, 1.0);
            var ny = Math.Clamp(p.Y / h, 0.0, 1.0);
            _pendingNx = nx; _pendingNy = ny;
        }

        private void PreviewCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

        private bool _isFullscreen = false;
        private double _oldWidth = 760, _oldHeight = 440;
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F)
            {
                if (_isFullscreen)
                {
                    // Вернуть обычный режим
                    WindowStyle = WindowStyle.None;
                    WindowState = WindowState.Normal;
                    ResizeMode = ResizeMode.CanResize;
                    Width = _oldWidth;
                    Height = _oldHeight;
                    WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
                else
                {
                    _oldHeight = Height;
                    _oldWidth = Width;

                    // Включить фуллскрин
                    WindowStyle = WindowStyle.None;
                    WindowState = WindowState.Maximized;
                    ResizeMode = ResizeMode.NoResize;
                }

                _isFullscreen = !_isFullscreen;
            }
        }

        private async Task FlushAsync()
        {
            try
            {
                var nx = _pendingNx; var ny = _pendingNy;
                if (nx < 0 || ny < 0) return;
                if (Math.Abs(nx - _lastNx) < 0.001 && Math.Abs(ny - _lastNy) < 0.001)
                    return;

                _lastNx = nx; _lastNy = ny;

                if (_webrtcTeacher != null && _webrtcTeacher.DataChannelOpen)
                {
                    _webrtcTeacher.SendCoords((float)nx, (float)ny);
                }
                else
                {
                    await _signalR.SendCoordsAsync(_token, (float)nx, (float)ny);
                }
            }
            catch { }
        }

        private void OnFinishClick(object sender, RoutedEventArgs e)
        {
            var main = new MainWindow();
            main.Show();
            this.Close();
        }
    }
}

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

        private readonly UdpSession _session;
        private readonly string _token;
        private double _lastNx = -1, _lastNy = -1;
        private readonly double _deltaThreshold = 0.002; // настройте при необходимости
        private readonly int _minIntervalMs = 15;
        private DateTime _lastSent = DateTime.MinValue;

        public TeacherWorkWindow(UdpSession session, string token)
        {
            InitializeComponent();
            _session = session;
            _token = token;
            // optionally send a JOIN as teacher? For our server we already created room when teacher did CreateRoom
        }

        private void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(PreviewCanvas);
            var w = PreviewCanvas.ActualWidth;
            var h = PreviewCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;
            var nx = Math.Max(0, Math.Min(1, pos.X / w));
            var ny = Math.Max(0, Math.Min(1, pos.Y / h));

            var dx = nx - _lastNx;
            var dy = ny - _lastNy;
            var dist = Math.Sqrt((dx * dx) + (dy * dy));
            var now = DateTime.UtcNow;
            if (_lastNx >= 0 && dist < _deltaThreshold && (now - _lastSent).TotalMilliseconds < _minIntervalMs)
            {
                // ignore small/no move
                return;
            }

            _lastNx = nx; _lastNy = ny;
            _lastSent = now;

            // quantize to ushort
            var ux = (ushort)Math.Round(nx * 65535.0);
            var uy = (ushort)Math.Round(ny * 65535.0);
            _ = _session.SendCursorAsync(ux, uy);
        }

        private async void OnFinishClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await _session.SendLeaveAsync();
            }
            catch { }
            _session.Dispose();

            // Переход в главное меню
            var main = new MainWindow();
            main.Show();
            Close(); // закрыть TeacherWorkWindow
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F)
            {
                ToggleFullscreen();
            }
        }

        private void PreviewCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

        private bool? _isFullscreen = null;
        private double _oldWidth = 760, _oldHeight = 440;
        private void ToggleFullscreen()
        {
            if (_isFullscreen == null)
            {
                if(WindowState == WindowState.Maximized && ResizeMode == ResizeMode.NoResize && WindowStyle == WindowStyle.None)
                {
                    _isFullscreen = true;
                }
                else
                {
                    _isFullscreen = false;
                }
            }

            if ((bool)_isFullscreen)
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
}
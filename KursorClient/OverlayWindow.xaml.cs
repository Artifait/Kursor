using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace KursorClient
{
    /// <summary>
    /// Логика взаимодействия для OverlayWindow.xaml
    /// </summary>


    public partial class OverlayWindow : Window
    {
        // target window top-left in device-independent units (DIP)
        private double _targetLeft;
        private double _targetTop;

        private const double SmoothingAlpha = 0.6;
        private const double TeleportThreshold = 200.0; // px (DIP units)
        private const int InactivitySeconds = 5;
        private DateTime _lastUpdate = DateTime.MinValue;

        private readonly DispatcherTimer _animTimer;
        private readonly DispatcherTimer _inactivityTimer;

        public OverlayWindow()
        {
            InitializeComponent();

            // place initially off-screen-ish
            Left = SystemParameters.PrimaryScreenWidth - Width - 20;
            Top = SystemParameters.PrimaryScreenHeight - Height - 80;

            Loaded += (s, e) => MakeWindowClickThrough();

            _animTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (s, e) => AnimateStep(), Dispatcher);
            _animTimer.Start();

            _inactivityTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (s, e) => CheckInactivity(), Dispatcher);
            _inactivityTimer.Start();
        }

        /// <summary>
        /// Устанавливает цель: нормализованные координаты (0..1).
        /// Центр окна будет расположен в (nx * screenWidth, ny * screenHeight).
        /// Конвертация выполняется DPI-безопасно.
        /// </summary>
        public void SetTargetNormalizedPosition(float nx, float ny)
        {
            // sanitize input
            if (float.IsNaN(nx) || float.IsInfinity(nx)) nx = 0f;
            if (float.IsNaN(ny) || float.IsInfinity(ny)) ny = 0f;
            nx = Math.Max(0f, Math.Min(1f, nx));
            ny = Math.Max(0f, Math.Min(1f, ny));

            // получаем физические пиксели экрана (primary)
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            var pixelW = screen.Bounds.Width;
            var pixelH = screen.Bounds.Height;

            // получаем матрицу преобразования device->DIP (если она доступна)
            // CompositionTarget.TransformFromDevice конвертирует пиксели -> DIPs
            double dipWidth, dipHeight;
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                var transformFromDevice = source.CompositionTarget.TransformFromDevice;
                dipWidth = pixelW * transformFromDevice.M11;
                dipHeight = pixelH * transformFromDevice.M22;
            }
            else
            {
                // fallback: используем SystemParameters (DIP)
                dipWidth = SystemParameters.PrimaryScreenWidth;
                dipHeight = SystemParameters.PrimaryScreenHeight;
            }

            // центр в DIP
            var centerX = nx * dipWidth;
            var centerY = ny * dipHeight;

            // чтобы центр окна совпал с точкой centerX/centerY,
            // вычисляем top-left окна (диаметр окна учтён)
            var left = centerX - (this.Width / 2.0);
            var top = centerY - (this.Height / 2.0);

            // небольшие границы — не принципиально, но предотвращают extreme values
            var maxLeft = dipWidth - 0.5;
            var maxTop = dipHeight - 0.5;
            if (left < -10000) left = -10000;
            if (top < -10000) top = -10000;
            if (left > maxLeft) left = maxLeft;
            if (top > maxTop) top = maxTop;

            _targetLeft = left;
            _targetTop = top;

            _lastUpdate = DateTime.UtcNow;
        }

        private void AnimateStep()
        {
            // дистанция в DIPs
            var dx = _targetLeft - this.Left;
            var dy = _targetTop - this.Top;
            var dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist > TeleportThreshold)
            {
                this.Left = _targetLeft;
                this.Top = _targetTop;
            }
            else
            {
                this.Left += dx * SmoothingAlpha;
                this.Top += dy * SmoothingAlpha;
            }
        }

        private void CheckInactivity()
        {
            if (_lastUpdate == DateTime.MinValue) return;
            if ((DateTime.UtcNow - _lastUpdate).TotalSeconds > InactivitySeconds)
            {
                try
                {
                    _animTimer?.Stop();
                    _inactivityTimer?.Stop();
                    this.Close();
                }
                catch { /* ignore */ }
            }
        }

        private void MakeWindowClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            exStyle |= NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW;
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
        }

        private static class NativeMethods
        {
            public const int GWL_EXSTYLE = -20;
            public const int WS_EX_TRANSPARENT = 0x20;
            public const int WS_EX_TOOLWINDOW = 0x80;

            [DllImport("user32.dll", SetLastError = true)]
            public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        }
    }
}

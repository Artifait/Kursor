using System;
using System.Windows;
using System.Windows.Threading;

namespace KursorClient.Windows
{
    public partial class OverlayWindow : Window
    {
        private double _targetX, _targetY;
        private readonly DispatcherTimer _timer;
        private const double Lerp = 0.5; // интерполяция
        public OverlayWindow()
        {
            InitializeComponent();
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(8)
            };
            _timer.Tick += (s, e) => Tick();
            _timer.Start();
        }
        public void SetTargetNormalized(double nx, double ny)
        {
            var sw = SystemParameters.PrimaryScreenWidth;
            var sh = SystemParameters.PrimaryScreenHeight;
            _targetX = nx * sw - (Width / 2);
            _targetY = ny * sh - (Height / 2);
        }
        private void Tick()
        {
            var curX = Left; var curY = Top;
            var nx = curX + (_targetX - curX) * Lerp;
            var ny = curY + (_targetY - curY) * Lerp;
            Left = nx; Top = ny;
        }
    }
}

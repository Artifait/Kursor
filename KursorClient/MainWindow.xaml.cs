using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;


namespace KursorClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public partial class MainWindow : Window
    {
        private readonly NetworkService _net = new();
        private CancellationTokenSource? _teacherCts;
        private float _lastNormX = 0f, _lastNormY = 0f;
        private volatile bool _hasMouse = false;
        private int _seq = 0;

        public MainWindow()
        {
            InitializeComponent();
            ModeCombo.SelectionChanged += ModeCombo_SelectionChanged;
            CaptureArea.Width = 400;
            CaptureArea.Height = 240;
        }

        private void ModeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var mode = ModeCombo.SelectedIndex;
            TeacherPanel.Visibility = mode == 0 ? Visibility.Visible : Visibility.Collapsed;
            StudentPanel.Visibility = mode == 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        // Teacher: create room
        // Create room
        private async void CreateRoomBtn_Click(object sender, RoutedEventArgs e)
        {
            var server = ServerUrlBox.Text.Trim();
            var pass = TeacherPassword.Text;
            try
            {
                var res = await _net.CreateRoomAsync(server, pass); // теперь возвращает (roomId, teacherToken, udpPort)
                TeacherInfo.Text = $"Room: {res.roomId} | Token: {res.teacherToken}";
                // Инициализируем UDP (с учётом udpPort, который _net установил)
                await _net.InitUdpAsync(server);
                MessageBox.Show($"Room created: {res.roomId} (udpPort={res.udpPort})");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Create room failed: " + ex.Message);
            }
        }

        // Teacher: start sending loop
        private void StartTeacherBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_teacherCts != null) return;
            var server = ServerUrlBox.Text.Trim();
            var tokenTxt = ExtractTokenFromTeacherInfo();
            if (!Guid.TryParse(tokenTxt, out var token))
            {
                MessageBox.Show("Invalid teacher token. Create a room first.");
                return;
            }

            _teacherCts = new CancellationTokenSource();
            _seq = 0;
            _ = Task.Run(() => TeacherLoopAsync(server, token, _teacherCts.Token));
            MessageBox.Show("Started sending cursor to server.");
        }

        private void StopTeacherBtn_Click(object sender, RoutedEventArgs e)
        {
            _teacherCts?.Cancel();
            _teacherCts = null;
        }

        // Student: join room (HTTP)
        private async void JoinRoomBtn_Click(object sender, RoutedEventArgs e)
        {
            var server = ServerUrlBox.Text.Trim();
            var id = StudentRoomId.Text.Trim();
            var pass = StudentPassword.Text;
            try
            {
                var res = await _net.JoinRoomAsync(server, id, pass); // returns (studentToken, udpPort)
                StudentInfo.Text = $"Token: {res.studentToken}";
                await _net.InitUdpAsync(server);
                MessageBox.Show($"Joined room. Student token received (udpPort={res.udpPort}).");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Join failed: " + ex.Message);
            }
        }

        private CancellationTokenSource? _studentCts;
        private OverlayWindow? _overlay;

        private void StartStudentBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_studentCts != null) return;
            var server = ServerUrlBox.Text.Trim();
            var tokenTxt = ExtractTokenFromStudentInfo();
            if (!Guid.TryParse(tokenTxt, out var token))
            {
                MessageBox.Show("Invalid student token. Join room first.");
                return;
            }
            _studentCts = new CancellationTokenSource();

            // open overlay
            _overlay = new OverlayWindow();
            _overlay.Show();

            _ = Task.Run(() => StudentLoopAsync(server, token, _studentCts.Token));
            MessageBox.Show("Started receiving.");
        }

        private void StopStudentBtn_Click(object sender, RoutedEventArgs e)
        {
            _studentCts?.Cancel();
            _studentCts = null;
            _overlay?.Close();
            _overlay = null;
        }

        private string ExtractTokenFromTeacherInfo()
        {
            // TeacherInfo format: "Room: <id> | Token: <guid>"
            var parts = TeacherInfo.Text.Split("Token:");
            return parts.Length > 1 ? parts[1].Trim() : "";
        }

        private string ExtractTokenFromStudentInfo()
        {
            // StudentInfo format: "Token: <guid>"
            var parts = StudentInfo.Text.Split("Token:");
            return parts.Length > 1 ? parts[1].Trim() : "";
        }

        private void CaptureArea_MouseMove(object sender, MouseEventArgs e)
        {
            var p = e.GetPosition(CaptureArea);
            var w = CaptureArea.ActualWidth;
            var h = CaptureArea.ActualHeight;
            if (w <= 0 || h <= 0) return;
            var nx = (float)(p.X / w);
            var ny = (float)(p.Y / h);
            if (nx < 0f) nx = 0f;
            if (nx > 1f) nx = 1f;
            if (ny < 0f) ny = 0f;
            if (ny > 1f) ny = 1f;
            _lastNormX = nx;
            _lastNormY = ny;
            _hasMouse = true;
        }

        private async Task TeacherLoopAsync(string serverUrl, Guid teacherToken, CancellationToken ct)
        {
            try
            {
                // create UDP client and send initial keepalive
                await _net.InitUdpAsync(serverUrl);
                await _net.SendKeepaliveAsync(teacherToken, isTeacher: true);

                // send at ~30Hz
                var interval = TimeSpan.FromMilliseconds(33);
                while (!ct.IsCancellationRequested)
                {
                    // If no mouse movement yet — still may want to send keepalive occasionally
                    if (_hasMouse)
                    {
                        var x = _lastNormX;
                        var y = _lastNormY;
                        var seq = (uint)Interlocked.Increment(ref _seq);
                        await _net.SendCursorAsync(teacherToken, seq, x, y);
                    }
                    else
                    {
                        // send keepalive every second if idle
                        await _net.SendKeepaliveAsync(teacherToken, isTeacher: true);
                    }

                    await Task.Delay(interval, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show("Teacher loop error: " + ex.Message));
            }
        }

        private async Task StudentLoopAsync(string serverUrl, Guid studentToken, CancellationToken ct)
        {
            try
            {
                await _net.InitUdpAsync(serverUrl);
                await _net.SendKeepaliveAsync(studentToken, isTeacher: false);

                // обновим строку с инфой о экране сразу при старте
                Dispatcher.Invoke(() =>
                {
                    ScreenInfoText.Text = GetScreenInfoString();
                });

                // start receiving loop
                await foreach (var pkt in _net.ReceiveCursorPacketsAsync(ct))
                {
                    var x = pkt.x;
                    var y = pkt.y;

                    // marshal to UI: обновляем overlay и значения в UI
                    Dispatcher.Invoke(() =>
                    {
                        // показываем полученные нормализованные координаты с тремя знаками
                        ReceivedCoordsText.Text = $"x={x:0.000}, y={y:0.000}";

                        // обновляем overlay позицию как раньше
                        _overlay?.SetTargetNormalizedPosition(x, y);

                        // обновляем информацию о текущем экране (вдруг разрешение/масштаб поменялся)
                        ScreenInfoText.Text = GetScreenInfoString();
                    });
                }
            }
            catch (OperationCanceledException) { /* отменено */ }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show("Student loop error: " + ex.Message));
            }
            finally
            {
                // гарантированно закрываем overlay, если он всё ещё открыт
                try
                {
                    Dispatcher.Invoke(() =>
                    {
                        _overlay?.Close();
                        _overlay = null;
                    });
                }
                catch { }
            }
        }

        // Вспомогательный метод: собирает строку с физическим разрешением,
        // DIP-разрешением и масштабом (пример: "1920x1080 / 1280x720 / scale=1.50x")
        private string GetScreenInfoString()
        {
            try
            {
                // физическое разрешение (пиксели)
                var scr = System.Windows.Forms.Screen.PrimaryScreen;
                var pixelW = scr.Bounds.Width;
                var pixelH = scr.Bounds.Height;

                // Попытка получить трансформацию device -> DIP (если доступно)
                var source = PresentationSource.FromVisual(this);
                double dipW, dipH;
                double scaleX = 1.0, scaleY = 1.0;

                if (source?.CompositionTarget != null)
                {
                    var t = source.CompositionTarget.TransformFromDevice;
                    // TransformFromDevice: devicePx -> DIP  => dip = device * M11
                    dipW = pixelW * t.M11;
                    dipH = pixelH * t.M22;
                    if (t.M11 != 0) scaleX = 1.0 / t.M11;
                    if (t.M22 != 0) scaleY = 1.0 / t.M22;
                }
                else
                {
                    // fallback: SystemParameters (DIP units)
                    dipW = SystemParameters.PrimaryScreenWidth;
                    dipH = SystemParameters.PrimaryScreenHeight;
                }

                var scaleAvg = (scaleX + scaleY) / 2.0;
                return $"{pixelW}x{pixelH} px / {Math.Round(dipW)}x{Math.Round(dipH)} dip / scale={scaleAvg:0.00}x";
            }
            catch (Exception ex)
            {
                return "screen info err: " + ex.Message;
            }
        }
    }
}
using System.Windows;
using KursorClient.Services;
using System;

namespace KursorClient.Windows
{
    public partial class StudentSetupWindow : Window
    {
        private UdpSession? _session;
        private OverlayWindow? _overlay;

        public StudentSetupWindow()
        {
            InitializeComponent();
        }

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            var token = TokenBox.Text?.Trim();
            if (string.IsNullOrEmpty(token))
            {
                MessageBox.Show("Введите токен комнаты");
                return;
            }
            if (ConfigService.ServerEndpoint == null)
            {
                MessageBox.Show("Укажите Server URL в настройках");
                return;
            }

            try
            {
                _session?.Dispose();
                _session = new UdpSession(ConfigService.ServerEndpoint);
                var ok = await _session.JoinRoomAsync(token);
                if (!ok)
                {
                    MessageBox.Show("Не удалось подключиться к комнате (токен неверен)");
                    return;
                }

                var win = new StudentCenterWindow(_session, token);
                win.Show();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

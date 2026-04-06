using InteractiveT.Core.Enum;
using InteractiveT.Infrastructure.Services;
using InteractiveTWPF;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using System.Windows.Controls;

namespace InteractiveTWPF
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var login = LoginTextBox.Text;
            var password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                ShowError("Введите логин и пароль");
                return;
            }

            try
            {
                var authService = App.ServiceProvider.GetRequiredService<AuthService>();
                var success = await authService.LoginAsync(login, password);

                if (success)
                {
                    OpenDashboard(authService.CurrentUser.Role);
                    this.Hide(); // Скрываем вместо закрытия, чтобы приложение не завершилось
                }
                else
                {
                    ShowError("Неверный логин или пароль");
                }
            }
            catch (Exception ex)
            {
                ShowError("Ошибка: " + ex.Message);
            }
        }

        private void OpenDashboard(UserRole role)
        {
            Window dashboard = null;
            var authService = App.ServiceProvider.GetRequiredService<AuthService>();

            if (role == UserRole.Student)
            {
                dashboard = new StudentDashboardWindow(authService.CurrentUser);
            }
            else if (role == UserRole.Teacher)
            {
                dashboard = new TeacherDashboardWindow();
            }
            else if (role == UserRole.Admin)
            {
                dashboard = new AdminDashboardWindow();
            }
            else if (role == UserRole.DeputyPrincipal)
            {
                dashboard = new DeputyPrincipalDashboardWindow();
            }

            if (dashboard != null)
            {
                dashboard.Show();
                this.Hide();
                dashboard.Closed += (s, ev) =>
                {
                    // Очищаем поля при возврате
                    LoginTextBox.Clear();
                    PasswordBox.Clear();
                    ErrorTextBlock.Visibility = Visibility.Collapsed;
                    this.Show();
                };
            }
        }

        private void ShowError(string message)
        {
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;
        }
    }
}
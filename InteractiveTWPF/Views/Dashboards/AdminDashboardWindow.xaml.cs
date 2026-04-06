using InteractiveT.Core.Enum;
using InteractiveT.Core.Models;
using InteractiveT.Infrastructure.Data;
using InteractiveT.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Thickness = System.Windows.Thickness;

namespace InteractiveTWPF
{
    public partial class AdminDashboardWindow : Window
    {
        private readonly ApplicationDbContext _context;
        private readonly AuthService _authService;
        private ObservableCollection<User> _users;
        private ObservableCollection<Subject> _subjects;
        private ObservableCollection<Class> _classes;
        private ObservableCollection<RoleStatItem> _roleStats;

        public AdminDashboardWindow()
        {
            InitializeComponent();

            _context = new ApplicationDbContext();
            _authService = App.ServiceProvider.GetService(typeof(AuthService)) as AuthService;

            _users = new ObservableCollection<User>();
            _subjects = new ObservableCollection<Subject>();
            _classes = new ObservableCollection<Class>();
            _roleStats = new ObservableCollection<RoleStatItem>();

            RoleStatsItemsControl.ItemsSource = _roleStats;

            Loaded += AdminDashboardWindow_Loaded;
        }

        private async void AdminDashboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_authService?.CurrentUser != null)
            {
                WelcomeTextBlock.Text = $"Администратор: {_authService.CurrentUser.FullName}";
            }

            await LoadAllDataAsync();
        }

        private async Task LoadAllDataAsync()
        {
            await LoadUsersAsync();
            await LoadSubjectsAsync();
            await LoadClassesAsync();
            await LoadStatisticsAsync();
        }

        private async Task LoadUsersAsync()
        {
            try
            {
                var users = await _context.Users
                    .ToListAsync();

                _users.Clear();
                foreach (var user in users)
                {
                    _users.Add(user);
                }

                UsersDataGrid.ItemsSource = _users;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки пользователей: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadSubjectsAsync()
        {
            try
            {
                var subjects = await _context.Subjects
                    .Include(s => s.Tests)
                    .ToListAsync();

                _subjects.Clear();
                foreach (var subject in subjects)
                {
                    _subjects.Add(subject);
                }

                SubjectsDataGrid.ItemsSource = _subjects;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки предметов: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadClassesAsync()
        {
            try
            {
                var classes = await _context.Classes
                    .Include(c => c.UserClasses)
                    .ToListAsync();

                _classes.Clear();
                foreach (var cls in classes)
                {
                    _classes.Add(cls);
                }

                ClassesDataGrid.ItemsSource = _classes;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки классов: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadStatisticsAsync()
        {
            try
            {
                var totalUsers = await _context.Users.CountAsync();
                var totalTests = await _context.Tests.CountAsync();
                var totalSubjects = await _context.Subjects.CountAsync();
                var totalAttempts = await _context.TestAttempts.CountAsync();

                TotalUsersTextBlock.Text = totalUsers.ToString();
                TotalTestsTextBlock.Text = totalTests.ToString();
                TotalSubjectsTextBlock.Text = totalSubjects.ToString();
                TotalAttemptsTextBlock.Text = totalAttempts.ToString();

                StatsTextBlock.Text = $"Пользователей: {totalUsers} | Тестов: {totalTests} | Предметов: {totalSubjects}";

                // Статистика по ролям
                var roleStats = await _context.Users
                    .GroupBy(u => u.Role)
                    .Select(g => new { Role = g.Key, Count = g.Count() })
                    .ToListAsync();

                _roleStats.Clear();
                foreach (var rs in roleStats)
                {
                    string roleName;
                    switch (rs.Role)
                    {
                        case UserRole.Admin:
                            roleName = "Администраторы";
                            break;
                        case UserRole.DeputyPrincipal:
                            roleName = "Завучи";
                            break;
                        case UserRole.Teacher:
                            roleName = "Учителя";
                            break;
                        case UserRole.Student:
                            roleName = "Ученики";
                            break;
                        default:
                            roleName = rs.Role.ToString();
                            break;
                    }

                    _roleStats.Add(new RoleStatItem
                    {
                        RoleName = roleName,
                        Count = rs.Count
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки статистики: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AddUserButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new AddUserWindow();
            window.Owner = this;
            window.ShowDialog();

            this.Activate();

            if (window.Success)
            {
                await LoadUsersAsync();
                await LoadStatisticsAsync();
            }
        }

        private async void RefreshUsersButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadAllDataAsync();
            MessageBox.Show("Данные обновлены!", "Обновление", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void EditUserButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is User user)
            {
                var window = new EditUserWindow(user);
                window.Owner = this;
                var result = window.ShowDialog();

                this.Activate();

                if (result == true)
                {
                    await LoadUsersAsync();
                    await LoadStatisticsAsync();
                }
            }
        }

        private async void DeleteUserButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is User user)
            {
                var result = MessageBox.Show(
                    string.Format("Вы уверены, что хотите удалить пользователя {0}?", user.FullName),
                    "Удаление пользователя",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        user.IsActive = false; // Мягкое удаление
                        _context.Users.Update(user);
                        await _context.SaveChangesAsync();

                        await LoadUsersAsync();
                        await LoadStatisticsAsync();
                        MessageBox.Show("Пользователь деактивирован!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Ошибка удаления: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void AddSubjectButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new AddSubjectWindow();
            window.Owner = this;
            window.ShowDialog();

            this.Activate();

            if (window.Success)
            {
                await LoadSubjectsAsync();
                await LoadStatisticsAsync();
            }
        }

        private async void AddClassButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new AddClassWindow();
            window.Owner = this;
            window.ShowDialog();

            this.Activate();

            if (window.Success)
            {
                await LoadClassesAsync();
                await LoadStatisticsAsync();
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class RoleStatItem
    {
        public string RoleName { get; set; }
        public int Count { get; set; }
    }
}

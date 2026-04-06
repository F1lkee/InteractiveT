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
    public partial class DeputyPrincipalDashboardWindow : Window
    {
        private readonly ApplicationDbContext _context;
        private readonly AuthService _authService;
        private ObservableCollection<TestAttempt> _allResults;
        private ObservableCollection<TopStudentItem> _topStudents;
        private ObservableCollection<LowPassRateTestItem> _lowPassRateTests;
        private ObservableCollection<User> _teachers;
        private ObservableCollection<Class> _classes;

        public DeputyPrincipalDashboardWindow()
        {
            InitializeComponent();

            _context = new ApplicationDbContext();
            _authService = App.ServiceProvider.GetService(typeof(AuthService)) as AuthService;

            _allResults = new ObservableCollection<TestAttempt>();
            _topStudents = new ObservableCollection<TopStudentItem>();
            _lowPassRateTests = new ObservableCollection<LowPassRateTestItem>();
            _teachers = new ObservableCollection<User>();
            _classes = new ObservableCollection<Class>();

            Loaded += DeputyPrincipalDashboardWindow_Loaded;
        }

        private async void DeputyPrincipalDashboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_authService?.CurrentUser != null)
            {
                WelcomeTextBlock.Text = $"Завуч: {_authService.CurrentUser.FullName}";
            }

            await LoadAllDataAsync();
        }

        private async Task LoadAllDataAsync()
        {
            await LoadResultsAsync();
            await LoadAnalyticsAsync();
            await LoadTeachersAsync();
            await LoadClassesAsync();
        }

        private async Task LoadResultsAsync()
        {
            try
            {
                var results = await _context.TestAttempts
                    .Include(a => a.User)
                    .Include(a => a.Test)
                    .ThenInclude(t => t.Subject)
                    .OrderByDescending(a => a.StartedAt)
                    .ToListAsync();

                _allResults.Clear();
                foreach (var result in results)
                {
                    _allResults.Add(result);
                }

                AllResultsDataGrid.ItemsSource = _allResults;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки результатов: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadAnalyticsAsync()
        {
            try
            {
                // Общая статистика
                var allAttempts = await _context.TestAttempts
                    .Include(a => a.User)
                    .Include(a => a.Test)
                    .ThenInclude(t => t.Subject)
                    .ToListAsync();

                var totalTests = allAttempts.Count;
                TotalTestsTextBlock.Text = totalTests.ToString();

                // Средний балл
                var attemptsWithMaxScore = allAttempts.Where(a => a.MaxScore > 0).ToList();
                if (attemptsWithMaxScore.Any())
                {
                    var avgScore = attemptsWithMaxScore.Average(a => (a.Score / a.MaxScore) * 100);
                    AvgScoreTextBlock.Text = string.Format("{0:F1}%", avgScore);

                    // Сдаваемость
                    var passedCount = attemptsWithMaxScore.Count(a => a.IsPassed);
                    var passRate = (double)passedCount / attemptsWithMaxScore.Count * 100;
                    PassRateTextBlock.Text = string.Format("{0:F1}%", passRate);
                }
                else
                {
                    AvgScoreTextBlock.Text = "0%";
                    PassRateTextBlock.Text = "0%";
                }

                // Лучшие ученики
                var studentStats = allAttempts
                    .Where(a => a.IsCompleted && a.MaxScore > 0)
                    .GroupBy(a => a.UserId)
                    .Select(g => new
                    {
                        UserId = g.Key,
                        FullName = g.First().User.FullName,
                        AttemptCount = g.Count(),
                        AvgScore = g.Average(a => (a.Score / a.MaxScore) * 100),
                        PassedCount = g.Count(a => a.IsPassed)
                    })
                    .OrderByDescending(s => s.AvgScore)
                    .Take(10)
                    .ToList();

                _topStudents.Clear();
                foreach (var student in studentStats)
                {
                    _topStudents.Add(new TopStudentItem
                    {
                        FullName = student.FullName,
                        AttemptCount = student.AttemptCount,
                        AvgScoreDisplay = string.Format("{0:F1}%", student.AvgScore),
                        PassedCount = student.PassedCount
                    });
                }

                TopStudentsDataGrid.ItemsSource = _topStudents;

                // Тесты с низкой сдаваемостью
                var testStats = allAttempts
                    .Where(a => a.IsCompleted)
                    .GroupBy(a => a.TestId)
                    .Select(g => new
                    {
                        Test = g.First().Test,
                        AttemptCount = g.Count(),
                        PassRate = (double)g.Count(a => a.IsPassed) / g.Count() * 100
                    })
                    .Where(t => t.PassRate < 70 && t.AttemptCount >= 2)
                    .OrderBy(t => t.PassRate)
                    .Take(10)
                    .ToList();

                _lowPassRateTests.Clear();
                foreach (var test in testStats)
                {
                    _lowPassRateTests.Add(new LowPassRateTestItem
                    {
                        Title = test.Test.Title,
                        Subject = test.Test.Subject,
                        AttemptCount = test.AttemptCount,
                        PassRateDisplay = string.Format("{0:F1}%", test.PassRate)
                    });
                }

                LowPassRateTestsDataGrid.ItemsSource = _lowPassRateTests;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки аналитики: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadTeachersAsync()
        {
            try
            {
                var teachers = await _context.Users
                    .Include(u => u.CreatedTests)
                    .Where(u => u.Role == UserRole.Teacher)
                    .ToListAsync();

                _teachers.Clear();
                foreach (var teacher in teachers)
                {
                    _teachers.Add(teacher);
                }

                TeachersDataGrid.ItemsSource = _teachers;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки учителей: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private async void RefreshResultsButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadAllDataAsync();
            MessageBox.Show("Данные обновлены!", "Обновление", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class TopStudentItem
    {
        public string FullName { get; set; }
        public int AttemptCount { get; set; }
        public string AvgScoreDisplay { get; set; }
        public int PassedCount { get; set; }
    }

    public class LowPassRateTestItem
    {
        public string Title { get; set; }
        public Subject Subject { get; set; }
        public int AttemptCount { get; set; }
        public string PassRateDisplay { get; set; }
    }
}

using InteractiveT.Core.Models;
using InteractiveT.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace InteractiveTWPF
{
    public partial class StudentDashboardWindow : Window
    {
        private readonly User _currentUser;
        private readonly ApplicationDbContext _context;
        private ObservableCollection<TestCardViewModel> _availableTests;

        public StudentDashboardWindow(User user)
        {
            InitializeComponent();
            _currentUser = user;
            _context = new ApplicationDbContext();
            _availableTests = new ObservableCollection<TestCardViewModel>();

            LoadUserData();
            LoadAvailableTests();
            LoadTestHistory();
        }

        private void LoadUserData()
        {
            WelcomeTextBlock.Text = string.Format("Добро пожаловать, {0}!", _currentUser.FullName);

            var userClass = _context.UserClasses
                .Include(uc => uc.Class)
                .FirstOrDefault(uc => uc.UserId == _currentUser.Id);

            if (userClass?.Class != null)
            {
                ClassTextBlock.Text = string.Format("Класс: {0}", userClass.Class.Name);
            }
            else
            {
                ClassTextBlock.Text = "Класс не назначен";
            }
        }

        private void LoadAvailableTests()
        {
            try
            {
                // Получаем класс ученика
                var userClass = _context.UserClasses
                    .FirstOrDefault(uc => uc.UserId == _currentUser.Id);

                // Загружаем опубликованные тесты
                var tests = _context.Tests
                    .Include(t => t.Subject)
                    .Include(t => t.Class)
                    .Include(t => t.Questions)
                    .Where(t => t.IsPublished)
                    .ToList();

                // Фильтрация по классу если он указан у теста
                var filteredTests = tests.Where(t =>
                {
                    // Если тест привязан к классу, показываем только ученикам этого класса
                    if (t.ClassId.HasValue)
                    {
                        return userClass != null && userClass.ClassId == t.ClassId.Value;
                    }
                    // Если класс не указан — показываем всем
                    return true;
                }).ToList();

                _availableTests.Clear();
                foreach (var test in filteredTests)
                {
                    _availableTests.Add(new TestCardViewModel(test));
                }

                TestsItemsControl.ItemsSource = _availableTests;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки тестов: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadTestHistory()
        {
            try
            {
                var attempts = _context.TestAttempts
                    .Include(a => a.Test)
                    .Where(a => a.UserId == _currentUser.Id)
                    .OrderByDescending(a => a.StartedAt)
                    .Select(a => new TestHistoryViewModel
                    {
                        TestTitle = a.Test != null ? a.Test.Title : "Удалённый тест",
                        StartedAt = a.StartedAt,
                        Score = a.Score,
                        MaxScore = a.MaxScore,
                        IsCompleted = a.CompletedAt.HasValue
                    })
                    .ToList();

                HistoryDataGrid.ItemsSource = attempts;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки истории: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StartTestButton_Click(object sender, RoutedEventArgs e)
        {
            // Получаем Id теста из Tag кнопки
            if (!(sender is Button button && button.Tag is Guid testId))
            {
                return;
            }

            var test = _context.Tests
                .Include(t => t.Questions)
                .FirstOrDefault(t => t.Id == testId);

            if (test == null)
            {
                MessageBox.Show("Тест не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Проверяем лимит попыток
            var attemptsCount = _context.TestAttempts
                .Count(a => a.TestId == test.Id && a.UserId == _currentUser.Id);

            if (test.AttemptsLimit > 0 && attemptsCount >= test.AttemptsLimit)
            {
                MessageBox.Show(
                    string.Format("Вы исчерпали лимит попыток ({0}).", test.AttemptsLimit),
                    "Лимит попыток",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var attempt = new TestAttempt
            {
                Id = Guid.NewGuid(),
                TestId = test.Id,
                UserId = _currentUser.Id,
                StartedAt = DateTime.UtcNow
            };

            _context.TestAttempts.Add(attempt);
            await _context.SaveChangesAsync();

            var testWindow = new TestTakingWindow(test, attempt);
            testWindow.Closed += (s, args) =>
            {
                // Перезагружаем данные после прохождения
                LoadAvailableTests();
                LoadTestHistory();
            };
            testWindow.Show();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    /// <summary>
    /// ViewModel для карточки теста
    /// </summary>
    public class TestCardViewModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string SubjectName { get; set; }
        public string ClassName { get; set; }
        public int? TimeLimitSeconds { get; set; }
        public int QuestionsCount { get; set; }
        public string TimeLimitDisplay
        {
            get
            {
                if (!TimeLimitSeconds.HasValue) return "Без лимита";
                var minutes = TimeLimitSeconds.Value / 60;
                var seconds = TimeLimitSeconds.Value % 60;
                return string.Format("{0} мин {1} сек", minutes, seconds);
            }
        }

        public TestCardViewModel(Test test)
        {
            Id = test.Id;
            Title = test.Title;
            Description = test.Description;
            SubjectName = test.Subject?.Name ?? "Без предмета";
            ClassName = test.Class?.Name;
            TimeLimitSeconds = test.TimeLimitSeconds;
            QuestionsCount = test.Questions != null ? test.Questions.Count : 0;
        }
    }

    /// <summary>
    /// ViewModel для истории тестов
    /// </summary>
    public class TestHistoryViewModel
    {
        public string TestTitle { get; set; }
        public DateTime StartedAt { get; set; }
        public double? Score { get; set; }
        public double? MaxScore { get; set; }
        public bool IsCompleted { get; set; }

        public string ScoreDisplay
        {
            get
            {
                if (!Score.HasValue) return "—";
                return string.Format("{0:F1} / {1:F1}", Score.Value, MaxScore ?? 0);
            }
        }

        public string StatusDisplay
        {
            get { return IsCompleted ? "Завершён" : "В процессе"; }
        }
    }
}
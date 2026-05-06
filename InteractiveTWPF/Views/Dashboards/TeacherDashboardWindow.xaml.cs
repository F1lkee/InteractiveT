using InteractiveT.Core.Enum;
using InteractiveT.Core.Models;
using InteractiveT.Infrastructure.Data;
using InteractiveT.Infrastructure.Services;
using InteractiveTWPF.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Thickness = System.Windows.Thickness;

namespace InteractiveTWPF
{
    public partial class TeacherDashboardWindow : Window
    {
        private readonly ApplicationDbContext _context;
        private readonly AIService _aiService;
        private readonly User _currentUser;
        private ObservableCollection<Test> _tests;
        private ObservableCollection<Subject> _subjects;
        private ObservableCollection<Class> _classes;
        private ObservableCollection<TestAttempt> _results;
        private AiChatPanel _aiChatPanel;

        public TeacherDashboardWindow()
        {
            InitializeComponent();

            _context = new ApplicationDbContext();
            _aiService = App.ServiceProvider.GetService(typeof(AIService)) as AIService;
            var authService = App.ServiceProvider.GetService(typeof(AuthService)) as AuthService;
            _currentUser = authService?.CurrentUser;

            _tests = new ObservableCollection<Test>();
            _subjects = new ObservableCollection<Subject>();
            _classes = new ObservableCollection<Class>();
            _results = new ObservableCollection<TestAttempt>();

            // Создаём AI-чат панель и добавляем поверх всего
            if (_aiService != null && _currentUser != null)
            {
                _aiChatPanel = new AiChatPanel(_aiService, _currentUser.Id);

                // Добавляем в последний ряд Grid (поверх всего)
                if (this.Content is Grid mainGrid)
                {
                    Grid.SetRowSpan(_aiChatPanel, mainGrid.RowDefinitions.Count);
                    mainGrid.Children.Add(_aiChatPanel);
                }
            }

            Loaded += TeacherDashboardWindow_Loaded;
        }

        private async void TeacherDashboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_currentUser != null)
            {
                WelcomeTextBlock.Text = $"Добро пожаловать, {_currentUser.FullName}!";
            }

            await LoadAllDataAsync();
        }

        private async Task LoadAllDataAsync()
        {
            await LoadTestsAsync();
            await LoadSubjectsAsync();
            await LoadClassesAsync();
            await LoadResultsAsync();
        }

        private async Task LoadTestsAsync()
        {
            try
            {
                var tests = await _context.Tests
                    .Include(t => t.Subject)
                    .Include(t => t.Questions)
                    .Include(t => t.Attempts)
                    .Where(t => t.AuthorId == _currentUser.Id)
                    .ToListAsync();

                _tests.Clear();
                foreach (var test in tests)
                {
                    _tests.Add(test);
                }

                TestsDataGrid.ItemsSource = _tests;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки тестов: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                SubjectsInfoTextBlock.Text = $"Предметов: {_subjects.Count}";
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

        private async Task LoadResultsAsync()
        {
            try
            {
                var attempts = await _context.TestAttempts
                    .Include(a => a.User)
                    .Include(a => a.Test)
                    .Where(a => a.Test.AuthorId == _currentUser.Id)
                    .OrderByDescending(a => a.StartedAt)
                    .ToListAsync();

                _results.Clear();
                foreach (var attempt in attempts)
                {
                    _results.Add(attempt);
                }

                ResultsDataGrid.ItemsSource = _results;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки результатов: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CreateTestButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new CreateTestWindow(_currentUser.Id);
            window.Owner = this;
            var result = window.ShowDialog();

            this.Activate();

            if (result == true && window.CreatedTest != null)
            {
                await LoadTestsAsync();
            }
        }

        private async void RefreshTestsButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadTestsAsync();
            MessageBox.Show("Список тестов обновлён!", "Обновление", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void TestsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TestsDataGrid.SelectedItem is Test selectedTest)
            {
                OpenEditTestWindow(selectedTest);
            }
        }

        private async void OpenEditTestWindow(Test test)
        {
            var window = new EditTestWindow(test);
            window.Owner = this;
            window.ShowDialog();

            this.Activate();

            // Обновляем список тестов
            await LoadTestsAsync();
            await LoadSubjectsAsync();
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
            }
        }

        private void ExportToExcelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_results == null || _results.Count == 0)
            {
                MessageBox.Show("Нет данных для экспорта. Сначала загрузите результаты тестов.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var exportService = new ExcelExportService();
            exportService.ExportTestAttemptsToExcelWithDialog(_results.ToList());
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}

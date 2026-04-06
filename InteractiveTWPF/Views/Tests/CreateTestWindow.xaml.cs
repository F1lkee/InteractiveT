using InteractiveT.Core.Models;
using InteractiveT.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace InteractiveTWPF
{
    public partial class CreateTestWindow : Window
    {
        private readonly Guid _authorId;
        public Test CreatedTest { get; private set; }

        public CreateTestWindow(Guid authorId)
        {
            InitializeComponent();
            _authorId = authorId;

            LoadSubjects();
            LoadClasses();
        }

        private void LoadSubjects()
        {
            try
            {
                var context = new ApplicationDbContext();
                var subjects = context.Subjects.ToList();
                SubjectComboBox.ItemsSource = subjects;

                if (subjects.Any())
                    SubjectComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки предметов: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadClasses(int? gradeLevel = null)
        {
            try
            {
                var context = new ApplicationDbContext();
                var query = context.Classes.AsQueryable();

                if (gradeLevel.HasValue)
                {
                    query = query.Where(c => c.GradeLevel == gradeLevel.Value);
                }

                var classes = query.OrderBy(c => c.GradeLevel).ThenBy(c => c.Name).ToList();
                ClassComboBox.ItemsSource = classes;

                if (!classes.Any())
                {
                    ClassComboBox.SelectedIndex = -1;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки классов: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SubjectComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Фильтрация классов по уровню предмета (если предмет выбран)
            // Пока просто загружаем все классы
        }

        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            var title = TitleTextBox.Text.Trim();
            var description = DescriptionTextBox.Text.Trim();
            var timeText = TimeLimitTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show("Введите название теста.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SubjectComboBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите предмет.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int timeLimit;
            if (!int.TryParse(timeText, out timeLimit) || timeLimit < 0)
            {
                MessageBox.Show("Лимит времени должен быть неотрицательным числом.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedSubject = SubjectComboBox.SelectedItem as Subject;
            var selectedClass = ClassComboBox.SelectedItem as Class;
            var isPublished = IsPublishedCheckBox.IsChecked == true;
            var shuffleQuestions = ShuffleQuestionsCheckBox.IsChecked == true;
            var shuffleAnswers = ShuffleAnswersCheckBox.IsChecked == true;

            try
            {
                var context = new ApplicationDbContext();

                var test = new Test
                {
                    Title = title,
                    Description = description,
                    SubjectId = selectedSubject.Id,
                    ClassId = selectedClass?.Id,
                    AuthorId = _authorId,
                    TimeLimitSeconds = timeLimit > 0 ? timeLimit : (int?)null,
                    ShuffleQuestions = shuffleQuestions,
                    ShuffleAnswers = shuffleAnswers,
                    IsPublished = isPublished
                };

                context.Tests.Add(test);
                await context.SaveChangesAsync();

                CreatedTest = test;
                MessageBox.Show(string.Format("Тест \"{0}\" успешно создан!", title), "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка создания теста: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}

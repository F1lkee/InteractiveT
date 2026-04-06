using InteractiveT.Core.Enum;
using InteractiveT.Core.Models;
using InteractiveT.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace InteractiveTWPF
{
    public partial class EditTestWindow : Window
    {
        private readonly Test _test;
        private readonly ApplicationDbContext _context;
        private ObservableCollection<Question> _questions;

        public EditTestWindow(Test test)
        {
            InitializeComponent();

            _test = test;
            _context = new ApplicationDbContext();
            _questions = new ObservableCollection<Question>();

            // Загружаем вопросы с ответами (без трекинга)
            var questions = _context.Questions
                .AsNoTracking()
                .Where(q => q.TestId == _test.Id)
                .OrderBy(q => q.OrderIndex)
                .Include(q => q.Answers)
                .ToList();

            foreach (var q in questions)
            {
                _questions.Add(q);
            }

            QuestionsDataGrid.ItemsSource = _questions;

            // Загружаем предметы и классы
            LoadSubjects();
            LoadClasses();

            // Заполняем поля
            TestTitleTextBlock.Text = _test.Title;
            TestInfoTextBlock.Text = string.Format("Предмет: {0} | Класс: {1} | Вопросов: {2}",
                _test.Subject?.Name ?? "Не указан",
                _test.Class?.Name ?? "Не указан",
                _questions.Count);

            TitleTextBox.Text = _test.Title;
            DescriptionTextBox.Text = _test.Description;
            TimeLimitTextBox.Text = (_test.TimeLimitSeconds ?? 0).ToString();
            ShuffleQuestionsCheckBox.IsChecked = _test.ShuffleQuestions;
            ShuffleAnswersCheckBox.IsChecked = _test.ShuffleAnswers;
            ShowResultsCheckBox.IsChecked = _test.ShowResultsImmediately;
            IsPublishedCheckBox.IsChecked = _test.IsPublished;

            // Устанавливаем выбранный предмет
            if (_test.SubjectId != Guid.Empty)
            {
                var subject = _context.Subjects.Find(_test.SubjectId);
                if (subject != null)
                {
                    SubjectComboBox.SelectedItem = subject;
                }
            }

            // Устанавливаем выбранный класс
            if (_test.ClassId.HasValue)
            {
                var cls = _context.Classes.Find(_test.ClassId.Value);
                if (cls != null)
                {
                    ClassComboBox.SelectedItem = cls;
                }
            }

            UpdateStatus();
        }

        private void LoadSubjects()
        {
            try
            {
                var subjects = _context.Subjects.ToList();
                SubjectComboBox.ItemsSource = subjects;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки предметов: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadClasses()
        {
            try
            {
                var classes = _context.Classes.OrderBy(c => c.GradeLevel).ThenBy(c => c.Name).ToList();
                ClassComboBox.ItemsSource = classes;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки классов: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateStatus()
        {
            StatusTextBlock.Text = string.Format("Вопросов: {0} | Всего баллов: {1}",
                _questions.Count, _questions.Sum(q => q.Points));
        }

        private void AddQuestionButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new AddEditQuestionWindow(null, _questions.Count);
            window.Owner = this;
            var result = window.ShowDialog();

            this.Activate();

            if (result == true && window.Question != null)
            {
                _questions.Add(window.Question);
                UpdateStatus();
            }
        }

        private void EditQuestionButton_Click(object sender, RoutedEventArgs e)
        {
            if (QuestionsDataGrid.SelectedItem is Question question)
            {
                var window = new AddEditQuestionWindow(question, -1);
                window.Owner = this;
                var result = window.ShowDialog();

                this.Activate();

                if (result == true)
                {
                    // Обновляем вопрос в коллекции
                    var index = _questions.IndexOf(question);
                    if (index >= 0)
                    {
                        _questions[index] = window.Question;
                    }
                    UpdateStatus();
                }
            }
            else
            {
                MessageBox.Show("Выберите вопрос для редактирования.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeleteQuestionButton_Click(object sender, RoutedEventArgs e)
        {
            if (QuestionsDataGrid.SelectedItem is Question question)
            {
                var result = MessageBox.Show(
                    string.Format("Удалить вопрос \"{0}\"?", question.Text.Length > 50 ? question.Text.Substring(0, 50) + "..." : question.Text),
                    "Удаление вопроса",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // Удаляем из БД если вопрос уже сохранён
                    if (question.Id != Guid.Empty)
                    {
                        _context.Questions.Remove(question);
                        _context.SaveChanges();
                    }

                    _questions.Remove(question);

                    // Пересчитываем OrderIndex
                    for (int i = 0; i < _questions.Count; i++)
                    {
                        _questions[i].OrderIndex = i + 1;
                    }

                    UpdateStatus();
                }
            }
            else
            {
                MessageBox.Show("Выберите вопрос для удаления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Обновляем настройки теста
                var testInDb = _context.Tests.Find(_test.Id);
                if (testInDb == null)
                {
                    MessageBox.Show("Тест не найден в базе данных.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                testInDb.Title = TitleTextBox.Text.Trim();
                testInDb.Description = DescriptionTextBox.Text.Trim();

                // Обновляем предмет
                if (SubjectComboBox.SelectedItem is Subject selectedSubject)
                {
                    testInDb.SubjectId = selectedSubject.Id;
                }

                // Обновляем класс
                if (ClassComboBox.SelectedItem is Class selectedClass)
                {
                    testInDb.ClassId = selectedClass.Id;
                }
                else
                {
                    testInDb.ClassId = null;
                }

                int timeLimit;
                if (int.TryParse(TimeLimitTextBox.Text, out timeLimit))
                {
                    testInDb.TimeLimitSeconds = timeLimit > 0 ? timeLimit : (int?)null;
                }

                testInDb.ShuffleQuestions = ShuffleQuestionsCheckBox.IsChecked == true;
                testInDb.ShuffleAnswers = ShuffleAnswersCheckBox.IsChecked == true;
                testInDb.ShowResultsImmediately = ShowResultsCheckBox.IsChecked == true;
                testInDb.IsPublished = IsPublishedCheckBox.IsChecked == true;
                testInDb.UpdatedAt = DateTime.UtcNow;

                // === Синхронизация вопросов ===
                // Загружаем текущие вопросы из БД
                var dbQuestions = _context.Questions
                    .Include(q => q.Answers)
                    .Where(q => q.TestId == _test.Id)
                    .ToList();

                var dbQuestionIds = new HashSet<Guid>(dbQuestions.Select(q => q.Id));
                var currentQuestionIds = new HashSet<Guid>(_questions.Where(q => q.Id != Guid.Empty).Select(q => q.Id));

                // 1. Удаляем вопросы, которых нет в текущей коллекции
                var questionsToDelete = dbQuestions.Where(q => !currentQuestionIds.Contains(q.Id)).ToList();
                foreach (var q in questionsToDelete)
                {
                    var answers = _context.Answers.Where(a => a.QuestionId == q.Id).ToList();
                    _context.Answers.RemoveRange(answers);
                    _context.Questions.Remove(q);
                }

                // 2. Обновляем или добавляем вопросы
                for (int i = 0; i < _questions.Count; i++)
                {
                    var q = _questions[i];

                    if (q.Id != Guid.Empty && dbQuestionIds.Contains(q.Id))
                    {
                        // Обновляем существующий вопрос
                        var dbQ = dbQuestions.First(x => x.Id == q.Id);
                        dbQ.Text = q.Text;
                        dbQ.Type = q.Type;
                        dbQ.Points = q.Points;
                        dbQ.Explanation = q.Explanation;
                        dbQ.ImageData = q.ImageData;
                        dbQ.OrderIndex = i + 1;

                        // Синхронизируем ответы
                        var dbAnswerIds = new HashSet<Guid>(dbQ.Answers.Select(a => a.Id));
                        var currentAnswerIds = new HashSet<Guid>(q.Answers.Where(a => a.Id != Guid.Empty).Select(a => a.Id));

                        // Удаляем лишние ответы
                        var answersToDelete = dbQ.Answers.Where(a => !currentAnswerIds.Contains(a.Id)).ToList();
                        foreach (var a in answersToDelete)
                        {
                            _context.Answers.Remove(a);
                        }

                        // Обновляем или добавляем ответы
                        foreach (var a in q.Answers)
                        {
                            if (a.Id != Guid.Empty && dbAnswerIds.Contains(a.Id))
                            {
                                var dbA = dbQ.Answers.First(x => x.Id == a.Id);
                                dbA.Text = a.Text;
                                dbA.IsCorrect = a.IsCorrect;
                            }
                            else
                            {
                                _context.Answers.Add(new Answer
                                {
                                    Id = Guid.NewGuid(),
                                    Text = a.Text,
                                    IsCorrect = a.IsCorrect,
                                    QuestionId = dbQ.Id
                                });
                            }
                        }
                    }
                    else
                    {
                        // Добавляем новый вопрос
                        var newQuestion = new Question
                        {
                            Id = Guid.NewGuid(),
                            Text = q.Text,
                            Type = q.Type,
                            Points = q.Points,
                            Explanation = q.Explanation,
                            ImageData = q.ImageData,
                            OrderIndex = i + 1,
                            TestId = _test.Id
                        };

                        _context.Questions.Add(newQuestion);
                        await _context.SaveChangesAsync(); // Сохраняем чтобы получить Id

                        // Добавляем ответы
                        foreach (var a in q.Answers)
                        {
                            _context.Answers.Add(new Answer
                            {
                                Id = Guid.NewGuid(),
                                Text = a.Text,
                                IsCorrect = a.IsCorrect,
                                QuestionId = newQuestion.Id
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();

                // Обновляем UI
                TestTitleTextBlock.Text = testInDb.Title;
                TestInfoTextBlock.Text = string.Format("Предмет: {0} | Класс: {1} | Вопросов: {2}",
                    testInDb.Subject != null ? testInDb.Subject.Name : "Не указан",
                    testInDb.Class != null ? testInDb.Class.Name : "Не указан",
                    _questions.Count);

                // Перезагружаем вопросы из БД для синхронизации
                ReloadQuestionsFromDb();

                MessageBox.Show("Тест успешно сохранён!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ReloadQuestionsFromDb()
        {
            // Отсоединяем все текущие tracked entities
            var trackedQuestions = _context.Questions.Local.ToList();
            foreach (var q in trackedQuestions)
            {
                _context.Entry(q).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            }

            // Перезагружаем из БД
            var questions = _context.Questions
                .Where(q => q.TestId == _test.Id)
                .OrderBy(q => q.OrderIndex)
                .Include(q => q.Answers)
                .ToList();

            _questions.Clear();
            foreach (var q in questions)
            {
                _questions.Add(q);
            }

            QuestionsDataGrid.ItemsSource = null;
            QuestionsDataGrid.ItemsSource = _questions;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}

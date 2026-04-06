using InteractiveT.Core.Enum;
using InteractiveT.Core.Models;
using InteractiveT.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace InteractiveTWPF
{
    public partial class TestTakingWindow : Window
    {
        private readonly Test _test;
        private readonly TestAttempt _attempt;
        private readonly ApplicationDbContext _context;
        private List<Question> _questions;
        private int _currentQuestionIndex = 0;
        private DispatcherTimer _timer;
        private int _remainingSeconds;
        private DateTime _startTime;

        // Ответы пользователя
        private Dictionary<int, List<Guid>> _selectedAnswerIds;  // questionIndex -> answerIds
        private Dictionary<int, string> _textAnswers;             // questionIndex -> text

        public TestTakingWindow(Test test, TestAttempt attempt)
        {
            InitializeComponent();
            _test = test;
            _attempt = attempt;
            _context = new ApplicationDbContext();
            _selectedAnswerIds = new Dictionary<int, List<Guid>>();
            _textAnswers = new Dictionary<int, string>();

            // Перезагружаем тест с вопросами из БД
            LoadTestFromDatabase();
        }

        private void LoadTestFromDatabase()
        {
            var testWithQuestions = _context.Tests
                .Include(t => t.Questions)
                    .ThenInclude(q => q.Answers)
                .FirstOrDefault(t => t.Id == _test.Id);

            if (testWithQuestions == null)
            {
                MessageBox.Show("Тест не найден в базе данных.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
                return;
            }

            _questions = testWithQuestions.Questions.OrderBy(q => q.OrderIndex).ToList();

            if (_test.ShuffleQuestions)
            {
                var random = new Random();
                _questions = _questions.OrderBy(x => random.Next()).ToList();
            }

            TestTitleTextBlock.Text = _test.Title;
            _startTime = DateTime.UtcNow;

            // Таймер
            if (_test.TimeLimitSeconds.HasValue && _test.TimeLimitSeconds.Value > 0)
            {
                _remainingSeconds = _test.TimeLimitSeconds.Value;
                StartTimer();
            }
            else
            {
                TimerBorder.Visibility = Visibility.Collapsed;
            }

            // Строим навигацию
            BuildQuestionNavigation();

            // Показываем первый вопрос
            ShowQuestion(0);
        }

        private void BuildQuestionNavigation()
        {
            QuestionNavigationPanel.Children.Clear();

            for (int i = 0; i < _questions.Count; i++)
            {
                var btn = new Button
                {
                    Content = (i + 1).ToString(),
                    Width = 40,
                    Height = 40,
                    Margin = new Thickness(4),
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = i
                };

                UpdateNavigationButton(btn, i);

                btn.Click += (s, e) =>
                {
                    if (s is Button b && b.Tag is int index)
                    {
                        ShowQuestion(index);
                    }
                };

                QuestionNavigationPanel.Children.Add(btn);
            }
        }

        private void UpdateNavigationButton(Button btn, int index)
        {
            bool isAnswered = IsQuestionAnswered(index);

            if (index == _currentQuestionIndex)
            {
                btn.Background = (Brush)FindResource("AccentBrush");
                btn.Foreground = Brushes.White;
            }
            else if (isAnswered)
            {
                btn.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // зелёный
                btn.Foreground = Brushes.White;
            }
            else
            {
                btn.Background = new SolidColorBrush(Color.FromRgb(224, 224, 224));
                btn.Foreground = (Brush)FindResource("TextPrimaryBrush");
            }
        }

        private void UpdateAllNavigationButtons()
        {
            foreach (var child in QuestionNavigationPanel.Children)
            {
                if (child is Button btn && btn.Tag is int index)
                {
                    UpdateNavigationButton(btn, index);
                }
            }
        }

        private bool IsQuestionAnswered(int index)
        {
            if (_selectedAnswerIds.ContainsKey(index) && _selectedAnswerIds[index].Count > 0)
                return true;
            if (_textAnswers.ContainsKey(index) && !string.IsNullOrWhiteSpace(_textAnswers[index]))
                return true;
            return false;
        }

        private void ShowQuestion(int index)
        {
            if (index < 0 || index >= _questions.Count)
                return;

            _currentQuestionIndex = index;
            var question = _questions[index];

            // Перезагружаем вопрос с ответами
            var questionWithAnswers = _context.Questions
                .Include(q => q.Answers)
                .FirstOrDefault(q => q.Id == question.Id);

            if (questionWithAnswers == null)
            {
                QuestionTextBlock.Text = "Вопрос не найден.";
                AnswersPanel.Children.Clear();
                return;
            }

            // Заголовок и прогресс
            QuestionNumberTextBlock.Text = string.Format("Вопрос {0}", index + 1);
            QuestionTextBlock.Text = questionWithAnswers.Text;
            ProgressTextBlock.Text = string.Format("Вопрос {0} из {1}", index + 1, _questions.Count);
            ProgressBar.Value = (double)(index + 1) / _questions.Count * 100;

            // Изображение
            if (!string.IsNullOrEmpty(questionWithAnswers.ImageData))
            {
                try
                {
                    var bytes = Convert.FromBase64String(questionWithAnswers.ImageData);
                    var stream = new MemoryStream(bytes);
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.StreamSource = stream;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.EndInit();
                    QuestionImage.Source = image;
                    QuestionImageBorder.Visibility = Visibility.Visible;
                }
                catch
                {
                    QuestionImageBorder.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                QuestionImageBorder.Visibility = Visibility.Collapsed;
            }

            // Ответы
            AnswersPanel.Children.Clear();
            var answers = questionWithAnswers.Answers.OrderBy(a => a.Id).ToList();

            if (_test.ShuffleAnswers)
            {
                var random = new Random();
                answers = answers.OrderBy(x => random.Next()).ToList();
            }

            switch (questionWithAnswers.Type)
            {
                case QuestionType.SingleChoice:
                    CreateSingleChoiceAnswers(answers, index);
                    break;
                case QuestionType.MultipleChoice:
                    CreateMultipleChoiceAnswers(answers, index);
                    break;
                case QuestionType.TextInput:
                    CreateTextInputAnswer(index);
                    break;
                case QuestionType.Matching:
                    CreateTextInputAnswer(index); // Пока как текстовый ввод
                    break;
            }

            // Кнопки навигации
            PrevButton.IsEnabled = index > 0;

            if (index == _questions.Count - 1)
                NextButton.Content = "Далее →";
            else
                NextButton.Content = "Далее →";

            // Обновляем цвета кнопок навигации
            UpdateAllNavigationButtons();
        }

        private void CreateSingleChoiceAnswers(List<Answer> answers, int questionIndex)
        {
            var groupName = "Question_" + questionIndex;

            foreach (var answer in answers)
            {
                var radio = new RadioButton
                {
                    Content = answer.Text,
                    Tag = answer.Id,
                    FontSize = 16,
                    Margin = new Thickness(0, 8, 0, 8),
                    Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush"),
                    GroupName = groupName
                };

                // Восстанавливаем выбранный ответ
                if (_selectedAnswerIds.ContainsKey(questionIndex) &&
                    _selectedAnswerIds[questionIndex].Contains(answer.Id))
                {
                    radio.IsChecked = true;
                }

                radio.Checked += (s, e) =>
                {
                    _selectedAnswerIds[questionIndex] = new List<Guid> { answer.Id };
                    UpdateAllNavigationButtons();
                };

                AnswersPanel.Children.Add(radio);
            }
        }

        private void CreateMultipleChoiceAnswers(List<Answer> answers, int questionIndex)
        {
            foreach (var answer in answers)
            {
                var checkBox = new CheckBox
                {
                    Content = answer.Text,
                    Tag = answer.Id,
                    FontSize = 16,
                    Margin = new Thickness(0, 8, 0, 8),
                    Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush")
                };

                // Восстанавлием выбранные ответы
                if (_selectedAnswerIds.ContainsKey(questionIndex) &&
                    _selectedAnswerIds[questionIndex].Contains(answer.Id))
                {
                    checkBox.IsChecked = true;
                }

                checkBox.Checked += (s, e) =>
                {
                    ToggleAnswer(questionIndex, answer.Id, true);
                    UpdateAllNavigationButtons();
                };
                checkBox.Unchecked += (s, e) =>
                {
                    ToggleAnswer(questionIndex, answer.Id, false);
                    UpdateAllNavigationButtons();
                };

                AnswersPanel.Children.Add(checkBox);
            }
        }

        private void CreateTextInputAnswer(int questionIndex)
        {
            var textBox = new TextBox
            {
                FontSize = 16,
                Padding = new Thickness(12),
                MinHeight = 80,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Style = (Style)FindResource("ModernTextBoxStyle")
            };

            // Восстанавливаем текст
            if (_textAnswers.ContainsKey(questionIndex))
                textBox.Text = _textAnswers[questionIndex];

            textBox.TextChanged += (s, e) =>
            {
                _textAnswers[questionIndex] = textBox.Text;
                UpdateAllNavigationButtons();
            };

            AnswersPanel.Children.Add(textBox);
        }

        private void ToggleAnswer(int questionIndex, Guid answerId, bool isChecked)
        {
            if (!_selectedAnswerIds.ContainsKey(questionIndex))
                _selectedAnswerIds[questionIndex] = new List<Guid>();

            var list = _selectedAnswerIds[questionIndex];

            if (isChecked)
            {
                if (!list.Contains(answerId))
                    list.Add(answerId);
            }
            else
            {
                list.Remove(answerId);
            }
        }

        private void StartTimer()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
            UpdateTimerDisplay();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            _remainingSeconds--;
            UpdateTimerDisplay();

            if (_remainingSeconds <= 0)
            {
                _timer.Stop();
                MessageBox.Show("Время вышло! Тест будет завершён.", "Время вышло",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                CalculateAndSaveResults();
                ShowResults();
            }
        }

        private void UpdateTimerDisplay()
        {
            var time = TimeSpan.FromSeconds(Math.Max(0, _remainingSeconds));
            TimerTextBlock.Text = string.Format("{0:D2}:{1:D2}", time.Minutes, time.Seconds);

            if (_remainingSeconds < 60)
                TimerTextBlock.Foreground = Brushes.White;
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentQuestionIndex > 0)
                ShowQuestion(_currentQuestionIndex - 1);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentQuestionIndex < _questions.Count - 1)
                ShowQuestion(_currentQuestionIndex + 1);
        }

        private void FinishButton_Click(object sender, RoutedEventArgs e)
        {
            int answeredCount = 0;
            for (int i = 0; i < _questions.Count; i++)
            {
                if (IsQuestionAnswered(i)) answeredCount++;
            }
            int unansweredCount = _questions.Count - answeredCount;

            var message = string.Format(
                "Вы ответили на {0} из {1} вопросов.{2}",
                answeredCount,
                _questions.Count,
                unansweredCount > 0 ? string.Format("\nНе отвечено: {0} вопросов.", unansweredCount) : ""
            );

            var result = MessageBox.Show(
                message + "\n\nЗавершить тест?",
                "Завершение теста",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                CalculateAndSaveResults();
                ShowResults();
            }
        }

        private void CalculateAndSaveResults()
        {
            if (_timer != null)
                _timer.Stop();

            double totalScore = 0;
            double maxScore = 0;
            int correctAnswers = 0;

            for (int i = 0; i < _questions.Count; i++)
            {
                var question = _questions[i];
                maxScore += question.Points;

                var correctAnswerIds = _context.Answers
                    .Where(a => a.QuestionId == question.Id && a.IsCorrect)
                    .Select(a => a.Id)
                    .ToList();

                if (question.Type == QuestionType.SingleChoice || question.Type == QuestionType.MultipleChoice)
                {
                    var userAnswerIds = _selectedAnswerIds.ContainsKey(i)
                        ? _selectedAnswerIds[i]
                        : new List<Guid>();

                    bool isCorrect = userAnswerIds.OrderBy(id => id).SequenceEqual(correctAnswerIds.OrderBy(id => id));
                    if (isCorrect)
                    {
                        totalScore += question.Points;
                        correctAnswers++;
                    }
                }
                else if (question.Type == QuestionType.TextInput || question.Type == QuestionType.Matching)
                {
                    var userText = _textAnswers.ContainsKey(i) ? _textAnswers[i].Trim().ToLower() : "";

                    var correctTexts = _context.Answers
                        .Where(a => a.QuestionId == question.Id && a.IsCorrect)
                        .Select(a => a.Text.Trim().ToLower())
                        .ToList();

                    if (correctTexts.Contains(userText))
                    {
                        totalScore += question.Points;
                        correctAnswers++;
                    }
                }

                // Сохраняем ответы ученика
                SaveStudentAnswers(question, i);
            }

            // Обновляем попытку
            var attemptInDb = _context.TestAttempts.Find(_attempt.Id);
            if (attemptInDb != null)
            {
                attemptInDb.Score = totalScore;
                attemptInDb.MaxScore = maxScore;
                attemptInDb.CompletedAt = DateTime.UtcNow;
                attemptInDb.IsCompleted = true;
                attemptInDb.TimeSpent = DateTime.UtcNow - _startTime;

                if (_test.PassingThreshold.HasValue)
                    attemptInDb.IsPassed = (totalScore / maxScore) >= _test.PassingThreshold.Value;
                else
                    attemptInDb.IsPassed = maxScore > 0 && (totalScore / maxScore) >= 0.6;
            }

            _context.SaveChanges();

            // Сохраняем для отображения
            _resultCorrectAnswers = correctAnswers;
            _resultTotalQuestions = _questions.Count;
            _resultScore = maxScore > 0 ? (totalScore / maxScore) * 100 : 0;
            _resultIsPassed = attemptInDb?.IsPassed ?? false;
            _resultTimeSpent = attemptInDb?.TimeSpent;
        }

        private void SaveStudentAnswers(Question question, int questionIndex)
        {
            if (question.Type == QuestionType.SingleChoice || question.Type == QuestionType.MultipleChoice)
            {
                var answerIds = _selectedAnswerIds.ContainsKey(questionIndex)
                    ? _selectedAnswerIds[questionIndex]
                    : new List<Guid>();

                foreach (var answerId in answerIds)
                {
                    _context.StudentAnswers.Add(new StudentAnswer
                    {
                        AttemptId = _attempt.Id,
                        QuestionId = question.Id,
                        SelectedAnswerId = answerId,
                        AnsweredAt = DateTime.UtcNow
                    });
                }
            }
            else if (question.Type == QuestionType.TextInput || question.Type == QuestionType.Matching)
            {
                var userText = _textAnswers.ContainsKey(questionIndex) ? _textAnswers[questionIndex] : "";

                _context.StudentAnswers.Add(new StudentAnswer
                {
                    AttemptId = _attempt.Id,
                    QuestionId = question.Id,
                    TextAnswer = userText,
                    AnsweredAt = DateTime.UtcNow
                });
            }
        }

        // Поля для результатов
        private int _resultCorrectAnswers;
        private int _resultTotalQuestions;
        private double _resultScore;
        private bool _resultIsPassed;
        private TimeSpan? _resultTimeSpent;

        private void ShowResults()
        {
            ResultScoreTextBlock.Text = string.Format("{0:F0}%", _resultScore);
            ResultDetailsTextBlock.Text = string.Format("{0} из {1} правильных", _resultCorrectAnswers, _resultTotalQuestions);

            if (_resultIsPassed)
            {
                ResultPassTextBlock.Text = "Тест сдан!";
                ResultPassTextBlock.Foreground = (Brush)FindResource("AccentBrush");
            }
            else
            {
                ResultPassTextBlock.Text = "Тест не сдан";
                ResultPassTextBlock.Foreground = (Brush)FindResource("ErrorBrush");
            }

            if (_resultTimeSpent.HasValue)
            {
                var time = _resultTimeSpent.Value;
                ResultTimeTextBlock.Text = string.Format(
                    "Затраченное время: {0} мин {1} сек",
                    (int)time.TotalMinutes, time.Seconds);
            }

            ResultsOverlay.Visibility = Visibility.Visible;
        }

        private void CloseResultsButton_Click(object sender, RoutedEventArgs e)
        {
            _context.Dispose();
            this.Close();
        }
    }
}

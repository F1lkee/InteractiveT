using InteractiveT.Core.Enum;
using InteractiveT.Core.Models;
using InteractiveT.Infrastructure.Data;
using InteractiveT.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace InteractiveTWPF
{
    public partial class AiChatPanel : UserControl
    {
        private readonly AIService _aiService;
        private readonly Guid? _authorId;
        private bool _isOpen = false;
        private bool _isAnimating = false;

        public ObservableCollection<ChatMessage> Messages { get; } = new ObservableCollection<ChatMessage>();

        private const string PlaceholderText = "О чём сгенерировать вопросы?";

        private void MessageInput_GotFocus(object sender, RoutedEventArgs e)
        {
            if (MessageInput.Text == PlaceholderText)
            {
                MessageInput.Text = "";
            }
        }

        private void MessageInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MessageInput.Text))
            {
                MessageInput.Text = PlaceholderText;
            }
        }

        public event EventHandler<QuestionsGeneratedEventArgs> QuestionsGenerated;

        public AiChatPanel(AIService aiService, Guid? authorId = null)
        {
            InitializeComponent();
            _aiService = aiService;
            _authorId = authorId;

            MessagesItemsControl.ItemsSource = Messages;

            // Приветственное сообщение
            Messages.Add(new ChatMessage
            {
                Text = "Привет! Я AI-помощник для генерации тестовых вопросов. Опишите предмет и тему, и я создам вопросы!",
                IsUser = false
            });
        }

        private async void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isAnimating) return;
            _isAnimating = true;

            if (_isOpen)
            {
                await ClosePanelAsync();
            }
            else
            {
                await OpenPanelAsync();
            }

            _isAnimating = false;
        }

        private async void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isAnimating) return;
            _isAnimating = true;
            await ClosePanelAsync();
            _isAnimating = false;
        }

        private void RootGrid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Закрываем панель только если клик НЕ по ChatPanel
            if (_isOpen && !_isAnimating)
            {
                var hitTest = VisualTreeHelper.HitTest(ChatPanel, e.GetPosition(ChatPanel));
                if (hitTest == null)
                {
                    _isAnimating = true;
                    ClosePanelAsync().ContinueWith(_ => _isAnimating = false);
                }
            }
        }

        private async Task OpenPanelAsync()
        {
            ToggleButton.Visibility = Visibility.Collapsed;
            OverlayBorder.Visibility = Visibility.Visible;
            ChatPanel.Visibility = Visibility.Visible;

            var sb = (Storyboard)FindResource("SlideInAnimation");
            sb.Begin(ChatPanel);

            _isOpen = true;
            await Task.Delay(300);
        }

        private async Task ClosePanelAsync()
        {
            var sb = (Storyboard)FindResource("SlideOutAnimation");
            sb.Begin(ChatPanel);

            await Task.Delay(300);

            ChatPanel.Visibility = Visibility.Collapsed;
            OverlayBorder.Visibility = Visibility.Collapsed;
            ToggleButton.Visibility = Visibility.Visible;

            _isOpen = false;
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessageAsync();
        }

        private async void MessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                await SendMessageAsync();
            }
        }

        private async Task SendMessageAsync()
        {
            var text = MessageInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(text) || text == PlaceholderText) return;

            // Добавляем сообщение пользователя
            Messages.Add(new ChatMessage { Text = text, IsUser = true });
            MessageInput.Text = "";
            SendButton.IsEnabled = false;

            // Прокрутка вниз
            ScrollToBottom();

            try
            {
                // Определяем предмет и тему из сообщения
                var (subject, topic, count) = ParseUserInput(text);

                // Показываем "печатает..."
                var thinkingMsg = new ChatMessage { Text = "⏳ Генерация вопросов...", IsUser = false };
                Messages.Add(thinkingMsg);
                ScrollToBottom();

                // Генерируем вопросы
                var questions = await _aiService.GenerateQuestionsAsync(subject, topic, count, "средний");

                // Удаляем "печатает..."
                Messages.Remove(thinkingMsg);

                if (questions.Count > 0)
                {
                    // Показываем результат
                    var resultText = string.Format("✅ Сгенерировано {0} вопросов!\n\nПредмет: {1}\nТема: {2}",
                        questions.Count, subject, topic);

                    Messages.Add(new ChatMessage { Text = resultText, IsUser = false });

                    // Сохраняем вопросы в БД если указан authorId
                    if (_authorId.HasValue)
                    {
                        await SaveQuestionsToDatabaseAsync(questions, subject, topic);
                    }

                    // Оповещаем внешний код
                    QuestionsGenerated?.Invoke(this, new QuestionsGeneratedEventArgs(questions));
                }
                else
                {
                    Messages.Add(new ChatMessage
                    {
                        Text = "Не удалось сгенерировать вопросы. Попробуйте другой запрос.",
                        IsUser = false
                    });
                }
            }
            catch (Exception ex)
            {
                // Удаляем "печатает..." если есть
                var thinking = Messages.LastOrDefault(m => m.Text.Contains("⏳"));
                if (thinking != null) Messages.Remove(thinking);

                Messages.Add(new ChatMessage
                {
                    Text = "❌ Ошибка: " + ex.Message,
                    IsUser = false
                });
            }
            finally
            {
                SendButton.IsEnabled = true;
                ScrollToBottom();
            }
        }

        private (string subject, string topic, int count) ParseUserInput(string text)
        {
            // Простой парсер: ищем ключевые слова
            string subject = "Общий";
            string topic = text;
            int count = 5;

            // Пытаемся найти число
            var words = text.Split(' ', '\n', '\r');
            foreach (var word in words)
            {
                if (int.TryParse(word, out int num) && num >= 1 && num <= 20)
                {
                    count = num;
                    break;
                }
            }

            // Пытаемся определить предмет по ключевым словам
            var lower = text.ToLower();
            if (lower.Contains("математик") || lower.Contains("алгебр") || lower.Contains("геометр") || lower.Contains("уравнен"))
            {
                subject = "Математика";
            }
            else if (lower.Contains("физик") || lower.Contains("механ") || lower.Contains("энерг"))
            {
                subject = "Физика";
            }
            else if (lower.Contains("хим") || lower.Contains("реакц") || lower.Contains("элемент"))
            {
                subject = "Химия";
            }
            else if (lower.Contains("истор") || lower.Contains("войн") || lower.Contains("революц"))
            {
                subject = "История";
            }
            else if (lower.Contains("русск") || lower.Contains("грамматик") || lower.Contains("орфограф"))
            {
                subject = "Русский язык";
            }
            else if (lower.Contains("биолог") || lower.Contains("эколог") || lower.Contains("эволюц"))
            {
                subject = "Биология";
            }
            else if (lower.Contains("географ") || lower.Contains("климат") || lower.Contains("континент"))
            {
                subject = "География";
            }
            else if (lower.Contains("английск") || lower.Contains("english") || lower.Contains("граммар"))
            {
                subject = "Английский язык";
            }
            else if (lower.Contains("литератур") || lower.Contains("роман") || lower.Contains("поэм"))
            {
                subject = "Литература";
            }
            else if (lower.Contains("информатик") || lower.Contains("программ") || lower.Contains("алгоритм"))
            {
                subject = "Информатика";
            }

            // Тема — первый предложением без числа
            topic = text;
            foreach (var word in words)
            {
                if (int.TryParse(word, out _))
                {
                    topic = topic.Replace(word, "").Trim();
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(topic))
                topic = text;

            return (subject, topic, count);
        }

        private async Task SaveQuestionsToDatabaseAsync(System.Collections.Generic.List<GeneratedQuestion> questions, string subject, string topic)
        {
            try
            {
                using (var context = new ApplicationDbContext())
                {
                    // Находим или создаём предмет
                    var subjectEntity = await context.Subjects
                        .FirstOrDefaultAsync(s => s.Name == subject);

                    if (subjectEntity == null)
                    {
                        subjectEntity = new Subject { Name = subject };
                        context.Subjects.Add(subjectEntity);
                        await context.SaveChangesAsync();
                    }

                    // Находим или создаём тест
                    var testTitle = string.Format("{0}: {1}", subject, topic.Length > 50 ? topic.Substring(0, 50) + "..." : topic);

                    var test = new Test
                    {
                        Title = testTitle,
                        Description = string.Format("AI-генерация: {0}", topic),
                        SubjectId = subjectEntity.Id,
                        AuthorId = _authorId.Value,
                        IsPublished = false,
                        TimeLimitSeconds = 900,
                        ShuffleQuestions = false,
                        ShuffleAnswers = false,
                        ShowResultsImmediately = true
                    };

                    context.Tests.Add(test);
                    await context.SaveChangesAsync();

                    // Сохраняем вопросы
                    for (int i = 0; i < questions.Count; i++)
                    {
                        var q = questions[i];

                        var question = new Question
                        {
                            Text = q.Text,
                            Type = q.Type,
                            Points = q.Points,
                            Explanation = q.Explanation,
                            OrderIndex = i + 1,
                            TestId = test.Id
                        };

                        context.Questions.Add(question);
                        await context.SaveChangesAsync();

                        // Сохраняем ответы
                        foreach (var a in q.Answers)
                        {
                            context.Answers.Add(new Answer
                            {
                                Text = a.Text,
                                IsCorrect = a.IsCorrect,
                                QuestionId = question.Id
                            });
                        }
                    }

                    await context.SaveChangesAsync();

                    Messages.Add(new ChatMessage
                    {
                        Text = string.Format("💾 Тест \"{0}\" сохранён в базу данных!", testTitle),
                        IsUser = false
                    });
                }
            }
            catch (Exception ex)
            {
                Messages.Add(new ChatMessage
                {
                    Text = "⚠️ Ошибка сохранения: " + ex.Message,
                    IsUser = false
                });
            }
        }

        private void QuickAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string count)
            {
                MessageInput.Text = string.Format("Сгенерируй {0} вопросов", count);
                MessageInput.Focus();
                MessageInput.SelectAll();
            }
        }

        private void QuickTestAction_Click(object sender, RoutedEventArgs e)
        {
            MessageInput.Text = "Создай тест из 10 вопросов на общую эрудицию";
            MessageInput.Focus();
            MessageInput.SelectAll();
        }

        private void ScrollToBottom()
        {
            MessagesScrollViewer.Dispatcher.BeginInvoke(new Action(() =>
            {
                MessagesScrollViewer.ScrollToEnd();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    /// <summary>
    /// Сообщение чата
    /// </summary>
    public class ChatMessage
    {
        public string Text { get; set; }
        public bool IsUser { get; set; }
    }

    /// <summary>
    /// Аргументы события генерации вопросов
    /// </summary>
    public class QuestionsGeneratedEventArgs : EventArgs
    {
        public System.Collections.Generic.List<GeneratedQuestion> Questions { get; }

        public QuestionsGeneratedEventArgs(System.Collections.Generic.List<GeneratedQuestion> questions)
        {
            Questions = questions;
        }
    }

    /// <summary>
    /// Селектор шаблонов для сообщений
    /// </summary>
    public class ChatMessageTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is ChatMessage msg && container is FrameworkElement element)
            {
                return msg.IsUser
                    ? (DataTemplate)element.FindResource("UserMessageTemplate")
                    : (DataTemplate)element.FindResource("AiMessageTemplate");
            }
            return null;
        }
    }
}

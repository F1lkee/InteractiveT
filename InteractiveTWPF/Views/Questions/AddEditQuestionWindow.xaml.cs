using InteractiveT.Core.Enum;
using InteractiveT.Core.Models;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace InteractiveTWPF
{
    public partial class AddEditQuestionWindow : Window
    {
        private readonly Question _originalQuestion;
        private ObservableCollection<AnswerItem> _answers;
        private string _imageDataBase64;

        public Question Question { get; private set; }

        public AddEditQuestionWindow(Question question, int orderIndex)
        {
            InitializeComponent();

            _originalQuestion = question;
            _answers = new ObservableCollection<AnswerItem>();
            AnswersItemsControl.ItemsSource = _answers;

            if (question != null)
            {
                // Режим редактирования
                TitleTextBlock.Text = "Редактирование вопроса";
                QuestionTextTextBox.Text = question.Text;
                PointsTextBox.Text = question.Points.ToString();
                ExplanationTextBox.Text = question.Explanation;

                // Тип
                int typeIndex = (int)question.Type;
                QuestionTypeComboBox.SelectedIndex = typeIndex > 2 ? 0 : typeIndex;

                // Картинка
                if (!string.IsNullOrEmpty(question.ImageData))
                {
                    _imageDataBase64 = question.ImageData;
                    ShowImage(question.ImageData);
                }

                // Ответы
                foreach (var a in question.Answers)
                {
                    _answers.Add(new AnswerItem { Text = a.Text, IsCorrect = a.IsCorrect });
                }
            }
            else
            {
                // Режим создания — добавляем 4 пустых ответа
                for (int i = 0; i < 4; i++)
                {
                    _answers.Add(new AnswerItem { Text = "", IsCorrect = false });
                }
            }
        }

        private void ShowImage(string base64)
        {
            try
            {
                var bytes = Convert.FromBase64String(base64);
                var image = new BitmapImage();
                using (var ms = new MemoryStream(bytes))
                {
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = ms;
                    image.EndInit();
                }
                QuestionImage.Source = image;
                QuestionImage.Visibility = Visibility.Visible;
            }
            catch
            {
                QuestionImage.Visibility = Visibility.Collapsed;
            }
        }

        private void BrowseImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.gif;*.bmp",
                Title = "Выберите изображение"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var bytes = File.ReadAllBytes(dialog.FileName);
                    _imageDataBase64 = Convert.ToBase64String(bytes);
                    ShowImage(_imageDataBase64);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка загрузки изображения: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RemoveImageButton_Click(object sender, RoutedEventArgs e)
        {
            _imageDataBase64 = null;
            QuestionImage.Source = null;
            QuestionImage.Visibility = Visibility.Collapsed;
        }

        private void AddAnswerButton_Click(object sender, RoutedEventArgs e)
        {
            _answers.Add(new AnswerItem { Text = "", IsCorrect = false });
        }

        private void RemoveAnswerButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is AnswerItem item)
            {
                _answers.Remove(item);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var text = QuestionTextTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("Введите текст вопроса.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var pointsText = PointsTextBox.Text.Trim();
            int points;
            if (!int.TryParse(pointsText, out points) || points < 0)
            {
                MessageBox.Show("Баллы должны быть неотрицательным числом.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var type = QuestionType.SingleChoice;
            if (QuestionTypeComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                var tag = item.Tag.ToString();
                switch (tag)
                {
                    case "MultipleChoice":
                        type = QuestionType.MultipleChoice;
                        break;
                    case "TextInput":
                        type = QuestionType.TextInput;
                        break;
                }
            }

            // Для текстового типа ответы не нужны
            if (type == QuestionType.TextInput)
            {
                _answers.Clear();
            }
            else
            {
                // Проверяем что есть хотя бы один правильный ответ
                var correctAnswers = _answers;
                if (!correctAnswers.Any(a => a.IsCorrect))
                {
                    MessageBox.Show("Укажите хотя бы один правильный ответ.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // Создаём/обновляем вопрос
            Question = new Question
            {
                Id = _originalQuestion != null ? _originalQuestion.Id : Guid.NewGuid(),
                Text = text,
                Type = type,
                Points = points,
                Explanation = ExplanationTextBox.Text.Trim(),
                ImageData = _imageDataBase64,
                OrderIndex = _originalQuestion != null ? _originalQuestion.OrderIndex : 0
            };

            // Создаём ответы
            foreach (var a in _answers)
            {
                if (!string.IsNullOrWhiteSpace(a.Text))
                {
                    Question.Answers.Add(new Answer
                    {
                        Id = Guid.NewGuid(),
                        Text = a.Text.Trim(),
                        IsCorrect = a.IsCorrect,
                        QuestionId = Question.Id
                    });
                }
            }

            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }

    public class AnswerItem : DependencyObject
    {
        public string Text { get; set; }
        public bool IsCorrect { get; set; }
    }
}

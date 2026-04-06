using InteractiveT.Core.Models;
using InteractiveT.Infrastructure.Data;
using System;
using System.Windows;

namespace InteractiveTWPF
{
    public partial class AddClassWindow : Window
    {
        public bool Success { get; private set; }

        public AddClassWindow()
        {
            InitializeComponent();
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var className = ClassNameTextBox.Text.Trim();
            var gradeText = GradeLevelTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(className))
            {
                MessageBox.Show("Введите название класса.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int gradeLevel;
            if (!int.TryParse(gradeText, out gradeLevel) || gradeLevel < 1 || gradeLevel > 12)
            {
                MessageBox.Show("Уровень класса должен быть числом от 1 до 12.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var context = new ApplicationDbContext();

                var cls = new Class
                {
                    Name = className,
                    GradeLevel = gradeLevel
                };

                context.Classes.Add(cls);
                await context.SaveChangesAsync();

                Success = true;
                MessageBox.Show(string.Format("Класс \"{0}\" успешно добавлен!", className), "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка добавления класса: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}

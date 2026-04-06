using InteractiveT.Core.Models;
using InteractiveT.Infrastructure.Data;
using System;
using System.Windows;

namespace InteractiveTWPF
{
    public partial class AddSubjectWindow : Window
    {
        public bool Success { get; private set; }

        public AddSubjectWindow()
        {
            InitializeComponent();
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var name = SubjectNameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Введите название предмета.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var context = new ApplicationDbContext();

                var subject = new Subject { Name = name };
                context.Subjects.Add(subject);
                await context.SaveChangesAsync();

                Success = true;
                MessageBox.Show(string.Format("Предмет \"{0}\" успешно добавлен!", name), "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка добавления предмета: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}

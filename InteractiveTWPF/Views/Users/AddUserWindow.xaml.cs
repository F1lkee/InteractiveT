using InteractiveT.Core.Enum;
using InteractiveT.Core.Models;
using InteractiveT.Infrastructure.Data;
using InteractiveT.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace InteractiveTWPF
{
    public partial class AddUserWindow : Window
    {
        public bool Success { get; private set; }

        public AddUserWindow()
        {
            InitializeComponent();
            LoadClasses();
            UpdateClassVisibility();
        }

        private void LoadClasses()
        {
            try
            {
                var context = new ApplicationDbContext();
                var classes = context.Classes.OrderBy(c => c.GradeLevel).ThenBy(c => c.Name).ToList();
                ClassComboBox.ItemsSource = classes;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки классов: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateClassVisibility()
        {
            var role = UserRole.Student;
            if (RoleComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                role = (UserRole)int.Parse(item.Tag.ToString());
            }

            if (ClassSelectionPanel != null)
            {
                ClassSelectionPanel.Visibility = role == UserRole.Student ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void RoleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateClassVisibility();
        }

        private void AddClassButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new AddClassWindow();
            window.Owner = this;
            window.ShowDialog();

            if (window.Success)
            {
                LoadClasses();
                if (ClassComboBox.Items.Count > 0)
                    ClassComboBox.SelectedIndex = ClassComboBox.Items.Count - 1;
            }
        }

        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            var fullName = FullNameTextBox.Text.Trim();
            var login = LoginTextBox.Text.Trim();
            var password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(fullName))
            {
                MessageBox.Show("Введите ФИО пользователя.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(login))
            {
                MessageBox.Show("Введите логин пользователя.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Введите пароль пользователя.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var role = UserRole.Student;
            if (RoleComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                role = (UserRole)int.Parse(item.Tag.ToString());
            }

            // Проверяем, что для ученика выбран класс
            if (role == UserRole.Student && ClassComboBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите класс для ученика.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var isActive = IsActiveCheckBox.IsChecked == true;

            try
            {
                var context = new ApplicationDbContext();
                var authService = App.ServiceProvider.GetService(typeof(AuthService)) as AuthService;

                var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Login == login);
                if (existingUser != null)
                {
                    MessageBox.Show("Пользователь с таким логином уже существует.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var user = await authService.CreateUserAsync(fullName, login, password, role);
                user.IsActive = isActive;

                // Привязываем ученика к классу
                if (role == UserRole.Student && ClassComboBox.SelectedItem is Class selectedClass)
                {
                    var userClass = new UserClass
                    {
                        UserId = user.Id,
                        ClassId = selectedClass.Id
                    };
                    context.UserClasses.Add(userClass);
                }

                await context.SaveChangesAsync();

                Success = true;
                MessageBox.Show(string.Format("Пользователь \"{0}\" успешно создан!", fullName), "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка создания пользователя: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}

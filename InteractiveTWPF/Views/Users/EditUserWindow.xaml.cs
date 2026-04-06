using InteractiveT.Core.Enum;
using InteractiveT.Core.Models;
using InteractiveT.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace InteractiveTWPF
{
    public partial class EditUserWindow : Window
    {
        private readonly User _user;
        private ApplicationDbContext _context;

        public EditUserWindow(User user)
        {
            InitializeComponent();
            _user = user;
            _context = new ApplicationDbContext();

            FullNameTextBox.Text = user.FullName;
            LoginTextBox.Text = user.Login;
            IsActiveCheckBox.IsChecked = user.IsActive;

            // Устанавливаем роль
            int roleIndex = (int)user.Role;
            for (int i = 0; i < RoleComboBox.Items.Count; i++)
            {
                if (RoleComboBox.Items[i] is ComboBoxItem item && item.Tag != null)
                {
                    int tag = int.Parse(item.Tag.ToString());
                    if (tag == roleIndex)
                    {
                        RoleComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }

            // Загружаем классы
            LoadClasses();

            // Загружаем текущий класс ученика
            if (user.Role == UserRole.Student)
            {
                var userClass = _context.UserClasses
                    .Include(uc => uc.Class)
                    .FirstOrDefault(uc => uc.UserId == user.Id);

                if (userClass != null && userClass.Class != null)
                {
                    ClassComboBox.SelectedItem = userClass.Class;
                }
            }

            UpdateClassVisibility();
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

        private void UpdateClassVisibility()
        {
            var role = UserRole.Student;
            if (RoleComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                role = (UserRole)int.Parse(item.Tag.ToString());
            }

            ClassSelectionPanel.Visibility = role == UserRole.Student ? Visibility.Visible : Visibility.Collapsed;
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

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var fullName = FullNameTextBox.Text.Trim();
            var login = LoginTextBox.Text.Trim();

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

            // Проверяем уникальность логина (исключая текущего пользователя)
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Login == login && u.Id != _user.Id);
            if (existingUser != null)
            {
                MessageBox.Show("Пользователь с таким логином уже существует.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
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

            _user.FullName = fullName;
            _user.Login = login;
            _user.Role = role;
            _user.IsActive = IsActiveCheckBox.IsChecked == true;

            try
            {
                // Если роль сменилась на ученика или у ученика изменился класс
                if (role == UserRole.Student)
                {
                    // Удаляем старую привязку
                    var oldUserClass = await _context.UserClasses.FirstOrDefaultAsync(uc => uc.UserId == _user.Id);
                    if (oldUserClass != null)
                    {
                        _context.UserClasses.Remove(oldUserClass);
                    }

                    // Добавляем новую привязку
                    if (ClassComboBox.SelectedItem is Class selectedClass)
                    {
                        var newUserClass = new UserClass
                        {
                            UserId = _user.Id,
                            ClassId = selectedClass.Id
                        };
                        _context.UserClasses.Add(newUserClass);
                    }
                }
                else
                {
                    // Если роль не ученик, удаляем привязку к классу
                    var oldUserClass = await _context.UserClasses.FirstOrDefaultAsync(uc => uc.UserId == _user.Id);
                    if (oldUserClass != null)
                    {
                        _context.UserClasses.Remove(oldUserClass);
                    }
                }

                _context.Users.Update(_user);
                await _context.SaveChangesAsync();

                MessageBox.Show("Пользователь успешно обновлён!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка обновления: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}

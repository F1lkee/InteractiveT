using InteractiveT.Infrastructure.Data;
using InteractiveT.Infrastructure.Services;
using InteractiveT.Core.Enum; // Убедитесь, что этот using есть для UserRole
using InteractiveTWPF;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace InteractiveTWPF
{
    public partial class App : Application
    {
        private static IServiceProvider _serviceProvider;
        public static IServiceProvider ServiceProvider => _serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<AuthService>();
            services.AddScoped<AIService>();

            _serviceProvider = services.BuildServiceProvider();

            // Запускаем создание базы и тестовых данных
            InitializeDatabaseAsync();

            var mainWindow = new MainWindow();
            mainWindow.Show();
        }

        private async void InitializeDatabaseAsync()
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    await context.Database.EnsureCreatedAsync();

                    var authService = scope.ServiceProvider.GetRequiredService<AuthService>();

                    // Добавляем тестовых пользователей, если таблица пуста
                    if (!context.Users.Any())
                    {
                        // 1. Администратор
                        await authService.CreateUserAsync("Администратор", "admin", "admin123", UserRole.Admin);

                        // 2. Преподаватель
                        await authService.CreateUserAsync("Иванов Иван (Учитель)", "teacher", "teacher123", UserRole.Teacher);

                        // 3. Студент
                        await authService.CreateUserAsync("Петров Петр (Студент)", "student", "student123", UserRole.Student);

                        // (Опционально) Еще один студент для тестов
                        await authService.CreateUserAsync("Сидоров Сидор", "test", "test", UserRole.Student);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации данных: {ex.Message}", "БД", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

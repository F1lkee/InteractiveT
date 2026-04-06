using InteractiveT.Core.Enum;
using InteractiveT.Core.Models;
using InteractiveT.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace InteractiveT.Infrastructure.Services
{
    public class AuthService
    {
        private readonly ApplicationDbContext _context;
        private User _currentUser;

        public AuthService(ApplicationDbContext context)
        {
            _context = context;
        }

        public User CurrentUser
        {
            get { return _currentUser; }
        }

        public async Task<bool> LoginAsync(string login, string password)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Login == login && u.IsActive);

            if (user == null) return false;

            var passwordHash = HashPassword(password);
            if (user.PasswordHash != passwordHash) return false;

            _currentUser = user;
            return true;
        }

        public void Logout()
        {
            _currentUser = null;
        }

        public async Task<User> CreateUserAsync(string fullName, string login, string password, UserRole role)
        {
            var exists = await _context.Users.AnyAsync(u => u.Login == login);
            if (exists)
                throw new Exception("Пользователь с таким логином уже существует");

            var user = new User
            {
                FullName = fullName,
                Login = login,
                PasswordHash = HashPassword(password),
                Role = role
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public static string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }
    }
}

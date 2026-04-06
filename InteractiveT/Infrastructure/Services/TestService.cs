using InteractiveT.Core.Models;
using InteractiveT.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InteractiveT.Infrastructure.Services
{
    public class TestService
    {
        private readonly ApplicationDbContext _context;

        public TestService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Получает все доступные тесты для ученика (тесты предметов, к которым он привязан)
        /// </summary>
        public async Task<List<Test>> GetAvailableTestsForStudentAsync(Guid studentId)
        {
            var availableTests = await _context.Tests
                .Where(t => t.IsPublished &&
                            t.Subject.StudentAssignments.Any(sa => sa.UserId == studentId))
                .Include(t => t.Subject)
                .Include(t => t.Questions)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return availableTests;
        }

        /// <summary>
        /// Привязать ученика к предмету
        /// </summary>
        public async Task<UserSubject> AssignStudentToSubjectAsync(Guid studentId, Guid subjectId)
        {
            var existing = await _context.UserSubjects
                .FirstOrDefaultAsync(us => us.UserId == studentId && us.SubjectId == subjectId);

            if (existing != null)
                return existing;

            var userSubject = new UserSubject
            {
                UserId = studentId,
                SubjectId = subjectId,
                AssignedAt = DateTime.UtcNow
            };

            _context.UserSubjects.Add(userSubject);
            await _context.SaveChangesAsync();

            return userSubject;
        }

        /// <summary>
        /// Отвязать ученика от предмета
        /// </summary>
        public async Task RemoveStudentFromSubjectAsync(Guid studentId, Guid subjectId)
        {
            var userSubject = await _context.UserSubjects
                .FirstOrDefaultAsync(us => us.UserId == studentId && us.SubjectId == subjectId);

            if (userSubject != null)
            {
                _context.UserSubjects.Remove(userSubject);
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Привязать тест к предмету
        /// </summary>
        public async Task<Test> AssignTestToSubjectAsync(Guid testId, Guid subjectId)
        {
            var test = await _context.Tests.FindAsync(testId);
            if (test == null)
                throw new Exception("Тест не найден");

            test.SubjectId = subjectId;
            test.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return test;
        }
    }
}

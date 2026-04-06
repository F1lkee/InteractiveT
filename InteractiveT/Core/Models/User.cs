using InteractiveT.Core.Enum;
using InteractiveT.Core.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace InteractiveT.Core.Models
{
    public class User
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Login { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public UserRole Role { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        
        public virtual ICollection<UserClass> UserClasses { get; set; } = new List<UserClass>();
        public virtual ICollection<UserSubject> SubjectAssignments { get; set; } = new List<UserSubject>();
        public virtual ICollection<TeacherSubjectClass> TeacherAssignments { get; set; } = new List<TeacherSubjectClass>();
        public virtual ICollection<Test> CreatedTests { get; set; } = new List<Test>();
        public virtual ICollection<TestAttempt> Attempts { get; set; } = new List<TestAttempt>();
    }
}

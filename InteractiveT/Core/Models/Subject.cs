using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace InteractiveT.Core.Models
{
    public class Subject
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<TeacherSubjectClass> TeacherAssignments { get; set; } = new List<TeacherSubjectClass>();
        public virtual ICollection<UserSubject> StudentAssignments { get; set; } = new List<UserSubject>();
        public virtual ICollection<Test> Tests { get; set; } = new List<Test>();
    }
}

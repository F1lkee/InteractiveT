using InteractiveT.Core.Enum;
using InteractiveT.Core.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InteractiveT.Core.Models
{
    public class Test
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; }

        public Guid SubjectId { get; set; }
        public virtual Subject Subject { get; set; } = null;

        public Guid? ClassId { get; set; }
        public virtual Class Class { get; set; } = null;

        public Guid AuthorId { get; set; }
        public virtual User Author { get; set; } = null;

        
        public TestAccessMode AccessMode { get; set; } = TestAccessMode.Instant;
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string AccessPassword { get; set; }

        
        public int? TimeLimitSeconds { get; set; }
        public int AttemptsLimit { get; set; } = 1;
        public bool ShuffleQuestions { get; set; } = false;
        public bool ShuffleAnswers { get; set; } = false;
        public bool ShowResultsImmediately { get; set; } = true;
        public double? PassingThreshold { get; set; } // 0.8 для 80%

        public bool IsPublished { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        
        public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
        public virtual ICollection<TestAttempt> Attempts { get; set; } = new List<TestAttempt>();
    }
}

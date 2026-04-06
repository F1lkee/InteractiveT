using InteractiveT.Core.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InteractiveT.Core.Models
{
    public class TestAttempt
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null;

        public Guid TestId { get; set; }
        public virtual Test Test { get; set; } = null;

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        public double Score { get; set; } = 0;
        public double MaxScore { get; set; } = 0;
        public bool IsPassed { get; set; } = false;
        public bool IsCompleted { get; set; } = false;

        public TimeSpan? TimeSpent { get; set; }

        
        public virtual ICollection<StudentAnswer> Answers { get; set; } = new List<StudentAnswer>();
    }
}

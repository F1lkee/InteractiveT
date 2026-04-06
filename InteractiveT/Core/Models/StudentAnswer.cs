using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InteractiveT.Core.Models
{
    public class StudentAnswer
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid AttemptId { get; set; }
        public virtual TestAttempt Attempt { get; set; } = null;

        public Guid QuestionId { get; set; }
        public virtual Question Question { get; set; } = null;

        public Guid? SelectedAnswerId { get; set; }
        public virtual Answer SelectedAnswer { get; set; }

        public string TextAnswer { get; set; }

        public bool IsCorrect { get; set; } = false;
        public int PointsEarned { get; set; } = 0;

        public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;
    }
}

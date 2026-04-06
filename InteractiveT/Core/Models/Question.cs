using InteractiveT.Core.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace InteractiveT.Core.Models
{
    public class Question
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TestId { get; set; }
        public virtual Test Test { get; set; } = null;

        [Required]
        public string Text { get; set; } = string.Empty;

        public string ImageData { get; set; }

        public QuestionType Type { get; set; } = QuestionType.SingleChoice;

        public int OrderIndex { get; set; } = 0;

        public int Points { get; set; } = 1;

        public string Explanation { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


        public virtual ICollection<Answer> Answers { get; set; } = new List<Answer>();
        public virtual ICollection<StudentAnswer> StudentAnswers { get; set; } = new List<StudentAnswer>();
    }
}

using System;
using System.ComponentModel.DataAnnotations;

namespace InteractiveT.Core.Models
{
    public class UserSubject
    {
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null;

        public Guid SubjectId { get; set; }
        public virtual Subject Subject { get; set; } = null;

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }
}

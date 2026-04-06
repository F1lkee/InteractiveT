using InteractiveT.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InteractiveT.Core.Models
{
    public class UserClass
    {
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null;

        public Guid ClassId { get; set; }
        public virtual Class Class { get; set; } = null;

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }
}

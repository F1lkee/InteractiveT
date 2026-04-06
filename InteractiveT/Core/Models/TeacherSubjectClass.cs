using InteractiveT.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InteractiveT.Core.Models
{
    public class TeacherSubjectClass
    {
        public Guid TeacherId { get; set; }
        public virtual User Teacher { get; set; } = null;

        public Guid SubjectId { get; set; }
        public virtual Subject Subject { get; set; } = null;

        public Guid ClassId { get; set; }
        public virtual Class Class { get; set; } = null;
    }
}

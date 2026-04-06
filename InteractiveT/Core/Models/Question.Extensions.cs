using InteractiveT.Core.Models;

namespace InteractiveT.Core.Models
{
    public partial class Question
    {
        public string HasImage
        {
            get { return string.IsNullOrEmpty(ImageData) ? "— Нет" : "✓ Да"; }
        }
    }
}

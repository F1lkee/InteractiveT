using InteractiveT.Infrastructure.Services;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace InteractiveTWPF.Converters
{
    public class QuestionTypeDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is GeneratedQuestion)
            {
                var q = value as GeneratedQuestion;
                switch (q.Type)
                {
                    case InteractiveT.Core.Enum.QuestionType.SingleChoice:
                        return "Один правильный ответ";
                    case InteractiveT.Core.Enum.QuestionType.MultipleChoice:
                        return "Несколько правильных ответов";
                    case InteractiveT.Core.Enum.QuestionType.TextInput:
                        return "Текстовый ответ";
                    case InteractiveT.Core.Enum.QuestionType.Matching:
                        return "Соответствие";
                    default:
                        return "Неизвестный тип";
                }
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class AnswersDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var q = value as GeneratedQuestion;
            if (q != null && q.Answers != null && q.Answers.Any())
            {
                var correctAnswers = q.Answers.Where(a => a.IsCorrect).Select(a => a.Text);
                var count = correctAnswers.Count();
                return "Правильн" + (count == 1 ? "ый" : "ые") + ": " + string.Join(", ", correctAnswers);
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ScoreDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is InteractiveT.Core.Models.TestAttempt attempt)
            {
                if (attempt.MaxScore > 0)
                {
                    var percent = (attempt.Score / attempt.MaxScore) * 100;
                    return string.Format("{0:F0}% ({1:F0}/{2:F0})", percent, attempt.Score, attempt.MaxScore);
                }
                return "0%";
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is InteractiveT.Core.Models.TestAttempt attempt)
            {
                if (!attempt.IsCompleted)
                    return "В процессе";
                if (attempt.IsPassed)
                    return "✓ Сдан";
                return "✗ Не сдан";
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

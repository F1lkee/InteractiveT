using System;
using System.Globalization;
using System.Windows.Data;

namespace InteractiveTWPF.Converters
{
    public class ScoreDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int score)
            {
                return $"{score} баллов";
            }
            return value?.ToString() ?? "0 баллов";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
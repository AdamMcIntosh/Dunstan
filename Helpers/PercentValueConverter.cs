using Microsoft.UI.Xaml.Data;

namespace Dustan.Helpers;

/// <summary>Formats slider values as whole percents for thumb tooltips (e.g. 28 → "28%").</summary>
public sealed class PercentValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var number = value switch
        {
            double d => d,
            float f => f,
            int i => i,
            _ => 0d
        };
        return $"{Math.Clamp((int)Math.Round(number), 0, 100)}%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

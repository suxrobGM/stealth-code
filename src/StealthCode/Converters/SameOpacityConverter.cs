using System.Globalization;
using Avalonia.Data.Converters;

namespace StealthCode.Converters;

/// <summary>Reports whether an opacity preset is the one currently in use.</summary>
public sealed class SameOpacityConverter : IMultiValueConverter
{
    /// <summary>The instance XAML binds to.</summary>
    public static SameOpacityConverter Instance { get; } = new();

    /// <summary>Compares the preset in values[0] with the current opacity in values[1].</summary>
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => values.Count == 2
           && values[0] is double preset
           && values[1] is double current
           && Math.Abs(preset - current) < 0.001;
}

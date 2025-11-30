using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace NLAM.App.Converters;

public class BoolToOutlineBrushConverter : IValueConverter
{
    public static readonly BoolToOutlineBrushConverter Instance = new();

    private static readonly SolidColorBrush SelectedBrush = new(Colors.White);
    private static readonly SolidColorBrush UnselectedBrush = new(Color.FromArgb(80, 255, 255, 255));

    static BoolToOutlineBrushConverter()
    {
        SelectedBrush.Freeze();
        UnselectedBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isSelected && isSelected)
        {
            return SelectedBrush;
        }
        return UnselectedBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToOutlineThicknessConverter : IValueConverter
{
    public static readonly BoolToOutlineThicknessConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isSelected && isSelected)
        {
            return new Thickness(3);
        }
        return new Thickness(1);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

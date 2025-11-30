using System.Globalization;
using System.Windows.Data;

namespace NLAM.App.Converters;

public class TimeToPixelsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // Values: [time, zoomLevel, viewportWidth, totalLength]
        if (values.Length >= 4 && 
            values[0] is double time && 
            values[1] is double zoom &&
            values[2] is double viewportWidth &&
            values[3] is double totalLength)
        {
            if (totalLength <= 0 || viewportWidth <= 0) return 0.0;
            
            // Base: at 1% zoom (0.01), entire timeline fits in viewport
            double basePixelsPerSecond = viewportWidth / totalLength;
            
            // At 100% zoom, consistent detail level
            double targetPixelsPerSecond = 100.0;
            double zoomFactor = (zoom - 0.01) / (1.0 - 0.01);
            double maxZoomPps = Math.Max(basePixelsPerSecond * 100.0, targetPixelsPerSecond);
            double pixelsPerSecond = basePixelsPerSecond + zoomFactor * (maxZoomPps - basePixelsPerSecond);
            
            return time * pixelsPerSecond;
        }
        
        // Fallback for 2-value binding (legacy)
        if (values.Length == 2 && values[0] is double t && values[1] is double z)
        {
            return t * z * 100;
        }
        
        return 0.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

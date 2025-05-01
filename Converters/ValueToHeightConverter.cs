using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace RightClickVolume;

public class ValueToHeightConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if(values.Length == 2 && values[0] is double sliderValue && values[1] is Slider slider && slider.ActualHeight > 0 && !double.IsNaN(slider.ActualHeight) && !double.IsInfinity(slider.ActualHeight))
        {
            double trackHeight = slider.ActualHeight;
            trackHeight = Math.Max(0, trackHeight - 18);
            double range = slider.Maximum - slider.Minimum;
            if(range <= 0) return 0.0;
            double proportion = (sliderValue - slider.Minimum) / range;
            return Math.Max(0.0, proportion * trackHeight);
        }
        return 0.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
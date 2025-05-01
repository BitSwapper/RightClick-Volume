using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace RightClickVolume;

public class SliderValueToHeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if(value is double sliderValue && parameter is Slider slider)
        {
            double trackHeight = slider.ActualHeight;
            if(trackHeight <= 0 || double.IsNaN(trackHeight) || double.IsInfinity(trackHeight)) return 0;

            double range = slider.Maximum - slider.Minimum;
            if(range <= 0) return 0;

            double proportion = (sliderValue - slider.Minimum) / range;
            return Math.Max(0, proportion * trackHeight);
        }
        return 0;
    }


    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

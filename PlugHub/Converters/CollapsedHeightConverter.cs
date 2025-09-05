using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace PlugHub.Converters
{
    /// <summary>
    /// Converts a boolean collapsed state to a height value for UI elements, enabling show/hide behavior via height binding.
    /// </summary>
    /// <remarks>
    /// Returns <see cref="CollapsedHeight"/> (default 0) when <c>true</c>, or <see cref="double.NaN"/> (auto/visible) when <c>false</c>.
    /// Useful for collapsing controls in Avalonia layouts by binding to their <c>Height</c> property.
    /// </remarks>
    public class CollapsedHeightConverter : IValueConverter
    {
        /// <summary>
        /// Gets or sets the height to use when the control is collapsed. Defaults to 0.
        /// </summary>
        public double CollapsedHeight { get; set; } = 0;

        /// <summary>
        /// Converts a boolean value to a height: <see cref="CollapsedHeight"/> if <c>true</c>, otherwise <see cref="double.NaN"/>.
        /// </summary>
        /// <param name="value">The collapsed state as a boolean.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">Optional converter parameter.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>
        /// <see cref="CollapsedHeight"/> if collapsed; otherwise, <see cref="double.NaN"/>.
        /// </returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isCollapsed)
            {
                return isCollapsed ? this.CollapsedHeight : double.NaN;
            }
            return double.NaN;
        }

        /// <summary>
        /// Not supported. Throws <see cref="NotImplementedException"/>.
        /// </summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
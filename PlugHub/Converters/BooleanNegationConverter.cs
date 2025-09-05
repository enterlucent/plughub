using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace PlugHub.Converters
{
    /// <summary>
    /// Converts a boolean value to its logical negation for use in Avalonia data bindings.
    /// </summary>
    /// <remarks>
    /// Useful in MVVM scenarios where a view requires the inverse of a boolean property, such as toggling visibility or enabling/disabling controls.
    /// </remarks>
    public class BooleanNegationConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to its negated value.
        /// </summary>
        /// <param name="value">The input value to negate. Expected to be a boolean.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">Optional converter parameter (not used).</param>
        /// <param name="culture">The culture to use in the converter (not used).</param>
        /// <returns>
        /// <c>true</c> if <paramref name="value"/> is <c>false</c>; <c>false</c> if <paramref name="value"/> is <c>true</c>;
        /// otherwise, <c>false</c> if the input is not a boolean.
        /// </returns>
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool booleanValue)
            {
                return !booleanValue;
            }
            return false;
        }

        /// <summary>
        /// Not supported. Throws <see cref="NotImplementedException"/>.
        /// </summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
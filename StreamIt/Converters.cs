using System;
using System.Globalization;
using System.Windows.Data;

namespace Com.Josh2112.StreamIt.Converters
{
    public abstract class TrueFalseConverter : IValueConverter
    {
        public object TrueValue { get; set; } = true;
        public object FalseValue { get; set; } = false;

        protected abstract bool Convert( object value, object parameter );

        public object Convert( object value, Type targetType, object parameter, CultureInfo culture ) =>
            Convert( value, parameter ) ? TrueValue : FalseValue;

        public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture ) =>
            throw new NotImplementedException();
    }

    public class StringIsNullOrWhitespaceConverter : TrueFalseConverter
    {
        protected override bool Convert( object value, object parameter ) => string.IsNullOrWhiteSpace( (string?)value );
    }

    public class MediaStateMatchConverter : TrueFalseConverter
    {
        protected override bool Convert( object value, object parameter )
        {
            if( Enum.TryParse( value?.ToString(), out MediaStates v ) && Enum.IsDefined( v ) &&
                Enum.TryParse( parameter?.ToString(), out MediaStates p ) && Enum.IsDefined( p ) )
                return v == p;
            return false;
        }

    }

    public class DisplayNameToLetterIconConverter : IValueConverter
    {
        public object Convert( object value, Type targetType, object parameter, CultureInfo culture ) =>
            value?.ToString()?.ToUpper()[..2] ?? string.Empty;

        public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture ) =>
            throw new NotImplementedException();
    }
}

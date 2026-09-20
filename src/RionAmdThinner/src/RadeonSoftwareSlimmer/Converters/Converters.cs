using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RadeonSoftwareSlimmer.Converters
{
    public sealed class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => !(value is true);
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => !(value is true);
    }

    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public bool Collapse { get; set; } = true;
        public bool Invert { get; set; }

        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            bool b = value is true;
            if (Invert) b = !b;
            return b ? Visibility.Visible : (Collapse ? Visibility.Collapsed : Visibility.Hidden);
        }

        public object ConvertBack(object value, Type t, object p, CultureInfo c) => value is Visibility.Visible;
    }

    public sealed class NullToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            bool has = value != null;
            if (value is string s) has = !string.IsNullOrEmpty(s);
            if (Invert) has = !has;
            return has ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    public sealed class EmptyStringToVisibilityConverter : IValueConverter
    {
        /// <summary>When true, empty string -> Visible (use for placeholder text).</summary>
        public bool ShowWhenEmpty { get; set; }

        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            bool empty = string.IsNullOrEmpty(value as string);
            bool visible = ShowWhenEmpty ? empty : !empty;
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    /// <summary>Two-way enum &lt;-&gt; bool for radio/toggle groups. ConverterParameter = enum member name.</summary>
    public sealed class EnumToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value?.ToString() == p as string;

        public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
            value is true && p is string s ? Enum.Parse(t.IsEnum ? t : Nullable.GetUnderlyingType(t) ?? t, s) : Binding.DoNothing;
    }

    /// <summary>Zero -> Collapsed, non-zero -> Visible. Invert flips it.</summary>
    public sealed class CountToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            long n = 0;
            if (value != null) long.TryParse(value.ToString(), out n);
            bool visible = n != 0;
            if (Invert) visible = !visible;
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }
}

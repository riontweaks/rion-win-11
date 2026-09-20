using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace GlassKit;

/// <summary>
/// WPF has no letter-spacing property. For short uppercase labels — which is where tracking
/// actually earns its keep — re-emitting the string with thin spaces (U+2009) between characters
/// gets most of the way there for free. Set <c>Tracking.Text</c> instead of <c>Text</c>.
/// </summary>
public static class Tracking
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(Tracking), new PropertyMetadata(null, OnTextChanged));

    public static void SetText(DependencyObject o, string v) => o.SetValue(TextProperty, v);
    public static string GetText(DependencyObject o) => (string)o.GetValue(TextProperty);

    private static void OnTextChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        if (o is not TextBlock tb) return;
        string s = e.NewValue as string ?? "";
        StringBuilder sb = new(s.Length * 2);
        for (int i = 0; i < s.Length; i++)
        {
            if (i > 0) sb.Append('\u2009');
            sb.Append(s[i]);
        }
        tb.Text = sb.ToString();
    }
}

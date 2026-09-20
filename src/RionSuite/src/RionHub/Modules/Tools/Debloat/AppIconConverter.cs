using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RionHub.Modules.Tools.Debloat;

public sealed class AppIconConverter : IValueConverter
{
    private readonly Dictionary<string, ImageSource?> cache = new(StringComparer.OrdinalIgnoreCase);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file, int index, IntPtr[] large, IntPtr[] small, uint count);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value as string ?? "";
        if (cache.TryGetValue(text, out var cached)) return cached;
        ImageSource? result = null;
        try
        {
            string path = Environment.ExpandEnvironmentVariables(text.Trim());
            int index = 0;
            int comma = path.LastIndexOf(',');
            if (comma >= 0 && int.TryParse(path[(comma + 1)..], out index)) path = path[..comma];
            path = path.Trim('"');
            // Only local installed assets; never resolve a network icon path.
            if (Path.IsPathFullyQualified(path) && !path.StartsWith(@"\\") && File.Exists(path))
            {
                if (Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".ico")
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(path); bitmap.DecodePixelWidth = 64; bitmap.EndInit();
                    bitmap.Freeze(); result = bitmap;
                }
                else
                {
                    var large = new IntPtr[1]; var small = new IntPtr[1];
                    try
                    {
                        ExtractIconEx(path, index, large, small, 1);
                        var handle = large[0] != IntPtr.Zero ? large[0] : small[0];
                        if (handle != IntPtr.Zero)
                        {
                            var bitmap = Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                            bitmap.Freeze(); result = bitmap;
                        }
                    }
                    finally { if (large[0] != IntPtr.Zero) DestroyIcon(large[0]); if (small[0] != IntPtr.Zero) DestroyIcon(small[0]); }
                }
            }
        }
        catch { /* Missing or malformed publisher icons must not break inventory. */ }
        // Retry missing artwork on refresh; installation or access may have changed.
        if (result != null) cache[text] = result;
        return result;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

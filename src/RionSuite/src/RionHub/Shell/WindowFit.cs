using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace RionHub.Shell;

public static class WindowFit
{
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    public static Size SizeFor(Rect work, double width, double height) => new(Math.Min(width, Math.Max(1, work.Width - 24)), Math.Min(height, Math.Max(1, work.Height - 24)));
    internal static void Apply(Window window, double width, double height)
    {
        Rect work = SystemParameters.WorkArea;
        var handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero)
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(MonitorFromWindow(handle, 2), ref info))
            {
                var transform = PresentationSource.FromVisual(window)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
                work = new Rect(transform.Transform(new Point(info.Work.Left, info.Work.Top)), transform.Transform(new Point(info.Work.Right, info.Work.Bottom)));
            }
        }
        var size = SizeFor(work, width, height);
        window.MinWidth = Math.Min(window.MinWidth, size.Width);
        window.MinHeight = Math.Min(window.MinHeight, size.Height);
        window.Width = size.Width; window.Height = size.Height;
        window.Left = work.Left + (work.Width - size.Width) / 2;
        window.Top = work.Top + (work.Height - size.Height) / 2;
    }
}

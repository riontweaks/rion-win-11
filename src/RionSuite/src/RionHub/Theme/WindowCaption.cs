using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace RionHub.Theme;

public static class WindowCaption
{
    public static void Apply(Window window)
    {
        if(window.WindowStyle==WindowStyle.None)return;
        var handle=new WindowInteropHelper(window).Handle;
        if(handle==IntPtr.Zero)return;
        int dark=1;
        DwmSetWindowAttribute(handle,20,ref dark,4);
        SetColor(35,"Bg1");SetColor(36,"Text");
        void SetColor(int attribute,string key)
        {
            if(window.TryFindResource(key) is not SolidColorBrush brush)return;
            var color=brush.Color;int value=color.R|(color.G<<8)|(color.B<<16);
            DwmSetWindowAttribute(handle,attribute,ref value,4);
        }
    }
    public static void Attach(Window window)=>window.SourceInitialized+=(_,_)=>Apply(window);
    [DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(IntPtr handle,int attribute,ref int value,int size);
}

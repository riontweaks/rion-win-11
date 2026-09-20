using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace RadeonSoftwareSlimmer.Views
{
    public partial class HelpLink : UserControl
    {
        public static readonly DependencyProperty LinkProperty =
            DependencyProperty.Register(nameof(Link), typeof(Uri), typeof(HelpLink));

        public HelpLink()
        {
            InitializeComponent();
        }

        public Uri Link
        {
            get { return (Uri)GetValue(LinkProperty); }
            set
            {
                SetValue(LinkProperty, value);
                btnLink.ToolTip = "Open the wiki page for this topic: " + value?.AbsoluteUri;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "WPF event handlers cannot be static.")]
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (Link == null)
                return;

            ProcessStartInfo startInfo = new ProcessStartInfo(Link.AbsoluteUri) { UseShellExecute = true };
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
            }

            e.Handled = true;
        }
    }
}

using System.Windows.Controls;

namespace RadeonSoftwareSlimmer.Views
{
    /// <summary>
    /// The driver-tool content pane (step header + step body + Back/Next + message strip),
    /// hosted inside AMD GPU Manager. DataContext is the shared <c>WizardViewModel</c>; the
    /// step rail lives in the merged module's second sidebar, not here.
    /// </summary>
    public partial class DriverToolContentView : UserControl
    {
        public DriverToolContentView() => InitializeComponent();
    }
}

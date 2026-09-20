using System;
using System.IO.Abstractions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using RadeonSoftwareSlimmer.ViewModels;

namespace RadeonSoftwareSlimmer.Views
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public partial class PreInstallView : UserControl
    {
        private PreInstallViewModel _viewModel;

        /// <summary>Raised when the user clicks "Run installer". The host (InstallerView) handles
        /// the watch-and-apply flow; if nobody subscribes we just run Setup.exe.</summary>
        public event EventHandler RunInstallerRequested;

        public PreInstallView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Use the view model supplied by the host (InstallerSection.Pre) if there is one.
            _viewModel = DataContext as PreInstallViewModel;
            if (_viewModel == null)
            {
                _viewModel = new PreInstallViewModel(new FileSystem());
                DataContext = _viewModel;
            }
            flpWizard.SelectedIndex = (int)_viewModel.FlipViewIndex;
        }

        private async Task UpdateWizardIndexAsync()
        {
            flpWizard.SelectedIndex = (int)_viewModel.FlipViewIndex;

            if (_viewModel.FlipViewIndex == PreInstallViewModel.WizardIndex.ExtractingInstaller)
            {
                await _viewModel.ExtractInstallerFilesAsync();
                await UpdateWizardIndexAsync();
            }

            if (_viewModel.FlipViewIndex == PreInstallViewModel.WizardIndex.ModifyInstaller)
            {
                _viewModel.ReadFromExtractedInstaller();
            }
        }


        private async void btnSkip0_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SkipInstallFile();
            await UpdateWizardIndexAsync();
        }

        private void btnInstallerFileBrowse_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.BrowseForInstallerFile();
        }

        private async void btnNext0_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ValidateInstallerFile();
            await UpdateWizardIndexAsync();
        }

        private async void btnDownloadLatest_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.DownloadLatestDriverAsync();
            await UpdateWizardIndexAsync();
        }

        private async void btnDownloadSeries_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.DownloadGpuSeriesPackageAsync();
            await UpdateWizardIndexAsync();
        }


        private async void btnBack1_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Back();
            await UpdateWizardIndexAsync();
        }

        private void btnExtractLocatonBrowse_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.BrowseForExtractLocation();
        }

        private async void btnNext1_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ValidateExtractLocation();
            await UpdateWizardIndexAsync();
        }


        private void btnModifyInstallerFiles_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ModifyInstaller();
        }

        private void btnResetToDefault_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ResetInstallerToDefaults();
        }

        private void btnRunInstaller_Click(object sender, RoutedEventArgs e)
        {
            if (RunInstallerRequested != null)
                RunInstallerRequested(this, EventArgs.Empty);
            else
                _viewModel.RunRadeonSoftwareSetup();
        }

        private void btnSaveModified_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SaveModifiedInstaller();
        }

        private void btnRunCleanup_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.RunAmdCleanupUtility();
        }

        private async void btnNewInstaller_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SelectNewInstaller();
            await UpdateWizardIndexAsync();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "WPF event handlers cannot be static.")]
        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = true;
        }


        private void btnPackageSelectAll_Click(object sender, RoutedEventArgs e) => _viewModel.Packages_SetAll(true);
        private void btnPackageSelectNone_Click(object sender, RoutedEventArgs e) => _viewModel.Packages_SetAll(false);
        private void btnScheduledTaskSelectAll_Click(object sender, RoutedEventArgs e) => _viewModel.ScheduledTask_SetAll(true);
        private void btnScheduledTaskSelectNone_Click(object sender, RoutedEventArgs e) => _viewModel.ScheduledTask_SetAll(false);
        private void btnDisplayComponentsSelectAll_Click(object sender, RoutedEventArgs e) => _viewModel.DisplayComponents_SetAll(true);
        private void btnDisplayComponentsSelectNone_Click(object sender, RoutedEventArgs e) => _viewModel.DisplayComponents_SetAll(false);
    }
}

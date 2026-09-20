using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using NvidiaDriverTool.Services;
using RadeonSoftwareSlimmer.Optimize;
using RadeonSoftwareSlimmer.Services;
using RadeonSoftwareSlimmer.ViewModels;
namespace NvidiaDriverTool.ViewModels
{
    public sealed class InstallStepViewModel : NvidiaWizardStepViewModel
    {
        public InstallStepViewModel(NvidiaWizardViewModel wizard) : base(wizard, NvidiaWizardStep.Install, "Install", "")
        { RunCommand = new AsyncRelayCommand(RunAsync, () => !IsRunning && !IsComplete && Wizard.GpuIsNvidia); }
        public ObservableCollection<string> LogLines { get; } = new();
        private bool _isRunning;
        public bool IsRunning { get => _isRunning; private set { if (Set(ref _isRunning, value)) { NotifyAdvanceChanged(); RunCommand.RaiseCanExecuteChanged(); } } }
        private string _error = "";
        public string Error { get => _error; private set => Set(ref _error, value); }
        public AsyncRelayCommand RunCommand { get; }
        private string _status = "Ready to install.";
        public string Status { get => _status; private set => Set(ref _status, value); }
        private string _elapsed = "";
        public string Elapsed { get => _elapsed; private set => Set(ref _elapsed, value); }
        private double? _percent;
        public double Percent => _percent ?? 0;
        public bool IsIndeterminate => !_percent.HasValue;
        private void ReportInstaller(AmdInstallerStatus ui)
        {
            if (!ui.WindowFound) return;
            if (!string.IsNullOrWhiteSpace(ui.Text)) Status = ui.Text;
            _percent = ui.Percent;
            Raise(nameof(Percent)); Raise(nameof(IsIndeterminate));
        }
        private static bool NvidiaAppRunning()
        {
            var processes = Process.GetProcessesByName("NVIDIA app");
            try { return processes.Length > 0; }
            finally { foreach (var process in processes) process.Dispose(); }
        }
        public override bool CanAdvance => !IsRunning && IsComplete;
        public override bool CanGoBack => !IsRunning && !IsComplete;
        public override void OnEnter()
        {
            Error = Wizard.GpuIsNvidia ? "" : "No NVIDIA GPU detected. Package inspection is available; installation requires an NVIDIA GPU.";
            RunCommand.RaiseCanExecuteChanged();
        }
        private void Log(string line) => Application.Current.Dispatcher.Invoke(() => LogLines.Add(line));
        private async Task RunAsync()
        {
            if (IsRunning || IsComplete || !Wizard.GpuIsNvidia) return;
            IsRunning = true; Error = ""; LogLines.Clear();
            try
            {
                if (Wizard.ExtractedPath == null) throw new InvalidOperationException("Analyze a package first.");
                string setup = Path.Combine(Wizard.ExtractedPath, "setup.exe");
                if (!NvidiaInstallPlan.IsTrusted(await Task.Run(() => Wizard.Validator.Inspect(setup))))
                    throw new InvalidOperationException("setup.exe does not have a valid NVIDIA signature.");
                var prepared = await NvidiaPackagePreparation.PrepareAsync(Wizard.ExtractedPath, Wizard.ExcludedComponents, Wizard.InstallationOptions);
                string arguments = prepared.Arguments;
                string logPath = Path.Combine(Wizard.ExtractedPath, "RionInstallLogs");
                Directory.CreateDirectory(logPath);
                arguments += " -log:\"" + logPath + "\" -loglevel:6";
                File.WriteAllText(Path.Combine(Wizard.ExtractedPath, "Rion-install-plan.txt"), arguments);
                Log(await Task.Run(() => RestorePoint.TryCreate("Rion NVIDIA driver installation"))
                    ? "Restore point created." : "System Restore point unavailable.");
                Log("Running NVIDIA setup. Logs: " + logPath);
                Status = "NVIDIA setup is installing the selected packages…";
                var clock = Stopwatch.StartNew();
                bool appWasRunning = NvidiaAppRunning();
                var install = ShellRunner.RunAsync(setup, arguments, Log, timeoutMs: 0);
                while (!install.IsCompleted)
                {
                    ReportInstaller(await Task.Run(AmdInstallerUiReader.ReadNvidia));
                    bool appRunning = NvidiaAppRunning();
                    if (appRunning && !appWasRunning)
                    {
                        Status = "NVIDIA app opened — checking the installed driver…";
                        var current = await Task.Run(() => new HardwareInspector().Inspect("10DE"));
                        Log(DriverValidation.IsReady(current, "10DE")
                            ? "NVIDIA app launched; driver is healthy. Waiting for setup to finish before post-install changes."
                            : "NVIDIA app launched; driver validation is still pending.");
                    }
                    appWasRunning = appRunning;
                    Elapsed = $"Elapsed {clock.Elapsed:mm\\:ss}";
                    await Task.WhenAny(install, Task.Delay(1000));
                }
                var result = await install;
                while ((await Task.Run(AmdInstallerUiReader.ReadNvidia)) is { WindowFound: true } window)
                {
                    ReportInstaller(window);
                    await Task.Delay(1000);
                }
                if (result.ExitCode != 0 && result.ExitCode != 1)
                    throw new InvalidOperationException($"Installer exited with {result.ExitCode}. See {logPath}");
                Log(result.ExitCode == 1 ? "Installer succeeded; restart required." : "Installer reported success.");
                Status = "NVIDIA installer closed — validating the installed driver…";
                RadeonSoftwareSlimmer.Models.HardwareInfo detected = null;
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    detected = await Task.Run(() => new HardwareInspector().Inspect("10DE"));
                    if (DriverValidation.IsReady(detected, "10DE")) break;
                    Elapsed = $"Validating · {attempt + 1}/30";
                    await Task.Delay(1000);
                }
                if (!DriverValidation.IsReady(detected, "10DE"))
                    throw new InvalidOperationException("NVIDIA driver validation failed. Restart Windows if required, then retry. Post-install changes were not applied.");
                Log("Validated installed driver: " + detected.DriverVersion);
                
                Wizard.RefreshMonitor();
                Status = "Driver validated. Opening post-install tools.";
                IsComplete = true;
            }
            catch (Exception ex) { Error = ex.Message; Status = "Installation needs attention."; }
            finally { IsRunning = false; }
            if (IsComplete && Wizard.Current == this) Wizard.NextCommand.Execute(null);
        }
    }
}

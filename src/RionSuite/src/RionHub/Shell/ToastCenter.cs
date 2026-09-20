using System.Windows.Threading;

namespace RionHub.Shell;

/// <summary>
/// Decoupled toast pub/sub: any code in RionHub (viewmodels living in Modules\* that have no
/// reference back to <see cref="ShellViewModel"/>) can call <see cref="Show"/> or
/// <see cref="ShowProgress"/>; the shell subscribes once at startup and renders the queue as a
/// self-dismissing overlay.
/// </summary>
public static class ToastCenter
{
    public static event Action<string, bool>? Requested;
    public static event Action<ProgressToastVm>? ProgressRequested;

    public static void Show(string message, bool isError = false) => Requested?.Invoke(message, isError);

    /// <summary>Starts a toast that mutates in place as an operation runs — the returned handle's
    /// Report/Complete/Fail update it live instead of firing a new toast each time.</summary>
    public static IProgressToastHandle ShowProgress(string initialMessage)
    {
        var vm = new ProgressToastVm(initialMessage);
        ProgressRequested?.Invoke(vm);
        return new ProgressToastHandle(vm);
    }
}

public interface IProgressToastHandle
{
    void Report(int current, int total, string? message = null);
    void Complete(string finalMessage);
    void Fail(string finalMessage);
}

internal sealed class ProgressToastHandle : IProgressToastHandle
{
    private readonly ProgressToastVm _vm;
    internal ProgressToastHandle(ProgressToastVm vm) => _vm = vm;
    public void Report(int current, int total, string? message = null) => _vm.Report(current, total, message);
    public void Complete(string finalMessage) => _vm.Finish(finalMessage, isError: false);
    public void Fail(string finalMessage) => _vm.Finish(finalMessage, isError: true);
}

/// <summary>One toast entry. Plain data — nothing on it changes after creation, so it doesn't
/// need to be an ObservableObject.</summary>
public sealed class ToastVm
{
    public required string Message { get; init; }
    public bool IsError { get; init; }
}

/// <summary>One progress-reporting toast entry — mutates in place while an operation runs (the
/// current/total/message update live, same visual container as <see cref="ToastVm"/>), then
/// behaves like a normal self-dismissing toast once Complete/Fail is called. Property changes may
/// arrive from any thread (background work reports progress); marshals to the UI thread itself,
/// same shape as RadeonSoftwareSlimmer.Optimize.OptimizeMonitor's OnUi helper.</summary>
public sealed class ProgressToastVm : ObservableObject
{
    private readonly Dispatcher _ui = Dispatcher.CurrentDispatcher;

    private bool _isError;
    public bool IsError { get => _isError; private set => Set(ref _isError, value); }

    private string _message;
    public string Message { get => _message; private set => Set(ref _message, value); }

    private int _current;
    public int Current { get => _current; private set => Set(ref _current, value); }

    private int _total;
    public int Total { get => _total; private set => Set(ref _total, value); }

    /// <summary>True once Total is known and > 0 — bound to the progress bar's IsIndeterminate
    /// (inverted).</summary>
    public bool HasDeterminateProgress => _total > 0;

    public double Percent => _total > 0 ? Math.Clamp(_current * 100.0 / _total, 0, 100) : 0;

    /// <summary>True while running; false once Complete()/Fail() is called — at that point the
    /// toast looks like a plain ToastVm (progress row collapses) and the shell starts its
    /// auto-dismiss timer.</summary>
    private bool _isRunning = true;
    public bool IsRunning { get => _isRunning; private set => Set(ref _isRunning, value); }

    internal ProgressToastVm(string initialMessage) => _message = initialMessage;

    internal void Report(int current, int total, string? message)
    {
        OnUi(() =>
        {
            Current = current;
            Total = total;
            if (message != null) Message = message;
            Raise(nameof(HasDeterminateProgress));
            Raise(nameof(Percent));
        });
    }

    internal void Finish(string finalMessage, bool isError)
    {
        OnUi(() =>
        {
            Message = finalMessage;
            IsError = isError;
            IsRunning = false;
        });
    }

    private void OnUi(Action a)
    {
        if (_ui.CheckAccess()) a();
        else _ui.BeginInvoke(a);
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RadeonSoftwareSlimmer.Optimize
{
    /// <summary>Runs a console tool / PowerShell script and streams stdout+stderr lines to a callback.</summary>
    public static class ShellRunner
    {
        public static string PowerShellPath
        {
            get
            {
                string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "WindowsPowerShell", "v1.0", "powershell.exe");
                return RequireExecutable(p, "Windows PowerShell is missing from its Windows system location. Repair Windows PowerShell before running this action.");
            }
        }

        internal static string RequireExecutable(string path, string message)
        {
            if (!Path.IsPathFullyQualified(path) || !File.Exists(path))
                throw new FileNotFoundException(message, path);
            return path;
        }

        public sealed class Result
        {
            public int ExitCode { get; set; }
            public string Output { get; set; } = string.Empty;
            public bool TimedOut { get; set; }
        }

        public static Task<Result> RunAsync(string fileName, string arguments,
            Action<string> onLine, int timeoutMs = 15 * 60 * 1000, CancellationToken ct = default)
        {
            return RunAsync(fileName, arguments, null, onLine, timeoutMs, ct);
        }

        /// <summary>Passes each argument separately. The runtime applies Windows quoting, so an
        /// argument carrying quotes, spaces or shell metacharacters reaches the child verbatim
        /// without the caller escaping it into one command string.</summary>
        public static Task<Result> RunAsync(string fileName, IEnumerable<string> argumentList,
            Action<string> onLine, int timeoutMs = 15 * 60 * 1000, CancellationToken ct = default)
        {
            return RunAsync(fileName, null, argumentList, onLine, timeoutMs, ct);
        }

        private static Task<Result> RunAsync(string fileName, string arguments, IEnumerable<string> argumentList,
            Action<string> onLine, int timeoutMs, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var sb = new StringBuilder();
                var pendingLines = new ConcurrentQueue<string>();
                int closedStreams = 0;
                Exception callbackError = null;

                // Process events can arrive concurrently. Only this worker invokes the
                // consumer and writes the combined transcript.
                void DrainOutput(int limit = int.MaxValue)
                {
                    while (limit-- > 0 && pendingLines.TryDequeue(out string line))
                    {
                        sb.AppendLine(line);
                        string trimmed = line.Trim();
                        if (trimmed.Length == 0 || callbackError != null) continue;
                        try { onLine?.Invoke(trimmed); }
                        catch (Exception ex) { callbackError = ex; }
                    }
                }

                var psi = new ProcessStartInfo(fileName)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };
                if (argumentList != null)
                    foreach (string argument in argumentList) psi.ArgumentList.Add(argument);
                else if (arguments != null) psi.Arguments = arguments;

                using (var proc = new Process { StartInfo = psi, EnableRaisingEvents = true })
                {
                    void Handle(string line)
                    {
                        if (line == null) Interlocked.Increment(ref closedStreams);
                        else pendingLines.Enqueue(line);
                    }

                    proc.OutputDataReceived += (_, e) => Handle(e.Data);
                    proc.ErrorDataReceived += (_, e) => Handle(e.Data);

                    try { proc.Start(); }
                    catch (Exception ex) { return new Result { ExitCode = -1, Output = ex.Message }; }

                    var elapsed = Stopwatch.StartNew();
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();

                    bool cancelled = false;
                    bool timedOut = false;
                    while (!proc.WaitForExit(50))
                    {
                        DrainOutput(256);
                        cancelled = ct.IsCancellationRequested;
                        timedOut = !cancelled && timeoutMs > 0 && elapsed.ElapsedMilliseconds >= timeoutMs;
                        if (cancelled || timedOut) break;
                    }

                    bool exited = proc.HasExited;
                    if (!exited)
                    {
                        try { proc.Kill(entireProcessTree: true); }
                        catch (Exception ex)
                        {
                            sb.AppendLine("Could not stop the process: " + ex.Message);
                        }
                        exited = proc.WaitForExit(5000);
                        if (!exited) sb.AppendLine("Process termination was not confirmed. The operation may still be running.");
                    }

                    // A descendant can retain redirected pipe handles after the parent
                    // exits. Wait for queued output, with a bound even in that case.
                    var drainTime = Stopwatch.StartNew();
                    while (Volatile.Read(ref closedStreams) < 2 && drainTime.ElapsedMilliseconds < 5000)
                    {
                        DrainOutput(256);
                        Thread.Sleep(10);
                    }
                    bool outputComplete = Volatile.Read(ref closedStreams) == 2;
                    DrainOutput(pendingLines.Count);
                    if (!outputComplete) sb.AppendLine("Process output did not close; the transcript may be incomplete.");
                    if (callbackError != null) sb.AppendLine("Could not process command output: " + callbackError.Message);
                    if (cancelled) sb.AppendLine("Operation cancelled.");
                    if (timedOut) sb.AppendLine("Operation timed out.");

                    return new Result
                    {
                        ExitCode = exited && !cancelled && !timedOut && outputComplete && callbackError == null ? proc.ExitCode : -1,
                        Output = sb.ToString(),
                        TimedOut = timedOut
                    };
                }
            }, ct);
        }

        /// <summary>Largest script sent as plain text. CreateProcess accepts a 32767-character
        /// command line; the rest of the budget covers the quoted interpreter path and the flags.</summary>
        internal const int MaxInlineScriptChars = 32000;

        /// <summary>Runs a script through Windows PowerShell.
        ///
        /// The script travels as one plain -Command argument, quoted by the runtime, so the child
        /// receives the same text that appears in source with no decoding step in between. Execution
        /// policy does not apply to -Command, so this needs no policy override. A script starting with
        /// its own param() block must be wrapped by the caller in a script block, which is where param()
        /// is legal.
        ///
        /// Only a script too long for a command line falls back to UTF-16 -EncodedCommand transport,
        /// which no current caller reaches. Neither form disables AMSI: PowerShell submits the script
        /// to antimalware inspection either way.</summary>
        public static Task<Result> PowerShellAsync(string script, Action<string> onLine,
            int timeoutMs = 15 * 60 * 1000, CancellationToken ct = default)
        {
            if (script != null && script.Length <= MaxInlineScriptChars)
                return RunAsync(PowerShellPath,
                    new[] { "-NoProfile", "-NonInteractive", "-Command", script },
                    onLine, timeoutMs, ct);

            string enc = Convert.ToBase64String(Encoding.Unicode.GetBytes(script ?? string.Empty));
            return RunAsync(PowerShellPath,
                "-NoProfile -NonInteractive -EncodedCommand " + enc,
                onLine, timeoutMs, ct);
        }
    }
}


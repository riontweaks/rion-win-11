using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace RionHub.Features;

public sealed record UtilityEntry(string Name, string EntryPoint, string Icon);

public static class UtilityPackage
{
    public const string Url = "https://github.com/riontweaks/rion-win-11/releases/download/Files/Rion2.zip";
    public const string ArchiveHash = "6319CE6A4F893D7C8BBA4AA515884AD0438061A95A4A2011D47F1DE8EFAB334C";
    public static string DataRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RionWin11");
    public static string Root => Path.Combine(DataRoot, "Utilities", ArchiveHash[..12]);
    public static string Status { get; private set; } = "Utilities have not been prepared.";
    public static event Action? Changed;
    private static readonly SemaphoreSlim Gate = new(1, 1);
    public static readonly UtilityEntry[] Entries =
    [
        new("Cinebench R23", "CinebenchR23/Cinebench.exe", "Cinebench R23"),
        new("CoreCycler", "CoreCycler-v0.11.0.3/Run CoreCycler.bat", ""),
        new("CPU-Z", "cpu-z_3.01-en/cpuz_x64.exe", "CPU-Z"),
        new("RAM Test Pro", "RAMTestPro1.5.0/RAM Test Pro.exe", "RAM Test Pro"),
        new("HWiNFO", "HWiNFO64.exe", "HWiNFO"),
        new("SMU Debug Tool", "SMUDebugTool_v1.40/SMUDebugTool.exe", "SMU Debug Tool"),
        new("TestMem5 / TM5", "testmem/TM5.exe", "TestMem5"),
        new("Thaiphoon Burner", "thphn174/Thaiphoon.exe", "Thaiphoon Burner"),
        new("ZenTimings", "ZenTimings_v1.39/ZenTimings.exe", "ZenTimings")
    ];
    public static string Resolve(string relative) => SafeTarget(Root, relative);
    public static string SafeTarget(string root, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Contains(':')) throw new InvalidDataException("Absolute archive path rejected.");
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var result = Path.GetFullPath(Path.Combine(fullRoot, relative));
        if (!result.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Archive path escapes the utility folder.");
        return result;
    }
    private static void Report(string text) { Status = text; Changed?.Invoke(); }
    public static async Task PrepareAsync(CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        string? staging = null;
        try
        {
            if (File.Exists(Path.Combine(Root, ".complete")) && Entries.All(e => File.Exists(Resolve(e.EntryPoint))))
            { Report("Utilities ready. Existing settings and profiles preserved."); return; }
            if (Directory.Exists(Root)) throw new IOException("Incomplete utility folder retained. Choose a new cache location or recover its files before replacing it.");
            Directory.CreateDirectory(Path.GetDirectoryName(Root)!);
            staging = Root + ".staging-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(staging);
            var archive = Path.Combine(staging, "payload.zip");
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            using var response = await http.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            await using (var input = await response.Content.ReadAsStreamAsync(ct))
            await using (var output = File.Create(archive))
            {
                var buffer = new byte[1024 * 1024]; long bytes = 0; long last = -1; int read;
                while ((read = await input.ReadAsync(buffer, ct)) > 0)
                {
                    bytes += read;
                    if (bytes > 1024L * 1024 * 1024) throw new InvalidDataException("Utility archive exceeds the download limit.");
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                    long mb = bytes / 1048576;
                    if (mb != last) { last = mb; Report($"Preparing utilities: downloaded {mb} MB / {response.Content.Headers.ContentLength / 1048576} MB. Cancel is available in Utilities."); }
                }
            }
            await using (var input = File.OpenRead(archive))
                if (Convert.ToHexString(await SHA256.HashDataAsync(input, ct)) != ArchiveHash)
                    throw new InvalidDataException("Utility package checksum changed. The new release must be reviewed before use.");
            Report("Verified package. Extracting utilities…");
            var payload = Path.Combine(staging, "files"); Directory.CreateDirectory(payload);
            await Task.Run(() => Extract(archive, payload, ct), ct);
            foreach (var entry in Entries)
                if (!File.Exists(SafeTarget(payload, entry.EntryPoint))) throw new InvalidDataException("Package is missing " + entry.EntryPoint);
            await File.WriteAllTextAsync(Path.Combine(payload, ".complete"), ArchiveHash, ct);
            Directory.Move(payload, Root);
            Report("Utilities ready. Select a program to launch it.");
        }
        catch (OperationCanceledException) { Report("Utility preparation cancelled. Existing tools are unchanged."); throw; }
        catch (Exception e) { Report("Utility preparation failed: " + e.Message); throw; }
        finally
        {
            if (staging != null && Directory.Exists(staging)) Directory.Delete(staging, true);
            Gate.Release();
        }
    }
    public static void Extract(string archive, string destination, CancellationToken ct)
    {
        using var zip = ZipFile.OpenRead(archive);
        var allowed = Entries.Select(e => e.EntryPoint.Split('/')[0]).Append("scewin").Append("HWiNFO64.INI").ToHashSet(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var entry in zip.Entries)
        {
            ct.ThrowIfCancellationRequested();
            var name = entry.FullName.Replace('\\', '/');
            if (!name.StartsWith("Rion2/", StringComparison.Ordinal)) throw new InvalidDataException("Unexpected archive root.");
            name = name[6..]; if (name.Length == 0) continue;
            SafeTarget(destination, name);
            if (!allowed.Contains(name.Split('/')[0])) continue;
            if ((entry.ExternalAttributes >> 16 & 0xF000) == 0xA000) throw new InvalidDataException("Archive symlinks are not supported.");
            if (name.StartsWith("scewin/", StringComparison.OrdinalIgnoreCase) &&
                (name.EndsWith("nvram.txt", StringComparison.OrdinalIgnoreCase) || name.Contains("/profiles/", StringComparison.OrdinalIgnoreCase) || name.EndsWith("log-file.txt", StringComparison.OrdinalIgnoreCase))) continue;
            total += entry.Length; if (total > 4L * 1024 * 1024 * 1024) throw new InvalidDataException("Extraction limit exceeded.");
            string target = SafeTarget(destination, name);
            if (name.EndsWith('/')) { Directory.CreateDirectory(target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, false);
        }
    }
}

public static class CoreCyclerConfig
{
    public static readonly string[] Programs = ["PRIME95", "YCRUNCHER", "YCRUNCHER_OLD"];
    public static string Read(string text)
    {
        var (_, match) = Locate(text);
        return match.Groups["value"].Value.Trim().Replace("-", "").ToUpperInvariant();
    }
    private static (int Start, Match Match) Locate(string text)
    {
        var section = Regex.Match(text, @"(?ims)^\[General\][^\r\n]*\r?\n(?<body>.*?)(?=^\[|\z)");
        if (!section.Success) throw new InvalidDataException("Missing [General] section.");
        var matches = Regex.Matches(section.Groups["body"].Value, @"(?m)^(?<prefix>[ \t]*stressTestProgram[ \t]*=[ \t]*)(?<value>[^\r\n;#]+)");
        if (matches.Count != 1) throw new InvalidDataException("Expected one stressTestProgram setting.");
        return (section.Groups["body"].Index, matches[0]);
    }
    public static string Edit(string text, string program)
    {
        if (!Programs.Contains(program)) throw new ArgumentException("Unsupported stress test program.");
        if (Read(text) == program) return text;
        var (start, match) = Locate(text); var value = match.Groups["value"];
        var trailing = value.Value[value.Value.TrimEnd().Length..];
        return text[..(start + value.Index)] + program + trailing + text[(start + value.Index + value.Length)..];
    }
    public static void Save(string path, string program, string expectedHash)
    {
        var bytes = File.ReadAllBytes(path);
        if (Convert.ToHexString(SHA256.HashData(bytes)) != expectedHash) throw new IOException("CoreCycler config changed externally. Reload before saving.");
        bool bom = bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 });
        var encoding = new UTF8Encoding(false, true);
        string text = encoding.GetString(bytes.AsSpan(bom ? 3 : 0));
        string edited = Edit(text, program); if (edited == text) return;
        byte[] output = (bom ? new byte[] {239,187,191} : []).Concat(encoding.GetBytes(edited)).ToArray();
        var backup = path + ".rion-" + Guid.NewGuid().ToString("N") + ".bak";
        var temp = path + ".rion-tmp";
        File.WriteAllBytes(temp, output);
        File.Replace(temp, path, backup);
        if (Read(File.ReadAllText(path)) != program) throw new IOException("CoreCycler configuration verification failed.");
    }
}

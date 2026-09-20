using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using RadeonSoftwareSlimmer.Intefaces;

namespace RadeonSoftwareSlimmer.Optimize;

public static class MultimediaTweakCatalog
{
	public const string ProfilePath = "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile";

	public const string Documentation = "https://learn.microsoft.com/en-us/windows/win32/procthread/multimedia-class-scheduler-service";

	private const string ScopeNote = "Only threads explicitly registered with this task are affected. Changes can interrupt audio, capture or playback; restart Windows. No general FPS or latency gain is established.";

	public static IReadOnlyList<string> TaskNames { get; } = new string[7] { "Games", "Audio", "Capture", "Distribution", "Playback", "Pro Audio", "Window Manager" };

	public static IReadOnlyList<SystemTweak> All { get; } = Build();

	public static string TaskId(string task)
	{
		return task.ToLowerInvariant().Replace(' ', '-');
	}

	private static SystemTweak Number(string task, string key, string suffix, string title, string summary, params TweakPreset[] choices)
	{
		string path = "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\\Tasks\\" + task;
		return new SystemTweak
		{
			Id = "mmcss-" + TaskId(task) + "-" + suffix,
			Title = title,
			Summary = summary,
			Category = TweakCategory.Performance,
			Group = "Multimedia scheduling",
			Grade = TweakGrade.A,
			Source = "https://learn.microsoft.com/en-us/windows/win32/procthread/multimedia-class-scheduler-service",
			TradeOff = "Only threads explicitly registered with this task are affected. Changes can interrupt audio, capture or playback; restart Windows. No general FPS or latency gain is established.",
			ManualOnly = true,
			IndividualApplyOnly = true,
			RebootRequired = true,
			KeyPath = path,
			ValueName = key,
			Presets = choices,
			TargetValue = choices[0].Value,
			CheckApplySupport = delegate(IRegistry registry)
			{
				using IRegistryKey registryKey = registry.LocalMachine.OpenSubKey(path, writable: false);
				return (registryKey == null) ? "This multimedia task is not present. Rion will not create a task that applications have not registered." : null;
			},
			VerificationNote = "Configuration read back; application registration and runtime scheduling are not measured."
		};
	}

	private static SystemTweak Text(string task, string key, string suffix, string title, string summary, params string[] choices)
	{
		SystemTweak systemTweak = Number(task, key, suffix, title, summary, choices.Select((string s, int i) => new TweakPreset(s, i)).ToArray());
		systemTweak.ValueKind = RegistryValueKind.String;
		systemTweak.StringValues = choices.Select((string s, int i) => new { s, i }).ToDictionary(v => (long)v.i, v => v.s);
		return systemTweak;
	}

	private static IReadOnlyList<SystemTweak> Build()
	{
		List<SystemTweak> list = new List<SystemTweak>();
		foreach (string taskName in TaskNames)
		{
			list.Add(Number(taskName, "Priority", "priority", "Task priority", "Priority within the selected MMCSS task. High scheduling category always treats this as 2.", (from n in Enumerable.Range(1, 8)
				select new TweakPreset(n.ToString(), n)).ToArray()));
			list.Add(Number(taskName, "BackgroundPriority", "background-priority", "Background priority", "Priority used for registered background multimedia threads; does not set the process priority.", (from n in Enumerable.Range(1, 8)
				select new TweakPreset(n.ToString(), n)).ToArray()));
			list.Add(Text(taskName, "Scheduling Category", "category", "Scheduling category", "Select Low, Medium or High for registered threads. High is intended for professional audio and can delay other work.", "Medium", "Low", "High"));
			list.Add(Text(taskName, "Background Only", "background-only", "Background-only task", "True makes this a background task whose thread priority does not change with window focus.", "False", "True"));
			list.Add(Number(taskName, "Affinity", "affinity", "Remove task CPU restriction", "Use Windows processor placement instead of a task affinity mask. Zero means no task affinity restriction.", new TweakPreset("No task affinity restriction", 0L)));
			list.Add(Number(taskName, "Clock Rate", "clock-rate", "Scheduling clock hint", "A scheduling-granularity hint in 100-nanosecond units. Windows 7 and later provide no clock-rate guarantee; this does not set timer resolution.", new TweakPreset("1 millisecond hint", 10000L), new TweakPreset("2 millisecond hint", 20000L), new TweakPreset("10 millisecond hint", 100000L)));
		}
		list.Add(new SystemTweak
		{
			Id = "graphics-mpo-off",
			Title = "Disable multi-plane overlays",
			Category = TweakCategory.Graphics,
			Summary = "Disables MPO for display troubleshooting. Restart Windows after changing it.",
			TradeOff = "MPO normally reduces composition work and power use. Disabling it can increase GPU work; this is not a universal latency improvement.",
			Source = "https://nvidia.custhelp.com/app/answers/detail/a_id/5157",
			Grade = TweakGrade.A,
			ManualOnly = true,
			IndividualApplyOnly = true,
			RebootRequired = true,
			KeyPath = "SOFTWARE\\Microsoft\\Windows\\Dwm",
			ValueName = "OverlayTestMode",
			TargetValue = 5L,
			CheckApplySupport = delegate(IRegistry registry)
			{
				using IRegistryKey registryKey = registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", writable: false);
				int result;
				return (int.TryParse(registryKey?.GetValue("CurrentBuildNumber", null) as string, out result) && result >= 22000) ? null : "This control is exposed for Windows 11. The current Windows build could not be verified.";
			}
		});
		return list;
	}
}

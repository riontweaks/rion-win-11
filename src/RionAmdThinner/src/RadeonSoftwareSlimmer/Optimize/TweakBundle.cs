using System;
using System.Collections.Generic;
using System.Linq;

namespace RadeonSoftwareSlimmer.Optimize;

public sealed class TweakBundle
{
	public string Id { get; }

	public string Title { get; }

	public string Description { get; }

	public string Section { get; }

	public string Filter { get; }

	public IReadOnlyList<SystemTweak> Members { get; }

	public bool IsFeatured { get; set; }

	public int Count => Members.Count;

	public bool RebootRequired => Members.Any((SystemTweak m) => m.RebootRequired);

	public bool SecuritySensitive => Members.Any((SystemTweak m) => m.SecuritySensitive);

	public bool ManualOnly => Members.All((SystemTweak m) => m.ManualOnly);

	public bool IndividualApplyOnly => Members.Any((SystemTweak m) => m.IndividualApplyOnly);

	public bool IsAdjustable => Members.Any((SystemTweak m) => m.IsAdjustable);

	public bool HasMemberAdjustments
	{
		get
		{
			if (Members.Count > 1)
			{
				return IsAdjustable;
			}
			return false;
		}
	}

	public IReadOnlyList<TweakPreset> Presets
	{
		get
		{
			if (!IsAdjustable || HasMemberAdjustments)
			{
				return Array.Empty<TweakPreset>();
			}
			return Members[0].Presets;
		}
	}

	public IEnumerable<SystemTweak> Appliable => Members.Where((SystemTweak m) => string.IsNullOrEmpty(m.ApplyBlockedReason));

	public bool ApplyBlocked => !Appliable.Any();

	public string ApplyBlockedReason
	{
		get
		{
			if (!ApplyBlocked)
			{
				return null;
			}
			return Members.Select((SystemTweak m) => m.ApplyBlockedReason).First((string r) => !string.IsNullOrEmpty(r));
		}
	}

	public string TradeOff => string.Join(" ", (from m in Members
		select m.TradeOff into t
		where !string.IsNullOrWhiteSpace(t)
		select t).Distinct<string>(StringComparer.Ordinal));

	public TweakGrade Grade
	{
		get
		{
			if (Members.Count != 0)
			{
				return Members.Max((SystemTweak m) => m.Grade);
			}
			return TweakGrade.C;
		}
	}

	public IEnumerable<string> Sources => (from m in Members
		select m.Source into s
		where !string.IsNullOrWhiteSpace(s)
		select s).Distinct<string>(StringComparer.OrdinalIgnoreCase);

	public TweakBundle(string id, string title, string description, string section, string filter, IReadOnlyList<SystemTweak> members)
	{
		Id = id;
		Title = title;
		Description = description;
		Section = section;
		Filter = filter;
		Members = members ?? Array.Empty<SystemTweak>();
	}

	public bool Contains(string tweakId)
	{
		return Members.Any((SystemTweak m) => m.Id == tweakId);
	}
}

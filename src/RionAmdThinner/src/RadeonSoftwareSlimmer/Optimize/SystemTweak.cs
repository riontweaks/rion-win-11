using System;
using System.Collections.Generic;
using Microsoft.Win32;
using RadeonSoftwareSlimmer.Intefaces;

namespace RadeonSoftwareSlimmer.Optimize;

public sealed class SystemTweak
{
	public bool Experimental { get; set; }

	public bool ManualOnly { get; set; }

	public bool IndividualApplyOnly { get; set; }

	public bool SecuritySensitive { get; set; }

	public string Group { get; set; }

	public Func<IRegistry, string> CheckApplySupport { get; set; }

	public string VerificationNote { get; set; }

	public string SourceCommand { get; set; }

	public byte[] BinaryTargetValue { get; set; }

	public IReadOnlyDictionary<long, string> StringValues { get; set; }

	public bool DeleteRegistryValue { get; set; }

	public bool DeferInitialInspection { get; set; }

	public bool InspectOnLoad { get; set; }

	public Func<bool> HasSavedState { get; set; }

	public string TargetLabel
	{
		get
		{
			if (!DeleteRegistryValue)
			{
				if (BinaryTargetValue == null)
				{
					if (StringValues == null)
					{
						return EffectiveTarget.ToString();
					}
					return StringValues[EffectiveTarget];
				}
				return BitConverter.ToString(BinaryTargetValue).Replace("-", "");
			}
			return "Remove value";
		}
	}

	public string ApplyBlockedReason { get; set; }

	public string Id { get; set; }

	public string Title { get; set; }

	public string Summary { get; set; }

	public string TradeOff { get; set; }

	public TweakCategory Category { get; set; }

	public TweakGrade Grade { get; set; } = TweakGrade.C;

	public string Source { get; set; }

	public bool RebootRequired { get; set; }

	public TweakHive Hive { get; set; }

	public string KeyPath { get; set; }

	public string ValueName { get; set; }

	public RegistryValueKind ValueKind { get; set; } = RegistryValueKind.DWord;

	public long TargetValue { get; set; }

	public Func<long> DynamicTargetValue { get; set; }

	public long EffectiveTarget
	{
		get
		{
			if (DynamicTargetValue == null)
			{
				return TargetValue;
			}
			return DynamicTargetValue();
		}
	}

	public long? DefaultValue { get; set; }

	public Func<string> DynamicKeyPath { get; set; }

	public IReadOnlyList<TweakPreset> Presets { get; set; }

	public bool IsAdjustable
	{
		get
		{
			if (Presets != null)
			{
				return Presets.Count > 0;
			}
			return false;
		}
	}

	public string ApplyCommand { get; set; }

	public string RevertCommand { get; set; }

	public bool IsCommandBased => !string.IsNullOrEmpty(ApplyCommand);

	public Func<bool?> InspectCommandState { get; set; }

	public Func<bool> CustomApply { get; set; }

	public Func<bool> CustomRevert { get; set; }

	public bool IsCustom => CustomApply != null;

	public bool IsFeatured { get; set; }

	public string GradeLabel => Grade switch
	{
		TweakGrade.A => "A - Documented mechanism", 
		TweakGrade.B => "B - Community documented", 
		TweakGrade.C => "C - Benefit unverified", 
		_ => "F - Unsupported claim", 
	};
}

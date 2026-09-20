using System;
using System.Threading;

namespace RadeonSoftwareSlimmer.Optimize;

public static class TweakConsent
{
	private sealed class Scope : IDisposable
	{
		private bool disposed;

		public void Dispose()
		{
			if (!disposed)
			{
				disposed = true;
				depth.Value--;
			}
		}
	}

	private static readonly AsyncLocal<int> depth = new AsyncLocal<int>();

	public static Func<string, bool> BeforeApply { get; set; }

	public static bool Request(string operation)
	{
		if (depth.Value <= 0 && BeforeApply != null)
		{
			return BeforeApply(operation);
		}
		return true;
	}

	public static IDisposable ApprovedBatch()
	{
		depth.Value++;
		return new Scope();
	}
}

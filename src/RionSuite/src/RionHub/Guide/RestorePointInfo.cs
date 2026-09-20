using System;

namespace RionHub.Guide;

public sealed record RestorePointInfo(long Sequence, string Description, DateTimeOffset CreatedUtc);

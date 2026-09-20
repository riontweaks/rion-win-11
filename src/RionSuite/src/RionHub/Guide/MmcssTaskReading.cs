using System.Collections.Generic;

namespace RionHub.Guide;

public sealed record MmcssTaskReading(string Task, bool Present, IReadOnlyDictionary<string, object?> Values, string Error = "");

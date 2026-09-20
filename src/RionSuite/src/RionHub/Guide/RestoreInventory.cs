namespace RionHub.Guide;

public sealed record RestoreInventory(bool Known, RestorePointInfo[] Points, string Error);

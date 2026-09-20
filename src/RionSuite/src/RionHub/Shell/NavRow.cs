namespace RionHub;

/// <summary>
/// One row in the flattened sub-nav: either a clickable leaf, or a non-interactive section
/// header. The top tab strip picks the area, so this list is always flat under it.
/// </summary>
public sealed class NavRow
{
    public bool IsHeader { get; init; }
    public string Title { get; init; } = "";

    /// <summary>Null on header rows.</summary>
    public NavItem? Item { get; init; }

    public static NavRow Header(string title) => new() { IsHeader = true, Title = title };
    public static NavRow Leaf(NavItem item) => new() { Title = item.Title, Item = item };
}

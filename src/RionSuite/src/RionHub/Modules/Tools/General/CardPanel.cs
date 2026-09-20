using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace RionHub.Modules.Tools.General;

public sealed class CardPanel : Panel
{
    public static readonly DependencyProperty MinCardWidthProperty = DependencyProperty.Register(
        nameof(MinCardWidth), typeof(double), typeof(CardPanel),
        new FrameworkPropertyMetadata(340d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double MinCardWidth
    {
        get => (double)GetValue(MinCardWidthProperty);
        set => SetValue(MinCardWidthProperty, value);
    }

    public static readonly DependencyProperty GutterProperty = DependencyProperty.Register(
        nameof(Gutter), typeof(double), typeof(CardPanel),
        new FrameworkPropertyMetadata(12d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double Gutter
    {
        get => (double)GetValue(GutterProperty);
        set => SetValue(GutterProperty, value);
    }

    public int ColumnCount { get; private set; } = 1;

    private int ColumnsFor(double width)
    {
        if (double.IsInfinity(width) || double.IsNaN(width) || width <= 0) return 1;
        double min = Math.Max(1, MinCardWidth);
        int columns = (int)Math.Floor((width + Gutter) / (min + Gutter));
        return Math.Clamp(columns, 1, 3);
    }

    private UIElement[] VisibleChildren => InternalChildren.Cast<UIElement>()
        .Where(child => child.Visibility != Visibility.Collapsed).ToArray();

    protected override Size MeasureOverride(Size availableSize)
    {
        ColumnCount = ColumnsFor(availableSize.Width);
        double width = double.IsInfinity(availableSize.Width)
            ? MinCardWidth * ColumnCount + Gutter * (ColumnCount - 1) : availableSize.Width;
        double columnWidth = Math.Max(1, (width - Gutter * (ColumnCount - 1)) / ColumnCount);
        var children = VisibleChildren;
        double height = 0;
        for (int first = 0; first < children.Length; first += ColumnCount)
        {
            double rowHeight = 0;
            for (int i = first; i < Math.Min(first + ColumnCount, children.Length); i++)
            {
                children[i].Measure(new Size(columnWidth, double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, children[i].DesiredSize.Height);
            }
            height += rowHeight + Gutter;
        }
        return new Size(width, Math.Max(0, height - Gutter));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        ColumnCount = ColumnsFor(finalSize.Width);
        double columnWidth = Math.Max(1, (finalSize.Width - Gutter * (ColumnCount - 1)) / ColumnCount);
        var children = VisibleChildren;
        double y = 0;
        for (int first = 0; first < children.Length; first += ColumnCount)
        {
            double rowHeight = children.Skip(first).Take(ColumnCount).Max(child => child.DesiredSize.Height);
            for (int i = first; i < Math.Min(first + ColumnCount, children.Length); i++)
                children[i].Arrange(new Rect((i - first) * (columnWidth + Gutter), y, columnWidth, rowHeight));
            y += rowHeight + Gutter;
        }
        return finalSize;
    }
}

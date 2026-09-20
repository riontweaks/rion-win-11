using System;
using System.Windows;

namespace RionHub.Guide;

/// <summary>Bindable state for the guided-tour overlay bubble. Driven by ShellWindow.</summary>
public sealed class TourState : ObservableObject
{
    private bool _active;
    public bool Active { get => _active; set => Set(ref _active, value); }

    private string _heading = "";
    public string Heading { get => _heading; set => Set(ref _heading, value); }

    private string _body = "";
    public string Body { get => _body; set => Set(ref _body, value); }

    private string _progress = "";
    public string Progress { get => _progress; set => Set(ref _progress, value); }

    private string _nextLabel = "Next";
    public string NextLabel { get => _nextLabel; set => Set(ref _nextLabel, value); }

    private bool _canBack;
    public bool CanBack { get => _canBack; set => Set(ref _canBack, value); }

    private bool _hasSpotlight;
    public bool HasSpotlight { get => _hasSpotlight; set => Set(ref _hasSpotlight, value); }

    // spotlight ring geometry, in overlay coordinates
    private Thickness _ringMargin;
    public Thickness RingMargin { get => _ringMargin; set => Set(ref _ringMargin, value); }

    private double _ringWidth;
    public double RingWidth { get => _ringWidth; set => Set(ref _ringWidth, value); }

    private double _ringHeight;
    public double RingHeight { get => _ringHeight; set => Set(ref _ringHeight, value); }

    // bubble placement, in overlay coordinates
    private Thickness _bubbleMargin = new Thickness(0);
    public Thickness BubbleMargin { get => _bubbleMargin; set => Set(ref _bubbleMargin, value); }

    private HorizontalAlignment _bubbleHAlign = HorizontalAlignment.Center;
    public HorizontalAlignment BubbleHAlign { get => _bubbleHAlign; set => Set(ref _bubbleHAlign, value); }

    private VerticalAlignment _bubbleVAlign = VerticalAlignment.Center;
    public VerticalAlignment BubbleVAlign { get => _bubbleVAlign; set => Set(ref _bubbleVAlign, value); }

    public RelayCommand NextCommand { get; }
    public RelayCommand BackCommand { get; }
    public RelayCommand SkipCommand { get; }

    public TourState(Action next, Action back, Action skip)
    {
        NextCommand = new RelayCommand(next);
        BackCommand = new RelayCommand(back);
        SkipCommand = new RelayCommand(skip);
    }
}

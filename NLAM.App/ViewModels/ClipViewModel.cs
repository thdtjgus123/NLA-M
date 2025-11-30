using CommunityToolkit.Mvvm.ComponentModel;
using NLAM.App.Models;

namespace NLAM.App.ViewModels;

public partial class ClipViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private double _startTime;

    [ObservableProperty]
    private double _duration;

    [ObservableProperty]
    private string _color;

    [ObservableProperty]
    private MacroActionType _actionType;

    [ObservableProperty]
    private string _customScript = "";

    [ObservableProperty]
    private bool _isSelected;

    public ClipViewModel(string name, double startTime, double duration, string color = "#007ACC", MacroActionType actionType = MacroActionType.Wait)
    {
        Name = name;
        StartTime = startTime;
        Duration = duration;
        Color = color;
        ActionType = actionType;
    }
}

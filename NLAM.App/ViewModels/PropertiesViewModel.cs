using CommunityToolkit.Mvvm.ComponentModel;

namespace NLAM.App.ViewModels;

public partial class PropertiesViewModel : ObservableObject
{
    [ObservableProperty]
    private ClipViewModel? _selectedClip;

    public void SetSelectedClip(ClipViewModel? clip)
    {
        // Deselect previous clip
        if (SelectedClip != null)
        {
            SelectedClip.IsSelected = false;
        }

        SelectedClip = clip;

        // Select new clip
        if (clip != null)
        {
            clip.IsSelected = true;
        }
    }
}

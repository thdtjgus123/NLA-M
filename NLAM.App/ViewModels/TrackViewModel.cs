using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace NLAM.App.ViewModels;

public partial class TrackViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = "New Track";

    [ObservableProperty]
    private double _height = 50.0;

    public ObservableCollection<ClipViewModel> Clips { get; } = new();

    public TrackViewModel(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Check if a clip would collide with other clips at the given position
    /// </summary>
    public bool WouldCollide(ClipViewModel clip, double newStartTime, double? newDuration = null)
    {
        double duration = newDuration ?? clip.Duration;
        double endTime = newStartTime + duration;

        foreach (var other in Clips)
        {
            if (other == clip) continue;
            
            double otherEnd = other.StartTime + other.Duration;
            
            // Check for overlap
            if (newStartTime < otherEnd && endTime > other.StartTime)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Find the nearest valid position for a clip that doesn't collide
    /// </summary>
    public double FindNearestValidPosition(ClipViewModel clip, double desiredStartTime, double timelineLength)
    {
        if (!WouldCollide(clip, desiredStartTime))
        {
            return Math.Max(0, Math.Min(desiredStartTime, timelineLength - clip.Duration));
        }

        // Try to find a nearby valid position
        double searchRange = 10.0; // seconds
        double step = 0.1;

        for (double offset = step; offset <= searchRange; offset += step)
        {
            // Try moving right
            double rightPos = desiredStartTime + offset;
            if (rightPos + clip.Duration <= timelineLength && !WouldCollide(clip, rightPos))
            {
                return rightPos;
            }

            // Try moving left
            double leftPos = desiredStartTime - offset;
            if (leftPos >= 0 && !WouldCollide(clip, leftPos))
            {
                return leftPos;
            }
        }

        // Return original position if no valid position found
        return clip.StartTime;
    }

    /// <summary>
    /// Find maximum allowed duration for a clip without collision
    /// </summary>
    public double FindMaxDuration(ClipViewModel clip, double timelineLength)
    {
        double maxEnd = timelineLength;

        foreach (var other in Clips)
        {
            if (other == clip) continue;
            
            // If other clip is after this one, limit the max end
            if (other.StartTime > clip.StartTime)
            {
                maxEnd = Math.Min(maxEnd, other.StartTime);
            }
        }

        return maxEnd - clip.StartTime;
    }
}

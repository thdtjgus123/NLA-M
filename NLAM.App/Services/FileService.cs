using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NLAM.App.ViewModels;

namespace NLAM.App.Services;

public class FileService
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public void SaveTimeline(TimelineViewModel timeline, string filePath)
    {
        var data = new TimelineData
        {
            Tracks = timeline.Tracks.Select(t => new TrackData
            {
                Name = t.Name,
                Height = t.Height,
                Clips = t.Clips.Select(c => new ClipData
                {
                    Name = c.Name,
                    StartTime = c.StartTime,
                    Duration = c.Duration,
                    Color = c.Color,
                    ActionType = c.ActionType
                }).ToList()
            }).ToList()
        };

        var json = JsonSerializer.Serialize(data, _options);
        File.WriteAllText(filePath, json);
    }

    public void LoadTimeline(TimelineViewModel timeline, string filePath)
    {
        var json = File.ReadAllText(filePath);
        var data = JsonSerializer.Deserialize<TimelineData>(json, _options);

        if (data == null) return;

        timeline.Tracks.Clear();
        foreach (var trackData in data.Tracks)
        {
            var track = new TrackViewModel(trackData.Name) { Height = trackData.Height };
            foreach (var clipData in trackData.Clips)
            {
                track.Clips.Add(new ClipViewModel(
                    clipData.Name,
                    clipData.StartTime,
                    clipData.Duration,
                    clipData.Color,
                    clipData.ActionType
                ));
            }
            timeline.Tracks.Add(track);
        }
    }

    private class TimelineData
    {
        public List<TrackData> Tracks { get; set; } = new();
    }

    private class TrackData
    {
        public string Name { get; set; } = "";
        public double Height { get; set; }
        public List<ClipData> Clips { get; set; } = new();
    }

    private class ClipData
    {
        public string Name { get; set; } = "";
        public double StartTime { get; set; }
        public double Duration { get; set; }
        public string Color { get; set; } = "";
        public Models.MacroActionType ActionType { get; set; }
    }
}

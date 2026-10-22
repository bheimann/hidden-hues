using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace HiddenHues;

/// <summary>
/// Player settings in user://settings.cfg, which lives in the user's app data folder and is never committed.
/// </summary>
public static class Settings
{
    private const string FilePath = "user://settings.cfg";
    private const string EnvKey = "PIXABAY_API_KEY";

    /// <summary>The Pixabay API key. Falls back to the PIXABAY_API_KEY environment variable.</summary>
    public static string ApiKey
    {
        get
        {
            var saved = (string)Load().GetValue("pixabay", "api_key", "");
            return saved.Length > 0 ? saved : System.Environment.GetEnvironmentVariable(EnvKey) ?? "";
        }
        set => Save(config => config.SetValue("pixabay", "api_key", value.Trim()));
    }

    /// <summary>Pictures someone chose never to see again.</summary>
    public static IReadOnlyList<long> HiddenPictureIds
    {
        get => ((string)Load().GetValue("pixabay", "hidden_ids", ""))
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(long.Parse)
            .ToList();
        set => Save(config => config.SetValue("pixabay", "hidden_ids", string.Join(",", value)));
    }

    private static ConfigFile Load()
    {
        var config = new ConfigFile();
        config.Load(FilePath); // a missing file just means no settings yet
        return config;
    }

    private static void Save(Action<ConfigFile> change)
    {
        var config = Load();
        change(config);
        config.Save(FilePath);
    }
}

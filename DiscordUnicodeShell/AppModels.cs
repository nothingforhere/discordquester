using System.Text.Json.Serialization;

namespace DiscordUnicodeShell;

public sealed class AppSettings
{
    [JsonPropertyName("browser_mode")]
    public string BrowserMode { get; set; } = "headless";

    [JsonPropertyName("selected_profile")]
    public string SelectedProfile { get; set; } = string.Empty;

    [JsonPropertyName("last_page")]
    public string LastPage { get; set; } = "spoofer";

    [JsonPropertyName("buffer_minutes")]
    public int BufferMinutes { get; set; } = 2;

    [JsonPropertyName("auto_scan_after_spoofer")]
    public bool AutoScanAfterSpoofer { get; set; } = true;

    [JsonPropertyName("last_scan_source")]
    public string LastScanSource { get; set; } = "-";

    [JsonPropertyName("last_scan_mode")]
    public string LastScanMode { get; set; } = "Headless";

    [JsonPropertyName("last_scan_profile")]
    public string LastScanProfile { get; set; } = "-";

    [JsonPropertyName("last_scan_total")]
    public int LastScanTotal { get; set; }
}

public sealed class ProfileInfo
{
    [JsonPropertyName("directory")]
    public string Directory { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(UserName) ? Name : $"{Name} - {UserName}";
    }
}

public sealed class QuestInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("appId")]
    public string AppId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("appName")]
    public string AppName { get; set; } = string.Empty;

    [JsonPropertyName("progressText")]
    public string ProgressText { get; set; } = string.Empty;

    [JsonPropertyName("statusText")]
    public string StatusText { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("progressPercent")]
    public int? ProgressPercent { get; set; }

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }

    [JsonPropertyName("claimed")]
    public bool Claimed { get; set; }

    [JsonPropertyName("accepted")]
    public bool Accepted { get; set; }

    [JsonPropertyName("targetSeconds")]
    public int TargetSeconds { get; set; }

    [JsonPropertyName("progressSeconds")]
    public int ProgressSeconds { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    public bool Done => Completed || Claimed;
}

public sealed class ProfilesResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("profiles")]
    public List<ProfileInfo> Profiles { get; set; } = new();

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class ScrapeResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("quests")]
    public List<QuestInfo> Quests { get; set; } = new();

    [JsonPropertyName("profile")]
    public ProfileInfo? Profile { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

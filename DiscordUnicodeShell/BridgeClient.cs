using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace DiscordUnicodeShell;

public sealed class BridgeClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string ProjectRoot { get; }
    public string SettingsPath => Path.Combine(ProjectRoot, "settings.json");
    public string QuestBridgePath => Path.Combine(ProjectRoot, "quest_bridge.js");
    public string LogPath => Path.Combine(ProjectRoot, "discordunicode.log");
    public string NodeExecutablePath => ResolveNodeExecutablePath();

    public BridgeClient()
    {
        ProjectRoot = ResolveProjectRoot();
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        if (!File.Exists(SettingsPath))
        {
            return new AppSettings();
        }

        await using var stream = File.OpenRead(SettingsPath);
        return await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions) ?? new AppSettings();
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(SettingsPath, json, Encoding.UTF8);
    }

    public async Task<ProfilesResponse> GetProfilesAsync()
    {
        var json = await RunNodeAsync("profiles");
        return JsonSerializer.Deserialize<ProfilesResponse>(json, JsonOptions)
            ?? throw new InvalidOperationException("Profil JSON okunamadi.");
    }

    public async Task<ScrapeResponse> ScrapeQuestsAsync(string profileDirectory, bool headless)
    {
        var json = await RunNodeAsync("scrape", "--profile", profileDirectory, "--headless", headless ? "true" : "false");
        return JsonSerializer.Deserialize<ScrapeResponse>(json, JsonOptions)
            ?? throw new InvalidOperationException("Gorev JSON okunamadi.");
    }

    public async Task<string> ReadLogTailAsync(int maxLines = 250)
    {
        if (!File.Exists(LogPath))
        {
            return string.Empty;
        }

        var lines = await File.ReadAllLinesAsync(LogPath);
        return string.Join(Environment.NewLine, lines.TakeLast(Math.Max(1, maxLines)));
    }

    private async Task<string> RunNodeAsync(params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = NodeExecutablePath,
            WorkingDirectory = ProjectRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        startInfo.ArgumentList.Add(Path.GetFullPath(QuestBridgePath));
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(180));
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignore cleanup failures after timeout.
            }

            throw new InvalidOperationException("Node bridge zaman asimina ugradi.");
        }

        var stdout = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();

        if (string.IsNullOrWhiteSpace(stdout))
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? "Kopru yanit vermedi." : stderr);
        }

        JsonDocument payload;
        try
        {
            payload = JsonDocument.Parse(stdout);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Kopru yaniti okunamadi: {ex.Message}");
        }

        using (payload)
        {
            if (process.ExitCode != 0 || !payload.RootElement.TryGetProperty("ok", out var okElement) || !okElement.GetBoolean())
            {
                if (payload.RootElement.TryGetProperty("error", out var errorElement))
                {
                    var payloadError = errorElement.GetString();
                    if (!string.IsNullOrWhiteSpace(payloadError))
                    {
                        throw new InvalidOperationException(payloadError);
                    }
                }

                throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? "Islem basarisiz." : stderr);
            }
        }

        return stdout;
    }

    private string ResolveNodeExecutablePath()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("NODE_EXE_PATH"),
            Path.Combine(ProjectRoot, "node.exe"),
            Path.Combine(ProjectRoot, "nodejs", "node.exe"),
            Path.Combine(ProjectRoot, "node", "node.exe")
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
            {
                return candidate;
            }
        }

        return "node";
    }

    private static string ResolveProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var questBridgePath = Path.Combine(current.FullName, "quest_bridge.js");
            var settingsPath = Path.Combine(current.FullName, "settings.json");
            if (File.Exists(questBridgePath) && File.Exists(settingsPath))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("discordunicode proje kok dizini bulunamadi.");
    }
}

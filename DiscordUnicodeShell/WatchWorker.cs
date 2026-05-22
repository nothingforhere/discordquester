using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace DiscordUnicodeShell;

public sealed class WatchProgressEventArgs : EventArgs
{
    public WatchProgressEventArgs(string title, int remainingSeconds, string statusText, int? progressPercent)
    {
        Title = title;
        RemainingSeconds = remainingSeconds;
        StatusText = statusText;
        ProgressPercent = progressPercent;
    }

    public string Title { get; }
    public int RemainingSeconds { get; }
    public string StatusText { get; }
    public int? ProgressPercent { get; }
}

public sealed class WatchWorker
{
    private readonly string profileDirectory;
    private readonly bool headless;
    private readonly BridgeClient bridgeClient;
    private readonly CancellationTokenSource cts = new();
    private Process? nodeProcess;
    private Task? runTask;

    public event EventHandler<WatchProgressEventArgs>? ProgressChanged;
    public event EventHandler<string>? LogReceived;
    public event EventHandler? Finished;
    public event EventHandler<string>? Failed;

    public WatchWorker(string profileDirectory, bool headless, BridgeClient bridgeClient)
    {
        this.profileDirectory = profileDirectory;
        this.headless = headless;
        this.bridgeClient = bridgeClient;
    }

    public void Start()
    {
        runTask = Task.Run(RunAsync);
    }

    public void Stop()
    {
        cts.Cancel();
        try
        {
            if (nodeProcess is { HasExited: false })
            {
                nodeProcess.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Ignore cleanup failures
        }
    }

    private async Task RunAsync()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = bridgeClient.NodeExecutablePath,
                WorkingDirectory = bridgeClient.ProjectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            startInfo.ArgumentList.Add(Path.GetFullPath(bridgeClient.QuestBridgePath));
            startInfo.ArgumentList.Add("watch");
            startInfo.ArgumentList.Add("--profile");
            startInfo.ArgumentList.Add(profileDirectory);
            startInfo.ArgumentList.Add("--headless");
            startInfo.ArgumentList.Add(headless ? "true" : "false");

            nodeProcess = new Process { StartInfo = startInfo };
            nodeProcess.Start();

            var stdoutTask = ReadStdoutAsync(nodeProcess.StandardOutput, cts.Token);
            var stderrTask = ReadStderrAsync(nodeProcess.StandardError, cts.Token);

            await Task.WhenAll(stdoutTask, stderrTask);
            await nodeProcess.WaitForExitAsync(cts.Token);

            if (!cts.IsCancellationRequested)
            {
                if (nodeProcess.ExitCode == 0)
                {
                    Finished?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    Failed?.Invoke(this, $"Gorev izleme basarisiz oldu. Hata Kodu: {nodeProcess.ExitCode}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
        catch (Exception ex)
        {
            Failed?.Invoke(this, ex.Message);
        }
    }

    private async Task ReadStdoutAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;

            line = line.Trim();
            if (line.StartsWith("{") && line.EndsWith("}"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "progress")
                    {
                        var title = root.GetProperty("title").GetString() ?? "Izleme Gorevi";
                        var remainingSeconds = root.GetProperty("remainingSeconds").GetInt32();
                        var statusText = root.GetProperty("statusText").GetString() ?? "Aktif";
                        int? progressPercent = null;
                        if (root.TryGetProperty("progressPercent", out var pctProp) && pctProp.ValueKind != JsonValueKind.Null)
                        {
                            progressPercent = pctProp.GetInt32();
                        }

                        ProgressChanged?.Invoke(this, new WatchProgressEventArgs(title, remainingSeconds, statusText, progressPercent));
                    }
                }
                catch
                {
                    // If not a progress JSON, output as general log
                    LogReceived?.Invoke(this, line);
                }
            }
            else
            {
                LogReceived?.Invoke(this, line);
            }
        }
    }

    private async Task ReadStderrAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(line))
            {
                LogReceived?.Invoke(this, line.Trim());
            }
        }
    }
}

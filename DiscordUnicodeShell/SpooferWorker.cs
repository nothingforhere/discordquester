using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordUnicodeShell;

public sealed class SpooferProgressEventArgs : EventArgs
{
    public SpooferProgressEventArgs(int elapsedSeconds, int remainingSeconds)
    {
        ElapsedSeconds = elapsedSeconds;
        RemainingSeconds = remainingSeconds;
    }

    public int ElapsedSeconds { get; }
    public int RemainingSeconds { get; }
}

public sealed class SpooferWorker
{
    private readonly string clientId;
    private readonly int durationSeconds;
    private readonly string title;
    private readonly string appName;
    private readonly string processName;
    private readonly int customElapsedHours;
    private readonly CancellationTokenSource cts = new();
    private Task? runTask;

    public SpooferWorker(string clientId, int durationSeconds, string title, string appName, string processName, int customElapsedHours = 0)
    {
        this.clientId = clientId;
        this.durationSeconds = durationSeconds;
        this.title = string.IsNullOrWhiteSpace(title) ? $"App ID {clientId}" : title;
        this.appName = string.IsNullOrWhiteSpace(appName) ? this.title : appName;
        this.processName = string.IsNullOrWhiteSpace(processName) ? this.appName : processName;
        this.customElapsedHours = customElapsedHours;
    }

    public event EventHandler<SpooferProgressEventArgs>? ProgressChanged;
    public event EventHandler? Finished;
    public event EventHandler<string>? Failed;

    public void Start()
    {
        runTask = Task.Run(RunAsync);
    }

    public void Stop()
    {
        cts.Cancel();
    }

    private async Task RunAsync()
    {
        Process? helperProcess = null;
        string? buildDir = null;
        string? tempDir = null;

        try
        {
            var safeName = GetSafeProcessName();
            tempDir = Path.Combine(Path.GetTempPath(), "dummy");
            buildDir = Path.Combine(tempDir, $"{safeName}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(buildDir);

            var (exePath, publishDir) = await BuildHelperExeAsync(buildDir, safeName, cts.Token);

            helperProcess = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = publishDir,
                UseShellExecute = false,
                CreateNoWindow = false
            });

            if (helperProcess is null)
            {
                throw new InvalidOperationException("Gecici helper exe baslatilamadi.");
            }

            var stopwatch = Stopwatch.StartNew();
            while (!cts.IsCancellationRequested)
            {
                if (helperProcess.HasExited)
                {
                    break;
                }

                var elapsed = Math.Min(durationSeconds, (int)stopwatch.Elapsed.TotalSeconds);
                var remaining = Math.Max(0, durationSeconds - elapsed);
                ProgressChanged?.Invoke(this, new SpooferProgressEventArgs(elapsed, remaining));

                if (elapsed >= durationSeconds)
                {
                    break;
                }

                await Task.Delay(1000, cts.Token);
            }

            if (!cts.IsCancellationRequested)
            {
                Finished?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Failed?.Invoke(this, ex.Message);
        }
        finally
        {
            if (helperProcess is not null)
            {
                try
                {
                    if (!helperProcess.HasExited)
                    {
                        helperProcess.Kill(true);
                        helperProcess.WaitForExit(2000);
                    }
                }
                catch
                {
                }
                finally
                {
                    helperProcess.Dispose();
                }
            }

            if (buildDir is not null && Directory.Exists(buildDir))
            {
                await TryDeleteDirectoryAsync(buildDir);
            }

            if (tempDir is not null && Directory.Exists(tempDir))
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(tempDir).Any())
                    {
                        Directory.Delete(tempDir);
                    }
                }
                catch
                {
                }
            }
        }
    }

    private async Task<(string ExePath, string PublishDir)> BuildHelperExeAsync(string buildDir, string safeName, CancellationToken cancellationToken)
    {
        var projectPath = Path.Combine(buildDir, $"{safeName}.csproj");
        var programPath = Path.Combine(buildDir, "Program.cs");
        var ipcPath = Path.Combine(buildDir, "DiscordIpc.cs");
        var publishDir = Path.Combine(buildDir, "publish");
        var exePath = Path.Combine(publishDir, $"{safeName}.exe");

        var escapedAppId = EscapeForCSharp(clientId);
        var escapedAppName = EscapeForCSharp(appName);
        var escapedTitle = EscapeForCSharp(title);
        var customElapsedSeconds = (long)customElapsedHours * 3600;

        var iconSourcePath = FindIconPath();
        var hasIcon = false;
        if (iconSourcePath is not null)
        {
            try
            {
                var iconDestPath = Path.Combine(buildDir, "icon.ico");
                File.Copy(iconSourcePath, iconDestPath, overwrite: true);
                hasIcon = true;
            }
            catch
            {
            }
        }

        var projectSource = $$"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>WinExe</OutputType>
            <TargetFramework>net9.0-windows</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
            <UseWindowsForms>true</UseWindowsForms>
            <AssemblyName>{{safeName}}</AssemblyName>
            <RootNamespace>DiscordHelper</RootNamespace>
            {{(hasIcon ? "<ApplicationIcon>icon.ico</ApplicationIcon>" : "")}}
          </PropertyGroup>
        </Project>
        """;

        var programSource = $$"""
        using System.Drawing;
        using System.Windows.Forms;

        namespace DiscordHelper;

        internal static class Program
        {
            [STAThread]
            private static void Main()
            {
                ApplicationConfiguration.Initialize();
                Application.Run(new CountdownForm());
                Environment.Exit(0);
            }

            private sealed class CountdownForm : Form
            {
                private readonly DateTime endTime = DateTime.Now.AddSeconds({{durationSeconds}});
                private readonly Label titleLabel;
                private readonly Label appIdLabel;
                private readonly Label statusLabel;
                private readonly Label timeLabel;
                private readonly System.Windows.Forms.Timer timer;
                private DiscordIpc? discordIpc;

                public CountdownForm()
                {
                    Text = "{{escapedAppName}}";
                    StartPosition = FormStartPosition.CenterScreen;
                    FormBorderStyle = FormBorderStyle.FixedDialog;
                    MaximizeBox = false;
                    MinimizeBox = false;
                    TopMost = true;
                    ClientSize = new Size(340, 170);
                    BackColor = Color.WhiteSmoke;

                    try
                    {
                        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                    }
                    catch
                    {
                    }

                    titleLabel = new Label
                    {
                        Text = "{{escapedTitle}}",
                        Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
                        Location = new Point(18, 16),
                        Size = new Size(304, 28)
                    };
                    Controls.Add(titleLabel);

                    appIdLabel = new Label
                    {
                        Text = "App ID: {{escapedAppId}}",
                        Font = new Font("Segoe UI", 9.5F),
                        ForeColor = Color.DimGray,
                        Location = new Point(18, 48),
                        Size = new Size(304, 22)
                    };
                    Controls.Add(appIdLabel);

                    statusLabel = new Label
                    {
                        Text = "Bot calisiyor",
                        Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                        Location = new Point(18, 82),
                        Size = new Size(304, 24)
                    };
                    Controls.Add(statusLabel);

                    timeLabel = new Label
                    {
                        Text = "Kalan zaman: 00:00",
                        Font = new Font("Consolas", 16F, FontStyle.Bold),
                        Location = new Point(18, 112),
                        Size = new Size(304, 32)
                    };
                    Controls.Add(timeLabel);

                    timer = new System.Windows.Forms.Timer { Interval = 1000 };
                    timer.Tick += (_, _) => UpdateRemaining();
                    Shown += async (_, _) =>
                    {
                        UpdateRemaining();
                        timer.Start();
                        await StartDiscordRpcAsync();
                    };
                    FormClosed += (_, _) =>
                    {
                        timer.Dispose();
                        discordIpc?.Dispose();
                    };
                }

                private async Task StartDiscordRpcAsync()
                {
                    try
                    {
                        discordIpc = new DiscordIpc("{{escapedAppId}}");
                        await discordIpc.ConnectAsync(CancellationToken.None);
                        
                        var startUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - {{customElapsedSeconds}};
                        var activity = new
                        {
                            state = "Gorev Spoof Ediliyor",
                            details = "{{escapedTitle}}",
                            timestamps = new { start = startUnix },
                            assets = new
                            {
                                large_image = "discord_logo",
                                  large_text = "Discord Unicode"
                            }
                        };
                        
                        await discordIpc.SetActivityAsync(activity, System.Diagnostics.Process.GetCurrentProcess().Id, CancellationToken.None);
                    }
                    catch
                    {
                        statusLabel.Text = "Discord baglantisi basarisiz";
                    }
                }

                private void UpdateRemaining()
                {
                    var remaining = endTime - DateTime.Now;
                    if (remaining.TotalSeconds <= 0)
                    {
                        timeLabel.Text = "Kalan zaman: 00:00";
                        timer.Stop();
                        Close();
                        return;
                    }

                    timeLabel.Text = $"Kalan zaman: {remaining.Minutes:00}:{remaining.Seconds:00}";
                }
            }
        }
        """;

        var ipcSource = $$"""
        using System.IO.Pipes;
        using System.Text;
        using System.Text.Json;

        namespace DiscordHelper;

        public sealed class DiscordIpc : IDisposable
        {
            private readonly string clientId;
            private NamedPipeClientStream? pipe;

            public DiscordIpc(string clientId)
            {
                this.clientId = clientId;
            }

            public async Task ConnectAsync(CancellationToken cancellationToken)
            {
                for (var i = 0; i < 10; i++)
                {
                    var candidate = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.Asynchronous);
                    try
                    {
                        await candidate.ConnectAsync(300, cancellationToken);
                        pipe = candidate;
                        await SendAsync(0, new { v = 1, client_id = clientId }, cancellationToken);
                        var response = await ReceiveAsync(cancellationToken);
                        if (response.Op == 2)
                        {
                            throw new InvalidOperationException(response.Payload.RootElement.TryGetProperty("message", out var message)
                                ? message.GetString()
                                : "Handshake failed");
                        }
                        return;
                    }
                    catch
                    {
                        candidate.Dispose();
                        if (i == 9)
                        {
                            throw;
                        }
                    }
                }

                throw new InvalidOperationException("Discord is not running or IPC pipe could not be opened.");
            }

            public async Task SetActivityAsync(object activity, int pid, CancellationToken cancellationToken)
            {
                await SendAsync(
                    1,
                    new
                    {
                        cmd = "SET_ACTIVITY",
                        args = new
                        {
                            pid,
                            activity
                        },
                        nonce = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
                    },
                    cancellationToken);

                await ReceiveAsync(cancellationToken);
            }

            private async Task SendAsync(int op, object payload, CancellationToken cancellationToken)
            {
                if (pipe is null)
                {
                    throw new InvalidOperationException("Not connected to Discord.");
                }

                var json = JsonSerializer.Serialize(payload);
                var body = Encoding.UTF8.GetBytes(json);
                var header = new byte[8];
                BitConverter.GetBytes(op).CopyTo(header, 0);
                BitConverter.GetBytes(body.Length).CopyTo(header, 4);

                await pipe.WriteAsync(header, cancellationToken);
                await pipe.WriteAsync(body, cancellationToken);
                await pipe.FlushAsync(cancellationToken);
            }

            private async Task<(int Op, JsonDocument Payload)> ReceiveAsync(CancellationToken cancellationToken)
            {
                if (pipe is null)
                {
                    throw new InvalidOperationException("Not connected to Discord.");
                }

                var header = await ReadExactAsync(pipe, 8, cancellationToken);
                var op = BitConverter.ToInt32(header, 0);
                var length = BitConverter.ToInt32(header, 4);
                var payload = await ReadExactAsync(pipe, length, cancellationToken);
                return (op, JsonDocument.Parse(payload));
            }

            private static async Task<byte[]> ReadExactAsync(Stream stream, int length, CancellationToken cancellationToken)
            {
                var buffer = new byte[length];
                var offset = 0;
                while (offset < length)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);
                    if (read == 0)
                    {
                        throw new EndOfStreamException("Connection closed by Discord.");
                    }
                    offset += read;
                }

                return buffer;
            }

            public void Dispose()
            {
                pipe?.Dispose();
            }
        }
        """;

        await File.WriteAllTextAsync(projectPath, projectSource, Encoding.UTF8, cancellationToken);
        await File.WriteAllTextAsync(programPath, programSource, Encoding.UTF8, cancellationToken);
        await File.WriteAllTextAsync(ipcPath, ipcSource, Encoding.UTF8, cancellationToken);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = buildDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("publish");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("-r");
        startInfo.ArgumentList.Add("win-x64");
        startInfo.ArgumentList.Add("--self-contained");
        startInfo.ArgumentList.Add("false");
        startInfo.ArgumentList.Add("-p:PublishSingleFile=true");
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(publishDir);

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0 || !File.Exists(exePath))
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
        }

        return (exePath, publishDir);
    }

    public static async Task BuildStandaloneExeAsync(
        string clientId,
        int durationSeconds,
        string title,
        string appName,
        string processName,
        string targetPath,
        CancellationToken cancellationToken)
    {
        await BuildStandaloneExeWithHoursAsync(clientId, durationSeconds, title, appName, processName, targetPath, 0, cancellationToken);
    }

    public static async Task BuildStandaloneExeWithHoursAsync(
        string clientId,
        int durationSeconds,
        string title,
        string appName,
        string processName,
        string targetPath,
        int customElapsedHours,
        CancellationToken cancellationToken)
    {
        var safeName = Regex.Replace(processName, "[\\\\/*?:\"<>|]", string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "DiscordGame";
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "dummy");
        var buildDir = Path.Combine(tempDir, $"{safeName}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(buildDir);

        try
        {
            var worker = new SpooferWorker(clientId, durationSeconds, title, appName, processName, customElapsedHours);
            var (exePath, publishDir) = await worker.BuildHelperExeAsync(buildDir, safeName, cancellationToken);

            var targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }
            File.Copy(exePath, targetPath, overwrite: true);
        }
        finally
        {
            if (Directory.Exists(buildDir))
            {
                await TryDeleteDirectoryAsync(buildDir);
            }
            if (Directory.Exists(tempDir))
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(tempDir).Any())
                    {
                        Directory.Delete(tempDir);
                    }
                }
                catch
                {
                }
            }
        }
    }

    private string GetSafeProcessName()
    {
        var safeName = Regex.Replace(processName, "[\\\\/*?:\"<>|]", string.Empty).Trim();
        return string.IsNullOrWhiteSpace(safeName) ? "DiscordGame" : safeName;
    }

    private static string? FindIconPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var path = Path.Combine(current.FullName, "assets", "icon.ico");
            if (File.Exists(path)) return path;
            current = current.Parent;
        }

        const string userDevPath = @"C:\Users\oguzf\Music\discordquestlog\discordunicode\assets\icon.ico";
        if (File.Exists(userDevPath)) return userDevPath;

        return null;
    }

    private static string EscapeForCSharp(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static async Task TryDeleteDirectoryAsync(string directoryPath)
    {
        await Task.Delay(300);
        for (var attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                if (Directory.Exists(directoryPath))
                {
                    Directory.Delete(directoryPath, true);
                }

                return;
            }
            catch when (attempt < 7)
            {
                await Task.Delay(500);
            }
        }
    }
}

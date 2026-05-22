using System.Drawing.Imaging;
using System.Text;

namespace DiscordUnicodeShell;

public sealed class Form1 : Form
{
    private readonly BridgeClient bridgeClient = new();
    private readonly List<ProfileInfo> profiles = new();
    private readonly List<QuestInfo> allQuests = new();
    private readonly List<QuestInfo> spooferQueue = new();
    private readonly Dictionary<string, Button> menuButtons = new();
    private readonly Dictionary<string, Control> pages = new();
    private readonly Dictionary<string, FlowLayoutPanel> questHosts = new();
    private readonly Dictionary<string, Label> pageCountLabels = new();
    private readonly Dictionary<string, string> pageTitles = new()
    {
        ["home"] = "Ana Sayfa",
        ["game"] = "Oyun",
        ["spoofer"] = "Spoofer",
        ["video"] = "Izleme",
        ["completed"] = "Tamamlanan",
        ["terminal"] = "Terminal",
        ["settings"] = "Ayarlar"
    };
    private readonly Dictionary<string, string> fallbackAppIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["roblox"] = "1256426466981515324",
        ["fisch on roblox"] = "1256426466981515324",
        ["arknights"] = "1047805177993240636",
        ["wuthering waves"] = "1236894086704988220",
        ["zenless zone zero"] = "1256426466981515324",
        ["rune dice"] = "1256426466981515324"
    };

    private AppSettings currentSettings = new();
    private SpooferWorker? spooferWorker;
    private QuestInfo? currentSpoofedQuest;
    private bool busy;
    private bool isUpdatingUi;
    private string currentPage = "home";

    private string lastScanLabel = "Henuz taranmadi";
    private string lastScanSource = "-";
    private string lastScanMode = "Headless";
    private string lastScanProfile = "-";
    private int lastScanTotal;

    private PictureBox backgroundPictureBox = null!;
    private Panel overlayPanel = null!;
    private Panel topBar = null!;
    private Label lblTitle = null!;
    private Label lblTopMeta = null!;
    private Button btnScan = null!;
    private Panel menuPanel = null!;
    private Panel contentPanel = null!;
    private ComboBox cmbProfiles = null!;
    private Button btnRefreshProfiles = null!;
    private CheckBox chkHeadless = null!;
    private Label lblStatus = null!;
    private Label lblMeta = null!;
    private TextBox txtInlineLog = null!;
    private TextBox txtLog = null!;
    private Button btnRefreshLogs = null!;

    private Label lblTotalCount = null!;
    private Label lblGameCount = null!;
    private Label lblVideoCount = null!;
    private Label lblCompletedCount = null!;
    private Label lblOtherCount = null!;
    private Label lblHomeProfile = null!;
    private Label lblHomeMode = null!;
    private Label lblHomeSource = null!;
    private Label lblHomeLastScan = null!;

    private Label lblSettingsProfile = null!;
    private Label lblSettingsMode = null!;
    private Label lblSettingsRoot = null!;
    private Label lblSettingsLog = null!;
    private Label lblProfileCount = null!;
    private Label lblSettingsBuffer = null!;
    private Label lblSettingsAutoScan = null!;
    private Label lblSettingsLastScanSource = null!;
    private Label lblSettingsLastScanTotal = null!;

    private DataGridView gridSpooferQueue = null!;
    private Label lblSpooferQueueCount = null!;
    private Label lblSpooferState = null!;
    private Label lblSpooferActiveTitle = null!;
    private Label lblSpooferActiveId = null!;
    private Label lblSpooferTime = null!;
    private ProgressBar progressSpoofer = null!;
    private Button btnStartAutoSpoofer = null!;
    private Button btnStartManualSpoofer = null!;
    private Button btnBuildManualExe = null!;
    private Button btnStopSpoofer = null!;
    private TextBox txtManualAppId = null!;
    private TextBox txtManualAppName = null!;
    private NumericUpDown numManualDuration = null!;
    private NumericUpDown numBufferMinutes = null!;
    private CheckBox chkAutoScanAfterSpoofer = null!;
    private Label lblManualAppId = null!;
    private Label lblManualAppName = null!;
    private Label lblManualDuration = null!;
    private Label lblBuffer = null!;

    private Button btnStartAutoWatch = null!;
    private Button btnStopAutoWatch = null!;
    private Label lblManualCustomHours = null!;
    private NumericUpDown numManualCustomHours = null!;
    private WatchWorker? watchWorker;

    private FileSystemWatcher? logWatcher;

    public Form1()
    {
        InitializeComponent();
        ConfigureWindow();
        BuildUi();
        HookEvents();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            logWatcher?.Dispose();
            spooferWorker?.Stop();
        }

        base.Dispose(disposing);
    }

    private async void Form1_Load(object? sender, EventArgs e)
    {
        LoadBackground();
        SetupLogWatcher();
        await LoadInitialStateAsync();
    }

    private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
    {
        spooferWorker?.Stop();
    }

    private async void BtnRefreshProfiles_Click(object? sender, EventArgs e)
    {
        await LoadProfilesAsync(scanAfterLoad: false);
    }

    private async void BtnScan_Click(object? sender, EventArgs e)
    {
        if (cmbProfiles.SelectedItem is not ProfileInfo profile)
        {
            MessageBox.Show("Bir profil sec.", "Profil gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        await ScanQuestsAsync(profile.Directory);
    }

    private async void BtnRefreshLogs_Click(object? sender, EventArgs e)
    {
        await LoadLogFileAsync();
    }

    private async void CmbProfiles_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (isUpdatingUi || cmbProfiles.SelectedItem is not ProfileInfo profile)
        {
            return;
        }

        currentSettings.SelectedProfile = profile.Directory;
        await SaveSettingsAsync();
        UpdateMeta(profile);
        UpdateSettingsView();
        AppendLog($"Profil secildi: {RenderProfileName(profile)}");
    }

    private async void ChkHeadless_CheckedChanged(object? sender, EventArgs e)
    {
        if (isUpdatingUi)
        {
            return;
        }

        currentSettings.BrowserMode = chkHeadless.Checked ? "headless" : "visible";
        await SaveSettingsAsync();
        UpdateHomeInfo();
        UpdateSettingsView();
        UpdateMeta(cmbProfiles.SelectedItem as ProfileInfo);
        AppendLog($"Chrome modu: {FriendlyMode(currentSettings.BrowserMode)}");
    }

    private async void NumBufferMinutes_ValueChanged(object? sender, EventArgs e)
    {
        if (isUpdatingUi)
        {
            return;
        }

        currentSettings.BufferMinutes = (int)numBufferMinutes.Value;
        await SaveSettingsAsync();
        UpdateSettingsView();
        AppendLog($"Ek tolerans suresi: {currentSettings.BufferMinutes} dk");
    }

    private async void ChkAutoScanAfterSpoofer_CheckedChanged(object? sender, EventArgs e)
    {
        if (isUpdatingUi)
        {
            return;
        }

        currentSettings.AutoScanAfterSpoofer = chkAutoScanAfterSpoofer.Checked;
        await SaveSettingsAsync();
        UpdateSettingsView();
    }

    private void BtnStartAutoSpoofer_Click(object? sender, EventArgs e)
    {
        StartAutoSpoofer();
    }

    private void BtnStartManualSpoofer_Click(object? sender, EventArgs e)
    {
        StartManualSpoofer();
    }

    private async void BtnBuildManualExe_Click(object? sender, EventArgs e)
    {
        await BuildManualExeAsync();
    }



    private void BtnStopSpoofer_Click(object? sender, EventArgs e)
    {
        StopSpoofer();
    }

    private void BtnStartAutoWatch_Click(object? sender, EventArgs e)
    {
        StartAutoWatch();
    }

    private void BtnStopAutoWatch_Click(object? sender, EventArgs e)
    {
        StopAutoWatch();
    }

    private void StartAutoWatch()
    {
        if (busy || watchWorker is not null)
        {
            return;
        }

        if (cmbProfiles.SelectedItem is not ProfileInfo profile)
        {
            MessageBox.Show("Lutfen bir Chrome profili secin.", "Profil gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        AppendLog($"Otomatik video izleme baslatiliyor. Profil: {RenderProfileName(profile)}");
        if (btnStartAutoWatch is not null) btnStartAutoWatch.Enabled = false;
        if (btnStopAutoWatch is not null) btnStopAutoWatch.Enabled = true;

        watchWorker = new WatchWorker(profile.Directory, chkHeadless.Checked, bridgeClient);

        watchWorker.ProgressChanged += (_, args) =>
        {
            BeginInvoke(new Action(() =>
            {
                var remainingStr = args.RemainingSeconds > 0 ? $"{args.RemainingSeconds} sn kaldi" : "Bitti";
                var percentStr = args.ProgressPercent.HasValue ? $" (%{args.ProgressPercent.Value})" : "";
                AppendLog($"[Izleme Gecmisi] {args.Title}: {args.StatusText} - {remainingStr}{percentStr}");
            }));
        };

        watchWorker.LogReceived += (_, log) =>
        {
            BeginInvoke(new Action(() =>
            {
                AppendLog($"[Izleme] {log}");
            }));
        };

        watchWorker.Finished += (_, _) =>
        {
            BeginInvoke(new Action(() =>
            {
                AppendLog("Otomatik video izleme basariyla tamamlandi.");
                CleanupWatchUi();
                if (chkAutoScanAfterSpoofer.Checked)
                {
                    _ = ScanQuestsAsync(profile.Directory);
                }
            }));
        };

        watchWorker.Failed += (_, error) =>
        {
            BeginInvoke(new Action(() =>
            {
                HandleError($"Izleme Hatasi: {error}");
                CleanupWatchUi();
            }));
        };

        watchWorker.Start();
    }

    private void StopAutoWatch()
    {
        if (watchWorker is null)
        {
            return;
        }

        AppendLog("Otomatik video izleme durduruluyor...");
        watchWorker.Stop();
        CleanupWatchUi();
    }

    private void CleanupWatchUi()
    {
        watchWorker = null;
        if (btnStartAutoWatch is not null) btnStartAutoWatch.Enabled = true;
        if (btnStopAutoWatch is not null) btnStopAutoWatch.Enabled = false;
    }

    private async void PageButton_Click(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.Tag is not string pageKey)
        {
            return;
        }

        await SwitchPageAsync(pageKey);
    }

    private async Task BuildManualExeAsync()
    {
        var appId = txtManualAppId.Text.Trim();
        if (string.IsNullOrWhiteSpace(appId) || !appId.All(char.IsDigit))
        {
            AppendLog("Hata: EXE olusturmak icin gecerli bir Application ID gir.");
            return;
        }

        var appName = string.IsNullOrWhiteSpace(txtManualAppName.Text) ? "Manual Helper" : txtManualAppName.Text.Trim();
        var durationMinutes = (int)numManualDuration.Value;
        var bufferMinutes = (int)numBufferMinutes.Value;
        var customElapsedHours = (int)numManualCustomHours.Value;
        var totalSeconds = (durationMinutes + bufferMinutes) * 60;
        var safeName = string.Concat(appName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)).Trim();
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "DiscordHelper";
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Kalici helper EXE olustur",
            Filter = "Executable (*.exe)|*.exe",
            FileName = $"{safeName}.exe",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            btnBuildManualExe.Enabled = false;
            AppendLog($"EXE olusturuluyor: {dialog.FileName}");
            await SpooferWorker.BuildStandaloneExeWithHoursAsync(
                appId,
                totalSeconds,
                appName,
                appName,
                safeName,
                dialog.FileName,
                customElapsedHours,
                CancellationToken.None);
            AppendLog($"EXE hazir: {dialog.FileName}");
            MessageBox.Show(this, "EXE basariyla olusturuldu.", "Hazir", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            HandleError($"EXE olusturma hatasi: {ex.Message}");
        }
        finally
        {
            btnBuildManualExe.Enabled = true;
        }
    }



    private async Task LoadInitialStateAsync()
    {
        ToggleBusy(true, "Baslatiliyor...");
        try
        {
            currentSettings = await bridgeClient.LoadSettingsAsync();
            lastScanSource = currentSettings.LastScanSource;
            lastScanMode = string.IsNullOrWhiteSpace(currentSettings.LastScanMode) ? FriendlyMode(currentSettings.BrowserMode) : currentSettings.LastScanMode;
            lastScanProfile = string.IsNullOrWhiteSpace(currentSettings.LastScanProfile) ? "-" : currentSettings.LastScanProfile;
            lastScanTotal = currentSettings.LastScanTotal;

            isUpdatingUi = true;
            chkHeadless.Checked = currentSettings.BrowserMode == "headless";
            numBufferMinutes.Value = Math.Max(numBufferMinutes.Minimum, Math.Min(numBufferMinutes.Maximum, currentSettings.BufferMinutes));
            chkAutoScanAfterSpoofer.Checked = currentSettings.AutoScanAfterSpoofer;
            numManualDuration.Value = 15;
            txtManualAppName.Text = "Bilinmeyen Oyun";
            isUpdatingUi = false;

            await SwitchPageAsync(string.IsNullOrWhiteSpace(currentSettings.LastPage) ? "home" : currentSettings.LastPage, save: false);
            UpdateSettingsView();
            UpdateHomeInfo();
            UpdateTopMeta();
            await LoadProfilesAsync(scanAfterLoad: true);
            await LoadLogFileAsync();
            RenderSpooferQueue();
        }
        finally
        {
            ToggleBusy(false, lblStatus.Text);
        }
    }

    private async Task LoadProfilesAsync(bool scanAfterLoad)
    {
        ToggleBusy(true, "Profiller yukleniyor...");
        try
        {
            var response = await bridgeClient.GetProfilesAsync();
            if (!response.Ok)
            {
                throw new InvalidOperationException(response.Error ?? "Profil listesi alinamadi.");
            }

            ApplyProfiles(response.Profiles);
            UpdateSettingsView();

            if (scanAfterLoad && cmbProfiles.SelectedItem is ProfileInfo selected)
            {
                await ScanQuestsAsync(selected.Directory);
            }
        }
        catch (Exception ex)
        {
            HandleError($"Profil hatasi: {ex.Message}");
        }
        finally
        {
            ToggleBusy(false, lblStatus.Text);
        }
    }

    private void ApplyProfiles(List<ProfileInfo> items)
    {
        profiles.Clear();
        profiles.AddRange(items);

        isUpdatingUi = true;
        cmbProfiles.DataSource = null;
        cmbProfiles.DataSource = profiles.ToList();
        var selected = profiles.FirstOrDefault(x => x.Directory == currentSettings.SelectedProfile) ?? profiles.FirstOrDefault();
        if (selected is not null)
        {
            cmbProfiles.SelectedItem = selected;
            currentSettings.SelectedProfile = selected.Directory;
        }
        isUpdatingUi = false;

        lblProfileCount.Text = profiles.Count.ToString();
        lblStatus.Text = $"{profiles.Count} profil bulundu";
        UpdateMeta(selected);
        AppendLog($"{profiles.Count} profil yuklendi.");
    }

    private async Task ScanQuestsAsync(string profileDirectory)
    {
        ToggleBusy(true, "Gorevler taraniyor...");
        try
        {
            var response = await bridgeClient.ScrapeQuestsAsync(profileDirectory, chkHeadless.Checked);
            if (!response.Ok)
            {
                throw new InvalidOperationException(response.Error ?? "Gorev taramasi basarisiz.");
            }

            ApplyScanResult(response);
            await LoadLogFileAsync();
        }
        catch (Exception ex)
        {
            HandleError($"Tarama hatasi: {ex.Message}");
        }
        finally
        {
            ToggleBusy(false, lblStatus.Text);
        }
    }

    private void ApplyScanResult(ScrapeResponse response)
    {
        allQuests.Clear();
        allQuests.AddRange(response.Quests.Select(NormalizeQuest));

        UpdateProfileSelectionAfterScan(response.Profile);

        lastScanLabel = DateTime.Now.ToString("HH:mm:ss");
        lastScanSource = string.IsNullOrWhiteSpace(allQuests.FirstOrDefault()?.Source) ? "-" : allQuests.First().Source;
        lastScanMode = FriendlyMode(currentSettings.BrowserMode);
        lastScanProfile = cmbProfiles.SelectedItem is ProfileInfo profile ? RenderProfileName(profile) : "-";
        lastScanTotal = allQuests.Count;

        currentSettings.LastScanSource = lastScanSource;
        currentSettings.LastScanMode = lastScanMode;
        currentSettings.LastScanProfile = lastScanProfile;
        currentSettings.LastScanTotal = lastScanTotal;
        _ = SaveSettingsAsync();

        RenderQuestViews();
        RenderSpooferQueue();
        UpdateHomeInfo();
        UpdateSettingsView();
        UpdateMeta(cmbProfiles.SelectedItem as ProfileInfo);

        lblStatus.Text = $"{allQuests.Count} gorev yuklendi";
        lblMeta.Text = response.Profile is null
            ? "Profil bilgisi yok"
            : $"Profil: {RenderProfileName(response.Profile)} ({response.Profile.Directory})";

        AppendLog($"Tarama tamamlandi. {allQuests.Count} gorev bulundu.");
    }

    private QuestInfo NormalizeQuest(QuestInfo quest)
    {
        quest.Title = NormalizeText(quest.Title, "Bilinmeyen gorev");
        quest.Description = NormalizeText(quest.Description);
        quest.AppName = NormalizeText(quest.AppName);
        quest.ProgressText = NormalizeText(quest.ProgressText, "Bilinmiyor");
        quest.StatusText = NormalizeText(quest.StatusText, "Durum yok");
        quest.Category = NormalizeText(quest.Category, "other").ToLowerInvariant();
        if (quest.Category is not ("game" or "video"))
        {
            quest.Category = "other";
        }

        if (quest.ProgressPercent is not null)
        {
            quest.ProgressPercent = Math.Max(0, Math.Min(100, quest.ProgressPercent.Value));
        }

        quest.Source = NormalizeText(quest.Source, "-").ToUpperInvariant();
        quest.AppId = NormalizeText(quest.AppId);
        return quest;
    }

    private void RenderQuestViews()
    {
        var activeGames = GetActiveGameQuests().ToList();
        var activeVideos = allQuests.Where(q => q.Category == "video" && !q.Done).ToList();
        var completed = allQuests.Where(q => q.Done).ToList();
        var otherCount = allQuests.Count(q => q.Category is not ("game" or "video"));

        lblTotalCount.Text = allQuests.Count.ToString();
        lblGameCount.Text = activeGames.Count.ToString();
        lblVideoCount.Text = activeVideos.Count.ToString();
        lblCompletedCount.Text = completed.Count.ToString();
        lblOtherCount.Text = otherCount.ToString();

        pageCountLabels["game"].Text = $"{activeGames.Count} gorev";
        pageCountLabels["video"].Text = $"{activeVideos.Count} gorev";
        pageCountLabels["completed"].Text = $"{completed.Count} gorev";

        RenderQuestHost(questHosts["home"], allQuests, "Gorev yok.");
        RenderQuestHost(questHosts["game"], activeGames, "Aktif oyun gorevi yok.");
        RenderQuestHost(questHosts["video"], activeVideos, "Aktif izleme gorevi yok.");
        RenderQuestHost(questHosts["completed"], completed, "Tamamlanan gorev yok.");
    }

    private void RenderQuestHost(FlowLayoutPanel host, IReadOnlyCollection<QuestInfo> quests, string emptyText)
    {
        host.SuspendLayout();
        host.Controls.Clear();

        if (quests.Count == 0)
        {
            var empty = new Label
            {
                AutoSize = true,
                Text = emptyText,
                ForeColor = Color.DimGray,
                Padding = new Padding(8)
            };
            host.Controls.Add(empty);
            host.ResumeLayout();
            return;
        }

        foreach (var quest in quests)
        {
            var card = CreateQuestCard(quest, host);
            host.Controls.Add(card);
        }

        host.ResumeLayout();
    }

    private Panel CreateQuestCard(QuestInfo quest, Control host)
    {
        var card = new Panel
        {
            BackColor = quest.Done ? Color.FromArgb(10, 32, 24) : Color.FromArgb(11, 17, 26),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(8),
            Padding = new Padding(14),
            Width = Math.Max(760, host.ClientSize.Width - 40),
            Height = string.IsNullOrWhiteSpace(quest.Description) ? 144 : 176
        };
        MakeRounded(card, 10);
        card.Paint += (s, e) =>
        {
            var borderColor = quest.Done ? Color.FromArgb(44, 255, 154) : Color.FromArgb(16, 42, 56);
            using var pen = new Pen(borderColor, 1);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            var r = 10;
            var d = r * 2;
            path.StartFigure();
            path.AddArc(1, 1, d, d, 180, 90);
            path.AddArc(card.Width - d - 2, 1, d, d, 270, 90);
            path.AddArc(card.Width - d - 2, card.Height - d - 2, d, d, 0, 90);
            path.AddArc(1, card.Height - d - 2, d, d, 90, 90);
            path.CloseFigure();
            e.Graphics.DrawPath(pen, path);
        };

        var title = new Label
        {
            Text = quest.Title,
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(234, 247, 255),
            BackColor = Color.Transparent,
            AutoSize = false,
            Width = card.Width - 160,
            Height = 24,
            Location = new Point(12, 10)
        };

        var badge = new Label
        {
            Text = quest.StatusText,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = quest.Done ? Color.FromArgb(18, 54, 36) : Color.FromArgb(7, 27, 38),
            ForeColor = quest.Done ? Color.FromArgb(44, 255, 154) : Color.FromArgb(0, 200, 255),
            Location = new Point(card.Width - 150, 10),
            Size = new Size(120, 24)
        };
        MakeRounded(badge, 6);

        var metaParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(quest.AppName))
        {
            metaParts.Add(quest.AppName);
        }
        if (!string.IsNullOrWhiteSpace(quest.AppId))
        {
            metaParts.Add($"App ID: {quest.AppId}");
        }
        if (!string.IsNullOrWhiteSpace(quest.Source) && quest.Source != "-")
        {
            metaParts.Add(quest.Source);
        }

        var meta = new Label
        {
            Text = string.Join(" | ", metaParts),
            ForeColor = Color.FromArgb(124, 139, 153),
            BackColor = Color.Transparent,
            AutoSize = false,
            Width = card.Width - 24,
            Height = 20,
            Location = new Point(12, 40)
        };

        var progress = new Label
        {
            Text = quest.ProgressText,
            ForeColor = Color.FromArgb(200, 214, 224),
            BackColor = Color.Transparent,
            AutoSize = false,
            Width = card.Width - 24,
            Height = 20,
            Location = new Point(12, string.IsNullOrWhiteSpace(quest.Description) ? 70 : 102)
        };

        card.Controls.Add(title);
        card.Controls.Add(badge);
        card.Controls.Add(meta);

        if (!string.IsNullOrWhiteSpace(quest.Description))
        {
            var description = new Label
            {
                Text = quest.Description,
                AutoSize = false,
                Width = card.Width - 24,
                Height = 42,
                Location = new Point(12, 62),
                ForeColor = Color.FromArgb(124, 139, 153),
                BackColor = Color.Transparent
            };
            card.Controls.Add(description);
        }

        card.Controls.Add(progress);

        if (quest.ProgressPercent is not null)
        {
            var pbBg = new Panel
            {
                Location = new Point(12, card.Height - 20),
                Width = card.Width - 24,
                Height = 6,
                BackColor = Color.FromArgb(8, 13, 20),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            MakeRounded(pbBg, 3);

            var pbFill = new Panel
            {
                Location = new Point(0, 0),
                Width = (int)Math.Round(pbBg.Width * (Math.Max(0, Math.Min(100, quest.ProgressPercent.Value)) / 100.0)),
                Height = 6,
                BackColor = quest.Done ? Color.FromArgb(44, 255, 154) : Color.FromArgb(0, 200, 255)
            };
            MakeRounded(pbFill, 3);
            pbBg.Controls.Add(pbFill);

            pbBg.SizeChanged += (s, e) =>
            {
                pbFill.Width = (int)Math.Round(pbBg.Width * (Math.Max(0, Math.Min(100, quest.ProgressPercent.Value)) / 100.0));
                MakeRounded(pbFill, 3);
                MakeRounded(pbBg, 3);
            };

            card.Controls.Add(pbBg);
        }

        return card;
    }

    private void RenderSpooferQueue()
    {
        var queueRows = GetActiveGameQuests()
            .Select((quest, index) => new SpooferQueueRow
            {
                Order = index + 1,
                Title = quest.Title,
                AppName = quest.AppName,
                AppId = string.IsNullOrWhiteSpace(quest.AppId) ? "-" : quest.AppId,
                RemainingMinutes = GetRemainingMinutes(quest),
                Accepted = quest.Accepted ? "Evet" : "Hayir"
            })
            .ToList();

        gridSpooferQueue.DataSource = queueRows;
        lblSpooferQueueCount.Text = $"{queueRows.Count} gorev";
    }

    private void StartAutoSpoofer()
    {
        if (busy || spooferWorker is not null)
        {
            return;
        }

        spooferQueue.Clear();
        spooferQueue.AddRange(GetActiveGameQuests());
        if (spooferQueue.Count == 0)
        {
            AppendLog("Spoofer baslatilamadi. Sirada bekleyen aktif oyun gorevi yok.");
            return;
        }

        AppendLog($"Otomatik spoofer baslatiliyor. Toplam {spooferQueue.Count} gorev siraya alindi.");
        SetSpooferRunningUi("Calisiyor");
        ProcessNextSpooferQueueItem();
    }

    private void StartManualSpoofer()
    {
        if (busy || spooferWorker is not null)
        {
            return;
        }

        var appId = txtManualAppId.Text.Trim();
        if (string.IsNullOrWhiteSpace(appId) || !appId.All(char.IsDigit))
        {
            AppendLog("Hata: Gecersiz Application ID.");
            return;
        }

        var durationMinutes = (int)numManualDuration.Value;
        if (durationMinutes <= 0)
        {
            AppendLog("Hata: Gecersiz sure.");
            return;
        }

        var bufferMinutes = (int)numBufferMinutes.Value;
        var totalSeconds = (durationMinutes + bufferMinutes) * 60;
        var appName = string.IsNullOrWhiteSpace(txtManualAppName.Text) ? "Manuel Gorev" : txtManualAppName.Text.Trim();
        var customElapsedHours = (int)numManualCustomHours.Value;

        AppendLog($"Manuel spoofer baslatiliyor: App ID {appId} ({appName}) ({durationMinutes} dk + {bufferMinutes} dk ek sure)");
        SetSpooferRunningUi("Manuel Calisiyor");
        lblSpooferActiveTitle.Text = appName;
        lblSpooferActiveId.Text = $"App ID: {appId}";
        RunSpooferWorker(appId, totalSeconds, appName, appName, customElapsedHours);
    }

    private void ProcessNextSpooferQueueItem()
    {
        if (spooferQueue.Count == 0)
        {
            AppendLog("Tum siradaki gorevler tamamlandi.");
            CleanupSpooferUi();
            if (chkAutoScanAfterSpoofer.Checked && cmbProfiles.SelectedItem is ProfileInfo profile)
            {
                _ = ScanQuestsAsync(profile.Directory);
            }
            return;
        }

        var quest = spooferQueue[0];
        spooferQueue.RemoveAt(0);
        currentSpoofedQuest = quest;

        var appId = ResolveQuestAppId(quest);
        var remainingSeconds = quest.TargetSeconds - quest.ProgressSeconds;
        if (remainingSeconds <= 0)
        {
            remainingSeconds = 15 * 60;
        }

        var bufferMinutes = (int)numBufferMinutes.Value;
        var totalSeconds = remainingSeconds + (bufferMinutes * 60);

        lblSpooferActiveTitle.Text = quest.Title;
        lblSpooferActiveId.Text = $"App ID: {appId}";
        AppendLog($"Gorev baslatildi: {quest.Title} ({Math.Max(1, remainingSeconds / 60)} dk + {bufferMinutes} dk ek sure)");
        RunSpooferWorker(appId, totalSeconds, quest.Title, string.IsNullOrWhiteSpace(quest.AppName) ? quest.Title : quest.AppName);
    }

    private void RunSpooferWorker(string appId, int totalSeconds, string title, string appName, int customElapsedHours = 0)
    {
        var worker = new SpooferWorker(appId, totalSeconds, title, appName, title, customElapsedHours);
        spooferWorker = worker;

        worker.ProgressChanged += (_, args) =>
        {
            if (!ReferenceEquals(spooferWorker, worker))
            {
                return;
            }

            BeginInvoke(new Action(() =>
            {
                var percent = totalSeconds <= 0 ? 0 : Math.Min(100, (int)Math.Round((double)args.ElapsedSeconds / totalSeconds * 100));
                progressSpoofer.Value = percent;
                lblSpooferTime.Text = $"{args.RemainingSeconds / 60:00}:{args.RemainingSeconds % 60:00}";
            }));
        };

        worker.Finished += (_, _) =>
        {
            if (!ReferenceEquals(spooferWorker, worker))
            {
                return;
            }

            BeginInvoke(new Action(() =>
            {
                AppendLog("Gorev spoofing basariyla tamamlandi.");
                spooferWorker = null;
                if (spooferQueue.Count > 0)
                {
                    ProcessNextSpooferQueueItem();
                }
                else
                {
                    CleanupSpooferUi();
                    if (chkAutoScanAfterSpoofer.Checked && cmbProfiles.SelectedItem is ProfileInfo profile)
                    {
                        _ = ScanQuestsAsync(profile.Directory);
                    }
                }
            }));
        };

        worker.Failed += (_, message) =>
        {
            if (!ReferenceEquals(spooferWorker, worker))
            {
                return;
            }

            BeginInvoke(new Action(() =>
            {
                HandleError($"Spoofer Hatasi: {message}");
                spooferWorker = null;
                if (spooferQueue.Count > 0)
                {
                    ProcessNextSpooferQueueItem();
                }
                else
                {
                    CleanupSpooferUi();
                }
            }));
        };

        worker.Start();
    }

    private void StopSpoofer()
    {
        if (spooferWorker is null)
        {
            return;
        }

        AppendLog("Spoofer durduruluyor...");
        spooferWorker.Stop();
        CleanupSpooferUi();
    }

    private void CleanupSpooferUi()
    {
        spooferWorker = null;
        spooferQueue.Clear();
        currentSpoofedQuest = null;
        lblSpooferState.Text = "Bekliyor";
        lblSpooferActiveTitle.Text = "-";
        lblSpooferActiveId.Text = "-";
        lblSpooferTime.Text = "00:00";
        progressSpoofer.Value = 0;
        btnStartAutoSpoofer.Enabled = true;
        btnStartManualSpoofer.Enabled = true;
        btnStopSpoofer.Enabled = false;
        RenderSpooferQueue();
    }

    private void SetSpooferRunningUi(string state)
    {
        lblSpooferState.Text = state;
        btnStartAutoSpoofer.Enabled = false;
        btnStartManualSpoofer.Enabled = false;
        btnStopSpoofer.Enabled = true;
        progressSpoofer.Value = 0;
        lblSpooferTime.Text = "00:00";
    }

    private string ResolveQuestAppId(QuestInfo quest)
    {
        if (!string.IsNullOrWhiteSpace(quest.AppId))
        {
            return quest.AppId;
        }

        if (fallbackAppIds.TryGetValue(quest.AppName, out var appId) || fallbackAppIds.TryGetValue(quest.Title, out appId))
        {
            return appId;
        }

        foreach (var pair in fallbackAppIds)
        {
            if ((!string.IsNullOrWhiteSpace(quest.AppName) && quest.AppName.Contains(pair.Key, StringComparison.OrdinalIgnoreCase)) ||
                quest.Title.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        AppendLog($"Uyari: {quest.Title} icin App ID bulunamadi. Varsayilan (Roblox) kullaniliyor.");
        return "1256426466981515324";
    }

    private IEnumerable<QuestInfo> GetActiveGameQuests()
    {
        return allQuests
            .Where(q => q.Category == "game" && !q.Done)
            .OrderByDescending(q => q.Accepted)
            .ThenBy(q => string.IsNullOrWhiteSpace(q.AppId))
            .ThenBy(q => q.Title);
    }

    private static int GetRemainingMinutes(QuestInfo quest)
    {
        var remainingSeconds = quest.TargetSeconds - quest.ProgressSeconds;
        return remainingSeconds <= 0 ? 1 : Math.Max(1, remainingSeconds / 60);
    }

    private async Task LoadLogFileAsync()
    {
        txtLog.Text = await bridgeClient.ReadLogTailAsync();
        txtLog.SelectionStart = txtLog.TextLength;
        txtLog.ScrollToCaret();
    }

    private async Task SaveSettingsAsync()
    {
        await bridgeClient.SaveSettingsAsync(currentSettings);
    }

    private void UpdateProfileSelectionAfterScan(ProfileInfo? profile)
    {
        if (profile is null)
        {
            return;
        }

        var selected = profiles.FirstOrDefault(p => string.Equals(p.Directory, profile.Directory, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            return;
        }

        isUpdatingUi = true;
        cmbProfiles.SelectedItem = selected;
        isUpdatingUi = false;
        currentSettings.SelectedProfile = selected.Directory;
    }

    private void HandleError(string message)
    {
        AppendLog(message);
        lblStatus.Text = "Hata";
        ToggleBusy(false, lblStatus.Text);
        UpdateTopMeta();
    }

    private void UpdateMeta(ProfileInfo? profile)
    {
        var profileText = profile is null ? "Profil bekleniyor" : RenderProfileName(profile);
        lblMeta.Text = $"Profil: {profileText}";
        UpdateTopMeta();
    }

    private void UpdateTopMeta()
    {
        var parts = new List<string> { pageTitles.GetValueOrDefault(currentPage, "Ana Sayfa") };
        var profileText = cmbProfiles.SelectedItem is ProfileInfo profile ? RenderProfileName(profile) : "-";
        if (!string.IsNullOrWhiteSpace(profileText) && profileText != "-")
        {
            parts.Add(profileText);
        }
        parts.Add(FriendlyMode(currentSettings.BrowserMode));
        if (busy)
        {
            parts.Add("Tarama suruyor");
        }
        else if (!string.IsNullOrWhiteSpace(lastScanLabel))
        {
            parts.Add(lastScanLabel);
        }
        lblTopMeta.Text = string.Join(" • ", parts);
    }

    private void UpdateHomeInfo()
    {
        lblHomeProfile.Text = string.IsNullOrWhiteSpace(lastScanProfile) ? "-" : lastScanProfile;
        lblHomeMode.Text = lastScanMode;
        lblHomeSource.Text = lastScanSource;
        lblHomeLastScan.Text = lastScanLabel;
    }

    private void UpdateSettingsView()
    {
        lblSettingsProfile.Text = string.IsNullOrWhiteSpace(currentSettings.SelectedProfile) ? "-" : currentSettings.SelectedProfile;
        lblSettingsMode.Text = FriendlyMode(currentSettings.BrowserMode);
        lblSettingsRoot.Text = bridgeClient.ProjectRoot;
        lblSettingsLog.Text = bridgeClient.LogPath;
        lblSettingsBuffer.Text = currentSettings.BufferMinutes.ToString();
        lblSettingsAutoScan.Text = currentSettings.AutoScanAfterSpoofer ? "Acik" : "Kapali";
        lblSettingsLastScanSource.Text = lastScanSource;
        lblSettingsLastScanTotal.Text = lastScanTotal.ToString();
    }

    private void ToggleBusy(bool value, string status)
    {
        busy = value;
        btnScan.Enabled = !value;
        btnRefreshProfiles.Enabled = !value;
        cmbProfiles.Enabled = !value;
        chkHeadless.Enabled = !value;
        lblStatus.Text = status;
        UpdateTopMeta();
    }

    private async Task SwitchPageAsync(string pageKey, bool save = true)
    {
        if (!pages.TryGetValue(pageKey, out var page))
        {
            pageKey = "home";
            page = pages[pageKey];
        }

        currentPage = pageKey;
        foreach (var pair in pages)
        {
            pair.Value.Visible = pair.Key == pageKey;
        }

        foreach (var pair in menuButtons)
        {
            if (pair.Key == pageKey)
            {
                pair.Value.BackColor = Color.FromArgb(7, 27, 38);
                pair.Value.ForeColor = Color.FromArgb(0, 200, 255);
            }
            else
            {
                pair.Value.BackColor = Color.Transparent;
                pair.Value.ForeColor = Color.FromArgb(70, 70, 70);
            }
        }

        currentSettings.LastPage = pageKey;
        if (save)
        {
            await SaveSettingsAsync();
        }
        UpdateTopMeta();
    }

    private void EnableDoubleBuffering(Control control)
    {
        try
        {
            var property = typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            property?.SetValue(control, true);
        }
        catch {}
        foreach (Control child in control.Controls)
        {
            EnableDoubleBuffering(child);
        }
    }

    private void MakeRounded(Control control, int radius)
    {
        void ApplyRegion()
        {
            if (control.Width <= 0 || control.Height <= 0) return;
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            var d = radius * 2;
            path.StartFigure();
            path.AddArc(0, 0, d, d, 180, 90);
            path.AddArc(control.Width - d, 0, d, d, 270, 90);
            path.AddArc(control.Width - d, control.Height - d, d, d, 0, 90);
            path.AddArc(0, control.Height - d, d, d, 90, 90);
            path.CloseFigure();
            control.Region = new Region(path);
        }

        control.SizeChanged += (s, e) => ApplyRegion();
        if (control.IsHandleCreated)
        {
            ApplyRegion();
        }
        else
        {
            control.HandleCreated += (s, e) => ApplyRegion();
        }
    }

    private void StyleControlsTheme(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control == topBar || control == menuPanel || control == contentPanel)
            {
                StyleControlsTheme(control);
                continue;
            }

            if (pages.Values.Contains(control) || questHosts.Values.Contains(control))
            {
                control.BackColor = Color.Transparent;
                StyleControlsTheme(control);
                continue;
            }

            switch (control)
            {
                case Button btn:
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.Font = new Font("Segoe UI Semibold", menuButtons.Values.Contains(btn) ? 10F : 9.5F, FontStyle.Bold);
                    if (menuButtons.Values.Contains(btn))
                    {
                        btn.FlatAppearance.BorderSize = 0;
                        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(10, 27, 38);
                        btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(14, 35, 48);
                        btn.BackColor = btn.Tag as string == currentPage ? Color.FromArgb(7, 27, 38) : Color.Transparent;
                        btn.ForeColor = btn.Tag as string == currentPage ? Color.FromArgb(0, 200, 255) : Color.FromArgb(124, 139, 153);
                        MakeRounded(btn, 8);
                    }
                    else
                    {
                        btn.FlatAppearance.BorderSize = 1;
                        btn.BackColor = Color.FromArgb(11, 17, 26);
                        btn.FlatAppearance.BorderColor = btn.Text.Contains("Durdur", StringComparison.OrdinalIgnoreCase)
                            ? Color.FromArgb(255, 77, 109)
                            : Color.FromArgb(18, 50, 66);
                        btn.ForeColor = btn.Text.Contains("Durdur", StringComparison.OrdinalIgnoreCase)
                            ? Color.FromArgb(255, 122, 142)
                            : Color.FromArgb(0, 200, 255);
                        btn.FlatAppearance.MouseOverBackColor = btn.Text.Contains("Durdur", StringComparison.OrdinalIgnoreCase)
                            ? Color.FromArgb(45, 16, 24)
                            : Color.FromArgb(15, 25, 36);
                        btn.FlatAppearance.MouseDownBackColor = btn.Text.Contains("Durdur", StringComparison.OrdinalIgnoreCase)
                            ? Color.FromArgb(55, 20, 30)
                            : Color.FromArgb(18, 30, 42);
                        MakeRounded(btn, 12);
                    }
                    break;

                case Label lbl:
                    if (lbl == lblTitle)
                    {
                        lbl.ForeColor = Color.FromArgb(234, 247, 255);
                    }
                    else if (lbl == lblTopMeta || lbl.ForeColor == Color.DimGray || lbl.ForeColor == Color.Gray || lbl.ForeColor == Color.FromArgb(60, 60, 60))
                    {
                        lbl.ForeColor = Color.FromArgb(124, 139, 153);
                    }
                    else
                    {
                        lbl.ForeColor = Color.FromArgb(234, 247, 255);
                    }
                    break;

                case TextBox txt:
                    txt.BackColor = Color.FromArgb(11, 17, 26);
                    txt.ForeColor = Color.FromArgb(234, 247, 255);
                    txt.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case ComboBox cmb:
                    cmb.BackColor = Color.FromArgb(11, 17, 26);
                    cmb.ForeColor = Color.FromArgb(234, 247, 255);
                    cmb.FlatStyle = FlatStyle.Flat;
                    break;

                case NumericUpDown num:
                    num.BackColor = Color.FromArgb(11, 17, 26);
                    num.ForeColor = Color.FromArgb(234, 247, 255);
                    num.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case CheckBox chk:
                    chk.ForeColor = Color.FromArgb(234, 247, 255);
                    chk.FlatStyle = FlatStyle.Flat;
                    break;

                case GroupBox grp:
                    grp.ForeColor = Color.FromArgb(0, 200, 255);
                    grp.BackColor = Color.FromArgb(11, 17, 26);
                    StyleControlsTheme(grp);
                    break;

                case DataGridView dgv:
                    dgv.BackgroundColor = Color.FromArgb(11, 17, 26);
                    dgv.BackColor = Color.FromArgb(11, 17, 26);
                    dgv.ForeColor = Color.FromArgb(234, 247, 255);
                    dgv.GridColor = Color.FromArgb(16, 42, 56);
                    dgv.BorderStyle = BorderStyle.None;
                    dgv.EnableHeadersVisualStyles = false;
                    dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(9, 15, 24);
                    dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(0, 200, 255);
                    dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(9, 15, 24);
                    dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                    dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
                    dgv.DefaultCellStyle.BackColor = Color.FromArgb(11, 17, 26);
                    dgv.DefaultCellStyle.ForeColor = Color.FromArgb(234, 247, 255);
                    dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(7, 27, 38);
                    dgv.DefaultCellStyle.SelectionForeColor = Color.FromArgb(0, 200, 255);
                    dgv.RowHeadersVisible = false;
                    break;

                case Panel pnl:
                    if (pnl.Parent != contentPanel && pnl.Height > 200 && pnl.Width > 200)
                    {
                        pnl.BackColor = Color.FromArgb(11, 17, 26);
                        pnl.BorderStyle = BorderStyle.FixedSingle;
                        MakeRounded(pnl, 14);
                    }
                    StyleControlsTheme(pnl);
                    break;

                default:
                    if (control.HasChildren)
                    {
                        StyleControlsTheme(control);
                    }
                    break;
            }
        }
    }

    private void ConfigureWindow()
    {
        Text = "NEVERQUEST";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1200, 780);
        MinimumSize = new Size(980, 680);
        DoubleBuffered = true;

        var iconPath = Path.Combine(bridgeClient.ProjectRoot, "assets", "icon.ico");
        if (File.Exists(iconPath))
        {
            try
            {
                Icon = new Icon(iconPath);
            }
            catch
            {
            }
        }
    }

    private void InitializeComponent()
    {
        SuspendLayout();
        ResumeLayout(false);
    }

    private void BuildUi()
    {
        backgroundPictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.StretchImage,
            BackColor = Color.FromArgb(5, 8, 13)
        };
        Controls.Add(backgroundPictureBox);

        overlayPanel = new Panel
        {
            Parent = backgroundPictureBox,
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(5, 8, 13)
        };

        topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 74,
            BackColor = Color.FromArgb(7, 11, 17)
        };
        overlayPanel.Controls.Add(topBar);

        lblTitle = new Label
        {
            Text = "NEVERQUEST",
            Font = new Font("Segoe UI Semibold", 19F, FontStyle.Bold),
            Location = new Point(22, 10),
            AutoSize = true,
            ForeColor = Color.FromArgb(234, 247, 255),
            BackColor = Color.Transparent
        };
        topBar.Controls.Add(lblTitle);

        lblTopMeta = new Label
        {
            Text = "License: Premium (Lifetime) • Registered to: Customer",
            ForeColor = Color.FromArgb(124, 139, 153),
            Location = new Point(24, 42),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        topBar.Controls.Add(lblTopMeta);

        cmbProfiles = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(520, 18),
            Size = new Size(240, 28)
        };
        topBar.Controls.Add(cmbProfiles);

        btnRefreshProfiles = new Button
        {
            Text = "Profilleri Yukle",
            Location = new Point(770, 16),
            Size = new Size(126, 30)
        };
        topBar.Controls.Add(btnRefreshProfiles);

        chkHeadless = new CheckBox
        {
            Text = "Headless",
            Checked = true,
            CheckState = CheckState.Checked,
            Location = new Point(520, 48),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        topBar.Controls.Add(chkHeadless);

        btnScan = new Button
        {
            Text = "Gorevleri Tara",
            Location = new Point(910, 16),
            Size = new Size(126, 30)
        };
        topBar.Controls.Add(btnScan);

        lblStatus = new Label
        {
            Text = "Hazir",
            Location = new Point(1060, 15),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        topBar.Controls.Add(lblStatus);

        lblMeta = new Label
        {
            Text = "Profil bilgisi yok",
            ForeColor = Color.FromArgb(124, 139, 153),
            Location = new Point(1060, 40),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        topBar.Controls.Add(lblMeta);

        menuPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 178,
            BackColor = Color.FromArgb(7, 11, 17),
            Padding = new Padding(14, 18, 14, 18)
        };
        overlayPanel.Controls.Add(menuPanel);

        var menuTitle = new Label
        {
            Text = "Menu",
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(14, 14),
            ForeColor = Color.FromArgb(124, 139, 153),
            BackColor = Color.Transparent
        };
        menuPanel.Controls.Add(menuTitle);

        var menuTop = 52;
        foreach (var pair in pageTitles)
        {
            var button = new Button
            {
                Text = pair.Value,
                Tag = pair.Key,
                Width = 148,
                Height = 36,
                Left = 14,
                Top = menuTop,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(124, 139, 153),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9.8F, FontStyle.Bold)
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(10, 27, 38);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(14, 35, 48);
            button.Click += PageButton_Click;
            menuPanel.Controls.Add(button);
            menuButtons[pair.Key] = button;
            menuTop += 46;
            MakeRounded(button, 8);
        }

        contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 18, 18, 18),
            BackColor = Color.FromArgb(5, 8, 13)
        };
        overlayPanel.Controls.Add(contentPanel);

        backgroundPictureBox.SendToBack();
        overlayPanel.BringToFront();
        contentPanel.SendToBack();
        menuPanel.BringToFront();
        topBar.BringToFront();

        BuildPages();
        StyleControlsTheme(overlayPanel);
        EnableDoubleBuffering(this);

    }

    private void HookEvents()
    {
        Load += Form1_Load;
        FormClosing += Form1_FormClosing;
        btnRefreshProfiles.Click += BtnRefreshProfiles_Click;
        btnScan.Click += BtnScan_Click;
        btnRefreshLogs.Click += BtnRefreshLogs_Click;
        cmbProfiles.SelectedIndexChanged += CmbProfiles_SelectedIndexChanged;
        chkHeadless.CheckedChanged += ChkHeadless_CheckedChanged;
        numBufferMinutes.ValueChanged += NumBufferMinutes_ValueChanged;
        chkAutoScanAfterSpoofer.CheckedChanged += ChkAutoScanAfterSpoofer_CheckedChanged;
        btnStartAutoSpoofer.Click += BtnStartAutoSpoofer_Click;
        btnStartManualSpoofer.Click += BtnStartManualSpoofer_Click;
        btnStopSpoofer.Click += BtnStopSpoofer_Click;
        btnBuildManualExe.Click += BtnBuildManualExe_Click;
        if (btnStartAutoWatch is not null)
        {
            btnStartAutoWatch.Click += BtnStartAutoWatch_Click;
        }
        if (btnStopAutoWatch is not null)
        {
            btnStopAutoWatch.Click += BtnStopAutoWatch_Click;
        }
        Resize += (_, _) => ResizeQuestCards();
    }

    private void BuildPages()
    {
        var homePage = CreatePageContainer();
        BuildHomePage(homePage);
        pages["home"] = homePage;
        contentPanel.Controls.Add(homePage);

        var gamePage = CreatePageContainer();
        questHosts["game"] = BuildQuestPage(gamePage, "Oyun", out var gameCountLabel);
        pageCountLabels["game"] = gameCountLabel;
        pages["game"] = gamePage;
        contentPanel.Controls.Add(gamePage);

        var spooferPage = CreatePageContainer();
        BuildSpooferPage(spooferPage);
        pages["spoofer"] = spooferPage;
        contentPanel.Controls.Add(spooferPage);

        var videoPage = CreatePageContainer();
        questHosts["video"] = BuildQuestPage(videoPage, "Izleme", out var videoCountLabel);
        pageCountLabels["video"] = videoCountLabel;
        pages["video"] = videoPage;
        contentPanel.Controls.Add(videoPage);

        var completedPage = CreatePageContainer();
        questHosts["completed"] = BuildQuestPage(completedPage, "Tamamlanan", out var completedCountLabel);
        pageCountLabels["completed"] = completedCountLabel;
        pages["completed"] = completedPage;
        contentPanel.Controls.Add(completedPage);

        var terminalPage = CreatePageContainer();
        BuildTerminalPage(terminalPage);
        pages["terminal"] = terminalPage;
        contentPanel.Controls.Add(terminalPage);

        var settingsPage = CreatePageContainer();
        BuildSettingsPage(settingsPage);
        pages["settings"] = settingsPage;
        contentPanel.Controls.Add(settingsPage);
    }

    private Panel CreatePageContainer()
    {
        return new Panel
        {
            Dock = DockStyle.Fill,
            Visible = false,
            BackColor = Color.Transparent,
            AutoScroll = true
        };
    }

    private void BuildHomePage(Control page)
    {
        var shell = CreateCenteredSurface(page, 1020, 728, 18);
        page.Controls.Add(shell);

        var title = CreatePageTitle("Ana Sayfa");
        shell.Controls.Add(title);

        var infoPanel = new TableLayoutPanel
        {
            Left = 16,
            Top = 58,
            Width = 460,
            Height = 120,
            ColumnCount = 2
        };
        infoPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
        infoPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        infoPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        infoPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        infoPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        infoPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        shell.Controls.Add(infoPanel);

        lblHomeProfile = AddInfoLine(infoPanel, 0, "Profil");
        lblHomeMode = AddInfoLine(infoPanel, 1, "Mod");
        lblHomeSource = AddInfoLine(infoPanel, 2, "Kaynak");
        lblHomeLastScan = AddInfoLine(infoPanel, 3, "Son tarama");

        var summary = new TableLayoutPanel
        {
            Left = 16,
            Top = 188,
            Width = shell.ClientSize.Width - 32,
            Height = 94,
            ColumnCount = 5,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        for (var i = 0; i < 5; i++)
        {
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        }
        shell.Controls.Add(summary);

        lblTotalCount = AddSummaryCard(summary, 0, "Toplam");
        lblGameCount = AddSummaryCard(summary, 1, "Oyun");
        lblVideoCount = AddSummaryCard(summary, 2, "Izleme");
        lblCompletedCount = AddSummaryCard(summary, 3, "Tamamlanan");
        lblOtherCount = AddSummaryCard(summary, 4, "Diger");

        var questsTitle = CreateSectionLabel("Tum Gorevler", 16, 300);
        shell.Controls.Add(questsTitle);

        questHosts["home"] = CreateQuestHost(16, 332, shell.ClientSize.Width - 32, 256);
        questHosts["home"].Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        shell.Controls.Add(questHosts["home"]);

        var inlineLogTitle = CreateSectionLabel("Hizli Log", 16, 596);
        shell.Controls.Add(inlineLogTitle);

        txtInlineLog = new TextBox
        {
            Left = 16,
            Top = 628,
            Width = shell.ClientSize.Width - 32,
            Height = 94,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        shell.Controls.Add(txtInlineLog);
    }

    private FlowLayoutPanel BuildQuestPage(Control page, string titleText, out Label countLabel)
    {
        var shell = CreateCenteredSurface(page, 1020, 728, 18);
        page.Controls.Add(shell);

        var title = CreatePageTitle(titleText);
        shell.Controls.Add(title);

        countLabel = new Label
        {
            Text = "0 gorev",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Location = new Point(18, 50)
        };
        shell.Controls.Add(countLabel);

        if (titleText == "Izleme")
        {
            btnStartAutoWatch = new Button
            {
                Text = "Izlemeyi Baslat",
                Location = new Point(140, 44),
                Size = new Size(150, 28)
            };
            btnStopAutoWatch = new Button
            {
                Text = "Durdur",
                Location = new Point(300, 44),
                Size = new Size(100, 28),
                Enabled = false
            };
            shell.Controls.Add(btnStartAutoWatch);
            shell.Controls.Add(btnStopAutoWatch);
        }

        var host = CreateQuestHost(16, 82, shell.ClientSize.Width - 32, 620);
        host.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        shell.Controls.Add(host);
        return host;
    }

    private void BuildSpooferPage(Control page)
    {
        var shell = CreateCenteredSurface(page, 1020, 728, 18);
        page.Controls.Add(shell);

        shell.Controls.Add(CreatePageTitle("Spoofer"));

        var root = new TableLayoutPanel
        {
            Left = 16,
            Top = 58,
            Width = shell.ClientSize.Width - 32,
            Height = 664,
            ColumnCount = 2,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56F));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44F));
        shell.Controls.Add(root);

        var left = new Panel { Dock = DockStyle.Fill };
        root.Controls.Add(left, 0, 0);

        var queueTitle = CreateSectionLabel("Spoofer Gorev Sirasi", 0, 0);
        left.Controls.Add(queueTitle);

        lblSpooferQueueCount = new Label
        {
            Text = "0 gorev",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Location = new Point(0, 28)
        };
        left.Controls.Add(lblSpooferQueueCount);

        gridSpooferQueue = new DataGridView
        {
            Left = 0,
            Top = 56,
            Width = 620,
            Height = 580,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        left.Controls.Add(gridSpooferQueue);

        var right = new Panel { Dock = DockStyle.Fill };
        root.Controls.Add(right, 1, 0);

        btnStartAutoSpoofer = new Button
        {
            Text = "Otomatik Baslat",
            Left = 0,
            Top = 0,
            Width = 164,
            Height = 32
        };
        right.Controls.Add(btnStartAutoSpoofer);

        btnStopSpoofer = new Button
        {
            Text = "Durdur",
            Left = 180,
            Top = 0,
            Width = 120,
            Height = 32,
            Enabled = false
        };
        right.Controls.Add(btnStopSpoofer);

        var statusBox = new GroupBox
        {
            Text = "Durum",
            Left = 0,
            Top = 48,
            Width = 470,
            Height = 120
        };
        right.Controls.Add(statusBox);

        lblSpooferState = new Label { Text = "Bekliyor", Location = new Point(16, 24), AutoSize = true };
        lblSpooferActiveTitle = new Label { Text = "-", Location = new Point(16, 46), AutoSize = true };
        lblSpooferActiveId = new Label { Text = "-", Location = new Point(16, 68), AutoSize = true };
        lblSpooferTime = new Label { Text = "00:00", Font = new Font("Consolas", 14F, FontStyle.Bold), Location = new Point(378, 24), AutoSize = true };
        progressSpoofer = new ProgressBar { Location = new Point(16, 90), Width = 430, Height = 16 };
        statusBox.Controls.Add(lblSpooferState);
        statusBox.Controls.Add(lblSpooferActiveTitle);
        statusBox.Controls.Add(lblSpooferActiveId);
        statusBox.Controls.Add(lblSpooferTime);
        statusBox.Controls.Add(progressSpoofer);

        var manualBox = new GroupBox
        {
            Text = "Manuel Spoofer",
            Left = 0,
            Top = 184,
            Width = 470,
            Height = 226
        };
        right.Controls.Add(manualBox);

        lblManualAppId = new Label { Text = "Application ID", Left = 16, Top = 28, AutoSize = true };
        txtManualAppId = new TextBox { Left = 16, Top = 46, Width = 430 };
        lblManualAppName = new Label { Text = "Oyun Adi", Left = 16, Top = 78, AutoSize = true };
        txtManualAppName = new TextBox { Left = 16, Top = 96, Width = 430 };
        lblManualDuration = new Label { Text = "Sure (dakika)", Left = 16, Top = 128, AutoSize = true };
        numManualDuration = new NumericUpDown { Left = 16, Top = 146, Width = 120, Minimum = 1, Maximum = 600, Value = 15 };

        lblManualCustomHours = new Label { Text = "Gecmis Sure (saat)", Left = 160, Top = 128, AutoSize = true };
        numManualCustomHours = new NumericUpDown { Left = 160, Top = 146, Width = 120, Minimum = 0, Maximum = 100000, Value = 0 };

        btnStartManualSpoofer = new Button { Text = "Manuel Baslat", Left = 304, Top = 142, Width = 142, Height = 28 };
        btnBuildManualExe = new Button { Text = "EXE Olustur", Left = 304, Top = 178, Width = 142, Height = 28 };
        manualBox.Controls.Add(lblManualAppId);
        manualBox.Controls.Add(txtManualAppId);
        manualBox.Controls.Add(lblManualAppName);
        manualBox.Controls.Add(txtManualAppName);
        manualBox.Controls.Add(lblManualDuration);
        manualBox.Controls.Add(numManualDuration);
        manualBox.Controls.Add(lblManualCustomHours);
        manualBox.Controls.Add(numManualCustomHours);
        manualBox.Controls.Add(btnStartManualSpoofer);
        manualBox.Controls.Add(btnBuildManualExe);

        var settingsBox = new GroupBox
        {
            Text = "Spoofer Ayarlari",
            Left = 0,
            Top = 414,
            Width = 470,
            Height = 110
        };
        right.Controls.Add(settingsBox);

        lblBuffer = new Label { Text = "Ek tolerans suresi (dakika)", Left = 16, Top = 28, AutoSize = true };
        numBufferMinutes = new NumericUpDown { Left = 16, Top = 50, Width = 100, Minimum = 0, Maximum = 60, Value = 2 };
        chkAutoScanAfterSpoofer = new CheckBox { Text = "Tamamlaninca otomatik tara", Left = 16, Top = 78, AutoSize = true, Checked = true };
        settingsBox.Controls.Add(lblBuffer);
        settingsBox.Controls.Add(numBufferMinutes);
        settingsBox.Controls.Add(chkAutoScanAfterSpoofer);
    }

    private void BuildTerminalPage(Control page)
    {
        var shell = CreateCenteredSurface(page, 940, 690, 22);
        page.Controls.Add(shell);

        var title = new Label
        {
            Text = "Terminal",
            Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold),
            Location = new Point(24, 20),
            AutoSize = true
        };
        shell.Controls.Add(title);

        btnRefreshLogs = new Button
        {
            Text = "Log Yenile",
            Left = shell.ClientSize.Width - 142,
            Top = 18,
            Width = 110,
            Height = 30,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        shell.Controls.Add(btnRefreshLogs);

        txtLog = new TextBox
        {
            Left = 24,
            Top = 66,
            Width = shell.ClientSize.Width - 48,
            Height = shell.ClientSize.Height - 90,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 10.5F),
            BackColor = Color.FromArgb(8, 13, 20),
            ForeColor = Color.FromArgb(234, 247, 255),
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        shell.Controls.Add(txtLog);
    }

    private void BuildSettingsPage(Control page)
    {
        var shell = CreateCenteredSurface(page, 900, 540, 30);
        page.Controls.Add(shell);

        var title = new Label
        {
            Text = "Ayarlar",
            Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold),
            Location = new Point(24, 20),
            AutoSize = true
        };
        shell.Controls.Add(title);

        var table = new TableLayoutPanel
        {
            Left = 24,
            Top = 72,
            Width = shell.ClientSize.Width - 48,
            Height = 300,
            ColumnCount = 2,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var i = 0; i < 8; i++)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, i is 2 or 3 ? 72F : 34F));
        }
        shell.Controls.Add(table);

        lblSettingsProfile = AddSettingsLine(table, 0, "Secili profil");
        lblSettingsMode = AddSettingsLine(table, 1, "Mod");
        lblSettingsRoot = AddSettingsLine(table, 2, "Proje kok", 720);
        lblSettingsLog = AddSettingsLine(table, 3, "Log yolu", 720);
        lblProfileCount = AddSettingsLine(table, 4, "Profil sayisi");
        lblSettingsBuffer = AddSettingsLine(table, 5, "Buffer");
        lblSettingsAutoScan = AddSettingsLine(table, 6, "Auto scan");
        var sourceLabel = AddSettingsLine(table, 7, "Son kaynak");
        lblSettingsLastScanSource = sourceLabel;

        var totalCaption = new Label
        {
            Text = "Son Tarama Toplami",
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
            Location = new Point(24, 396),
            AutoSize = true
        };
        shell.Controls.Add(totalCaption);

        lblSettingsLastScanTotal = new Label
        {
            Text = "0",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
            Location = new Point(230, 398)
        };
        shell.Controls.Add(lblSettingsLastScanTotal);
    }

    private Label AddInfoLine(TableLayoutPanel table, int row, string label)
    {
        var left = new Label
        {
            Text = label,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            AutoSize = true
        };
        var right = new Label
        {
            Text = "-",
            AutoSize = true
        };
        table.Controls.Add(left, 0, row);
        table.Controls.Add(right, 1, row);
        return right;
    }

    private Label AddSettingsLine(TableLayoutPanel table, int row, string label, int maxWidth = 0)
    {
        var left = new Label
        {
            Text = label,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            AutoSize = true
        };
        var right = new Label
        {
            Text = "-",
            AutoSize = true
        };
        if (maxWidth > 0)
        {
            right.MaximumSize = new Size(maxWidth, 0);
        }
        table.Controls.Add(left, 0, row);
        table.Controls.Add(right, 1, row);
        return right;
    }

    private Label AddSummaryCard(TableLayoutPanel table, int column, string title)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(11, 17, 26),
            Margin = new Padding(6)
        };
        MakeRounded(panel, 8);

        var caption = new Label
        {
            Text = title,
            ForeColor = Color.FromArgb(124, 139, 153),
            Location = new Point(12, 10),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        panel.Controls.Add(caption);

        var value = new Label
        {
            Text = "0",
            Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 200, 255),
            Location = new Point(12, 32),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        panel.Controls.Add(value);

        table.Controls.Add(panel, column, 0);
        return value;
    }

    private Panel CreateCenteredSurface(Control page, int preferredWidth, int height, int top)
    {
        var panel = new Panel
        {
            Width = preferredWidth,
            Height = height,
            Top = top,
            BackColor = Color.FromArgb(11, 17, 26),
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Left
        };
        MakeRounded(panel, 14);

        void Recenter()
        {
            panel.Width = Math.Min(preferredWidth, Math.Max(640, page.ClientSize.Width - 64));
            panel.Left = Math.Max(18, (page.ClientSize.Width - panel.Width) / 2);
        }

        Recenter();
        page.Resize += (_, _) => Recenter();
        page.VisibleChanged += (_, _) => Recenter();
        page.Layout += (_, _) => Recenter();
        page.HandleCreated += (_, _) => BeginInvoke(new Action(Recenter));
        return panel;
    }

    private FlowLayoutPanel CreateQuestHost(int left, int top, int width, int height)
    {
        var host = new FlowLayoutPanel
        {
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        host.Resize += (_, _) =>
        {
            foreach (Control control in host.Controls)
            {
                if (control is Panel card)
                {
                    card.Width = Math.Max(760, host.ClientSize.Width - 40);
                }
            }
        };

        return host;
    }

    private Label CreatePageTitle(string text)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold),
            ForeColor = Color.FromArgb(35, 35, 35),
            Location = new Point(16, 14),
            AutoSize = true,
            BackColor = Color.Transparent
        };
    }

    private Label CreateSectionLabel(string text, int left, int top)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(55, 55, 55),
            Location = new Point(left, top),
            AutoSize = true,
            BackColor = Color.Transparent
        };
    }

    private void ResizeQuestCards()
    {
        foreach (var host in questHosts.Values)
        {
            foreach (Control control in host.Controls)
            {
                if (control is Panel card)
                {
                    card.Width = Math.Max(760, host.ClientSize.Width - 40);
                }
            }
        }
    }

    private void LoadBackground()
    {
        backgroundPictureBox.Image = null;
    }

    private void SetupLogWatcher()
    {
        try
        {
            logWatcher = new FileSystemWatcher(bridgeClient.ProjectRoot, "discordunicode.log")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            logWatcher.Changed += async (_, _) => await SafeReloadLogAsync();
            logWatcher.Created += async (_, _) => await SafeReloadLogAsync();
        }
        catch
        {
        }
    }

    private async Task SafeReloadLogAsync()
    {
        try
        {
            if (!IsHandleCreated)
            {
                return;
            }

            await Task.Delay(80);
            BeginInvoke(new Action(async () => await LoadLogFileAsync()));
        }
        catch
        {
        }
    }

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        txtInlineLog.AppendText(line + Environment.NewLine);

        try
        {
            File.AppendAllText(bridgeClient.LogPath, line + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
        }
    }

    private static string FriendlyMode(string mode)
    {
        return string.Equals(mode, "headless", StringComparison.OrdinalIgnoreCase) ? "Headless" : "Gorunur";
    }

    private static string NormalizeText(string? value, string fallback = "")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return string.Join(" ", value.Replace("Ã‚Â·", "•").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string RenderProfileName(ProfileInfo profile)
    {
        var name = NormalizeText(string.IsNullOrWhiteSpace(profile.Name) ? profile.Directory : profile.Name, "Profil");
        var userName = NormalizeText(profile.UserName);
        return string.IsNullOrWhiteSpace(userName) ? name : $"{name} • {userName}";
    }

    private sealed class SpooferQueueRow
    {
        public int Order { get; set; }
        public string Title { get; set; } = string.Empty;
        public string AppName { get; set; } = string.Empty;
        public string AppId { get; set; } = string.Empty;
        public int RemainingMinutes { get; set; }
        public string Accepted { get; set; } = string.Empty;
    }
}

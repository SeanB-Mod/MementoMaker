using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace TPMSimpleModMaker
{
    internal sealed class SettingsForm : Form
    {
        private readonly DiscoveryService _discovery;
        private readonly bool _embeddedMode;

        private TextBox _unityPath;
        private TextBox _sdkPath;
        private TextBox _outputPath;
        private TextBox _privateEnvironmentPath;
        private TextBox _moddersName;

        private SettingsStatusRow _unityStatus;
        private SettingsStatusRow _hubStatus;
        private SettingsStatusRow _sdkStatus;
        private SettingsStatusRow _environmentStatus;
        private SettingsStatusRow _workerStatus;
        private Label _overallStatus;

        private RichTextBox _buildLog;
        private TwoPointVerticalScrollBar _logScroll;
        private bool _syncingLogScroll;

        private Button _saveButton;
        private Button _refreshButton;
        private Button _rebuildButton;
        private Button _workshopCheckButton;
        private SettingsStatusRow _workshopStatusRow;
        private ToolTip _workshopToolTip;
        private ToolTip _uiToolTip;
        private CheckBox _keepUnityWorkerRunning;
        private TableLayoutPanel _pageLayout;
        private const float CompactEnvironmentStatusHeight = 165F;
        private const float ExpandedEnvironmentStatusHeight = 205F;
        private string _diagnosticsWorkerStatus = "Not started";

        public AppSettings Settings { get; private set; }
        public string NavigationTarget { get; private set; }

        public event EventHandler SettingsSaved;
        public event EventHandler RefreshRequested;
        public event EventHandler RebuildEnvironmentRequested;
        public event EventHandler ClearLogRequested;
        public event EventHandler WorkshopQueryRequested;

        public SettingsForm(AppSettings current, DiscoveryService discovery) : this(current, discovery, false)
        {
        }

        public SettingsForm(AppSettings current, DiscoveryService discovery, bool embeddedMode)
        {
            _embeddedMode = embeddedMode;
            _discovery = discovery;

            Settings = new AppSettings();
            Settings.UnityExePath = current == null ? null : current.UnityExePath;
            Settings.SdkZipPath = current == null ? null : current.SdkZipPath;
            Settings.LastOutputFolder = current == null ? null : current.LastOutputFolder;
            Settings.ModdersName = current == null ? null : current.ModdersName;
            Settings.WorkshopFamilyPreviewStyle = current == null ? null : current.WorkshopFamilyPreviewStyle;
            Settings.KeepUnityWorkerRunning = current == null || !current.KeepUnityWorkerRunning.HasValue
                ? (bool?)true
                : current.KeepUnityWorkerRunning;

            Text = "Memento Maker - Settings";
            StartPosition = FormStartPosition.CenterParent;
            Font = TwoPointTheme.BodyFont(9.25F);
            BackColor = TwoPointTheme.ContentBackground;
            MinimumSize = new Size(900, 600);

            if (_embeddedMode)
            {
                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.Manual;
                ClientSize = new Size(1280, 760);
            }
            else
            {
                ClientSize = new Size(1180, 760);
            }

            _uiToolTip = CreateUiToolTip();
            BuildInterface();
            PopulateFromSettings();
            UpdatePathValidation();

            if (!_embeddedMode)
            {
                TwoPointTheme.InstallShell(this, "Settings",
                    delegate
                    {
                        NavigationTarget = "Create";
                        DialogResult = DialogResult.Cancel;
                        Close();
                    },
                    delegate
                    {
                        NavigationTarget = "MyMods";
                        DialogResult = DialogResult.Cancel;
                        Close();
                    },
                    null);
            }
        }

        private void BuildInterface()
        {
            Panel root = new Panel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(7, 7, 7, 7);
            root.BackColor = TwoPointTheme.ContentBackground;
            Controls.Add(root);

            TableLayoutPanel page = new TableLayoutPanel();
            _pageLayout = page;
            page.Dock = DockStyle.Fill;
            page.ColumnCount = 2;
            page.RowCount = 4;
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57F));
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43F));
            page.RowStyles.Add(new RowStyle(SizeType.Percent, 56F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, CompactEnvironmentStatusHeight));
            page.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            root.Controls.Add(page);

            Panel setupSection = CreateSection("Setup Paths & Maintenance");
            setupSection.Margin = new Padding(0, 0, 6, 6);
            page.Controls.Add(setupSection, 0, 0);
            BuildSetupSection(GetSectionBody(setupSection));

            Panel outputSection = CreateSection("Build & Output");
            outputSection.Margin = new Padding(6, 0, 0, 6);
            page.Controls.Add(outputSection, 1, 0);
            BuildOutputSection(GetSectionBody(outputSection));

            Panel environmentSection = CreateSection("Environment Status");
            environmentSection.Margin = new Padding(0, 0, 0, 6);
            page.SetColumnSpan(environmentSection, 2);
            page.Controls.Add(environmentSection, 0, 1);
            BuildEnvironmentSection(GetSectionBody(environmentSection));

            Panel logSection = CreateSection("Build Log");
            logSection.Margin = new Padding(0, 0, 0, 6);
            page.SetColumnSpan(logSection, 2);
            page.Controls.Add(logSection, 0, 2);
            BuildLogSection(GetSectionBody(logSection));

            Panel actions = new Panel();
            actions.Dock = DockStyle.Fill;
            actions.BackColor = TwoPointTheme.ContentBackground;
            actions.Margin = new Padding(0);
            page.SetColumnSpan(actions, 2);
            page.Controls.Add(actions, 0, 3);
            BuildActions(actions);
        }

        private Panel CreateSection(string titleText)
        {
            Panel outer = new Panel();
            outer.Dock = DockStyle.Fill;
            outer.BackColor = TwoPointTheme.PanelLight;
            outer.BorderStyle = BorderStyle.FixedSingle;

            Label title = new Label();
            title.Text = titleText;
            title.Location = new Point(9, 7);
            title.Size = new Size(220, 29);
            TwoPointTheme.StyleSectionHeader(title);
            outer.Controls.Add(title);

            Panel body = new Panel();
            body.Name = "SectionBody";
            body.Location = new Point(7, 42);
            body.Size = new Size(Math.Max(10, outer.ClientSize.Width - 14), Math.Max(10, outer.ClientSize.Height - 49));
            body.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            body.BackColor = TwoPointTheme.PanelLight;
            outer.Controls.Add(body);

            return outer;
        }

        private static Panel GetSectionBody(Panel section)
        {
            foreach (Control control in section.Controls)
            {
                Panel body = control as Panel;
                if (body != null && string.Equals(body.Name, "SectionBody", StringComparison.Ordinal))
                    return body;
            }
            return section;
        }

        private void BuildSetupSection(Panel body)
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(7, 4, 7, 5);
            layout.ColumnCount = 1;
            layout.RowCount = 5;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            body.Controls.Add(layout);

            _unityPath = AddPathRow(layout, 0, "Unity Executable", "Unity 2020.3.47f1 Editor executable.", true,
                delegate { BrowseFile(_unityPath, "Unity executable|Unity.exe"); });
            _sdkPath = AddPathRow(layout, 1, "ModdingProject.zip (SDK)", "Official Two Point Museum Modding SDK archive.", true,
                delegate { BrowseFile(_sdkPath, "ModdingProject.zip|ModdingProject.zip|ZIP files|*.zip"); });
            _privateEnvironmentPath = AddPathRow(layout, 2, "Private Environment", "Managed automatically by Memento Maker.", false, null);
            _privateEnvironmentPath.ReadOnly = true;
            _privateEnvironmentPath.TabStop = false;
            _privateEnvironmentPath.BackColor = TwoPointTheme.ContentBackground;
            _privateEnvironmentPath.BorderStyle = BorderStyle.None;

            Panel maintenance = new Panel();
            maintenance.Dock = DockStyle.Fill;
            maintenance.Margin = new Padding(0, 3, 0, 0);
            layout.Controls.Add(maintenance, 0, 3);

            FlowLayoutPanel tools = new FlowLayoutPanel();
            tools.Dock = DockStyle.Top;
            tools.Height = 42;
            tools.FlowDirection = FlowDirection.LeftToRight;
            tools.WrapContents = false;
            tools.Padding = new Padding(0, 3, 0, 0);
            tools.BackColor = Color.Transparent;
            maintenance.Controls.Add(tools);

            Button detect = NewSecondaryButton("Auto Detect", 110);
            detect.Click += delegate { AutoDetect(); };
            tools.Controls.Add(detect);

            _refreshButton = NewSecondaryButton("Check & Repair", 125);
            _refreshButton.Click += delegate
            {
                ApplyFieldsToSettings();
                if (RefreshRequested != null)
                    RefreshRequested(this, EventArgs.Empty);
            };
            _refreshButton.AutoSize = false;
            tools.Controls.Add(_refreshButton);

            _rebuildButton = NewSecondaryButton("Rebuild Environment", 155);
            _rebuildButton.Click += delegate
            {
                ApplyFieldsToSettings();
                if (RebuildEnvironmentRequested != null)
                    RebuildEnvironmentRequested(this, EventArgs.Empty);
            };
            tools.Controls.Add(_rebuildButton);

            Button reset = NewSecondaryButton("Reset Defaults", 125);
            reset.Click += delegate { ResetDefaults(); };
            tools.Controls.Add(reset);

            _workshopCheckButton = NewSecondaryButton("Check Workshop", 128);
            _workshopCheckButton.Click += delegate
            {
                ApplyFieldsToSettings();
                if (WorkshopQueryRequested != null)
                    WorkshopQueryRequested(this, EventArgs.Empty);
            };
            tools.Controls.Add(_workshopCheckButton);

            BuildAboutDiagnostics(layout, 4);
        }

        private void BuildOutputSection(Panel body)
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(9, 6, 9, 8);
            layout.ColumnCount = 1;
            layout.RowCount = 4;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            body.Controls.Add(layout);

            _outputPath = AddPathRow(layout, 0, "Default Output Folder", "Built mods are saved here by default.", true,
                delegate { BrowseFolder(_outputPath); });

            Panel gameFolder = new Panel();
            gameFolder.Dock = DockStyle.Fill;
            gameFolder.Margin = new Padding(0, 4, 0, 0);
            layout.Controls.Add(gameFolder, 0, 1);

            Label gameTitle = NewLabel("Game Mods Folder", true);
            gameTitle.Location = new Point(0, 2);
            gameFolder.Controls.Add(gameTitle);

            Label gamePath = NewLabel(SettingsService.GetGameModsFolder(), false);
            gamePath.Location = new Point(0, 24);
            gamePath.Size = new Size(Math.Max(200, gameFolder.ClientSize.Width - 118), 30);
            gamePath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            gamePath.AutoEllipsis = true;
            gameFolder.Controls.Add(gamePath);

            Button open = NewSecondaryButton("Open Folder", 105);
            open.Location = new Point(Math.Max(0, gameFolder.ClientSize.Width - 108), 18);
            open.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            open.Click += delegate { OpenFolder(SettingsService.GetGameModsFolder()); };
            gameFolder.Controls.Add(open);

            Panel performance = new Panel();
            performance.Dock = DockStyle.Fill;
            performance.Margin = new Padding(0, 2, 0, 2);
            layout.Controls.Add(performance, 0, 2);

            _keepUnityWorkerRunning = new CheckBox();
            _keepUnityWorkerRunning.Text = "Keep private Unity session running (recommended)";
            _keepUnityWorkerRunning.Checked = Settings.KeepUnityWorkerRunning != false;
            _keepUnityWorkerRunning.AutoSize = true;
            _keepUnityWorkerRunning.Location = new Point(0, 2);
            _keepUnityWorkerRunning.ForeColor = TwoPointTheme.PrimaryText;
            _keepUnityWorkerRunning.Font = TwoPointTheme.BoldFont(8.8F);
            performance.Controls.Add(_keepUnityWorkerRunning);
            SetUiTip(_keepUnityWorkerRunning, "Keep Unity open privately in the background for faster builds and Workshop actions. Disable to launch Unity only when needed.");

            Label workerHint = NewLabel("Faster builds and Workshop actions; disable to use one-shot Unity sessions.", false);
            workerHint.Location = new Point(20, 24);
            workerHint.Size = new Size(Math.Max(220, performance.ClientSize.Width - 24), 18);
            workerHint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            performance.Controls.Add(workerHint);

            Panel modder = new Panel();
            modder.Dock = DockStyle.Fill;
            modder.Margin = new Padding(0, 5, 0, 0);
            layout.Controls.Add(modder, 0, 3);

            Label modderTitle = NewLabel("Modders Name", true);
            modderTitle.Location = new Point(0, 3);
            modderTitle.Size = new Size(170, 20);
            modder.Controls.Add(modderTitle);

            Label modderHint = NewLabel("Used in generated .asset filenames. Special symbols/characters are ignored.", false);
            modderHint.Location = new Point(0, 25);
            modderHint.Size = new Size(310, 38);
            modderHint.AutoSize = false;
            modder.Controls.Add(modderHint);

            _moddersName = new TextBox();
            _moddersName.Location = new Point(320, 10);
            _moddersName.Size = new Size(Math.Max(150, modder.ClientSize.Width - 325), 26);
            _moddersName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _moddersName.BorderStyle = BorderStyle.FixedSingle;
            _moddersName.BackColor = Color.White;
            _moddersName.ForeColor = TwoPointTheme.PrimaryText;
            _moddersName.Font = TwoPointTheme.BodyFont(9F);
            modder.Controls.Add(_moddersName);
            SetUiTip(_moddersName, "Used in generated .asset filenames for diagnostics. Special symbols are removed automatically.");

            _workshopToolTip = new ToolTip();
        }

        private void BuildAboutDiagnostics(TableLayoutPanel layout, int row)
        {
            Panel about = new Panel();
            about.Dock = DockStyle.Fill;
            about.Margin = new Padding(0, 3, 0, 0);
            about.BackColor = TwoPointTheme.PanelLight;
            layout.Controls.Add(about, 0, row);

            Label title = NewLabel("About & Diagnostics", true);
            title.Location = new Point(0, 2);
            title.Size = new Size(155, 20);
            about.Controls.Add(title);

            Label version = NewLabel("Memento Maker " + AppInfo.DisplayVersion + "  •  Automation " + EnvironmentService.AutomationVersion, false);
            version.Location = new Point(0, 23);
            version.Size = new Size(Math.Max(180, about.ClientSize.Width - 270), 20);
            version.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            about.Controls.Add(version);

            PictureBox creatorLogo = new PictureBox();
            creatorLogo.Image = TwoPointTheme.LoadThemeImage("SeanB_Logo.png");
            creatorLogo.SizeMode = PictureBoxSizeMode.Zoom;
            creatorLogo.BackColor = Color.Transparent;
            creatorLogo.Location = new Point(0, 47);
            creatorLogo.Size = new Size(54, 36);
            about.Controls.Add(creatorLogo);

            Label credits = NewLabel(AppInfo.CreatorCredit + "\r\n" + AppInfo.AiAssistanceCredit, false);
            credits.Location = new Point(82, 50);
            credits.Size = new Size(Math.Max(220, about.ClientSize.Width - 92), 44);
            credits.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            credits.ForeColor = TwoPointTheme.BodyText;
            about.Controls.Add(credits);

            // Keep diagnostic actions in a dedicated right-docked strip.
            // Do not calculate button positions from about.ClientSize here: this method is
            // called before the TableLayoutPanel has completed its first layout pass, so
            // ClientSize can still be zero/small on some DPI and window configurations.
            FlowLayoutPanel diagnosticActions = new FlowLayoutPanel();
            diagnosticActions.Dock = DockStyle.Right;
            diagnosticActions.Width = 430;
            diagnosticActions.Height = 40;
            diagnosticActions.FlowDirection = FlowDirection.LeftToRight;
            diagnosticActions.WrapContents = false;
            diagnosticActions.Padding = new Padding(0, 5, 0, 0);
            diagnosticActions.Margin = new Padding(0);
            diagnosticActions.BackColor = Color.Transparent;
            about.Controls.Add(diagnosticActions);
            diagnosticActions.BringToFront();

            Button copy = NewSecondaryButton("Copy Diagnostics", 128);
            copy.Size = new Size(128, 28);
            copy.Margin = new Padding(0, 0, 8, 0);
            copy.Click += delegate { CopyDiagnostics(); };
            diagnosticActions.Controls.Add(copy);

            Button bundle = NewSecondaryButton("Create Support Bundle", 148);
            bundle.Size = new Size(148, 28);
            bundle.Margin = new Padding(0, 0, 8, 0);
            bundle.Click += delegate { CreateSupportBundle(); };
            diagnosticActions.Controls.Add(bundle);

            Button logs = NewSecondaryButton("Open Logs Folder", 123);
            logs.Size = new Size(123, 28);
            logs.Margin = new Padding(0);
            logs.Click += delegate
            {
                string folder = BetaSupportService.LogsFolder;
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
                OpenFolder(folder);
            };
            diagnosticActions.Controls.Add(logs);
        }

        private void CopyDiagnostics()
        {
            try
            {
                ApplyFieldsToSettings();
                string diagnostics = AppInfo.BuildDiagnostics(Settings, _privateEnvironmentPath == null ? null : _privateEnvironmentPath.Text, _diagnosticsWorkerStatus);
                Clipboard.SetText(diagnostics);
                TwoPointTheme.ShowMessage(this,
                    "Diagnostics copied to the clipboard.\n\nYou can paste this information into a support message without copying the full Build Log.",
                    "Diagnostics Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                TwoPointTheme.ShowMessage(this, ex.Message, "Diagnostics Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CreateSupportBundle()
        {
            try
            {
                ApplyFieldsToSettings();
                SaveFileDialog dialog = new SaveFileDialog();
                dialog.Title = "Save Memento Maker Support Bundle";
                dialog.Filter = "ZIP archive|*.zip";
                dialog.DefaultExt = "zip";
                dialog.AddExtension = true;
                dialog.FileName = "MementoMaker_Support_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".zip";
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (!string.IsNullOrEmpty(desktop) && Directory.Exists(desktop))
                    dialog.InitialDirectory = desktop;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                BetaSupportService.CreateSupportBundle(
                    dialog.FileName,
                    Settings,
                    _privateEnvironmentPath == null ? null : _privateEnvironmentPath.Text,
                    _diagnosticsWorkerStatus,
                    _buildLog == null ? null : _buildLog.Text);

                TwoPointTheme.ShowMessage(this,
                    "Support bundle created successfully.\n\n" + dialog.FileName +
                    "\n\nIt contains diagnostics and logs, but no source artwork or built mod content.",
                    "Support Bundle Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                TwoPointTheme.ShowMessage(this,
                    "Memento Maker could not create the support bundle.\n\n" + ex.Message,
                    "Support Bundle Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BuildEnvironmentSection(Panel body)
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(7, 7, 7, 13);
            layout.ColumnCount = 6;
            layout.RowCount = 1;
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            for (int i = 0; i < 6; i++)
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.6667F));
            body.Controls.Add(layout);

            _sdkStatus = new SettingsStatusRow("SDK Found");
            _hubStatus = new SettingsStatusRow("Unity Hub");
            _unityStatus = new SettingsStatusRow("Unity 2020.3.47f1");
            _environmentStatus = new SettingsStatusRow("Private Environment");
            _workerStatus = new SettingsStatusRow("Unity Worker");
            _workshopStatusRow = new SettingsStatusRow("Steam Workshop");

            _sdkStatus.SetAction("Install", delegate { PrerequisiteInstallService.PromptInstallSdk(this); });
            _hubStatus.SetAction("Install", delegate { PrerequisiteInstallService.PromptInstallUnityHub(this); });
            _unityStatus.SetAction("Install", delegate { PrerequisiteInstallService.PromptInstallUnityEditor(this); });

            _sdkStatus.Margin = new Padding(0, 0, 4, 8);
            _hubStatus.Margin = new Padding(4, 0, 4, 8);
            _unityStatus.Margin = new Padding(4, 0, 4, 8);
            _environmentStatus.Margin = new Padding(4, 0, 4, 8);
            _workerStatus.Margin = new Padding(4, 0, 4, 8);
            _workshopStatusRow.Margin = new Padding(4, 0, 0, 8);

            layout.Controls.Add(_sdkStatus, 0, 0);
            layout.Controls.Add(_hubStatus, 1, 0);
            layout.Controls.Add(_unityStatus, 2, 0);
            layout.Controls.Add(_environmentStatus, 3, 0);
            layout.Controls.Add(_workerStatus, 4, 0);
            layout.Controls.Add(_workshopStatusRow, 5, 0);

            _workshopStatusRow.SetWarning("Not checked yet. Use Check Workshop above.");
            if (_workshopToolTip != null)
                _workshopToolTip.SetToolTip(_workshopStatusRow, "Keep Steam running and logged in, then choose Check Workshop.");
        }

        private void UpdateEnvironmentStatusLayout()
        {
            if (_pageLayout == null || _pageLayout.RowStyles.Count < 2)
                return;

            bool needsActionRow =
                (_sdkStatus != null && _sdkStatus.HasVisibleAction) ||
                (_hubStatus != null && _hubStatus.HasVisibleAction) ||
                (_unityStatus != null && _unityStatus.HasVisibleAction);

            float targetHeight = needsActionRow ? ExpandedEnvironmentStatusHeight : CompactEnvironmentStatusHeight;
            RowStyle environmentRow = _pageLayout.RowStyles[1];
            if (environmentRow.SizeType != SizeType.Absolute || Math.Abs(environmentRow.Height - targetHeight) > 0.1F)
            {
                environmentRow.SizeType = SizeType.Absolute;
                environmentRow.Height = targetHeight;
                _pageLayout.PerformLayout();
                _pageLayout.Invalidate();
            }
        }

        private void BuildLogSection(Panel body)
        {
            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.Padding = new Padding(6, 5, 6, 5);
            host.BackColor = TwoPointTheme.PanelLight;
            body.Controls.Add(host);

            Button clear = NewSecondaryButton("Clear Log", 95);
            clear.Size = new Size(95, 27);
            clear.Location = new Point(Math.Max(0, host.ClientSize.Width - 101), 0);
            clear.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            clear.Click += delegate
            {
                _buildLog.Clear();
                UpdateLogScrollBar();
                if (ClearLogRequested != null)
                    ClearLogRequested(this, EventArgs.Empty);
            };
            host.Controls.Add(clear);

            Panel logFrame = new Panel();
            logFrame.Location = new Point(0, 33);
            logFrame.Size = new Size(Math.Max(10, host.ClientSize.Width), Math.Max(10, host.ClientSize.Height - 33));
            logFrame.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            logFrame.BackColor = TwoPointTheme.LogBackground;
            logFrame.BorderStyle = BorderStyle.FixedSingle;
            host.Controls.Add(logFrame);

            _buildLog = new RichTextBox();
            _buildLog.Location = new Point(7, 5);
            _buildLog.Size = new Size(Math.Max(10, logFrame.ClientSize.Width - 29), Math.Max(10, logFrame.ClientSize.Height - 10));
            _buildLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _buildLog.ReadOnly = true;
            _buildLog.BorderStyle = BorderStyle.None;
            _buildLog.ScrollBars = RichTextBoxScrollBars.None;
            _buildLog.BackColor = TwoPointTheme.LogBackground;
            _buildLog.ForeColor = TwoPointTheme.LogText;
            _buildLog.Font = new Font("Consolas", 8.5F);
            _buildLog.ShortcutsEnabled = true;
            _buildLog.DetectUrls = false;
            _buildLog.HideSelection = false;
            _buildLog.ContextMenuStrip = BuildLogContextMenu();
            _buildLog.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.A)
                {
                    _buildLog.SelectAll();
                    e.SuppressKeyPress = true;
                }
            };
            _buildLog.MouseWheel += delegate(object sender, MouseEventArgs e)
            {
                if (_logScroll == null || _logScroll.Maximum <= 0)
                    return;
                int step = Math.Max(1, _logScroll.LargeChange / 3);
                _logScroll.Value = Math.Max(0, Math.Min(_logScroll.Maximum, _logScroll.Value + (e.Delta > 0 ? -step : step)));
                ScrollLogToValue();
            };
            logFrame.Controls.Add(_buildLog);

            _logScroll = new TwoPointVerticalScrollBar();
            _logScroll.Location = new Point(Math.Max(0, logFrame.ClientSize.Width - 20), 2);
            _logScroll.Size = new Size(18, Math.Max(10, logFrame.ClientSize.Height - 4));
            _logScroll.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
            _logScroll.ValueChanged += delegate { ScrollLogToValue(); };
            logFrame.Controls.Add(_logScroll);

            logFrame.Resize += delegate { UpdateLogScrollBar(); };
        }

        private ContextMenuStrip BuildLogContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = TwoPointTheme.BodyFont(9F);

            ToolStripMenuItem copy = new ToolStripMenuItem("Copy");
            copy.Enabled = false;
            copy.Click += delegate
            {
                if (!string.IsNullOrEmpty(_buildLog.SelectedText))
                    _buildLog.Copy();
            };
            menu.Items.Add(copy);

            ToolStripMenuItem selectAll = new ToolStripMenuItem("Select All");
            selectAll.Click += delegate { _buildLog.SelectAll(); };
            menu.Items.Add(selectAll);

            menu.Opening += delegate(object sender, System.ComponentModel.CancelEventArgs e)
            {
                copy.Enabled = _buildLog != null && !string.IsNullOrEmpty(_buildLog.SelectedText);
            };

            TwoPointTheme.StyleContextMenu(menu);
            return menu;
        }

        private void BuildActions(Panel body)
        {
            _overallStatus = new Label();
            _overallStatus.Text = "Checking environment...";
            _overallStatus.ForeColor = TwoPointTheme.BodyText;
            _overallStatus.Font = TwoPointTheme.BoldFont(9F);
            _overallStatus.Location = new Point(8, 14);
            _overallStatus.Size = new Size(Math.Max(200, body.ClientSize.Width - 190), 26);
            _overallStatus.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            body.Controls.Add(_overallStatus);

            _saveButton = new Button();
            _saveButton.Text = "Save Settings";
            _saveButton.Size = TwoPointTheme.BottomActionButtonSize;
            _saveButton.Location = new Point(Math.Max(0, body.ClientSize.Width - 156), 6);
            _saveButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _saveButton.Click += delegate { SaveAndStay(); };
            TwoPointTheme.StylePrimaryButton(_saveButton);
            TwoPointTheme.ApplyBottomActionFont(_saveButton);
            body.Controls.Add(_saveButton);
            SetUiTip(_saveButton, "Save these settings and keep the Settings page open.");
        }

        private TextBox AddPathRow(TableLayoutPanel parent, int row, string titleText, string hintText, bool editable, EventHandler browseHandler)
        {
            Panel holder = new Panel();
            holder.Dock = DockStyle.Fill;
            holder.Margin = new Padding(0, 1, 0, 1);
            parent.Controls.Add(holder, 0, row);

            Label title = NewLabel(titleText, true);
            title.Location = new Point(0, 0);
            title.Size = new Size(180, 20);
            holder.Controls.Add(title);

            Label hint = NewLabel(hintText, false);
            hint.Location = new Point(0, 21);
            hint.Size = new Size(215, 30);
            holder.Controls.Add(hint);

            int browseWidth = browseHandler == null ? 0 : 88;
            TextBox box = new TextBox();
            box.Location = new Point(220, 9);
            box.Size = new Size(Math.Max(80, holder.ClientSize.Width - 226 - browseWidth - (browseWidth > 0 ? 7 : 0)), 25);
            box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            box.ReadOnly = !editable;
            box.BackColor = editable ? TwoPointTheme.FieldBackground : TwoPointTheme.ContentBackground;
            box.ForeColor = TwoPointTheme.BodyText;
            box.Font = TwoPointTheme.BodyFont(8.7F);
            box.TextChanged += delegate { UpdatePathValidation(); };
            holder.Controls.Add(box);


            if (browseHandler != null)
            {
                Button browse = NewSecondaryButton("Browse...", browseWidth);
                browse.Location = new Point(Math.Max(0, holder.ClientSize.Width - browseWidth), 7);
                browse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                browse.Click += browseHandler;
                holder.Controls.Add(browse);

            }

            return box;
        }

        private static Label NewLabel(string text, bool bold)
        {
            Label label = new Label();
            label.Text = text;
            // Labels in Memento Maker use ampersands as visible wording, not keyboard mnemonics.
            label.UseMnemonic = false;
            label.AutoSize = false;
            label.ForeColor = bold ? TwoPointTheme.PrimaryText : TwoPointTheme.BodyText;
            label.Font = bold ? TwoPointTheme.BoldFont(9F) : TwoPointTheme.BodyFont(8.1F);
            return label;
        }

        private Button NewSecondaryButton(string text, int width)
        {
            Button button = new Button();
            button.Text = text;
            // Ampersands in Memento Maker button captions are visible wording, not WinForms mnemonics.
            button.UseMnemonic = false;
            button.Size = new Size(width, 32);
            button.Margin = new Padding(0, 0, 7, 0);
            TwoPointTheme.StyleButton(button);
            SetUiTip(button, GetSettingsButtonToolTip(text));
            return button;
        }

        private static ToolTip CreateUiToolTip()
        {
            ToolTip tip = new ToolTip();
            tip.AutoPopDelay = 8000;
            tip.InitialDelay = 550;
            tip.ReshowDelay = 120;
            tip.ShowAlways = true;
            return tip;
        }

        private void SetUiTip(Control control, string text)
        {
            if (_uiToolTip != null && control != null && !string.IsNullOrWhiteSpace(text))
                _uiToolTip.SetToolTip(control, text);
        }

        private static string GetSettingsButtonToolTip(string text)
        {
            switch (text)
            {
                case "Auto Detect": return "Search common locations for Unity 2020.3.47f1 and the Two Point Museum Modding SDK.";
                case "Check & Repair": return "Validate the configured paths and repair the private Memento Maker Unity environment when needed.";
                case "Rebuild Environment": return "Recreate Memento Maker's private Unity environment from the configured SDK. Use this when the SDK changes or repair cannot recover it.";
                case "Reset Defaults": return "Reset Memento Maker setup/output settings to their recommended defaults.";
                case "Check Workshop": return "Refresh Steam Workshop connectivity and published-item status. Steam must be running and logged in.";
                case "Copy Diagnostics": return "Copy version, path and environment status information to the clipboard for support.";
                case "Create Support Bundle": return "Create a diagnostic support bundle containing logs and environment information.";
                case "Open Logs Folder": return "Open Memento Maker logs, including installer and build diagnostics.";
                case "Clear Log": return "Clear the visible Build Log. This does not delete persistent log files.";
                default: return string.Empty;
            }
        }

        private void PopulateFromSettings()
        {
            _unityPath.Text = Settings.UnityExePath ?? "";
            _sdkPath.Text = Settings.SdkZipPath ?? "";
            _outputPath.Text = string.IsNullOrEmpty(Settings.LastOutputFolder) ? SettingsService.GetGameModsFolder() : Settings.LastOutputFolder;
            if (_moddersName != null)
                _moddersName.Text = Settings.ModdersName ?? "";
            if (_keepUnityWorkerRunning != null)
                _keepUnityWorkerRunning.Checked = Settings.KeepUnityWorkerRunning != false;
            _privateEnvironmentPath.Text = Path.Combine(AppInfo.LocalDataRoot, "Environment", "MuseumModding");
        }

        private void ApplyFieldsToSettings()
        {
            Settings.UnityExePath = (_unityPath.Text ?? "").Trim();
            Settings.SdkZipPath = (_sdkPath.Text ?? "").Trim();
            Settings.LastOutputFolder = (_outputPath.Text ?? "").Trim();
            Settings.ModdersName = _moddersName == null ? (Settings.ModdersName ?? "") : (_moddersName.Text ?? "").Trim();
            Settings.KeepUnityWorkerRunning = _keepUnityWorkerRunning == null || _keepUnityWorkerRunning.Checked;
        }

        private void SaveAndStay()
        {
            ApplyFieldsToSettings();
            if (SettingsSaved != null)
                SettingsSaved(this, EventArgs.Empty);
        }

        private void AutoDetect()
        {
            string unity = _discovery.FindUnity(_unityPath.Text);
            if (!string.IsNullOrEmpty(unity))
                _unityPath.Text = unity;

            string sdk = _discovery.FindSdkZip(_sdkPath.Text);
            if (!string.IsNullOrEmpty(sdk))
                _sdkPath.Text = sdk;

            UpdatePathValidation();
        }

        private void ResetDefaults()
        {
            DialogResult result = TwoPointTheme.ShowMessage(this,
                "Reset the Settings page to the recommended paths?\n\nThis does not delete any mods or the private Unity environment.",
                "Reset Defaults", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
                return;

            _unityPath.Text = _discovery.FindUnity(null) ?? "";
            _sdkPath.Text = _discovery.FindSdkZip(null) ?? "";
            _outputPath.Text = SettingsService.GetGameModsFolder();
            if (_keepUnityWorkerRunning != null)
                _keepUnityWorkerRunning.Checked = true;
            UpdatePathValidation();
        }

        public void SetModdersName(string value)
        {
            string text = (value ?? "").Trim();
            Settings.ModdersName = text;
            if (_moddersName != null && !_moddersName.IsDisposed)
                _moddersName.Text = text;
        }

        public void UpdateEnvironmentStatus(PrerequisiteState state)
        {
            if (state == null)
            {
                UpdatePathValidation();
                return;
            }

            if (!string.IsNullOrEmpty(state.UnityExePath) && !_discovery.IsUnityPathValid(_unityPath.Text))
                _unityPath.Text = state.UnityExePath;
            if (!string.IsNullOrEmpty(state.SdkZipPath) && !_discovery.IsSdkZipValid(_sdkPath.Text))
                _sdkPath.Text = state.SdkZipPath;
            if (!string.IsNullOrEmpty(state.EnvironmentProjectPath))
                _privateEnvironmentPath.Text = state.EnvironmentProjectPath;

            _sdkStatus.SetState(state.SdkFound, state.SdkFound ? "ModdingProject.zip is valid and accessible." : "ModdingProject.zip was not found.");
            _hubStatus.SetState(state.UnityHubFound, state.UnityHubFound ? "Unity Hub is installed." : "Unity Hub was not found.");
            _unityStatus.SetState(state.UnityFound, state.UnityFound ? "Unity 2020.3.47f1 is available." : "Unity 2020.3.47f1 was not found.");
            UpdateEnvironmentStatusLayout();
            string environmentMessage = string.IsNullOrEmpty(state.EnvironmentStatusMessage)
                ? (state.EnvironmentReady ? "Private environment is healthy and ready for builds." : "Private environment is not ready yet.")
                : state.EnvironmentStatusMessage;
            bool environmentOk = state.EnvironmentReady || (state.EnvironmentHealth != null && state.EnvironmentHealth.IsUsable);
            if (state.EnvironmentHealth != null &&
                (state.EnvironmentHealth.SdkChanged ||
                 (state.EnvironmentHealth.NeedsRepair && !state.EnvironmentHealth.NeedsRebuild)))
            {
                _environmentStatus.SetWarning(environmentMessage);
            }
            else
            {
                _environmentStatus.SetState(environmentOk, environmentMessage);
            }
            if (_workerStatus != null && Settings.KeepUnityWorkerRunning == false)
                _workerStatus.SetState(true, "Persistent worker disabled. One-shot Unity mode is active.");

            bool ready = state.UnityFound && state.SdkFound && state.EnvironmentReady;
            bool sdkChanged = state.EnvironmentHealth != null && state.EnvironmentHealth.SdkChanged;
            if (ready && !state.UnityHubFound)
            {
                _overallStatus.Text = "! Ready to build, but Unity Hub was not detected. Install it to manage the supported Unity version and licence.";
                _overallStatus.ForeColor = Color.FromArgb(178, 92, 24);
            }
            else if (ready && sdkChanged)
            {
                _overallStatus.Text = "! Ready to build, but the SDK has changed. Rebuild Environment is recommended.";
                _overallStatus.ForeColor = Color.FromArgb(178, 92, 24);
            }
            else
            {
                _overallStatus.Text = ready ? "\u2713 All systems go! Memento Maker is ready to build mods." : "Setup requires attention before mods can be built.";
                _overallStatus.ForeColor = ready ? Color.FromArgb(58, 139, 37) : Color.FromArgb(178, 92, 24);
            }
        }

        public void UpdateUnityWorkerStatus(bool enabled, bool running, bool ready, string message)
        {
            _diagnosticsWorkerStatus = string.IsNullOrEmpty(message)
                ? (ready ? "Ready" : (running ? "Starting" : "Not running"))
                : message;

            if (_workerStatus == null)
                return;
            if (InvokeRequired)
            {
                BeginInvoke((Action<bool, bool, bool, string>)UpdateUnityWorkerStatus, enabled, running, ready, message);
                return;
            }

            if (!enabled)
            {
                _workerStatus.SetState(true, "Persistent worker disabled. One-shot Unity mode is active.");
                return;
            }

            string detail = message;
            if (string.IsNullOrEmpty(detail))
                detail = ready ? "Running and ready for builds and Workshop actions." : (running ? "Unity is starting in the background..." : "Not running yet.");
            _workerStatus.SetState(ready, detail);
        }

        private void UpdatePathValidation()
        {
            if (_unityStatus == null || _hubStatus == null || _sdkStatus == null || _environmentStatus == null)
                return;

            bool unity = _discovery.IsUnityPathValid(_unityPath.Text);
            bool hub = !string.IsNullOrEmpty(_discovery.FindUnityHub());
            bool sdk = _discovery.IsSdkZipValid(_sdkPath.Text);
            _sdkStatus.SetState(sdk, sdk ? "ModdingProject.zip path is valid." : "SDK archive is not valid.");
            _hubStatus.SetState(hub, hub ? "Unity Hub is installed." : "Unity Hub was not found.");
            _unityStatus.SetState(unity, unity ? "Unity 2020.3.47f1 path is valid." : "Supported Unity 2020.3.47f1 was not found.");
            UpdateEnvironmentStatusLayout();
        }

        public void SetWorkshopQueryState(bool busy, WorkshopQueryResult result, string message)
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
            {
                BeginInvoke((Action<bool, WorkshopQueryResult, string>)SetWorkshopQueryState, busy, result, message);
                return;
            }

            if (_workshopCheckButton != null)
            {
                _workshopCheckButton.Enabled = !busy;
                _workshopCheckButton.Text = busy ? "Checking..." : "Check Workshop";
            }

            string status = message ?? "";
            string detailTip = status;
            if (result != null && result.Success)
            {
                int count = result.Items == null ? 0 : result.Items.Count;
                string person = string.IsNullOrEmpty(result.SteamPersonaName) ? "Steam" : result.SteamPersonaName;
                status = "Connected as " + person + ". Found " + count + " published Workshop item" + (count == 1 ? "." : "s.");

                if (count > 0)
                {
                    System.Text.StringBuilder tip = new System.Text.StringBuilder();
                    tip.AppendLine(status);
                    int shown = Math.Min(12, count);
                    for (int i = 0; i < shown; i++)
                    {
                        WorkshopItemSummary item = result.Items[i];
                        if (item == null)
                            continue;
                        tip.AppendLine((i + 1) + ". " + (item.Title ?? "") + "  [" + (item.PublishedFileId ?? "") + "]");
                    }
                    if (count > shown)
                        tip.AppendLine("...and " + (count - shown) + " more.");
                    detailTip = tip.ToString().TrimEnd();
                }
                else
                {
                    detailTip = status;
                }
            }

            if (_workshopStatusRow != null)
            {
                string cardText = string.IsNullOrEmpty(status) ? (busy ? "Checking Steam Workshop..." : "Not checked yet.") : status;
                if (busy)
                    _workshopStatusRow.SetWarning(cardText);
                else
                    _workshopStatusRow.SetState(result != null && result.Success, cardText);
                if (_workshopToolTip != null)
                    _workshopToolTip.SetToolTip(_workshopStatusRow, string.IsNullOrEmpty(detailTip) ? cardText : detailTip);
            }
        }

        public void SetBuildLogText(string text)
        {
            if (_buildLog == null)
                return;
            _buildLog.Text = text ?? "";
            _buildLog.SelectionStart = _buildLog.TextLength;
            _buildLog.SelectionLength = 0;
            _buildLog.ScrollToCaret();
            UpdateLogScrollBar(true);
        }

        public void AppendBuildLogLine(string formattedLine)
        {
            if (_buildLog == null)
                return;
            _buildLog.AppendText((formattedLine ?? "") + Environment.NewLine);
            _buildLog.SelectionStart = _buildLog.TextLength;
            _buildLog.SelectionLength = 0;
            _buildLog.ScrollToCaret();
            UpdateLogScrollBar(true);
        }

        private void UpdateLogScrollBar()
        {
            UpdateLogScrollBar(false);
        }

        private void UpdateLogScrollBar(bool scrollToBottom)
        {
            if (_buildLog == null || _logScroll == null)
                return;

            int lineHeight = Math.Max(13, _buildLog.Font.Height);
            int visibleLines = Math.Max(1, _buildLog.ClientSize.Height / lineHeight);
            int totalLines = Math.Max(0, _buildLog.Lines.Length);
            int maximum = Math.Max(0, totalLines - visibleLines);

            _logScroll.LargeChange = visibleLines;
            _logScroll.Maximum = maximum;
            if (scrollToBottom)
                _logScroll.Value = maximum;
        }

        private void ScrollLogToValue()
        {
            if (_syncingLogScroll || _buildLog == null || _logScroll == null)
                return;
            if (_buildLog.Lines.Length == 0)
                return;

            try
            {
                _syncingLogScroll = true;
                int line = Math.Max(0, Math.Min(_buildLog.Lines.Length - 1, _logScroll.Value));
                int index = _buildLog.GetFirstCharIndexFromLine(line);
                if (index >= 0)
                {
                    _buildLog.SelectionStart = index;
                    _buildLog.SelectionLength = 0;
                    _buildLog.ScrollToCaret();
                }
            }
            finally
            {
                _syncingLogScroll = false;
            }
        }

        private void BrowseFile(TextBox target, string filter)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = filter;
                if (File.Exists(target.Text))
                    dialog.InitialDirectory = Path.GetDirectoryName(target.Text);
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    target.Text = dialog.FileName;
            }
        }

        private void BrowseFolder(TextBox target)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.SelectedPath = Directory.Exists(target.Text) ? target.Text : SettingsService.GetGameModsFolder();
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    target.Text = dialog.SelectedPath;
            }
        }

        private void OpenFolder(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch (Exception ex)
            {
                TwoPointTheme.ShowMessage(this, ex.Message, "Open Folder", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private sealed class SettingsStatusRow : Panel
        {
            private int _state;
            private readonly Label _title;
            private readonly Label _detail;
            private Button _actionButton;

            public SettingsStatusRow(string title)
            {
                Dock = DockStyle.Fill;
                BackColor = Color.FromArgb(248, 233, 201);
                BorderStyle = BorderStyle.FixedSingle;
                Padding = new Padding(50, 10, 9, 8);
                MinimumSize = new Size(180, 72);

                _title = new Label();
                _title.Text = title;
                _title.UseMnemonic = false;
                _title.ForeColor = TwoPointTheme.PrimaryText;
                _title.Font = TwoPointTheme.BoldFont(9.2F);
                _title.Location = new Point(52, 10);
                _title.Size = new Size(Math.Max(100, Width - 61), 20);
                _title.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                Controls.Add(_title);

                _detail = new Label();
                _detail.UseMnemonic = false;
                _detail.ForeColor = TwoPointTheme.BodyText;
                _detail.Font = TwoPointTheme.BodyFont(8.2F);
                _detail.Location = new Point(52, 33);
                _detail.Size = new Size(Math.Max(100, Width - 61), 34);
                _detail.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                Controls.Add(_detail);

                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            }

            public bool HasVisibleAction
            {
                get { return _actionButton != null && _actionButton.Visible; }
            }

            public void SetAction(string text, EventHandler handler)
            {
                if (_actionButton == null)
                {
                    _actionButton = new Button();
                    _actionButton.Size = new Size(78, 25);
                    _actionButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                    TwoPointTheme.StyleButton(_actionButton);
                    Controls.Add(_actionButton);
                }
                _actionButton.Text = string.IsNullOrEmpty(text) ? "Install" : text;
                _actionButton.Click += handler;
                LayoutActionButton();
                _actionButton.Visible = _state != 1;
            }

            private void LayoutActionButton()
            {
                if (_actionButton == null) return;
                _actionButton.Location = new Point(52, Math.Max(68, Height - 31));
                _actionButton.Width = Math.Max(78, Width - 62);
                _detail.Height = Math.Max(28, (_actionButton.Visible ? _actionButton.Top : Height - 8) - _detail.Top - 4);
            }

            public void SetState(bool ready, string detail)
            {
                _state = ready ? 1 : 0;
                _detail.Text = detail ?? "";
                if (_actionButton != null) _actionButton.Visible = !ready;
                LayoutActionButton();
                Invalidate();
            }

            public void SetWarning(string detail)
            {
                _state = 2;
                _detail.Text = detail ?? "";
                if (_actionButton != null) _actionButton.Visible = true;
                LayoutActionButton();
                Invalidate();
            }

            protected override void OnResize(EventArgs eventargs)
            {
                base.OnResize(eventargs);
                LayoutActionButton();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                int badgeY = HasVisibleAction ? 12 : Math.Max(12, (Height - 30) / 2);
                Rectangle badge = new Rectangle(14, badgeY, 30, 30);
                Color fill = _state == 1 ? Color.FromArgb(93, 174, 25) : (_state == 2 ? Color.FromArgb(241, 158, 0) : Color.FromArgb(213, 59, 49));
                using (SolidBrush brush = new SolidBrush(fill))
                    e.Graphics.FillEllipse(brush, badge);
                using (Pen pen = new Pen(Color.White, 2.5F))
                {
                    pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    if (_state == 1)
                    {
                        e.Graphics.DrawLine(pen, badge.Left + 7, badge.Top + 15, badge.Left + 12, badge.Top + 20);
                        e.Graphics.DrawLine(pen, badge.Left + 12, badge.Top + 20, badge.Left + 22, badge.Top + 9);
                    }
                    else if (_state == 2)
                    {
                        e.Graphics.DrawLine(pen, badge.Left + 15, badge.Top + 7, badge.Left + 15, badge.Top + 18);
                        e.Graphics.DrawEllipse(pen, badge.Left + 14, badge.Top + 22, 2, 2);
                    }
                    else
                    {
                        e.Graphics.DrawLine(pen, badge.Left + 8, badge.Top + 8, badge.Right - 8, badge.Bottom - 8);
                        e.Graphics.DrawLine(pen, badge.Right - 8, badge.Top + 8, badge.Left + 8, badge.Bottom - 8);
                    }
                }
            }
        }
    }
}

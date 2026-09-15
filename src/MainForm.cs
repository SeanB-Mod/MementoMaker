using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TPMSimpleModMaker
{
    internal sealed class MainForm : Form
    {
        private sealed class ArtworkPictureBox : PictureBox
        {
            public ArtworkPictureBox()
            {
                SetStyle(ControlStyles.Selectable, true);
                TabStop = true;
            }

            protected override bool IsInputKey(Keys keyData)
            {
                Keys keyCode = keyData & Keys.KeyCode;
                if (keyCode == Keys.Left || keyCode == Keys.Right || keyCode == Keys.Up || keyCode == Keys.Down)
                    return true;
                return base.IsInputKey(keyData);
            }
        }

        private static readonly string[] ArtworkTips = new string[]
        {
            "Tip: Drag the mouse to pan artwork.",
            "Tip: Arrow keys pan artwork.",
            "Tip: Shift + Arrow keys fine-tune pan.",
            "Tip: Mouse wheel zooms artwork.",
            "Tip: Shift + Mouse wheel fine-tunes zoom.",
            "Tip: Ctrl + Mouse wheel rotates artwork by 5°.",
            "Tip: Ctrl + Shift + Mouse wheel fine-tunes rotation by 1°.",
            "Tip: Ctrl + double-click resets rotation to 0°."
        };

        private readonly List<Control> _createPageControls = new List<Control>();
        private Panel _tabPageHost;
        private MyModsForm _embeddedMyMods;
        private SettingsForm _embeddedSettings;
        private bool _myModsNeedsRefresh;

        // Graphical Create Mod page. The existing build controls remain the source
        // of truth while this layer presents them in the themed workflow.
        private Panel _createVisualRoot;
        private Panel _rugOptionsVisualPanel;
        private TableLayoutPanel _createLeftLayout;
        private TableLayoutPanel _createPageLayout;
        private TableLayoutPanel _createMainLayout;
        private TableLayoutPanel _createRightLayout;
        private TableLayoutPanel _detailsRightFields;
        private TableLayoutPanel _artworkLayout;
        private FlowLayoutPanel _itemTilesFlow;
        private Panel _itemTilesViewport;
        private TwoPointHorizontalScrollBar _itemTilesScrollBar;
        private FlowLayoutPanel _shapeTilesFlow;
        private Panel _decorOptionsContentPanel;
        private Button _wallpaperOptionTile;
        private Panel _posterOptionsContentPanel;
        private Panel _rugOptionsContentPanel;
        private Panel _bannerOptionsContentPanel;
        private Panel _doubleBannerOptionsContentPanel;
        private Panel _hangingSignOptionsContentPanel;
        private Panel _wallSignOptionsContentPanel;
        private Panel _bannerTilesViewport;
        private FlowLayoutPanel _bannerTilesFlow;
        private TwoPointHorizontalScrollBar _bannerTilesScrollBar;
        private Panel _doubleBannerTilesViewport;
        private FlowLayoutPanel _doubleBannerTilesFlow;
        private TwoPointHorizontalScrollBar _doubleBannerTilesScrollBar;
        private TableLayoutPanel _createBottomActions;
        private Panel _createProgressHost;
        private Label _createProgressLabel;
        private Timer _createProgressHideTimer;
        private bool _createProgressBusy;
        private bool _applyingResponsiveProfile;
        private Label _artworkDropHint;
        private CheckBox _recolourToggle;
        private TwoPointSlider _costSlider;
        private TwoPointSlider _kudoshSlider;
        private Panel _costHolder;
        private Panel _kudoshHolder;
        private ComboBox _variantParentBox;
        private Label _variantOptionsLabel;
        private Label _variantHelpLabel;
        private bool _loadingVariantChoice;
        private ComboBox _decorPackBox;
        private Label _decorPackOptionsLabel;
        private Label _decorPackHelpLabel;
        private bool _loadingDecorPackChoice;
        private readonly Dictionary<string, Button> _itemTypeTiles = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Button> _posterSizeTiles = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Button> _rugShapeTiles = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Button> _bannerThemeTiles = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Button> _doubleBannerThemeTiles = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Button> _hangingSignSizeTiles = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Button> _wallSignSizeTiles = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Button> _fitButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Button> _guideColorSwatches = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        private ToolTip _uiToolTip;
        private Button _artworkBrowseButton;
        private bool _syncingGraphicalTemplateControls;
        private bool _preservePosterEditorStateOnTemplateChange;
        private bool _preserveRugEditorStateOnTemplateChange;
        private bool _preserveBannerEditorStateOnTemplateChange;
        private bool _preserveDoubleBannerEditorStateOnTemplateChange;
        private TableLayoutPanel _doubleBannerPreviewGrid;
        private TableLayoutPanel _doubleBannerLeftPane;
        private TableLayoutPanel _doubleBannerRightPane;
        private Panel _doubleBannerLeftPreviewFrame;
        private Panel _doubleBannerRightPreviewFrame;
        private PictureBox _doubleBannerRightPreview;
        private Label _doubleBannerRightDropHint;
        private Button _doubleBannerLeftBrowseButton;
        private Button _doubleBannerRightBrowseButton;
        private Button _doubleBannerLeftResetButton;
        private Button _doubleBannerRightResetButton;
        private Label _doubleBannerLeftTitleLabel;
        private Label _doubleBannerRightTitleLabel;
        private bool _doubleBannerEditingRight;
        private bool _switchingDoubleBannerSide;
        private string _doubleBannerLeftArtworkPath = "";
        private string _doubleBannerRightArtworkPath = "";
        private string _doubleBannerLeftFitMode = "Fill";
        private string _doubleBannerRightFitMode = "Fill";
        private ImagePlacementState _doubleBannerLeftPlacement = new ImagePlacementState();
        private ImagePlacementState _doubleBannerRightPlacement = new ImagePlacementState();
        private readonly SettingsService _settingsService;
        private readonly DiscoveryService _discovery;
        private readonly EnvironmentService _environment;
        private readonly BuildService _buildService;
        private readonly ImageProcessingService _imageProcessor;
        private readonly ProjectLibraryService _projectLibrary;
        private readonly WorkshopService _workshopService;
        private readonly UnityWorkerService _unityWorker;

        private AppSettings _settings;
        private List<TemplateDefinition> _templates;
        private PrerequisiteState _state;
        private WorkshopQueryResult _lastWorkshopQueryResult;
        private DateTime _lastWorkshopQueryUtc = DateTime.MinValue;
        private readonly Dictionary<string, DateTime> _verifiedWorkshopItemsUtc = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private static readonly TimeSpan WorkshopVerificationCacheLifetime = TimeSpan.FromMinutes(10);

        private readonly Label _unityStatus;
        private readonly Label _sdkStatus;
        private readonly Label _environmentStatus;
        private readonly ComboBox _templateBox;
        private readonly TextBox _modName;
        private readonly TextBox _description;
        private readonly NumericUpDown _cost;
        private readonly NumericUpDown _kudosh;
        private readonly TextBox _imagePath;
        private readonly PictureBox _imagePreview;
        private readonly Label _previewTitle;
        private readonly ComboBox _fitMode;
        private readonly ComboBox _guideColor;
        private readonly Button _resetViewButton;
        private readonly Label _mappingInfo;
        private readonly ComboBox _iconMode;
        private readonly TextBox _iconPath;
        private readonly Button _iconBrowse;
        private readonly PictureBox _iconPreview;
        private readonly TextBox _outputPath;
        private readonly Label _imageHint;
        private readonly Label _projectStatus;
        private readonly Button _buildButton;
        private readonly Button _newProjectButton;
        private readonly Button _myModsButton;
        private readonly Button _openOutputButton;
        private readonly Button _rebuildEnvironmentButton;
        private readonly RichTextBox _log;
        private readonly Label _activity;
        private readonly ProgressBar _progress;
        private string _lastBuiltOutput;
        private ImagePlacementState _placement = new ImagePlacementState();
        private bool _isDraggingPreview;
        private Point _lastPreviewMouse;
        private Timer _interactivePreviewTimer;
        private Timer _previewCommitTimer;
        private Timer _artworkTipTimer;
        private int _artworkTipIndex;
        private bool _artworkTipPaused;
        private bool _interactivePreviewPending;
        private Bitmap _interactiveArtworkSource;
        private string _interactiveArtworkSourcePath;
        private DateTime _interactiveArtworkSourceWriteUtc;
        private long _interactiveArtworkSourceLength;
        private ModProjectRecord _activeProject;
        private bool _environmentPreparedThisSession;
        private bool _allowClose;
        private bool _closingWorker;
        private readonly bool _showFirstRunSetup;

        private sealed class DecorPackChoice
        {
            public bool Standalone;
            public bool NewPack;
            public string PackKey;
            public string PackName;
            public int MemberCount;

            public override string ToString()
            {
                if (Standalone)
                    return "Standalone Wallpaper";
                if (NewPack)
                    return "New Décor Pack";
                string name = string.IsNullOrWhiteSpace(PackName) ? "Wallpaper Pack" : PackName.Trim();
                string count = MemberCount > 0 ? " (" + MemberCount + " wallpaper" + (MemberCount == 1 ? "" : "s") + ")" : "";
                return name + count;
            }
        }

        private sealed class QueuedRebuildWorkItem
        {
            public int QueuePosition;
            public int QueueTotal;
            public ModProjectRecord Record;
            public TemplateDefinition Template;
            public string OriginalArtwork;
            public string SecondaryArtwork;
            public string CustomIcon;
            public string FitMode;
            public string SecondaryFitMode;
            public ImagePlacementState Placement;
            public ImagePlacementState SecondaryPlacement;
            public string IconMode;
            public string OutputRoot;
            public string PreparedArtwork;
            public string PreparedIcon;
            public BuildJob Job;

            public string Prefix
            {
                get { return "[" + QueuePosition + "/" + QueueTotal + "] "; }
            }
        }

        public MainForm()
        {
            _settingsService = new SettingsService();
            _discovery = new DiscoveryService();
            _environment = new EnvironmentService();
            _buildService = new BuildService();
            _imageProcessor = new ImageProcessingService();
            _projectLibrary = new ProjectLibraryService();
            _workshopService = new WorkshopService();
            _unityWorker = new UnityWorkerService();
            _uiToolTip = CreateUiToolTip();

            // The artwork editor renders a lightweight cached preview while the user is
            // actively dragging/zooming. Full-quality texture + icon rendering is deferred
            // until interaction settles, keeping the UI responsive even with large artwork.
            _interactivePreviewTimer = new Timer();
            _interactivePreviewTimer.Interval = 16;
            _interactivePreviewTimer.Tick += delegate
            {
                if (_interactivePreviewPending)
                {
                    _interactivePreviewPending = false;
                    UpdateArtworkPreviewInteractive();
                }

                if (!_isDraggingPreview && !_interactivePreviewPending)
                    _interactivePreviewTimer.Stop();
            };

            _previewCommitTimer = new Timer();
            _previewCommitTimer.Interval = 140;
            _previewCommitTimer.Tick += delegate
            {
                _previewCommitTimer.Stop();
                _interactivePreviewTimer.Stop();
                _interactivePreviewPending = false;

                // Double Banner interactions use one shared placement editor backed by
                // two side-specific placement states. Commit the active side before the
                // full-quality redraw so the redraw can never restore an older pan/zoom/
                // rotation state over the live interactive result.
                CaptureActiveDoubleBannerSideState();
                UpdateArtworkPreview();
            };

            _artworkTipTimer = new Timer();
            _artworkTipTimer.Interval = 3600;
            _artworkTipTimer.Tick += delegate { AdvanceArtworkTip(); };

            _settings = _settingsService.Load();
            _showFirstRunSetup = !_settingsService.HadExistingSettings;
            _templates = LoadTemplates();

            Text = "Memento Maker";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(960, 620);
            ClientSize = new Size(1060, 900);
            WindowState = FormWindowState.Maximized;
            Font = new Font("Segoe UI", 9F);

            Panel header = new Panel();
            header.Name = "LegacyHeader";
            header.Dock = DockStyle.Top;
            header.Height = 70;
            header.BackColor = Color.FromArgb(39, 51, 61);
            Controls.Add(header);

            Label title = new Label();
            title.Text = "Memento Maker";
            title.ForeColor = Color.White;
            title.Font = new Font("Segoe UI Semibold", 18F);
            title.Location = new Point(20, 12);
            title.AutoSize = true;
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "Create ready-to-use texture mods without opening Unity";
            subtitle.ForeColor = Color.Gainsboro;
            subtitle.Location = new Point(23, 44);
            subtitle.AutoSize = true;
            header.Controls.Add(subtitle);

            _myModsButton = new Button();
            _myModsButton.Text = "My Mods";
            _myModsButton.Location = new Point(842, 20);
            _myModsButton.Size = new Size(90, 32);
            _myModsButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _myModsButton.Click += async delegate { await ShowMyModsAsync(); };
            header.Controls.Add(_myModsButton);

            _newProjectButton = new Button();
            _newProjectButton.Text = "New Mod";
            _newProjectButton.Location = new Point(938, 20);
            _newProjectButton.Size = new Size(90, 32);
            _newProjectButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _newProjectButton.Click += delegate { RequestNewProject(); };
            header.Controls.Add(_newProjectButton);

            Panel setup = new Panel();
            setup.Location = new Point(18, 84);
            setup.Size = new Size(1024, 108);
            setup.BorderStyle = BorderStyle.FixedSingle;
            setup.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(setup);

            Label setupTitle = new Label();
            setupTitle.Text = "Environment";
            setupTitle.Font = new Font("Segoe UI Semibold", 11F);
            setupTitle.Location = new Point(12, 9);
            setupTitle.AutoSize = true;
            setup.Controls.Add(setupTitle);

            _unityStatus = NewStatusLabel(14, 36, 735);
            _sdkStatus = NewStatusLabel(14, 58, 735);
            _environmentStatus = NewStatusLabel(14, 80, 735);
            setup.Controls.Add(_unityStatus);
            setup.Controls.Add(_sdkStatus);
            setup.Controls.Add(_environmentStatus);

            Button settingsButton = new Button();
            settingsButton.Text = "Settings";
            settingsButton.Location = new Point(824, 18);
            settingsButton.Size = new Size(86, 30);
            settingsButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            settingsButton.Click += async delegate { await ShowSettingsAsync(); };
            setup.Controls.Add(settingsButton);

            Button refreshButton = new Button();
            refreshButton.Text = "Refresh";
            refreshButton.Location = new Point(915, 18);
            refreshButton.Size = new Size(86, 30);
            refreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            refreshButton.Click += async delegate { await RefreshEnvironmentAsync(false); };
            setup.Controls.Add(refreshButton);

            _rebuildEnvironmentButton = new Button();
            _rebuildEnvironmentButton.Text = "Rebuild Environment";
            _rebuildEnvironmentButton.Location = new Point(824, 58);
            _rebuildEnvironmentButton.Size = new Size(177, 30);
            _rebuildEnvironmentButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _rebuildEnvironmentButton.Click += async delegate { await RebuildEnvironmentAsync(); };
            setup.Controls.Add(_rebuildEnvironmentButton);

            Panel form = new Panel();
            form.Location = new Point(18, 206);
            form.Size = new Size(1024, 466);
            form.BorderStyle = BorderStyle.FixedSingle;
            form.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(form);

            int leftLabel = 15;
            int leftControl = 145;

            AddLabel(form, "Item type", leftLabel, 18);
            _templateBox = new ComboBox();
            _templateBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _templateBox.Location = new Point(leftControl, 14);
            _templateBox.Size = new Size(300, 26);
            foreach (TemplateDefinition template in _templates)
                _templateBox.Items.Add(template);
            _templateBox.SelectedIndexChanged += delegate { TemplateChanged(); };
            form.Controls.Add(_templateBox);

            _imageHint = new Label();
            _imageHint.Location = new Point(460, 15);
            _imageHint.Size = new Size(540, 43);
            _imageHint.ForeColor = Color.DimGray;
            form.Controls.Add(_imageHint);

            AddLabel(form, "Mod name", leftLabel, 58);
            _modName = new TextBox();
            _modName.Location = new Point(leftControl, 55);
            _modName.Size = new Size(300, 25);
            form.Controls.Add(_modName);

            _projectStatus = new Label();
            _projectStatus.Location = new Point(460, 57);
            _projectStatus.Size = new Size(540, 22);
            _projectStatus.ForeColor = Color.DimGray;
            form.Controls.Add(_projectStatus);

            AddLabel(form, "Description", leftLabel, 94);
            _description = new TextBox();
            _description.Location = new Point(leftControl, 91);
            _description.Size = new Size(300, 58);
            _description.Multiline = true;
            form.Controls.Add(_description);

            AddLabel(form, "Item cost", leftLabel, 162);
            _cost = new NumericUpDown();
            _cost.Location = new Point(leftControl, 158);
            _cost.Maximum = 1000000;
            _cost.Size = new Size(120, 25);
            form.Controls.Add(_cost);

            AddLabel(form, "Kudosh cost", leftLabel, 197);
            _kudosh = new NumericUpDown();
            _kudosh.Location = new Point(leftControl, 193);
            _kudosh.Maximum = 1000000;
            _kudosh.Size = new Size(120, 25);
            form.Controls.Add(_kudosh);

            AddLabel(form, "Artwork image", leftLabel, 238);
            _imagePath = new TextBox();
            _imagePath.Location = new Point(leftControl, 234);
            _imagePath.Size = new Size(500, 25);
            _imagePath.ReadOnly = true;
            form.Controls.Add(_imagePath);

            Button imageBrowse = new Button();
            imageBrowse.Text = "Browse...";
            imageBrowse.Location = new Point(654, 233);
            imageBrowse.Size = new Size(82, 27);
            imageBrowse.Click += delegate { BrowseArtwork(); };
            form.Controls.Add(imageBrowse);

            AddLabel(form, "Image fitting", leftLabel, 273);
            _fitMode = new ComboBox();
            _fitMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _fitMode.Location = new Point(leftControl, 269);
            _fitMode.Size = new Size(130, 26);
            _fitMode.Items.Add("Fill");
            _fitMode.Items.Add("Fit");
            _fitMode.Items.Add("Stretch");
            _fitMode.SelectedIndexChanged += delegate { if (!_switchingDoubleBannerSide) ResetPlacement(false); UpdateArtworkPreview(); };
            form.Controls.Add(_fitMode);

            Label fitHelp = new Label();
            fitHelp.Text = "Fill = crop to area   |   Fit = show whole image   |   Stretch = distort to area";
            fitHelp.Location = new Point(288, 273);
            fitHelp.Size = new Size(448, 22);
            fitHelp.ForeColor = Color.DimGray;
            form.Controls.Add(fitHelp);

            Label guideLabel = new Label();
            guideLabel.Text = "Guide colour";
            guideLabel.Location = new Point(758, 39);
            guideLabel.Size = new Size(78, 22);
            form.Controls.Add(guideLabel);

            _guideColor = new ComboBox();
            _guideColor.DropDownStyle = ComboBoxStyle.DropDownList;
            _guideColor.Location = new Point(843, 35);
            _guideColor.Size = new Size(100, 26);
            _guideColor.Items.AddRange(new object[] { "White", "Red", "Yellow", "Blue", "Orange", "Green", "Pink", "Purple" });
            _guideColor.SelectedIndexChanged += delegate { UpdateGuideColour(); };
            form.Controls.Add(_guideColor);

            _resetViewButton = new Button();
            _resetViewButton.Text = "Reset View";
            _resetViewButton.Location = new Point(949, 34);
            _resetViewButton.Size = new Size(77, 28);
            _resetViewButton.Click += delegate { ResetPlacement(true); };
            form.Controls.Add(_resetViewButton);

            AddLabel(form, "Icon source", leftLabel, 306);
            _iconMode = new ComboBox();
            _iconMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _iconMode.Location = new Point(leftControl, 302);
            _iconMode.Size = new Size(185, 26);
            _iconMode.Items.Add("Generated");
            _iconMode.Items.Add("Original Artwork");
            _iconMode.Items.Add("Custom");
            _iconMode.SelectedIndexChanged += delegate { UpdateIconControls(); };
            form.Controls.Add(_iconMode);

            Label iconModeHelp = new Label();
            iconModeHelp.Text = "All icons are generated at a maximum of 256 x 256.";
            iconModeHelp.Location = new Point(340, 306);
            iconModeHelp.Size = new Size(396, 22);
            iconModeHelp.ForeColor = Color.DimGray;
            form.Controls.Add(iconModeHelp);

            AddLabel(form, "Custom icon", leftLabel, 344);
            _iconPath = new TextBox();
            _iconPath.Location = new Point(leftControl, 340);
            _iconPath.Size = new Size(500, 25);
            _iconPath.ReadOnly = true;
            form.Controls.Add(_iconPath);

            _iconBrowse = new Button();
            _iconBrowse.Text = "Browse...";
            _iconBrowse.Location = new Point(654, 339);
            _iconBrowse.Size = new Size(82, 27);
            _iconBrowse.Click += delegate { BrowseIcon(); };
            form.Controls.Add(_iconBrowse);

            AddLabel(form, "Output folder", leftLabel, 384);
            _outputPath = new TextBox();
            _outputPath.Location = new Point(leftControl, 380);
            _outputPath.Size = new Size(500, 25);
            _outputPath.Text = _settings.LastOutputFolder ?? "";
            form.Controls.Add(_outputPath);

            Button outputBrowse = new Button();
            outputBrowse.Text = "Browse...";
            outputBrowse.Location = new Point(654, 379);
            outputBrowse.Size = new Size(82, 27);
            outputBrowse.Click += delegate { BrowseOutputFolder(); };
            form.Controls.Add(outputBrowse);

            Button resetOutput = new Button();
            resetOutput.Text = "Game Mods Folder";
            resetOutput.Location = new Point(145, 414);
            resetOutput.Size = new Size(130, 28);
            resetOutput.Click += delegate { _outputPath.Text = SettingsService.GetGameModsFolder(); };
            form.Controls.Add(resetOutput);

            Label outputHelp = new Label();
            outputHelp.Text = "Default: AppData\\LocalLow\\Two Point Studios\\Two Point Museum\\Mods";
            outputHelp.Location = new Point(285, 419);
            outputHelp.Size = new Size(451, 20);
            outputHelp.ForeColor = Color.DimGray;
            form.Controls.Add(outputHelp);

            _previewTitle = new Label();
            _previewTitle.Text = "Final texture preview";
            _previewTitle.Font = new Font("Segoe UI Semibold", 10F);
            _previewTitle.Location = new Point(758, 67);
            _previewTitle.AutoSize = true;
            form.Controls.Add(_previewTitle);

            _imagePreview = NewArtworkPreviewBox(758, 91, 242, 230);
            _imagePreview.MouseDown += ImagePreview_MouseDown;
            _imagePreview.MouseMove += ImagePreview_MouseMove;
            _imagePreview.MouseUp += ImagePreview_MouseUp;
            _imagePreview.MouseEnter += delegate { _imagePreview.Focus(); };
            _imagePreview.MouseWheel += ImagePreview_MouseWheel;
            _imagePreview.KeyDown += ImagePreview_KeyDown;
            _imagePreview.DoubleClick += ImagePreview_DoubleClick;
            form.Controls.Add(_imagePreview);

            _mappingInfo = new Label();
            _mappingInfo.Location = new Point(758, 325);
            _mappingInfo.Size = new Size(242, 58);
            _mappingInfo.ForeColor = Color.DimGray;
            form.Controls.Add(_mappingInfo);

            Label iconPreviewTitle = new Label();
            iconPreviewTitle.Text = "Generated icon preview";
            iconPreviewTitle.Location = new Point(758, 386);
            iconPreviewTitle.AutoSize = true;
            form.Controls.Add(iconPreviewTitle);

            _iconPreview = NewPreviewBox(845, 379, 72, 72);
            form.Controls.Add(_iconPreview);

            _buildButton = new Button();
            _buildButton.Text = "Create Mod";
            _buildButton.Font = new Font("Segoe UI Semibold", 11F);
            _buildButton.Location = new Point(18, 688);
            _buildButton.Size = new Size(175, 43);
            _buildButton.Click += async delegate { await BuildModAsync(); };
            Controls.Add(_buildButton);

            _openOutputButton = new Button();
            _openOutputButton.Text = "Open Built Mod";
            _openOutputButton.Location = new Point(202, 688);
            _openOutputButton.Size = new Size(130, 43);
            _openOutputButton.Enabled = false;
            _openOutputButton.Click += delegate { OpenLastOutput(); };
            Controls.Add(_openOutputButton);

            _activity = new Label();
            _activity.Text = "Starting...";
            _activity.Location = new Point(349, 695);
            _activity.Size = new Size(693, 22);
            _activity.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_activity);

            _progress = new ProgressBar();
            _progress.Location = new Point(349, 718);
            _progress.Size = new Size(693, 13);
            _progress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _progress.Style = ProgressBarStyle.Marquee;
            _progress.MarqueeAnimationSpeed = 0;
            Controls.Add(_progress);

            _log = new RichTextBox();
            _log.Location = new Point(18, 747);
            _log.Size = new Size(1024, 135);
            _log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _log.ReadOnly = true;
            _log.BackColor = Color.FromArgb(25, 27, 29);
            _log.ForeColor = Color.Gainsboro;
            _log.Font = new Font("Consolas", 8.5F);
            Controls.Add(_log);

            if (_guideColor.Items.Count > 0)
                _guideColor.SelectedItem = "Yellow";
            if (_iconMode.Items.Count > 0)
                _iconMode.SelectedIndex = 0;
            TemplateDefinition initialPoster = FindTemplateByKey("Standard Poster");
            if (initialPoster != null)
                _templateBox.SelectedItem = initialPoster;
            else if (_templates.Count > 0)
                _templateBox.SelectedIndex = 0;
            UpdateIconControls();
            UpdateProjectStateUi();

            // Apply the shared folder-style shell without changing the underlying build controls.
            header.Controls.Remove(_newProjectButton);
            Controls.Add(_newProjectButton);
            header.Visible = false;

            ClientSize = new Size(ClientSize.Width, ClientSize.Height + 52);
            TwoPointTheme.ShiftDirectControls(this, header, 0, 38);
            // The log is bottom-anchored; restoring its baseline height avoids the shell
            // offset being applied on top of the automatic anchor resize.
            _log.Height = 135;
            _newProjectButton.Text = "New Mod";
            _newProjectButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            TwoPointTheme.InstallShell(this, "Create Mod",
                delegate { ShowCreatePage(); },
                async delegate { await ShowMyModsAsync(); },
                async delegate { await ShowSettingsAsync(); });
            TwoPointTheme.StyleSectionHeader(setupTitle);
            TwoPointTheme.StylePrimaryButton(_buildButton);

            BuildCreateModPage(setup, form);
            ApplyCreateModToolTips();
            _newProjectButton.Size = new Size(108, 34);
            _newProjectButton.Left = Math.Max(20, ClientSize.Width - _newProjectButton.Width - 32);
            TwoPointTheme.StylePrimaryButton(_newProjectButton);
            InitialiseSingleWindowPages(header);
            // The current Create Mod page uses responsive dock/table layout.

            ApplyRecommendedInitialWindowSize();
            ClientSizeChanged += delegate
            {
                SyncUnifiedTabPageBounds();
                ApplyCreateModResponsiveProfile();
            };
            ApplyCreateModResponsiveProfile();

            Shown += async delegate
            {
                if (!string.IsNullOrWhiteSpace(_settingsService.LoadWarning))
                {
                    TwoPointTheme.ShowMessage(this,
                        _settingsService.LoadWarning + "\n\nRecovery details were written to:\n" + Path.Combine(AppInfo.LogsFolder, "reliability.log"),
                        "Settings Recovery", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                await RefreshEnvironmentAsync(true);

                if (_showFirstRunSetup)
                {
                    using (FirstRunSetupForm welcome = new FirstRunSetupForm(_state, _settings.ModdersName))
                    {
                        welcome.ShowDialog(this);
                        _settings.ModdersName = welcome.ModdersName;
                        _settingsService.Save(_settings);
                        if (welcome.OpenSettingsRequested)
                            await ShowSettingsAsync();
                    }
                }

                StartUnityWorkerInBackground();
            };
            FormClosing += MainForm_FormClosing;
            FormClosed += delegate
            {
                DisposeInteractiveArtworkSource();
                if (_interactivePreviewTimer != null)
                    _interactivePreviewTimer.Dispose();
                if (_previewCommitTimer != null)
                    _previewCommitTimer.Dispose();
                if (_artworkTipTimer != null)
                    _artworkTipTimer.Dispose();
            };
        }

        private List<TemplateDefinition> LoadTemplates()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "templates.json");
            TemplateCatalog catalog = JsonFile.Read<TemplateCatalog>(path);
            if (catalog == null || catalog.Templates == null || catalog.Templates.Count == 0)
                throw new InvalidDataException("Config\\templates.json is missing or contains no templates.");
            return catalog.Templates;
        }

        private async Task RefreshEnvironmentAsync(bool provisionIfMissing)
        {
            SetBusy(true, "Checking Unity, SDK and private environment...");
            try
            {
                _state = DetectPrerequisites();
                UpdatePrerequisiteLabels();

                if (_state.UnityFound && _state.SdkFound)
                {
                    EnvironmentHealthReport health = _state.EnvironmentHealth ?? _environment.Inspect(_state.SdkZipPath);
                    // The private Unity project and Memento Maker automation are application-managed
                    // resources. A missing/damaged private environment can therefore be recreated, and
                    // missing/outdated Memento Maker automation can be repaired automatically.
                    // A changed official SDK is deliberately different: replacing the SDK remains an
                    // explicit Rebuild Environment decision.
                    bool shouldPrepareEnvironment = !health.ProjectValid;
                    bool shouldRepairAutomation = health.ProjectValid && health.NeedsRepair && !health.NeedsRebuild && !health.SdkChanged;

                    if ((shouldPrepareEnvironment || shouldRepairAutomation) && _unityWorker.IsRunning)
                    {
                        AppendLog("Stopping the private Unity worker before environment maintenance...");
                        await _unityWorker.ShutdownAsync(delegate(string message) { SafeAppendLog(message); });
                    }

                    if (shouldPrepareEnvironment)
                    {
                        AppendLog(provisionIfMissing
                            ? "[Startup] Preparing the private modding environment automatically..."
                            : "Preparing the private modding environment...");
                        await Task.Run(delegate
                        {
                            _environment.EnsureEnvironment(_state.SdkZipPath, false, delegate(string message) { SafeAppendLog(message); });
                        });
                        if (provisionIfMissing)
                            AppendLog("[Startup] Private modding environment prepared successfully.");
                        _environmentPreparedThisSession = true;
                    }
                    else if (shouldRepairAutomation)
                    {
                        bool automaticRepair = provisionIfMissing;
                        if (automaticRepair)
                        {
                            AppendLog("[Startup] Memento Maker automation requires repair.");
                            AppendLog("[Startup] Repairing Memento Maker automation automatically...");
                        }
                        else
                        {
                            AppendLog("Private environment repair required. Refreshing Memento Maker automation only...");
                        }

                        try
                        {
                            await Task.Run(delegate
                            {
                                _environment.RepairAutomation(_state.SdkZipPath, delegate(string message) { SafeAppendLog(message); });
                            });
                            if (automaticRepair)
                                AppendLog("[Startup] Memento Maker automation repaired successfully.");
                            _environmentPreparedThisSession = true;
                        }
                        catch (Exception repairEx)
                        {
                            AppendLog((automaticRepair ? "[Startup] Automatic automation repair failed: " : "Automation repair failed: ") + repairEx.Message);
                            if (automaticRepair)
                            {
                                TwoPointTheme.ShowMessage(this,
                                    "Memento Maker found a problem with its private Unity automation and tried to repair it automatically, but the repair could not be completed.\n\n" +
                                    repairEx.Message + "\n\nOpen Settings → Setup Paths & Maintenance and use Check & Repair. If the problem remains, use Rebuild Environment.",
                                    "Automatic Repair Could Not Complete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                            else
                            {
                                throw;
                            }
                        }
                    }
                    else if (!_environmentPreparedThisSession && health.ProjectValid && health.AutomationCurrent)
                    {
                        AppendLog("Private environment health check passed. No repair required.");
                        _environmentPreparedThisSession = true;
                    }

                    _state = DetectPrerequisites();
                    UpdatePrerequisiteLabels();
                }

                if (!_state.UnityFound || !_state.SdkFound)
                {
                    _activity.Text = "Setup requires attention. Open Settings to locate the missing prerequisite.";
                }
                else if (_state.EnvironmentReady)
                {
                    _activity.Text = "Ready to create a mod.";
                    if (_state.EnvironmentHealth != null && _state.EnvironmentHealth.SdkChanged)
                        AppendLog("SDK UPDATE DETECTED: the selected ModdingProject.zip has changed. Rebuild Environment is recommended before building or publishing.");
                }
                else
                {
                    _activity.Text = "Private environment requires attention. Open Settings and use Check & Repair.";
                }
            }
            catch (Exception ex)
            {
                AppendLog("SETUP ERROR: " + ex.Message);
                TwoPointTheme.ShowMessage(this,
                    ex.Message + "\n\nOpen Settings → Setup Paths & Maintenance and use Check & Repair. If the problem remains, use Rebuild Environment.",
                    "Setup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false, _activity.Text);
            }
        }

        private bool IsPersistentWorkerEnabled()
        {
            return _settings == null || _settings.KeepUnityWorkerRunning != false;
        }

        private UnityWorkerService GetPersistentWorker()
        {
            return IsPersistentWorkerEnabled() ? _unityWorker : null;
        }

        private void StartUnityWorkerInBackground()
        {
            if (!IsPersistentWorkerEnabled())
            {
                UpdateEmbeddedUnityWorkerStatus("Persistent worker disabled. One-shot Unity mode is active.");
                return;
            }

            Task ignored = StartUnityWorkerAsync();
        }

        private async Task StartUnityWorkerAsync()
        {
            if (!IsPersistentWorkerEnabled())
                return;

            PrerequisiteState state = _state ?? DetectPrerequisites();
            if (state == null || !state.UnityFound || !state.EnvironmentReady)
            {
                UpdateEmbeddedUnityWorkerStatus("Waiting for Unity and the private environment.");
                return;
            }

            try
            {
                bool firstSetup = _environmentPreparedThisSession;
                SetCreateEnvironmentStatus(true, firstSetup
                    ? "Configuring the modding environment. You can prepare your mod while this finishes..."
                    : "Starting the private Unity worker...");
                UpdateEmbeddedUnityWorkerStatus("Unity is starting in the background...");
                await _unityWorker.StartAsync(
                    state.UnityExePath,
                    state.EnvironmentProjectPath,
                    delegate(string message)
                    {
                        SafeAppendLog("[Worker] " + message);
                        SafeUpdateEmbeddedUnityWorkerStatus(message);
                        SafeSetCreateEnvironmentStatus(true, firstSetup ? "Configuring Unity environment..." : message);
                    },
                    delegate(string line) { SafeAppendLog(line); });
                UpdateEmbeddedUnityWorkerStatus("Running and ready for builds and Workshop actions.");
                SetCreateEnvironmentStatus(false, "Environment ready.");
            }
            catch (Exception ex)
            {
                AppendLog("UNITY WORKER START ERROR: " + ex.Message);
                UpdateEmbeddedUnityWorkerStatus("Could not start. Memento Maker will retry when an action needs Unity.");
                SetCreateEnvironmentStatus(false, "Unity setup paused. It will retry automatically when needed.");
            }
        }

        private async Task ApplyUnityWorkerPreferenceAsync()
        {
            if (!IsPersistentWorkerEnabled())
            {
                if (_unityWorker.IsRunning)
                    await _unityWorker.ShutdownAsync(delegate(string message) { SafeAppendLog(message); });
                UpdateEmbeddedUnityWorkerStatus("Persistent worker disabled. One-shot Unity mode is active.");
                return;
            }

            await StartUnityWorkerAsync();
        }

        private void UpdateEmbeddedUnityWorkerStatus(string message)
        {
            if (_embeddedSettings == null || _embeddedSettings.IsDisposed)
                return;
            _embeddedSettings.UpdateUnityWorkerStatus(
                IsPersistentWorkerEnabled(),
                _unityWorker.IsRunning,
                _unityWorker.IsReady,
                message);
        }

        private void SafeUpdateEmbeddedUnityWorkerStatus(string message)
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
            {
                BeginInvoke((Action<string>)SafeUpdateEmbeddedUnityWorkerStatus, message);
                return;
            }
            UpdateEmbeddedUnityWorkerStatus(message);
        }

        private async void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_allowClose)
                return;
            if (_closingWorker)
            {
                e.Cancel = true;
                return;
            }

            if (!_unityWorker.IsRunning)
            {
                DisposeEmbeddedForms();
                _allowClose = true;
                return;
            }

            e.Cancel = true;
            _closingWorker = true;
            try
            {
                AppendLog("Closing Memento Maker: stopping the private Unity worker...");
                await _unityWorker.ShutdownAsync(delegate(string message) { SafeAppendLog(message); });
            }
            catch (Exception ex)
            {
                AppendLog("UNITY WORKER SHUTDOWN WARNING: " + ex.Message);
            }
            finally
            {
                DisposeEmbeddedForms();
                _allowClose = true;
                _closingWorker = false;
                _unityWorker.Dispose();
                Close();
            }
        }

        private void DisposeEmbeddedForms()
        {
            if (_embeddedMyMods != null && !_embeddedMyMods.IsDisposed)
                _embeddedMyMods.Dispose();
            _embeddedMyMods = null;

            if (_embeddedSettings != null && !_embeddedSettings.IsDisposed)
                _embeddedSettings.Dispose();
            _embeddedSettings = null;
        }

        private async Task RebuildEnvironmentAsync()
        {
            PrerequisiteState state = DetectPrerequisites();
            if (!state.SdkFound)
            {
                TwoPointTheme.ShowMessage(this, "ModdingProject.zip could not be found. Open Settings to locate it.", "SDK Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult confirm = TwoPointTheme.ShowMessage(this,
                "Rebuild the private Unity environment from the selected ModdingProject.zip?\n\nUse this when the SDK has changed or Check & Repair cannot fix the environment. Memento Maker projects and installed game mods are not deleted.\n\nThe private Unity worker will be stopped while the environment is recreated.",
                "Rebuild Environment", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return;

            SetBusy(true, "Rebuilding private modding environment...");
            try
            {
                if (_unityWorker.IsRunning)
                    await _unityWorker.ShutdownAsync(delegate(string message) { SafeAppendLog(message); });

                _environmentPreparedThisSession = false;
                await Task.Run(delegate
                {
                    _environment.EnsureEnvironment(state.SdkZipPath, true, delegate(string message) { SafeAppendLog(message); });
                });
                _environmentPreparedThisSession = true;
                await RefreshEnvironmentAsync(false);
                StartUnityWorkerInBackground();
            }
            catch (Exception ex)
            {
                AppendLog("REBUILD ERROR: " + ex.Message);
                TwoPointTheme.ShowMessage(this, ex.Message, "Rebuild Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false, "Ready.");
            }
        }

        private PrerequisiteState DetectPrerequisites()
        {
            PrerequisiteState state = new PrerequisiteState();
            state.UnityExePath = _discovery.FindUnity(_settings.UnityExePath);
            state.UnityHubPath = _discovery.FindUnityHub();
            state.SdkZipPath = _discovery.FindSdkZip(_settings.SdkZipPath);
            state.EnvironmentProjectPath = _environment.ProjectDirectory;
            state.EnvironmentHealth = _environment.Inspect(state.SdkZipPath);
            state.EnvironmentReady = state.EnvironmentHealth != null && state.EnvironmentHealth.IsUsable && state.EnvironmentHealth.AutomationCurrent;
            state.EnvironmentStatusMessage = state.EnvironmentHealth == null ? "Environment status unavailable." : state.EnvironmentHealth.Message;

            if (!string.IsNullOrEmpty(state.UnityExePath))
                _settings.UnityExePath = state.UnityExePath;
            if (!string.IsNullOrEmpty(state.SdkZipPath))
                _settings.SdkZipPath = state.SdkZipPath;
            _settingsService.Save(_settings);
            return state;
        }

        private void UpdatePrerequisiteLabels()
        {
            if (_state == null)
                return;

            SetStatus(_unityStatus, _state.UnityFound, "Unity 2020.3.47f1", _state.UnityFound ? _state.UnityExePath : "Not found");
            SetStatus(_sdkStatus, _state.SdkFound, "Two Point Museum Modding SDK", _state.SdkFound ? _state.SdkZipPath : "ModdingProject.zip not found");
            SetStatus(_environmentStatus, _state.EnvironmentReady, "Private modding environment", _state.EnvironmentReady ? _state.EnvironmentProjectPath : "Not prepared yet");
            _buildButton.Enabled = _state.UnityFound && _state.SdkFound && _state.EnvironmentReady;

            if (_embeddedSettings != null && !_embeddedSettings.IsDisposed && _embeddedSettings.Parent != null)
                _embeddedSettings.UpdateEnvironmentStatus(_state);
        }

        private bool EnsureModdersNameForBuild()
        {
            string current = _settings == null ? "" : (_settings.ModdersName ?? "");
            if (AssetNamingService.HasUsableModdersName(current))
                return true;

            using (ModdersNamePromptDialog prompt = new ModdersNamePromptDialog(current))
            {
                TwoPointTheme.ApplyApplicationIcon(prompt);
                if (prompt.ShowDialog(this) != DialogResult.OK)
                    return false;

                if (_settings == null)
                    _settings = _settingsService.Load();

                _settings.ModdersName = prompt.ModdersName;
                _settingsService.Save(_settings);

                if (_embeddedSettings != null && !_embeddedSettings.IsDisposed)
                    _embeddedSettings.SetModdersName(_settings.ModdersName);

                AppendLog("Modders Name saved: " + _settings.ModdersName);
                return true;
            }
        }

        private async Task BuildModAsync()
        {
            if (_state == null || !_state.UnityFound || !_state.EnvironmentReady)
            {
                TwoPointTheme.ShowMessage(this,
                    "The private Unity build environment is not ready yet.\n\nWait for the Unity Worker status to show Ready, then try again. If it does not become ready, open Settings → Setup Paths & Maintenance to check or repair the environment.",
                    "Unity Environment Not Ready", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            TemplateDefinition template = _templateBox.SelectedItem as TemplateDefinition;
            if (template == null)
                return;

            DecorPackChoice decorPackChoice = IsWallpaperTemplate(template) ? GetSelectedCreateDecorPackChoice() : null;

            if (_activeProject != null && BuildPackageModes.Normalise(_activeProject.LastBuildPackageMode) == BuildPackageModes.Family)
            {
                DialogResult splitFamily = TwoPointTheme.ShowMessage(this,
                    "Build this item as a standalone mod?\n\nOther members of its combined family will need the family rebuilt.",
                    "Split Family Package", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (splitFamily != DialogResult.Yes)
                    return;
            }
            else if (_activeProject != null && BuildPackageModes.Normalise(_activeProject.LastBuildPackageMode) == BuildPackageModes.DecorPack)
            {
                string currentPackKey = _activeProject.LastBuiltFamilyKey ?? "";
                bool stayingInSamePack = decorPackChoice != null && !decorPackChoice.Standalone && !decorPackChoice.NewPack &&
                    string.Equals(decorPackChoice.PackKey ?? "", currentPackKey, StringComparison.Ordinal);
                if (!stayingInSamePack)
                {
                    bool movingToPack = decorPackChoice != null && !decorPackChoice.Standalone;
                    string message = movingToPack
                        ? "Move this Wallpaper out of its current Décor Pack and build it into the selected pack?\n\nThe remaining wallpapers in the old pack will no longer share the current installed package until rebuilt."
                        : "Build this Wallpaper as a standalone mod?\n\nThe other wallpapers in its Décor Pack will no longer share this installed package.";
                    DialogResult splitPack = TwoPointTheme.ShowMessage(this, message,
                        movingToPack ? "Move Wallpaper Between Décor Packs" : "Split Décor Pack", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (splitPack != DialogResult.Yes)
                        return;
                }
            }

            if (string.IsNullOrWhiteSpace(_modName.Text))
            {
                TwoPointTheme.ShowMessage(this,
                    "Enter a name in Details → Name before creating the mod.\n\nThis name is saved with the Memento Maker project and is used for the built Two Point Museum item.",
                    "Missing Mod Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!EnsureModdersNameForBuild())
                return;

            bool dualArtworkBuild = IsDualArtworkTemplate(template);
            if (dualArtworkBuild)
            {
                CaptureActiveDoubleBannerSideState();
                if (!File.Exists(_doubleBannerLeftArtworkPath) || !File.Exists(_doubleBannerRightArtworkPath))
                {
                    string primaryLabel = IsTwoSidedSignTemplate(template) ? "Front" : "Left Banner";
                    string secondaryLabel = IsTwoSidedSignTemplate(template) ? "Back" : "Right Banner";
                    string itemTypeLabel = IsWallSignTemplate(template) ? "Wall Sign" : (IsHangingSignTemplate(template) ? "Hanging Sign" : "Double Banner");
                    TwoPointTheme.ShowMessage(this,
                        itemTypeLabel + " requires artwork for both the " + primaryLabel + " and " + secondaryLabel + " before it can be built.\n\nUse the " + primaryLabel + " and " + secondaryLabel + " artwork panes to choose an image for each side.",
                        "Both Artwork Images Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            else if (!File.Exists(_imagePath.Text))
            {
                TwoPointTheme.ShowMessage(this,
                    "Choose an artwork/image before creating the mod.\n\nUse the Artwork / Image panel to browse for a PNG or JPG, or drag an image into the preview area.",
                    "Missing Artwork", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.Equals(GetIconMode(), "Custom Icon File", StringComparison.OrdinalIgnoreCase) && !File.Exists(_iconPath.Text))
            {
                TwoPointTheme.ShowMessage(this,
                    "The selected custom icon file could not be found.\n\nChoose another icon file, or change the Icon Source before building the mod.",
                    "Missing Icon", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            VariantParentChoice variantChoice = GetSelectedCreateVariantChoice();
            string variantMode = variantChoice == null ? VariantModes.Standalone : VariantModes.Normalise(variantChoice.Mode);
            string variantParentModId = variantChoice == null ? "" : (variantChoice.ModId ?? "");
            try
            {
                _projectLibrary.ValidateVariantState(_activeProject, variantMode, variantParentModId);
            }
            catch (Exception variantError)
            {
                TwoPointTheme.ShowMessage(this, variantError.Message, "Variant Parent Not Available",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                RefreshCreateVariantChoices();
                return;
            }

            if (string.IsNullOrWhiteSpace(_outputPath.Text))
            {
                TwoPointTheme.ShowMessage(this,
                    "Choose a valid output folder before creating the mod.\n\nMemento Maker needs this location to prepare and save the build output.",
                    "Missing Output Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Directory.CreateDirectory(_outputPath.Text);
            _settings.LastOutputFolder = _outputPath.Text;
            _settingsService.Save(_settings);

            if (IsWallpaperTemplate(template) && decorPackChoice != null && !decorPackChoice.Standalone)
            {
                await BuildCurrentWallpaperDecorPackAsync(template, decorPackChoice);
                return;
            }

            string originalArtwork = dualArtworkBuild ? Path.GetFullPath(_doubleBannerLeftArtworkPath) : Path.GetFullPath(_imagePath.Text);
            string secondaryArtwork = dualArtworkBuild ? Path.GetFullPath(_doubleBannerRightArtworkPath) : "";
            string fitMode = dualArtworkBuild ? _doubleBannerLeftFitMode : GetFitMode(template);
            ImagePlacementState primaryPlacement = dualArtworkBuild ? ClonePlacement(_doubleBannerLeftPlacement) : ClonePlacement(_placement);
            string secondaryFitMode = dualArtworkBuild ? _doubleBannerRightFitMode : "";
            ImagePlacementState secondaryPlacement = dualArtworkBuild ? ClonePlacement(_doubleBannerRightPlacement) : null;
            string preparedArtwork = null;
            string preparedIcon = null;

            _openOutputButton.Enabled = false;
            _lastBuiltOutput = null;
            AppendLog("------------------------------------------------------------");
            AppendLog("Building " + template.DisplayName + ": " + _modName.Text.Trim());
            if (template.ImageProcessingEnabled)
                AppendLog("Preparing " + template.TextureWidth + " x " + template.TextureHeight + " texture using " + fitMode + " mode...");
            SetBusy(true, "Preparing artwork...");

            try
            {
                if (dualArtworkBuild)
                {
                    AppendLog(IsWallSignTemplate(template)
                        ? "Preparing Front + Back artwork into one 1024 x 1024 Wall Sign texture for the Sign material..."
                        : IsHangingSignTemplate(template)
                            ? "Preparing Front + Back artwork into one 1024 x 1024 Hanging Sign texture for the Sign material..."
                            : "Preparing Left + Right artwork into one 1024 x 1024 Double Banner texture...");
                    preparedArtwork = _imageProcessor.PrepareDoubleBannerArtwork(
                        originalArtwork, secondaryArtwork, template,
                        fitMode, primaryPlacement, secondaryFitMode, secondaryPlacement);
                    AppendLog((IsWallSignTemplate(template) ? "Preparing Wall Sign icon using " : (IsHangingSignTemplate(template) ? "Preparing Hanging Sign icon using " : "Preparing Double Banner icon using ")) + GetIconMode() + " mode...");
                    preparedIcon = _imageProcessor.PrepareDoubleBannerIcon(
                        originalArtwork, secondaryArtwork, preparedArtwork, _iconPath.Text.Trim(), template,
                        fitMode, primaryPlacement, secondaryFitMode, secondaryPlacement, GetIconMode());
                }
                else
                {
                    preparedArtwork = _imageProcessor.PrepareArtwork(originalArtwork, template, fitMode, primaryPlacement);
                    AppendLog("Preparing icon using " + GetIconMode() + " mode...");
                    preparedIcon = _imageProcessor.PrepareIcon(originalArtwork, preparedArtwork, _iconPath.Text.Trim(), template, fitMode, primaryPlacement, GetIconMode());
                }

                BuildJob job = new BuildJob();
                job.Template = template.Key;
                job.ModName = _modName.Text.Trim();
                job.ModdersName = _settings == null ? "" : (_settings.ModdersName ?? "");
                job.Description = _description.Text.Trim();
                job.ImagePath = preparedArtwork;
                job.OutputPath = Path.GetFullPath(_outputPath.Text);
                job.ItemCost = Decimal.ToInt32(_cost.Value);
                job.KudoshCost = Decimal.ToInt32(_kudosh.Value);
                job.ItemModId = _activeProject == null ? "" : (_activeProject.ModId ?? "");
                job.VariantMode = variantMode;
                job.VariantParentModId = variantParentModId;
                job.VariantParentBaseArchetypeId = _activeProject == null ? "" : (_activeProject.VariantParentBaseArchetypeId ?? "");
                job.ItemCustomisationId = _activeProject == null ? "" : (_activeProject.ItemCustomisationId ?? "");

                job.IconPath = preparedIcon;
                job.UseItemImageAsIcon = false;
                job.KeepGeneratedAssets = false;

                BuildResultFile result = await _buildService.BuildAsync(
                    _state.UnityExePath,
                    _environment.ProjectDirectory,
                    job,
                    delegate(string message) { SafeSetActivity(message); },
                    delegate(string line) { SafeAppendLog(line); },
                    GetPersistentWorker());

                string previousOutput = _activeProject == null ? "" : (_activeProject.LastBuiltOutputPath ?? "");
                string previousOutputRoot = _activeProject == null ? "" : (_activeProject.OutputRoot ?? "");
                string previousPackageMode = _activeProject == null ? BuildPackageModes.Single : BuildPackageModes.Normalise(_activeProject.LastBuildPackageMode);
                string previousFamilyKey = _activeProject == null ? "" : (_activeProject.LastBuiltFamilyKey ?? "");

                _activeProject = _projectLibrary.SaveAfterBuild(
                    _activeProject,
                    result,
                    originalArtwork,
                    _iconPath.Text.Trim(),
                    template.Key,
                    _modName.Text.Trim(),
                    _description.Text.Trim(),
                    Decimal.ToInt32(_cost.Value),
                    Decimal.ToInt32(_kudosh.Value),
                    fitMode,
                    primaryPlacement,
                    secondaryArtwork,
                    secondaryFitMode,
                    secondaryPlacement,
                    GetIconMode(),
                    Path.GetFullPath(_outputPath.Text),
                    variantMode,
                    variantParentModId);

                _myModsNeedsRefresh = true;
                if (previousPackageMode == BuildPackageModes.Family && !string.IsNullOrEmpty(previousFamilyKey))
                    _projectLibrary.DetachFamilyPackage(previousFamilyKey, new string[] { _activeProject.ModId }, previousOutput);
                else if (previousPackageMode == BuildPackageModes.DecorPack && !string.IsNullOrEmpty(previousFamilyKey))
                    _projectLibrary.DetachDecorPack(previousFamilyKey, new string[] { _activeProject.ModId }, previousOutput);
                RemovePreviousInstalledBuild(previousOutput, previousOutputRoot, result.OutputPath);

                _imagePath.Text = _projectLibrary.GetArtworkPath(_activeProject);
                if (dualArtworkBuild)
                {
                    _doubleBannerLeftArtworkPath = _projectLibrary.GetArtworkPath(_activeProject);
                    _doubleBannerRightArtworkPath = _projectLibrary.GetSecondaryArtworkPath(_activeProject);
                }
                if (string.Equals(GetIconMode(), "Custom Icon File", StringComparison.OrdinalIgnoreCase))
                    _iconPath.Text = _projectLibrary.GetCustomIconPath(_activeProject);

                string completedOutput = result.OutputPath;
                string completedName = result.ModName;
                _activity.Text = "Success: " + completedName;
                TwoPointTheme.ShowMessage(this,
                    "'" + completedName + "' was built and installed.\n\nSaved to My Mods and ready to use.",
                    "Build Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // A completed build now starts a follow-on NEW project without throwing away
                // the user's composition. Keep the current item type/template, artwork source(s),
                // fitting, guide, zoom, pan and rotation so a closely related mod can be created
                // immediately. The saved project is detached so the next build receives a new ID.
                StartFollowOnProjectAfterBuild();

                // Keep the latest successful output available while composing the follow-on mod.
                _lastBuiltOutput = completedOutput;
                _openOutputButton.Enabled = !string.IsNullOrEmpty(_lastBuiltOutput) && Directory.Exists(_lastBuiltOutput);
                _activity.Text = "Success: " + completedName + " - composition kept for a new mod.";
            }
            catch (Exception ex)
            {
                _activity.Text = "Build failed.";
                AppendLog("BUILD FAILED: " + ex.Message);
                TwoPointTheme.ShowMessage(this, ex.Message, "Build Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (!string.IsNullOrEmpty(preparedIcon))
                    ImageProcessingService.DeletePreparedFile(preparedIcon);
                if (!string.IsNullOrEmpty(preparedArtwork) && !string.Equals(preparedArtwork, originalArtwork, StringComparison.OrdinalIgnoreCase))
                    ImageProcessingService.DeletePreparedFile(preparedArtwork);
                SetBusy(false, _activity.Text);
            }
        }

        private void ResetArtworkTipCycle(TemplateDefinition template)
        {
            if (_imageHint == null)
                return;

            if (template == null || !template.ImageProcessingEnabled)
            {
                if (_artworkTipTimer != null)
                    _artworkTipTimer.Stop();
                _artworkTipIndex = 0;
                _imageHint.Text = template == null ? "" : (template.ImageHint ?? "Choose artwork for this item.");
                return;
            }

            _artworkTipIndex = 0;
            ShowCurrentArtworkTip();
            ResumeArtworkTipCycle();
        }

        private void AdvanceArtworkTip()
        {
            TemplateDefinition template = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            if (_artworkTipPaused || template == null || !template.ImageProcessingEnabled || ArtworkTips.Length == 0)
                return;

            int tipCount = ArtworkTips.Length + (IsWallpaperTemplate(template) ? 1 : 0);
            _artworkTipIndex = (_artworkTipIndex + 1) % tipCount;
            ShowCurrentArtworkTip();
        }

        private void ShowCurrentArtworkTip()
        {
            if (_imageHint == null || ArtworkTips.Length == 0)
                return;

            TemplateDefinition template = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            bool wallpaper = IsWallpaperTemplate(template);
            int tipCount = ArtworkTips.Length + (wallpaper ? 1 : 0);
            if (_artworkTipIndex < 0 || _artworkTipIndex >= tipCount)
                _artworkTipIndex = 0;

            if (wallpaper && _artworkTipIndex == 0)
            {
                _imageHint.Text = "Tip: Wallpaper texture area is 1024 x 1024.";
                return;
            }

            int generalIndex = wallpaper ? _artworkTipIndex - 1 : _artworkTipIndex;
            _imageHint.Text = ArtworkTips[generalIndex];
        }

        private void ResumeArtworkTipCycle()
        {
            if (_artworkTipTimer == null || _artworkTipPaused)
                return;

            TemplateDefinition template = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            bool createPageVisible = _createVisualRoot == null || _createVisualRoot.Visible;
            if (template != null && template.ImageProcessingEnabled && createPageVisible)
            {
                if (!_artworkTipTimer.Enabled)
                    _artworkTipTimer.Start();
            }
            else
            {
                _artworkTipTimer.Stop();
            }
        }

        private void TemplateChanged()
        {
            TemplateDefinition template = _templateBox.SelectedItem as TemplateDefinition;
            if (template == null)
                return;

            bool preservePosterEditorState = _preservePosterEditorStateOnTemplateChange && IsPosterTemplate(template);
            bool preserveRugEditorState = _preserveRugEditorStateOnTemplateChange && IsRugTemplate(template);
            bool preserveBannerEditorState = _preserveBannerEditorStateOnTemplateChange && IsSingleBannerTemplate(template);
            bool preserveDoubleBannerEditorState = _preserveDoubleBannerEditorStateOnTemplateChange && IsDualArtworkTemplate(template);
            bool preserveEditorState = preservePosterEditorState || preserveRugEditorState || preserveBannerEditorState || preserveDoubleBannerEditorState;
            VariantParentChoice preservedVariantChoice = preserveEditorState ? GetSelectedCreateVariantChoice() : null;
            if (!preserveEditorState)
            {
                _cost.Value = Math.Max(_cost.Minimum, Math.Min(_cost.Maximum, template.DefaultCost));
                _kudosh.Value = Math.Max(_kudosh.Minimum, Math.Min(_kudosh.Maximum, template.DefaultKudosh));
            }
            ResetArtworkTipCycle(template);

            bool wallpaperTemplate = IsWallpaperTemplate(template);
            _fitMode.Enabled = template.ImageProcessingEnabled;
            _guideColor.Enabled = template.ImageProcessingEnabled;
            SetGuideColorSwatchesEnabled(template.ImageProcessingEnabled);
            _resetViewButton.Enabled = template.ImageProcessingEnabled;
            if (_artworkBrowseButton != null)
                _artworkBrowseButton.Enabled = template.ImageProcessingEnabled || wallpaperTemplate;
            if (template.ImageProcessingEnabled)
            {
                if (!preserveEditorState)
                {
                    string wanted = string.IsNullOrEmpty(template.DefaultFitMode) ? "Fill" : template.DefaultFitMode;
                    int index = _fitMode.Items.IndexOf(wanted);
                    _fitMode.SelectedIndex = index >= 0 ? index : 0;
                }
            }
            else
            {
                _fitMode.SelectedIndex = -1;
                _fitMode.Text = "";
            }

            if (!preserveEditorState)
            {
                SetDefaultIconMode(template);
                ResetPlacement(false);
                if (IsDualArtworkTemplate(template))
                    InitialiseDoubleBannerStateFromCurrent(template);
            }
            UpdateArtworkPreview();
            SyncCreateModFromTemplate(template);
            RefreshCreateVariantChoices();
            RefreshCreateDecorPackChoices();
            if (preservedVariantChoice != null)
                RestoreCreateVariantChoice(preservedVariantChoice.Mode, preservedVariantChoice.ModId);
        }

        private static ImagePlacementState ClonePlacement(ImagePlacementState source)
        {
            ImagePlacementState clone = new ImagePlacementState();
            if (source == null) return clone;
            clone.Zoom = source.Zoom <= 0.0f ? 1.0f : source.Zoom;
            clone.OffsetX = source.OffsetX;
            clone.OffsetY = source.OffsetY;
            clone.RotationDegrees = source.RotationDegrees;
            clone.GuideColorName = NormaliseGuideColorName(source.GuideColorName);
            clone.ReferenceWidth = source.ReferenceWidth;
            clone.ReferenceHeight = source.ReferenceHeight;
            return clone;
        }

        private void InitialiseDoubleBannerStateFromCurrent(TemplateDefinition template)
        {
            string defaultFit = template == null || string.IsNullOrEmpty(template.DefaultFitMode) ? "Fill" : template.DefaultFitMode;
            _doubleBannerEditingRight = false;
            SetDoubleBannerDisplaySideState(false,
                _imagePath == null ? "" : (_imagePath.Text ?? ""),
                GetFitMode(template),
                ClonePlacement(_placement));
            ImagePlacementState opposite = new ImagePlacementState();
            opposite.GuideColorName = NormaliseGuideColorName(_placement == null ? "Yellow" : _placement.GuideColorName);
            SetDoubleBannerDisplaySideState(true, "", defaultFit, opposite);
            StyleDoubleBannerActivePane();
        }

        // Double Banner textures intentionally keep their established atlas order:
        // primary artwork = texture left half, secondary artwork = texture right half.
        // In-game, those appear on opposite physical banners, so the Create Mod and
        // My Mods editors present a swapped display mapping: the visible Left editor
        // controls the secondary/stored-right artwork and the visible Right editor
        // controls the primary/stored-left artwork.
        private bool UsesSwappedDualArtworkDisplay(TemplateDefinition template)
        {
            return IsDoubleBannerTemplate(template);
        }

        private string GetDoubleBannerDisplayArtworkPath(bool rightDisplaySide)
        {
            TemplateDefinition template = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            if (UsesSwappedDualArtworkDisplay(template))
                return rightDisplaySide ? _doubleBannerLeftArtworkPath : _doubleBannerRightArtworkPath;
            return rightDisplaySide ? _doubleBannerRightArtworkPath : _doubleBannerLeftArtworkPath;
        }

        private string GetDoubleBannerDisplayFitMode(bool rightDisplaySide)
        {
            TemplateDefinition template = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            if (UsesSwappedDualArtworkDisplay(template))
                return rightDisplaySide ? _doubleBannerLeftFitMode : _doubleBannerRightFitMode;
            return rightDisplaySide ? _doubleBannerRightFitMode : _doubleBannerLeftFitMode;
        }

        private ImagePlacementState GetDoubleBannerDisplayPlacement(bool rightDisplaySide)
        {
            TemplateDefinition template = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            if (UsesSwappedDualArtworkDisplay(template))
                return rightDisplaySide ? _doubleBannerLeftPlacement : _doubleBannerRightPlacement;
            return rightDisplaySide ? _doubleBannerRightPlacement : _doubleBannerLeftPlacement;
        }

        private void SetDoubleBannerDisplaySideState(bool rightDisplaySide, string path, string fitMode, ImagePlacementState placement)
        {
            TemplateDefinition template = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            bool swapped = UsesSwappedDualArtworkDisplay(template);
            bool storeToLeftSlot = swapped ? rightDisplaySide : !rightDisplaySide;

            if (storeToLeftSlot)
            {
                _doubleBannerLeftArtworkPath = path ?? "";
                _doubleBannerLeftFitMode = string.IsNullOrEmpty(fitMode) ? "Fill" : fitMode;
                _doubleBannerLeftPlacement = ClonePlacement(placement);
            }
            else
            {
                _doubleBannerRightArtworkPath = path ?? "";
                _doubleBannerRightFitMode = string.IsNullOrEmpty(fitMode) ? "Fill" : fitMode;
                _doubleBannerRightPlacement = ClonePlacement(placement);
            }
        }

        private void CaptureActiveDoubleBannerSideState()
        {
            TemplateDefinition template = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            if (!IsDualArtworkTemplate(template) || _switchingDoubleBannerSide)
                return;

            string path = _imagePath == null ? "" : (_imagePath.Text ?? "");
            string fit = GetFitMode(template);
            SetDoubleBannerDisplaySideState(_doubleBannerEditingRight, path, fit, _placement);
        }

        private void ActivateDoubleBannerSide(bool editRight, bool refreshPreview)
        {
            TemplateDefinition template = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            if (!IsDualArtworkTemplate(template))
                return;

            if (!_switchingDoubleBannerSide && _doubleBannerEditingRight != editRight)
                CaptureActiveDoubleBannerSideState();

            _doubleBannerEditingRight = editRight;
            _switchingDoubleBannerSide = true;
            try
            {
                string path = GetDoubleBannerDisplayArtworkPath(editRight);
                string fit = GetDoubleBannerDisplayFitMode(editRight);
                ImagePlacementState placement = GetDoubleBannerDisplayPlacement(editRight);
                _placement = ClonePlacement(placement);
                _imagePath.Text = path ?? "";
                int fitIndex = _fitMode.Items.IndexOf(string.IsNullOrEmpty(fit) ? (template.DefaultFitMode ?? "Fill") : fit);
                _fitMode.SelectedIndex = fitIndex >= 0 ? fitIndex : 0;
                int guideIndex = _guideColor.Items.IndexOf(NormaliseGuideColorName(_placement.GuideColorName));
                _guideColor.SelectedIndex = guideIndex >= 0 ? guideIndex : _guideColor.Items.IndexOf("Yellow");
                SyncFitButtons();
                SyncGuideColorSwatches();
                DisposeInteractiveArtworkSource();
                StyleDoubleBannerActivePane();
            }
            finally
            {
                _switchingDoubleBannerSide = false;
            }

            if (refreshPreview)
                UpdateArtworkPreview();
            else
                UpdateArtworkPreviewText(template);
        }

        private void ConfigureDoubleBannerArtworkLayout(bool visible)
        {
            TemplateDefinition template = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            bool twoSidedSign = visible && IsTwoSidedSignTemplate(template);
            string primaryLabel = twoSidedSign ? "Front" : "Left Banner";
            string secondaryLabel = twoSidedSign ? "Back" : "Right Banner";

            if (_doubleBannerLeftTitleLabel != null)
                _doubleBannerLeftTitleLabel.Text = primaryLabel;
            if (_doubleBannerRightTitleLabel != null)
                _doubleBannerRightTitleLabel.Text = secondaryLabel;

            if (_artworkDropHint != null)
                _artworkDropHint.Text = visible
                    ? "Drop " + primaryLabel + " artwork here\nor click to browse"
                    : "Drop artwork image here\nor click to browse";
            if (_doubleBannerRightDropHint != null)
                _doubleBannerRightDropHint.Text = "Drop " + secondaryLabel + " artwork here\nor click to browse";

            if (_doubleBannerPreviewGrid != null && _doubleBannerLeftPane != null && _doubleBannerRightPane != null)
            {
                _doubleBannerRightPane.Visible = visible;
                _doubleBannerPreviewGrid.SetColumnSpan(_doubleBannerLeftPane, visible ? 1 : 2);
                _doubleBannerLeftPane.Margin = visible ? new Padding(0, 0, 4, 0) : new Padding(0);
                if (_doubleBannerLeftPane.RowStyles.Count > 0)
                    _doubleBannerLeftPane.RowStyles[0].Height = visible ? 36F : 0F;
                if (_doubleBannerRightPane.RowStyles.Count > 0)
                    _doubleBannerRightPane.RowStyles[0].Height = 36F;
            }

            if (_resetViewButton != null) _resetViewButton.Visible = !visible;
            if (_artworkBrowseButton != null) _artworkBrowseButton.Visible = !visible;
            if (_doubleBannerLeftBrowseButton != null)
            {
                _doubleBannerLeftBrowseButton.Visible = visible;
                _doubleBannerLeftBrowseButton.Enabled = visible;
            }
            if (_doubleBannerRightBrowseButton != null)
            {
                _doubleBannerRightBrowseButton.Visible = visible;
                _doubleBannerRightBrowseButton.Enabled = visible;
            }
            if (_doubleBannerLeftResetButton != null)
            {
                _doubleBannerLeftResetButton.Visible = visible;
                _doubleBannerLeftResetButton.Enabled = visible;
            }
            if (_doubleBannerRightResetButton != null)
            {
                _doubleBannerRightResetButton.Visible = visible;
                _doubleBannerRightResetButton.Enabled = visible;
            }

            if (visible)
                StyleDoubleBannerActivePane();
        }

        private void StyleDoubleBannerActivePane()
        {
            // Use the frame colour as the lightweight active-side indicator. Clicking or
            // interacting with either preview makes its shared Fitting/Guide controls active.
            if (_doubleBannerLeftPreviewFrame != null)
                _doubleBannerLeftPreviewFrame.BackColor = !_doubleBannerEditingRight ? TwoPointTheme.SectionBlue : TwoPointTheme.FieldBackground;
            if (_doubleBannerRightPreviewFrame != null)
                _doubleBannerRightPreviewFrame.BackColor = _doubleBannerEditingRight ? TwoPointTheme.SectionBlue : TwoPointTheme.FieldBackground;
        }

        private string GetFitMode(TemplateDefinition template)
        {
            if (template == null || !template.ImageProcessingEnabled)
                return "Original";
            return _fitMode.SelectedItem == null ? (template.DefaultFitMode ?? "Fill") : _fitMode.SelectedItem.ToString();
        }

        private string GetIconMode()
        {
            string display = _iconMode.SelectedItem == null ? "Generated" : _iconMode.SelectedItem.ToString();
            if (string.Equals(display, "Generated", StringComparison.OrdinalIgnoreCase))
                return "Template Styled";
            if (string.Equals(display, "Custom", StringComparison.OrdinalIgnoreCase))
                return "Custom Icon File";
            return "Original Artwork";
        }

        private string GetIconDisplayMode(string canonical)
        {
            if (string.Equals(canonical, "Custom Icon File", StringComparison.OrdinalIgnoreCase))
                return "Custom";
            if (string.Equals(canonical, "Template Styled", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(canonical, "Final Texture", StringComparison.OrdinalIgnoreCase))
                return "Generated";
            return "Original Artwork";
        }

        private void SetDefaultIconMode(TemplateDefinition template)
        {
            string wanted = "Original Artwork";
            if (template != null)
            {
                if (IsWallpaperTemplate(template) ||
                    IsPosterTemplate(template) ||
                    string.Equals(template.Key, "Mural", StringComparison.OrdinalIgnoreCase) ||
                    template.Key.IndexOf("Rug", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    IsBannerTemplate(template))
                {
                    wanted = "Generated";
                }
            }

            int index = _iconMode.Items.IndexOf(wanted);
            _iconMode.SelectedIndex = index >= 0 ? index : 0;
        }

        private void UpdateArtworkPreview()
        {
            TemplateDefinition template = _templateBox.SelectedItem as TemplateDefinition;
            if (template == null)
                return;

            if (IsDualArtworkTemplate(template))
            {
                if (!_switchingDoubleBannerSide)
                    CaptureActiveDoubleBannerSideState();
                UpdateDoubleBannerArtworkPreviews(template);
                UpdateArtworkPreviewText(template);
                UpdateIconControls();
                return;
            }

            bool hasArtwork = File.Exists(_imagePath.Text);
            if (_artworkDropHint != null)
                _artworkDropHint.Visible = !hasArtwork;

            if (!hasArtwork)
            {
                if (IsWallpaperTemplate(template))
                {
                    Bitmap guidePreview = _imageProcessor.CreateWallpaperGuidePreview(
                        Math.Max(1, _imagePreview.Width - 8), Math.Max(1, _imagePreview.Height - 8), ResolveGuideColor());
                    SwapPreviewImage(_imagePreview, guidePreview);
                }
                else
                {
                    ClearPreview(_imagePreview);
                }
                DisposeInteractiveArtworkSource();
            }
            else
            {
                try
                {
                    Bitmap preview = _imageProcessor.CreatePreview(_imagePath.Text, template, GetFitMode(template), _placement, _imagePreview.Width - 8, _imagePreview.Height - 8, ResolveGuideColor());
                    SwapPreviewImage(_imagePreview, preview);
                }
                catch (Exception ex)
                {
                    _mappingInfo.Text = "Preview error: " + ex.Message;
                    return;
                }
            }

            UpdateArtworkPreviewText(template);
            UpdateIconControls();
        }

        private void UpdateDoubleBannerArtworkPreviews(TemplateDefinition template)
        {
            if (!IsDualArtworkTemplate(template))
                return;

            RenderDoubleBannerSidePreview(false, template);
            RenderDoubleBannerSidePreview(true, template);
            StyleDoubleBannerActivePane();
        }

        private void RenderDoubleBannerSidePreview(bool right, TemplateDefinition template)
        {
            PictureBox previewBox = right ? _doubleBannerRightPreview : _imagePreview;
            Label dropHint = right ? _doubleBannerRightDropHint : _artworkDropHint;
            string path = GetDoubleBannerDisplayArtworkPath(right);
            string fit = GetDoubleBannerDisplayFitMode(right);
            ImagePlacementState placement = GetDoubleBannerDisplayPlacement(right);
            bool hasArtwork = !string.IsNullOrWhiteSpace(path) && File.Exists(path);

            if (dropHint != null)
                dropHint.Visible = !hasArtwork;
            if (previewBox == null)
                return;

            if (!hasArtwork)
            {
                ClearPreview(previewBox);
                return;
            }

            try
            {
                Bitmap preview = _imageProcessor.CreatePreview(
                    path, template, fit, placement,
                    Math.Max(1, previewBox.Width - 8), Math.Max(1, previewBox.Height - 8),
                    ResolveGuideColor(placement == null ? "Yellow" : placement.GuideColorName));
                SwapPreviewImage(previewBox, preview);
            }
            catch (Exception ex)
            {
                ClearPreview(previewBox);
                _mappingInfo.Text = (right ? "Right" : "Left") + " preview error: " + ex.Message;
            }
        }

        private void UpdateArtworkPreviewInteractive()
        {
            TemplateDefinition template = _templateBox.SelectedItem as TemplateDefinition;
            if (template == null || !template.ImageProcessingEnabled || !File.Exists(_imagePath.Text))
                return;

            try
            {
                Bitmap source = GetInteractiveArtworkSource();
                if (source == null)
                    return;

                PictureBox activePreview = GetActiveArtworkPreviewBox();
                Bitmap preview = _imageProcessor.CreateInteractivePreview(
                    source,
                    template,
                    GetFitMode(template),
                    _placement,
                    Math.Max(1, activePreview.Width - 8),
                    Math.Max(1, activePreview.Height - 8),
                    ResolveGuideColor());

                SwapPreviewImage(activePreview, preview);
                UpdateArtworkPreviewText(template);
            }
            catch
            {
                // Interactive rendering is deliberately best-effort. If a source image is
                // being replaced at the same instant, the normal committed refresh will
                // rebuild the preview once the interaction ends.
            }
        }

        private void QueueInteractiveArtworkPreview()
        {
            _interactivePreviewPending = true;
            if (!_interactivePreviewTimer.Enabled)
                _interactivePreviewTimer.Start();
        }

        private Bitmap GetInteractiveArtworkSource()
        {
            string path = _imagePath == null ? null : _imagePath.Text;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                DisposeInteractiveArtworkSource();
                return null;
            }

            FileInfo info = new FileInfo(path);
            string fullPath = Path.GetFullPath(path);
            bool cacheValid = _interactiveArtworkSource != null &&
                string.Equals(_interactiveArtworkSourcePath, fullPath, StringComparison.OrdinalIgnoreCase) &&
                _interactiveArtworkSourceWriteUtc == info.LastWriteTimeUtc &&
                _interactiveArtworkSourceLength == info.Length;

            if (cacheValid)
                return _interactiveArtworkSource;

            DisposeInteractiveArtworkSource();
            byte[] bytes = File.ReadAllBytes(fullPath);
            using (MemoryStream stream = new MemoryStream(bytes))
            using (Image loaded = Image.FromStream(stream))
                _interactiveArtworkSource = new Bitmap(loaded);

            _interactiveArtworkSourcePath = fullPath;
            _interactiveArtworkSourceWriteUtc = info.LastWriteTimeUtc;
            _interactiveArtworkSourceLength = info.Length;
            return _interactiveArtworkSource;
        }

        private void DisposeInteractiveArtworkSource()
        {
            if (_interactiveArtworkSource != null)
            {
                _interactiveArtworkSource.Dispose();
                _interactiveArtworkSource = null;
            }
            _interactiveArtworkSourcePath = null;
            _interactiveArtworkSourceWriteUtc = DateTime.MinValue;
            _interactiveArtworkSourceLength = 0;
        }

        private static void SwapPreviewImage(PictureBox box, Image next)
        {
            if (box == null)
            {
                if (next != null)
                    next.Dispose();
                return;
            }

            Image old = box.Image;
            box.Image = next;
            box.Invalidate();
            if (old != null && !object.ReferenceEquals(old, next))
                old.Dispose();
        }

        private void UpdateArtworkPreviewText(TemplateDefinition template)
        {
            if (template.ImageProcessingEnabled)
            {
                if (IsWallpaperTemplate(template))
                {
                    _previewTitle.Text = "Wallpaper texture preview";
                    _mappingInfo.Text = "Guide/output: 1024 x 1024. The edited square texture repeats across room walls in-game." + Environment.NewLine +
                        "Zoom: " + _placement.Zoom.ToString("0.00") + "x  •  Rotation: " + FormatRotationDegrees(_placement.RotationDegrees) + Environment.NewLine +
                        "Drag to pan. Mouse wheel zooms; Ctrl + wheel rotates.";
                }
                else if (IsDualArtworkTemplate(template))
                {
                    _previewTitle.Text = IsWallSignTemplate(template) ? "Wall Sign artwork editors" : (IsHangingSignTemplate(template) ? "Hanging Sign artwork editors" : "Double Banner artwork editors");
                    string currentPane = IsTwoSidedSignTemplate(template) ? (_doubleBannerEditingRight ? "Back" : "Front") : (_doubleBannerEditingRight ? "Right" : "Left");
                    string dualSummary = IsTwoSidedSignTemplate(template)
                        ? "Front and Back artwork are shown side-by-side. Shared Fitting/Guide controls apply to the blue-highlighted pane (currently " + currentPane + ")."
                        : "Left and Right artwork are shown side-by-side. Shared Fitting/Guide controls apply to the blue-highlighted pane (currently " + currentPane + ").";
                    string finalSummary = IsWallSignTemplate(template)
                        ? "Final output: one 1024 x 1024 Wall Sign texture for the Sign material."
                        : IsHangingSignTemplate(template)
                            ? "Final output: one 1024 x 1024 Hanging Sign texture for the Sign material."
                            : "Final output: one 1024 x 1024 Double Banner texture.";
                    _mappingInfo.Text = dualSummary + Environment.NewLine +
                        finalSummary + Environment.NewLine +
                        "Zoom: " + _placement.Zoom.ToString("0.00") + "x  •  Rotation: " + FormatRotationDegrees(_placement.RotationDegrees) + Environment.NewLine +
                        template.MappingNote;
                }
                else if (IsRugTemplate(template))
                {
                    _previewTitle.Text = "Rug shape editor";
                    _mappingInfo.Text = "Artwork is shown normally on the full rug shape." + Environment.NewLine +
                        "Zoom: " + _placement.Zoom.ToString("0.00") + "x  •  Rotation: " + FormatRotationDegrees(_placement.RotationDegrees) + Environment.NewLine +
                        "All rugs use replacement meshes. Unity bakes this clean design through the selected mesh UVs and applies the generated Two Point/Lit material to every LOD.";
                }
                else
                {
                    _previewTitle.Text = "Final texture preview";
                    _mappingInfo.Text = "Texture: " + template.TextureWidth + " x " + template.TextureHeight + Environment.NewLine +
                        "Visible: " + template.VisibleWidth + " x " + template.VisibleHeight + " at " + template.VisibleX + "," + template.VisibleY + Environment.NewLine +
                        "Zoom: " + _placement.Zoom.ToString("0.00") + "x  •  Rotation: " + FormatRotationDegrees(_placement.RotationDegrees) + Environment.NewLine +
                        template.MappingNote;
                }
            }
            else
            {
                _previewTitle.Text = "Final texture preview";
                _mappingInfo.Text = template.MappingNote;
            }
        }

        private void UpdateIconControls()
        {
            bool customEnabled = string.Equals(GetIconMode(), "Custom Icon File", StringComparison.OrdinalIgnoreCase);
            _iconPath.Enabled = customEnabled;
            _iconBrowse.Enabled = customEnabled;
            // Visibility must always reflect the selected icon source, including during
            // initial Create Mod layout construction. Previously this was gated on
            // _createVisualRoot, so the path field and browse button stayed visible on
            // first launch until another UI event refreshed the icon controls.
            _iconPath.Visible = customEnabled;
            _iconBrowse.Visible = customEnabled;
            _iconPreview.Enabled = true;

            ClearPreview(_iconPreview);
            TemplateDefinition template = _templateBox.SelectedItem as TemplateDefinition;
            if (!customEnabled)
            {
                if (IsDualArtworkTemplate(template))
                {
                    CaptureActiveDoubleBannerSideState();
                    if (!File.Exists(_doubleBannerLeftArtworkPath) && !File.Exists(_doubleBannerRightArtworkPath))
                        return;
                }
                else if (!File.Exists(_imagePath.Text))
                {
                    return;
                }
            }

            try
            {
                Bitmap iconPreview;
                if (IsDualArtworkTemplate(template))
                {
                    CaptureActiveDoubleBannerSideState();
                    iconPreview = _imageProcessor.CreateDoubleBannerIconPreview(
                        _doubleBannerLeftArtworkPath,
                        _doubleBannerRightArtworkPath,
                        _iconPath.Text,
                        template,
                        _doubleBannerLeftFitMode,
                        _doubleBannerLeftPlacement,
                        _doubleBannerRightFitMode,
                        _doubleBannerRightPlacement,
                        GetIconMode(),
                        _iconPreview.Width - 4,
                        _iconPreview.Height - 4);
                }
                else
                {
                    iconPreview = _imageProcessor.CreateIconPreview(
                        _imagePath.Text,
                        _iconPath.Text,
                        template,
                        GetFitMode(template),
                        _placement,
                        GetIconMode(),
                        _iconPreview.Width - 4,
                        _iconPreview.Height - 4);
                }
                _iconPreview.Image = iconPreview;
            }
            catch
            {
                ClearPreview(_iconPreview);
            }
        }

        private void BrowseDoubleBannerArtwork(bool right)
        {
            TemplateDefinition template = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            if (!IsDualArtworkTemplate(template))
            {
                BrowseArtwork();
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                TemplateDefinition activeTemplate = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
                dialog.Title = right
                    ? "Choose " + (IsTwoSidedSignTemplate(activeTemplate) ? "Back" : "Right Banner") + " artwork / image"
                    : "Choose " + (IsTwoSidedSignTemplate(activeTemplate) ? "Front" : "Left Banner") + " artwork / image";
                dialog.Filter = "Image files|*.png;*.jpg;*.jpeg|PNG files|*.png|JPEG files|*.jpg;*.jpeg";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                ApplyDoubleBannerArtworkFile(right, dialog.FileName);
            }
        }

        private void ResetDoubleBannerSide(bool right)
        {
            TemplateDefinition template = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            if (!IsDualArtworkTemplate(template))
                return;

            ActivateDoubleBannerSide(right, false);
            string guide = NormaliseGuideColorName(_placement == null ? "Yellow" : _placement.GuideColorName);
            _placement = new ImagePlacementState();
            _placement.GuideColorName = guide;
            _imageProcessor.EnsureSizeFamilyReference(template, _placement);
            CaptureActiveDoubleBannerSideState();
            UpdateArtworkPreview();
        }

        private void DoubleBannerArtwork_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void DoubleBannerArtwork_DragDrop(object sender, DragEventArgs e)
        {
            TemplateDefinition template = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            if (!IsDualArtworkTemplate(template))
            {
                Artwork_DragDrop(sender, e);
                return;
            }

            string[] files = e.Data == null ? null : e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0 || !File.Exists(files[0]))
                return;
            string ext = Path.GetExtension(files[0]).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                return;

            Control control = sender as Control;
            bool right = control != null && string.Equals(control.Tag as string, "DoubleBannerRight", StringComparison.Ordinal);
            ApplyDoubleBannerArtworkFile(right, files[0]);
        }

        private void ApplyDoubleBannerArtworkFile(bool right, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            ActivateDoubleBannerSide(right, false);
            _imagePath.Text = filePath;
            _placement = new ImagePlacementState();
            ImagePlacementState displayPlacement = GetDoubleBannerDisplayPlacement(right);
            _placement.GuideColorName = NormaliseGuideColorName(displayPlacement == null ? "Yellow" : displayPlacement.GuideColorName);
            TemplateDefinition template = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            _imageProcessor.EnsureSizeFamilyReference(template, _placement);
            CaptureActiveDoubleBannerSideState();
            DisposeInteractiveArtworkSource();
            UpdateArtworkPreview();
        }

        private void BrowseArtwork()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Image files|*.png;*.jpg;*.jpeg|PNG files|*.png|JPEG files|*.jpg;*.jpeg";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _imagePath.Text = dialog.FileName;
                    ResetPlacement(false);
                    UpdateArtworkPreview();
                }
            }
        }

        private void BrowseIcon()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Image files|*.png;*.jpg;*.jpeg|PNG files|*.png|JPEG files|*.jpg;*.jpeg";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _iconPath.Text = dialog.FileName;
                    UpdateIconControls();
                }
            }
        }

        private void BrowseOutputFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.SelectedPath = Directory.Exists(_outputPath.Text) ? _outputPath.Text : SettingsService.GetGameModsFolder();
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    _outputPath.Text = dialog.SelectedPath;
            }
        }

        private void BuildCreateModPage(Panel legacySetup, Panel legacyForm)
        {
            // The old functional controls are retained but reorganised into the graphical
            // Create Mod workflow. Legacy containers stay hidden as a compatibility layer.
            legacySetup.Tag = "CreateModLegacyHidden";
            legacySetup.Visible = false;
            legacyForm.Tag = "CreateModLegacyHidden";
            legacyForm.Visible = false;

            _createVisualRoot = new Panel();
            _createVisualRoot.Name = "CreateModVisualRoot";
            // Create Mod and the embedded tabs share identical page bounds inside the folder.
            _createVisualRoot.Bounds = GetUnifiedTabPageBounds();
            _createVisualRoot.Anchor = AnchorStyles.None;
            _createVisualRoot.BackColor = TwoPointTheme.ContentBackground;
            Controls.Add(_createVisualRoot);
            ApplyCreateModRootRegion();

            TableLayoutPanel page = new TableLayoutPanel();
            _createPageLayout = page;
            page.Name = "CreateModPageLayout";
            page.Dock = DockStyle.Fill;
            page.BackColor = Color.Transparent;
            page.ColumnCount = 1;
            page.RowCount = 2;
            page.Padding = new Padding(7);
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            page.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            _createVisualRoot.Controls.Add(page);

            TableLayoutPanel main = new TableLayoutPanel();
            _createMainLayout = main;
            main.Name = "CreateModMainLayout";
            main.Dock = DockStyle.Fill;
            // Match the embedded My Mods / Settings page geometry exactly.
            // TableLayoutPanel defaults to a 3px Margin, which previously inset every
            // Create Mod section frame by 3px on the left and right.
            main.Margin = new Padding(0);
            main.ColumnCount = 2;
            main.RowCount = 1;
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54F));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46F));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            page.Controls.Add(main, 0, 0);

            TableLayoutPanel left = new TableLayoutPanel();
            _createLeftLayout = left;
            left.Dock = DockStyle.Fill;
            left.ColumnCount = 1;
            left.RowCount = 3;
            left.Margin = new Padding(0, 0, 5, 5);
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 175F));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 170F));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            main.Controls.Add(left, 0, 0);

            Panel itemSection = CreateModSection("Item Type");
            left.Controls.Add(itemSection, 0, 0);
            BuildItemTypeTiles(GetSectionBody(itemSection));

            _rugOptionsVisualPanel = CreateModSection("Item Options");
            left.Controls.Add(_rugOptionsVisualPanel, 0, 1);
            Panel itemOptionsBody = GetSectionBody(_rugOptionsVisualPanel);
            BuildDecorOptions(itemOptionsBody);
            BuildPosterOptions(itemOptionsBody);
            BuildRugOptions(itemOptionsBody);
            BuildBannerOptions(itemOptionsBody);
            BuildDoubleBannerOptions(itemOptionsBody);
            BuildHangingSignOptions(itemOptionsBody);
            BuildWallSignOptions(itemOptionsBody);

            Panel detailsSection = CreateModSection("Details");
            left.Controls.Add(detailsSection, 0, 2);
            BuildCreateModDetails(GetSectionBody(detailsSection));

            TableLayoutPanel right = new TableLayoutPanel();
            _createRightLayout = right;
            right.Name = "CreateModRightLayout";
            right.Dock = DockStyle.Fill;
            right.ColumnCount = 1;
            right.RowCount = 1;
            right.Margin = new Padding(5, 0, 0, 5);
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            main.Controls.Add(right, 1, 0);

            // Build progress lives in the shared bottom action strip,
            // matching My Mods. Artwork therefore receives the full right-hand height.
            Panel artworkSection = CreateModSection("Artwork / Image");
            right.Controls.Add(artworkSection, 0, 0);
            BuildArtworkEditor(GetSectionBody(artworkSection));

            TableLayoutPanel bottom = new TableLayoutPanel();
            _createBottomActions = bottom;
            bottom.Name = "CreateModBottomActions";
            bottom.Dock = DockStyle.Fill;
            bottom.ColumnCount = 2;
            bottom.RowCount = 1;
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 326F));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            // Use the same bottom action-strip geometry as My Mods.
            bottom.Padding = new Padding(6, 6, 6, 5);
            bottom.Margin = new Padding(0);
            bottom.BackColor = TwoPointTheme.ContentBackground;
            page.Controls.Add(bottom, 0, 1);

            FlowLayoutPanel createActions = new FlowLayoutPanel();
            createActions.Dock = DockStyle.Fill;
            createActions.FlowDirection = FlowDirection.LeftToRight;
            createActions.WrapContents = false;
            createActions.Margin = new Padding(0);
            createActions.Padding = new Padding(0);
            createActions.BackColor = Color.Transparent;
            bottom.Controls.Add(createActions, 0, 0);

            _createProgressHost = new Panel();
            _createProgressHost.Dock = DockStyle.Fill;
            _createProgressHost.Margin = new Padding(12, 0, 12, 0);
            _createProgressHost.BackColor = Color.Transparent;
            _createProgressHost.Visible = false;
            bottom.Controls.Add(_createProgressHost, 1, 0);

            _createProgressLabel = new Label();
            _createProgressLabel.Dock = DockStyle.Top;
            _createProgressLabel.Height = 20;
            _createProgressLabel.ForeColor = TwoPointTheme.PrimaryText;
            _createProgressLabel.Font = TwoPointTheme.BoldFont(8.2F);
            _createProgressLabel.TextAlign = ContentAlignment.MiddleLeft;
            _createProgressHost.Controls.Add(_createProgressLabel);

            _progress.Parent = _createProgressHost;
            _progress.Dock = DockStyle.Top;
            _progress.Height = 12;
            _progress.Top = 23;
            _progress.Margin = new Padding(0);
            _progress.Style = ProgressBarStyle.Marquee;
            _progress.MarqueeAnimationSpeed = 0;
            _progress.BringToFront();

            _createProgressHideTimer = new Timer();
            _createProgressHideTimer.Interval = 4500;
            _createProgressHideTimer.Tick += delegate
            {
                _createProgressHideTimer.Stop();
                if (!_createProgressBusy && _createProgressHost != null)
                    _createProgressHost.Visible = false;
            };

            // Keep the technical log alive as a hidden diagnostic sink, but remove it from
            // the everyday Create Mod workflow as requested.
            _log.Parent = _createVisualRoot;
            _log.Visible = false;

            _buildButton.Parent = createActions;
            _buildButton.Dock = DockStyle.None;
            _buildButton.Size = TwoPointTheme.BottomActionButtonSize;
            _buildButton.Margin = new Padding(0, 0, 8, 0);
            _buildButton.Font = TwoPointTheme.BoldFont(TwoPointTheme.BottomActionFontSize);
            _buildButton.TextImageRelation = TextImageRelation.TextBeforeImage;
            TwoPointTheme.StylePrimaryButton(_buildButton);
            TwoPointTheme.ApplyBottomActionFont(_buildButton);

            _openOutputButton.Parent = createActions;
            _openOutputButton.Dock = DockStyle.None;
            _openOutputButton.Size = TwoPointTheme.BottomActionButtonSize;
            _openOutputButton.Margin = new Padding(0);
            _openOutputButton.Font = TwoPointTheme.BoldFont(TwoPointTheme.BottomActionFontSize);
            _openOutputButton.Text = "Open Built Mod";
            _openOutputButton.Visible = true;
            TwoPointTheme.StyleButton(_openOutputButton);
            TwoPointTheme.ApplyBottomActionFont(_openOutputButton);

            // Output configuration belongs in Settings. Keep the existing hidden field as the
            // build pipeline's value source so saved settings continue to work unchanged.
            _outputPath.Visible = false;

            // The legacy template dropdown remains the data source for existing build logic.
            _templateBox.Visible = false;
            _fitMode.Visible = false;
            _imagePath.Visible = false;

            _createVisualRoot.SizeChanged += delegate
            {
                ApplyCreateModRootRegion();
                ApplyCreateModResponsiveProfile();
                if (_createVisualRoot.Visible && IsHandleCreated)
                    BeginInvoke((Action)delegate { UpdateArtworkPreview(); });
            };

            _fitMode.SelectedIndexChanged += delegate { SyncFitButtons(); };

            SyncCreateModFromTemplate(_templateBox.SelectedItem as TemplateDefinition);
            _createVisualRoot.BringToFront();
            TwoPointTheme.BringShellToFront(this);
        }

        private void ApplyRecommendedInitialWindowSize()
        {
            Screen screen = Screen.PrimaryScreen;
            if (screen == null)
                return;

            Rectangle bounds = screen.Bounds;
            Rectangle work = screen.WorkingArea;
            Size target;

            // Primary design target: the approved 1920 x 1080 opening size.
            if (bounds.Width >= 1900 && bounds.Height >= 1060)
                target = new Size(1512, 987);
            // Common 16:9 lower resolution: 1600 x 900.
            else if (bounds.Width >= 1580 && bounds.Height >= 880)
                target = new Size(1380, 820);
            // Common 16:9 lower resolution: 1366 x 768 (and nearby 1360 x 768 displays).
            // At this resolution the app benefits from using most of the working area; keeping
            // a large desktop margin makes the Details/Artwork controls unnecessarily cramped.
            else if (bounds.Width >= 1340 && bounds.Height >= 750)
                target = new Size(Math.Min(1320, Math.Max(1240, work.Width - 18)),
                    Math.Min(718, Math.Max(690, work.Height - 10)));
            else
                target = new Size(Math.Min(1180, Math.Max(960, work.Width - 30)),
                    Math.Min(680, Math.Max(620, work.Height - 20)));

            target.Width = Math.Min(target.Width, Math.Max(960, work.Width - 24));
            target.Height = Math.Min(target.Height, Math.Max(620, work.Height - 20));
            Size = target;
        }

        private void ApplyCreateModResponsiveProfile()
        {
            if (_applyingResponsiveProfile || _createVisualRoot == null || _createPageLayout == null ||
                _createMainLayout == null || _createLeftLayout == null || _createRightLayout == null)
                return;

            _applyingResponsiveProfile = true;
            try
            {
                int width = Math.Max(1, ClientSize.Width);
                int height = Math.Max(1, ClientSize.Height);
                // Compact is the 1366x768-class profile. Medium covers 1600x900.
                // Use client height as the main trigger because Windows taskbars/title bars
                // reduce the actual usable height considerably on a nominal 768px display.
                bool compact = height < 780 || width < 1320;
                bool medium = !compact && (height < 900 || width < 1500);

                float itemHeight;
                float optionHeight;
                float bottomHeight;
                int itemWrapperWidth;
                int itemWrapperHeight;
                int itemButtonSize;
                int itemCaptionY;
                int shapeButtonSize;
                float moneyRowHeight;
                int iconPreviewSize;
                int iconSourceX;

                if (compact)
                {
                    // 1366x768: preserve the full visual hierarchy while reclaiming enough
                    // vertical space for Details and the complete Artwork controls.
                    itemHeight = 122F;
                    optionHeight = 108F;
                    bottomHeight = 60F;
                    itemWrapperWidth = 80;
                    itemWrapperHeight = 76;
                    itemButtonSize = 54;
                    itemCaptionY = 56;
                    shapeButtonSize = 42;
                    moneyRowHeight = 44F;
                    iconPreviewSize = 62;
                    iconSourceX = 74;
                }
                else if (medium)
                {
                    itemHeight = 155F;
                    optionHeight = 145F;
                    bottomHeight = 60F;
                    itemWrapperWidth = 94;
                    itemWrapperHeight = 96;
                    itemButtonSize = 70;
                    itemCaptionY = 72;
                    shapeButtonSize = 50;
                    moneyRowHeight = 56F;
                    iconPreviewSize = 82;
                    iconSourceX = 96;
                }
                else
                {
                    itemHeight = 175F;
                    optionHeight = 170F;
                    bottomHeight = 60F;
                    itemWrapperWidth = 104;
                    itemWrapperHeight = 104;
                    itemButtonSize = 80;
                    itemCaptionY = 82;
                    shapeButtonSize = 54;
                    moneyRowHeight = 64F;
                    iconPreviewSize = 96;
                    iconSourceX = 110;
                }

                _createPageLayout.RowStyles[1].Height = bottomHeight;
                _createLeftLayout.RowStyles[0].Height = itemHeight;
                _createLeftLayout.RowStyles[1].Height = optionHeight;

                // Give the artwork a little more width as windows get smaller while retaining
                // the same two-column visual structure.
                if (compact)
                {
                    _createMainLayout.ColumnStyles[0].Width = 47F;
                    _createMainLayout.ColumnStyles[1].Width = 53F;
                }
                else if (medium)
                {
                    _createMainLayout.ColumnStyles[0].Width = 52F;
                    _createMainLayout.ColumnStyles[1].Width = 48F;
                }
                else
                {
                    _createMainLayout.ColumnStyles[0].Width = 54F;
                    _createMainLayout.ColumnStyles[1].Width = 46F;
                }

                if (_createBottomActions != null)
                {
                    _createBottomActions.Padding = new Padding(6, 6, 6, 5);
                    _createBottomActions.ColumnStyles[0].Width = 326F;
                    _buildButton.Size = TwoPointTheme.BottomActionButtonSize;
                    _openOutputButton.Size = TwoPointTheme.BottomActionButtonSize;
                    if (_createProgressLabel != null)
                        _createProgressLabel.Height = compact ? 17 : 20;
                    if (_progress != null)
                        _progress.Height = compact ? 10 : 12;
                }

                if (_itemTilesFlow != null)
                {
                    _itemTilesFlow.Padding = compact ? new Padding(4, 1, 4, 0) : new Padding(6, 3, 6, 1);
                    foreach (KeyValuePair<string, Button> pair in _itemTypeTiles)
                    {
                        Button button = pair.Value;
                        Panel wrapper = button == null ? null : button.Parent as Panel;
                        if (button == null || wrapper == null)
                            continue;

                        wrapper.Size = new Size(itemWrapperWidth, itemWrapperHeight);
                        wrapper.Margin = new Padding(3, 0, compact ? 3 : 5, 0);
                        button.Size = new Size(itemButtonSize, itemButtonSize);
                        button.Location = new Point((itemWrapperWidth - itemButtonSize) / 2, 1);

                        foreach (Control child in wrapper.Controls)
                        {
                            Label caption = child as Label;
                            if (caption == null)
                                continue;
                            caption.Location = new Point(0, itemCaptionY);
                            caption.Size = new Size(itemWrapperWidth, Math.Max(18, itemWrapperHeight - itemCaptionY));
                        }
                    }
                }

                if (_shapeTilesFlow != null)
                {
                    _shapeTilesFlow.Height = shapeButtonSize + 8;
                    foreach (KeyValuePair<string, Button> pair in _rugShapeTiles)
                    {
                        Button button = pair.Value;
                        if (button == null)
                            continue;
                        button.Size = new Size(shapeButtonSize, shapeButtonSize);
                        button.Margin = compact ? new Padding(3, 1, 3, 1) : new Padding(4, 2, 4, 2);
                    }
                }

                if (_bannerTilesFlow != null)
                {
                    foreach (KeyValuePair<string, Button> pair in _bannerThemeTiles)
                    {
                        Button button = pair.Value;
                        if (button == null)
                            continue;
                        button.Size = new Size(shapeButtonSize, shapeButtonSize);
                        button.Margin = compact ? new Padding(3, 1, 3, 1) : new Padding(4, 2, 4, 2);
                    }
                    SyncBannerThemeScrollBar();
                }

                if (_doubleBannerTilesFlow != null)
                {
                    foreach (KeyValuePair<string, Button> pair in _doubleBannerThemeTiles)
                    {
                        Button button = pair.Value;
                        if (button == null)
                            continue;
                        button.Size = new Size(shapeButtonSize, shapeButtonSize);
                        button.Margin = compact ? new Padding(3, 1, 3, 1) : new Padding(4, 2, 4, 2);
                    }
                    SyncDoubleBannerThemeScrollBar();
                }

                if (_detailsRightFields != null && _detailsRightFields.RowStyles.Count >= 3)
                {
                    _detailsRightFields.RowStyles[0].Height = moneyRowHeight;
                    _detailsRightFields.RowStyles[1].Height = moneyRowHeight;
                }

                if (_iconPreview != null && _iconPreview.Parent != null)
                {
                    _iconPreview.Size = new Size(iconPreviewSize, iconPreviewSize);
                    _iconPreview.Location = new Point(0, 24);

                    Control holder = _iconPreview.Parent;
                    foreach (Control child in holder.Controls)
                    {
                        Label label = child as Label;
                        if (label != null && string.Equals(label.Text, "Source", StringComparison.OrdinalIgnoreCase))
                            label.Location = new Point(iconSourceX, compact ? 4 : 8);
                    }

                    _iconMode.Location = new Point(iconSourceX, compact ? 22 : 30);
                    _iconMode.Size = new Size(compact ? 112 : medium ? 132 : 145, compact ? 24 : 27);
                    _iconPath.Location = new Point(iconSourceX, compact ? 50 : 68);
                    _iconPath.Size = new Size(compact ? 128 : medium ? 155 : 175, 24);
                    _iconBrowse.Location = new Point(_iconPath.Right + 6, compact ? 48 : 66);
                }

                if (_artworkLayout != null && _artworkLayout.RowStyles.Count >= 3)
                {
                    // Fix 16 uses three deterministic bands: toolbar, flexible preview and
                    // a fixed two-row control block. Only the preview is allowed to resize.
                    _artworkLayout.Padding = compact
                        ? new Padding(5, 5, 5, 5)
                        : medium ? new Padding(7, 6, 7, 6) : new Padding(7, 7, 7, 7);
                    _artworkLayout.RowStyles[0].Height = 40F;
                    _artworkLayout.RowStyles[2].Height = 82F;
                }

                ApplyCreateModRootRegion();
            }
            finally
            {
                _applyingResponsiveProfile = false;
            }
        }

        private Rectangle GetUnifiedTabPageBounds()
        {
            return new Rectangle(18, 106,
                Math.Max(100, ClientSize.Width - 36),
                Math.Max(100, ClientSize.Height - 128));
        }

        private void SyncUnifiedTabPageBounds()
        {
            Rectangle bounds = GetUnifiedTabPageBounds();
            if (_createVisualRoot != null && !_createVisualRoot.IsDisposed)
                _createVisualRoot.Bounds = bounds;
            if (_tabPageHost != null && !_tabPageHost.IsDisposed)
                _tabPageHost.Bounds = bounds;
        }

        private void ApplyCreateModRootRegion()
        {
            if (_createVisualRoot == null || _createVisualRoot.Width <= 2 || _createVisualRoot.Height <= 2)
                return;

            Region oldRegion = _createVisualRoot.Region;
            using (System.Drawing.Drawing2D.GraphicsPath path =
                TwoPointTheme.RoundedRectangle(
                    new Rectangle(0, 0, Math.Max(1, _createVisualRoot.Width - 1), Math.Max(1, _createVisualRoot.Height - 1)),
                    12))
            {
                _createVisualRoot.Region = new Region(path);
            }

            if (oldRegion != null)
                oldRegion.Dispose();
        }

        private void ApplyTabPageHostRegion()
        {
            if (_tabPageHost == null || _tabPageHost.Width <= 2 || _tabPageHost.Height <= 2)
                return;

            Region oldRegion = _tabPageHost.Region;
            using (System.Drawing.Drawing2D.GraphicsPath path =
                TwoPointTheme.RoundedRectangle(
                    new Rectangle(0, 0, Math.Max(1, _tabPageHost.Width - 1), Math.Max(1, _tabPageHost.Height - 1)),
                    12))
            {
                _tabPageHost.Region = new Region(path);
            }

            if (oldRegion != null)
                oldRegion.Dispose();
        }

        private Panel CreateModSection(string title)
        {
            Panel section = new Panel();
            section.Dock = DockStyle.Fill;
            section.Margin = new Padding(0, 0, 0, 5);
            section.BackColor = TwoPointTheme.PanelLight;
            section.BorderStyle = BorderStyle.FixedSingle;

            Label header = new Label();
            header.Text = title;
            header.Name = "CreateModSectionHeader";
            header.Location = new Point(10, 7);
            header.Size = new Size(190, 28);
            TwoPointTheme.StyleSectionHeader(header);
            section.Controls.Add(header);

            Panel body = new Panel();
            body.Name = "CreateModSectionBody";
            body.Location = new Point(8, 39);
            body.Size = new Size(Math.Max(10, section.ClientSize.Width - 16), Math.Max(10, section.ClientSize.Height - 47));
            body.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            body.BackColor = TwoPointTheme.PanelLight;
            section.Controls.Add(body);
            return section;
        }

        private static Panel GetSectionBody(Panel section)
        {
            foreach (Control control in section.Controls)
            {
                if (control.Name == "CreateModSectionBody")
                    return control as Panel;
            }
            return section;
        }

        private void BuildItemTypeTiles(Panel body)
        {
            _itemTilesViewport = new Panel();
            _itemTilesViewport.Dock = DockStyle.Fill;
            _itemTilesViewport.BackColor = Color.Transparent;
            body.Controls.Add(_itemTilesViewport);

            FlowLayoutPanel tiles = new FlowLayoutPanel();
            _itemTilesFlow = tiles;
            tiles.Name = "CreateModItemTilesFlow";
            tiles.AutoSize = true;
            tiles.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tiles.FlowDirection = FlowDirection.LeftToRight;
            tiles.WrapContents = false;
            tiles.Padding = new Padding(6, 3, 6, 1);
            tiles.BackColor = Color.Transparent;
            tiles.Location = new Point(0, 0);
            _itemTilesViewport.Controls.Add(tiles);

            _itemTilesScrollBar = new TwoPointHorizontalScrollBar();
            _itemTilesScrollBar.Dock = DockStyle.Bottom;
            _itemTilesScrollBar.Height = 14;
            _itemTilesScrollBar.Visible = false;
            _itemTilesViewport.Controls.Add(_itemTilesScrollBar);
            _itemTilesScrollBar.BringToFront();

            AddItemTypeTile(tiles, "Decor", LoadItemTypeImage("item_decor.png", "Decor"));
            AddItemTypeTile(tiles, "Poster", LoadItemTypeImage("item_poster.png", "Poster"));
            AddItemTypeTile(tiles, "Mural", LoadItemTypeImage("item_mural.png", "Mural"));
            AddItemTypeTile(tiles, "Small Rug", LoadItemTypeImage("item_small_rug.png", "Small Rug"));
            AddItemTypeTile(tiles, "Large Rug", LoadItemTypeImage("item_large_rug.png", "Large Rug"));
            AddItemTypeTile(tiles, "Single Banner", LoadItemTypeImage("item_single_banner.png", "Single Banner"));
            AddItemTypeTile(tiles, "Double Banner", LoadItemTypeImage("item_double_banner.png", "Double Banner"));
            AddItemTypeTile(tiles, "Hanging Sign", LoadItemTypeImage("item_hanging_sign.png", "Hanging Sign"));
            AddItemTypeTile(tiles, "Wall Sign", LoadItemTypeImage("item_wall_sign.png", "Wall Sign"));

            _itemTilesViewport.Resize += delegate { SyncItemTypeScrollBar(); };
            tiles.SizeChanged += delegate { SyncItemTypeScrollBar(); };
            _itemTilesScrollBar.ValueChanged += delegate
            {
                tiles.Left = -_itemTilesScrollBar.Value;
            };

            MouseEventHandler wheel = delegate(object sender, MouseEventArgs e)
            {
                if (_itemTilesScrollBar == null || !_itemTilesScrollBar.Visible)
                    return;
                int delta = e.Delta > 0 ? -45 : 45;
                _itemTilesScrollBar.Value = Math.Max(0, Math.Min(_itemTilesScrollBar.Maximum, _itemTilesScrollBar.Value + delta));
                tiles.Left = -_itemTilesScrollBar.Value;
            };
            tiles.MouseWheel += wheel;
            _itemTilesViewport.MouseWheel += wheel;

            SyncItemTypeScrollBar();
        }

        private void SyncItemTypeScrollBar()
        {
            if (_itemTilesViewport == null || _itemTilesFlow == null || _itemTilesScrollBar == null)
                return;

            int contentWidth = Math.Max(_itemTilesFlow.PreferredSize.Width, _itemTilesFlow.Width);
            int viewportWidth = Math.Max(1, _itemTilesViewport.ClientSize.Width);
            int maximum = Math.Max(0, contentWidth - viewportWidth);
            _itemTilesScrollBar.Maximum = maximum;
            _itemTilesScrollBar.LargeChange = viewportWidth;
            _itemTilesScrollBar.Visible = maximum > 0;
            if (maximum <= 0)
            {
                _itemTilesScrollBar.Value = 0;
                _itemTilesFlow.Left = 0;
            }
            else
            {
                _itemTilesScrollBar.Value = Math.Min(_itemTilesScrollBar.Value, maximum);
                _itemTilesFlow.Left = -_itemTilesScrollBar.Value;
            }
        }

        private Image LoadItemTypeImage(string fileName, string fallbackType)
        {
            Image image = TwoPointTheme.LoadThemeImage(fileName);
            return image ?? CreateItemTypeTileImage(fallbackType);
        }

        private void AddItemTypeTile(FlowLayoutPanel parent, string key, Image image)
        {
            string displayName = string.Equals(key, "Single Banner", StringComparison.OrdinalIgnoreCase) ? "Banner" :
                (string.Equals(key, "Decor", StringComparison.OrdinalIgnoreCase) ? "Décor" : key);

            Panel wrapper = new Panel();
            wrapper.Size = new Size(104, 104);
            wrapper.Margin = new Padding(3, 0, 5, 0);
            wrapper.BackColor = Color.Transparent;

            Button button = new Button();
            button.Text = string.Empty;
            button.Tag = key;
            button.Size = new Size(80, 80);
            button.Location = new Point(12, 1);
            button.Image = CreateCenteredTileIcon(image, 64);
            button.ImageAlign = ContentAlignment.MiddleCenter;
            button.BackgroundImage = null;
            button.Click += delegate { SelectItemType(key); };
            TwoPointTheme.StyleSelectableButton(button, false);
            button.Padding = new Padding(2);
            wrapper.Controls.Add(button);

            Label caption = NewCreateModLabel(displayName);
            caption.AutoSize = false;
            caption.TextAlign = ContentAlignment.MiddleCenter;
            caption.Location = new Point(0, 82);
            caption.Size = new Size(104, 21);
            wrapper.Controls.Add(caption);

            parent.Controls.Add(wrapper);
            _itemTypeTiles[key] = button;
            string itemTypeTip = string.Equals(key, "Decor", StringComparison.OrdinalIgnoreCase)
                ? "Create room visual décor such as Wallpaper. Additional Décor options can be added here later."
                : string.Equals(key, "Double Banner", StringComparison.OrdinalIgnoreCase)
                ? "Create a Double Banner mod with independently positioned Left and Right artwork."
                : string.Equals(key, "Hanging Sign", StringComparison.OrdinalIgnoreCase)
                    ? "Create a Hanging Sign mod with independently positioned Front and Back artwork."
                    : string.Equals(key, "Wall Sign", StringComparison.OrdinalIgnoreCase)
                        ? "Create a Wall Sign mod with independently positioned Front and Back artwork."
                        : "Create a " + displayName + " mod.";
            SetUiTip(button, itemTypeTip);
            SetUiTip(caption, itemTypeTip);
        }

        private static Bitmap CreateCenteredTileIcon(Image source, int canvasSize)
        {
            int size = Math.Max(16, canvasSize);
            Bitmap output = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            if (source == null)
                return output;

            Rectangle sourceBounds = new Rectangle(0, 0, source.Width, source.Height);
            Bitmap sourceBitmap = source as Bitmap;

            if (sourceBitmap != null && sourceBitmap.PixelFormat != System.Drawing.Imaging.PixelFormat.Undefined)
            {
                int minX = sourceBitmap.Width;
                int minY = sourceBitmap.Height;
                int maxX = -1;
                int maxY = -1;

                for (int y = 0; y < sourceBitmap.Height; y++)
                {
                    for (int x = 0; x < sourceBitmap.Width; x++)
                    {
                        if (sourceBitmap.GetPixel(x, y).A <= 8)
                            continue;
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }

                if (maxX >= minX && maxY >= minY)
                    sourceBounds = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
            }

            int available = Math.Max(8, size - 6);
            double scale = Math.Min(
                (double)available / Math.Max(1, sourceBounds.Width),
                (double)available / Math.Max(1, sourceBounds.Height));

            int drawWidth = Math.Max(1, (int)Math.Round(sourceBounds.Width * scale));
            int drawHeight = Math.Max(1, (int)Math.Round(sourceBounds.Height * scale));
            Rectangle destination = new Rectangle(
                (size - drawWidth) / 2,
                (size - drawHeight) / 2,
                drawWidth,
                drawHeight);

            using (Graphics g = Graphics.FromImage(output))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.DrawImage(source, destination, sourceBounds, GraphicsUnit.Pixel);
            }

            return output;
        }


        private static Bitmap CreateRugShapeTileIcon(Image source)
        {
            // Preserve the user's complete 80x80 option-icon canvas. Do not crop/trim the
            // transparent padding around the artwork: that spacing is intentional and keeps
            // the Square, Rectangle, Circle and Octagon visually proportional to one another.
            // The entire source canvas is scaled uniformly and centred in the option button.
            const int canvasSize = 40;
            Bitmap output = new Bitmap(canvasSize, canvasSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            if (source == null)
                return output;

            double scale = Math.Min(
                (double)canvasSize / Math.Max(1, source.Width),
                (double)canvasSize / Math.Max(1, source.Height));
            int drawWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
            int drawHeight = Math.Max(1, (int)Math.Round(source.Height * scale));
            Rectangle destination = new Rectangle(
                (canvasSize - drawWidth) / 2,
                (canvasSize - drawHeight) / 2,
                drawWidth,
                drawHeight);

            using (Graphics g = Graphics.FromImage(output))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.DrawImage(source, destination, new Rectangle(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
            }

            return output;
        }

        private static Bitmap CreateHangingSignSizeTileIcon(Image source)
        {
            // Preserve the supplied 80x80 option-icon composition instead of trimming its
            // transparent/blank padding.  The generic tile renderer crops to the visible
            // artwork and enlarges it, which makes the Small/Large signs lose their intended
            // relative size.  Scale the whole source canvas into a common centred 40x40 box.
            const int canvasSize = 40;
            Bitmap output = new Bitmap(canvasSize, canvasSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            if (source == null)
                return output;

            int available = canvasSize;
            double scale = Math.Min(
                (double)available / Math.Max(1, source.Width),
                (double)available / Math.Max(1, source.Height));
            int drawWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
            int drawHeight = Math.Max(1, (int)Math.Round(source.Height * scale));
            Rectangle destination = new Rectangle(
                (canvasSize - drawWidth) / 2,
                (canvasSize - drawHeight) / 2,
                drawWidth,
                drawHeight);

            using (Graphics g = Graphics.FromImage(output))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.DrawImage(source, destination, new Rectangle(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
            }

            return output;
        }

        private static Bitmap CreatePosterSizeTileIcon(Image source)
        {
            // Poster-size option icons need to be comparable by height.  The generic
            // CreateCenteredTileIcon helper trims transparent padding and then scales each
            // image to fill the same square; that made the short poster appear wider/larger.
            // Instead, crop only for measurement, render every poster at one fixed width,
            // preserve its aspect ratio, and centre it in the common canvas.
            const int canvasSize = 46;
            const int targetWidth = 22;

            Bitmap output = new Bitmap(canvasSize, canvasSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            if (source == null)
                return output;

            Rectangle sourceBounds = new Rectangle(0, 0, source.Width, source.Height);
            Bitmap sourceBitmap = source as Bitmap;
            if (sourceBitmap != null && sourceBitmap.PixelFormat != System.Drawing.Imaging.PixelFormat.Undefined)
            {
                int minX = sourceBitmap.Width;
                int minY = sourceBitmap.Height;
                int maxX = -1;
                int maxY = -1;

                for (int y = 0; y < sourceBitmap.Height; y++)
                {
                    for (int x = 0; x < sourceBitmap.Width; x++)
                    {
                        if (sourceBitmap.GetPixel(x, y).A <= 8)
                            continue;
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }

                if (maxX >= minX && maxY >= minY)
                    sourceBounds = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
            }

            double scale = (double)targetWidth / Math.Max(1, sourceBounds.Width);
            int drawWidth = targetWidth;
            int drawHeight = Math.Max(1, (int)Math.Round(sourceBounds.Height * scale));

            // Defensive cap only; with the supplied poster icons this is not reached.
            int maxHeight = canvasSize - 2;
            if (drawHeight > maxHeight)
            {
                double heightScale = (double)maxHeight / drawHeight;
                drawHeight = maxHeight;
                drawWidth = Math.Max(1, (int)Math.Round(drawWidth * heightScale));
            }

            Rectangle destination = new Rectangle(
                (canvasSize - drawWidth) / 2,
                (canvasSize - drawHeight) / 2,
                drawWidth,
                drawHeight);

            using (Graphics g = Graphics.FromImage(output))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.DrawImage(source, destination, sourceBounds, GraphicsUnit.Pixel);
            }

            return output;
        }

        private Image CreateItemTypeTileImage(string type)
        {
            Bitmap image = new Bitmap(92, 66);
            using (Graphics g = Graphics.FromImage(image))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                Color dark = Color.FromArgb(74, 59, 43);
                Color warm = Color.FromArgb(205, 108, 53);
                Color light = Color.FromArgb(255, 238, 184);
                using (Pen darkPen = new Pen(dark, 4F))
                using (Brush warmBrush = new SolidBrush(warm))
                using (Brush lightBrush = new SolidBrush(light))
                {
                    if (type == "Poster")
                    {
                        Rectangle rect = new Rectangle(25, 4, 42, 57);
                        g.FillRectangle(warmBrush, rect);
                        g.DrawRectangle(darkPen, rect);
                        g.FillEllipse(lightBrush, 34, 16, 24, 24);
                    }
                    else if (type == "Mural")
                    {
                        Rectangle rect = new Rectangle(7, 17, 78, 34);
                        g.FillRectangle(warmBrush, rect);
                        g.DrawRectangle(darkPen, rect);
                        g.DrawLine(darkPen, 20, 42, 37, 28);
                        g.DrawLine(darkPen, 37, 28, 51, 41);
                        g.DrawLine(darkPen, 51, 41, 67, 25);
                    }
                    else if (type == "Small Rug")
                    {
                        g.FillEllipse(warmBrush, 22, 5, 50, 50);
                        g.DrawEllipse(darkPen, 22, 5, 50, 50);
                        g.DrawEllipse(new Pen(light, 5F), 33, 16, 28, 28);
                    }
                    else if (type == "Single Banner")
                    {
                        Rectangle banner = new Rectangle(32, 4, 28, 58);
                        g.FillRectangle(warmBrush, banner);
                        g.DrawRectangle(darkPen, banner);
                        g.DrawLine(darkPen, 27, 4, 65, 4);
                        g.DrawLine(darkPen, 27, 62, 65, 62);
                    }
                    else if (type == "Double Banner")
                    {
                        Rectangle leftBanner = new Rectangle(18, 7, 24, 51);
                        Rectangle rightBanner = new Rectangle(50, 7, 24, 51);
                        g.FillRectangle(warmBrush, leftBanner);
                        g.FillRectangle(lightBrush, rightBanner);
                        g.DrawRectangle(darkPen, leftBanner);
                        g.DrawRectangle(darkPen, rightBanner);
                        g.DrawLine(darkPen, 46, 3, 46, 63);
                        g.DrawLine(darkPen, 13, 5, 79, 5);
                    }
                    else if (type == "Hanging Sign")
                    {
                        Rectangle sign = new Rectangle(14, 22, 64, 28);
                        g.FillRectangle(warmBrush, sign);
                        g.DrawRectangle(darkPen, sign);
                        g.DrawLine(darkPen, 22, 5, 22, 22);
                        g.DrawLine(darkPen, 70, 5, 70, 22);
                        g.DrawLine(darkPen, 18, 22, 26, 14);
                        g.DrawLine(darkPen, 66, 14, 74, 22);
                    }
                    else if (type == "Wall Sign")
                    {
                        Rectangle sign = new Rectangle(12, 20, 66, 28);
                        g.FillRectangle(warmBrush, sign);
                        g.DrawRectangle(darkPen, sign);
                        g.DrawRectangle(darkPen, 78, 25, 10, 18);
                    }
                    else
                    {
                        Point[] pts = new Point[] { new Point(10, 18), new Point(23, 6), new Point(77, 6), new Point(86, 18), new Point(80, 52), new Point(16, 52) };
                        g.FillPolygon(warmBrush, pts);
                        g.DrawPolygon(darkPen, pts);
                        g.DrawRectangle(new Pen(light, 5F), 29, 18, 38, 23);
                    }
                }
            }
            return image;
        }

        private void BuildDecorOptions(Panel body)
        {
            Panel host = new Panel();
            _decorOptionsContentPanel = host;
            host.Name = "CreateModDecorOptionsContent";
            host.Dock = DockStyle.Fill;
            host.BackColor = Color.Transparent;
            host.Visible = false;
            body.Controls.Add(host);

            Label typeLabel = NewCreateModLabel("Type");
            typeLabel.Location = new Point(10, 3);
            host.Controls.Add(typeLabel);

            _wallpaperOptionTile = new Button();
            _wallpaperOptionTile.Text = string.Empty;
            _wallpaperOptionTile.Tag = "Wallpaper";
            _wallpaperOptionTile.Size = new Size(54, 54);
            _wallpaperOptionTile.Location = new Point(10, 29);
            _wallpaperOptionTile.Image = CreateCenteredTileIcon(TwoPointTheme.LoadThemeImage("item_option_wallpaper.png"), 40);
            _wallpaperOptionTile.ImageAlign = ContentAlignment.MiddleCenter;
            _wallpaperOptionTile.Padding = new Padding(0);
            _wallpaperOptionTile.AccessibleName = "Wallpaper";
            _wallpaperOptionTile.Click += delegate
            {
                if (_syncingGraphicalTemplateControls)
                    return;
                SelectWallpaperTemplate();
            };
            TwoPointTheme.StyleSelectableButton(_wallpaperOptionTile, true);
            host.Controls.Add(_wallpaperOptionTile);
            SetUiTip(_wallpaperOptionTile, "Wallpaper");
        }

        private void BuildPosterOptions(Panel body)
        {
            Panel host = new Panel();
            _posterOptionsContentPanel = host;
            host.Name = "CreateModPosterOptionsContent";
            host.Dock = DockStyle.Fill;
            host.BackColor = Color.Transparent;
            host.Visible = false;
            body.Controls.Add(host);

            Label sizeLabel = NewCreateModLabel("Size");
            sizeLabel.Location = new Point(10, 3);
            host.Controls.Add(sizeLabel);

            FlowLayoutPanel sizeTiles = new FlowLayoutPanel();
            sizeTiles.Name = "CreateModPosterSizeTilesFlow";
            sizeTiles.Location = new Point(6, 27);
            sizeTiles.Size = new Size(Math.Max(220, host.ClientSize.Width - 12), 62);
            sizeTiles.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            sizeTiles.FlowDirection = FlowDirection.LeftToRight;
            sizeTiles.WrapContents = false;
            sizeTiles.Padding = new Padding(0);
            sizeTiles.BackColor = Color.Transparent;
            host.Controls.Add(sizeTiles);

            AddPosterSizeTile(sizeTiles, "Small Poster", "poster_size_small.png", "Small");
            AddPosterSizeTile(sizeTiles, "Standard Poster", "poster_size_standard.png", "Standard");
            AddPosterSizeTile(sizeTiles, "Tall Poster", "poster_size_tall.png", "Tall");

            if (_posterSizeTiles.ContainsKey("Standard Poster"))
                TwoPointTheme.StyleSelectableButton(_posterSizeTiles["Standard Poster"], true);
        }

        private void AddPosterSizeTile(FlowLayoutPanel parent, string templateKey, string imageFile, string tooltipText)
        {
            Button button = new Button();
            button.Text = string.Empty;
            button.Tag = templateKey;
            button.Size = new Size(54, 54);
            button.Margin = new Padding(4, 2, 4, 2);
            button.Image = CreatePosterSizeTileIcon(TwoPointTheme.LoadThemeImage(imageFile));
            button.ImageAlign = ContentAlignment.MiddleCenter;
            button.Padding = new Padding(0);
            button.AccessibleName = templateKey;
            button.Click += delegate
            {
                if (_syncingGraphicalTemplateControls)
                    return;
                foreach (KeyValuePair<string, Button> pair in _posterSizeTiles)
                    TwoPointTheme.StyleSelectableButton(pair.Value, string.Equals(pair.Key, templateKey, StringComparison.OrdinalIgnoreCase));
                SelectPosterTemplate(templateKey);
            };
            TwoPointTheme.StyleSelectableButton(button, false);
            parent.Controls.Add(button);
            _posterSizeTiles[templateKey] = button;
            SetUiTip(button, tooltipText);
        }


        private void BuildHangingSignOptions(Panel body)
        {
            Panel host = new Panel();
            _hangingSignOptionsContentPanel = host;
            host.Name = "CreateModHangingSignOptionsContent";
            host.Dock = DockStyle.Fill;
            host.BackColor = Color.Transparent;
            host.Visible = false;
            body.Controls.Add(host);

            Label sizeLabel = NewCreateModLabel("Size");
            sizeLabel.Location = new Point(10, 3);
            host.Controls.Add(sizeLabel);

            FlowLayoutPanel sizeTiles = new FlowLayoutPanel();
            sizeTiles.Name = "CreateModHangingSignSizeTilesFlow";
            sizeTiles.Location = new Point(6, 27);
            sizeTiles.Size = new Size(Math.Max(220, host.ClientSize.Width - 12), 62);
            sizeTiles.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            sizeTiles.FlowDirection = FlowDirection.LeftToRight;
            sizeTiles.WrapContents = false;
            sizeTiles.Padding = new Padding(0);
            sizeTiles.BackColor = Color.Transparent;
            host.Controls.Add(sizeTiles);

            AddHangingSignSizeTile(sizeTiles, "Small Hanging Sign", "hanging_sign_size_small.png", "Small");
            AddHangingSignSizeTile(sizeTiles, "Large Hanging Sign", "hanging_sign_size_large.png", "Large");

            if (_hangingSignSizeTiles.ContainsKey("Small Hanging Sign"))
                TwoPointTheme.StyleSelectableButton(_hangingSignSizeTiles["Small Hanging Sign"], true);
        }

        private void AddHangingSignSizeTile(FlowLayoutPanel parent, string templateKey, string imageFile, string tooltipText)
        {
            Button button = new Button();
            button.Text = string.Empty;
            button.Tag = templateKey;
            button.Size = new Size(54, 54);
            button.Margin = new Padding(4, 2, 4, 2);
            button.Image = CreateHangingSignSizeTileIcon(TwoPointTheme.LoadThemeImage(imageFile));
            button.ImageAlign = ContentAlignment.MiddleCenter;
            button.Padding = new Padding(0);
            button.AccessibleName = templateKey;
            button.Click += delegate
            {
                if (_syncingGraphicalTemplateControls)
                    return;
                foreach (KeyValuePair<string, Button> pair in _hangingSignSizeTiles)
                    TwoPointTheme.StyleSelectableButton(pair.Value, string.Equals(pair.Key, templateKey, StringComparison.OrdinalIgnoreCase));
                SelectHangingSignTemplate(templateKey);
            };
            TwoPointTheme.StyleSelectableButton(button, false);
            parent.Controls.Add(button);
            _hangingSignSizeTiles[templateKey] = button;
            SetUiTip(button, tooltipText);
        }

        private void BuildWallSignOptions(Panel body)
        {
            Panel host = new Panel();
            _wallSignOptionsContentPanel = host;
            host.Name = "CreateModWallSignOptionsContent";
            host.Dock = DockStyle.Fill;
            host.BackColor = Color.Transparent;
            host.Visible = false;
            body.Controls.Add(host);

            Label sizeLabel = NewCreateModLabel("Size");
            sizeLabel.Location = new Point(10, 3);
            host.Controls.Add(sizeLabel);

            FlowLayoutPanel sizeTiles = new FlowLayoutPanel();
            sizeTiles.Name = "CreateModWallSignSizeTilesFlow";
            sizeTiles.Location = new Point(6, 27);
            sizeTiles.Size = new Size(Math.Max(220, host.ClientSize.Width - 12), 62);
            sizeTiles.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            sizeTiles.FlowDirection = FlowDirection.LeftToRight;
            sizeTiles.WrapContents = false;
            sizeTiles.Padding = new Padding(0);
            sizeTiles.BackColor = Color.Transparent;
            host.Controls.Add(sizeTiles);

            AddWallSignSizeTile(sizeTiles, "Small Wall Sign", "wall_sign_size_small.png", "Small");
            AddWallSignSizeTile(sizeTiles, "Large Wall Sign", "wall_sign_size_large.png", "Large");

            if (_wallSignSizeTiles.ContainsKey("Small Wall Sign"))
                TwoPointTheme.StyleSelectableButton(_wallSignSizeTiles["Small Wall Sign"], true);
        }

        private void AddWallSignSizeTile(FlowLayoutPanel parent, string templateKey, string imageFile, string tooltipText)
        {
            Button button = new Button();
            button.Text = string.Empty;
            button.Tag = templateKey;
            button.Size = new Size(54, 54);
            button.Margin = new Padding(4, 2, 4, 2);
            // Preserve the complete 80x80 source canvas exactly like the accepted
            // Hanging Sign/Rug option-icon behaviour: no trimming, centred.
            button.Image = CreateHangingSignSizeTileIcon(TwoPointTheme.LoadThemeImage(imageFile));
            button.ImageAlign = ContentAlignment.MiddleCenter;
            button.Padding = new Padding(0);
            button.AccessibleName = templateKey;
            button.Click += delegate
            {
                if (_syncingGraphicalTemplateControls)
                    return;
                foreach (KeyValuePair<string, Button> pair in _wallSignSizeTiles)
                    TwoPointTheme.StyleSelectableButton(pair.Value, string.Equals(pair.Key, templateKey, StringComparison.OrdinalIgnoreCase));
                SelectWallSignTemplate(templateKey);
            };
            TwoPointTheme.StyleSelectableButton(button, false);
            parent.Controls.Add(button);
            _wallSignSizeTiles[templateKey] = button;
            SetUiTip(button, tooltipText);
        }

        private void BuildRugOptions(Panel body)
        {
            Panel host = new Panel();
            _rugOptionsContentPanel = host;
            host.Name = "CreateModRugOptionsContent";
            host.Dock = DockStyle.Fill;
            host.BackColor = Color.Transparent;
            body.Controls.Add(host);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 2;
            layout.RowCount = 1;
            layout.Padding = new Padding(0);
            layout.Margin = new Padding(0);
            // Shape is the primary rug choice, so keep it first and give it the flexible width.
            // Recolourable is the smaller secondary option aligned on the right.
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118F));
            host.Controls.Add(layout);

            Panel shapesPanel = new Panel();
            shapesPanel.Dock = DockStyle.Fill;
            shapesPanel.Margin = new Padding(0);
            shapesPanel.BackColor = Color.Transparent;
            layout.Controls.Add(shapesPanel, 0, 0);

            Label shapeLabel = NewCreateModLabel("Shape");
            shapeLabel.Location = new Point(10, 3);
            shapesPanel.Controls.Add(shapeLabel);

            FlowLayoutPanel shapeTiles = new FlowLayoutPanel();
            _shapeTilesFlow = shapeTiles;
            shapeTiles.Name = "CreateModShapeTilesFlow";
            shapeTiles.Location = new Point(6, 27);
            shapeTiles.Size = new Size(Math.Max(220, shapesPanel.ClientSize.Width - 12), 62);
            shapeTiles.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            shapeTiles.FlowDirection = FlowDirection.LeftToRight;
            shapeTiles.WrapContents = false;
            shapeTiles.Padding = new Padding(0);
            shapeTiles.BackColor = Color.Transparent;
            shapesPanel.Controls.Add(shapeTiles);

            AddRugShapeTile(shapeTiles, "Square");
            AddRugShapeTile(shapeTiles, "Rectangle");
            AddRugShapeTile(shapeTiles, "Circle");
            AddRugShapeTile(shapeTiles, "Octagon");

            Panel recolourPanel = new Panel();
            recolourPanel.Dock = DockStyle.Fill;
            recolourPanel.Margin = new Padding(0);
            recolourPanel.BackColor = Color.Transparent;
            layout.Controls.Add(recolourPanel, 1, 0);

            Label recolourLabel = NewCreateModLabel("Recolourable");
            recolourLabel.Location = new Point(10, 3);
            recolourPanel.Controls.Add(recolourLabel);

            _recolourToggle = new TwoPointToggle();
            _recolourToggle.Location = new Point(10, 29);
            _recolourToggle.Size = new Size(78, 30);
            _recolourToggle.CheckedChanged += delegate
            {
                if (_syncingGraphicalTemplateControls)
                    return;
                StyleRecolourToggle();
                SelectRugTemplate();
            };
            recolourPanel.Controls.Add(_recolourToggle);
        }


        private void BuildBannerOptions(Panel body)
        {
            Panel host = new Panel();
            _bannerOptionsContentPanel = host;
            host.Name = "CreateModBannerOptionsContent";
            host.Dock = DockStyle.Fill;
            host.BackColor = Color.Transparent;
            host.Visible = false;
            body.Controls.Add(host);

            Label themeLabel = NewCreateModLabel("Theme");
            themeLabel.Location = new Point(10, 3);
            host.Controls.Add(themeLabel);

            Panel themeViewport = new Panel();
            _bannerTilesViewport = themeViewport;
            themeViewport.Name = "CreateModBannerThemeTilesViewport";
            themeViewport.Location = new Point(6, 27);
            themeViewport.Size = new Size(Math.Max(220, host.ClientSize.Width - 12), 76);
            themeViewport.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            themeViewport.BackColor = Color.Transparent;
            host.Controls.Add(themeViewport);

            FlowLayoutPanel themeTiles = new FlowLayoutPanel();
            _bannerTilesFlow = themeTiles;
            themeTiles.Name = "CreateModBannerThemeTilesFlow";
            themeTiles.AutoSize = true;
            themeTiles.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            themeTiles.Location = new Point(0, 0);
            themeTiles.FlowDirection = FlowDirection.LeftToRight;
            themeTiles.WrapContents = false;
            themeTiles.Padding = new Padding(0);
            themeTiles.BackColor = Color.Transparent;
            themeViewport.Controls.Add(themeTiles);

            _bannerTilesScrollBar = new TwoPointHorizontalScrollBar();
            _bannerTilesScrollBar.Dock = DockStyle.Bottom;
            _bannerTilesScrollBar.Height = 14;
            _bannerTilesScrollBar.Visible = false;
            themeViewport.Controls.Add(_bannerTilesScrollBar);
            _bannerTilesScrollBar.BringToFront();

            AddBannerThemeTile(themeTiles, "Prehistory Banner", "banner_01_Prehistory.png");
            AddBannerThemeTile(themeTiles, "Botany Banner", "banner_02_Botany.png");
            AddBannerThemeTile(themeTiles, "Marine Life Banner", "banner_03_Marine.png");
            AddBannerThemeTile(themeTiles, "Supernatural Banner", "banner_04_Supernatural.png");
            AddBannerThemeTile(themeTiles, "Space Banner", "banner_05_Space.png");
            AddBannerThemeTile(themeTiles, "Science Banner", "banner_06_Science.png");
            AddBannerThemeTile(themeTiles, "Fantasy Banner", "banner_07_Fantasy.png");
            AddBannerThemeTile(themeTiles, "Digiverse Banner", "banner_08_Digiverse.png");
            AddBannerThemeTile(themeTiles, "Wildlife Banner", "banner_09_Wildlife.png");
            AddBannerThemeTile(themeTiles, "Art Banner", "banner_10_Art.png");
            AddBannerThemeTile(themeTiles, "General Banner", "banner_99_General.png");

            if (_bannerThemeTiles.ContainsKey("Prehistory Banner"))
                TwoPointTheme.StyleSelectableButton(_bannerThemeTiles["Prehistory Banner"], true);

            themeViewport.Resize += delegate { SyncBannerThemeScrollBar(); };
            themeTiles.SizeChanged += delegate { SyncBannerThemeScrollBar(); };
            _bannerTilesScrollBar.ValueChanged += delegate
            {
                themeTiles.Left = -_bannerTilesScrollBar.Value;
            };
            MouseEventHandler wheel = delegate(object sender, MouseEventArgs e)
            {
                if (_bannerTilesScrollBar == null || !_bannerTilesScrollBar.Visible)
                    return;
                int delta = e.Delta > 0 ? -45 : 45;
                _bannerTilesScrollBar.Value = Math.Max(0, Math.Min(_bannerTilesScrollBar.Maximum, _bannerTilesScrollBar.Value + delta));
                themeTiles.Left = -_bannerTilesScrollBar.Value;
            };
            themeTiles.MouseWheel += wheel;
            themeViewport.MouseWheel += wheel;
            SyncBannerThemeScrollBar();
        }

        private void BuildDoubleBannerOptions(Panel body)
        {
            Panel host = new Panel();
            _doubleBannerOptionsContentPanel = host;
            host.Name = "CreateModDoubleBannerOptionsContent";
            host.Dock = DockStyle.Fill;
            host.BackColor = Color.Transparent;
            host.Visible = false;
            body.Controls.Add(host);

            Label themeLabel = NewCreateModLabel("Theme");
            themeLabel.Location = new Point(10, 3);
            host.Controls.Add(themeLabel);

            Panel themeViewport = new Panel();
            _doubleBannerTilesViewport = themeViewport;
            themeViewport.Name = "CreateModDoubleBannerThemeTilesViewport";
            themeViewport.Location = new Point(6, 27);
            themeViewport.Size = new Size(Math.Max(220, host.ClientSize.Width - 12), 76);
            themeViewport.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            themeViewport.BackColor = Color.Transparent;
            host.Controls.Add(themeViewport);

            FlowLayoutPanel themeTiles = new FlowLayoutPanel();
            _doubleBannerTilesFlow = themeTiles;
            themeTiles.Name = "CreateModDoubleBannerThemeTilesFlow";
            themeTiles.AutoSize = true;
            themeTiles.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            themeTiles.Location = new Point(0, 0);
            themeTiles.FlowDirection = FlowDirection.LeftToRight;
            themeTiles.WrapContents = false;
            themeTiles.Padding = new Padding(0);
            themeTiles.BackColor = Color.Transparent;
            themeViewport.Controls.Add(themeTiles);

            _doubleBannerTilesScrollBar = new TwoPointHorizontalScrollBar();
            _doubleBannerTilesScrollBar.Dock = DockStyle.Bottom;
            _doubleBannerTilesScrollBar.Height = 14;
            _doubleBannerTilesScrollBar.Visible = false;
            themeViewport.Controls.Add(_doubleBannerTilesScrollBar);
            _doubleBannerTilesScrollBar.BringToFront();

            AddDoubleBannerThemeTile(themeTiles, "Prehistory Double Banner", "banner_01_Prehistory.png");
            AddDoubleBannerThemeTile(themeTiles, "Botany Double Banner", "banner_02_Botany.png");
            AddDoubleBannerThemeTile(themeTiles, "Marine Life Double Banner", "banner_03_Marine.png");
            AddDoubleBannerThemeTile(themeTiles, "Supernatural Double Banner", "banner_04_Supernatural.png");
            AddDoubleBannerThemeTile(themeTiles, "Space Double Banner", "banner_05_Space.png");
            AddDoubleBannerThemeTile(themeTiles, "Science Double Banner", "banner_06_Science.png");
            AddDoubleBannerThemeTile(themeTiles, "Fantasy Double Banner", "banner_07_Fantasy.png");
            AddDoubleBannerThemeTile(themeTiles, "Wildlife Double Banner", "banner_09_Wildlife.png");
            AddDoubleBannerThemeTile(themeTiles, "Art Double Banner", "banner_10_Art.png");
            AddDoubleBannerThemeTile(themeTiles, "General Double Banner", "banner_99_General.png");

            if (_doubleBannerThemeTiles.ContainsKey("Prehistory Double Banner"))
                TwoPointTheme.StyleSelectableButton(_doubleBannerThemeTiles["Prehistory Double Banner"], true);

            themeViewport.Resize += delegate { SyncDoubleBannerThemeScrollBar(); };
            themeTiles.SizeChanged += delegate { SyncDoubleBannerThemeScrollBar(); };
            _doubleBannerTilesScrollBar.ValueChanged += delegate { themeTiles.Left = -_doubleBannerTilesScrollBar.Value; };
            MouseEventHandler wheel = delegate(object sender, MouseEventArgs e)
            {
                if (_doubleBannerTilesScrollBar == null || !_doubleBannerTilesScrollBar.Visible)
                    return;
                int delta = e.Delta > 0 ? -45 : 45;
                _doubleBannerTilesScrollBar.Value = Math.Max(0, Math.Min(_doubleBannerTilesScrollBar.Maximum, _doubleBannerTilesScrollBar.Value + delta));
                themeTiles.Left = -_doubleBannerTilesScrollBar.Value;
            };
            themeTiles.MouseWheel += wheel;
            themeViewport.MouseWheel += wheel;
            SyncDoubleBannerThemeScrollBar();
        }

        private void SyncDoubleBannerThemeScrollBar()
        {
            if (_doubleBannerTilesViewport == null || _doubleBannerTilesFlow == null || _doubleBannerTilesScrollBar == null)
                return;

            int contentWidth = Math.Max(_doubleBannerTilesFlow.PreferredSize.Width, _doubleBannerTilesFlow.Width);
            int viewportWidth = Math.Max(1, _doubleBannerTilesViewport.ClientSize.Width);
            int maximum = Math.Max(0, contentWidth - viewportWidth);
            _doubleBannerTilesScrollBar.Maximum = maximum;
            _doubleBannerTilesScrollBar.LargeChange = viewportWidth;
            _doubleBannerTilesScrollBar.Visible = maximum > 0;
            if (maximum <= 0)
            {
                _doubleBannerTilesScrollBar.Value = 0;
                _doubleBannerTilesFlow.Left = 0;
            }
            else
            {
                _doubleBannerTilesScrollBar.Value = Math.Min(_doubleBannerTilesScrollBar.Value, maximum);
                _doubleBannerTilesFlow.Left = -_doubleBannerTilesScrollBar.Value;
            }
        }

        private void AddDoubleBannerThemeTile(FlowLayoutPanel parent, string templateKey, string imageFile)
        {
            Button button = new Button();
            button.Text = string.Empty;
            button.Tag = templateKey;
            button.Size = new Size(54, 54);
            button.Margin = new Padding(4, 2, 4, 2);
            button.Image = CreateCenteredTileIcon(TwoPointTheme.LoadThemeImage(imageFile), 40);
            button.ImageAlign = ContentAlignment.MiddleCenter;
            button.Padding = new Padding(0);
            button.AccessibleName = templateKey;
            button.Click += delegate
            {
                if (_syncingGraphicalTemplateControls)
                    return;
                foreach (KeyValuePair<string, Button> pair in _doubleBannerThemeTiles)
                    TwoPointTheme.StyleSelectableButton(pair.Value, string.Equals(pair.Key, templateKey, StringComparison.OrdinalIgnoreCase));
                SelectDoubleBannerTemplate(templateKey);
            };
            TwoPointTheme.StyleSelectableButton(button, false);
            parent.Controls.Add(button);
            _doubleBannerThemeTiles[templateKey] = button;
            string tip = templateKey.EndsWith(" Double Banner", StringComparison.OrdinalIgnoreCase)
                ? templateKey.Substring(0, templateKey.Length - " Double Banner".Length)
                : templateKey;
            SetUiTip(button, tip);
        }

        private void SyncBannerThemeScrollBar()
        {
            if (_bannerTilesViewport == null || _bannerTilesFlow == null || _bannerTilesScrollBar == null)
                return;

            int contentWidth = Math.Max(_bannerTilesFlow.PreferredSize.Width, _bannerTilesFlow.Width);
            int viewportWidth = Math.Max(1, _bannerTilesViewport.ClientSize.Width);
            int maximum = Math.Max(0, contentWidth - viewportWidth);
            _bannerTilesScrollBar.Maximum = maximum;
            _bannerTilesScrollBar.LargeChange = viewportWidth;
            _bannerTilesScrollBar.Visible = maximum > 0;
            if (maximum <= 0)
            {
                _bannerTilesScrollBar.Value = 0;
                _bannerTilesFlow.Left = 0;
            }
            else
            {
                _bannerTilesScrollBar.Value = Math.Min(_bannerTilesScrollBar.Value, maximum);
                _bannerTilesFlow.Left = -_bannerTilesScrollBar.Value;
            }
        }

        private void AddBannerThemeTile(FlowLayoutPanel parent, string templateKey, string imageFile)
        {
            Button button = new Button();
            button.Text = string.Empty;
            button.Tag = templateKey;
            button.Size = new Size(54, 54);
            button.Margin = new Padding(4, 2, 4, 2);
            button.Image = CreateCenteredTileIcon(TwoPointTheme.LoadThemeImage(imageFile), 40);
            button.ImageAlign = ContentAlignment.MiddleCenter;
            button.Padding = new Padding(0);
            button.AccessibleName = templateKey;
            button.Click += delegate
            {
                if (_syncingGraphicalTemplateControls)
                    return;
                foreach (KeyValuePair<string, Button> pair in _bannerThemeTiles)
                    TwoPointTheme.StyleSelectableButton(pair.Value, string.Equals(pair.Key, templateKey, StringComparison.OrdinalIgnoreCase));
                SelectBannerTemplate(templateKey);
            };
            TwoPointTheme.StyleSelectableButton(button, false);
            parent.Controls.Add(button);
            _bannerThemeTiles[templateKey] = button;
            string bannerTip = templateKey.EndsWith(" Banner", StringComparison.OrdinalIgnoreCase)
                ? templateKey.Substring(0, templateKey.Length - " Banner".Length)
                : templateKey;
            SetUiTip(button, bannerTip);
        }

        private void AddRugShapeTile(FlowLayoutPanel parent, string shape)
        {
            Button button = new Button();
            button.Text = string.Empty;
            button.Tag = shape;
            button.Size = new Size(54, 54);
            button.Margin = new Padding(4, 2, 4, 2);
            button.Image = CreateRugShapeTileIcon(CreateRugShapeImage(shape));
            button.ImageAlign = ContentAlignment.MiddleCenter;
            button.Click += delegate
            {
                if (_syncingGraphicalTemplateControls)
                    return;
                foreach (KeyValuePair<string, Button> pair in _rugShapeTiles)
                    TwoPointTheme.StyleSelectableButton(pair.Value, string.Equals(pair.Key, shape, StringComparison.OrdinalIgnoreCase));
                SelectRugTemplate();
            };
            TwoPointTheme.StyleSelectableButton(button, false);
            button.Padding = new Padding(0);
            parent.Controls.Add(button);
            _rugShapeTiles[shape] = button;
            SetUiTip(button, shape);
        }

        private Image CreateRugShapeImage(string shape)
        {
            string iconFile = GetRugShapeIconFileName(shape);
            if (!string.IsNullOrEmpty(iconFile))
            {
                Image themed = TwoPointTheme.LoadThemeImage(iconFile);
                if (themed != null)
                    return themed;
            }

            Bitmap image = new Bitmap(46, 46);
            using (Graphics g = Graphics.FromImage(image))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (Brush brush = new SolidBrush(Color.FromArgb(78, 69, 54)))
                {
                    if (shape == "Square")
                        g.FillRectangle(brush, 9, 9, 28, 28);
                    else if (shape == "Rectangle")
                        g.FillRectangle(brush, 5, 13, 36, 20);
                    else if (shape == "Circle")
                        g.FillEllipse(brush, 8, 8, 30, 30);
                    else
                    {
                        Point[] p = new Point[]
                        {
                            new Point(15, 5), new Point(31, 5), new Point(41, 15), new Point(41, 31),
                            new Point(31, 41), new Point(15, 41), new Point(5, 31), new Point(5, 15)
                        };
                        g.FillPolygon(brush, p);
                    }
                }
            }
            return image;
        }

        private static string GetRugShapeIconFileName(string shape)
        {
            if (string.Equals(shape, "Circle", StringComparison.OrdinalIgnoreCase))
                return "rug_shape_circle.png";
            if (string.Equals(shape, "Square", StringComparison.OrdinalIgnoreCase))
                return "rug_shape_square.png";
            if (string.Equals(shape, "Rectangle", StringComparison.OrdinalIgnoreCase))
                return "rug_shape_rectangle.png";
            if (string.Equals(shape, "Octagon", StringComparison.OrdinalIgnoreCase))
                return "rug_shape_octagon.png";
            return null;
        }

        private void BuildCreateModDetails(Panel body)
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 2;
            layout.RowCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
            body.Controls.Add(layout);

            TableLayoutPanel textFields = new TableLayoutPanel();
            textFields.Name = "CreateModDetailsTextFields";
            textFields.Dock = DockStyle.Fill;
            textFields.Padding = new Padding(8, 3, 8, 4);
            textFields.ColumnCount = 1;
            textFields.RowCount = 4;
            textFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 23F));
            textFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            textFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 23F));
            textFields.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.Controls.Add(textFields, 0, 0);

            textFields.Controls.Add(NewCreateModLabel("Name"), 0, 0);
            _modName.Dock = DockStyle.Fill;
            _modName.Margin = new Padding(0, 0, 0, 5);
            textFields.Controls.Add(_modName, 0, 1);
            textFields.Controls.Add(NewCreateModLabel("Description"), 0, 2);
            _description.Dock = DockStyle.Fill;
            _description.Margin = new Padding(0);
            textFields.Controls.Add(_description, 0, 3);

            TableLayoutPanel rightFields = new TableLayoutPanel();
            _detailsRightFields = rightFields;
            rightFields.Name = "CreateModDetailsRightFields";
            rightFields.Dock = DockStyle.Fill;
            rightFields.Padding = new Padding(8, 2, 8, 3);
            rightFields.ColumnCount = 1;
            rightFields.RowCount = 3;
            rightFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
            rightFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
            rightFields.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.Controls.Add(rightFields, 1, 0);

            BuildMoneyControl(rightFields, 0, "Cost", _cost, true);
            BuildMoneyControl(rightFields, 1, "Kudosh", _kudosh, false);
            BuildIconOptions(rightFields, 2);
        }

        private void BuildIconOptions(TableLayoutPanel parent, int row)
        {
            Panel holder = new Panel();
            holder.Dock = DockStyle.Fill;
            holder.BackColor = TwoPointTheme.PanelLight;
            parent.Controls.Add(holder, 0, row);

            Label previewLabel = NewCreateModLabel("Icon Preview");
            previewLabel.Location = new Point(0, 2);
            holder.Controls.Add(previewLabel);

            _iconPreview.Parent = holder;
            _iconPreview.Location = new Point(0, 24);
            _iconPreview.Size = new Size(96, 96);
            _iconPreview.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            _iconPreview.SizeMode = PictureBoxSizeMode.Zoom;
            _iconPreview.BackColor = TwoPointTheme.PanelLight;

            Label sourceLabel = NewCreateModLabel("Source");
            sourceLabel.Location = new Point(110, 8);
            holder.Controls.Add(sourceLabel);

            _iconMode.Parent = holder;
            _iconMode.Location = new Point(110, 30);
            _iconMode.Size = new Size(145, 27);
            _iconMode.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            // Custom path only appears when Custom is selected. Keep it aligned beneath
            // the compact source selector rather than consuming the whole Details width.
            _iconPath.Parent = holder;
            _iconPath.Location = new Point(110, 68);
            _iconPath.Size = new Size(175, 25);
            _iconPath.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            _iconBrowse.Parent = holder;
            _iconBrowse.Text = string.Empty;
            _iconBrowse.Location = new Point(292, 66);
            _iconBrowse.Size = new Size(38, 28);
            _iconBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            _iconBrowse.Image = TwoPointTheme.LoadThemeImage("browse_folder.png");
            _iconBrowse.ImageAlign = ContentAlignment.MiddleCenter;
            _iconBrowse.Padding = new Padding(0);
            _iconBrowse.AccessibleName = "Browse for custom icon";
            TwoPointTheme.StyleButton(_iconBrowse);

            _variantOptionsLabel = NewCreateModLabel("Variant Options");
            _variantOptionsLabel.Location = new Point(0, 132);
            holder.Controls.Add(_variantOptionsLabel);

            _variantParentBox = new VariantParentComboBox();
            _variantParentBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _variantParentBox.Location = new Point(0, 153);
            _variantParentBox.Size = new Size(Math.Max(220, holder.ClientSize.Width - 6), 27);
            _variantParentBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _variantParentBox.BackColor = TwoPointTheme.FieldBackground;
            _variantParentBox.ForeColor = TwoPointTheme.BodyText;
            _variantParentBox.Font = TwoPointTheme.BodyFont(8.8F);
            _variantParentBox.SelectedIndexChanged += delegate
            {
                if (_loadingVariantChoice)
                    return;
                ApplyCreateVariantRules();
            };
            holder.Controls.Add(_variantParentBox);

            _variantHelpLabel = new Label();
            _variantHelpLabel.Location = new Point(0, 183);
            _variantHelpLabel.Size = new Size(Math.Max(220, holder.ClientSize.Width - 6), 34);
            _variantHelpLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _variantHelpLabel.ForeColor = Color.FromArgb(108, 99, 84);
            _variantHelpLabel.Font = TwoPointTheme.BodyFont(7.6F);
            _variantHelpLabel.AutoEllipsis = true;
            holder.Controls.Add(_variantHelpLabel);

            _decorPackOptionsLabel = NewCreateModLabel("Décor Pack Options");
            _decorPackOptionsLabel.Location = new Point(0, 132);
            holder.Controls.Add(_decorPackOptionsLabel);

            _decorPackBox = new ComboBox();
            _decorPackBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _decorPackBox.Location = new Point(0, 153);
            _decorPackBox.Size = new Size(Math.Max(220, holder.ClientSize.Width - 6), 27);
            _decorPackBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _decorPackBox.BackColor = TwoPointTheme.FieldBackground;
            _decorPackBox.ForeColor = TwoPointTheme.BodyText;
            _decorPackBox.Font = TwoPointTheme.BodyFont(8.8F);
            _decorPackBox.SelectedIndexChanged += delegate
            {
                if (_loadingDecorPackChoice)
                    return;
                ApplyCreateDecorPackRules();
            };
            holder.Controls.Add(_decorPackBox);

            _decorPackHelpLabel = new Label();
            _decorPackHelpLabel.Location = new Point(0, 183);
            _decorPackHelpLabel.Size = new Size(Math.Max(220, holder.ClientSize.Width - 6), 40);
            _decorPackHelpLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _decorPackHelpLabel.ForeColor = Color.FromArgb(108, 99, 84);
            _decorPackHelpLabel.Font = TwoPointTheme.BodyFont(7.6F);
            _decorPackHelpLabel.AutoEllipsis = true;
            holder.Controls.Add(_decorPackHelpLabel);

            RefreshCreateVariantChoices();
            RefreshCreateDecorPackChoices();
        }

        private void RefreshCreateVariantChoices()
        {
            if (_variantParentBox == null)
                return;

            TemplateDefinition template = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            if (template == null)
                return;

            bool supportsVariants = !IsWallpaperTemplate(template);
            if (_variantOptionsLabel != null) _variantOptionsLabel.Visible = supportsVariants;
            if (_variantParentBox != null) _variantParentBox.Visible = supportsVariants;
            if (_variantHelpLabel != null) _variantHelpLabel.Visible = supportsVariants;
            if (!supportsVariants)
            {
                _loadingVariantChoice = true;
                try
                {
                    _variantParentBox.Items.Clear();
                    _variantParentBox.Items.Add(new VariantParentChoice { Mode = VariantModes.Standalone, Name = "Standalone" });
                    _variantParentBox.SelectedIndex = 0;
                    _variantParentBox.Enabled = false;
                }
                finally
                {
                    _loadingVariantChoice = false;
                }
                ApplyCreateVariantRules();
                return;
            }

            string desiredMode = _activeProject == null
                ? VariantModes.Standalone
                : VariantModes.Normalise(_activeProject.VariantMode);
            string desiredParentId = _activeProject == null ? "" : (_activeProject.VariantParentModId ?? "");
            List<ModProjectRecord> children = _activeProject == null
                ? new List<ModProjectRecord>()
                : _projectLibrary.FindChildren(_activeProject.ModId);
            bool lockedAsParent = children.Count > 0;
            if (lockedAsParent)
            {
                desiredMode = VariantModes.Standalone;
                desiredParentId = "";
            }

            _loadingVariantChoice = true;
            try
            {
                _variantParentBox.Items.Clear();
                _variantParentBox.Items.Add(new VariantParentChoice
                {
                    Mode = VariantModes.Standalone,
                    Name = "Standalone Item / Parent Item"
                });
                _variantParentBox.Items.Add(new VariantParentChoice
                {
                    Mode = VariantModes.BaseGame,
                    Name = template.BaseItemName ?? template.DisplayName ?? template.Key
                });
                _variantParentBox.Items.Add(new VariantParentChoice
                {
                    IsSeparator = true
                });

                List<ModProjectRecord> parents = _projectLibrary.GetEligibleVariantParents(_activeProject == null ? "" : _activeProject.ModId);
                bool desiredParentFound = false;
                for (int i = 0; i < parents.Count; i++)
                {
                    ModProjectRecord parent = parents[i];
                    if (parent == null)
                        continue;
                    VariantParentChoice choice = new VariantParentChoice
                    {
                        Mode = VariantModes.Modded,
                        ModId = parent.ModId,
                        Name = string.IsNullOrEmpty(parent.Name) ? parent.ModId : parent.Name,
                        ItemType = GetVariantItemType(parent.Template)
                    };
                    _variantParentBox.Items.Add(choice);
                    if (!string.IsNullOrEmpty(desiredParentId) &&
                        string.Equals(parent.ModId, desiredParentId, StringComparison.Ordinal))
                        desiredParentFound = true;
                }

                // Keep an invalid/missing current relationship visible so the user can see
                // exactly what needs fixing rather than silently changing the saved project.
                if (desiredMode == VariantModes.Modded && !desiredParentFound && !string.IsNullOrEmpty(desiredParentId))
                {
                    ModProjectRecord missingParent = _projectLibrary.Load(desiredParentId);
                    _variantParentBox.Items.Add(new VariantParentChoice
                    {
                        Mode = VariantModes.Modded,
                        ModId = desiredParentId,
                        Name = missingParent == null ? "Missing parent - " + desiredParentId :
                            (string.IsNullOrEmpty(missingParent.Name) ? desiredParentId : missingParent.Name),
                        ItemType = missingParent == null ? "" : GetVariantItemType(missingParent.Template)
                    });
                }

                int selected = -1;
                for (int i = 0; i < _variantParentBox.Items.Count; i++)
                {
                    VariantParentChoice choice = _variantParentBox.Items[i] as VariantParentChoice;
                    if (choice == null || choice.IsSeparator)
                        continue;
                    if (VariantModes.Normalise(choice.Mode) != desiredMode)
                        continue;
                    if (desiredMode == VariantModes.Modded &&
                        !string.Equals(choice.ModId ?? "", desiredParentId, StringComparison.Ordinal))
                        continue;
                    selected = i;
                    break;
                }

                if (selected < 0)
                    selected = 0;
                _variantParentBox.SelectedIndex = selected;
                _variantParentBox.Enabled = !lockedAsParent;
            }
            finally
            {
                _loadingVariantChoice = false;
            }

            ApplyCreateVariantRules();

            if (lockedAsParent && _variantHelpLabel != null)
            {
                _variantHelpLabel.Text = "Parent of " + children.Count + " variant" + (children.Count == 1 ? "" : "s") +
                    ". A parent item cannot itself be a child.";
            }
        }

        private void RestoreCreateVariantChoice(string mode, string modId)
        {
            if (_variantParentBox == null)
                return;

            string wantedMode = VariantModes.Normalise(mode);
            string wantedModId = modId ?? "";
            _loadingVariantChoice = true;
            try
            {
                for (int i = 0; i < _variantParentBox.Items.Count; i++)
                {
                    VariantParentChoice choice = _variantParentBox.Items[i] as VariantParentChoice;
                    if (choice == null || choice.IsSeparator || VariantModes.Normalise(choice.Mode) != wantedMode)
                        continue;
                    if (wantedMode == VariantModes.Modded &&
                        !string.Equals(choice.ModId ?? "", wantedModId, StringComparison.Ordinal))
                        continue;
                    _variantParentBox.SelectedIndex = i;
                    break;
                }
            }
            finally
            {
                _loadingVariantChoice = false;
            }
            ApplyCreateVariantRules();
        }

        private VariantParentChoice GetSelectedCreateVariantChoice()
        {
            return _variantParentBox == null ? null : _variantParentBox.SelectedItem as VariantParentChoice;
        }

        private DecorPackChoice GetSelectedCreateDecorPackChoice()
        {
            return _decorPackBox == null ? null : _decorPackBox.SelectedItem as DecorPackChoice;
        }

        private void RefreshCreateDecorPackChoices()
        {
            if (_decorPackBox == null)
                return;

            TemplateDefinition template = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            bool wallpaper = IsWallpaperTemplate(template);
            if (_decorPackOptionsLabel != null) _decorPackOptionsLabel.Visible = wallpaper;
            if (_decorPackBox != null) _decorPackBox.Visible = wallpaper;
            if (_decorPackHelpLabel != null) _decorPackHelpLabel.Visible = wallpaper;
            if (!wallpaper)
                return;

            DecorPackChoice current = GetSelectedCreateDecorPackChoice();
            string desiredKey = current == null ? "" : (current.PackKey ?? "");
            bool desiredNew = current != null && current.NewPack;
            bool desiredStandalone = current != null && current.Standalone;

            // When an existing Wallpaper project is opened, its current built pack is the
            // default choice unless the user has already deliberately selected something else.
            if (!desiredStandalone && string.IsNullOrEmpty(desiredKey) && !desiredNew && _activeProject != null &&
                BuildPackageModes.Normalise(_activeProject.LastBuildPackageMode) == BuildPackageModes.DecorPack &&
                !string.IsNullOrEmpty(_activeProject.LastBuiltFamilyKey))
            {
                desiredKey = _activeProject.LastBuiltFamilyKey;
            }

            Dictionary<string, DecorPackChoice> packs = new Dictionary<string, DecorPackChoice>(StringComparer.Ordinal);
            List<ModProjectRecord> records = _projectLibrary.LoadAll();
            for (int i = 0; i < records.Count; i++)
            {
                ModProjectRecord record = records[i];
                if (record == null || !string.Equals(record.Template ?? "", "Wallpaper", StringComparison.OrdinalIgnoreCase) ||
                    BuildPackageModes.Normalise(record.LastBuildPackageMode) != BuildPackageModes.DecorPack ||
                    string.IsNullOrEmpty(record.LastBuiltFamilyKey))
                    continue;

                DecorPackChoice pack;
                if (!packs.TryGetValue(record.LastBuiltFamilyKey, out pack))
                {
                    pack = new DecorPackChoice();
                    pack.PackKey = record.LastBuiltFamilyKey;
                    pack.PackName = string.IsNullOrWhiteSpace(record.LastBuiltFamilyName) ? "Wallpaper Pack" : record.LastBuiltFamilyName.Trim();
                    packs[pack.PackKey] = pack;
                }
                pack.MemberCount++;
            }

            List<DecorPackChoice> orderedPacks = new List<DecorPackChoice>(packs.Values);
            orderedPacks.Sort(delegate(DecorPackChoice a, DecorPackChoice b)
            {
                int byName = string.Compare(a == null ? "" : a.PackName, b == null ? "" : b.PackName, StringComparison.OrdinalIgnoreCase);
                if (byName != 0) return byName;
                return string.Compare(a == null ? "" : a.PackKey, b == null ? "" : b.PackKey, StringComparison.Ordinal);
            });

            _loadingDecorPackChoice = true;
            try
            {
                _decorPackBox.Items.Clear();
                _decorPackBox.Items.Add(new DecorPackChoice { Standalone = true, PackName = "Standalone Wallpaper" });
                _decorPackBox.Items.Add(new DecorPackChoice { NewPack = true, PackName = "New Décor Pack" });
                for (int i = 0; i < orderedPacks.Count; i++)
                    _decorPackBox.Items.Add(orderedPacks[i]);

                int selected = 0;
                if (desiredNew)
                {
                    selected = Math.Min(1, _decorPackBox.Items.Count - 1);
                }
                else if (!string.IsNullOrEmpty(desiredKey))
                {
                    for (int i = 0; i < _decorPackBox.Items.Count; i++)
                    {
                        DecorPackChoice choice = _decorPackBox.Items[i] as DecorPackChoice;
                        if (choice != null && string.Equals(choice.PackKey ?? "", desiredKey, StringComparison.Ordinal))
                        {
                            selected = i;
                            break;
                        }
                    }
                }
                _decorPackBox.SelectedIndex = selected;
            }
            finally
            {
                _loadingDecorPackChoice = false;
            }

            ApplyCreateDecorPackRules();
        }

        private void RestoreCreateDecorPackChoice(string packKey, bool newPack)
        {
            if (_decorPackBox == null)
                return;

            _loadingDecorPackChoice = true;
            try
            {
                int selected = 0;
                for (int i = 0; i < _decorPackBox.Items.Count; i++)
                {
                    DecorPackChoice choice = _decorPackBox.Items[i] as DecorPackChoice;
                    if (choice == null)
                        continue;
                    if (newPack && choice.NewPack)
                    {
                        selected = i;
                        break;
                    }
                    if (!newPack && !string.IsNullOrEmpty(packKey) &&
                        string.Equals(choice.PackKey ?? "", packKey, StringComparison.Ordinal))
                    {
                        selected = i;
                        break;
                    }
                }
                if (_decorPackBox.Items.Count > 0)
                    _decorPackBox.SelectedIndex = selected;
            }
            finally
            {
                _loadingDecorPackChoice = false;
            }
            ApplyCreateDecorPackRules();
        }

        private void ApplyCreateDecorPackRules()
        {
            TemplateDefinition template = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            if (!IsWallpaperTemplate(template))
            {
                if (_decorPackOptionsLabel != null) _decorPackOptionsLabel.Visible = false;
                if (_decorPackBox != null) _decorPackBox.Visible = false;
                if (_decorPackHelpLabel != null) _decorPackHelpLabel.Visible = false;
                UpdateProjectStateUi();
                return;
            }

            DecorPackChoice choice = GetSelectedCreateDecorPackChoice();
            if (_decorPackHelpLabel != null)
            {
                if (choice == null || choice.Standalone)
                    _decorPackHelpLabel.Text = "Builds this Wallpaper as its own mod.";
                else if (choice.NewPack)
                    _decorPackHelpLabel.Text = "Starts a new Décor Pack. After the build, this pack stays selected so you can add the next Wallpaper immediately.";
                else
                    _decorPackHelpLabel.Text = "Adds this Wallpaper to the selected Décor Pack and rebuilds the complete shared package.";
            }
            UpdateProjectStateUi();
        }

        private bool IsCreateDecorPackSelected()
        {
            DecorPackChoice choice = GetSelectedCreateDecorPackChoice();
            return choice != null && !choice.Standalone;
        }

        private void ApplyCreateVariantRules()
        {
            RemoveDuplicateKudoshNote();
            TemplateDefinition template = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            bool wallpaper = IsWallpaperTemplate(template);
            VariantParentChoice choice = GetSelectedCreateVariantChoice();
            string mode = wallpaper ? VariantModes.Standalone : (choice == null ? VariantModes.Standalone : VariantModes.Normalise(choice.Mode));
            bool isVariant = mode != VariantModes.Standalone;

            if (isVariant && _kudosh != null && _kudosh.Value != 0)
                _kudosh.Value = 0;
            if (_kudoshHolder != null)
                _kudoshHolder.Enabled = !isVariant && !wallpaper;
            else
            {
                if (_kudosh != null)
                    _kudosh.Enabled = !isVariant && !wallpaper;
                if (_kudoshSlider != null)
                    _kudoshSlider.Enabled = !isVariant && !wallpaper;
            }

            if (_variantHelpLabel == null)
                return;

            if (mode == VariantModes.BaseGame)
            {
                _variantHelpLabel.Text = "Appears as a variant of the selected base-game item. Kudosh is fixed at 0.";
            }
            else if (mode == VariantModes.Modded)
            {
                _variantHelpLabel.Text = "Appears under this built Memento Maker mod in the in-game Variant menu. Kudosh is fixed at 0.";
            }
            else
            {
                _variantHelpLabel.Text = "Appears as its own item and can be selected as the parent of other Memento Maker variants.";
            }
        }

        private static string GetVariantItemType(string templateKey)
        {
            string key = templateKey ?? "";
            if (string.Equals(key, "Poster", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Small Poster", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Standard Poster", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Tall Poster", StringComparison.OrdinalIgnoreCase))
                return "Poster";
            if (string.Equals(key, "Wallpaper", StringComparison.OrdinalIgnoreCase))
                return "Décor";
            if (string.Equals(key, "Mural", StringComparison.OrdinalIgnoreCase))
                return "Mural";
            if (key.IndexOf("Rug", StringComparison.OrdinalIgnoreCase) >= 0)
                return key.StartsWith("Large ", StringComparison.OrdinalIgnoreCase) ? "Large Rug" : "Small Rug";
            if (key.EndsWith(" Hanging Sign", StringComparison.OrdinalIgnoreCase))
                return "Hanging Sign";
            if (key.EndsWith(" Wall Sign", StringComparison.OrdinalIgnoreCase))
                return "Wall Sign";
            if (key.EndsWith(" Double Banner", StringComparison.OrdinalIgnoreCase))
                return "Double Banner";
            if (key.EndsWith(" Banner", StringComparison.OrdinalIgnoreCase))
                return "Banner";
            return key;
        }

        private void RemoveDuplicateKudoshNote()
        {
            if (_kudoshHolder == null)
                return;

            // Keep only the normal Kudosh heading. Any other Label in this holder is
            // legacy explanatory UI from an earlier beta and must not be rendered.
            for (int i = _kudoshHolder.Controls.Count - 1; i >= 0; i--)
            {
                Label label = _kudoshHolder.Controls[i] as Label;
                if (label != null && !string.Equals(label.Text, "Kudosh", StringComparison.Ordinal))
                {
                    _kudoshHolder.Controls.RemoveAt(i);
                    label.Dispose();
                }
            }
        }

        private void BuildMoneyControl(TableLayoutPanel parent, int row, string labelText, NumericUpDown numeric, bool cash)
        {
            Panel holder = new Panel();
            holder.Dock = DockStyle.Fill;
            holder.BackColor = TwoPointTheme.PanelLight;
            parent.Controls.Add(holder, 0, row);

            Label label = NewCreateModLabel(labelText);
            label.Location = new Point(0, 0);
            holder.Controls.Add(label);

            PictureBox icon = new PictureBox();
            icon.Size = new Size(32, 32);
            icon.Location = new Point(0, 24);
            icon.SizeMode = PictureBoxSizeMode.Zoom;
            icon.BackColor = Color.Transparent;
            icon.Image = TwoPointTheme.LoadThemeImage(cash ? "cash.png" : "kudosh.png");
            holder.Controls.Add(icon);

            TwoPointSlider slider = new TwoPointSlider();
            slider.Location = new Point(38, 24);
            slider.Size = new Size(Math.Max(120, holder.ClientSize.Width - 155), 30);
            slider.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            slider.Minimum = 0;
            slider.Maximum = cash ? 5000 : 500;
            holder.Controls.Add(slider);

            numeric.Parent = holder;
            numeric.Location = new Point(Math.Max(180, holder.ClientSize.Width - 105), 25);
            numeric.Size = new Size(100, 28);
            numeric.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            numeric.BorderStyle = BorderStyle.Fixed3D;

            if (cash)
            {
                _costSlider = slider;
                _costHolder = holder;
            }
            else
            {
                _kudoshSlider = slider;
                _kudoshHolder = holder;

            }

            slider.Scroll += delegate
            {
                decimal value = slider.Value;
                if (value >= numeric.Minimum && value <= numeric.Maximum)
                    numeric.Value = value;
            };
            numeric.ValueChanged += delegate
            {
                int value = Decimal.ToInt32(numeric.Value);
                slider.Value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, value));
            };
            slider.Value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, Decimal.ToInt32(numeric.Value)));
            SetUiTip(slider, cash ? "Set the purchase cost." : "Set the in-game Kudosh unlock cost.");
            SetUiTip(numeric, cash ? "Enter the exact purchase cost." : "Enter the exact Kudosh unlock cost.");
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

        private void ApplyCreateModToolTips()
        {
            SetUiTip(_newProjectButton, "Start a new mod.");
            SetUiTip(_buildButton, "Build your mod.");
            SetUiTip(_openOutputButton, "Open the folder containing the most recently built mod.");
            SetUiTip(_modName, "The item name shown in-game.");
            SetUiTip(_description, "The description shown for the item in-game.");


            SetUiTip(_variantParentBox, "Choose whether this item is standalone, a variant of its base-game item, or a child of another built Memento Maker mod.");
            SetUiTip(_decorPackBox, "Choose whether this Wallpaper is built standalone, starts a new Décor Pack, or is added to an existing Décor Pack.");
            SetUiTip(_recolourToggle, "Switch between recolourable and non-recolourable. Recolourable works best for Greyscale textures.");



        }

        private Label NewCreateModLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.ForeColor = TwoPointTheme.PrimaryText;
            label.Font = TwoPointTheme.BoldFont(9.5F);
            label.AutoSize = true;
            return label;
        }

        private TableLayoutPanel BuildDoubleBannerArtworkPane(bool right, PictureBox preview)
        {
            TableLayoutPanel pane = new TableLayoutPanel();
            pane.Name = right ? "CreateModDoubleBannerRightPane" : "CreateModDoubleBannerLeftPane";
            pane.Dock = DockStyle.Fill;
            pane.Margin = right ? new Padding(4, 0, 0, 0) : new Padding(0, 0, 4, 0);
            pane.Padding = new Padding(0);
            pane.ColumnCount = 1;
            pane.RowCount = 2;
            pane.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            pane.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            TableLayoutPanel header = new TableLayoutPanel();
            header.Dock = DockStyle.Fill;
            header.Margin = new Padding(0);
            header.Padding = new Padding(0);
            header.ColumnCount = 3;
            header.RowCount = 1;
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88F));
            pane.Controls.Add(header, 0, 0);

            Label label = NewCreateModLabel(right ? "Right Banner" : "Left Banner");
            label.Dock = DockStyle.Fill;
            label.AutoSize = false;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Margin = new Padding(4, 0, 0, 0);
            header.Controls.Add(label, 0, 0);
            if (right)
                _doubleBannerRightTitleLabel = label;
            else
                _doubleBannerLeftTitleLabel = label;

            Button reset = new Button();
            reset.Dock = DockStyle.None;
            reset.Anchor = AnchorStyles.None;
            reset.Size = new Size(64, 27);
            reset.Margin = new Padding(2);
            ConfigureArtworkToolbarButton(reset, "Reset");
            reset.Tag = right ? "DoubleBannerRight" : "DoubleBannerLeft";
            reset.Click += delegate { ResetDoubleBannerSide(right); };
            header.Controls.Add(reset, 1, 0);

            Button browse = new Button();
            browse.Dock = DockStyle.None;
            browse.Anchor = AnchorStyles.None;
            browse.Size = new Size(82, 27);
            browse.Margin = new Padding(2);
            ConfigureArtworkToolbarButton(browse, "Browse...");
            browse.Tag = right ? "DoubleBannerRight" : "DoubleBannerLeft";
            browse.Click += delegate { BrowseDoubleBannerArtwork(right); };
            header.Controls.Add(browse, 2, 0);

            Panel frame = new Panel();
            frame.Name = right ? "CreateModDoubleBannerRightPreviewFrame" : "CreateModDoubleBannerLeftPreviewFrame";
            frame.Dock = DockStyle.Fill;
            frame.Margin = new Padding(0);
            frame.Padding = new Padding(3);
            frame.BackColor = TwoPointTheme.FieldBackground;
            frame.AllowDrop = true;
            frame.Tag = right ? "DoubleBannerRight" : "DoubleBannerLeft";
            frame.DragEnter += DoubleBannerArtwork_DragEnter;
            frame.DragDrop += DoubleBannerArtwork_DragDrop;
            pane.Controls.Add(frame, 0, 1);

            preview.Parent = frame;
            preview.Dock = DockStyle.Fill;
            preview.AllowDrop = true;
            preview.BackColor = Color.FromArgb(44, 44, 44);
            preview.Tag = right ? "DoubleBannerRight" : "DoubleBannerLeft";
            preview.DragEnter += DoubleBannerArtwork_DragEnter;
            preview.DragDrop += DoubleBannerArtwork_DragDrop;
            preview.Click += delegate { ActivateDoubleBannerSide(right, false); };

            Label dropHint = new Label();
            dropHint.Dock = DockStyle.Fill;
            dropHint.Text = right
                ? "Drop Right Banner artwork here\nor click to browse"
                : "Drop Left Banner artwork here\nor click to browse";
            dropHint.TextAlign = ContentAlignment.MiddleCenter;
            dropHint.ForeColor = Color.FromArgb(224, 213, 192);
            dropHint.BackColor = Color.FromArgb(44, 44, 44);
            dropHint.Font = TwoPointTheme.BoldFont(10F);
            dropHint.AllowDrop = true;
            dropHint.Cursor = Cursors.Hand;
            dropHint.Tag = right ? "DoubleBannerRight" : "DoubleBannerLeft";
            dropHint.DragEnter += DoubleBannerArtwork_DragEnter;
            dropHint.DragDrop += DoubleBannerArtwork_DragDrop;
            dropHint.Click += delegate { BrowseDoubleBannerArtwork(right); };
            frame.Controls.Add(dropHint);
            dropHint.BringToFront();

            if (right)
            {
                _doubleBannerRightDropHint = dropHint;
                _doubleBannerRightPreviewFrame = frame;
                _doubleBannerRightBrowseButton = browse;
                _doubleBannerRightResetButton = reset;
            }
            else
            {
                _artworkDropHint = dropHint;
                _doubleBannerLeftPreviewFrame = frame;
                _doubleBannerLeftBrowseButton = browse;
                _doubleBannerLeftResetButton = reset;
            }

            return pane;
        }

        private void BuildArtworkEditor(Panel body)
        {
            // Fix 16: use a deterministic three-band layout instead of allowing the artwork,
            // fitting controls and guide colours to compete for the same TableLayout height.
            // Top = tips/actions, middle = flexible preview, bottom = two fixed control rows.
            TableLayoutPanel layout = new TableLayoutPanel();
            _artworkLayout = layout;
            layout.Name = "CreateModArtworkLayout";
            layout.Dock = DockStyle.Fill;
            layout.Margin = new Padding(0);
            layout.Padding = new Padding(7, 7, 7, 7);
            layout.ColumnCount = 1;
            layout.RowCount = 3;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));
            body.Controls.Add(layout);

            _imageHint.Dock = DockStyle.Fill;
            _imageHint.Margin = new Padding(0);
            _imageHint.ForeColor = TwoPointTheme.BodyText;
            _imageHint.Font = TwoPointTheme.BodyFont(8.5F);
            _imageHint.TextAlign = ContentAlignment.MiddleLeft;
            _imageHint.AutoEllipsis = true;
            _imageHint.MouseEnter += delegate
            {
                _artworkTipPaused = true;
                if (_artworkTipTimer != null)
                    _artworkTipTimer.Stop();
            };
            _imageHint.MouseLeave += delegate
            {
                _artworkTipPaused = false;
                ResumeArtworkTipCycle();
            };

            // Top toolbar: keep the tip flexible but give Reset/Browse fixed, centred buttons.
            TableLayoutPanel topRow = new TableLayoutPanel();
            topRow.Name = "CreateModArtworkTopToolbar";
            topRow.Dock = DockStyle.Fill;
            topRow.Margin = new Padding(0);
            topRow.Padding = new Padding(0);
            topRow.ColumnCount = 3;
            topRow.RowCount = 1;
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76F));
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94F));
            topRow.Controls.Add(_imageHint, 0, 0);
            layout.Controls.Add(topRow, 0, 0);

            // Double Banners use two editors side-by-side in the existing artwork area.
            // Interacting with either pane makes that pane active for the shared placement controls.

            _resetViewButton.Dock = DockStyle.None;
            _resetViewButton.Anchor = AnchorStyles.None;
            _resetViewButton.Size = new Size(68, 29);
            _resetViewButton.Margin = new Padding(3);
            ConfigureArtworkToolbarButton(_resetViewButton, "Reset");
            topRow.Controls.Add(_resetViewButton, 1, 0);

            _artworkBrowseButton = new Button();
            _artworkBrowseButton.Dock = DockStyle.None;
            _artworkBrowseButton.Anchor = AnchorStyles.None;
            _artworkBrowseButton.Size = new Size(86, 29);
            _artworkBrowseButton.Margin = new Padding(3);
            _artworkBrowseButton.Click += delegate { BrowseArtwork(); };
            ConfigureArtworkToolbarButton(_artworkBrowseButton, "Browse...");
            topRow.Controls.Add(_artworkBrowseButton, 2, 0);

            _doubleBannerPreviewGrid = new TableLayoutPanel();
            _doubleBannerPreviewGrid.Name = "CreateModArtworkPreviewGrid";
            _doubleBannerPreviewGrid.Dock = DockStyle.Fill;
            _doubleBannerPreviewGrid.Margin = new Padding(0);
            _doubleBannerPreviewGrid.Padding = new Padding(0);
            _doubleBannerPreviewGrid.ColumnCount = 2;
            _doubleBannerPreviewGrid.RowCount = 1;
            _doubleBannerPreviewGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            _doubleBannerPreviewGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layout.Controls.Add(_doubleBannerPreviewGrid, 0, 1);

            _doubleBannerLeftPane = BuildDoubleBannerArtworkPane(false, _imagePreview);
            _doubleBannerRightPreview = NewArtworkPreviewBox(0, 0, 100, 100);
            _doubleBannerRightPreview.MouseDown += ImagePreview_MouseDown;
            _doubleBannerRightPreview.MouseMove += ImagePreview_MouseMove;
            _doubleBannerRightPreview.MouseUp += ImagePreview_MouseUp;
            _doubleBannerRightPreview.MouseEnter += delegate { _doubleBannerRightPreview.Focus(); };
            _doubleBannerRightPreview.MouseWheel += ImagePreview_MouseWheel;
            _doubleBannerRightPreview.KeyDown += ImagePreview_KeyDown;
            _doubleBannerRightPreview.DoubleClick += ImagePreview_DoubleClick;
            _doubleBannerRightPane = BuildDoubleBannerArtworkPane(true, _doubleBannerRightPreview);

            _doubleBannerPreviewGrid.Controls.Add(_doubleBannerLeftPane, 0, 0);
            _doubleBannerPreviewGrid.Controls.Add(_doubleBannerRightPane, 1, 0);
            _doubleBannerPreviewGrid.SetColumnSpan(_doubleBannerLeftPane, 2);
            _doubleBannerRightPane.Visible = false;

            // Bottom controls are one fixed block so neither row can be pushed below the
            // artwork frame. Fitting and Guide Colours each get a dedicated row.
            TableLayoutPanel bottomControls = new TableLayoutPanel();
            bottomControls.Name = "CreateModArtworkBottomControls";
            bottomControls.Dock = DockStyle.Fill;
            bottomControls.Margin = new Padding(0);
            bottomControls.Padding = new Padding(0);
            bottomControls.ColumnCount = 1;
            bottomControls.RowCount = 2;
            bottomControls.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            bottomControls.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layout.Controls.Add(bottomControls, 0, 2);

            TableLayoutPanel fitRow = new TableLayoutPanel();
            fitRow.Name = "CreateModArtworkFitRow";
            fitRow.Dock = DockStyle.Fill;
            fitRow.Margin = new Padding(0);
            fitRow.Padding = new Padding(0);
            fitRow.ColumnCount = 4;
            fitRow.RowCount = 1;
            fitRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
            fitRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            fitRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            fitRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
            bottomControls.Controls.Add(fitRow, 0, 0);

            Label fitLabel = NewCreateModLabel("Fitting");
            fitLabel.AutoSize = false;
            fitLabel.Dock = DockStyle.Fill;
            fitLabel.Margin = new Padding(0);
            fitLabel.TextAlign = ContentAlignment.MiddleLeft;
            fitRow.Controls.Add(fitLabel, 0, 0);
            AddFitButton(fitRow, 1, "Fill");
            AddFitButton(fitRow, 2, "Fit");
            AddFitButton(fitRow, 3, "Stretch");

            // Keep the Guide Colours caption and swatches in one explicit flow row.
            // A TableLayout label cell proved DPI-sensitive even with a fixed width, so the
            // caption now has a concrete size and is added directly before the swatches.
            FlowLayoutPanel guideRow = new FlowLayoutPanel();
            guideRow.Name = "CreateModArtworkGuideRow";
            guideRow.Dock = DockStyle.Fill;
            guideRow.FlowDirection = FlowDirection.LeftToRight;
            guideRow.WrapContents = false;
            guideRow.Margin = new Padding(0);
            guideRow.Padding = new Padding(0, 5, 0, 3);
            guideRow.BackColor = Color.Transparent;
            bottomControls.Controls.Add(guideRow, 0, 1);

            Label guideLabel = new Label();
            guideLabel.Name = "CreateModGuideColoursLabel";
            guideLabel.Text = "Guide Colours";
            guideLabel.AutoSize = false;
            guideLabel.Size = new Size(108, 28);
            guideLabel.Margin = new Padding(0, 0, 6, 0);
            guideLabel.Padding = new Padding(0);
            guideLabel.ForeColor = TwoPointTheme.PrimaryText;
            guideLabel.BackColor = Color.Transparent;
            guideLabel.Font = TwoPointTheme.BoldFont(9.5F);
            guideLabel.TextAlign = ContentAlignment.MiddleLeft;
            guideRow.Controls.Add(guideLabel);

            FlowLayoutPanel swatchRow = new FlowLayoutPanel();
            swatchRow.Name = "CreateModGuideColourSwatches";
            swatchRow.AutoSize = true;
            swatchRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            swatchRow.FlowDirection = FlowDirection.LeftToRight;
            swatchRow.WrapContents = false;
            swatchRow.Margin = new Padding(0);
            swatchRow.Padding = new Padding(0);
            swatchRow.BackColor = Color.Transparent;
            guideRow.Controls.Add(swatchRow);

            AddGuideColorSwatch(swatchRow, "White", Color.White);
            AddGuideColorSwatch(swatchRow, "Red", Color.Red);
            AddGuideColorSwatch(swatchRow, "Yellow", Color.Yellow);
            AddGuideColorSwatch(swatchRow, "Blue", Color.DeepSkyBlue);
            AddGuideColorSwatch(swatchRow, "Orange", Color.Orange);
            AddGuideColorSwatch(swatchRow, "Green", Color.LimeGreen);
            AddGuideColorSwatch(swatchRow, "Pink", Color.DeepPink);
            AddGuideColorSwatch(swatchRow, "Purple", Color.MediumPurple);

            SyncGuideColorSwatches();
            TemplateDefinition currentTemplate = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            bool artworkEnabled = currentTemplate != null && currentTemplate.ImageProcessingEnabled;
            SetGuideColorSwatchesEnabled(artworkEnabled);
            _artworkBrowseButton.Enabled = artworkEnabled;
        }

        private void ConfigureArtworkToolbarButton(Button button, string caption)
        {
            if (button == null)
                return;

            // Use the normal Memento Maker themed button path. Keeping real button text
            // avoids the blank-caption issue caused by the previous custom Paint handler.
            button.Text = caption;
            button.AccessibleName = caption;
            button.UseMnemonic = false;
            button.Padding = new Padding(0);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Font = TwoPointTheme.BoldFont(8.5F);
            TwoPointTheme.StyleButton(button);
        }

        private void AddGuideColorSwatch(FlowLayoutPanel parent, string name, Color color)
        {
            Button swatch = new Button();
            swatch.Name = "GuideColorSwatch" + name;
            swatch.Tag = name;
            swatch.Size = new Size(26, 26);
            swatch.Margin = new Padding(2, 2, 2, 2);
            swatch.Cursor = Cursors.Hand;
            swatch.AccessibleName = name + " guide colour";
            swatch.Text = string.Empty;
            swatch.Image = CreateGuideColorSwatchImage(color, 14);
            swatch.ImageAlign = ContentAlignment.MiddleCenter;
            TwoPointTheme.StyleSelectableButton(swatch, false);
            swatch.Padding = new Padding(0);
            swatch.Click += delegate
            {
                int index = _guideColor.Items.IndexOf(name);
                if (index >= 0)
                    _guideColor.SelectedIndex = index;
            };
            parent.Controls.Add(swatch);
            _guideColorSwatches[name] = swatch;

        }

        private static Bitmap CreateGuideColorSwatchImage(Color color, int size)
        {
            int imageSize = Math.Max(10, size);
            Bitmap image = new Bitmap(imageSize, imageSize);
            using (Graphics g = Graphics.FromImage(image))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                Rectangle bounds = new Rectangle(1, 1, imageSize - 3, imageSize - 3);
                using (System.Drawing.Drawing2D.GraphicsPath path = TwoPointTheme.RoundedRectangle(bounds, 3))
                using (SolidBrush fill = new SolidBrush(color))
                using (Pen border = new Pen(Color.FromArgb(110, 92, 70), 1F))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(border, path);
                }
            }
            return image;
        }

        private void SyncGuideColorSwatches()
        {
            string selected = NormaliseGuideColorName(_guideColor == null || _guideColor.SelectedItem == null
                ? "Yellow"
                : _guideColor.SelectedItem.ToString());

            foreach (KeyValuePair<string, Button> pair in _guideColorSwatches)
                TwoPointTheme.StyleSelectableButton(pair.Value, string.Equals(pair.Key, selected, StringComparison.OrdinalIgnoreCase));
        }

        private void SetGuideColorSwatchesEnabled(bool enabled)
        {
            foreach (KeyValuePair<string, Button> pair in _guideColorSwatches)
            {
                if (pair.Value != null)
                    pair.Value.Enabled = enabled;
            }
        }

        private static string NormaliseGuideColorName(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "Black", StringComparison.OrdinalIgnoreCase))
                return "Yellow";

            switch (value.Trim())
            {
                case "White":
                case "Red":
                case "Yellow":
                case "Blue":
                case "Orange":
                case "Green":
                case "Pink":
                case "Purple":
                    return value.Trim();
                default:
                    return "Yellow";
            }
        }

        private void AddFitButton(TableLayoutPanel parent, int column, string mode)
        {
            Button button = new Button();
            button.Text = mode;
            button.Tag = mode;
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(2, 5, 2, 5);
            button.Click += delegate
            {
                int index = _fitMode.Items.IndexOf(mode);
                if (index >= 0)
                    _fitMode.SelectedIndex = index;
                SyncFitButtons();
            };
            TwoPointTheme.StyleSegmentButton(button, false);
            parent.Controls.Add(button, column, 0);
            _fitButtons[mode] = button;

        }
        private void SelectItemType(string type)
        {
            TemplateDefinition currentBeforeItemTypeChange = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            if (IsDualArtworkTemplate(currentBeforeItemTypeChange))
                CaptureActiveDoubleBannerSideState();

            foreach (KeyValuePair<string, Button> pair in _itemTypeTiles)
                TwoPointTheme.StyleSelectableButton(pair.Value, string.Equals(pair.Key, type, StringComparison.OrdinalIgnoreCase));

            if (string.Equals(type, "Decor", StringComparison.OrdinalIgnoreCase))
            {
                SelectWallpaperTemplate();
            }
            else if (string.Equals(type, "Poster", StringComparison.OrdinalIgnoreCase))
            {
                string selectedPoster = GetSelectedPosterTemplateKey();
                SelectPosterTemplate(selectedPoster);
            }
            else if (string.Equals(type, "Mural", StringComparison.OrdinalIgnoreCase))
            {
                TemplateDefinition wanted = FindTemplateByKey("Mural");
                if (wanted != null)
                    _templateBox.SelectedItem = wanted;
            }
            else if (string.Equals(type, "Single Banner", StringComparison.OrdinalIgnoreCase))
            {
                string selectedTheme = GetSelectedBannerTheme();
                if (string.IsNullOrEmpty(selectedTheme))
                    selectedTheme = "Prehistory Banner";
                SelectBannerTemplate(selectedTheme);
            }
            else if (string.Equals(type, "Double Banner", StringComparison.OrdinalIgnoreCase))
            {
                string selectedTheme = GetSelectedDoubleBannerTheme();
                if (string.IsNullOrEmpty(selectedTheme))
                    selectedTheme = "Prehistory Double Banner";
                SelectDoubleBannerTemplate(selectedTheme);
            }
            else if (string.Equals(type, "Hanging Sign", StringComparison.OrdinalIgnoreCase))
            {
                string selectedSize = GetSelectedHangingSignTemplateKey();
                if (string.IsNullOrEmpty(selectedSize))
                    selectedSize = "Small Hanging Sign";
                SelectHangingSignTemplate(selectedSize);
            }
            else if (string.Equals(type, "Wall Sign", StringComparison.OrdinalIgnoreCase))
            {
                string selectedSize = GetSelectedWallSignTemplateKey();
                if (string.IsNullOrEmpty(selectedSize))
                    selectedSize = "Small Wall Sign";
                SelectWallSignTemplate(selectedSize);
            }
            else
            {
                if (!_rugShapeTiles.ContainsKey("Square"))
                    return;
                TemplateDefinition currentTemplate = _templateBox.SelectedItem as TemplateDefinition;
                bool enteringRugFromAnotherType = currentTemplate == null || !IsRugTemplate(currentTemplate);
                if (enteringRugFromAnotherType)
                {
                    _syncingGraphicalTemplateControls = true;
                    _recolourToggle.Checked = true;
                    StyleRecolourToggle();
                    foreach (KeyValuePair<string, Button> pair in _rugShapeTiles)
                        TwoPointTheme.StyleSelectableButton(pair.Value, string.Equals(pair.Key, "Square", StringComparison.OrdinalIgnoreCase));
                    _syncingGraphicalTemplateControls = false;
                }
                else
                {
                    bool anySelected = false;
                    foreach (KeyValuePair<string, Button> pair in _rugShapeTiles)
                    {
                        if (pair.Value.FlatAppearance.BorderSize == 2)
                            anySelected = true;
                    }
                    if (!anySelected)
                        TwoPointTheme.StyleSelectableButton(_rugShapeTiles["Square"], true);
                }
                SelectRugTemplate();
            }
        }

        private void SelectWallpaperTemplate()
        {
            TemplateDefinition wanted = FindTemplateByKey("Wallpaper");
            if (wanted == null)
                return;
            if (_wallpaperOptionTile != null)
                TwoPointTheme.StyleSelectableButton(_wallpaperOptionTile, true);
            _templateBox.SelectedItem = wanted;
        }

        private void SelectPosterTemplate(string templateKey)
        {
            if (string.IsNullOrWhiteSpace(templateKey))
                templateKey = "Standard Poster";

            TemplateDefinition wanted = FindTemplateByKey(templateKey);
            if (wanted == null || !IsPosterTemplate(wanted))
                wanted = FindTemplateByKey("Standard Poster");
            if (wanted == null)
                return;

            foreach (KeyValuePair<string, Button> pair in _posterSizeTiles)
                TwoPointTheme.StyleSelectableButton(pair.Value, string.Equals(pair.Key, wanted.Key, StringComparison.OrdinalIgnoreCase));

            TemplateDefinition current = _templateBox.SelectedItem as TemplateDefinition;
            bool preserve = current != null && IsPosterTemplate(current);
            if (preserve)
                _imageProcessor.EnsureSizeFamilyReference(current, _placement);
            _preservePosterEditorStateOnTemplateChange = preserve;
            try
            {
                _templateBox.SelectedItem = wanted;
            }
            finally
            {
                _preservePosterEditorStateOnTemplateChange = false;
            }
        }

        private void SelectBannerTemplate(string templateKey)
        {
            if (string.IsNullOrWhiteSpace(templateKey))
                templateKey = "Prehistory Banner";

            TemplateDefinition wanted = FindTemplateByKey(templateKey);
            if (wanted == null || !IsSingleBannerTemplate(wanted))
                return;

            foreach (KeyValuePair<string, Button> pair in _bannerThemeTiles)
                TwoPointTheme.StyleSelectableButton(pair.Value, string.Equals(pair.Key, wanted.Key, StringComparison.OrdinalIgnoreCase));

            TemplateDefinition current = _templateBox.SelectedItem as TemplateDefinition;
            bool preserve = current != null && IsSingleBannerTemplate(current);
            _preserveBannerEditorStateOnTemplateChange = preserve;
            try
            {
                _templateBox.SelectedItem = wanted;
            }
            finally
            {
                _preserveBannerEditorStateOnTemplateChange = false;
            }
        }

        private void SelectDoubleBannerTemplate(string templateKey)
        {
            CaptureActiveDoubleBannerSideState();
            if (string.IsNullOrWhiteSpace(templateKey))
                templateKey = "Prehistory Double Banner";

            TemplateDefinition wanted = FindTemplateByKey(templateKey);
            if (wanted == null || !IsDoubleBannerTemplate(wanted))
                return;

            foreach (KeyValuePair<string, Button> pair in _doubleBannerThemeTiles)
                TwoPointTheme.StyleSelectableButton(pair.Value, string.Equals(pair.Key, wanted.Key, StringComparison.OrdinalIgnoreCase));

            TemplateDefinition current = _templateBox.SelectedItem as TemplateDefinition;
            bool preserve = current != null && IsDoubleBannerTemplate(current);
            _preserveDoubleBannerEditorStateOnTemplateChange = preserve;
            try
            {
                _templateBox.SelectedItem = wanted;
            }
            finally
            {
                _preserveDoubleBannerEditorStateOnTemplateChange = false;
            }
        }

        private void SelectRugTemplate()
        {
            string size = GetSelectedItemType();
            if (!string.Equals(size, "Small Rug", StringComparison.OrdinalIgnoreCase) && !string.Equals(size, "Large Rug", StringComparison.OrdinalIgnoreCase))
                return;

            string shape = GetSelectedRugShape();
            if (string.IsNullOrEmpty(shape))
                shape = "Square";
            string key = (string.Equals(size, "Large Rug", StringComparison.OrdinalIgnoreCase) ? "Large " : "") +
                (_recolourToggle != null && _recolourToggle.Checked ? "Staff " : "Marketing ") + shape + " Rug";
            TemplateDefinition wanted = FindTemplateByKey(key);
            if (wanted != null)
            {
                TemplateDefinition current = _templateBox.SelectedItem as TemplateDefinition;
                bool preserve = current != null && IsRugTemplate(current) && IsRugTemplate(wanted);
                _preserveRugEditorStateOnTemplateChange = preserve;
                try
                {
                    _templateBox.SelectedItem = wanted;
                }
                finally
                {
                    _preserveRugEditorStateOnTemplateChange = false;
                }
            }
        }

        private TemplateDefinition FindTemplateByKey(string key)
        {
            // Projects created before Poster Sizes stored the single poster template as
            // "Poster". Treat that legacy key as Standard Poster so old projects open
            // with the same physical poster size and can be rebuilt without migration.
            if (string.Equals(key, "Poster", StringComparison.OrdinalIgnoreCase))
                key = "Standard Poster";

            foreach (TemplateDefinition template in _templates)
            {
                if (string.Equals(template.Key, key, StringComparison.OrdinalIgnoreCase))
                    return template;
            }
            return null;
        }

        private string GetSelectedItemType()
        {
            foreach (KeyValuePair<string, Button> pair in _itemTypeTiles)
            {
                if (pair.Value.FlatAppearance.BorderSize == 2)
                    return pair.Key;
            }
            return "Poster";
        }

        private string GetSelectedPosterTemplateKey()
        {
            foreach (KeyValuePair<string, Button> pair in _posterSizeTiles)
            {
                if (pair.Value.FlatAppearance.BorderSize == 2)
                    return pair.Key;
            }
            return "Standard Poster";
        }

        private string GetSelectedRugShape()
        {
            foreach (KeyValuePair<string, Button> pair in _rugShapeTiles)
            {
                if (pair.Value.FlatAppearance.BorderSize == 2)
                    return pair.Key;
            }
            return "Square";
        }

        private string GetSelectedBannerTheme()
        {
            foreach (KeyValuePair<string, Button> pair in _bannerThemeTiles)
            {
                if (pair.Value.FlatAppearance.BorderSize == 2)
                    return pair.Key;
            }
            return "Prehistory Banner";
        }

        private string GetSelectedDoubleBannerTheme()
        {
            foreach (KeyValuePair<string, Button> pair in _doubleBannerThemeTiles)
            {
                if (pair.Value.FlatAppearance.BorderSize == 2)
                    return pair.Key;
            }
            return "Prehistory Double Banner";
        }

        private string GetSelectedHangingSignTemplateKey()
        {
            foreach (KeyValuePair<string, Button> pair in _hangingSignSizeTiles)
            {
                if (pair.Value.FlatAppearance.BorderSize == 2)
                    return pair.Key;
            }
            return "Small Hanging Sign";
        }

        private void SelectHangingSignTemplate(string templateKey)
        {
            CaptureActiveDoubleBannerSideState();
            if (string.IsNullOrWhiteSpace(templateKey))
                templateKey = "Small Hanging Sign";

            TemplateDefinition wanted = FindTemplateByKey(templateKey);
            if (wanted == null || !IsHangingSignTemplate(wanted))
                wanted = FindTemplateByKey("Small Hanging Sign");
            if (wanted == null)
                return;

            foreach (KeyValuePair<string, Button> pair in _hangingSignSizeTiles)
                TwoPointTheme.StyleSelectableButton(pair.Value, string.Equals(pair.Key, wanted.Key, StringComparison.OrdinalIgnoreCase));

            TemplateDefinition current = _templateBox.SelectedItem as TemplateDefinition;
            bool preserve = current != null && IsHangingSignTemplate(current);
            if (preserve)
            {
                _imageProcessor.EnsureSizeFamilyReference(current, _doubleBannerLeftPlacement);
                _imageProcessor.EnsureSizeFamilyReference(current, _doubleBannerRightPlacement);
                _imageProcessor.EnsureSizeFamilyReference(current, _placement);
            }
            _preserveDoubleBannerEditorStateOnTemplateChange = preserve;
            try
            {
                _templateBox.SelectedItem = wanted;
            }
            finally
            {
                _preserveDoubleBannerEditorStateOnTemplateChange = false;
            }
        }

        private string GetSelectedWallSignTemplateKey()
        {
            foreach (KeyValuePair<string, Button> pair in _wallSignSizeTiles)
            {
                if (pair.Value.FlatAppearance.BorderSize == 2)
                    return pair.Key;
            }
            return "Small Wall Sign";
        }

        private void SelectWallSignTemplate(string templateKey)
        {
            CaptureActiveDoubleBannerSideState();
            if (string.IsNullOrWhiteSpace(templateKey))
                templateKey = "Small Wall Sign";

            TemplateDefinition wanted = FindTemplateByKey(templateKey);
            if (wanted == null || !IsWallSignTemplate(wanted))
                wanted = FindTemplateByKey("Small Wall Sign");
            if (wanted == null)
                return;

            foreach (KeyValuePair<string, Button> pair in _wallSignSizeTiles)
                TwoPointTheme.StyleSelectableButton(pair.Value, string.Equals(pair.Key, wanted.Key, StringComparison.OrdinalIgnoreCase));

            TemplateDefinition current = _templateBox.SelectedItem as TemplateDefinition;
            bool preserve = current != null && IsWallSignTemplate(current);
            if (preserve)
            {
                _imageProcessor.EnsureSizeFamilyReference(current, _doubleBannerLeftPlacement);
                _imageProcessor.EnsureSizeFamilyReference(current, _doubleBannerRightPlacement);
                _imageProcessor.EnsureSizeFamilyReference(current, _placement);
            }
            _preserveDoubleBannerEditorStateOnTemplateChange = preserve;
            try
            {
                _templateBox.SelectedItem = wanted;
            }
            finally
            {
                _preserveDoubleBannerEditorStateOnTemplateChange = false;
            }
        }

        private void SyncCreateModFromTemplate(TemplateDefinition template)
        {
            if (template == null || _itemTypeTiles.Count == 0)
                return;

            _syncingGraphicalTemplateControls = true;
            try
            {
                string type;
                string shape = "Square";
                bool recolourable = false;
                bool wallpaper = IsWallpaperTemplate(template);
                bool poster = IsPosterTemplate(template);
                bool rug = IsRugTemplate(template);
                bool banner = IsSingleBannerTemplate(template);
                bool hangingSign = IsHangingSignTemplate(template);
                bool wallSign = IsWallSignTemplate(template);
                bool doubleBanner = IsDoubleBannerTemplate(template);

                if (wallpaper)
                    type = "Decor";
                else if (wallSign)
                    type = "Wall Sign";
                else if (hangingSign)
                    type = "Hanging Sign";
                else if (doubleBanner)
                    type = "Double Banner";
                else if (banner)
                    type = "Single Banner";
                else if (poster)
                    type = "Poster";
                else if (!rug)
                    type = "Mural";
                else
                {
                    type = template.Key.StartsWith("Large ", StringComparison.OrdinalIgnoreCase) ? "Large Rug" : "Small Rug";
                    recolourable = template.Key.IndexOf("Staff", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (template.Key.IndexOf("Rectangle", StringComparison.OrdinalIgnoreCase) >= 0) shape = "Rectangle";
                    else if (template.Key.IndexOf("Square", StringComparison.OrdinalIgnoreCase) >= 0) shape = "Square";
                    else if (template.Key.IndexOf("Octagon", StringComparison.OrdinalIgnoreCase) >= 0) shape = "Octagon";
                }

                foreach (KeyValuePair<string, Button> pair in _itemTypeTiles)
                    TwoPointTheme.StyleSelectableButton(pair.Value, string.Equals(pair.Key, type, StringComparison.OrdinalIgnoreCase));
                if (_wallpaperOptionTile != null)
                    TwoPointTheme.StyleSelectableButton(_wallpaperOptionTile, wallpaper);
                foreach (KeyValuePair<string, Button> pair in _posterSizeTiles)
                {
                    string selectedPosterKey = string.Equals(template.Key, "Poster", StringComparison.OrdinalIgnoreCase)
                        ? "Standard Poster" : template.Key;
                    TwoPointTheme.StyleSelectableButton(pair.Value, poster && string.Equals(pair.Key, selectedPosterKey, StringComparison.OrdinalIgnoreCase));
                }
                foreach (KeyValuePair<string, Button> pair in _rugShapeTiles)
                    TwoPointTheme.StyleSelectableButton(pair.Value, string.Equals(pair.Key, shape, StringComparison.OrdinalIgnoreCase));
                foreach (KeyValuePair<string, Button> pair in _bannerThemeTiles)
                    TwoPointTheme.StyleSelectableButton(pair.Value, banner && string.Equals(pair.Key, template.Key, StringComparison.OrdinalIgnoreCase));
                foreach (KeyValuePair<string, Button> pair in _doubleBannerThemeTiles)
                    TwoPointTheme.StyleSelectableButton(pair.Value, doubleBanner && string.Equals(pair.Key, template.Key, StringComparison.OrdinalIgnoreCase));
                foreach (KeyValuePair<string, Button> pair in _hangingSignSizeTiles)
                    TwoPointTheme.StyleSelectableButton(pair.Value, hangingSign && string.Equals(pair.Key, template.Key, StringComparison.OrdinalIgnoreCase));
                foreach (KeyValuePair<string, Button> pair in _wallSignSizeTiles)
                    TwoPointTheme.StyleSelectableButton(pair.Value, wallSign && string.Equals(pair.Key, template.Key, StringComparison.OrdinalIgnoreCase));

                if (_recolourToggle != null)
                    _recolourToggle.Checked = recolourable;
                StyleRecolourToggle();
                if (_rugOptionsVisualPanel != null)
                    _rugOptionsVisualPanel.Visible = wallpaper || poster || rug || banner || doubleBanner || hangingSign || wallSign;
                if (_decorOptionsContentPanel != null)
                {
                    _decorOptionsContentPanel.Visible = wallpaper;
                    if (wallpaper)
                        _decorOptionsContentPanel.BringToFront();
                }
                if (_posterOptionsContentPanel != null)
                {
                    _posterOptionsContentPanel.Visible = poster;
                    if (poster)
                        _posterOptionsContentPanel.BringToFront();
                }
                if (_rugOptionsContentPanel != null)
                    _rugOptionsContentPanel.Visible = rug;
                if (_bannerOptionsContentPanel != null)
                {
                    _bannerOptionsContentPanel.Visible = banner;
                    if (banner)
                        _bannerOptionsContentPanel.BringToFront();
                }
                if (_doubleBannerOptionsContentPanel != null)
                {
                    _doubleBannerOptionsContentPanel.Visible = doubleBanner;
                    if (doubleBanner)
                        _doubleBannerOptionsContentPanel.BringToFront();
                }
                if (_hangingSignOptionsContentPanel != null)
                {
                    _hangingSignOptionsContentPanel.Visible = hangingSign;
                    if (hangingSign)
                        _hangingSignOptionsContentPanel.BringToFront();
                }
                if (_wallSignOptionsContentPanel != null)
                {
                    _wallSignOptionsContentPanel.Visible = wallSign;
                    if (wallSign)
                        _wallSignOptionsContentPanel.BringToFront();
                }
                ApplyWallpaperCreateRules(wallpaper);
                ConfigureDoubleBannerArtworkLayout(doubleBanner || hangingSign || wallSign);
                if (_createLeftLayout != null && _createLeftLayout.RowStyles.Count > 1)
                {
                    // Reserve the Item Options slot even for Poster/Mural. This keeps Details
                    // locked to one predictable position instead of jumping when options hide.
                    _createLeftLayout.RowStyles[1].SizeType = SizeType.Absolute;
                    _createLeftLayout.RowStyles[1].Height = 170F;
                }
                SyncFitButtons();
            }
            finally
            {
                _syncingGraphicalTemplateControls = false;
            }
        }

        private void StyleRecolourToggle()
        {
            if (_recolourToggle == null)
                return;
            _recolourToggle.Text = _recolourToggle.Checked ? "ON" : "OFF";
            TwoPointToggle graphicalToggle = _recolourToggle as TwoPointToggle;
            if (graphicalToggle != null)
            {
                graphicalToggle.Invalidate();
                return;
            }
            _recolourToggle.FlatStyle = FlatStyle.Flat;
            _recolourToggle.FlatAppearance.BorderSize = 1;
            _recolourToggle.FlatAppearance.BorderColor = _recolourToggle.Checked ? TwoPointTheme.PrimaryGreenDark : Color.Gray;
            _recolourToggle.BackColor = _recolourToggle.Checked ? TwoPointTheme.PrimaryGreen : Color.FromArgb(172, 168, 158);
            _recolourToggle.ForeColor = Color.White;
        }

        private void SyncFitButtons()
        {
            string selected = _fitMode.SelectedItem == null ? "Fill" : _fitMode.SelectedItem.ToString();
            foreach (KeyValuePair<string, Button> pair in _fitButtons)
                TwoPointTheme.StyleSegmentButton(pair.Value, string.Equals(pair.Key, selected, StringComparison.OrdinalIgnoreCase));
        }
        private void Artwork_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
                return;
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0 || !File.Exists(files[0]))
                return;
            string ext = Path.GetExtension(files[0]).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                return;
            _imagePath.Text = files[0];
            ResetPlacement(false);
            UpdateArtworkPreview();
        }

        private void InitialiseSingleWindowPages(Control legacyHeader)
        {
            foreach (Control control in Controls)
            {
                if (control == legacyHeader || control is FolderBackdropPanel || control is FolderTabButton ||
                    control.Name == "TwoPointShellTitle" || string.Equals(control.Tag as string, "CreateModLegacyHidden", StringComparison.Ordinal))
                    continue;
                _createPageControls.Add(control);
            }

            _tabPageHost = new Panel();
            _tabPageHost.Name = "TwoPointTabPageHost";
            _tabPageHost.Bounds = GetUnifiedTabPageBounds();
            _tabPageHost.Anchor = AnchorStyles.None;
            _tabPageHost.BackColor = TwoPointTheme.ContentBackground;
            _tabPageHost.Visible = false;
            Controls.Add(_tabPageHost);
            ApplyTabPageHostRegion();
            _tabPageHost.SizeChanged += delegate { ApplyTabPageHostRegion(); };
            TwoPointTheme.BringShellToFront(this);
        }

        private void SetCreateControlsVisible(bool visible)
        {
            foreach (Control control in _createPageControls)
            {
                if (control != null && !control.IsDisposed)
                    control.Visible = visible;
            }
        }

        private void ShowCreatePage()
        {
            if (_activeProject != null && _projectLibrary.Load(_activeProject.ModId) == null)
            {
                StartNewProject();
                AppendLog("The active project record was deleted from My Mods. The editor has been reset to a new mod.");
            }

            if (_tabPageHost != null)
            {
                _tabPageHost.Visible = false;
                _tabPageHost.Controls.Clear();
            }
            SetCreateControlsVisible(true);
            RefreshCreateDecorPackChoices();
            TwoPointTheme.SetActiveTab(this, "Create Mod");
            _artworkTipPaused = false;
            ResumeArtworkTipCycle();
        }

        private async Task ShowSettingsAsync()
        {
            if (_artworkTipTimer != null)
                _artworkTipTimer.Stop();
            SetCreateControlsVisible(false);
            _tabPageHost.Controls.Clear();
            _tabPageHost.Visible = true;
            _tabPageHost.BringToFront();
            TwoPointTheme.SetActiveTab(this, "Settings");

            if (_embeddedSettings != null && !_embeddedSettings.IsDisposed)
                _embeddedSettings.Dispose();

            _embeddedSettings = new SettingsForm(_settings, _discovery, true);
            _embeddedSettings.TopLevel = false;
            _embeddedSettings.FormBorderStyle = FormBorderStyle.None;
            _embeddedSettings.Dock = DockStyle.Fill;
            _embeddedSettings.SettingsSaved += async delegate
            {
                _settings = _embeddedSettings.Settings;
                _outputPath.Text = _settings.LastOutputFolder ?? SettingsService.GetGameModsFolder();
                _settingsService.Save(_settings);
                await RefreshEnvironmentAsync(true);
                await ApplyUnityWorkerPreferenceAsync();
                TwoPointTheme.SetActiveTab(this, "Settings");
            };
            _embeddedSettings.RefreshRequested += async delegate
            {
                _settings = _embeddedSettings.Settings;
                _outputPath.Text = _settings.LastOutputFolder ?? SettingsService.GetGameModsFolder();
                _settingsService.Save(_settings);
                await RefreshEnvironmentAsync(true);
                await ApplyUnityWorkerPreferenceAsync();
                TwoPointTheme.SetActiveTab(this, "Settings");
            };
            _embeddedSettings.RebuildEnvironmentRequested += async delegate
            {
                _settings = _embeddedSettings.Settings;
                _outputPath.Text = _settings.LastOutputFolder ?? SettingsService.GetGameModsFolder();
                _settingsService.Save(_settings);
                await RebuildEnvironmentAsync();
                TwoPointTheme.SetActiveTab(this, "Settings");
            };
            _embeddedSettings.ClearLogRequested += delegate
            {
                _log.Clear();
            };
            _embeddedSettings.WorkshopQueryRequested += async delegate
            {
                _settings = _embeddedSettings.Settings;
                _settingsService.Save(_settings);
                _state = DetectPrerequisites();
                _embeddedSettings.SetWorkshopQueryState(true, null,
                    IsPersistentWorkerEnabled() ? "Waiting for the private Unity worker..." : "Preparing the one-shot Unity Workshop check...");
                await ApplyUnityWorkerPreferenceAsync();

                if (_state == null || !_state.UnityFound || !_state.EnvironmentReady)
                {
                    _embeddedSettings.SetWorkshopQueryState(false, null, "Unity and the private environment must be ready before checking Steam Workshop.");
                    return;
                }

                _embeddedSettings.SetWorkshopQueryState(true, null, "Starting the official SDK Steam Workshop check...");
                AppendLog("============================================================");
                AppendLog("Steam Workshop connection check started.");

                try
                {
                    WorkshopQueryResult result = await _workshopService.QueryPublishedItemsAsync(
                        _state.UnityExePath,
                        _state.EnvironmentProjectPath,
                        delegate(string message)
                        {
                            if (_embeddedSettings != null && !_embeddedSettings.IsDisposed)
                                _embeddedSettings.SetWorkshopQueryState(true, null, message);
                        },
                        delegate(string message) { SafeAppendLog(message); },
                        GetPersistentWorker());

                    RememberWorkshopQuery(result);
                    int linkChanges = _projectLibrary.ApplyWorkshopQueryState(result, null);
                    if (linkChanges > 0)
                    {
                        AppendLog("Workshop link validation updated " + linkChanges + " saved Memento Maker project(s).");
                        _myModsNeedsRefresh = true;
                    }
                    if (_embeddedSettings != null && !_embeddedSettings.IsDisposed)
                        _embeddedSettings.SetWorkshopQueryState(false, result, result.Message);

                    int count = result.Items == null ? 0 : result.Items.Count;
                    AppendLog("Steam Workshop connection check complete: " + count + " published item" + (count == 1 ? "." : "s."));
                    if (result.Items != null)
                    {
                        int shown = Math.Min(8, result.Items.Count);
                        for (int i = 0; i < shown; i++)
                        {
                            WorkshopItemSummary item = result.Items[i];
                            if (item != null)
                                AppendLog("Workshop item: " + (item.Title ?? "") + " [" + (item.PublishedFileId ?? "") + "] Tags: " + (item.Tags ?? ""));
                        }
                        if (result.Items.Count > shown)
                            AppendLog("...and " + (result.Items.Count - shown) + " more Workshop item(s).");
                    }
                }
                catch (Exception ex)
                {
                    AppendLog("Steam Workshop check failed: " + ex.Message);
                    if (_embeddedSettings != null && !_embeddedSettings.IsDisposed)
                        _embeddedSettings.SetWorkshopQueryState(false, null, "Workshop check failed. Try again or use Check & Repair if the problem continues.");
                }
            };
            _tabPageHost.Controls.Add(_embeddedSettings);
            _embeddedSettings.Show();
            _embeddedSettings.SetBuildLogText(_log.Text);
            // Re-inspect the private environment whenever Settings is opened. Files may have been
            // removed or changed outside Memento Maker while the application was running.
            _state = DetectPrerequisites();
            _embeddedSettings.UpdateEnvironmentStatus(_state);
            UpdateEmbeddedUnityWorkerStatus(null);
            if (_lastWorkshopQueryResult != null)
                _embeddedSettings.SetWorkshopQueryState(false, _lastWorkshopQueryResult, _lastWorkshopQueryResult.Message);
            TwoPointTheme.BringShellToFront(this);
            await Task.Yield();
        }

        private async Task ShowMyModsAsync()
        {
            if (_artworkTipTimer != null)
                _artworkTipTimer.Stop();
            SetCreateControlsVisible(false);
            _tabPageHost.Controls.Clear();
            _tabPageHost.Visible = true;
            _tabPageHost.BringToFront();
            TwoPointTheme.SetActiveTab(this, "My Mods");

            // Keep a single embedded My Mods page alive. Recreating the form caused the
            // icon previews and project list to be rebuilt on every tab switch. On the first
            // open, create the shell immediately and initialise the project list asynchronously
            // after it is visible so the tab no longer appears to freeze.
            bool createdMyMods = false;
            if (_embeddedMyMods == null || _embeddedMyMods.IsDisposed)
            {
                _embeddedMyMods = new MyModsForm(_projectLibrary, true);
                createdMyMods = true;
                _embeddedMyMods.TopLevel = false;
                _embeddedMyMods.FormBorderStyle = FormBorderStyle.None;
                _embeddedMyMods.Dock = DockStyle.Fill;
                _embeddedMyMods.SelectionRequested += async delegate
                {
                    MyModsSelection selection = _embeddedMyMods.Selection;
                    if (selection == null)
                        return;

                    bool familyWorkshopAction = selection.Projects != null && selection.Projects.Count > 0 &&
                        !string.IsNullOrEmpty(selection.FamilyKey);

                    if (string.Equals(selection.Action, "Workshop", StringComparison.OrdinalIgnoreCase))
                    {
                        if (familyWorkshopAction)
                            await PublishOrUpdateWorkshopFamilyAsync(selection.Projects, selection.FamilyKey, selection.FamilyName, selection.Project);
                        else
                            await PublishOrUpdateWorkshopAsync(selection.Project);
                        if (_embeddedMyMods != null && !_embeddedMyMods.IsDisposed && selection.Project != null)
                            _embeddedMyMods.RefreshProjects(selection.Project.ModId);
                        return;
                    }

                    if (string.Equals(selection.Action, "WorkshopOpen", StringComparison.OrdinalIgnoreCase))
                    {
                        OpenLinkedWorkshopPage(familyWorkshopAction
                            ? BuildWorkshopFamilyProxy(selection.Projects, selection.FamilyKey, selection.FamilyName, selection.Project)
                            : selection.Project);
                        return;
                    }

                    if (string.Equals(selection.Action, "WorkshopRefresh", StringComparison.OrdinalIgnoreCase))
                    {
                        if (familyWorkshopAction)
                            await RefreshWorkshopFamilyStatusAsync(selection.Projects, selection.FamilyKey, true, selection.Project);
                        else
                            await RefreshWorkshopStatusAsync(selection.Project, true);
                        return;
                    }

                    if (string.Equals(selection.Action, "WorkshopRelink", StringComparison.OrdinalIgnoreCase))
                    {
                        if (familyWorkshopAction)
                            await RelinkWorkshopFamilyItemAsync(selection.Projects, selection.FamilyKey, selection.FamilyName, selection.Project);
                        else
                            await RelinkWorkshopItemAsync(selection.Project);
                        return;
                    }

                    if (string.Equals(selection.Action, "WorkshopUnlink", StringComparison.OrdinalIgnoreCase))
                    {
                        if (familyWorkshopAction)
                            UnlinkWorkshopFamilyItem(selection.Projects, selection.FamilyKey, selection.FamilyName, selection.Project);
                        else
                            UnlinkWorkshopItem(selection.Project);
                        return;
                    }

                    if (string.Equals(selection.Action, "RebuildFamily", StringComparison.OrdinalIgnoreCase))
                    {
                        string selectAfter = selection.Project != null ? selection.Project.ModId :
                            (selection.Projects != null && selection.Projects.Count > 0 ? selection.Projects[0].ModId : null);
                        await RebuildVariantFamilyAsync(selection.Projects, selection.FamilyKey, selection.FamilyName);
                        if (_embeddedMyMods != null && !_embeddedMyMods.IsDisposed)
                            _embeddedMyMods.RefreshProjects(selectAfter);
                        _myModsNeedsRefresh = false;
                        return;
                    }

                    if (string.Equals(selection.Action, "RebuildDecorPack", StringComparison.OrdinalIgnoreCase))
                    {
                        string selectAfter = selection.Project != null ? selection.Project.ModId :
                            (selection.Projects != null && selection.Projects.Count > 0 ? selection.Projects[0].ModId : null);
                        await RebuildWallpaperDecorPackAsync(selection.Projects, selection.FamilyKey, selection.FamilyName);
                        if (_embeddedMyMods != null && !_embeddedMyMods.IsDisposed)
                            _embeddedMyMods.RefreshProjects(selectAfter);
                        _myModsNeedsRefresh = false;
                        return;
                    }

                    if (string.Equals(selection.Action, "RebuildQueue", StringComparison.OrdinalIgnoreCase))
                    {
                        string selectAfter = selection.Project != null ? selection.Project.ModId :
                            (selection.Projects != null && selection.Projects.Count > 0 ? selection.Projects[0].ModId : null);
                        await RebuildProjectsQueueAsync(selection.Projects);
                        if (_embeddedMyMods != null && !_embeddedMyMods.IsDisposed)
                            _embeddedMyMods.RefreshProjects(selectAfter);
                        _myModsNeedsRefresh = false;
                        return;
                    }

                    if (selection.Project != null)
                    {
                        LoadProjectIntoEditor(selection.Project);
                        ShowCreatePage();
                    }
                };
                _myModsNeedsRefresh = false;
            }
            else if (_myModsNeedsRefresh)
            {
                _embeddedMyMods.RefreshProjects(null);
                _myModsNeedsRefresh = false;
            }

            _tabPageHost.Controls.Add(_embeddedMyMods);
            _embeddedMyMods.Show();
            TwoPointTheme.BringShellToFront(this);
            await Task.Yield();

            if (createdMyMods)
                await _embeddedMyMods.InitialiseProjectsAsync(null);

            _embeddedMyMods.EnsureCurrentDetails();
        }

        private void OpenLinkedWorkshopPage(ModProjectRecord record)
        {
            if (record == null)
                return;

            ModProjectRecord saved = _projectLibrary.Load(record.ModId) ?? record;
            if (string.IsNullOrEmpty(saved.WorkshopPublishedFileId))
            {
                TwoPointTheme.ShowMessage(this,
                    string.Equals(saved.WorkshopLinkState, "Missing", StringComparison.OrdinalIgnoreCase)
                        ? "The previously linked Workshop item no longer exists, so there is no active Steam page to open."
                        : "This mod is not currently linked to a Steam Workshop item.",
                    "Workshop Page Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            OpenUrl("https://steamcommunity.com/sharedfiles/filedetails/?id=" + saved.WorkshopPublishedFileId);
        }

        private void RememberWorkshopQuery(WorkshopQueryResult query)
        {
            _lastWorkshopQueryResult = query;
            _lastWorkshopQueryUtc = DateTime.UtcNow;
            _verifiedWorkshopItemsUtc.Clear();
            if (query == null || !query.Success || query.Items == null)
                return;

            DateTime now = DateTime.UtcNow;
            for (int i = 0; i < query.Items.Count; i++)
            {
                WorkshopItemSummary item = query.Items[i];
                if (item != null && !string.IsNullOrEmpty(item.PublishedFileId))
                    _verifiedWorkshopItemsUtc[item.PublishedFileId] = now;
            }
        }

        private void MarkWorkshopItemVerified(string publishedFileId)
        {
            if (string.IsNullOrEmpty(publishedFileId))
                return;
            _verifiedWorkshopItemsUtc[publishedFileId] = DateTime.UtcNow;
        }

        private void InvalidateWorkshopVerification(string publishedFileId)
        {
            if (string.IsNullOrEmpty(publishedFileId))
                return;
            _verifiedWorkshopItemsUtc.Remove(publishedFileId);
        }

        private bool IsWorkshopItemRecentlyVerified(string publishedFileId)
        {
            if (string.IsNullOrEmpty(publishedFileId))
                return false;
            DateTime verifiedUtc;
            if (!_verifiedWorkshopItemsUtc.TryGetValue(publishedFileId, out verifiedUtc))
                return false;
            if (DateTime.UtcNow - verifiedUtc > WorkshopVerificationCacheLifetime)
            {
                _verifiedWorkshopItemsUtc.Remove(publishedFileId);
                return false;
            }
            return true;
        }

        private bool HasRecentWorkshopQuery()
        {
            return _lastWorkshopQueryResult != null && _lastWorkshopQueryResult.Success &&
                DateTime.UtcNow - _lastWorkshopQueryUtc <= WorkshopVerificationCacheLifetime;
        }

        private List<WorkshopItemSummary> GetRecentWorkshopItems()
        {
            if (!HasRecentWorkshopQuery() || _lastWorkshopQueryResult.Items == null)
                return new List<WorkshopItemSummary>();
            return new List<WorkshopItemSummary>(_lastWorkshopQueryResult.Items);
        }

        private static WorkshopItemSummary BuildWorkshopSummary(ModProjectRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.WorkshopPublishedFileId))
                return null;
            return new WorkshopItemSummary
            {
                PublishedFileId = record.WorkshopPublishedFileId,
                Title = string.IsNullOrEmpty(record.WorkshopTitle) ? (record.Name ?? "Workshop Item") : record.WorkshopTitle,
                Description = record.WorkshopDescription ?? record.Description ?? "",
                Visibility = record.WorkshopVisibility ?? "",
                Metadata = record.ModId ?? ""
            };
        }

        private static void EnsureWorkshopItemInList(List<WorkshopItemSummary> items, WorkshopItemSummary required)
        {
            if (items == null || required == null || string.IsNullOrEmpty(required.PublishedFileId))
                return;
            for (int i = 0; i < items.Count; i++)
            {
                WorkshopItemSummary item = items[i];
                if (item != null && string.Equals(item.PublishedFileId, required.PublishedFileId, StringComparison.Ordinal))
                    return;
            }
            items.Add(required);
        }

        private async Task RefreshWorkshopStatusAsync(ModProjectRecord record, bool showResult)
        {
            if (record == null)
                return;

            _state = _state ?? DetectPrerequisites();
            if (_state == null || !_state.UnityFound || !_state.EnvironmentReady)
            {
                TwoPointTheme.ShowMessage(this,
                    "Unity and the private modding environment must be ready before Memento Maker can refresh Steam Workshop status.",
                    "Workshop Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string modId = record.ModId;
            SafeSetMyModsBuildProgress(true, "Refreshing Steam Workshop status...");
            AppendLog("============================================================");
            AppendLog("Steam Workshop status refresh started for ItemModID " + modId + ".");

            try
            {
                WorkshopQueryResult query = await _workshopService.QueryPublishedItemsAsync(
                    _state.UnityExePath,
                    _state.EnvironmentProjectPath,
                    delegate(string message) { SafeSetMyModsBuildProgress(true, message); },
                    delegate(string message) { SafeAppendLog(message); },
                    GetPersistentWorker());

                RememberWorkshopQuery(query);
                ModProjectRecord saved = _projectLibrary.Load(modId) ?? record;
                bool hadActiveLink = !string.IsNullOrEmpty(saved.WorkshopPublishedFileId);
                string checkedId = saved.WorkshopPublishedFileId ?? "";

                if (hadActiveLink)
                    _projectLibrary.UpdateWorkshopLinkFromQuery(saved, query);

                _projectLibrary.ApplyWorkshopQueryState(query, modId);
                ModProjectRecord refreshed = _projectLibrary.Load(modId) ?? saved;

                AppendLog("Steam Workshop status refresh complete for ItemModID " + modId + ".");
                SafeSetMyModsBuildProgress(false, "Steam Workshop status refreshed.");

                if (_embeddedMyMods != null && !_embeddedMyMods.IsDisposed)
                    _embeddedMyMods.RefreshProjects(modId);

                if (showResult)
                {
                    string message;
                    if (string.Equals(refreshed.WorkshopLinkState, "Missing", StringComparison.OrdinalIgnoreCase))
                    {
                        message = "The linked Steam Workshop item" +
                            (string.IsNullOrEmpty(checkedId) ? "" : " (" + checkedId + ")") +
                            " is no longer present. Memento Maker has marked the link as Missing and will not update another item automatically.";
                    }
                    else if (!string.IsNullOrEmpty(refreshed.WorkshopPublishedFileId))
                    {
                        message = "Workshop link verified successfully.\n\nWorkshop ID: " + refreshed.WorkshopPublishedFileId +
                            (string.IsNullOrEmpty(refreshed.WorkshopTitle) ? "" : "\nTitle: " + refreshed.WorkshopTitle);
                    }
                    else
                    {
                        int count = query.Items == null ? 0 : query.Items.Count;
                        message = "This mod is not currently linked to a Workshop item.\n\nSteam returned " + count +
                            " published Two Point Museum item" + (count == 1 ? "." : "s.") +
                            " Use Workshop Tools → Relink Workshop Item if you want to associate an existing item.";
                    }

                    TwoPointTheme.ShowMessage(this, message, "Steam Workshop Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                AppendLog("WORKSHOP STATUS REFRESH ERROR: " + ex.Message);
                SafeSetMyModsBuildProgress(false, "Workshop status refresh failed.");
                TwoPointTheme.ShowMessage(this,
                    "Workshop status could not be refreshed. No links were changed.\n\n" + ex.Message,
                    "Workshop Refresh Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task RelinkWorkshopItemAsync(ModProjectRecord record)
        {
            if (record == null)
                return;

            _state = _state ?? DetectPrerequisites();
            if (_state == null || !_state.UnityFound || !_state.EnvironmentReady)
            {
                TwoPointTheme.ShowMessage(this,
                    "Unity and the private modding environment must be ready before Memento Maker can load your Steam Workshop items.",
                    "Workshop Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SafeSetMyModsBuildProgress(true, "Loading your Steam Workshop items...");
            AppendLog("============================================================");
            AppendLog("Steam Workshop relink list refresh started for ItemModID " + record.ModId + ".");

            try
            {
                WorkshopQueryResult query = await _workshopService.QueryPublishedItemsAsync(
                    _state.UnityExePath,
                    _state.EnvironmentProjectPath,
                    delegate(string message) { SafeSetMyModsBuildProgress(true, message); },
                    delegate(string message) { SafeAppendLog(message); },
                    GetPersistentWorker());

                RememberWorkshopQuery(query);
                _projectLibrary.ApplyWorkshopQueryState(query, record.ModId);
                List<WorkshopItemSummary> allItems = query.Items ?? new List<WorkshopItemSummary>();
                List<WorkshopItemSummary> items = _projectLibrary.FilterWorkshopItemsAvailableForProject(allItems, record.ModId);

                if (allItems.Count == 0)
                {
                    SafeSetMyModsBuildProgress(false, "No Workshop items available to relink.");
                    TwoPointTheme.ShowMessage(this,
                        "Steam did not return any published Two Point Museum Workshop items for the current account.",
                        "No Workshop Items", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (items.Count == 0)
                {
                    SafeSetMyModsBuildProgress(false, "No unlinked Workshop items available.");
                    TwoPointTheme.ShowMessage(this,
                        "All of the Workshop items returned by Steam are already linked to other Memento Maker mods.\n\n" +
                        "A Workshop item can only be linked to one Memento Maker mod at a time. Unlink the item from its current mod first if you want to move that association.",
                        "No Available Workshop Items", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                ModProjectRecord saved = _projectLibrary.Load(record.ModId) ?? record;
                using (WorkshopRelinkForm dialog = new WorkshopRelinkForm(saved, items))
                {
                    SafeSetMyModsBuildProgress(false, "Workshop items loaded.");
                    if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedWorkshopItem == null)
                        return;

                    WorkshopItemSummary selected = dialog.SelectedWorkshopItem;
                    if (string.Equals(saved.WorkshopPublishedFileId, selected.PublishedFileId, StringComparison.Ordinal))
                    {
                        TwoPointTheme.ShowMessage(this,
                            "This Memento Maker mod is already linked to Workshop item " + selected.PublishedFileId + ".",
                            "Already Linked", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    DialogResult confirm = TwoPointTheme.ShowMessage(this,
                        "Relink this Memento Maker mod to the following Steam Workshop item?\n\n" +
                        (selected.Title ?? "") + "\nWorkshop ID: " + selected.PublishedFileId +
                        "\n\nThis changes only Memento Maker's local link. It does NOT upload, overwrite or delete anything on Steam. " +
                        "The Workshop status will show Published (old) until you perform one successful update.",
                        "Confirm Workshop Relink", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (confirm != DialogResult.Yes)
                        return;

                    _projectLibrary.RelinkWorkshopItem(saved, selected);
                    MarkWorkshopItemVerified(selected.PublishedFileId);
                    AppendLog("Workshop relink saved: ItemModID " + saved.ModId + " -> PublishedFileId " + selected.PublishedFileId + ".");
                    if (_embeddedMyMods != null && !_embeddedMyMods.IsDisposed)
                        _embeddedMyMods.RefreshProjects(saved.ModId);

                    TwoPointTheme.ShowMessage(this,
                        "Workshop item linked successfully.\n\nThe mod is marked Published (old) until you use Update Workshop once, so Memento Maker never assumes the existing Steam content already matches the local build.",
                        "Workshop Relinked", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                AppendLog("WORKSHOP RELINK ERROR: " + ex.Message);
                SafeSetMyModsBuildProgress(false, "Workshop relink failed.");
                TwoPointTheme.ShowMessage(this,
                    "Workshop item could not be relinked. Nothing on Steam was changed.\n\n" + ex.Message,
                    "Workshop Relink Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UnlinkWorkshopItem(ModProjectRecord record)
        {
            if (record == null)
                return;

            ModProjectRecord saved = _projectLibrary.Load(record.ModId) ?? record;
            string id = !string.IsNullOrEmpty(saved.WorkshopPublishedFileId)
                ? saved.WorkshopPublishedFileId
                : (saved.WorkshopPreviousPublishedFileId ?? "");

            if (string.IsNullOrEmpty(id) &&
                !string.Equals(saved.WorkshopLinkState, "Missing", StringComparison.OrdinalIgnoreCase))
            {
                TwoPointTheme.ShowMessage(this, "This mod does not currently have a Workshop link to remove.",
                    "No Workshop Link", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult confirm = TwoPointTheme.ShowMessage(this,
                "Remove Memento Maker's local Workshop association" +
                (string.IsNullOrEmpty(id) ? "?" : " to item " + id + "?") +
                "\n\nThis does NOT delete, unpublish or modify anything on Steam. The Workshop item will remain exactly as it is.",
                "Unlink Workshop Item", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
                return;

            _projectLibrary.UnlinkWorkshopItem(saved);
            InvalidateWorkshopVerification(id);
            AppendLog("Workshop link removed locally for ItemModID " + saved.ModId +
                (string.IsNullOrEmpty(id) ? "." : " (previous PublishedFileId " + id + ")."));

            if (_embeddedMyMods != null && !_embeddedMyMods.IsDisposed)
                _embeddedMyMods.RefreshProjects(saved.ModId);

            TwoPointTheme.ShowMessage(this,
                "The local Workshop link has been removed. Nothing was changed on Steam.",
                "Workshop Unlinked", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string ResolveCombinedPackageMode(List<ModProjectRecord> members, ModProjectRecord preferred)
        {
            if (preferred != null)
            {
                string preferredMode = BuildPackageModes.Normalise(preferred.LastBuildPackageMode);
                if (BuildPackageModes.IsCombined(preferredMode))
                    return preferredMode;
                preferredMode = BuildPackageModes.Normalise(preferred.WorkshopPackageMode);
                if (BuildPackageModes.IsCombined(preferredMode))
                    return preferredMode;
            }

            if (members != null)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    ModProjectRecord member = members[i];
                    if (member == null) continue;
                    string mode = BuildPackageModes.Normalise(member.LastBuildPackageMode);
                    if (BuildPackageModes.IsCombined(mode))
                        return mode;
                    mode = BuildPackageModes.Normalise(member.WorkshopPackageMode);
                    if (BuildPackageModes.IsCombined(mode))
                        return mode;
                }
            }
            return BuildPackageModes.Family;
        }

        private static bool IsDecorPackMode(string packageMode)
        {
            return BuildPackageModes.Normalise(packageMode) == BuildPackageModes.DecorPack;
        }

        private ModProjectRecord BuildWorkshopFamilyProxy(List<ModProjectRecord> members, string familyKey, string familyName, ModProjectRecord preferred)
        {
            ModProjectRecord anchor = preferred;
            if (anchor == null && members != null && members.Count > 0) anchor = members[0];
            if (anchor == null) return null;

            ModProjectRecord latest = _projectLibrary.Load(anchor.ModId) ?? anchor;
            string packageMode = ResolveCombinedPackageMode(members, latest);
            bool decorPack = IsDecorPackMode(packageMode);
            ModProjectRecord proxy = new ModProjectRecord();
            proxy.ModId = decorPack
                ? (!string.IsNullOrEmpty(familyKey) && familyKey.StartsWith("decorpack:", StringComparison.OrdinalIgnoreCase)
                    ? familyKey
                    : "decorpack:" + (familyKey ?? latest.ModId ?? "wallpapers"))
                : "family:" + (familyKey ?? latest.ModId ?? "variant");
            proxy.Name = string.IsNullOrWhiteSpace(familyName)
                ? (decorPack ? (latest.LastBuiltFamilyName ?? "Wallpaper Pack") : (latest.Name ?? "Variant Family"))
                : familyName.Trim();
            proxy.Description = latest.Description ?? "";
            proxy.LastBuiltOutputPath = latest.LastBuiltOutputPath ?? "";
            proxy.WorkshopPackageMode = packageMode;
            proxy.WorkshopFamilyKey = familyKey ?? "";
            proxy.WorkshopFamilyName = proxy.Name;

            // The family-level registry is authoritative. Do not require an individual project
            // mirror to be correct before recognising a family that Memento Maker itself published.
            WorkshopFamilyState registry = _projectLibrary.LoadWorkshopFamilyState(familyKey);
            if (registry != null &&
                BuildPackageModes.Normalise(registry.PackageMode) == BuildPackageModes.Normalise(packageMode) &&
                !string.IsNullOrEmpty(registry.PublishedFileId))
            {
                proxy.WorkshopPublishedFileId = registry.PublishedFileId ?? "";
                proxy.WorkshopPreviousPublishedFileId = registry.PreviousPublishedFileId ?? "";
                proxy.WorkshopLinkState = registry.LinkState ?? "Linked";
                proxy.WorkshopLinkNeedsUpdate = registry.LinkNeedsUpdate;
                proxy.WorkshopTitle = registry.Title ?? "";
                proxy.WorkshopDescription = registry.Description ?? "";
                proxy.WorkshopVisibility = registry.Visibility ?? "Private";
                proxy.WorkshopLastChangeNote = registry.LastChangeNote ?? "";
                proxy.WorkshopLastPublishedUtc = registry.LastPublishedUtc ?? "";
                proxy.WorkshopLastUpdatedUtc = registry.LastUpdatedUtc ?? "";
                proxy.WorkshopFamilyMemberIds = registry.MemberIds == null ? new List<string>() : new List<string>(registry.MemberIds);
                return proxy;
            }

            // Compatibility fallback for older family project mirrors whose registry has not yet
            // been created or recovered.
            if (members != null)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    ModProjectRecord candidateSource = members[i];
                    if (candidateSource == null || string.IsNullOrEmpty(candidateSource.ModId)) continue;
                    ModProjectRecord candidate = _projectLibrary.Load(candidateSource.ModId) ?? candidateSource;
                    if (!HasSharedWorkshopFamilyIdentity(candidate, familyKey, packageMode)) continue;
                    proxy.WorkshopPublishedFileId = candidate.WorkshopPublishedFileId ?? "";
                    proxy.WorkshopPreviousPublishedFileId = candidate.WorkshopPreviousPublishedFileId ?? "";
                    proxy.WorkshopLinkState = candidate.WorkshopLinkState ?? "Linked";
                    proxy.WorkshopLinkNeedsUpdate = candidate.WorkshopLinkNeedsUpdate;
                    proxy.WorkshopTitle = candidate.WorkshopTitle ?? "";
                    proxy.WorkshopDescription = candidate.WorkshopDescription ?? "";
                    proxy.WorkshopVisibility = candidate.WorkshopVisibility ?? "Private";
                    proxy.WorkshopLastChangeNote = candidate.WorkshopLastChangeNote ?? "";
                    proxy.WorkshopLastPublishedUtc = candidate.WorkshopLastPublishedUtc ?? "";
                    proxy.WorkshopLastUpdatedUtc = candidate.WorkshopLastUpdatedUtc ?? "";
                    proxy.WorkshopFamilyMemberIds = candidate.WorkshopFamilyMemberIds == null ? new List<string>() : new List<string>(candidate.WorkshopFamilyMemberIds);
                    return proxy;
                }
            }

            // Existing per-item Workshop links are deliberately not the family identity.
            proxy.WorkshopPublishedFileId = "";
            proxy.WorkshopPreviousPublishedFileId = "";
            proxy.WorkshopLinkState = "Unlinked";
            proxy.WorkshopTitle = "";
            proxy.WorkshopDescription = "";
            proxy.WorkshopVisibility = "Private";
            proxy.WorkshopFamilyMemberIds = new List<string>();
            return proxy;
        }

        private static bool HasSharedWorkshopFamilyIdentity(ModProjectRecord record, string familyKey, string packageMode)
        {
            if (record == null || string.IsNullOrEmpty(record.WorkshopPublishedFileId))
                return false;
            if (BuildPackageModes.Normalise(record.WorkshopPackageMode) != BuildPackageModes.Normalise(packageMode))
                return false;
            if (!string.Equals(record.WorkshopFamilyKey ?? "", familyKey ?? "", StringComparison.Ordinal))
                return false;

            // A genuine family Workshop link is only created by SaveWorkshopFamilyState / RelinkWorkshopFamilyItem,
            // both of which persist a family-member snapshot. A legacy per-item Workshop ID may still be present on
            // a project after the family has been combined locally, but it must not be mistaken for the shared family
            // publication. Requiring the snapshot keeps the safe migration path: Publish Family defaults to NEW until
            // the user explicitly publishes/relinks the combined family.
            return record.WorkshopFamilyMemberIds != null && record.WorkshopFamilyMemberIds.Count > 0;
        }

        private static List<string> GetFamilyMemberIds(List<ModProjectRecord> members)
        {
            List<string> ids = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            if (members != null)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    ModProjectRecord member = members[i];
                    if (member != null && !string.IsNullOrEmpty(member.ModId) && seen.Add(member.ModId))
                        ids.Add(member.ModId);
                }
            }
            ids.Sort(StringComparer.Ordinal);
            return ids;
        }

        private bool ValidateCombinedFamilyForWorkshop(List<ModProjectRecord> members, string familyKey, ModProjectRecord selectedRecord,
            out string sharedOutputPath, out string problem)
        {
            sharedOutputPath = "";
            problem = "";
            string packageMode = ResolveCombinedPackageMode(members, selectedRecord);
            bool decorPack = IsDecorPackMode(packageMode);
            string packageLabel = decorPack ? "Décor Pack" : "variant family";
            string rebuildLabel = decorPack ? "Décor Pack" : "family";

            if (members == null || members.Count == 0)
            {
                problem = "The selected " + packageLabel + " has no Memento Maker members.";
                return false;
            }

            List<string> expectedIds = GetFamilyMemberIds(members);
            for (int i = 0; i < members.Count; i++)
            {
                ModProjectRecord source = members[i];
                if (source == null || string.IsNullOrEmpty(source.ModId))
                    continue;
                ModProjectRecord member = _projectLibrary.Load(source.ModId) ?? source;
                if (BuildPackageModes.Normalise(member.LastBuildPackageMode) != packageMode ||
                    !string.Equals(member.LastBuiltFamilyKey ?? "", familyKey ?? "", StringComparison.Ordinal))
                {
                    problem = "'" + (member.Name ?? member.ModId) + "' is not currently installed inside this combined " +
                        packageLabel + " package. Rebuild the " + rebuildLabel + " first.";
                    return false;
                }
                if (member.PendingChanges)
                {
                    problem = "'" + (member.Name ?? member.ModId) + "' has changes that have not been rebuilt. Rebuild the " +
                        rebuildLabel + " before publishing.";
                    return false;
                }
                if (string.IsNullOrEmpty(member.LastBuiltOutputPath) || !Directory.Exists(member.LastBuiltOutputPath))
                {
                    problem = "The combined " + packageLabel + " build folder cannot be found. Rebuild the " + rebuildLabel + " before publishing.";
                    return false;
                }
                if (string.IsNullOrEmpty(sharedOutputPath))
                    sharedOutputPath = member.LastBuiltOutputPath;
                else if (!string.Equals(sharedOutputPath, member.LastBuiltOutputPath, StringComparison.OrdinalIgnoreCase))
                {
                    problem = "The " + packageLabel + " members do not point to one shared build folder. Rebuild the " + rebuildLabel + " before publishing.";
                    return false;
                }

                List<string> builtIds = member.LastBuiltFamilyMemberIds == null
                    ? new List<string>() : new List<string>(member.LastBuiltFamilyMemberIds);
                builtIds.Sort(StringComparer.Ordinal);
                if (builtIds.Count != expectedIds.Count)
                {
                    problem = "The combined build membership no longer matches the current " + packageLabel + ". Rebuild the " + rebuildLabel + " before publishing.";
                    return false;
                }
                for (int j = 0; j < expectedIds.Count; j++)
                {
                    if (!string.Equals(expectedIds[j], builtIds[j], StringComparison.Ordinal))
                    {
                        problem = "The combined build membership no longer matches the current " + packageLabel + ". Rebuild the " + rebuildLabel + " before publishing.";
                        return false;
                    }
                }
            }
            return !string.IsNullOrEmpty(sharedOutputPath);
        }

        private async Task PublishOrUpdateWorkshopFamilyAsync(List<ModProjectRecord> members, string familyKey, string familyName, ModProjectRecord selectedRecord)
        {
            string sharedOutputPath;
            string problem;
            if (!ValidateCombinedFamilyForWorkshop(members, familyKey, selectedRecord, out sharedOutputPath, out problem))
            {
                string packageMode = ResolveCombinedPackageMode(members, selectedRecord);
                TwoPointTheme.ShowMessage(this, problem, IsDecorPackMode(packageMode) ? "Décor Pack Rebuild Required" : "Family Rebuild Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string combinedPackageMode = ResolveCombinedPackageMode(members, selectedRecord);
            bool decorPackMode = IsDecorPackMode(combinedPackageMode);
            _state = _state ?? DetectPrerequisites();
            if (_state == null || !_state.UnityFound || !_state.EnvironmentReady)
            {
                TwoPointTheme.ShowMessage(this, "Unity and the private modding environment must be ready before publishing to Steam Workshop.",
                    "Workshop Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ModProjectRecord proxy = BuildWorkshopFamilyProxy(members, familyKey, familyName, selectedRecord);
            bool hadFamilyLink = proxy != null && !string.IsNullOrEmpty(proxy.WorkshopPublishedFileId);
            List<WorkshopItemSummary> knownItems = _projectLibrary.FilterWorkshopItemsAvailableForFamily(
                GetRecentWorkshopItems(), GetFamilyMemberIds(members));
            bool familyLinkVerified = hadFamilyLink && IsWorkshopItemRecentlyVerified(proxy.WorkshopPublishedFileId);

            if (!hadFamilyLink)
            {
                // New combined uploads should not pay the cost of a Steam query. If the user
                // wants an existing item instead, Workshop Tools → Relink performs an explicit query.
                AppendLog(decorPackMode
                    ? "Décor Pack Workshop preflight skipped: publishing as a new item unless the user explicitly relinks it."
                    : "Family Workshop preflight skipped: publishing as a new item unless the user explicitly relinks it.");
            }
            else if (familyLinkVerified)
            {
                EnsureWorkshopItemInList(knownItems, BuildWorkshopSummary(proxy));
                AppendLog((decorPackMode ? "Décor Pack" : "Family") + " Workshop verification reused from this session: " + proxy.WorkshopPublishedFileId + ".");
            }
            else
            {
                try
                {
                    SafeSetMyModsBuildProgress(true, decorPackMode
                        ? "Verifying linked Décor Pack Workshop item..."
                        : "Verifying linked family Workshop item...");
                    AppendLog("============================================================");
                    AppendLog((decorPackMode ? "BL-022 Décor Pack Workshop verification started for item " : "BL-020 family Workshop verification started for item ") +
                        proxy.WorkshopPublishedFileId + ".");

                    WorkshopQueryResult query = await _workshopService.QueryPublishedItemsAsync(
                        _state.UnityExePath,
                        _state.EnvironmentProjectPath,
                        delegate(string message) { SafeSetMyModsBuildProgress(true, message); },
                        delegate(string message) { SafeAppendLog(message); },
                        GetPersistentWorker());
                    RememberWorkshopQuery(query);

                    string checkedId = proxy.WorkshopPublishedFileId;
                    bool valid = _projectLibrary.UpdateWorkshopFamilyLinkFromQuery(members, familyKey, query);
                    if (!valid)
                    {
                        InvalidateWorkshopVerification(checkedId);
                        TwoPointTheme.ShowMessage(this,
                            (decorPackMode
                                ? "The Steam Workshop item previously linked to this Décor Pack is no longer present.\n\n" +
                                  "The shared Décor Pack link has been marked Missing. You can publish the pack as a NEW Workshop item or explicitly relink one of your existing items."
                                : "The Steam Workshop item previously linked to this variant family is no longer present.\n\n" +
                                  "The shared family link has been marked Missing. You can publish the family as a NEW Workshop item or explicitly relink one of your existing items."),
                            decorPackMode ? "Décor Pack Workshop Item Missing" : "Family Workshop Item Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        MarkWorkshopItemVerified(checkedId);
                    }

                    _projectLibrary.ApplyWorkshopQueryState(query, "");
                    knownItems = _projectLibrary.FilterWorkshopItemsAvailableForFamily(
                        query.Items ?? new List<WorkshopItemSummary>(), GetFamilyMemberIds(members));
                    proxy = BuildWorkshopFamilyProxy(members, familyKey, familyName, selectedRecord);
                }
                catch (Exception ex)
                {
                    AppendLog((decorPackMode ? "DÉCOR PACK WORKSHOP VERIFICATION ERROR: " : "FAMILY WORKSHOP VERIFICATION ERROR: ") + ex.Message);
                    SafeSetMyModsBuildProgress(false, decorPackMode ? "Décor Pack Workshop link could not be verified." : "Family Workshop link could not be verified.");
                    TwoPointTheme.ShowMessage(this,
                        (decorPackMode ? "Memento Maker could not safely verify the linked Décor Pack Workshop item. No update has been attempted.\n\n" : "Memento Maker could not safely verify the linked family Workshop item. No update has been attempted.\n\n") + ex.Message,
                        "Workshop Safety Check Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                finally
                {
                    SafeSetMyModsBuildProgress(false, "Workshop verification complete.");
                }
            }

            // Keep the family parent first so hero-based layouts consistently use the parent/main
            // family item as the hero rather than whichever child happened to launch Publish.
            ModProjectRecord familyPreviewAnchor = members != null && members.Count > 0 ? members[0] : selectedRecord;
            List<string> familyPreviewImages = CollectWorkshopFamilyMemberPreviewPaths(members, familyPreviewAnchor);
            string previewPath = CreateWorkshopFamilyWorkshopPreview(familyPreviewImages);
            if (string.IsNullOrEmpty(previewPath))
            {
                previewPath = ResolveWorkshopDefaultPreview(selectedRecord ?? members[0]);
                if (string.IsNullOrEmpty(previewPath))
                {
                    for (int i = 0; i < members.Count && string.IsNullOrEmpty(previewPath); i++)
                        previewPath = ResolveWorkshopDefaultPreview(members[i]);
                }
            }

            proxy.LastBuiltOutputPath = sharedOutputPath;
            string defaultFamilyPreviewStyle = _settings == null ? "Simple Grid" : (_settings.WorkshopFamilyPreviewStyle ?? "Simple Grid");
            using (WorkshopPublishForm dialog = new WorkshopPublishForm(proxy, knownItems, previewPath, true, familyName, members.Count,
                familyPreviewImages, defaultFamilyPreviewStyle))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Job == null)
                    return;

                if (_settings != null && !string.IsNullOrWhiteSpace(dialog.SelectedFamilyPreviewStyle) &&
                    !string.Equals(_settings.WorkshopFamilyPreviewStyle ?? "", dialog.SelectedFamilyPreviewStyle, StringComparison.OrdinalIgnoreCase))
                {
                    _settings.WorkshopFamilyPreviewStyle = dialog.SelectedFamilyPreviewStyle;
                    try
                    {
                        _settingsService.Save(_settings);
                    }
                    catch (Exception settingsSaveException)
                    {
                        JsonFile.LogReliability("SETTINGS SAVE WARNING: Workshop family preview style could not be saved. " + settingsSaveException);
                        AppendLog("SETTINGS SAVE WARNING: Preview style preference could not be saved. See Logs\\reliability.log.");
                    }
                }

                WorkshopPublishJob job = dialog.Job;
                job.ContentPath = sharedOutputPath;
                job.MementoModId = decorPackMode ? (familyKey ?? "decorpack:wallpapers") : "family:" + (familyKey ?? "variant");
                job.RequiredPublishedFileId = "";
                job.PreviousRequiredPublishedFileId = "";
                job.AdditionalPreviewPaths = BuildFamilyWorkshopAdditionalPreviewPaths(familyPreviewImages, job.PreviewPath);
                job.ReplaceAdditionalPreviews = true;
                job.LegacyItemsToDeprecate = CollectLegacyFamilyWorkshopItems(members, familyKey, job.PublishedFileId, _lastWorkshopQueryResult);

                if (!string.IsNullOrEmpty(job.PublishedFileId))
                {
                    try { _projectLibrary.EnsureWorkshopItemAvailableForFamily(job.PublishedFileId, GetFamilyMemberIds(members)); }
                    catch (Exception conflict)
                    {
                        TwoPointTheme.ShowMessage(this, conflict.Message + "\n\nNo Steam content has been changed.",
                            "Workshop Item Already Linked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

                AppendLog("============================================================");
                AppendLog(string.IsNullOrEmpty(job.PublishedFileId)
                    ? (decorPackMode ? "BL-022 Décor Pack Workshop publish started." : "BL-020 family Workshop publish started.")
                    : (decorPackMode ? "BL-022 Décor Pack Workshop update started for item " : "BL-020 family Workshop update started for item ") + job.PublishedFileId + ".");
                SafeSetMyModsBuildProgress(true, string.IsNullOrEmpty(job.PublishedFileId)
                    ? (decorPackMode ? "Publishing Décor Pack to Steam Workshop..." : "Publishing variant family to Steam Workshop...")
                    : (decorPackMode ? "Updating Décor Pack Workshop item..." : "Updating family Workshop item..."));
                try
                {
                    WorkshopPublishResult result = await _workshopService.PublishOrUpdateAsync(
                        _state.UnityExePath,
                        _state.EnvironmentProjectPath,
                        job,
                        delegate(string message) { SafeSetMyModsBuildProgress(true, message); },
                        delegate(string message) { SafeAppendLog(message); },
                        GetPersistentWorker());

                    _projectLibrary.SaveWorkshopFamilyState(members, familyKey, familyName, job, result, job.PreviewPath);
                    MarkWorkshopItemVerified(result.PublishedFileId);
                    List<ModProjectRecord> repairedFamilyWorkshopMembers = _projectLibrary.SynchroniseWorkshopFamilyStateToMembers(members, familyKey);
                    WorkshopFamilyState persistedFamilyWorkshopState = _projectLibrary.LoadWorkshopFamilyState(familyKey);
                    AppendLog((decorPackMode ? "BL-022 Décor Pack Workshop identity persisted: " : "BL-020 family Workshop identity persisted: ") +
                        (persistedFamilyWorkshopState == null ? "NO REGISTRY" : (persistedFamilyWorkshopState.PublishedFileId ?? "<empty>")) +
                        "; mirrored to " + repairedFamilyWorkshopMembers.Count + " project record(s).");
                    AppendLog(result.Message);
                    SafeSetMyModsBuildProgress(false, result.CreatedNew
                        ? (decorPackMode ? "Décor Pack published to Steam Workshop." : "Family published to Steam Workshop.")
                        : (decorPackMode ? "Décor Pack Workshop update complete." : "Family Workshop update complete."));

                    string resultMessage = (result.CreatedNew
                        ? (decorPackMode ? "Décor Pack published successfully." : "Variant family published successfully.")
                        : (decorPackMode ? "Décor Pack Workshop item updated successfully." : "Variant family Workshop item updated successfully.")) +
                        "\n\nWorkshop ID: " + result.PublishedFileId + (decorPackMode ? "\nPack members: " : "\nFamily members: ") + members.Count;
                    string caption = result.CreatedNew
                        ? (decorPackMode ? "Décor Pack Workshop Published" : "Family Workshop Published")
                        : (decorPackMode ? "Décor Pack Workshop Updated" : "Family Workshop Updated");
                    if (result.NeedsLegalAgreement)
                    {
                        resultMessage += "\n\nSteam requires you to accept the Workshop Legal Agreement before the item can become publicly visible. Open the agreement now?";
                        DialogResult legal = TwoPointTheme.ShowMessage(this, resultMessage, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                        if (legal == DialogResult.Yes)
                            OpenUrl("https://steamcommunity.com/sharedfiles/workshoplegalagreement");
                    }
                    else
                    {
                        DialogResult open = TwoPointTheme.ShowMessage(this,
                            resultMessage + "\n\nOpen the shared Steam Workshop page now?",
                            caption, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                        if (open == DialogResult.Yes)
                            OpenUrl("steam://url/CommunityFilePage/" + result.PublishedFileId);
                    }
                    if (_embeddedMyMods != null && !_embeddedMyMods.IsDisposed)
                        _embeddedMyMods.RefreshProjects(selectedRecord == null ? members[0].ModId : selectedRecord.ModId);
                }
                catch (Exception ex)
                {
                    AppendLog((decorPackMode ? "DÉCOR PACK WORKSHOP PUBLISH ERROR: " : "FAMILY WORKSHOP PUBLISH ERROR: ") + ex.Message);
                    if (!string.IsNullOrEmpty(job.PublishedFileId) &&
                        ex.Message.IndexOf("WORKSHOP LINK SAFETY CHECK FAILED", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        InvalidateWorkshopVerification(job.PublishedFileId);
                    }
                    SafeSetMyModsBuildProgress(false, decorPackMode ? "Décor Pack Workshop publish/update failed." : "Family Workshop publish/update failed.");
                    TwoPointTheme.ShowMessage(this, ex.Message, decorPackMode ? "Décor Pack Steam Workshop Publish Failed" : "Family Steam Workshop Publish Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async Task RefreshWorkshopFamilyStatusAsync(List<ModProjectRecord> members, string familyKey, bool showResult, ModProjectRecord selectedRecord)
        {
            string packageMode = ResolveCombinedPackageMode(members, selectedRecord);
            bool decorPackMode = IsDecorPackMode(packageMode);

            _state = _state ?? DetectPrerequisites();
            if (_state == null || !_state.UnityFound || !_state.EnvironmentReady)
            {
                TwoPointTheme.ShowMessage(this, "Unity and the private modding environment must be ready before Memento Maker can refresh Steam Workshop status.",
                    "Workshop Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Refresh is deliberately verification-only. It must never create a new local
            // Workshop association. A user who explicitly unlinks a family/pack should be
            // able to refresh Steam immediately without Memento Maker reconstructing that
            // link from old publish jobs, metadata, titles, or previous Workshop history.
            ModProjectRecord beforeRefresh = BuildWorkshopFamilyProxy(members, familyKey, null, selectedRecord);
            bool hadActiveLocalLink = beforeRefresh != null && !string.IsNullOrEmpty(beforeRefresh.WorkshopPublishedFileId);
            string checkedPublishedFileId = hadActiveLocalLink ? (beforeRefresh.WorkshopPublishedFileId ?? "") : "";

            SafeSetMyModsBuildProgress(true, decorPackMode ? "Refreshing Décor Pack Workshop status..." : "Refreshing family Workshop status...");
            try
            {
                WorkshopQueryResult query = await _workshopService.QueryPublishedItemsAsync(
                    _state.UnityExePath, _state.EnvironmentProjectPath,
                    delegate(string message) { SafeSetMyModsBuildProgress(true, message); },
                    delegate(string message) { SafeAppendLog(message); }, GetPersistentWorker());
                RememberWorkshopQuery(query);

                bool linked = false;
                if (hadActiveLocalLink)
                    linked = _projectLibrary.UpdateWorkshopFamilyLinkFromQuery(members, familyKey, query);

                // Keep the normal per-project status refresh for unrelated standalone mods,
                // but do not use Workshop discovery as an implicit Relink operation.
                _projectLibrary.ApplyWorkshopQueryState(query, "");
                SafeSetMyModsBuildProgress(false, decorPackMode ? "Décor Pack Workshop status refreshed." : "Family Workshop status refreshed.");
                if (_embeddedMyMods != null && !_embeddedMyMods.IsDisposed)
                    _embeddedMyMods.RefreshProjects(selectedRecord == null ? null : selectedRecord.ModId);

                if (showResult)
                {
                    ModProjectRecord refreshed = selectedRecord == null ? null : _projectLibrary.Load(selectedRecord.ModId);
                    ModProjectRecord localProxy = BuildWorkshopFamilyProxy(members, familyKey, null, selectedRecord);
                    string message;

                    if (!hadActiveLocalLink)
                    {
                        int count = query.Items == null ? 0 : query.Items.Count;
                        message = (decorPackMode ? "This Décor Pack" : "This family") +
                            " is not currently linked to a Workshop item.\n\nSteam returned " + count +
                            " published Two Point Museum item" + (count == 1 ? "." : "s.") +
                            " Use Workshop Tools → Relink Workshop Item if you want to associate an existing item.";
                    }
                    else if (linked && refreshed != null && !string.IsNullOrEmpty(refreshed.WorkshopPublishedFileId))
                    {
                        message = (decorPackMode ? "Décor Pack Workshop link verified successfully.\n\nWorkshop ID: " : "Family Workshop link verified successfully.\n\nWorkshop ID: ") + refreshed.WorkshopPublishedFileId +
                            (string.IsNullOrEmpty(refreshed.WorkshopTitle) ? "" : "\nTitle: " + refreshed.WorkshopTitle);
                    }
                    else if (localProxy != null && !string.IsNullOrEmpty(localProxy.WorkshopPublishedFileId))
                    {
                        // Immediately after a successful publish Steam can briefly omit the new
                        // item from the account list. UpdateWorkshopFamilyLinkFromQuery preserves
                        // such a very recent active link; refresh should report that fact, not
                        // replace or rediscover it.
                        message = (decorPackMode
                            ? "The shared Décor Pack Workshop link is saved locally, but Steam has not returned the item in the latest Workshop-list sync yet.\n\n"
                            : "The shared family Workshop link is saved locally, but Steam has not returned the item in the latest Workshop-list sync yet.\n\n") +
                            "Workshop ID: " + localProxy.WorkshopPublishedFileId +
                            (decorPackMode
                                ? "\n\nMemento Maker has kept the Décor Pack link intact and will verify it again on a later refresh."
                                : "\n\nMemento Maker has kept the family link intact and will verify it again on a later refresh.");
                    }
                    else
                    {
                        message = "The linked Steam Workshop item" +
                            (string.IsNullOrEmpty(checkedPublishedFileId) ? "" : " (" + checkedPublishedFileId + ")") +
                            " is no longer present in the latest Workshop-list sync. Memento Maker has not linked this " +
                            (decorPackMode ? "Décor Pack" : "family") +
                            " to anything else automatically. Use Workshop Tools → Relink Workshop Item if you want to choose another item.";
                    }

                    TwoPointTheme.ShowMessage(this, message,
                        decorPackMode ? "Décor Pack Steam Workshop Status" : "Family Steam Workshop Status",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                SafeSetMyModsBuildProgress(false, decorPackMode ? "Décor Pack Workshop refresh failed." : "Family Workshop refresh failed.");
                TwoPointTheme.ShowMessage(this, ex.Message, decorPackMode ? "Décor Pack Workshop Refresh Failed" : "Family Workshop Refresh Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RelinkWorkshopFamilyItemFromQuery(List<ModProjectRecord> members, string familyKey, string familyName,
            ModProjectRecord selectedRecord, WorkshopQueryResult query)
        {
            string packageMode = ResolveCombinedPackageMode(members, selectedRecord);
            bool decorPackMode = IsDecorPackMode(packageMode);
            List<WorkshopItemSummary> items = _projectLibrary.FilterWorkshopItemsAvailableForFamily(
                query == null ? new List<WorkshopItemSummary>() : (query.Items ?? new List<WorkshopItemSummary>()), GetFamilyMemberIds(members));
            if (items.Count == 0)
            {
                TwoPointTheme.ShowMessage(this, decorPackMode ? "No Workshop items are available to link to this Décor Pack." : "No Workshop items are available to link to this family.",
                    "No Available Workshop Items", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ModProjectRecord proxy = BuildWorkshopFamilyProxy(members, familyKey, familyName, selectedRecord);
            using (WorkshopRelinkForm dialog = new WorkshopRelinkForm(proxy, items, familyName))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedWorkshopItem == null)
                    return;
                WorkshopItemSummary selected = dialog.SelectedWorkshopItem;
                DialogResult confirm = TwoPointTheme.ShowMessage(this,
                    (decorPackMode
                        ? "Link the COMPLETE Décor Pack '" + (familyName ?? "Wallpaper Pack") + "' to this Workshop item?\n\n"
                        : "Link the COMPLETE '" + (familyName ?? "Variant") + "' family to this Workshop item?\n\n") +
                    (selected.Title ?? "") + "\nWorkshop ID: " + selected.PublishedFileId +
                    (decorPackMode
                        ? "\n\nThis changes only Memento Maker's local Décor Pack link. It does not upload or delete anything on Steam. The pack will show Published (old) until one successful update is submitted."
                        : "\n\nThis changes only Memento Maker's local family link. It does not upload or delete anything on Steam. The family will show Published (old) until one successful family update is submitted."),
                    decorPackMode ? "Confirm Décor Pack Workshop Relink" : "Confirm Family Workshop Relink", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes) return;
                _projectLibrary.RelinkWorkshopFamilyItem(members, familyKey, familyName, selected);
                MarkWorkshopItemVerified(selected.PublishedFileId);
                if (_embeddedMyMods != null && !_embeddedMyMods.IsDisposed)
                    _embeddedMyMods.RefreshProjects(selectedRecord == null ? null : selectedRecord.ModId);
                TwoPointTheme.ShowMessage(this,
                    decorPackMode
                        ? "The Décor Pack Workshop link was saved. Use Update Workshop to make the Steam content current."
                        : "The family Workshop link was saved. Use Update Workshop to make the Steam content current.",
                    decorPackMode ? "Décor Pack Workshop Relinked" : "Family Workshop Relinked", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private async Task RelinkWorkshopFamilyItemAsync(List<ModProjectRecord> members, string familyKey, string familyName, ModProjectRecord selectedRecord)
        {
            string packageMode = ResolveCombinedPackageMode(members, selectedRecord);
            bool decorPackMode = IsDecorPackMode(packageMode);
            _state = _state ?? DetectPrerequisites();
            if (_state == null || !_state.UnityFound || !_state.EnvironmentReady)
            {
                TwoPointTheme.ShowMessage(this, "Unity and the private modding environment must be ready before Memento Maker can load your Steam Workshop items.",
                    "Workshop Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SafeSetMyModsBuildProgress(true, decorPackMode ? "Loading Workshop items for Décor Pack..." : "Loading Workshop items for variant family...");
            try
            {
                WorkshopQueryResult query = await _workshopService.QueryPublishedItemsAsync(
                    _state.UnityExePath, _state.EnvironmentProjectPath,
                    delegate(string message) { SafeSetMyModsBuildProgress(true, message); },
                    delegate(string message) { SafeAppendLog(message); }, GetPersistentWorker());
                RememberWorkshopQuery(query);
                _projectLibrary.ApplyWorkshopQueryState(query, "");
                List<WorkshopItemSummary> items = _projectLibrary.FilterWorkshopItemsAvailableForFamily(
                    query.Items ?? new List<WorkshopItemSummary>(), GetFamilyMemberIds(members));
                if (items.Count == 0)
                {
                    SafeSetMyModsBuildProgress(false, "No Workshop items available.");
                    TwoPointTheme.ShowMessage(this, decorPackMode ? "No Workshop items are available to link to this Décor Pack." : "No Workshop items are available to link to this family.",
                        "No Available Workshop Items", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                ModProjectRecord proxy = BuildWorkshopFamilyProxy(members, familyKey, familyName, selectedRecord);
                using (WorkshopRelinkForm dialog = new WorkshopRelinkForm(proxy, items, familyName))
                {
                    SafeSetMyModsBuildProgress(false, "Workshop items loaded.");
                    if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedWorkshopItem == null)
                        return;
                    WorkshopItemSummary selected = dialog.SelectedWorkshopItem;
                    DialogResult confirm = TwoPointTheme.ShowMessage(this,
                        (decorPackMode
                            ? "Link the COMPLETE Décor Pack '" + (familyName ?? "Wallpaper Pack") + "' to this Workshop item?\n\n"
                            : "Link the COMPLETE '" + (familyName ?? "Variant") + "' family to this Workshop item?\n\n") +
                        (selected.Title ?? "") + "\nWorkshop ID: " + selected.PublishedFileId +
                        (decorPackMode
                            ? "\n\nThis changes only Memento Maker's local Décor Pack link. It does not upload or delete anything on Steam. The pack will show Published (old) until one successful update is submitted."
                            : "\n\nThis changes only Memento Maker's local family link. It does not upload or delete anything on Steam. The family will show Published (old) until one successful family update is submitted."),
                        decorPackMode ? "Confirm Décor Pack Workshop Relink" : "Confirm Family Workshop Relink", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (confirm != DialogResult.Yes) return;
                    _projectLibrary.RelinkWorkshopFamilyItem(members, familyKey, familyName, selected);
                    MarkWorkshopItemVerified(selected.PublishedFileId);
                    if (_embeddedMyMods != null && !_embeddedMyMods.IsDisposed)
                        _embeddedMyMods.RefreshProjects(selectedRecord == null ? null : selectedRecord.ModId);
                    TwoPointTheme.ShowMessage(this,
                        decorPackMode ? "The Décor Pack Workshop link was saved. Use Update Workshop to make the Steam content current." : "The family Workshop link was saved. Use Update Workshop to make the Steam content current.",
                        decorPackMode ? "Décor Pack Workshop Relinked" : "Family Workshop Relinked", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                SafeSetMyModsBuildProgress(false, decorPackMode ? "Décor Pack Workshop relink failed." : "Family Workshop relink failed.");
                TwoPointTheme.ShowMessage(this, ex.Message, decorPackMode ? "Décor Pack Workshop Relink Failed" : "Family Workshop Relink Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UnlinkWorkshopFamilyItem(List<ModProjectRecord> members, string familyKey, string familyName, ModProjectRecord selectedRecord)
        {
            string packageMode = ResolveCombinedPackageMode(members, selectedRecord);
            bool decorPackMode = IsDecorPackMode(packageMode);
            ModProjectRecord proxy = BuildWorkshopFamilyProxy(members, familyKey, familyName, selectedRecord);
            string activeId = proxy == null ? "" : (proxy.WorkshopPublishedFileId ?? "");
            bool missingLink = proxy != null && string.Equals(proxy.WorkshopLinkState, "Missing", StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(activeId) && !missingLink)
            {
                TwoPointTheme.ShowMessage(this,
                    decorPackMode ? "This Décor Pack does not currently have a Workshop link to remove." : "This family does not currently have a Workshop link to remove.",
                    "No Workshop Link", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string id = !string.IsNullOrEmpty(activeId)
                ? activeId
                : (proxy == null ? "" : (proxy.WorkshopPreviousPublishedFileId ?? ""));
            DialogResult confirm = TwoPointTheme.ShowMessage(this,
                (decorPackMode
                    ? "Remove Memento Maker's shared Workshop association for the COMPLETE Décor Pack '" + (familyName ?? "Wallpaper Pack") + "'"
                    : "Remove Memento Maker's shared Workshop association for the COMPLETE '" + (familyName ?? "Variant") + "' family") +
                (string.IsNullOrEmpty(id) ? "?" : " (Workshop ID " + id + ")?") +
                "\n\nThis does NOT delete, unpublish or modify anything on Steam.",
                decorPackMode ? "Unlink Décor Pack Workshop Item" : "Unlink Family Workshop Item", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            try
            {
                _projectLibrary.UnlinkWorkshopFamilyItem(members, familyKey);
                InvalidateWorkshopVerification(id);
                AppendLog((decorPackMode ? "Décor Pack" : "Family") + " Workshop link removed locally for key " + (familyKey ?? "") +
                    (string.IsNullOrEmpty(id) ? "." : " (previous PublishedFileId " + id + ")."));
                if (_embeddedMyMods != null && !_embeddedMyMods.IsDisposed)
                    _embeddedMyMods.RefreshProjects(selectedRecord == null ? null : selectedRecord.ModId);
                TwoPointTheme.ShowMessage(this,
                    decorPackMode ? "The shared Décor Pack Workshop link has been removed locally. Nothing was changed on Steam." : "The shared family Workshop link has been removed locally. Nothing was changed on Steam.",
                    decorPackMode ? "Décor Pack Workshop Unlinked" : "Family Workshop Unlinked", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppendLog((decorPackMode ? "DECOR PACK" : "FAMILY") + " WORKSHOP UNLINK ERROR: " + ex.Message);
                if (_embeddedMyMods != null && !_embeddedMyMods.IsDisposed)
                    _embeddedMyMods.RefreshProjects(selectedRecord == null ? null : selectedRecord.ModId);
                TwoPointTheme.ShowMessage(this,
                    "Memento Maker could not remove the local Workshop link completely. Nothing was changed on Steam.\n\n" + ex.Message,
                    decorPackMode ? "Décor Pack Workshop Unlink Failed" : "Family Workshop Unlink Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task PublishOrUpdateWorkshopAsync(ModProjectRecord record)
        {
            if (record == null)
                return;
            if (record.PendingChanges)
            {
                TwoPointTheme.ShowMessage(this, "This mod has saved changes that have not been rebuilt yet. Rebuild the mod before publishing so Steam receives the current version.",
                    "Rebuild Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrEmpty(record.LastBuiltOutputPath) || !Directory.Exists(record.LastBuiltOutputPath))
            {
                TwoPointTheme.ShowMessage(this, "The built mod folder cannot be found. Rebuild the mod before publishing it to Steam Workshop.",
                    "Built Mod Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _state = _state ?? DetectPrerequisites();
            if (_state == null || !_state.UnityFound || !_state.EnvironmentReady)
            {
                TwoPointTheme.ShowMessage(this, "Unity and the private modding environment must be ready before publishing to Steam Workshop.",
                    "Workshop Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<WorkshopItemSummary> knownItems = _projectLibrary.FilterWorkshopItemsAvailableForProject(GetRecentWorkshopItems(), record.ModId);
            bool hadStoredLink = !string.IsNullOrEmpty(record.WorkshopPublishedFileId);
            bool workshopSyncSucceeded = false;
            bool requiresWorkshopParent = VariantModes.Normalise(record.VariantMode) == VariantModes.Modded;
            ModProjectRecord parentForVerification = requiresWorkshopParent ? _projectLibrary.Load(record.VariantParentModId) : null;

            // Protect projects that may already contain a duplicate local Workshop link.
            // Never allow an update until the duplicate association is resolved explicitly.
            if (hadStoredLink)
            {
                ModProjectRecord duplicateOwner = _projectLibrary.FindWorkshopLinkOwner(record.WorkshopPublishedFileId, record.ModId);
                if (duplicateOwner != null)
                {
                    string ownerName = string.IsNullOrEmpty(duplicateOwner.Name) ? duplicateOwner.ModId : duplicateOwner.Name;
                    TwoPointTheme.ShowMessage(this,
                        "Workshop item " + record.WorkshopPublishedFileId + " is also linked to another Memento Maker mod:\n\n" +
                        ownerName + " (" + duplicateOwner.ModId + ")\n\n" +
                        "For safety, Memento Maker will not update this Workshop item while two projects point to it. Use Workshop Tools → Unlink Workshop Item on one of the mods, then try again.",
                        "Duplicate Workshop Link", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            bool storedLinkVerified = hadStoredLink && IsWorkshopItemRecentlyVerified(record.WorkshopPublishedFileId);
            bool parentHasActiveLink = requiresWorkshopParent && parentForVerification != null &&
                !string.IsNullOrEmpty(parentForVerification.WorkshopPublishedFileId) &&
                !string.Equals(parentForVerification.WorkshopLinkState, "Missing", StringComparison.OrdinalIgnoreCase);
            bool parentLinkVerified = !requiresWorkshopParent ||
                (parentHasActiveLink && IsWorkshopItemRecentlyVerified(parentForVerification.WorkshopPublishedFileId));
            bool needsWorkshopVerification = (hadStoredLink && !storedLinkVerified) || (parentHasActiveLink && !parentLinkVerified);

            if (!needsWorkshopVerification)
            {
                workshopSyncSucceeded = hadStoredLink ? storedLinkVerified : (!requiresWorkshopParent || parentLinkVerified);
                if (storedLinkVerified)
                {
                    EnsureWorkshopItemInList(knownItems, BuildWorkshopSummary(record));
                    AppendLog("Workshop link verification reused from this session: " + record.WorkshopPublishedFileId + ".");
                }
                if (requiresWorkshopParent && parentLinkVerified)
                    AppendLog("Parent Workshop link verification reused from this session: " + parentForVerification.WorkshopPublishedFileId + ".");
                if (!hadStoredLink && !requiresWorkshopParent)
                    AppendLog("Workshop preflight skipped: this is a new publish and no Steam-side link verification is required.");
            }
            else
            {
                try
                {
                    string verificationMessage = hadStoredLink
                        ? "Verifying linked Workshop item..."
                        : "Verifying parent Workshop item...";
                    SafeSetMyModsBuildProgress(true, verificationMessage);
                    AppendLog("============================================================");
                    AppendLog(hadStoredLink
                        ? "Steam Workshop exact-link verification started for item " + record.WorkshopPublishedFileId + "."
                        : "Steam Workshop parent-link verification started for Publish.");
                    WorkshopQueryResult query = await _workshopService.QueryPublishedItemsAsync(
                        _state.UnityExePath,
                        _state.EnvironmentProjectPath,
                        delegate(string message) { SafeSetMyModsBuildProgress(true, message); },
                        delegate(string message) { SafeAppendLog(message); },
                        GetPersistentWorker());
                    RememberWorkshopQuery(query);
                    workshopSyncSucceeded = query != null && query.Success;
                    knownItems = _projectLibrary.FilterWorkshopItemsAvailableForProject(query.Items ?? new List<WorkshopItemSummary>(), record.ModId);

                    if (hadStoredLink)
                    {
                        string checkedId = record.WorkshopPublishedFileId;
                        bool stillLinked = _projectLibrary.UpdateWorkshopLinkFromQuery(record, query);
                        if (!stillLinked)
                        {
                            InvalidateWorkshopVerification(checkedId);
                            AppendLog("WORKSHOP LINK MISSING: PublishedFileId " + checkedId + " is no longer present in the current Steam user's published Two Point Museum Workshop items.");
                            TwoPointTheme.ShowMessage(this,
                                "The Steam Workshop item previously linked to this mod (" + checkedId + ") no longer exists.\n\n" +
                                "Memento Maker has disabled that old link so it cannot be updated accidentally. You can now publish this mod as a NEW Workshop item, or explicitly relink one of your existing Workshop items.",
                                "Workshop Item Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        else
                        {
                            MarkWorkshopItemVerified(checkedId);
                            AppendLog("Workshop link verified successfully: " + checkedId + ".");
                        }
                    }
                    _projectLibrary.ApplyWorkshopQueryState(query, record.ModId);
                    _myModsNeedsRefresh = true;
                }
                catch (Exception ex)
                {
                    AppendLog("WORKSHOP LINK VERIFICATION ERROR: " + ex.Message);
                    if (requiresWorkshopParent)
                    {
                        SafeSetMyModsBuildProgress(false, "Parent Workshop item could not be verified.");
                        TwoPointTheme.ShowMessage(this,
                            "This mod is a variant of another Memento Maker mod, so its parent Workshop item must be verified before publishing.\n\n" +
                            ex.Message + "\n\nNo Workshop upload was attempted.",
                            "Parent Workshop Item Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    if (hadStoredLink)
                    {
                        SafeSetMyModsBuildProgress(false, "Workshop link could not be verified.");
                        TwoPointTheme.ShowMessage(this,
                            "Memento Maker could not safely verify the linked Steam Workshop item. No update has been attempted.\n\n" + ex.Message +
                            "\n\nTry again when Steam is available. The existing Workshop item will not be modified until its exact PublishedFileId can be verified.",
                            "Workshop Safety Check Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                finally
                {
                    SafeSetMyModsBuildProgress(false, "Workshop verification complete.");
                }
            }

            string requiredPublishedFileId = "";
            if (requiresWorkshopParent)
            {
                ModProjectRecord parentRecord = _projectLibrary.Load(record.VariantParentModId);
                if (!workshopSyncSucceeded || parentRecord == null || string.IsNullOrEmpty(parentRecord.WorkshopPublishedFileId) ||
                    string.Equals(parentRecord.WorkshopLinkState, "Missing", StringComparison.OrdinalIgnoreCase))
                {
                    string parentName = parentRecord == null ? (record.VariantParentModId ?? "the selected parent") :
                        (string.IsNullOrEmpty(parentRecord.Name) ? parentRecord.ModId : parentRecord.Name);

                    if (parentRecord == null)
                    {
                        TwoPointTheme.ShowMessage(this,
                            "This mod is a variant of '" + parentName + "', but that parent project can no longer be found in Memento Maker.\n\n" +
                            "Choose a different parent or convert this mod to Standalone before publishing.",
                            "Parent Mod Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    DialogResult publishParent = TwoPointTheme.ShowMessage(this,
                        "This mod is a variant of '" + parentName + "'.\n\n" +
                        "The parent must be published or relinked to Steam Workshop first so Memento Maker can add it as a Required Item.\n\n" +
                        "Choose OK to open the parent mod's Workshop publish details now. After the parent is published, Memento Maker will return to this child automatically.",
                        "Publish Parent Mod First", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                    if (publishParent != DialogResult.OK)
                        return;

                    await PublishOrUpdateWorkshopAsync(parentRecord);

                    // Reload the parent because the publish flow saves the PublishedFileId into
                    // the project record only after Steam confirms success. If the user cancelled
                    // the parent dialog or publishing failed, leave the child untouched.
                    parentRecord = _projectLibrary.Load(record.VariantParentModId);
                    if (parentRecord == null || string.IsNullOrEmpty(parentRecord.WorkshopPublishedFileId) ||
                        string.Equals(parentRecord.WorkshopLinkState, "Missing", StringComparison.OrdinalIgnoreCase))
                    {
                        SafeSetMyModsBuildProgress(false, "Parent Workshop publish was not completed.");
                        return;
                    }

                    // Re-open the original child flow now that the dependency has a real
                    // PublishedFileId. This performs a fresh Workshop sync and then loads the
                    // child's publish details exactly as if the user had selected it again.
                    await PublishOrUpdateWorkshopAsync(record);
                    return;
                }
                requiredPublishedFileId = parentRecord.WorkshopPublishedFileId;
            }

            string previewPath = ResolveWorkshopDefaultPreview(record);

            using (WorkshopPublishForm dialog = new WorkshopPublishForm(record, knownItems, previewPath))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Job == null)
                    return;

                WorkshopPublishJob job = dialog.Job;
                job.RequiredPublishedFileId = requiredPublishedFileId;
                job.PreviousRequiredPublishedFileId = record.WorkshopDependencyPublishedFileId ?? "";

                // Re-check local ownership immediately before Steam is called. This
                // covers the unlikely case where another Memento Maker project was
                // linked while this publish dialog was open.
                if (!string.IsNullOrEmpty(job.PublishedFileId))
                {
                    try
                    {
                        _projectLibrary.EnsureWorkshopItemAvailableForProject(job.PublishedFileId, record.ModId);
                    }
                    catch (Exception conflict)
                    {
                        TwoPointTheme.ShowMessage(this, conflict.Message + "\n\nNo Steam content has been changed.",
                            "Workshop Item Already Linked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

                AppendLog("============================================================");
                AppendLog(string.IsNullOrEmpty(job.PublishedFileId) ? "Steam Workshop publish started." : "Steam Workshop update started for item " + job.PublishedFileId + ".");
                SafeSetMyModsBuildProgress(true, string.IsNullOrEmpty(job.PublishedFileId) ? "Publishing mod to Steam Workshop..." : "Updating Steam Workshop item...");
                try
                {
                    WorkshopPublishResult result = await _workshopService.PublishOrUpdateAsync(
                        _state.UnityExePath,
                        _state.EnvironmentProjectPath,
                        job,
                        delegate(string message) { SafeSetMyModsBuildProgress(true, message); },
                        delegate(string message) { SafeAppendLog(message); },
                        GetPersistentWorker());

                    _projectLibrary.SaveWorkshopState(record, job, result, job.PreviewPath);
                    MarkWorkshopItemVerified(result.PublishedFileId);
                    AppendLog(result.Message);
                    SafeSetMyModsBuildProgress(false, result.CreatedNew ? "Published to Steam Workshop." : "Steam Workshop update complete.");

                    string resultMessage =
                        (result.CreatedNew ? "Workshop item published successfully." : "Workshop item updated successfully.") +
                        "\n\nWorkshop ID: " + result.PublishedFileId;
                    if (!string.IsNullOrEmpty(result.DependencyPublishedFileId))
                        resultMessage += "\nRequired Item: " + result.DependencyPublishedFileId;
                    string resultCaption = result.CreatedNew ? "Workshop Published" : "Workshop Updated";
                    if (result.NeedsLegalAgreement)
                    {
                        resultMessage += "\n\nSteam requires you to accept the Workshop Legal Agreement before the item can become publicly visible. Open the agreement now?";
                        DialogResult legal = TwoPointTheme.ShowMessage(this, resultMessage, resultCaption, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                        if (legal == DialogResult.Yes)
                            OpenUrl("https://steamcommunity.com/sharedfiles/workshoplegalagreement");
                    }
                    else
                    {
                        DialogResult open = TwoPointTheme.ShowMessage(this,
                            resultMessage + "\n\nOpen the Steam Workshop page for '" + (record.Name ?? "this mod") + "' now?\n\nNo additional Workshop changes will be made.",
                            resultCaption, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                        if (open == DialogResult.Yes)
                            OpenUrl("steam://url/CommunityFilePage/" + result.PublishedFileId);
                    }
                }
                catch (Exception ex)
                {
                    AppendLog("WORKSHOP PUBLISH ERROR: " + ex.Message);
                    bool linkSafetyFailure = !string.IsNullOrEmpty(job.PublishedFileId) &&
                        ex.Message.IndexOf("WORKSHOP LINK SAFETY CHECK FAILED", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (linkSafetyFailure)
                    {
                        _projectLibrary.MarkWorkshopLinkMissing(record, job.PublishedFileId);
                        InvalidateWorkshopVerification(job.PublishedFileId);
                        _myModsNeedsRefresh = true;
                        SafeSetMyModsBuildProgress(false, "Workshop item is missing. No update was attempted.");
                    }
                    else
                    {
                        SafeSetMyModsBuildProgress(false, "Workshop publish/update failed.");
                    }
                    TwoPointTheme.ShowMessage(this, ex.Message, linkSafetyFailure ? "Workshop Item Missing" : "Steam Workshop Publish Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static void OpenUrl(string url)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(url);
                info.UseShellExecute = true;
                Process.Start(info);
            }
            catch { }
        }
        private async Task RebuildVariantFamilyAsync(List<ModProjectRecord> projects, string familyKey, string familyName)
        {
            if (projects == null || projects.Count == 0)
                return;

            if (_state == null || !_state.UnityFound || !_state.EnvironmentReady)
            {
                TwoPointTheme.ShowMessage(this,
                    "The private Unity build environment is not ready yet.\n\nWait for the Unity Worker status to show Ready, then try again. If it does not become ready, open Settings → Setup Paths & Maintenance to check or repair the environment.",
                    "Unity Environment Not Ready", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!EnsureModdersNameForBuild())
                return;

            familyKey = string.IsNullOrEmpty(familyKey) ? "family:" + (projects[0].ModId ?? Guid.NewGuid().ToString("N")) : familyKey;
            familyName = string.IsNullOrWhiteSpace(familyName) ? "Variant Family" : familyName.Trim();

            List<QueuedRebuildWorkItem> workItems = new List<QueuedRebuildWorkItem>();
            List<string> preparationFailures = new List<string>();
            AppendLog("============================================================");
            AppendLog("BL-020 combined family rebuild started: " + familyName + " (" + projects.Count + " item(s))");
            SetBusy(true, "Preparing combined family build...");
            SafeSetMyModsBuildProgress(true, "Preparing combined family build...");

            try
            {
                for (int i = 0; i < projects.Count; i++)
                {
                    ModProjectRecord record = projects[i];
                    try
                    {
                        SafeSetMyModsBuildProgress(true, "[Family " + (i + 1) + "/" + projects.Count + "] Preparing " + (record.Name ?? "mod") + "...");
                        workItems.Add(PrepareQueuedRebuildWorkItem(record, i + 1, projects.Count));
                    }
                    catch (Exception ex)
                    {
                        string name = record == null ? "Unknown mod" : (string.IsNullOrEmpty(record.Name) ? record.ModId : record.Name);
                        preparationFailures.Add(name + ": " + ex.Message);
                        AppendLog("[Family " + (i + 1) + "/" + projects.Count + "] PREPARATION FAILED: " + ex.Message);
                    }
                }

                if (preparationFailures.Count > 0 || workItems.Count != projects.Count)
                {
                    string message = "The family was not changed because one or more members could not be prepared. A combined family build is atomic: every member must be ready before Unity is called.";
                    int shown = Math.Min(preparationFailures.Count, 6);
                    for (int i = 0; i < shown; i++)
                        message += Environment.NewLine + "- " + preparationFailures[i];
                    TwoPointTheme.ShowMessage(this, message, "Family Build Preparation Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string familyOutputRoot = workItems[0].OutputRoot;
                for (int i = 1; i < workItems.Count; i++)
                {
                    if (!string.Equals(workItems[i].OutputRoot, familyOutputRoot, StringComparison.OrdinalIgnoreCase))
                        AppendLog("[Family] Output root for '" + (workItems[i].Record.Name ?? "mod") + "' differs; the combined package will use " + familyOutputRoot);
                }

                List<BuildJob> jobs = new List<BuildJob>();
                List<string> memberIds = new List<string>();
                Dictionary<string, string> previousRootByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, string> previousFamilyKeyByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < workItems.Count; i++)
                {
                    QueuedRebuildWorkItem item = workItems[i];
                    jobs.Add(item.Job);
                    if (!string.IsNullOrEmpty(item.Record.ModId))
                        memberIds.Add(item.Record.ModId);
                    if (!string.IsNullOrEmpty(item.Record.LastBuiltOutputPath))
                    {
                        previousRootByPath[item.Record.LastBuiltOutputPath] = string.IsNullOrWhiteSpace(item.Record.OutputRoot) ? familyOutputRoot : item.Record.OutputRoot;
                        if (BuildPackageModes.Normalise(item.Record.LastBuildPackageMode) == BuildPackageModes.Family &&
                            !string.IsNullOrEmpty(item.Record.LastBuiltFamilyKey))
                            previousFamilyKeyByPath[item.Record.LastBuiltOutputPath] = item.Record.LastBuiltFamilyKey;
                    }
                }
                memberIds.Sort(StringComparer.Ordinal);

                AppendLog("[Family] Prepared all members. Building one Addressables package with " + workItems.Count + " ItemModConfig(s)...");
                FamilyBuildResultFile familyResult = await _buildService.BuildFamilyAsync(
                    _state.UnityExePath,
                    _environment.ProjectDirectory,
                    jobs,
                    familyKey,
                    familyName,
                    familyOutputRoot,
                    delegate(string message)
                    {
                        SafeSetActivity(message);
                        SafeSetMyModsBuildProgress(true, message);
                    },
                    delegate(string line) { SafeAppendLog(line); },
                    GetPersistentWorker());

                if (familyResult.MemberResults == null || familyResult.MemberResults.Count != workItems.Count)
                    throw new InvalidDataException("Unity returned an incomplete combined-family result.");

                for (int i = 0; i < workItems.Count; i++)
                {
                    BuildResultFile memberResult = familyResult.MemberResults[i];
                    if (memberResult == null || !memberResult.Success)
                        throw new InvalidOperationException("Unity did not report a successful result for family member " + (i + 1) + ".");
                    FinaliseFamilyRebuild(workItems[i], memberResult, familyKey, familyName, memberIds);
                }

                int detachedFormerMembers = 0;
                foreach (KeyValuePair<string, string> oldFamily in previousFamilyKeyByPath)
                {
                    List<ModProjectRecord> detached = _projectLibrary.DetachFamilyPackage(oldFamily.Value, memberIds, oldFamily.Key);
                    detachedFormerMembers += detached.Count;
                }

                foreach (KeyValuePair<string, string> previous in previousRootByPath)
                    RemovePreviousInstalledBuild(previous.Key, previous.Value, familyResult.OutputPath);

                _lastBuiltOutput = familyResult.OutputPath;
                _openOutputButton.Enabled = !string.IsNullOrEmpty(_lastBuiltOutput) && Directory.Exists(_lastBuiltOutput);
                _myModsNeedsRefresh = true;

                string successMessage =
                    "'" + familyName + "' rebuilt successfully.\n\n" +
                    workItems.Count + " family member" + (workItems.Count == 1 ? "" : "s") + " are installed together in one mod.";
                if (detachedFormerMembers > 0)
                    successMessage += "\n\n" + detachedFormerMembers + " former member" + (detachedFormerMembers == 1 ? " is" : "s are") + " now Not Installed.";
                successMessage += "\n\nAny family change will show Installed (old) until the family is rebuilt.";

                TwoPointTheme.ShowMessage(this, successMessage, "Family Build Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppendLog("FAMILY BUILD FAILED: " + ex.Message);
                TwoPointTheme.ShowMessage(this,
                    "The combined family build failed. Existing installed family/individual folders were left untouched.\n\n" + ex.Message,
                    "Variant Family Build Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                foreach (QueuedRebuildWorkItem item in workItems)
                    CleanupQueuedRebuildWorkItem(item);
                SetBusy(false, "Family build complete.");
                SafeSetMyModsBuildProgress(false, "Family build complete.");
            }
        }

        private void FinaliseFamilyRebuild(QueuedRebuildWorkItem item, BuildResultFile result,
            string familyKey, string familyName, IList<string> memberIds)
        {
            ModProjectRecord updated = _projectLibrary.SaveAfterBuild(
                item.Record,
                result,
                item.OriginalArtwork,
                item.CustomIcon,
                item.Template.Key,
                item.Record.Name ?? "",
                item.Record.Description ?? "",
                item.Record.ItemCost,
                item.Record.KudoshCost,
                item.FitMode,
                item.Placement,
                item.SecondaryArtwork,
                item.SecondaryFitMode,
                item.SecondaryPlacement,
                item.IconMode,
                item.OutputRoot,
                item.Record.VariantMode,
                item.Record.VariantParentModId,
                BuildPackageModes.Family,
                familyKey,
                familyName,
                memberIds);

            item.Record.LastBuiltOutputPath = updated.LastBuiltOutputPath;
            item.Record.LastBuiltUtc = updated.LastBuiltUtc;
            item.Record.OutputRoot = updated.OutputRoot;
            item.Record.PendingChanges = false;
            item.Record.LastBuildPackageMode = updated.LastBuildPackageMode;
            item.Record.LastBuiltFamilyKey = updated.LastBuiltFamilyKey;
            item.Record.LastBuiltFamilyName = updated.LastBuiltFamilyName;
            item.Record.LastBuiltFamilyMemberIds = updated.LastBuiltFamilyMemberIds == null
                ? new List<string>()
                : new List<string>(updated.LastBuiltFamilyMemberIds);
        }

        private List<ModProjectRecord> GetCreateDecorPackMembers(string packKey)
        {
            List<ModProjectRecord> members = new List<ModProjectRecord>();
            if (string.IsNullOrEmpty(packKey))
                return members;

            List<ModProjectRecord> records = _projectLibrary.LoadAll();
            for (int i = 0; i < records.Count; i++)
            {
                ModProjectRecord record = records[i];
                if (record == null || !string.Equals(record.Template ?? "", "Wallpaper", StringComparison.OrdinalIgnoreCase) ||
                    BuildPackageModes.Normalise(record.LastBuildPackageMode) != BuildPackageModes.DecorPack ||
                    !string.Equals(record.LastBuiltFamilyKey ?? "", packKey, StringComparison.Ordinal))
                    continue;
                members.Add(record);
            }
            members.Sort(delegate(ModProjectRecord a, ModProjectRecord b)
            {
                return string.Compare(a == null ? "" : a.Name, b == null ? "" : b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return members;
        }

        private QueuedRebuildWorkItem PrepareCurrentWallpaperDecorPackWorkItem(TemplateDefinition template, int queuePosition, int queueTotal, string outputRoot)
        {
            if (!IsWallpaperTemplate(template))
                throw new InvalidOperationException("Only Wallpaper projects can be added to a Décor Pack from Create Mod.");

            string originalArtwork = Path.GetFullPath(_imagePath.Text);
            if (!File.Exists(originalArtwork))
                throw new FileNotFoundException("The selected Wallpaper artwork could not be found.", originalArtwork);

            string iconMode = GetIconMode();
            string customIcon = _iconPath.Text == null ? "" : _iconPath.Text.Trim();
            if (string.Equals(iconMode, "Custom Icon File", StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrEmpty(customIcon) || !File.Exists(customIcon)))
                throw new FileNotFoundException("The selected custom icon could not be found.", customIcon);

            string fitMode = GetFitMode(template);
            ImagePlacementState placement = ClonePlacement(_placement);
            outputRoot = Path.GetFullPath(outputRoot);
            Directory.CreateDirectory(outputRoot);

            QueuedRebuildWorkItem item = new QueuedRebuildWorkItem();
            item.QueuePosition = queuePosition;
            item.QueueTotal = queueTotal;
            item.Record = _activeProject;
            item.Template = template;
            item.OriginalArtwork = originalArtwork;
            item.SecondaryArtwork = "";
            item.CustomIcon = customIcon;
            item.FitMode = fitMode;
            item.SecondaryFitMode = "";
            item.Placement = placement;
            item.SecondaryPlacement = null;
            item.IconMode = iconMode;
            item.OutputRoot = outputRoot;

            try
            {
                item.PreparedArtwork = _imageProcessor.PrepareArtwork(originalArtwork, template, fitMode, placement);
                item.PreparedIcon = _imageProcessor.PrepareIcon(originalArtwork, item.PreparedArtwork, customIcon, template, fitMode, placement, iconMode);

                BuildJob job = new BuildJob();
                job.Template = template.Key;
                job.ModName = _modName.Text.Trim();
                job.ModdersName = _settings == null ? "" : (_settings.ModdersName ?? "");
                job.Description = _description.Text.Trim();
                job.ImagePath = item.PreparedArtwork;
                job.OutputPath = outputRoot;
                job.ItemCost = 0;
                job.KudoshCost = 0;
                job.ItemModId = _activeProject == null ? "" : (_activeProject.ModId ?? "");
                job.VariantMode = VariantModes.Standalone;
                job.VariantParentModId = "";
                job.VariantParentBaseArchetypeId = "";
                job.ItemCustomisationId = _activeProject == null ? "" : (_activeProject.ItemCustomisationId ?? "");
                job.IconPath = item.PreparedIcon;
                job.UseItemImageAsIcon = false;
                job.KeepGeneratedAssets = false;
                item.Job = job;
                return item;
            }
            catch
            {
                CleanupQueuedRebuildWorkItem(item);
                throw;
            }
        }

        private async Task BuildCurrentWallpaperDecorPackAsync(TemplateDefinition template, DecorPackChoice choice)
        {
            if (!IsWallpaperTemplate(template) || choice == null || choice.Standalone)
                return;

            string packKey = choice.NewPack || string.IsNullOrEmpty(choice.PackKey)
                ? "decorpack:" + Guid.NewGuid().ToString("N")
                : choice.PackKey;
            string packName = choice.NewPack
                ? (string.IsNullOrWhiteSpace(_modName.Text) ? "Wallpaper Pack" : _modName.Text.Trim() + " Pack")
                : (string.IsNullOrWhiteSpace(choice.PackName) ? "Wallpaper Pack" : choice.PackName.Trim());

            List<ModProjectRecord> existingMembers = choice.NewPack
                ? new List<ModProjectRecord>()
                : GetCreateDecorPackMembers(packKey);

            // The active Wallpaper is prepared from the current editor state below, so do not
            // also rebuild its older saved state when it already belongs to the target pack.
            if (_activeProject != null && !string.IsNullOrEmpty(_activeProject.ModId))
            {
                for (int i = existingMembers.Count - 1; i >= 0; i--)
                {
                    if (existingMembers[i] != null && string.Equals(existingMembers[i].ModId ?? "", _activeProject.ModId, StringComparison.Ordinal))
                        existingMembers.RemoveAt(i);
                }
            }

            int total = existingMembers.Count + 1;
            string packOutputRoot = Path.GetFullPath(_outputPath.Text);
            if (!choice.NewPack && existingMembers.Count > 0 && !string.IsNullOrWhiteSpace(existingMembers[0].OutputRoot))
                packOutputRoot = Path.GetFullPath(existingMembers[0].OutputRoot);
            Directory.CreateDirectory(packOutputRoot);

            List<QueuedRebuildWorkItem> workItems = new List<QueuedRebuildWorkItem>();
            QueuedRebuildWorkItem currentItem = null;
            Dictionary<string, string> previousRootByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> previousPackKeyByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            _openOutputButton.Enabled = false;
            _lastBuiltOutput = null;
            AppendLog("============================================================");
            AppendLog("Create Mod Décor Pack build started: " + packName + " (" + total + " wallpaper(s))");
            SetBusy(true, "Preparing Décor Pack...");
            SafeSetMyModsBuildProgress(true, "Preparing Décor Pack...");

            try
            {
                for (int i = 0; i < existingMembers.Count; i++)
                {
                    ModProjectRecord record = existingMembers[i];
                    SafeSetActivity("[Pack " + (i + 1) + "/" + total + "] Preparing " + (record.Name ?? "wallpaper") + "...");
                    workItems.Add(PrepareQueuedRebuildWorkItem(record, i + 1, total));
                }

                SafeSetActivity("[Pack " + total + "/" + total + "] Preparing " + _modName.Text.Trim() + "...");
                currentItem = PrepareCurrentWallpaperDecorPackWorkItem(template, total, total, packOutputRoot);
                workItems.Add(currentItem);

                List<BuildJob> jobs = new List<BuildJob>();
                for (int i = 0; i < workItems.Count; i++)
                {
                    QueuedRebuildWorkItem item = workItems[i];
                    if (item.Record != null && !string.IsNullOrEmpty(item.Record.LastBuiltOutputPath))
                    {
                        previousRootByPath[item.Record.LastBuiltOutputPath] = string.IsNullOrWhiteSpace(item.Record.OutputRoot)
                            ? packOutputRoot : item.Record.OutputRoot;
                        if (BuildPackageModes.Normalise(item.Record.LastBuildPackageMode) == BuildPackageModes.DecorPack &&
                            !string.IsNullOrEmpty(item.Record.LastBuiltFamilyKey))
                            previousPackKeyByPath[item.Record.LastBuiltOutputPath] = item.Record.LastBuiltFamilyKey;
                    }
                    item.OutputRoot = packOutputRoot;
                    item.Job.OutputPath = packOutputRoot;
                    jobs.Add(item.Job);
                }

                FamilyBuildResultFile packResult = await _buildService.BuildFamilyAsync(
                    _state.UnityExePath,
                    _environment.ProjectDirectory,
                    jobs,
                    packKey,
                    packName,
                    packOutputRoot,
                    delegate(string message)
                    {
                        SafeSetActivity(message);
                        SafeSetMyModsBuildProgress(true, message);
                    },
                    delegate(string line) { SafeAppendLog(line); },
                    GetPersistentWorker(),
                    BuildPackageModes.DecorPack);

                if (packResult.MemberResults == null || packResult.MemberResults.Count != workItems.Count)
                    throw new InvalidDataException("Unity returned an incomplete Décor Pack result.");

                List<string> memberIds = new List<string>();
                for (int i = 0; i < packResult.MemberResults.Count; i++)
                {
                    BuildResultFile memberResult = packResult.MemberResults[i];
                    if (memberResult == null || !memberResult.Success || string.IsNullOrEmpty(memberResult.ItemModId))
                        throw new InvalidOperationException("Unity did not return a successful result for Wallpaper " + (i + 1) + ".");
                    if (!memberIds.Contains(memberResult.ItemModId))
                        memberIds.Add(memberResult.ItemModId);
                }
                memberIds.Sort(StringComparer.Ordinal);

                ModProjectRecord savedCurrent = null;
                for (int i = 0; i < workItems.Count; i++)
                {
                    ModProjectRecord updated = FinaliseDecorPackRebuild(workItems[i], packResult.MemberResults[i], packKey, packName, memberIds);
                    if (object.ReferenceEquals(workItems[i], currentItem))
                        savedCurrent = updated;
                }
                if (savedCurrent == null)
                    throw new InvalidOperationException("The new Wallpaper project could not be saved after the Décor Pack build.");

                int detachedFormerMembers = 0;
                foreach (KeyValuePair<string, string> oldPack in previousPackKeyByPath)
                    detachedFormerMembers += _projectLibrary.DetachDecorPack(oldPack.Value, memberIds, oldPack.Key).Count;

                foreach (KeyValuePair<string, string> previous in previousRootByPath)
                    RemovePreviousInstalledBuild(previous.Key, previous.Value, packResult.OutputPath);

                _activeProject = savedCurrent;
                _imagePath.Text = _projectLibrary.GetArtworkPath(savedCurrent);
                if (string.Equals(GetIconMode(), "Custom Icon File", StringComparison.OrdinalIgnoreCase))
                    _iconPath.Text = _projectLibrary.GetCustomIconPath(savedCurrent);

                _lastBuiltOutput = packResult.OutputPath;
                _openOutputButton.Enabled = !string.IsNullOrEmpty(_lastBuiltOutput) && Directory.Exists(_lastBuiltOutput);
                _myModsNeedsRefresh = true;

                // Convert a just-created "New Décor Pack" choice into the real persistent pack
                // before starting the follow-on project, so the next Wallpaper can be added to
                // the same pack immediately without visiting My Mods first.
                RefreshCreateDecorPackChoices();
                RestoreCreateDecorPackChoice(packKey, false);

                string success = total == 1
                    ? "'" + (savedCurrent.Name ?? "Wallpaper") + "' was built and installed as the first Wallpaper in a new Décor Pack."
                    : total + " Wallpapers were built and installed together as one Décor Pack.";
                if (detachedFormerMembers > 0)
                    success += Environment.NewLine + Environment.NewLine + detachedFormerMembers + " former pack member" + (detachedFormerMembers == 1 ? " is" : "s are") + " now Not Installed.";
                success += Environment.NewLine + Environment.NewLine + "The Décor Pack remains selected so the next Wallpaper can be added while you create it.";
                TwoPointTheme.ShowMessage(this, success, "Décor Pack Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

                string completedOutput = packResult.OutputPath;
                string completedName = savedCurrent.Name ?? "Wallpaper";
                StartFollowOnProjectAfterBuild();
                _lastBuiltOutput = completedOutput;
                _openOutputButton.Enabled = !string.IsNullOrEmpty(_lastBuiltOutput) && Directory.Exists(_lastBuiltOutput);
                _activity.Text = "Success: " + completedName + " - Décor Pack kept selected for a new Wallpaper.";
            }
            catch (Exception ex)
            {
                AppendLog("CREATE MOD DÉCOR PACK BUILD FAILED: " + ex.Message);
                TwoPointTheme.ShowMessage(this,
                    "The Décor Pack could not be built. Existing installed mods were left unchanged.\n\n" + ex.Message,
                    "Décor Pack Build Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                for (int i = 0; i < workItems.Count; i++)
                    CleanupQueuedRebuildWorkItem(workItems[i]);
                SetBusy(false, _activity.Text);
                SafeSetMyModsBuildProgress(false, "Décor Pack build complete.");
            }
        }

        private async Task RebuildWallpaperDecorPackAsync(List<ModProjectRecord> projects, string existingPackKey, string existingPackName)
        {
            if (projects == null || projects.Count < 1)
            {
                AppendLog("DECOR PACK BUILD NOT STARTED: no Wallpaper projects were supplied.");
                TwoPointTheme.ShowMessage(this,
                    "No Wallpaper projects were supplied for this Décor Pack rebuild.",
                    "Décor Pack Rebuild", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // If this is an existing Décor Pack, reconstruct the current intended membership
            // from the saved pack key before building. The member snapshot can legitimately be
            // stale immediately after moving a Wallpaper in/out through My Mods.
            if (!string.IsNullOrEmpty(existingPackKey))
            {
                List<ModProjectRecord> currentRecords = _projectLibrary.LoadAll();
                List<ModProjectRecord> currentPackMembers = new List<ModProjectRecord>();
                HashSet<string> currentIds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < currentRecords.Count; i++)
                {
                    ModProjectRecord candidate = currentRecords[i];
                    if (candidate == null || string.IsNullOrEmpty(candidate.ModId) ||
                        !string.Equals(candidate.Template ?? "", "Wallpaper", StringComparison.OrdinalIgnoreCase) ||
                        BuildPackageModes.Normalise(candidate.LastBuildPackageMode) != BuildPackageModes.DecorPack ||
                        !string.Equals(candidate.LastBuiltFamilyKey ?? "", existingPackKey, StringComparison.Ordinal) ||
                        !currentIds.Add(candidate.ModId))
                        continue;
                    currentPackMembers.Add(candidate);
                }

                if (currentPackMembers.Count > 0)
                {
                    currentPackMembers.Sort(delegate(ModProjectRecord a, ModProjectRecord b)
                    {
                        return string.Compare(a == null ? "" : a.Name, b == null ? "" : b.Name, StringComparison.OrdinalIgnoreCase);
                    });
                    projects = currentPackMembers;
                }
            }

            for (int i = 0; i < projects.Count; i++)
            {
                if (projects[i] == null || !string.Equals(projects[i].Template ?? "", "Wallpaper", StringComparison.OrdinalIgnoreCase))
                {
                    TwoPointTheme.ShowMessage(this,
                        "Decor Packs currently contain Wallpaper mods only.",
                        "Wallpaper Pack Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            if (_state == null || !_state.UnityFound || !_state.EnvironmentReady)
            {
                TwoPointTheme.ShowMessage(this,
                    "The Unity build environment is not ready. Open Settings and use Check & Repair, then try again.",
                    "Unity Environment Not Ready", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!EnsureModdersNameForBuild())
                return;

            List<string> memberIds = new List<string>();
            for (int i = 0; i < projects.Count; i++)
                if (!string.IsNullOrEmpty(projects[i].ModId)) memberIds.Add(projects[i].ModId);
            memberIds.Sort(StringComparer.Ordinal);

            string packKey = !string.IsNullOrEmpty(existingPackKey) ? existingPackKey : CreateDecorPackKey(memberIds);
            string packName = string.IsNullOrWhiteSpace(existingPackName) ? "Wallpaper Pack" : existingPackName.Trim();

            List<QueuedRebuildWorkItem> workItems = new List<QueuedRebuildWorkItem>();
            List<string> preparationFailures = new List<string>();
            AppendLog("============================================================");
            AppendLog("Wallpaper Decor Pack rebuild started: " + packName + " (" + projects.Count + " wallpaper(s))");
            SetBusy(true, "Preparing Decor Pack...");
            SafeSetMyModsBuildProgress(true, "Preparing Decor Pack...");

            try
            {
                for (int i = 0; i < projects.Count; i++)
                {
                    ModProjectRecord record = projects[i];
                    try
                    {
                        SafeSetMyModsBuildProgress(true, "[Pack " + (i + 1) + "/" + projects.Count + "] Preparing " + (record.Name ?? "wallpaper") + "...");
                        workItems.Add(PrepareQueuedRebuildWorkItem(record, i + 1, projects.Count));
                    }
                    catch (Exception ex)
                    {
                        string name = record == null ? "Unknown wallpaper" : (string.IsNullOrEmpty(record.Name) ? record.ModId : record.Name);
                        preparationFailures.Add(name + ": " + ex.Message);
                        AppendLog("[Pack " + (i + 1) + "/" + projects.Count + "] PREPARATION FAILED: " + ex.Message);
                    }
                }

                if (preparationFailures.Count > 0 || workItems.Count != projects.Count)
                {
                    string message = "The Decor Pack was not changed because " + preparationFailures.Count +
                        (preparationFailures.Count == 1 ? " wallpaper could not be prepared." : " wallpapers could not be prepared.");
                    int shown = Math.Min(preparationFailures.Count, 4);
                    for (int i = 0; i < shown; i++)
                        message += Environment.NewLine + "- " + preparationFailures[i];
                    if (preparationFailures.Count > shown)
                        message += Environment.NewLine + "- Plus " + (preparationFailures.Count - shown) + " more failure" + (preparationFailures.Count - shown == 1 ? "." : "s.");
                    TwoPointTheme.ShowMessage(this, message, "Decor Pack Build Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string outputRoot = workItems[0].OutputRoot;
                List<BuildJob> jobs = new List<BuildJob>();
                Dictionary<string, string> previousRootByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, string> previousPackKeyByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < workItems.Count; i++)
                {
                    QueuedRebuildWorkItem item = workItems[i];
                    jobs.Add(item.Job);
                    if (!string.IsNullOrEmpty(item.Record.LastBuiltOutputPath))
                    {
                        previousRootByPath[item.Record.LastBuiltOutputPath] = string.IsNullOrWhiteSpace(item.Record.OutputRoot) ? outputRoot : item.Record.OutputRoot;
                        if (BuildPackageModes.Normalise(item.Record.LastBuildPackageMode) == BuildPackageModes.DecorPack &&
                            !string.IsNullOrEmpty(item.Record.LastBuiltFamilyKey))
                            previousPackKeyByPath[item.Record.LastBuiltOutputPath] = item.Record.LastBuiltFamilyKey;
                    }
                }

                FamilyBuildResultFile packResult = await _buildService.BuildFamilyAsync(
                    _state.UnityExePath,
                    _environment.ProjectDirectory,
                    jobs,
                    packKey,
                    packName,
                    outputRoot,
                    delegate(string message)
                    {
                        SafeSetActivity(message);
                        SafeSetMyModsBuildProgress(true, message);
                    },
                    delegate(string line) { SafeAppendLog(line); },
                    GetPersistentWorker(),
                    BuildPackageModes.DecorPack);

                if (packResult.MemberResults == null || packResult.MemberResults.Count != workItems.Count)
                    throw new InvalidDataException("Unity returned an incomplete Decor Pack result.");

                for (int i = 0; i < workItems.Count; i++)
                {
                    BuildResultFile memberResult = packResult.MemberResults[i];
                    if (memberResult == null || !memberResult.Success)
                        throw new InvalidOperationException("Unity did not return a successful result for wallpaper " + (i + 1) + ".");
                    FinaliseDecorPackRebuild(workItems[i], memberResult, packKey, packName, memberIds);
                }

                int detachedFormerMembers = 0;
                foreach (KeyValuePair<string, string> oldPack in previousPackKeyByPath)
                    detachedFormerMembers += _projectLibrary.DetachDecorPack(oldPack.Value, memberIds, oldPack.Key).Count;

                foreach (KeyValuePair<string, string> previous in previousRootByPath)
                    RemovePreviousInstalledBuild(previous.Key, previous.Value, packResult.OutputPath);

                _lastBuiltOutput = packResult.OutputPath;
                _openOutputButton.Enabled = !string.IsNullOrEmpty(_lastBuiltOutput) && Directory.Exists(_lastBuiltOutput);
                _myModsNeedsRefresh = true;

                string success = workItems.Count + " wallpapers built and installed as one Decor Pack.";
                if (detachedFormerMembers > 0)
                    success += Environment.NewLine + Environment.NewLine + detachedFormerMembers + " former pack member" + (detachedFormerMembers == 1 ? " is" : "s are") + " now Not Installed.";
                success += Environment.NewLine + Environment.NewLine + "Editing any member marks the pack Installed (old) until it is rebuilt.";
                TwoPointTheme.ShowMessage(this, success, "Decor Pack Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppendLog("DECOR PACK BUILD FAILED: " + ex.Message);
                TwoPointTheme.ShowMessage(this,
                    "The Decor Pack could not be built. Your existing installed mods were left unchanged.\n\n" + ex.Message,
                    "Decor Pack Build Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                foreach (QueuedRebuildWorkItem item in workItems)
                    CleanupQueuedRebuildWorkItem(item);
                SetBusy(false, "Decor Pack build complete.");
                SafeSetMyModsBuildProgress(false, "Decor Pack build complete.");
            }
        }

        private ModProjectRecord FinaliseDecorPackRebuild(QueuedRebuildWorkItem item, BuildResultFile result,
            string packKey, string packName, IList<string> memberIds)
        {
            if (item == null || item.Job == null)
                throw new InvalidOperationException("The Décor Pack member did not contain a valid build job.");

            ModProjectRecord updated = _projectLibrary.SaveAfterBuild(
                item.Record,
                result,
                item.OriginalArtwork,
                item.CustomIcon,
                item.Template.Key,
                item.Job.ModName ?? "",
                item.Job.Description ?? "",
                0,
                0,
                item.FitMode,
                item.Placement,
                item.SecondaryArtwork,
                item.SecondaryFitMode,
                item.SecondaryPlacement,
                item.IconMode,
                item.OutputRoot,
                VariantModes.Standalone,
                "",
                BuildPackageModes.DecorPack,
                packKey,
                packName,
                memberIds);

            item.Record = updated;
            item.Record.LastBuiltOutputPath = updated.LastBuiltOutputPath;
            item.Record.LastBuiltUtc = updated.LastBuiltUtc;
            item.Record.OutputRoot = updated.OutputRoot;
            item.Record.PendingChanges = false;
            item.Record.LastBuildPackageMode = updated.LastBuildPackageMode;
            item.Record.LastBuiltFamilyKey = updated.LastBuiltFamilyKey;
            item.Record.LastBuiltFamilyName = updated.LastBuiltFamilyName;
            item.Record.LastBuiltFamilyMemberIds = updated.LastBuiltFamilyMemberIds == null
                ? new List<string>()
                : new List<string>(updated.LastBuiltFamilyMemberIds);
            return updated;
        }

        private static string CreateDecorPackKey(IList<string> memberIds)
        {
            ulong hash = 1469598103934665603UL;
            if (memberIds != null)
            {
                for (int i = 0; i < memberIds.Count; i++)
                {
                    string value = memberIds[i] ?? "";
                    for (int c = 0; c < value.Length; c++)
                    {
                        hash ^= value[c];
                        hash *= 1099511628211UL;
                    }
                    hash ^= (byte)'|';
                    hash *= 1099511628211UL;
                }
            }
            return "decorpack:" + hash.ToString("x16");
        }

        private async Task RebuildProjectsQueueAsync(List<ModProjectRecord> projects)
        {
            if (projects == null || projects.Count == 0)
                return;

            if (_state == null || !_state.UnityFound || !_state.EnvironmentReady)
            {
                TwoPointTheme.ShowMessage(this,
                    "The private Unity build environment is not ready yet.\n\nWait for the Unity Worker status to show Ready, then try again. If it does not become ready, open Settings → Setup Paths & Maintenance to check or repair the environment.",
                    "Unity Environment Not Ready", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!EnsureModdersNameForBuild())
                return;

            DialogResult confirmation = TwoPointTheme.ShowMessage(this,
                "Rebuild " + projects.Count + (projects.Count == 1 ? " selected mod?" : " selected mods?") +
                "\n\nPublished Workshop items are not updated automatically.",
                "Rebuild Selected Mods", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirmation != DialogResult.Yes)
                return;

            int succeeded = 0;
            List<string> failures = new List<string>();
            List<QueuedRebuildWorkItem> workItems = new List<QueuedRebuildWorkItem>();

            AppendLog("============================================================");
            AppendLog("Queued rebuild started: " + projects.Count + (projects.Count == 1 ? " mod" : " mods"));
            AppendLog("Single-session mode: Unity will start once for the whole queue.");
            SetBusy(true, "Preparing rebuild queue...");
            SafeSetMyModsBuildProgress(true, "Preparing rebuild queue...");

            try
            {
                for (int i = 0; i < projects.Count; i++)
                {
                    ModProjectRecord record = projects[i];
                    string queuePrefix = "[" + (i + 1) + "/" + projects.Count + "] ";
                    SafeSetActivity(queuePrefix + "Preparing saved mod...");
                    SafeSetMyModsBuildProgress(true, queuePrefix + "Preparing saved mod...");
                    AppendLog("------------------------------------------------------------");
                    AppendLog(queuePrefix + "Queued rebuild: " + (record.Name ?? "") + " (ItemModID " + (record.ModId ?? "") + ")");

                    try
                    {
                        QueuedRebuildWorkItem item = PrepareQueuedRebuildWorkItem(record, i + 1, projects.Count);
                        workItems.Add(item);
                    }
                    catch (Exception ex)
                    {
                        string name = string.IsNullOrEmpty(record.Name) ? record.ModId : record.Name;
                        failures.Add(name + ": " + ex.Message);
                        AppendLog(queuePrefix + "PREPARATION FAILED: " + ex.Message);
                    }
                }

                if (workItems.Count > 0)
                {
                    List<BuildJob> jobs = new List<BuildJob>();
                    foreach (QueuedRebuildWorkItem item in workItems)
                        jobs.Add(item.Job);

                    AppendLog("------------------------------------------------------------");
                    AppendLog("Prepared " + workItems.Count + " buildable mod(s). " +
                        (IsPersistentWorkerEnabled() ? "Sending queue to the persistent Unity worker..." : "Starting a single Unity process..."));

                    List<BatchBuildOutcome> outcomes = await _buildService.BuildBatchAsync(
                        _state.UnityExePath,
                        _environment.ProjectDirectory,
                        jobs,
                        delegate(string message)
                        {
                            SafeSetActivity(message);
                            SafeSetMyModsBuildProgress(true, message);
                        },
                        delegate(string line) { SafeAppendLog(line); },
                        GetPersistentWorker());

                    for (int i = 0; i < workItems.Count; i++)
                    {
                        QueuedRebuildWorkItem item = workItems[i];
                        BatchBuildOutcome outcome = i < outcomes.Count ? outcomes[i] : null;

                        if (outcome == null || !outcome.Success)
                        {
                            string failureMessage = outcome == null || string.IsNullOrEmpty(outcome.ErrorMessage)
                                ? "No result was returned for this queued build."
                                : outcome.ErrorMessage;
                            string name = string.IsNullOrEmpty(item.Record.Name) ? item.Record.ModId : item.Record.Name;
                            failures.Add(name + ": " + failureMessage);
                            AppendLog(item.Prefix + "FAILED: " + failureMessage);
                            continue;
                        }

                        try
                        {
                            FinaliseQueuedRebuild(item, outcome.Result);
                            succeeded++;
                            AppendLog(item.Prefix + "SUCCESS: " + (item.Record.Name ?? ""));
                        }
                        catch (Exception ex)
                        {
                            string name = string.IsNullOrEmpty(item.Record.Name) ? item.Record.ModId : item.Record.Name;
                            failures.Add(name + ": " + ex.Message);
                            AppendLog(item.Prefix + "FINALISATION FAILED: " + ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog("BATCH SESSION FAILED: " + ex.Message);
                for (int i = 0; i < workItems.Count; i++)
                {
                    QueuedRebuildWorkItem item = workItems[i];
                    string name = string.IsNullOrEmpty(item.Record.Name) ? item.Record.ModId : item.Record.Name;
                    bool alreadyListed = false;
                    for (int f = 0; f < failures.Count; f++)
                    {
                        if (failures[f].StartsWith(name + ":", StringComparison.OrdinalIgnoreCase))
                        {
                            alreadyListed = true;
                            break;
                        }
                    }
                    if (!alreadyListed)
                        failures.Add(name + ": Batch Unity session failed - " + ex.Message);
                }
            }
            finally
            {
                foreach (QueuedRebuildWorkItem item in workItems)
                    CleanupQueuedRebuildWorkItem(item);

                string finalStatus = "Rebuild queue complete: " + succeeded + "/" + projects.Count + " succeeded.";
                SetBusy(false, finalStatus);
                SafeSetMyModsBuildProgress(false, finalStatus);
                AppendLog(finalStatus);
            }

            string summaryMessage = succeeded + " of " + projects.Count + (projects.Count == 1 ? " mod rebuilt successfully." : " mods rebuilt successfully.");
            if (failures.Count > 0)
            {
                summaryMessage += Environment.NewLine + Environment.NewLine + "Failed:";
                int shown = Math.Min(failures.Count, 6);
                for (int i = 0; i < shown; i++)
                    summaryMessage += Environment.NewLine + "- " + failures[i];
                if (failures.Count > shown)
                    summaryMessage += Environment.NewLine + "- Plus " + (failures.Count - shown) + " more failure" + (failures.Count - shown == 1 ? "." : "s.");
            }

            if (succeeded > 0)
            {
                summaryMessage += Environment.NewLine + Environment.NewLine +
                    "Published mods rebuilt locally now show Published (old) until Update Workshop is used.";
            }

            TwoPointTheme.ShowMessage(this, summaryMessage, "Rebuild Queue Complete", MessageBoxButtons.OK,
                failures.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private QueuedRebuildWorkItem PrepareQueuedRebuildWorkItem(ModProjectRecord record, int queuePosition, int queueTotal)
        {
            if (record == null)
                throw new ArgumentNullException("record");

            TemplateDefinition template = FindTemplate(record.Template);
            if (template == null)
                throw new InvalidOperationException("Saved template '" + (record.Template ?? "") + "' is not available in this version.");

            string originalArtwork = _projectLibrary.GetArtworkPath(record);
            if (string.IsNullOrEmpty(originalArtwork) || !File.Exists(originalArtwork))
                throw new FileNotFoundException("Saved artwork could not be found.", originalArtwork);

            bool doubleBanner = IsDualArtworkTemplate(template);
            string secondaryArtwork = doubleBanner ? _projectLibrary.GetSecondaryArtworkPath(record) : "";
            if (doubleBanner && (string.IsNullOrEmpty(secondaryArtwork) || !File.Exists(secondaryArtwork)))
                throw new FileNotFoundException((IsTwoSidedSignTemplate(template) ? "Saved Back artwork could not be found." : "Saved Right Banner artwork could not be found."), secondaryArtwork);

            string iconMode = string.IsNullOrEmpty(record.IconMode) ? "Original Artwork" : record.IconMode;
            string customIcon = _projectLibrary.GetCustomIconPath(record);
            if (string.Equals(iconMode, "Custom Icon File", StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrEmpty(customIcon) || !File.Exists(customIcon)))
            {
                throw new FileNotFoundException("Saved custom icon could not be found.", customIcon);
            }

            string fitMode = template.ImageProcessingEnabled
                ? (string.IsNullOrEmpty(record.FitMode) ? (template.DefaultFitMode ?? "Fill") : record.FitMode)
                : "Original";

            ImagePlacementState placement = new ImagePlacementState();
            placement.Zoom = record.Zoom <= 0 ? 1.0f : record.Zoom;
            placement.OffsetX = record.OffsetX;
            placement.OffsetY = record.OffsetY;
            placement.RotationDegrees = NormaliseRotationDegrees(record.RotationDegrees);
            placement.GuideColorName = NormaliseGuideColorName(record.GuideColor);
            placement.ReferenceWidth = record.ReferenceWidth;
            placement.ReferenceHeight = record.ReferenceHeight;

            string secondaryFitMode = doubleBanner
                ? (string.IsNullOrEmpty(record.SecondaryFitMode) ? (template.DefaultFitMode ?? "Fill") : record.SecondaryFitMode)
                : "";
            ImagePlacementState secondaryPlacement = null;
            if (doubleBanner)
            {
                secondaryPlacement = new ImagePlacementState();
                secondaryPlacement.Zoom = record.SecondaryZoom <= 0 ? 1.0f : record.SecondaryZoom;
                secondaryPlacement.OffsetX = record.SecondaryOffsetX;
                secondaryPlacement.OffsetY = record.SecondaryOffsetY;
                secondaryPlacement.RotationDegrees = NormaliseRotationDegrees(record.SecondaryRotationDegrees);
                secondaryPlacement.GuideColorName = NormaliseGuideColorName(record.SecondaryGuideColor);
                secondaryPlacement.ReferenceWidth = record.SecondaryReferenceWidth;
                secondaryPlacement.ReferenceHeight = record.SecondaryReferenceHeight;
            }

            string outputRoot = record.OutputRoot;
            if (string.IsNullOrWhiteSpace(outputRoot))
                outputRoot = !string.IsNullOrWhiteSpace(_outputPath.Text) ? _outputPath.Text : SettingsService.GetGameModsFolder();
            outputRoot = Path.GetFullPath(outputRoot);
            Directory.CreateDirectory(outputRoot);

            QueuedRebuildWorkItem item = new QueuedRebuildWorkItem();
            item.QueuePosition = queuePosition;
            item.QueueTotal = queueTotal;
            item.Record = record;
            item.Template = template;
            item.OriginalArtwork = originalArtwork;
            item.SecondaryArtwork = secondaryArtwork;
            item.CustomIcon = customIcon;
            item.FitMode = fitMode;
            item.SecondaryFitMode = secondaryFitMode;
            item.Placement = placement;
            item.SecondaryPlacement = secondaryPlacement;
            item.IconMode = iconMode;
            item.OutputRoot = outputRoot;

            try
            {
                if (doubleBanner)
                {
                    item.PreparedArtwork = _imageProcessor.PrepareDoubleBannerArtwork(
                        originalArtwork, secondaryArtwork, template,
                        fitMode, placement, secondaryFitMode, secondaryPlacement);
                    item.PreparedIcon = _imageProcessor.PrepareDoubleBannerIcon(
                        originalArtwork, secondaryArtwork, item.PreparedArtwork, customIcon, template,
                        fitMode, placement, secondaryFitMode, secondaryPlacement, iconMode);
                }
                else
                {
                    item.PreparedArtwork = _imageProcessor.PrepareArtwork(originalArtwork, template, fitMode, placement);
                    item.PreparedIcon = _imageProcessor.PrepareIcon(originalArtwork, item.PreparedArtwork, customIcon, template, fitMode, placement, iconMode);
                }

                BuildJob job = new BuildJob();
                job.Template = template.Key;
                job.ModName = record.Name ?? "";
                job.ModdersName = _settings == null ? "" : (_settings.ModdersName ?? "");
                job.Description = record.Description ?? "";
                job.ImagePath = item.PreparedArtwork;
                job.OutputPath = outputRoot;
                job.ItemCost = record.ItemCost;
                job.KudoshCost = record.KudoshCost;
                job.ItemModId = record.ModId ?? "";
                job.VariantMode = VariantModes.Normalise(record.VariantMode);
                job.VariantParentModId = record.VariantParentModId ?? "";
                job.VariantParentBaseArchetypeId = record.VariantParentBaseArchetypeId ?? "";
                job.ItemCustomisationId = record.ItemCustomisationId ?? "";
                job.IconPath = item.PreparedIcon;
                job.UseItemImageAsIcon = false;
                job.KeepGeneratedAssets = false;
                item.Job = job;
                return item;
            }
            catch
            {
                CleanupQueuedRebuildWorkItem(item);
                throw;
            }
        }

        private void FinaliseQueuedRebuild(QueuedRebuildWorkItem item, BuildResultFile result)
        {
            string previousOutput = item.Record.LastBuiltOutputPath ?? "";
            string previousOutputRoot = item.Record.OutputRoot ?? "";
            string previousPackageMode = BuildPackageModes.Normalise(item.Record.LastBuildPackageMode);
            string previousFamilyKey = item.Record.LastBuiltFamilyKey ?? "";

            ModProjectRecord updated = _projectLibrary.SaveAfterBuild(
                item.Record,
                result,
                item.OriginalArtwork,
                item.CustomIcon,
                item.Template.Key,
                item.Record.Name ?? "",
                item.Record.Description ?? "",
                item.Record.ItemCost,
                item.Record.KudoshCost,
                item.FitMode,
                item.Placement,
                item.SecondaryArtwork,
                item.SecondaryFitMode,
                item.SecondaryPlacement,
                item.IconMode,
                item.OutputRoot,
                item.Record.VariantMode,
                item.Record.VariantParentModId);

            if (previousPackageMode == BuildPackageModes.Family && !string.IsNullOrEmpty(previousFamilyKey))
                _projectLibrary.DetachFamilyPackage(previousFamilyKey, new string[] { item.Record.ModId }, previousOutput);
            else if (previousPackageMode == BuildPackageModes.DecorPack && !string.IsNullOrEmpty(previousFamilyKey))
                _projectLibrary.DetachDecorPack(previousFamilyKey, new string[] { item.Record.ModId }, previousOutput);
            RemovePreviousInstalledBuild(previousOutput, previousOutputRoot, result.OutputPath);

            item.Record.LastBuiltOutputPath = updated.LastBuiltOutputPath;
            item.Record.LastBuiltUtc = updated.LastBuiltUtc;
            item.Record.OutputRoot = updated.OutputRoot;
            _myModsNeedsRefresh = true;
        }

        private static void CleanupQueuedRebuildWorkItem(QueuedRebuildWorkItem item)
        {
            if (item == null)
                return;

            if (!string.IsNullOrEmpty(item.PreparedIcon))
                ImageProcessingService.DeletePreparedFile(item.PreparedIcon);
            if (!string.IsNullOrEmpty(item.PreparedArtwork) &&
                !string.Equals(item.PreparedArtwork, item.OriginalArtwork, StringComparison.OrdinalIgnoreCase))
            {
                ImageProcessingService.DeletePreparedFile(item.PreparedArtwork);
            }
        }

        private void LoadProjectIntoEditor(ModProjectRecord record)
        {
            if (record == null)
                return;

            TemplateDefinition template = FindTemplate(record.Template);
            if (template == null)
            {
                TwoPointTheme.ShowMessage(this, "The saved template '" + record.Template + "' is not available in this version of Memento Maker.",
                    "Template Not Available", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _activeProject = record;
            if (_decorPackBox != null)
            {
                _loadingDecorPackChoice = true;
                try { _decorPackBox.SelectedIndex = -1; }
                finally { _loadingDecorPackChoice = false; }
            }
            _templateBox.SelectedItem = template;
            _modName.Text = record.Name ?? "";
            _description.Text = record.Description ?? "";
            _cost.Value = Math.Max(_cost.Minimum, Math.Min(_cost.Maximum, record.ItemCost));
            _kudosh.Value = Math.Max(_kudosh.Minimum, Math.Min(_kudosh.Maximum, record.KudoshCost));

            if (template.ImageProcessingEnabled)
            {
                int fitIndex = _fitMode.Items.IndexOf(record.FitMode ?? template.DefaultFitMode);
                _fitMode.SelectedIndex = fitIndex >= 0 ? fitIndex : 0;
            }

            _placement = new ImagePlacementState();
            _placement.Zoom = record.Zoom <= 0 ? 1.0f : record.Zoom;
            _placement.OffsetX = record.OffsetX;
            _placement.OffsetY = record.OffsetY;
            _placement.RotationDegrees = NormaliseRotationDegrees(record.RotationDegrees);
            _placement.GuideColorName = NormaliseGuideColorName(record.GuideColor);
            _placement.ReferenceWidth = record.ReferenceWidth;
            _placement.ReferenceHeight = record.ReferenceHeight;
            int guideIndex = _guideColor.Items.IndexOf(_placement.GuideColorName);
            _guideColor.SelectedIndex = guideIndex >= 0 ? guideIndex : _guideColor.Items.IndexOf("Yellow");
            SyncGuideColorSwatches();

            string iconMode = string.IsNullOrEmpty(record.IconMode) ? "Original Artwork" : record.IconMode;
            string iconDisplayMode = GetIconDisplayMode(iconMode);
            int iconIndex = _iconMode.Items.IndexOf(iconDisplayMode);
            _iconMode.SelectedIndex = iconIndex >= 0 ? iconIndex : 0;

            _imagePath.Text = _projectLibrary.GetArtworkPath(record);
            if (IsDualArtworkTemplate(template))
            {
                _doubleBannerEditingRight = false;
                _doubleBannerLeftArtworkPath = _projectLibrary.GetArtworkPath(record);
                _doubleBannerRightArtworkPath = _projectLibrary.GetSecondaryArtworkPath(record);
                _doubleBannerLeftFitMode = string.IsNullOrEmpty(record.FitMode) ? (template.DefaultFitMode ?? "Fill") : record.FitMode;
                _doubleBannerRightFitMode = string.IsNullOrEmpty(record.SecondaryFitMode) ? (template.DefaultFitMode ?? "Fill") : record.SecondaryFitMode;
                _doubleBannerLeftPlacement = ClonePlacement(_placement);
                _doubleBannerRightPlacement = new ImagePlacementState();
                _doubleBannerRightPlacement.Zoom = record.SecondaryZoom <= 0 ? 1.0f : record.SecondaryZoom;
                _doubleBannerRightPlacement.OffsetX = record.SecondaryOffsetX;
                _doubleBannerRightPlacement.OffsetY = record.SecondaryOffsetY;
                _doubleBannerRightPlacement.RotationDegrees = NormaliseRotationDegrees(record.SecondaryRotationDegrees);
                _doubleBannerRightPlacement.GuideColorName = NormaliseGuideColorName(record.SecondaryGuideColor);
                _doubleBannerRightPlacement.ReferenceWidth = record.SecondaryReferenceWidth;
                _doubleBannerRightPlacement.ReferenceHeight = record.SecondaryReferenceHeight;

                // Visible editor sides intentionally match the physical left/right banners in
                // game while the established texture-atlas storage order remains unchanged.
                // Load the displayed Left Banner state into the shared placement controls only
                // after both saved sides are restored, preventing the primary stored path from
                // overwriting the display-left state during the first preview refresh.
                ActivateDoubleBannerSide(false, false);
            }
            _iconPath.Text = _projectLibrary.GetCustomIconPath(record);
            if (!string.IsNullOrEmpty(record.OutputRoot))
                _outputPath.Text = record.OutputRoot;

            _lastBuiltOutput = record.LastBuiltOutputPath;
            _openOutputButton.Enabled = !string.IsNullOrEmpty(_lastBuiltOutput) && Directory.Exists(_lastBuiltOutput);
            UpdateProjectStateUi();
            RefreshCreateVariantChoices();
            RefreshCreateDecorPackChoices();
            UpdateArtworkPreview();
            AppendLog("Loaded saved mod project: " + record.Name + " (ItemModID " + record.ModId + ")");
        }

        private void RequestNewProject()
        {
            bool hasInput = _activeProject != null ||
                !string.IsNullOrWhiteSpace(_modName.Text) ||
                !string.IsNullOrWhiteSpace(_description.Text) ||
                !string.IsNullOrWhiteSpace(_imagePath.Text) ||
                !string.IsNullOrWhiteSpace(_doubleBannerLeftArtworkPath) ||
                !string.IsNullOrWhiteSpace(_doubleBannerRightArtworkPath) ||
                !string.IsNullOrWhiteSpace(_iconPath.Text);

            if (hasInput)
            {
                DialogResult result = TwoPointTheme.ShowMessage(this,
                    "Start a new mod?\n\n" +
                    "This will clear the current name, description, artwork, icon and placement from the Create Mod page.\n\n" +
                    "Saved and built mods in My Mods will not be deleted.",
                    "Start New Mod", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result != DialogResult.Yes)
                    return;
            }

            StartNewProject();
        }

        private void StartFollowOnProjectAfterBuild()
        {
            VariantParentChoice preservedVariantChoice = GetSelectedCreateVariantChoice();
            string preservedVariantMode = preservedVariantChoice == null
                ? VariantModes.Standalone
                : VariantModes.Normalise(preservedVariantChoice.Mode);
            string preservedVariantParentId = preservedVariantChoice == null
                ? ""
                : (preservedVariantChoice.ModId ?? "");
            DecorPackChoice preservedDecorPackChoice = GetSelectedCreateDecorPackChoice();
            string preservedDecorPackKey = preservedDecorPackChoice == null ? "" : (preservedDecorPackChoice.PackKey ?? "");
            bool preservedNewDecorPack = preservedDecorPackChoice != null && preservedDecorPackChoice.NewPack;

            // Detach from the project that was just saved so the next build is a new mod.
            _activeProject = null;

            // Clear only identity text. The selected item type/options, artwork paths,
            // fit mode, guide colour, zoom, pan, rotation, cost/Kudosh and icon choices
            // deliberately remain untouched for fast iterative creation.
            _modName.Text = "";
            _description.Text = "";

            // SaveAfterBuild rewrites artwork paths to the project's durable copies. For
            // dual-artwork items make sure the active editor points at the correct retained side.
            TemplateDefinition template = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            if (IsDualArtworkTemplate(template))
            {
                _imagePath.Text = GetDoubleBannerDisplayArtworkPath(_doubleBannerEditingRight) ?? "";
                ActivateDoubleBannerSide(_doubleBannerEditingRight, false);
            }

            UpdateProjectStateUi();
            RefreshCreateVariantChoices();
            RestoreCreateVariantChoice(preservedVariantMode, preservedVariantParentId);
            RefreshCreateDecorPackChoices();
            RestoreCreateDecorPackChoice(preservedDecorPackKey, preservedNewDecorPack);
            UpdateArtworkPreview();
        }

        private void StartNewProject()
        {
            _activeProject = null;
            _modName.Text = "";
            _description.Text = "";
            _imagePath.Text = "";
            _iconPath.Text = "";
            _doubleBannerEditingRight = false;
            _doubleBannerLeftArtworkPath = "";
            _doubleBannerRightArtworkPath = "";
            _doubleBannerLeftFitMode = "Fill";
            _doubleBannerRightFitMode = "Fill";
            _doubleBannerLeftPlacement = new ImagePlacementState();
            _doubleBannerRightPlacement = new ImagePlacementState();
            _lastBuiltOutput = null;
            _openOutputButton.Enabled = false;

            TemplateDefinition posterTemplate = FindTemplateByKey("Standard Poster");
            if (posterTemplate != null && _templateBox.SelectedItem != posterTemplate)
                _templateBox.SelectedItem = posterTemplate;

            TemplateDefinition template = _templateBox.SelectedItem as TemplateDefinition;
            if (template != null)
            {
                _cost.Value = Math.Max(_cost.Minimum, Math.Min(_cost.Maximum, template.DefaultCost));
                _kudosh.Value = Math.Max(_kudosh.Minimum, Math.Min(_kudosh.Maximum, template.DefaultKudosh));
                SetDefaultIconMode(template);
            }

            ResetPlacement(false);
            UpdateProjectStateUi();
            RefreshCreateVariantChoices();
            RefreshCreateDecorPackChoices();
            RestoreCreateDecorPackChoice("", false);
            UpdateArtworkPreview();
            _activity.Text = "Ready to create a new mod.";
        }

        private List<string> CollectWorkshopFamilyMemberPreviewPaths(List<ModProjectRecord> members, ModProjectRecord preferred)
        {
            List<string> paths = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (preferred != null)
                AddWorkshopFamilyPreviewPath(paths, seen, preferred);
            if (members != null)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    ModProjectRecord member = members[i];
                    if (preferred != null && member != null && string.Equals(member.ModId, preferred.ModId, StringComparison.Ordinal))
                        continue;
                    AddWorkshopFamilyPreviewPath(paths, seen, member);
                }
            }
            return paths;
        }

        private void AddWorkshopFamilyPreviewPath(List<string> paths, HashSet<string> seen, ModProjectRecord record)
        {
            if (record == null)
                return;
            string path = ResolveWorkshopDefaultPreview(record);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;
            string fullPath = Path.GetFullPath(path);
            if (seen.Add(fullPath))
                paths.Add(fullPath);
        }

        private string CreateWorkshopFamilyWorkshopPreview(List<string> memberPreviewPaths)
        {
            if (memberPreviewPaths == null || memberPreviewPaths.Count == 0)
                return "";
            try
            {
                string style = _settings == null ? "Simple Grid" : (_settings.WorkshopFamilyPreviewStyle ?? "Simple Grid");
                return _imageProcessor.CreateWorkshopFamilyCompositePreview(memberPreviewPaths, style);
            }
            catch (Exception ex)
            {
                AppendLog("Family Workshop preview generation warning: " + ex.Message);
                return "";
            }
        }

        private List<string> BuildFamilyWorkshopAdditionalPreviewPaths(List<string> memberPreviewPaths, string mainPreviewPath)
        {
            List<string> extras = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string main = string.IsNullOrWhiteSpace(mainPreviewPath) ? "" : Path.GetFullPath(mainPreviewPath);
            if (memberPreviewPaths == null)
                return extras;
            for (int i = 0; i < memberPreviewPaths.Count; i++)
            {
                string path = memberPreviewPaths[i];
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    continue;
                string fullPath = Path.GetFullPath(path);
                if (!string.IsNullOrEmpty(main) && string.Equals(fullPath, main, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (seen.Add(fullPath))
                    extras.Add(fullPath);
            }
            return extras;
        }

        private List<WorkshopLegacyItem> CollectLegacyFamilyWorkshopItems(List<ModProjectRecord> members, string familyKey, string familyPublishedFileId, WorkshopQueryResult query)
        {
            List<WorkshopLegacyItem> items = new List<WorkshopLegacyItem>();
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            if (members == null)
                return items;

            for (int i = 0; i < members.Count; i++)
            {
                ModProjectRecord member = members[i];
                ModProjectRecord saved = member == null ? null : (_projectLibrary.Load(member.ModId) ?? member);
                if (saved == null)
                    continue;

                bool currentFamilyLink = !string.IsNullOrEmpty(saved.WorkshopPublishedFileId) &&
                    BuildPackageModes.IsCombined(saved.WorkshopPackageMode) &&
                    string.Equals(saved.WorkshopFamilyKey ?? "", familyKey ?? "", StringComparison.Ordinal);

                if (!currentFamilyLink)
                    TryAddLegacyWorkshopItem(items, ids, saved.WorkshopPublishedFileId, saved.WorkshopTitle, familyPublishedFileId, query);
                TryAddLegacyWorkshopItem(items, ids, saved.WorkshopPreviousPublishedFileId, saved.WorkshopTitle, familyPublishedFileId, query);
            }
            return items;
        }

        private static void TryAddLegacyWorkshopItem(List<WorkshopLegacyItem> items, HashSet<string> ids, string publishedFileId, string title, string excludedPublishedFileId, WorkshopQueryResult query)
        {
            string id = (publishedFileId ?? "").Trim();
            if (string.IsNullOrEmpty(id) || string.Equals(id, (excludedPublishedFileId ?? "").Trim(), StringComparison.Ordinal) || !ids.Add(id))
                return;

            if (query != null && query.Items != null)
            {
                for (int i = 0; i < query.Items.Count; i++)
                {
                    WorkshopItemSummary existing = query.Items[i];
                    if (existing == null || !string.Equals(existing.PublishedFileId ?? "", id, StringComparison.Ordinal))
                        continue;
                    bool alreadyDeprecated = (existing.Title ?? "").StartsWith("Deprecated - ", StringComparison.OrdinalIgnoreCase) &&
                        (string.Equals(existing.Visibility, "Private", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(existing.Visibility, "Hidden", StringComparison.OrdinalIgnoreCase));
                    if (alreadyDeprecated)
                        return;
                    title = existing.Title ?? title;
                    break;
                }
            }

            WorkshopLegacyItem legacy = new WorkshopLegacyItem();
            legacy.PublishedFileId = id;
            legacy.Title = title ?? "";
            items.Add(legacy);
        }

        private string ResolveWorkshopDefaultPreview(ModProjectRecord record)
        {
            if (record == null)
                return "";

            // Prefer a real ItemIcon.png from the built output when one is available.
            string builtIcon = _projectLibrary.GetBuiltItemIconPath(record);
            if (!string.IsNullOrEmpty(builtIcon) && File.Exists(builtIcon))
                return builtIcon;

            // Installed mod folders contain the packaged Addressables output rather than the
            // source Icons folder, so materialise the exact same 256x256 icon renderer used by
            // My Mods / the build pipeline into the saved project. This ensures Steam defaults
            // to the in-game ItemIcon rather than the raw artwork image.
            try
            {
                TemplateDefinition template = FindTemplate(record.Template);
                string artworkPath = _projectLibrary.GetArtworkPath(record);
                string customIconPath = _projectLibrary.GetCustomIconPath(record);
                string fitMode = string.IsNullOrEmpty(record.FitMode) ? (template == null ? "Fill" : (template.DefaultFitMode ?? "Fill")) : record.FitMode;
                string iconMode = string.IsNullOrEmpty(record.IconMode) ? "Original Artwork" : record.IconMode;

                ImagePlacementState placement = new ImagePlacementState();
                placement.Zoom = record.Zoom <= 0 ? 1.0F : record.Zoom;
                placement.OffsetX = record.OffsetX;
                placement.OffsetY = record.OffsetY;
                placement.RotationDegrees = NormaliseRotationDegrees(record.RotationDegrees);
                placement.GuideColorName = NormaliseGuideColorName(record.GuideColor);
                placement.ReferenceWidth = record.ReferenceWidth;
                placement.ReferenceHeight = record.ReferenceHeight;

                Bitmap itemIcon = null;
                try
                {
                    if (IsDualArtworkTemplate(template))
                    {
                        string secondaryArtworkPath = _projectLibrary.GetSecondaryArtworkPath(record);
                        string secondaryFitMode = string.IsNullOrEmpty(record.SecondaryFitMode)
                            ? (template == null ? "Fill" : (template.DefaultFitMode ?? "Fill"))
                            : record.SecondaryFitMode;
                        ImagePlacementState secondaryPlacement = new ImagePlacementState();
                        secondaryPlacement.Zoom = record.SecondaryZoom <= 0 ? 1.0F : record.SecondaryZoom;
                        secondaryPlacement.OffsetX = record.SecondaryOffsetX;
                        secondaryPlacement.OffsetY = record.SecondaryOffsetY;
                        secondaryPlacement.RotationDegrees = NormaliseRotationDegrees(record.SecondaryRotationDegrees);
                        secondaryPlacement.GuideColorName = NormaliseGuideColorName(record.SecondaryGuideColor);
                        secondaryPlacement.ReferenceWidth = record.SecondaryReferenceWidth;
                        secondaryPlacement.ReferenceHeight = record.SecondaryReferenceHeight;

                        itemIcon = _imageProcessor.CreateDoubleBannerIconPreview(
                            artworkPath, secondaryArtworkPath, customIconPath, template,
                            fitMode, placement, secondaryFitMode, secondaryPlacement,
                            iconMode, 256, 256);
                    }
                    else
                    {
                        itemIcon = _imageProcessor.CreateIconPreview(
                            artworkPath, customIconPath, template, fitMode, placement, iconMode, 256, 256);
                    }

                    if (itemIcon != null)
                    {
                        string projectDirectory = _projectLibrary.GetProjectDirectory(record.ModId);
                        Directory.CreateDirectory(projectDirectory);
                        string generatedIconPath = Path.Combine(projectDirectory, "ItemIcon.png");
                        itemIcon.Save(generatedIconPath, System.Drawing.Imaging.ImageFormat.Png);
                        return generatedIconPath;
                    }
                }
                finally
                {
                    if (itemIcon != null)
                        itemIcon.Dispose();
                }
            }
            catch (Exception ex)
            {
                AppendLog("Workshop preview icon generation warning: " + ex.Message);
            }

            // Older projects may already have an explicitly saved Workshop preview. Use it only
            // after the in-game ItemIcon paths have been exhausted.
            string savedPreview = _projectLibrary.GetWorkshopPreviewPath(record);
            if (!string.IsNullOrEmpty(savedPreview) && File.Exists(savedPreview))
                return savedPreview;

            // Last-resort compatibility fallback. This should only be reached if the project does
            // not contain enough information to reproduce its in-game icon.
            return _projectLibrary.GetArtworkPath(record);
        }

        private TemplateDefinition FindTemplate(string key)
        {
            // Compatibility: the original single Poster template is the Standard Poster.
            if (string.Equals(key, "Poster", StringComparison.OrdinalIgnoreCase))
                key = "Standard Poster";

            // Compatibility: the original single Rug template maps to Staff Rectangle Rug.
            if (string.Equals(key, "Rug", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Rectangle Rug", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "RectangleRug", StringComparison.OrdinalIgnoreCase))
            {
                key = "Staff Rectangle Rug";
            }

            foreach (TemplateDefinition template in _templates)
            {
                if (string.Equals(template.Key, key, StringComparison.OrdinalIgnoreCase))
                    return template;
            }
            return null;
        }

        private void UpdateProjectStateUi()
        {
            if (_projectStatus == null || _buildButton == null)
                return;

            TemplateDefinition template = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            bool decorPackSelected = IsWallpaperTemplate(template) && IsCreateDecorPackSelected();

            if (_activeProject == null)
            {
                _projectStatus.Text = decorPackSelected
                    ? "New Wallpaper - a unique Mod ID will be generated and added to the selected Décor Pack."
                    : "New mod - a unique ItemModID will be generated on the first successful build.";
                _projectStatus.ForeColor = Color.DimGray;
                _buildButton.Text = decorPackSelected ? "Build Décor Pack" : "Create Mod";
            }
            else
            {
                _projectStatus.Text = "Saved mod - Item Mod ID: " + _activeProject.ModId + "  (rebuilds keep this ID)";
                _projectStatus.ForeColor = Color.FromArgb(25, 125, 65);
                _buildButton.Text = decorPackSelected ? "Rebuild Décor Pack" : "Rebuild Mod";
            }
        }

        private void RemovePreviousInstalledBuild(string previousOutput, string previousOutputRoot, string newOutput)
        {
            if (string.IsNullOrEmpty(previousOutput) || string.IsNullOrEmpty(previousOutputRoot) ||
                string.IsNullOrEmpty(newOutput) || string.Equals(previousOutput, newOutput, StringComparison.OrdinalIgnoreCase) ||
                !Directory.Exists(previousOutput))
                return;

            try
            {
                string root = Path.GetFullPath(previousOutputRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string oldPath = Path.GetFullPath(previousOutput).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!oldPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return;

                DeleteInstalledDirectoryWithRetries(oldPath);
                AppendLog("Removed previous installed build: " + oldPath);
            }
            catch (Exception ex)
            {
                AppendLog("WARNING: The new build succeeded, but the previous build folder could not be removed: " + ex.Message);
            }
        }

        private static void DeleteInstalledDirectoryWithRetries(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return;

            Exception lastError = null;
            const int maxAttempts = 6;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (!Directory.Exists(directory))
                    return;

                try
                {
                    foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                    {
                        try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
                    }
                    foreach (string childDirectory in Directory.GetDirectories(directory, "*", SearchOption.AllDirectories))
                    {
                        try { File.SetAttributes(childDirectory, FileAttributes.Normal); } catch { }
                    }
                    try { File.SetAttributes(directory, FileAttributes.Normal); } catch { }

                    Directory.Delete(directory, true);
                    if (!Directory.Exists(directory))
                        return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }

                // Windows can transiently leave the now-empty top-level folder behind after
                // recursive deletion (Explorer, antivirus/indexing and filesystem notifications
                // can briefly hold it). If the contents have gone, explicitly retry the final
                // directory removal before repeating the full recursive cleanup.
                try
                {
                    if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length == 0)
                    {
                        Directory.Delete(directory, false);
                        if (!Directory.Exists(directory))
                            return;
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }

                System.Threading.Thread.Sleep(150 * (attempt + 1));
            }

            if (Directory.Exists(directory))
                throw new IOException("The old installed folder remained after repeated cleanup attempts: " + directory, lastError);
        }

        private void OpenLastOutput()
        {
            if (!string.IsNullOrEmpty(_lastBuiltOutput) && Directory.Exists(_lastBuiltOutput))
                Process.Start("explorer.exe", "\"" + _lastBuiltOutput + "\"");
        }

        private void SetBusy(bool busy, string message)
        {
            _templateBox.Enabled = !busy;
            _modName.Enabled = !busy;
            _description.Enabled = !busy;
            _cost.Enabled = !busy;
            _kudosh.Enabled = !busy;
            _imagePath.Enabled = !busy;
            bool templateUsesMapping = ((_templateBox.SelectedItem as TemplateDefinition) != null && ((TemplateDefinition)_templateBox.SelectedItem).ImageProcessingEnabled);
            _fitMode.Enabled = !busy && templateUsesMapping;
            _guideColor.Enabled = !busy && templateUsesMapping;
            SetGuideColorSwatchesEnabled(!busy && templateUsesMapping);
            _resetViewButton.Enabled = !busy && templateUsesMapping;
            if (_artworkBrowseButton != null)
                _artworkBrowseButton.Enabled = !busy && templateUsesMapping;
            bool doubleBannerSelected = IsDualArtworkTemplate(_templateBox.SelectedItem as TemplateDefinition);
            if (_doubleBannerLeftBrowseButton != null)
                _doubleBannerLeftBrowseButton.Enabled = !busy && doubleBannerSelected;
            if (_doubleBannerRightBrowseButton != null)
                _doubleBannerRightBrowseButton.Enabled = !busy && doubleBannerSelected;
            if (_doubleBannerLeftResetButton != null)
                _doubleBannerLeftResetButton.Enabled = !busy && doubleBannerSelected;
            if (_doubleBannerRightResetButton != null)
                _doubleBannerRightResetButton.Enabled = !busy && doubleBannerSelected;
            foreach (KeyValuePair<string, Button> pair in _doubleBannerThemeTiles)
                if (pair.Value != null) pair.Value.Enabled = !busy;
            foreach (KeyValuePair<string, Button> pair in _hangingSignSizeTiles)
                if (pair.Value != null) pair.Value.Enabled = !busy;
            foreach (KeyValuePair<string, Button> pair in _wallSignSizeTiles)
                if (pair.Value != null) pair.Value.Enabled = !busy;
            _iconMode.Enabled = !busy;
            bool customIconMode = string.Equals(GetIconMode(), "Custom Icon File", StringComparison.OrdinalIgnoreCase);
            _iconPath.Enabled = !busy && customIconMode;
            _iconBrowse.Enabled = !busy && customIconMode;
            _outputPath.Enabled = !busy;
            _rebuildEnvironmentButton.Enabled = !busy;
            _myModsButton.Enabled = !busy;
            _newProjectButton.Enabled = !busy;
            if (_state != null)
                _buildButton.Enabled = !busy && _state.UnityFound && _state.SdkFound && _state.EnvironmentReady;
            else
                _buildButton.Enabled = false;
            _activity.Text = message;

            _createProgressBusy = busy;
            if (_createProgressHideTimer != null)
                _createProgressHideTimer.Stop();
            if (_createProgressHost != null)
                _createProgressHost.Visible = busy || !string.IsNullOrEmpty(message);
            if (_createProgressLabel != null)
                _createProgressLabel.Text = string.IsNullOrEmpty(message) ? (busy ? "Building..." : "") : message;
            _progress.MarqueeAnimationSpeed = busy ? 28 : 0;
            if (!busy && _createProgressHost != null && _createProgressHost.Visible && _createProgressHideTimer != null)
                _createProgressHideTimer.Start();
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void SetCreateEnvironmentStatus(bool busy, string message)
        {
            if (_createProgressHideTimer != null)
                _createProgressHideTimer.Stop();
            if (_createProgressHost != null)
                _createProgressHost.Visible = busy || !string.IsNullOrEmpty(message);
            if (_createProgressLabel != null)
                _createProgressLabel.Text = message ?? "";
            if (_progress != null)
                _progress.MarqueeAnimationSpeed = busy ? 28 : 0;
            if (_buildButton != null)
                _buildButton.Enabled = !busy && _state != null && _state.UnityFound && _state.SdkFound && _state.EnvironmentReady;
            if (!busy && _createProgressHost != null && _createProgressHost.Visible && _createProgressHideTimer != null)
                _createProgressHideTimer.Start();
        }

        private void SafeSetCreateEnvironmentStatus(bool busy, string message)
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
            {
                BeginInvoke((Action<bool, string>)SafeSetCreateEnvironmentStatus, busy, message);
                return;
            }
            SetCreateEnvironmentStatus(busy, message);
        }

        private void SafeSetMyModsBuildProgress(bool busy, string message)
        {
            if (_embeddedMyMods == null || _embeddedMyMods.IsDisposed)
                return;
            _embeddedMyMods.SetBatchBuildProgress(busy, message);
        }

        private void SafeAppendLog(string message)
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
                BeginInvoke((Action<string>)AppendLog, message);
            else
                AppendLog(message);
        }

        private void SafeSetActivity(string message)
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
                BeginInvoke((Action<string>)SafeSetActivity, message);
            else
            {
                _activity.Text = message;
                if (_createProgressBusy && _createProgressHost != null)
                {
                    _createProgressHost.Visible = true;
                    if (_createProgressLabel != null)
                        _createProgressLabel.Text = message;
                }
            }
        }

        private void AppendLog(string message)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message;
            _log.AppendText(line + Environment.NewLine);
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();

            if (_embeddedSettings != null && !_embeddedSettings.IsDisposed && _embeddedSettings.Parent != null)
                _embeddedSettings.AppendBuildLogLine(line);
        }


        private void ResetPlacement(bool refreshPreview)
        {
            _placement = new ImagePlacementState();
            _placement.GuideColorName = NormaliseGuideColorName(_guideColor != null && _guideColor.SelectedItem != null ? _guideColor.SelectedItem.ToString() : "Yellow");
            TemplateDefinition template = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            _imageProcessor.EnsureSizeFamilyReference(template, _placement);
            if (refreshPreview)
                UpdateArtworkPreview();
        }

        private void UpdateGuideColour()
        {
            if (_placement == null)
                _placement = new ImagePlacementState();
            _placement.GuideColorName = NormaliseGuideColorName(_guideColor.SelectedItem == null ? "Yellow" : _guideColor.SelectedItem.ToString());
            SyncGuideColorSwatches();
            UpdateArtworkPreview();
        }

        private Color ResolveGuideColor()
        {
            return ResolveGuideColor(_guideColor != null && _guideColor.SelectedItem != null ? _guideColor.SelectedItem.ToString() : "Yellow");
        }

        private static Color ResolveGuideColor(string guideColorName)
        {
            string value = NormaliseGuideColorName(guideColorName);
            switch (value)
            {
                case "White": return Color.White;
                case "Red": return Color.Red;
                case "Yellow": return Color.Yellow;
                case "Blue": return Color.DeepSkyBlue;
                case "Orange": return Color.Orange;
                case "Green": return Color.LimeGreen;
                case "Pink": return Color.DeepPink;
                case "Purple": return Color.MediumPurple;
                default: return Color.Yellow;
            }
        }

        private PictureBox GetActiveArtworkPreviewBox()
        {
            TemplateDefinition template = _templateBox == null ? null : _templateBox.SelectedItem as TemplateDefinition;
            if (IsDualArtworkTemplate(template) && _doubleBannerEditingRight && _doubleBannerRightPreview != null)
                return _doubleBannerRightPreview;
            return _imagePreview;
        }

        private bool IsRightDoubleBannerPreviewSender(object sender)
        {
            Control control = sender as Control;
            if (control == null) return false;
            return object.ReferenceEquals(control, _doubleBannerRightPreview) ||
                string.Equals(control.Tag as string, "DoubleBannerRight", StringComparison.Ordinal);
        }

        private void ImagePreview_MouseDown(object sender, MouseEventArgs e)
        {
            TemplateDefinition template = _templateBox.SelectedItem as TemplateDefinition;
            PictureBox preview = sender as PictureBox ?? _imagePreview;
            if (IsDualArtworkTemplate(template))
                ActivateDoubleBannerSide(IsRightDoubleBannerPreviewSender(sender), false);

            if (e.Button != MouseButtons.Left || template == null || !template.ImageProcessingEnabled || preview.Image == null)
                return;

            _isDraggingPreview = true;
            _lastPreviewMouse = e.Location;
            preview.Capture = true;
            preview.Focus();
        }

        private void ImagePreview_MouseMove(object sender, MouseEventArgs e)
        {
            PictureBox preview = sender as PictureBox ?? GetActiveArtworkPreviewBox();
            if (!_isDraggingPreview || preview == null || preview.Image == null)
                return;

            TemplateDefinition template = _templateBox.SelectedItem as TemplateDefinition;
            if (template == null || !template.ImageProcessingEnabled)
                return;

            float previewScale = GetPreviewTextureScale(template, preview);
            if (previewScale <= 0.0001f)
                return;

            _placement.OffsetX += (e.X - _lastPreviewMouse.X) / previewScale;
            _placement.OffsetY += (e.Y - _lastPreviewMouse.Y) / previewScale;
            _lastPreviewMouse = e.Location;

            // Commit Double Banner panning continuously. The old behaviour only wrote
            // the new offsets back on MouseUp, so a committed preview refresh could load
            // the pane's previous stored placement and visually snap the artwork back.
            CaptureActiveDoubleBannerSideState();
            QueueInteractiveArtworkPreview();
        }

        private void ImagePreview_MouseUp(object sender, MouseEventArgs e)
        {
            PictureBox preview = sender as PictureBox ?? GetActiveArtworkPreviewBox();
            _isDraggingPreview = false;
            if (preview != null)
                preview.Capture = false;
            _interactivePreviewTimer.Stop();
            _interactivePreviewPending = false;
            _previewCommitTimer.Stop();
            CaptureActiveDoubleBannerSideState();
            UpdateArtworkPreview();
        }

        private void ImagePreview_MouseWheel(object sender, MouseEventArgs e)
        {
            TemplateDefinition template = _templateBox.SelectedItem as TemplateDefinition;
            if (IsDualArtworkTemplate(template))
                ActivateDoubleBannerSide(IsRightDoubleBannerPreviewSender(sender), false);
            if (template == null || !template.ImageProcessingEnabled || !File.Exists(_imagePath.Text))
                return;

            double wheelSteps = e.Delta / 120.0;
            bool controlHeld = (ModifierKeys & Keys.Control) == Keys.Control;
            bool shiftHeld = (ModifierKeys & Keys.Shift) == Keys.Shift;

            if (controlHeld)
            {
                float degreesPerStep = shiftHeld ? 1.0f : 5.0f;
                _placement.RotationDegrees = NormaliseRotationDegrees(_placement.RotationDegrees + (float)(degreesPerStep * wheelSteps));
            }
            else
            {
                double zoomPerStep = shiftHeld ? 1.0025 : 1.01;
                float factor = (float)Math.Pow(zoomPerStep, wheelSteps);
                _placement.Zoom = Math.Max(0.10f, Math.Min(8.0f, _placement.Zoom * factor));
            }

            CaptureActiveDoubleBannerSideState();
            QueueArtworkInteractionPreview();
        }

        private void ImagePreview_KeyDown(object sender, KeyEventArgs e)
        {
            TemplateDefinition template = _templateBox.SelectedItem as TemplateDefinition;
            if (IsDualArtworkTemplate(template))
                ActivateDoubleBannerSide(IsRightDoubleBannerPreviewSender(sender), false);
            if (template == null || !template.ImageProcessingEnabled || !File.Exists(_imagePath.Text))
                return;

            if (e.Control || e.Alt)
                return;

            float step = e.Shift ? 1.0f : 5.0f;
            bool handled = true;
            switch (e.KeyCode)
            {
                case Keys.Left:
                    _placement.OffsetX -= step;
                    break;
                case Keys.Right:
                    _placement.OffsetX += step;
                    break;
                case Keys.Up:
                    _placement.OffsetY -= step;
                    break;
                case Keys.Down:
                    _placement.OffsetY += step;
                    break;
                default:
                    handled = false;
                    break;
            }

            if (!handled)
                return;

            e.Handled = true;
            e.SuppressKeyPress = true;
            CaptureActiveDoubleBannerSideState();
            QueueArtworkInteractionPreview();
        }

        private void ImagePreview_DoubleClick(object sender, EventArgs e)
        {
            TemplateDefinition template = _templateBox.SelectedItem as TemplateDefinition;
            if (IsDualArtworkTemplate(template))
                ActivateDoubleBannerSide(IsRightDoubleBannerPreviewSender(sender), false);

            if ((ModifierKeys & Keys.Control) == Keys.Control)
            {
                if (Math.Abs(_placement.RotationDegrees) > 0.0001f)
                {
                    _placement.RotationDegrees = 0.0f;
                    CaptureActiveDoubleBannerSideState();
                    UpdateArtworkPreview();
                }
                return;
            }

            ResetPlacement(true);
        }

        private void QueueArtworkInteractionPreview()
        {
            QueueInteractiveArtworkPreview();
            _previewCommitTimer.Stop();
            _previewCommitTimer.Start();
        }

        private static float NormaliseRotationDegrees(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0.0f;

            value %= 360.0f;
            if (value < 0.0f)
                value += 360.0f;
            if (Math.Abs(value - 360.0f) < 0.0001f || Math.Abs(value) < 0.0001f)
                return 0.0f;
            return value;
        }

        private static string FormatRotationDegrees(float value)
        {
            float normalised = NormaliseRotationDegrees(value);
            return normalised.ToString("0.#") + "°";
        }

        private float GetPreviewTextureScale(TemplateDefinition template, PictureBox preview)
        {
            if (preview == null || preview.Image == null || template == null)
                return 0f;
            return _imageProcessor.GetPreviewPixelsPerTexturePixel(template, preview.Image.Width, preview.Image.Height);
        }

        private static bool IsWallpaperTemplate(TemplateDefinition template)
        {
            return template != null && string.Equals(template.Key, "Wallpaper", StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyWallpaperCreateRules(bool wallpaper)
        {
            if (!wallpaper)
            {
                if (_costHolder != null) _costHolder.Enabled = true;
                if (_decorPackOptionsLabel != null) _decorPackOptionsLabel.Visible = false;
                if (_decorPackBox != null) _decorPackBox.Visible = false;
                if (_decorPackHelpLabel != null) _decorPackHelpLabel.Visible = false;
                return;
            }

            if (_cost != null && _cost.Value != 0) _cost.Value = 0;
            if (_kudosh != null && _kudosh.Value != 0) _kudosh.Value = 0;
            if (_costHolder != null) _costHolder.Enabled = false;
            if (_kudoshHolder != null) _kudoshHolder.Enabled = false;
            if (_costSlider != null) _costSlider.Enabled = false;
            if (_kudoshSlider != null) _kudoshSlider.Enabled = false;
            if (_variantOptionsLabel != null) _variantOptionsLabel.Visible = false;
            if (_variantParentBox != null) _variantParentBox.Visible = false;
            if (_variantHelpLabel != null) _variantHelpLabel.Visible = false;
            if (_decorPackOptionsLabel != null) _decorPackOptionsLabel.Visible = true;
            if (_decorPackBox != null) _decorPackBox.Visible = true;
            if (_decorPackHelpLabel != null) _decorPackHelpLabel.Visible = true;
        }

        private static bool IsPosterTemplate(TemplateDefinition template)
        {
            if (template == null || string.IsNullOrEmpty(template.Key))
                return false;
            string key = template.Key;
            return string.Equals(key, "Poster", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Small Poster", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Standard Poster", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Tall Poster", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRugTemplate(TemplateDefinition template)
        {
            return template != null && !string.IsNullOrEmpty(template.Key) && template.Key.IndexOf("Rug", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsDoubleBannerTemplate(TemplateDefinition template)
        {
            return template != null && !string.IsNullOrEmpty(template.Key) &&
                template.Key.EndsWith(" Double Banner", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHangingSignTemplate(TemplateDefinition template)
        {
            return template != null && !string.IsNullOrEmpty(template.Key) &&
                template.Key.EndsWith(" Hanging Sign", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWallSignTemplate(TemplateDefinition template)
        {
            return template != null && !string.IsNullOrEmpty(template.Key) &&
                template.Key.EndsWith(" Wall Sign", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTwoSidedSignTemplate(TemplateDefinition template)
        {
            return IsHangingSignTemplate(template) || IsWallSignTemplate(template);
        }

        private static bool IsDualArtworkTemplate(TemplateDefinition template)
        {
            return IsDoubleBannerTemplate(template) || IsTwoSidedSignTemplate(template);
        }

        private static bool IsSingleBannerTemplate(TemplateDefinition template)
        {
            return template != null && !string.IsNullOrEmpty(template.Key) &&
                template.Key.EndsWith(" Banner", StringComparison.OrdinalIgnoreCase) &&
                !IsDualArtworkTemplate(template);
        }

        private static bool IsBannerTemplate(TemplateDefinition template)
        {
            return IsSingleBannerTemplate(template) || IsDualArtworkTemplate(template);
        }

        private static void SetStatus(Label label, bool ok, string title, string detail)
        {
            label.Text = (ok ? "✓ " : "✗ ") + title + ": " + detail;
            label.ForeColor = ok ? Color.FromArgb(25, 125, 65) : Color.Firebrick;
        }

        private static Label NewStatusLabel(int x, int y, int width)
        {
            Label label = new Label();
            label.Location = new Point(x, y);
            label.Size = new Size(width, 20);
            return label;
        }

        private static void AddLabel(Control parent, string text, int x, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.Location = new Point(x, y);
            label.Size = new Size(120, 22);
            parent.Controls.Add(label);
        }

        private static PictureBox NewArtworkPreviewBox(int x, int y, int width, int height)
        {
            ArtworkPictureBox box = new ArtworkPictureBox();
            ConfigurePreviewBox(box, x, y, width, height);
            return box;
        }

        private static PictureBox NewPreviewBox(int x, int y, int width, int height)
        {
            PictureBox box = new PictureBox();
            ConfigurePreviewBox(box, x, y, width, height);
            return box;
        }

        private static void ConfigurePreviewBox(PictureBox box, int x, int y, int width, int height)
        {
            box.Location = new Point(x, y);
            box.Size = new Size(width, height);
            box.BorderStyle = BorderStyle.FixedSingle;
            box.SizeMode = PictureBoxSizeMode.CenterImage;
            box.BackColor = Color.FromArgb(42, 42, 42);
            box.TabStop = true;
            box.Cursor = Cursors.SizeAll;
        }
        private static void ClearPreview(PictureBox box)
        {
            if (box.Image != null)
            {
                Image old = box.Image;
                box.Image = null;
                old.Dispose();
            }
        }
    }


}

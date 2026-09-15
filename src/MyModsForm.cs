using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TPMSimpleModMaker
{
    internal sealed class MyModsForm : Form
    {
        private sealed class VariantFamilyGroup
        {
            public string Key;
            public bool BaseGameParent;
            public bool DecorPack;
            public ModProjectRecord ParentRecord;
            public string ParentName;
            public string ItemType;
            public readonly List<ModProjectRecord> Children = new List<ModProjectRecord>();
        }

        private sealed class VariantFamilyHeaderTag
        {
            public VariantFamilyGroup Family;
        }

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
        private readonly ProjectLibraryService _library;
        private readonly bool _embeddedMode;
        private readonly ImageProcessingService _imageProcessor;
        private readonly List<TemplateDefinition> _templates;

        private TextBox _searchBox;
        private ComboBox _typeFilter;
        private ComboBox _displayFilter;
        private ComboBox _statusFilter;
        private ComboBox _sortBy;
        private Button _sortDirectionButton;
        private Button _familyExpandCollapseButton;
        private DataGridView _grid;
        private TwoPointVerticalScrollBar _gridScrollBar;
        private Panel _detailsViewport;
        private Panel _detailsContent;
        private TwoPointVerticalScrollBar _detailsScrollBar;
        private Label _countLabel;

        private PictureBox _preview;
        private Button _changeArtworkButton;
        private Label _doubleBannerSideLabel;
        private Button _doubleBannerLeftButton;
        private Button _doubleBannerRightButton;
        private TextBox _nameBox;
        private TextBox _descriptionBox;
        private TextBox _itemTypeBox;
        private Panel _itemOptionsPanel;
        private Panel _itemOptionsHolder;
        private Panel _recolourablePanel;
        private TwoPointToggle _recolourableToggle;
        private Panel _variantOptionsPanel;
        private Label _variantOptionsTitleLabel;
        private ComboBox _variantParentBox;
        private Button _manageFamilyButton;
        private Label _variantHelpLabel;
        private NumericUpDown _costValue;
        private NumericUpDown _kudoshValue;
        private TwoPointSlider _costSlider;
        private TwoPointSlider _kudoshSlider;
        private Panel _costHolder;
        private Panel _kudoshHolder;
        private TextBox _modIdBox;
        private Label _detailStateLabel;
        private Label _multiSelectionMessage;
        private string _detailShape;
        private string _detailOptionIconFile;

        private Button _editButton;
        private Button _rebuildButton;
        private Button _openButton;
        private Button _workshopButton;
        private Button _workshopToolsButton;
        private ContextMenuStrip _workshopToolsMenu;
        private ToolStripMenuItem _openWorkshopPageMenuItem;
        private ToolStripMenuItem _refreshWorkshopStatusMenuItem;
        private ToolStripMenuItem _relinkWorkshopMenuItem;
        private ToolStripMenuItem _unlinkWorkshopMenuItem;

        // Right-click actions for the main My Mods list. These mirror the bottom action bar
        // so there is one source of truth for availability and wording.
        private ContextMenuStrip _myModsContextMenu;
        private ToolStripMenuItem _contextRebuildMenuItem;
        private ToolStripMenuItem _contextEditMenuItem;
        private ToolStripMenuItem _contextOpenInstalledMenuItem;
        private ToolStripMenuItem _contextWorkshopMenuItem;
        private ToolStripMenuItem _contextOpenWorkshopPageMenuItem;
        private ToolStripMenuItem _contextRefreshWorkshopStatusMenuItem;
        private ToolStripMenuItem _contextRelinkWorkshopMenuItem;
        private ToolStripMenuItem _contextUnlinkWorkshopMenuItem;
        private ToolTip _uiToolTip;
        private Button _saveButton;
        private Button _deleteButton;
        private Panel _batchProgressHost;
        private Label _batchProgressLabel;
        private ProgressBar _batchProgressBar;
        private Timer _batchProgressHideTimer;

        private List<ModProjectRecord> _projects;
        private ModProjectRecord _selectedRecord;
        private string _pendingArtworkPath;
        private string _pendingSecondaryArtworkPath;
        private bool _editRightDoubleBanner;
        private bool _detailsDirty;
        private bool _loadingDetails;
        private bool _suppressSelectionEvents;
        private bool _sortDescending = true;
        private bool _batchBusy;
        private bool _selectionSyncPending;
        private readonly HashSet<string> _collapsedFamilyKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _knownFamilyKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, VariantFamilyGroup> _familyByMemberId = new Dictionary<string, VariantFamilyGroup>(StringComparer.Ordinal);
        private readonly Dictionary<string, Bitmap> _gridPreviewCache = new Dictionary<string, Bitmap>(StringComparer.Ordinal);
        private readonly object _gridPreviewCacheLock = new object();
        private readonly Dictionary<string, string> _viewInstalledStatusCache = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _viewWorkshopStatusCache = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, WorkshopFamilyState> _viewWorkshopFamilyStateCache = new Dictionary<string, WorkshopFamilyState>(StringComparer.Ordinal);
        private bool _familyIndexDirty = true;
        private bool _skipGridPreviewImages;
        private int _previewLoadGeneration;
        private bool _initialProjectsLoaded;

        public MyModsSelection Selection { get; private set; }
        public event EventHandler SelectionRequested;

        public MyModsForm(ProjectLibraryService library) : this(library, false)
        {
        }

        public MyModsForm(ProjectLibraryService library, bool embeddedMode)
        {
            _library = library;
            _embeddedMode = embeddedMode;
            _imageProcessor = new ImageProcessingService();
            _templates = LoadTemplateDefinitions();
            Selection = new MyModsSelection();

            Text = "Memento Maker - My Mods";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1180, 720);
            MinimumSize = new Size(900, 560);
            BackColor = TwoPointTheme.ContentBackground;
            Font = TwoPointTheme.BodyFont(9F);

            BuildLayout();
            InitialiseUiToolTips();
            WireEvents();
            if (!_embeddedMode)
            {
                LoadProjects(null);
                _initialProjectsLoaded = true;
            }

            if (_embeddedMode)
            {
                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.Manual;
            }
            else
            {
                TwoPointTheme.InstallShell(this, "My Mods", delegate { Close(); }, null, null);
            }
        }

        private void BuildLayout()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.BackColor = TwoPointTheme.ContentBackground;
            root.Padding = new Padding(7);
            root.ColumnCount = 2;
            root.RowCount = 2;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 61F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 39F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            Controls.Add(root);

            TableLayoutPanel left = new TableLayoutPanel();
            left.Dock = DockStyle.Fill;
            left.Margin = new Padding(0, 0, 5, 5);
            left.BackColor = Color.Transparent;
            left.ColumnCount = 1;
            left.RowCount = 2;
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 114F));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(left, 0, 0);

            Panel filterSection = CreateSection("Search, Filter & Sort");
            filterSection.Margin = new Padding(0, 0, 0, 5);
            left.Controls.Add(filterSection, 0, 0);
            BuildFilters((Panel)filterSection.Tag);

            Panel listSection = CreateSection("Your Mods");
            listSection.Margin = new Padding(0);
            left.Controls.Add(listSection, 0, 1);
            BuildList((Panel)listSection.Tag);

            Panel detailsSection = CreateSection("Mod Details");
            detailsSection.Margin = new Padding(0, 0, 0, 5);
            root.Controls.Add(detailsSection, 1, 0);
            BuildDetails((Panel)detailsSection.Tag);

            Panel actionsSection = new Panel();
            actionsSection.Dock = DockStyle.Fill;
            actionsSection.Margin = new Padding(0);
            actionsSection.BackColor = TwoPointTheme.ContentBackground;
            actionsSection.BorderStyle = BorderStyle.None;
            root.SetColumnSpan(actionsSection, 2);
            root.Controls.Add(actionsSection, 0, 1);
            BuildActions(actionsSection);
        }

        private Panel CreateSection(string titleText)
        {
            Panel outer = new Panel();
            outer.Dock = DockStyle.Fill;
            outer.BackColor = TwoPointTheme.PanelLight;
            outer.BorderStyle = BorderStyle.FixedSingle;

            Label title = new Label();
            title.Text = titleText;
            title.Location = new Point(10, 7);
            title.Size = new Size(190, 28);
            TwoPointTheme.StyleSectionHeader(title);
            outer.Controls.Add(title);

            Panel body = new Panel();
            body.Location = new Point(8, 39);
            body.Size = new Size(Math.Max(50, outer.ClientSize.Width - 16), Math.Max(30, outer.ClientSize.Height - 47));
            body.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            body.BackColor = TwoPointTheme.PanelLight;
            body.BorderStyle = BorderStyle.None;
            outer.Controls.Add(body);
            outer.Tag = body;
            return outer;
        }

        private void BuildFilters(Panel body)
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 6;
            layout.RowCount = 2;
            layout.Padding = new Padding(5, 3, 5, 5);
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 17F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 23F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            body.Controls.Add(layout);

            layout.Controls.Add(NewFieldLabel("Search"), 0, 0);
            layout.Controls.Add(NewFieldLabel("Item Type"), 1, 0);
            layout.Controls.Add(NewFieldLabel("Mod Type"), 2, 0);
            layout.Controls.Add(NewFieldLabel("Status"), 3, 0);
            layout.Controls.Add(NewFieldLabel("Sort By"), 4, 0);

            _searchBox = new TextBox();
            _searchBox.Dock = DockStyle.Fill;
            _searchBox.AutoSize = false;
            _searchBox.Margin = new Padding(0, 2, 10, 4);
            _searchBox.BackColor = TwoPointTheme.FieldBackground;
            _searchBox.ForeColor = TwoPointTheme.BodyText;
            _searchBox.Font = TwoPointTheme.BodyFont(9.25F);
            layout.Controls.Add(_searchBox, 0, 1);

            _typeFilter = NewCombo();
            _typeFilter.Items.AddRange(new object[] { "All Types", "Décor", "Poster", "Mural", "Small Rug", "Large Rug", "Banner", "Double Banner", "Hanging Sign", "Wall Sign" });
            _typeFilter.SelectedIndex = 0;
            layout.Controls.Add(_typeFilter, 1, 1);

            _displayFilter = NewCombo();
            _displayFilter.Items.AddRange(new object[] { "All Mods", "Families / Packs", "Stand Alone" });
            _displayFilter.SelectedIndex = 0;
            layout.Controls.Add(_displayFilter, 2, 1);

            _statusFilter = NewCombo();
            _statusFilter.Items.AddRange(new object[]
            {
                "All",
                "Installed",
                "Installed (old)",
                "Not Installed",
                "Published",
                "Published (old)",
                "Not Published",
                "Missing"
            });
            _statusFilter.SelectedIndex = 0;
            layout.Controls.Add(_statusFilter, 3, 1);

            _sortBy = NewCombo();
            _sortBy.Items.AddRange(new object[] { "Last Built", "Mod Name", "Item Type" });
            _sortBy.SelectedIndex = 0;
            layout.Controls.Add(_sortBy, 4, 1);

            TwoPointSortDirectionButton sortDirection = new TwoPointSortDirectionButton();
            sortDirection.Descending = _sortDescending;
            sortDirection.Dock = DockStyle.None;
            sortDirection.Size = new Size(40, 30);
            sortDirection.Anchor = AnchorStyles.None;
            sortDirection.Margin = new Padding(0);
            _sortDirectionButton = sortDirection;
            layout.Controls.Add(_sortDirectionButton, 5, 1);
        }

        private ComboBox NewCombo()
        {
            ComboBox combo = new ComboBox();
            combo.Dock = DockStyle.Fill;
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.FlatStyle = FlatStyle.Flat;
            combo.Margin = new Padding(0, 1, 10, 3);
            combo.BackColor = TwoPointTheme.FieldBackground;
            combo.ForeColor = TwoPointTheme.BodyText;
            combo.Font = TwoPointTheme.BodyFont(9.25F);
            return combo;
        }

        private Label NewFieldLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.ForeColor = TwoPointTheme.PrimaryText;
            label.Font = TwoPointTheme.BoldFont(9F);
            label.TextAlign = ContentAlignment.BottomLeft;
            return label;
        }

        private void BuildList(Panel body)
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 2;
            layout.Padding = new Padding(4, 0, 4, 4);
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            body.Controls.Add(layout);

            _grid = new DataGridView();
            _grid.Dock = DockStyle.Fill;
            _grid.Margin = new Padding(0);
            _grid.BackgroundColor = Color.FromArgb(248, 231, 194);
            _grid.BorderStyle = BorderStyle.None;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.AllowUserToResizeColumns = true;
            _grid.ReadOnly = true;
            _grid.MultiSelect = true;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.RowHeadersVisible = false;
            _grid.AutoGenerateColumns = false;
            _grid.EnableHeadersVisualStyles = false;
            _grid.ColumnHeadersHeight = 30;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.RowTemplate.Height = 58;
            _grid.DefaultCellStyle.BackColor = Color.FromArgb(248, 231, 194);
            _grid.DefaultCellStyle.ForeColor = TwoPointTheme.BodyText;
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 250, 236);
            _grid.DefaultCellStyle.SelectionForeColor = TwoPointTheme.PrimaryText;
            _grid.DefaultCellStyle.Font = TwoPointTheme.BodyFont(8.6F);
            _grid.DefaultCellStyle.Padding = new Padding(2, 1, 2, 1);
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(239, 207, 145);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = TwoPointTheme.PrimaryText;
            _grid.ColumnHeadersDefaultCellStyle.Font = TwoPointTheme.BoldFont(8.3F);
            _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            _grid.GridColor = Color.FromArgb(218, 181, 111);
            _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.ScrollBars = ScrollBars.None;

            DataGridViewImageColumn preview = new DataGridViewImageColumn();
            preview.Name = "Preview";
            preview.HeaderText = "Preview";
            preview.FillWeight = 11F;
            preview.MinimumWidth = 64;
            preview.ImageLayout = DataGridViewImageCellLayout.Zoom;
            preview.DefaultCellStyle.NullValue = null;
            _grid.Columns.Add(preview);

            AddTextColumn("ModName", "Mod Name", 23F, 120);
            AddTextColumn("ItemType", "Item Type", 14F, 84);
            AddTextColumn("ItemOptions", "Item Options", 11F, 65);
            AddTextColumn("Recolourable", "Recolourable", 12F, 76);
            AddTextColumn("LastBuilt", "Last Built", 15F, 92);
            AddTextColumn("Installed", "Installed", 16F, 100);
            AddTextColumn("Workshop", "Workshop", 17F, 110);

            _grid.Columns["ModName"].DefaultCellStyle.Font = TwoPointTheme.BoldFont(8.7F);
            _grid.Columns["ItemOptions"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.Columns["Recolourable"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.Columns["Recolourable"].DefaultCellStyle.Font = TwoPointTheme.BoldFont(9F);
            _grid.Columns["LastBuilt"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            _grid.Columns["Installed"].DefaultCellStyle.Font = TwoPointTheme.BoldFont(8.3F);

            TableLayoutPanel gridHost = new TableLayoutPanel();
            gridHost.Dock = DockStyle.Fill;
            gridHost.Margin = new Padding(0);
            gridHost.ColumnCount = 2;
            gridHost.RowCount = 1;
            gridHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            gridHost.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 18F));
            gridHost.Controls.Add(_grid, 0, 0);

            _gridScrollBar = new TwoPointVerticalScrollBar();
            _gridScrollBar.Dock = DockStyle.Fill;
            _gridScrollBar.Margin = new Padding(2, 0, 0, 0);
            gridHost.Controls.Add(_gridScrollBar, 1, 0);
            layout.Controls.Add(gridHost, 0, 0);

            TableLayoutPanel footer = new TableLayoutPanel();
            footer.Dock = DockStyle.Fill;
            footer.ColumnCount = 3;
            footer.RowCount = 1;
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142F));
            layout.Controls.Add(footer, 0, 1);

            _countLabel = new Label();
            _countLabel.Dock = DockStyle.Fill;
            _countLabel.ForeColor = TwoPointTheme.BodyText;
            _countLabel.Font = TwoPointTheme.BodyFont(8.5F);
            _countLabel.TextAlign = ContentAlignment.MiddleLeft;
            footer.Controls.Add(_countLabel, 0, 0);

            Label selectionHelp = new Label();
            selectionHelp.Dock = DockStyle.Fill;
            selectionHelp.ForeColor = Color.FromArgb(115, 105, 89);
            selectionHelp.Font = TwoPointTheme.BodyFont(7.8F);
            selectionHelp.Text = "Ctrl/Shift + click = select multiple mods";
            selectionHelp.TextAlign = ContentAlignment.MiddleRight;
            footer.Controls.Add(selectionHelp, 1, 0);

            _familyExpandCollapseButton = new Button();
            _familyExpandCollapseButton.Text = "Expand All";
            _familyExpandCollapseButton.Dock = DockStyle.Fill;
            _familyExpandCollapseButton.Margin = new Padding(6, 5, 8, 5);
            _familyExpandCollapseButton.Font = TwoPointTheme.BodyFont(8.0F);
            _familyExpandCollapseButton.TextAlign = ContentAlignment.MiddleCenter;
            _familyExpandCollapseButton.Padding = new Padding(0);
            TwoPointTheme.StyleButton(_familyExpandCollapseButton);
            footer.Controls.Add(_familyExpandCollapseButton, 2, 0);
        }

        private void AddTextColumn(string name, string header, float weight, int minimumWidth)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.FillWeight = weight;
            column.MinimumWidth = minimumWidth;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            _grid.Columns.Add(column);
        }

        private void BuildDetails(Panel body)
        {
            // The details page uses the same themed scrollbar as the rest of Memento Maker.
            // Do not use Panel.AutoScroll here because WinForms would draw an unthemed native
            // scrollbar.  Instead, keep a fixed-height content surface inside a clipped viewport
            // and move it with TwoPointVerticalScrollBar when a smaller window genuinely needs
            // scrolling.
            body.AutoScroll = false;

            TableLayoutPanel scrollHost = new TableLayoutPanel();
            scrollHost.Dock = DockStyle.Fill;
            scrollHost.Margin = new Padding(0);
            scrollHost.ColumnCount = 2;
            scrollHost.RowCount = 1;
            scrollHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            scrollHost.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 18F));
            body.Controls.Add(scrollHost);

            _detailsViewport = new Panel();
            _detailsViewport.Dock = DockStyle.Fill;
            _detailsViewport.Margin = new Padding(0);
            _detailsViewport.BackColor = TwoPointTheme.PanelLight;
            _detailsViewport.BorderStyle = BorderStyle.None;
            scrollHost.Controls.Add(_detailsViewport, 0, 0);

            _detailsContent = new Panel();
            _detailsContent.Location = new Point(0, 0);
            _detailsContent.Size = new Size(Math.Max(1, _detailsViewport.ClientSize.Width), 734);
            _detailsContent.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _detailsContent.BackColor = TwoPointTheme.PanelLight;
            _detailsViewport.Controls.Add(_detailsContent);

            _multiSelectionMessage = new Label();
            _multiSelectionMessage.Dock = DockStyle.Fill;
            _multiSelectionMessage.Padding = new Padding(18, 18, 18, 0);
            _multiSelectionMessage.BackColor = TwoPointTheme.PanelLight;
            _multiSelectionMessage.ForeColor = TwoPointTheme.BodyText;
            _multiSelectionMessage.Font = TwoPointTheme.BoldFont(9.0F);
            _multiSelectionMessage.TextAlign = ContentAlignment.TopLeft;
            _multiSelectionMessage.Visible = false;
            _detailsViewport.Controls.Add(_multiSelectionMessage);
            _multiSelectionMessage.BringToFront();

            _detailsScrollBar = new TwoPointVerticalScrollBar();
            _detailsScrollBar.Dock = DockStyle.Fill;
            _detailsScrollBar.Margin = new Padding(2, 0, 0, 0);
            _detailsScrollBar.Visible = false;
            scrollHost.Controls.Add(_detailsScrollBar, 1, 0);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.AutoSize = false;
            layout.Padding = new Padding(5, 0, 5, 4);
            layout.ColumnCount = 1;
            layout.RowCount = 9;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220F)); // artwork
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));  // change artwork
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));  // static identity
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));  // name
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));  // description
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F));  // cost
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));  // kudosh
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86F));  // recolourable + parent
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));  // local + Workshop state
            _detailsContent.Controls.Add(layout);

            Panel previewHolder = new Panel();
            previewHolder.Dock = DockStyle.Fill;
            previewHolder.Margin = new Padding(0, 0, 0, 4);
            previewHolder.BackColor = TwoPointTheme.FieldBackground;
            previewHolder.BorderStyle = BorderStyle.FixedSingle;
            layout.Controls.Add(previewHolder, 0, 0);

            Label artworkHeader = NewFieldLabel("Artwork / Image");
            artworkHeader.Dock = DockStyle.None;
            artworkHeader.Location = new Point(7, 5);
            artworkHeader.Size = new Size(180, 20);
            previewHolder.Controls.Add(artworkHeader);

            _preview = new PictureBox();
            _preview.Location = new Point(6, 28);
            _preview.Size = new Size(Math.Max(80, previewHolder.ClientSize.Width - 12), Math.Max(60, previewHolder.ClientSize.Height - 34));
            _preview.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _preview.Padding = new Padding(6);
            _preview.BackColor = TwoPointTheme.FieldBackground;
            _preview.SizeMode = PictureBoxSizeMode.Zoom;
            previewHolder.Controls.Add(_preview);

            TableLayoutPanel changeRow = new TableLayoutPanel();
            changeRow.Dock = DockStyle.Fill;
            changeRow.ColumnCount = 5;
            changeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            changeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82F));
            changeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58F));
            changeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58F));
            changeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95F));
            layout.Controls.Add(changeRow, 0, 1);

            _changeArtworkButton = new Button();
            _changeArtworkButton.Text = "Change Artwork / Image...";
            _changeArtworkButton.Size = new Size(185, 31);
            _changeArtworkButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _changeArtworkButton.Margin = new Padding(0, 5, 7, 0);
            TwoPointTheme.StyleButton(_changeArtworkButton);
            changeRow.Controls.Add(_changeArtworkButton, 0, 0);

            _doubleBannerSideLabel = NewFieldLabel("Editing Side");
            _doubleBannerSideLabel.Dock = DockStyle.Fill;
            _doubleBannerSideLabel.TextAlign = ContentAlignment.MiddleRight;
            _doubleBannerSideLabel.Margin = new Padding(0, 0, 4, 0);
            _doubleBannerSideLabel.Visible = false;
            changeRow.Controls.Add(_doubleBannerSideLabel, 1, 0);

            _doubleBannerLeftButton = new Button();
            _doubleBannerLeftButton.Text = "Left";
            _doubleBannerLeftButton.Dock = DockStyle.Fill;
            _doubleBannerLeftButton.Margin = new Padding(2, 7, 2, 7);
            _doubleBannerLeftButton.Visible = false;
            _doubleBannerLeftButton.Click += delegate { SetMyModsDoubleBannerSide(false); };
            TwoPointTheme.StyleSegmentButton(_doubleBannerLeftButton, true);
            SetUiTip(_doubleBannerLeftButton, "Edit the artwork displayed on the left banner.");
            changeRow.Controls.Add(_doubleBannerLeftButton, 2, 0);

            _doubleBannerRightButton = new Button();
            _doubleBannerRightButton.Text = "Right";
            _doubleBannerRightButton.Dock = DockStyle.Fill;
            _doubleBannerRightButton.Margin = new Padding(2, 7, 2, 7);
            _doubleBannerRightButton.Visible = false;
            _doubleBannerRightButton.Click += delegate { SetMyModsDoubleBannerSide(true); };
            TwoPointTheme.StyleSegmentButton(_doubleBannerRightButton, false);
            SetUiTip(_doubleBannerRightButton, "Edit the artwork displayed on the right banner.");
            changeRow.Controls.Add(_doubleBannerRightButton, 3, 0);

            Label formats = new Label();
            formats.Text = "PNG, JPG\nMax 8MB";
            formats.Dock = DockStyle.Fill;
            formats.ForeColor = TwoPointTheme.BodyText;
            formats.Font = TwoPointTheme.BodyFont(7.7F);
            formats.TextAlign = ContentAlignment.MiddleLeft;
            changeRow.Controls.Add(formats, 4, 0);

            // Static identity: these values describe what the saved mod is based on and
            // remain intentionally non-editable on My Mods. Item Options now sits on
            // this same identity line so the immutable information is grouped together.
            TableLayoutPanel identity = new TableLayoutPanel();
            identity.Dock = DockStyle.Fill;
            identity.ColumnCount = 3;
            identity.RowCount = 1;
            identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            identity.Margin = new Padding(0);
            layout.Controls.Add(identity, 0, 2);
            _itemTypeBox = BuildReadOnlyField(identity, 0, "Item Type");
            _modIdBox = BuildReadOnlyField(identity, 1, "Item Mod ID");
            _itemOptionsPanel = BuildReadOnlyOptionsField(identity, 2, "Item Options");
            _itemOptionsHolder = _itemOptionsPanel == null ? null : _itemOptionsPanel.Parent as Panel;

            _nameBox = BuildLabeledTextBox(layout, 3, "Mod Name", false);
            _descriptionBox = BuildLabeledTextBox(layout, 4, "Description", true);

            BuildMoneyControl(layout, 5, "Cost", true);
            BuildMoneyControl(layout, 6, "Kudosh", false);

            // Keep Variant Options beside Recolourable in a fixed two-column row.  The left
            // Recolourable slot remains reserved even for non-rug items, so Variant Options never
            // shifts left or changes size just because the Recolourable control is hidden.
            TableLayoutPanel optionRow = new TableLayoutPanel();
            optionRow.Dock = DockStyle.Fill;
            optionRow.Margin = new Padding(7, 1, 7, 0);
            optionRow.ColumnCount = 2;
            optionRow.RowCount = 1;
            optionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
            optionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.Controls.Add(optionRow, 0, 7);

            _recolourablePanel = new Panel();
            _recolourablePanel.Dock = DockStyle.Fill;
            _recolourablePanel.Margin = new Padding(0);
            optionRow.Controls.Add(_recolourablePanel, 0, 0);

            Label recolourTitle = NewFieldLabel("Recolourable");
            recolourTitle.Dock = DockStyle.None;
            recolourTitle.Location = new Point(0, 0);
            recolourTitle.Size = new Size(105, 18);
            _recolourablePanel.Controls.Add(recolourTitle);

            _recolourableToggle = new TwoPointToggle();
            _recolourableToggle.Location = new Point(0, 21);
            _recolourableToggle.Size = new Size(78, 30);
            _recolourablePanel.Controls.Add(_recolourableToggle);

            Panel variantRow = new Panel();
            _variantOptionsPanel = variantRow;
            variantRow.Dock = DockStyle.Fill;
            variantRow.Margin = new Padding(10, 0, 0, 0);
            optionRow.Controls.Add(variantRow, 1, 0);

            _variantOptionsTitleLabel = NewFieldLabel("Variant Options");
            _variantOptionsTitleLabel.Dock = DockStyle.None;
            _variantOptionsTitleLabel.Location = new Point(0, 0);
            _variantOptionsTitleLabel.Size = new Size(150, 18);
            variantRow.Controls.Add(_variantOptionsTitleLabel);

            _variantParentBox = new VariantParentComboBox();
            _variantParentBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _variantParentBox.Location = new Point(0, 21);
            _variantParentBox.Size = new Size(Math.Max(180, variantRow.ClientSize.Width - 102), 27);
            _variantParentBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _variantParentBox.BackColor = TwoPointTheme.FieldBackground;
            _variantParentBox.ForeColor = TwoPointTheme.BodyText;
            _variantParentBox.Font = TwoPointTheme.BodyFont(8.7F);
            variantRow.Controls.Add(_variantParentBox);

            _manageFamilyButton = new Button();
            _manageFamilyButton.Text = "Manage...";
            _manageFamilyButton.Size = new Size(92, 27);
            _manageFamilyButton.Location = new Point(Math.Max(184, variantRow.ClientSize.Width - 94), 20);
            _manageFamilyButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _manageFamilyButton.Font = TwoPointTheme.BodyFont(8.2F);
            TwoPointTheme.StyleButton(_manageFamilyButton);
            _manageFamilyButton.Click += delegate { ManageSelectedFamily(); };
            SetUiTip(_manageFamilyButton, "Add or remove several mods from this variant family at once.");
            variantRow.Controls.Add(_manageFamilyButton);

            _variantHelpLabel = new Label();
            _variantHelpLabel.Location = new Point(0, 51);
            _variantHelpLabel.Size = new Size(Math.Max(180, variantRow.ClientSize.Width - 4), 32);
            _variantHelpLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _variantHelpLabel.ForeColor = Color.FromArgb(108, 99, 84);
            _variantHelpLabel.Font = TwoPointTheme.BodyFont(7.4F);
            _variantHelpLabel.AutoEllipsis = true;
            variantRow.Controls.Add(_variantHelpLabel);

            variantRow.Resize += delegate
            {
                int available = Math.Max(180, variantRow.ClientSize.Width - 102);
                _variantParentBox.Width = available;
                if (_manageFamilyButton != null)
                    _manageFamilyButton.Left = Math.Max(184, variantRow.ClientSize.Width - 94);
                _variantHelpLabel.Width = Math.Max(180, variantRow.ClientSize.Width - 4);
            };

            _detailStateLabel = new Label();
            _detailStateLabel.Dock = DockStyle.Fill;
            _detailStateLabel.Margin = new Padding(0, 2, 0, 0);
            _detailStateLabel.ForeColor = Color.FromArgb(185, 125, 28);
            _detailStateLabel.Font = TwoPointTheme.BoldFont(8.2F);
            _detailStateLabel.TextAlign = ContentAlignment.TopLeft;
            _detailStateLabel.AutoEllipsis = true;
            layout.Controls.Add(_detailStateLabel, 0, 8);

            _detailsScrollBar.ValueChanged += delegate
            {
                if (_detailsContent != null)
                    _detailsContent.Top = -_detailsScrollBar.Value;
            };
            _detailsViewport.Resize += delegate { UpdateDetailsScrollBar(); };
            _detailsViewport.MouseWheel += delegate(object sender, MouseEventArgs e)
            {
                if (_detailsScrollBar == null || !_detailsScrollBar.Visible)
                    return;
                int step = Math.Max(38, _detailsViewport.ClientSize.Height / 8);
                int next = _detailsScrollBar.Value + (e.Delta < 0 ? step : -step);
                SetDetailsScrollValue(next);
            };
            _detailsContent.MouseWheel += delegate(object sender, MouseEventArgs e)
            {
                if (_detailsScrollBar == null || !_detailsScrollBar.Visible)
                    return;
                int step = Math.Max(38, _detailsViewport.ClientSize.Height / 8);
                int next = _detailsScrollBar.Value + (e.Delta < 0 ? step : -step);
                SetDetailsScrollValue(next);
            };

            UpdateDetailsScrollBar();
        }

        private void SetDetailsScrollValue(int value)
        {
            if (_detailsScrollBar == null || _detailsContent == null)
                return;
            _detailsScrollBar.Value = value;
            _detailsContent.Top = -_detailsScrollBar.Value;
        }

        private void UpdateDetailsScrollBar()
        {
            if (_detailsViewport == null || _detailsContent == null || _detailsScrollBar == null)
                return;

            if (!_detailsContent.Visible)
            {
                _detailsScrollBar.Visible = false;
                return;
            }

            _detailsContent.Width = Math.Max(1, _detailsViewport.ClientSize.Width);
            int maximum = Math.Max(0, _detailsContent.Height - _detailsViewport.ClientSize.Height);
            _detailsScrollBar.Maximum = maximum;
            _detailsScrollBar.LargeChange = Math.Max(1, _detailsViewport.ClientSize.Height);
            _detailsScrollBar.Visible = maximum > 0;
            SetDetailsScrollValue(Math.Min(_detailsScrollBar.Value, maximum));
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

        private void BuildMoneyControl(TableLayoutPanel parent, int row, string labelText, bool cash)
        {
            Panel holder = new Panel();
            holder.Dock = DockStyle.Fill;
            holder.Margin = new Padding(0);
            holder.BackColor = TwoPointTheme.PanelLight;
            parent.Controls.Add(holder, 0, row);

            Label label = NewFieldLabel(labelText);
            label.Dock = DockStyle.None;
            label.Location = new Point(7, 1);
            label.Size = new Size(120, 18);
            holder.Controls.Add(label);

            // Match the Create Mod money controls: 32px icon, 30px slider and
            // 100px amount box. Keep the group compact instead of stretching it
            // across the full width of the wider My Mods details panel.
            PictureBox icon = new PictureBox();
            icon.Size = new Size(32, 32);
            icon.Location = new Point(7, 24);
            icon.SizeMode = PictureBoxSizeMode.Zoom;
            icon.BackColor = Color.Transparent;
            icon.Image = TwoPointTheme.LoadThemeImage(cash ? "cash.png" : "kudosh.png");
            holder.Controls.Add(icon);

            TwoPointSlider slider = new TwoPointSlider();
            slider.Location = new Point(45, 24);
            slider.Size = new Size(315, 30);
            slider.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            slider.Minimum = 0;
            slider.Maximum = cash ? 5000 : 500;
            holder.Controls.Add(slider);

            NumericUpDown numeric = new NumericUpDown();
            numeric.Location = new Point(372, 25);
            numeric.Size = new Size(100, 28);
            numeric.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            numeric.Minimum = 0;
            numeric.Maximum = 1000000;
            numeric.DecimalPlaces = 0;
            numeric.BackColor = TwoPointTheme.FieldBackground;
            numeric.ForeColor = TwoPointTheme.BodyText;
            numeric.Font = TwoPointTheme.BodyFont(9.25F);
            numeric.BorderStyle = BorderStyle.Fixed3D;
            holder.Controls.Add(numeric);

            if (cash)
            {
                _costSlider = slider;
                _costValue = numeric;
                _costHolder = holder;
            }
            else
            {
                _kudoshSlider = slider;
                _kudoshValue = numeric;
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
                MarkDetailsDirty();
            };
        }

        private TextBox BuildLabeledTextBox(TableLayoutPanel parent, int row, string labelText, bool multiline)
        {
            Panel holder = new Panel();
            holder.Dock = DockStyle.Fill;
            holder.Margin = new Padding(0, 0, 0, 2);
            parent.Controls.Add(holder, 0, row);

            Label label = NewFieldLabel(labelText);
            label.Dock = DockStyle.None;
            label.Location = new Point(0, 0);
            label.Size = new Size(180, 19);
            holder.Controls.Add(label);

            TextBox box = new TextBox();
            box.Location = new Point(0, 20);
            box.Size = new Size(Math.Max(80, holder.ClientSize.Width), Math.Max(24, holder.ClientSize.Height - 21));
            box.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            box.Multiline = multiline;
            box.ScrollBars = ScrollBars.None;
            box.BackColor = TwoPointTheme.FieldBackground;
            box.ForeColor = TwoPointTheme.BodyText;
            box.Font = TwoPointTheme.BodyFont(9.25F);
            holder.Controls.Add(box);
            return box;
        }

        private TextBox BuildReadOnlyField(TableLayoutPanel parent, int column, string labelText)
        {
            Panel holder = new Panel();
            holder.Dock = DockStyle.Fill;
            holder.Margin = new Padding(column == 0 ? 0 : 7, 0, column == 2 ? 0 : 7, 0);
            holder.BackColor = TwoPointTheme.PanelLight;
            parent.Controls.Add(holder, column, 0);

            Label label = NewFieldLabel(labelText);
            label.Dock = DockStyle.None;
            label.Location = new Point(0, 0);
            label.Size = new Size(160, 18);
            holder.Controls.Add(label);

            TextBox box = new TextBox();
            box.Location = new Point(0, 20);
            box.Size = new Size(Math.Max(60, holder.ClientSize.Width), 25);
            box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            box.ReadOnly = true;
            box.TabStop = false;
            box.BackColor = TwoPointTheme.PanelLight;
            box.ForeColor = Color.FromArgb(95, 86, 72);
            box.BorderStyle = BorderStyle.None;
            box.Font = TwoPointTheme.BodyFont(8.8F);
            holder.Controls.Add(box);
            return box;
        }

        private Panel BuildReadOnlyOptionsField(TableLayoutPanel parent, int column, string labelText)
        {
            Panel holder = new Panel();
            holder.Dock = DockStyle.Fill;
            holder.Margin = new Padding(column == 0 ? 0 : 7, 0, 0, 0);
            holder.BackColor = TwoPointTheme.PanelLight;
            parent.Controls.Add(holder, column, 0);

            Label label = NewFieldLabel(labelText);
            label.Dock = DockStyle.None;
            label.Location = new Point(0, 0);
            label.AutoSize = true;
            label.MaximumSize = new Size(Math.Max(60, holder.ClientSize.Width), 14);
            // Keep the existing 50 px identity row unchanged, but anchor option artwork to
            // the actual rendered header width. This prevents different option icons from
            // appearing to jump left/right as their own aspect ratio changes.
            holder.Controls.Add(label);

            Panel field = new Panel();
            field.Location = new Point(0, 14);
            field.Size = new Size(Math.Max(60, holder.ClientSize.Width), 36);
            field.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            field.BackColor = TwoPointTheme.PanelLight;
            field.BorderStyle = BorderStyle.None;
            field.TabStop = false;
            field.Paint += delegate(object sender, PaintEventArgs e)
            {
                int headerCentreX = label.Left + Math.Max(1, label.PreferredWidth) / 2;
                headerCentreX = Math.Max(24, Math.Min(Math.Max(24, field.Width - 24), headerCentreX));

                if (!string.IsNullOrEmpty(_detailShape))
                {
                    // Match the My Mods table more closely while preserving the complete
                    // source icon canvas (including intentional transparent/blank margins).
                    // The icon is centred beneath the Item Options text, not inside the
                    // changing width of the whole identity column.
                    DrawShapeIcon(e.Graphics,
                        new Rectangle(headerCentreX - 22, 0, 44, field.Height), _detailShape);
                    return;
                }

                if (!string.IsNullOrEmpty(_detailOptionIconFile))
                {
                    using (Image icon = TwoPointTheme.LoadThemeImage(_detailOptionIconFile))
                    {
                        if (icon != null)
                        {
                            Rectangle iconSlot = new Rectangle(headerCentreX - 26, 0, 52, field.Height);
                            Rectangle target = FitImageBounds(icon, iconSlot, 48, 36);
                            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            e.Graphics.DrawImage(icon, target);
                        }
                    }
                }
            };
            holder.Controls.Add(field);
            return field;
        }

        private void InitialiseUiToolTips()
        {
            _uiToolTip = new ToolTip();
            _uiToolTip.AutoPopDelay = 8000;
            _uiToolTip.InitialDelay = 550;
            _uiToolTip.ReshowDelay = 120;
            _uiToolTip.ShowAlways = true;

            SetUiTip(_searchBox, "Search saved Memento Maker mods by name or Mod ID.");
            SetUiTip(_typeFilter, "Show only mods of the selected item type.");
            SetUiTip(_displayFilter, "Filter by mod structure: show all mods, only grouped Variant Families / Décor Packs, or only stand-alone mods.");
            SetUiTip(_statusFilter, "Filter by either Installed status or Steam Workshop publication status.");
            SetUiTip(_familyExpandCollapseButton, "Expand all visible Variant Families / Décor Packs when collapsed; collapse all grouped mods when any are expanded.");

            SetUiTip(_changeArtworkButton, "Choose replacement artwork for this saved mod. Positioning is unchanged.");
            SetUiTip(_nameBox, "The item name shown in-game.");
            SetUiTip(_descriptionBox, "The description shown for the item in-game.");
            SetUiTip(_recolourableToggle, "Switch between recolourable and non-recolourable. Recolourable works best for Greyscale textures.");
            SetUiTip(_variantParentBox, "Choose whether this item is standalone, belongs to a Décor Pack, is a variant of its base-game item, or is a child of another built Memento Maker mod.");
            SetUiTip(_manageFamilyButton, "Add or remove several mods from this variant family at once.");
            SetUiTip(_costValue, "Set the purchase cost.");
            SetUiTip(_kudoshValue, "Set the in-game Kudosh unlock cost.");
            SetUiTip(_rebuildButton, "Rebuild and reinstall the selected mod(s).");
            SetUiTip(_editButton, "Open the selected mod in Create Mod for editing.");
            SetUiTip(_openButton, "Open the installed build folder for the selected mod.");
            SetUiTip(_workshopButton, "Publish to or update Steam Workshop.");

            SetUiTip(_saveButton, "Save edits made in My Mods.");
            SetUiTip(_deleteButton, "Delete this saved Memento Maker project.");
        }

        private void SetUiTip(Control control, string text)
        {
            if (_uiToolTip != null && control != null && !string.IsNullOrWhiteSpace(text))
                _uiToolTip.SetToolTip(control, text);
        }

        private void BuildActions(Panel body)
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(6, 6, 6, 5);
            layout.ColumnCount = 3;
            layout.RowCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            body.Controls.Add(layout);

            FlowLayoutPanel leftActions = new FlowLayoutPanel();
            leftActions.AutoSize = true;
            leftActions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            leftActions.WrapContents = false;
            leftActions.FlowDirection = FlowDirection.LeftToRight;
            leftActions.Margin = new Padding(0);
            leftActions.BackColor = Color.Transparent;
            layout.Controls.Add(leftActions, 0, 0);

            FlowLayoutPanel rightActions = new FlowLayoutPanel();
            rightActions.AutoSize = true;
            rightActions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            rightActions.WrapContents = false;
            rightActions.FlowDirection = FlowDirection.LeftToRight;
            rightActions.Margin = new Padding(0);
            rightActions.BackColor = Color.Transparent;
            layout.Controls.Add(rightActions, 2, 0);

            _batchProgressHost = new Panel();
            _batchProgressHost.Dock = DockStyle.Fill;
            _batchProgressHost.Margin = new Padding(12, 0, 12, 0);
            _batchProgressHost.BackColor = Color.Transparent;
            _batchProgressHost.Visible = false;
            layout.Controls.Add(_batchProgressHost, 1, 0);

            _batchProgressLabel = new Label();
            _batchProgressLabel.Dock = DockStyle.Top;
            _batchProgressLabel.Height = 20;
            _batchProgressLabel.ForeColor = TwoPointTheme.PrimaryText;
            _batchProgressLabel.Font = TwoPointTheme.BoldFont(8.2F);
            _batchProgressLabel.TextAlign = ContentAlignment.MiddleLeft;
            _batchProgressHost.Controls.Add(_batchProgressLabel);

            _batchProgressBar = new ProgressBar();
            _batchProgressBar.Dock = DockStyle.Top;
            _batchProgressBar.Height = 12;
            _batchProgressBar.Top = 23;
            _batchProgressBar.Style = ProgressBarStyle.Marquee;
            _batchProgressBar.MarqueeAnimationSpeed = 0;
            _batchProgressHost.Controls.Add(_batchProgressBar);
            _batchProgressBar.BringToFront();

            _batchProgressHideTimer = new Timer();
            _batchProgressHideTimer.Interval = 4500;
            _batchProgressHideTimer.Tick += delegate
            {
                _batchProgressHideTimer.Stop();
                if (!_batchBusy && _batchProgressHost != null)
                    _batchProgressHost.Visible = false;
            };

            _editButton = NewActionButton("Edit Mod");
            _rebuildButton = NewActionButton("Rebuild Mod");
            _openButton = NewActionButton("Open Installed");
            _workshopButton = NewActionButton("Publish to Workshop");
            _workshopButton.Size = TwoPointTheme.BottomActionButtonSize;
            _workshopToolsButton = NewActionButton("...");
            _workshopToolsButton.Size = new Size(44, 40);
            _workshopToolsButton.Margin = new Padding(0, 0, 8, 0);
            _workshopToolsMenu = BuildWorkshopToolsMenu();
            _myModsContextMenu = BuildMyModsContextMenu();

            _saveButton = NewActionButton("Save Changes");
            _deleteButton = NewActionButton("Delete Mod");

            // Rebuild is the primary action; Edit remains a secondary action.
            // Their positions are swapped at the same time so the green primary action appears first.
            TwoPointTheme.StyleButton(_editButton);
            TwoPointTheme.StylePrimaryButton(_rebuildButton);
            TwoPointTheme.StyleButton(_openButton);
            TwoPointTheme.StyleButton(_workshopButton);
            FitWorkshopActionButtonText();
            TwoPointTheme.StyleButton(_workshopToolsButton);
            TwoPointTheme.StylePrimaryButton(_saveButton);
            TwoPointTheme.StyleDangerButton(_deleteButton);

            TwoPointTheme.ApplyBottomActionFont(_rebuildButton);
            TwoPointTheme.ApplyBottomActionFont(_editButton);
            TwoPointTheme.ApplyBottomActionFont(_openButton);
            TwoPointTheme.ApplyBottomActionFont(_workshopButton);
            TwoPointTheme.ApplyBottomActionFont(_saveButton);
            TwoPointTheme.ApplyBottomActionFont(_deleteButton);

            leftActions.Controls.Add(_rebuildButton);
            leftActions.Controls.Add(_editButton);
            leftActions.Controls.Add(_openButton);
            leftActions.Controls.Add(_workshopButton);
            leftActions.Controls.Add(_workshopToolsButton);
            rightActions.Controls.Add(_saveButton);
            rightActions.Controls.Add(_deleteButton);
        }

        private ContextMenuStrip BuildWorkshopToolsMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.BackColor = TwoPointTheme.PanelLight;
            menu.ForeColor = TwoPointTheme.BodyText;
            menu.Font = TwoPointTheme.BodyFont(9F);
            menu.ShowImageMargin = false;
            menu.Padding = new Padding(3);

            _openWorkshopPageMenuItem = new ToolStripMenuItem("Open Workshop Page");
            _refreshWorkshopStatusMenuItem = new ToolStripMenuItem("Refresh Workshop Status");
            _relinkWorkshopMenuItem = new ToolStripMenuItem("Relink Workshop Item...");
            _unlinkWorkshopMenuItem = new ToolStripMenuItem("Unlink Workshop Item");

            _openWorkshopPageMenuItem.Click += delegate { RequestSelectionAction("WorkshopOpen"); };
            _refreshWorkshopStatusMenuItem.Click += delegate { RequestSelectionAction("WorkshopRefresh"); };
            _relinkWorkshopMenuItem.Click += delegate { RequestSelectionAction("WorkshopRelink"); };
            _unlinkWorkshopMenuItem.Click += delegate { RequestSelectionAction("WorkshopUnlink"); };

            menu.Items.Add(_openWorkshopPageMenuItem);
            menu.Items.Add(_refreshWorkshopStatusMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_relinkWorkshopMenuItem);
            menu.Items.Add(_unlinkWorkshopMenuItem);
            TwoPointTheme.StyleContextMenu(menu);
            return menu;
        }

        private ContextMenuStrip BuildMyModsContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.BackColor = TwoPointTheme.PanelLight;
            menu.ForeColor = TwoPointTheme.BodyText;
            menu.Font = TwoPointTheme.BodyFont(9F);
            menu.ShowImageMargin = false;
            menu.Padding = new Padding(3);

            _contextRebuildMenuItem = new ToolStripMenuItem("Rebuild Mod");
            _contextEditMenuItem = new ToolStripMenuItem("Edit Mod");
            _contextOpenInstalledMenuItem = new ToolStripMenuItem("Open Installed");
            _contextWorkshopMenuItem = new ToolStripMenuItem("Publish to Workshop");
            _contextOpenWorkshopPageMenuItem = new ToolStripMenuItem("Open Workshop Page");
            _contextRefreshWorkshopStatusMenuItem = new ToolStripMenuItem("Refresh Workshop Status");
            _contextRelinkWorkshopMenuItem = new ToolStripMenuItem("Relink Workshop Item...");
            _contextUnlinkWorkshopMenuItem = new ToolStripMenuItem("Unlink Workshop Item");

            _contextRebuildMenuItem.Click += delegate { QueueRebuild(); };
            _contextEditMenuItem.Click += delegate { Choose("Edit"); };
            _contextOpenInstalledMenuItem.Click += delegate { OpenInstalled(); };
            _contextWorkshopMenuItem.Click += delegate { Choose("Workshop"); };
            _contextOpenWorkshopPageMenuItem.Click += delegate { RequestSelectionAction("WorkshopOpen"); };
            _contextRefreshWorkshopStatusMenuItem.Click += delegate { RequestSelectionAction("WorkshopRefresh"); };
            _contextRelinkWorkshopMenuItem.Click += delegate { RequestSelectionAction("WorkshopRelink"); };
            _contextUnlinkWorkshopMenuItem.Click += delegate { RequestSelectionAction("WorkshopUnlink"); };

            menu.Items.Add(_contextRebuildMenuItem);
            menu.Items.Add(_contextEditMenuItem);
            menu.Items.Add(_contextOpenInstalledMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_contextWorkshopMenuItem);
            menu.Items.Add(_contextOpenWorkshopPageMenuItem);
            menu.Items.Add(_contextRefreshWorkshopStatusMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_contextRelinkWorkshopMenuItem);
            menu.Items.Add(_contextUnlinkWorkshopMenuItem);
            TwoPointTheme.StyleContextMenu(menu);
            return menu;
        }

        private void UpdateMyModsContextMenu()
        {
            if (_myModsContextMenu == null)
                return;

            // UpdateButtons already contains the full family/pack/install/Workshop-state logic.
            // Mirror those results rather than maintaining a second set of rules for right-click.
            UpdateButtons();

            if (_contextRebuildMenuItem != null)
            {
                _contextRebuildMenuItem.Text = _rebuildButton == null ? "Rebuild Mod" : _rebuildButton.Text;
                _contextRebuildMenuItem.Enabled = _rebuildButton != null && _rebuildButton.Enabled;
            }
            if (_contextEditMenuItem != null)
                _contextEditMenuItem.Enabled = _editButton != null && _editButton.Visible && _editButton.Enabled;
            if (_contextOpenInstalledMenuItem != null)
                _contextOpenInstalledMenuItem.Enabled = _openButton != null && _openButton.Visible && _openButton.Enabled;
            if (_contextWorkshopMenuItem != null)
            {
                _contextWorkshopMenuItem.Text = _workshopButton == null ? "Publish to Workshop" : _workshopButton.Text;
                _contextWorkshopMenuItem.Enabled = _workshopButton != null && _workshopButton.Visible && _workshopButton.Enabled;
            }
            if (_contextOpenWorkshopPageMenuItem != null)
                _contextOpenWorkshopPageMenuItem.Enabled = _openWorkshopPageMenuItem != null && _openWorkshopPageMenuItem.Enabled;
            if (_contextRefreshWorkshopStatusMenuItem != null)
                _contextRefreshWorkshopStatusMenuItem.Enabled = _refreshWorkshopStatusMenuItem != null && _refreshWorkshopStatusMenuItem.Enabled;
            if (_contextRelinkWorkshopMenuItem != null)
                _contextRelinkWorkshopMenuItem.Enabled = _relinkWorkshopMenuItem != null && _relinkWorkshopMenuItem.Enabled;
            if (_contextUnlinkWorkshopMenuItem != null)
                _contextUnlinkWorkshopMenuItem.Enabled = _unlinkWorkshopMenuItem != null && _unlinkWorkshopMenuItem.Enabled;
        }

        private void ShowMyModsContextMenu(MouseEventArgs e)
        {
            if (_grid == null || e == null || e.Button != MouseButtons.Right)
                return;

            DataGridView.HitTestInfo hit = _grid.HitTest(e.X, e.Y);
            if (hit == null || hit.RowIndex < 0 || hit.RowIndex >= _grid.Rows.Count)
                return;

            DataGridViewRow row = _grid.Rows[hit.RowIndex];

            // Preserve an intentional multi-selection when right-clicking inside it. Otherwise,
            // make the clicked row the active selection before deriving the available actions.
            if (!row.Selected)
            {
                _suppressSelectionEvents = true;
                try
                {
                    _grid.ClearSelection();
                    row.Selected = true;
                    int columnIndex = hit.ColumnIndex >= 0 ? hit.ColumnIndex : 0;
                    if (columnIndex >= 0 && columnIndex < row.Cells.Count)
                        _grid.CurrentCell = row.Cells[columnIndex];
                }
                finally
                {
                    _suppressSelectionEvents = false;
                }
                HandleSelectionChanged();
            }
            else if (hit.ColumnIndex >= 0 && hit.ColumnIndex < row.Cells.Count)
            {
                // Keep the current multi-select intact, but ensure keyboard focus follows the row
                // the user actually right-clicked.
                _suppressSelectionEvents = true;
                try { _grid.CurrentCell = row.Cells[hit.ColumnIndex]; }
                finally { _suppressSelectionEvents = false; }
            }

            if (_myModsContextMenu == null)
                _myModsContextMenu = BuildMyModsContextMenu();
            UpdateMyModsContextMenu();
            _myModsContextMenu.Show(_grid, e.Location);
        }

        private Button NewActionButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.Size = TwoPointTheme.BottomActionButtonSize;
            button.Margin = new Padding(0, 0, 8, 0);
            button.Font = TwoPointTheme.BoldFont(TwoPointTheme.BottomActionFontSize);
            return button;
        }

        private void FitWorkshopActionButtonText()
        {
            // All standard bottom actions deliberately use the same font size.
            // 9pt Trebuchet MS Bold fits the longest current caption (Publish to Workshop)
            // inside the shared 150 x 40 button without making this one action look different.
            TwoPointTheme.ApplyBottomActionFont(_workshopButton);
        }

        private void WireEvents()
        {
            _searchBox.TextChanged += delegate { ApplyView(); };
            _typeFilter.SelectedIndexChanged += delegate { ApplyView(); };
            _displayFilter.SelectedIndexChanged += delegate { ApplyView(); };
            _statusFilter.SelectedIndexChanged += delegate { ApplyView(); };
            _sortBy.SelectedIndexChanged += delegate { ApplyView(); };
            _sortDirectionButton.Click += delegate
            {
                _sortDescending = !_sortDescending;
                TwoPointSortDirectionButton sortDirection = _sortDirectionButton as TwoPointSortDirectionButton;
                if (sortDirection != null)
                    sortDirection.Descending = _sortDescending;
                ApplyView();
            };
            _familyExpandCollapseButton.Click += delegate { SetAllFamiliesCollapsed(AnyVisibleFamilyExpanded()); };

            _grid.SelectionChanged += delegate { QueueSelectionSync(); };
            _grid.CurrentCellChanged += delegate { QueueSelectionSync(); };
            _grid.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Right)
                    ShowMyModsContextMenu(e);
            };
            _grid.CellClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0)
                    return;

                DataGridViewRow row = _grid.Rows[e.RowIndex];
                VariantFamilyGroup family = GetFamilyForRow(row);
                string columnName = e.ColumnIndex >= 0 ? _grid.Columns[e.ColumnIndex].Name : "";
                if (family != null && IsFamilyParentRow(row, family) &&
                    string.Equals(columnName, "ModName", StringComparison.Ordinal))
                {
                    ToggleFamily(family);
                    return;
                }

                // Synthetic base-game family headers are display/toggle rows only. Keep the
                // row selectable so the right pane can show a dedicated family summary, but
                // never allow it to flow into edit/rebuild/delete actions as if it were a mod.
                if (row.Tag is VariantFamilyHeaderTag)
                {
                    QueueSelectionSync();
                    return;
                }

                QueueSelectionSync();
            };
            _grid.RowEnter += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0)
                    QueueSelectionSync();
            };
            _grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0)
                    return;
                DataGridViewRow row = _grid.Rows[e.RowIndex];
                VariantFamilyGroup family = GetFamilyForRow(row);
                if (family != null && IsFamilyParentRow(row, family))
                {
                    ToggleFamily(family);
                    return;
                }
                if (GetSingleSelectedRecord() != null)
                    Choose("Edit");
            };
            _grid.CellFormatting += GridCellFormatting;
            _grid.CellPainting += GridCellPainting;
            _grid.Scroll += delegate { SyncGridScrollBar(); };
            _grid.MouseWheel += delegate(object sender, MouseEventArgs e)
            {
                int current = _grid.FirstDisplayedScrollingRowIndex < 0 ? 0 : _grid.FirstDisplayedScrollingRowIndex;
                int currentVisible = GetVisiblePositionForGridRow(current);
                int visibleCount = GetVisibleGridRowCount();
                int targetVisible = currentVisible + (e.Delta > 0 ? -1 : 1);
                targetVisible = Math.Max(0, Math.Min(Math.Max(0, visibleCount - 1), targetVisible));
                int target = GetGridRowIndexForVisiblePosition(targetVisible);
                if (target >= 0)
                {
                    try { _grid.FirstDisplayedScrollingRowIndex = target; } catch { }
                }
                SyncGridScrollBar();
            };
            _grid.Resize += delegate { BeginInvoke((Action)delegate { SyncGridScrollBar(); }); };
            _gridScrollBar.ValueChanged += delegate
            {
                if (_grid.Rows.Count == 0)
                    return;
                int target = GetGridRowIndexForVisiblePosition(_gridScrollBar.Value);
                if (target < 0)
                    return;
                try { _grid.FirstDisplayedScrollingRowIndex = target; } catch { }
            };

            _nameBox.TextChanged += delegate { MarkDetailsDirty(); };
            _descriptionBox.TextChanged += delegate { MarkDetailsDirty(); };
            _recolourableToggle.CheckedChanged += delegate { MarkDetailsDirty(); };
            _variantParentBox.SelectedIndexChanged += delegate
            {
                if (_loadingDetails)
                    return;
                if (IsWallpaperRecord(_selectedRecord))
                    ApplyWallpaperDecorPackRules();
                else
                    ApplyVariantRulesToDetails();
                MarkDetailsDirty();
            };
            _changeArtworkButton.Click += delegate { ChangeArtwork(); };

            _editButton.Click += delegate { Choose("Edit"); };
            _rebuildButton.Click += delegate { QueueRebuild(); };
            _openButton.Click += delegate { OpenInstalled(); };
            _workshopButton.Click += delegate { Choose("Workshop"); };
            _workshopToolsButton.Click += delegate
            {
                UpdateButtons();
                if (_workshopToolsMenu != null && _workshopToolsButton.Visible)
                    _workshopToolsMenu.Show(_workshopToolsButton, new Point(0, _workshopToolsButton.Height));
            };
            _saveButton.Click += delegate { SaveChanges(); };
            _deleteButton.Click += delegate { DeleteSelected(); };
        }

        public async Task InitialiseProjectsAsync(string selectModId)
        {
            if (_initialProjectsLoaded || IsDisposed)
                return;

            // First opening My Mods used to synchronously read every project and render every
            // preview before the tab could paint. Load project metadata off the UI thread,
            // render the rows immediately, then fill preview cells progressively in the
            // background. The tab therefore becomes interactive almost immediately.
            List<ModProjectRecord> loaded = await Task.Run(delegate { return _library.LoadAll(); });
            if (IsDisposed)
                return;

            _projects = loaded ?? new List<ModProjectRecord>();
            _familyIndexDirty = true;
            _skipGridPreviewImages = true;
            ApplyView();
            _skipGridPreviewImages = false;
            _initialProjectsLoaded = true;
            ShowProjectLoadWarnings();

            if (!string.IsNullOrEmpty(selectModId))
                SelectRecordById(selectModId);

            int generation = ++_previewLoadGeneration;
            await LoadGridPreviewsProgressivelyAsync(generation);
        }

        public void RefreshProjects(string selectModId)
        {
            _previewLoadGeneration++;
            LoadProjects(selectModId);
            _initialProjectsLoaded = true;
        }

        public void EnsureCurrentDetails()
        {
            if (IsDisposed || _grid == null)
                return;

            ModProjectRecord record = GetSingleSelectedRecord();
            if (record != null)
            {
                LoadDetails(record);
                UpdateButtons();
                return;
            }

            if (_grid.Rows.Count > 0)
            {
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    ModProjectRecord first = row.Tag as ModProjectRecord;
                    if (first != null)
                    {
                        SelectRecordById(first.ModId);
                        return;
                    }
                }
            }

            _selectedRecord = null;
            ClearDetails("Select a mod to view its details.");
            UpdateButtons();
        }

        public void SetBatchBuildProgress(bool busy, string message)
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
            {
                BeginInvoke((Action<bool, string>)SetBatchBuildProgress, busy, message);
                return;
            }

            _batchBusy = busy;
            if (_batchProgressHideTimer != null)
                _batchProgressHideTimer.Stop();

            if (_batchProgressHost != null)
                _batchProgressHost.Visible = busy || !string.IsNullOrEmpty(message);
            if (_batchProgressLabel != null)
                _batchProgressLabel.Text = string.IsNullOrEmpty(message) ? (busy ? "Building..." : "") : message;
            if (_batchProgressBar != null)
                _batchProgressBar.MarqueeAnimationSpeed = busy ? 28 : 0;

            UpdateButtons();

            if (!busy && _batchProgressHost != null && _batchProgressHost.Visible && _batchProgressHideTimer != null)
                _batchProgressHideTimer.Start();
        }

        private void LoadProjects(string selectModId)
        {
            _previewLoadGeneration++;
            ClearGridPreviewCache();
            _projects = _library.LoadAll();
            _familyIndexDirty = true;
            ApplyView();
            ShowProjectLoadWarnings();
            if (!string.IsNullOrEmpty(selectModId))
                SelectRecordById(selectModId);
        }

        private void ShowProjectLoadWarnings()
        {
            List<string> warnings = _library.ConsumeLoadWarnings();
            if (warnings == null || warnings.Count == 0 || IsDisposed)
                return;

            StringBuilder message = new StringBuilder();
            message.AppendLine(warnings.Count == 1
                ? "Memento Maker found a problem while loading one saved project."
                : "Memento Maker found problems while loading " + warnings.Count + " saved projects.");
            message.AppendLine();
            for (int i = 0; i < warnings.Count && i < 6; i++)
                message.AppendLine("• " + warnings[i]);
            if (warnings.Count > 6)
                message.AppendLine("• ...and " + (warnings.Count - 6) + " more.");
            message.AppendLine();
            message.AppendLine("No project folders were deleted. Recovery details were written to:");
            message.Append(Path.Combine(AppInfo.LogsFolder, "reliability.log"));

            TwoPointTheme.ShowMessage(this, message.ToString(), "Project Recovery",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void ApplyView()
        {
            if (_projects == null)
                return;

            // A filter/sort change should only rebuild lightweight row metadata. Expensive
            // preview rendering is cached and any cache misses are filled progressively after
            // the new rows have already painted. Status calculations are also cached for this
            // view pass so family Workshop registry files are not read repeatedly per child.
            int viewGeneration = ++_previewLoadGeneration;
            _viewInstalledStatusCache.Clear();
            _viewWorkshopStatusCache.Clear();
            _viewWorkshopFamilyStateCache.Clear();

            string search = (_searchBox.Text ?? "").Trim();
            string typeFilter = _typeFilter.SelectedItem as string ?? "All Types";
            string displayFilter = _displayFilter == null ? "All Mods" : (_displayFilter.SelectedItem as string ?? "All Mods");
            string statusFilter = _statusFilter == null ? "All" : (_statusFilter.SelectedItem as string ?? "All");

            // Family membership only changes when project data is reloaded/saved, not when
            // the user changes a view filter. Avoid rebuilding the family graph on every
            // combo-box selection.
            if (_familyIndexDirty)
            {
                BuildVariantFamilyIndex(_projects);
                ApplyDefaultCollapsedFamilyState();
                _familyIndexDirty = false;
            }

            List<ModProjectRecord> filtered = new List<ModProjectRecord>();
            foreach (ModProjectRecord record in _projects)
            {
                if (record == null)
                    continue;

                string itemType = GetItemType(record);
                string installed = GetViewInstalledStatus(record);
                bool belongsToFamily = !string.IsNullOrEmpty(record.ModId) && _familyByMemberId.ContainsKey(record.ModId);

                if ((string.Equals(displayFilter, "Families", StringComparison.OrdinalIgnoreCase) || string.Equals(displayFilter, "Families / Packs", StringComparison.OrdinalIgnoreCase)) && !belongsToFamily)
                    continue;
                if (string.Equals(displayFilter, "Stand Alone", StringComparison.OrdinalIgnoreCase) && belongsToFamily)
                    continue;

                if (!string.Equals(typeFilter, "All Types", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(typeFilter, itemType, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.Equals(statusFilter, "All", StringComparison.OrdinalIgnoreCase))
                {
                    string workshop = GetViewWorkshopStatus(record);
                    bool matchesInstalledStatus = string.Equals(statusFilter, installed, StringComparison.OrdinalIgnoreCase);
                    bool matchesWorkshopStatus = string.Equals(statusFilter, workshop, StringComparison.OrdinalIgnoreCase);
                    if (!matchesInstalledStatus && !matchesWorkshopStatus)
                        continue;
                }

                if (!string.IsNullOrEmpty(search))
                {
                    string haystack = (record.Name ?? "") + " " + (record.Description ?? "") + " " +
                        (record.ModId ?? "") + " " + itemType + " " + (record.Template ?? "");
                    if (haystack.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }

                filtered.Add(record);
            }

            filtered.Sort(CompareRecords);
            if (_sortDescending)
                filtered.Reverse();

            HashSet<string> emittedFamilies = new HashSet<string>(StringComparer.Ordinal);

            string selectedId = _selectedRecord == null ? null : _selectedRecord.ModId;
            DisposeGridImages();
            _suppressSelectionEvents = true;
            _grid.SuspendLayout();
            try
            {
                _grid.Rows.Clear();
                foreach (ModProjectRecord record in filtered)
                {
                    if (record == null)
                        continue;

                    VariantFamilyGroup family;
                    if (_familyByMemberId.TryGetValue(record.ModId ?? "", out family) && family != null)
                    {
                        if (!emittedFamilies.Add(family.Key))
                            continue;
                        AddFamilyRows(family, filtered);
                    }
                    else
                    {
                        AddGridRow(record);
                    }
                }
            }
            finally
            {
                _grid.ResumeLayout(false);
                _suppressSelectionEvents = false;
            }

            int visibleFamilyCount = 0;
            int visibleDecorPackCount = 0;
            HashSet<string> countedGroups = new HashSet<string>(StringComparer.Ordinal);
            foreach (VariantFamilyGroup visibleGroup in _familyByMemberId.Values)
            {
                if (visibleGroup == null || string.IsNullOrEmpty(visibleGroup.Key) ||
                    !emittedFamilies.Contains(visibleGroup.Key) || !countedGroups.Add(visibleGroup.Key))
                    continue;
                if (visibleGroup.DecorPack) visibleDecorPackCount++;
                else visibleFamilyCount++;
            }

            string countText = filtered.Count == 1 ? "Showing 1 mod" : "Showing " + filtered.Count + " mods";
            if (visibleFamilyCount > 0 || visibleDecorPackCount > 0)
            {
                List<string> groupedParts = new List<string>();
                if (visibleFamilyCount > 0)
                    groupedParts.Add(visibleFamilyCount + " variant famil" + (visibleFamilyCount == 1 ? "y" : "ies"));
                if (visibleDecorPackCount > 0)
                    groupedParts.Add(visibleDecorPackCount + " Décor Pack" + (visibleDecorPackCount == 1 ? "" : "s"));
                countText += " in " + string.Join(" and ", groupedParts.ToArray());
            }
            _countLabel.Text = countText;
            UpdateFamilyExpandCollapseButton();
            SyncGridScrollBar();

            if (!string.IsNullOrEmpty(selectedId))
                SelectRecordById(selectedId);
            else if (filtered.Count > 0)
            {
                SelectRecordById(filtered[0].ModId);
            }
            else
            {
                _selectedRecord = null;
                ClearDetails("No mods match the current filters.");
                UpdateButtons();
            }

            if (!_skipGridPreviewImages)
            {
                Task ignoredPreviewLoad = LoadGridPreviewsProgressivelyAsync(viewGeneration);
            }
        }

        private string GetViewInstalledStatus(ModProjectRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.ModId))
                return GetInstalledStatus(record);
            string value;
            if (_viewInstalledStatusCache.TryGetValue(record.ModId, out value))
                return value;
            value = GetInstalledStatus(record);
            _viewInstalledStatusCache[record.ModId] = value;
            return value;
        }

        private string GetViewWorkshopStatus(ModProjectRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.ModId))
                return GetWorkshopStatus(record);
            string value;
            if (_viewWorkshopStatusCache.TryGetValue(record.ModId, out value))
                return value;
            value = GetWorkshopStatus(record);
            _viewWorkshopStatusCache[record.ModId] = value;
            return value;
        }

        private WorkshopFamilyState GetViewWorkshopFamilyState(string familyKey)
        {
            if (string.IsNullOrEmpty(familyKey))
                return null;
            WorkshopFamilyState state;
            if (_viewWorkshopFamilyStateCache.TryGetValue(familyKey, out state))
                return state;
            state = _library.LoadWorkshopFamilyState(familyKey);
            _viewWorkshopFamilyStateCache[familyKey] = state;
            return state;
        }

        private void BuildVariantFamilyIndex(List<ModProjectRecord> records)
        {
            _familyByMemberId.Clear();
            if (records == null || records.Count == 0)
                return;

            Dictionary<string, ModProjectRecord> byId = new Dictionary<string, ModProjectRecord>(StringComparer.Ordinal);
            for (int i = 0; i < records.Count; i++)
            {
                ModProjectRecord record = records[i];
                if (record != null && !string.IsNullOrEmpty(record.ModId))
                    byId[record.ModId] = record;
            }

            Dictionary<string, VariantFamilyGroup> groups = new Dictionary<string, VariantFamilyGroup>(StringComparer.Ordinal);
            for (int i = 0; i < records.Count; i++)
            {
                ModProjectRecord child = records[i];
                if (child == null || string.IsNullOrEmpty(child.ModId))
                    continue;

                string mode = VariantModes.Normalise(child.VariantMode);
                VariantFamilyGroup family = null;
                if (mode == VariantModes.Modded && !string.IsNullOrEmpty(child.VariantParentModId))
                {
                    ModProjectRecord parent;
                    if (!byId.TryGetValue(child.VariantParentModId, out parent) || parent == null)
                        continue;

                    string key = "mod:" + parent.ModId;
                    if (!groups.TryGetValue(key, out family))
                    {
                        family = new VariantFamilyGroup
                        {
                            Key = key,
                            BaseGameParent = false,
                            ParentRecord = parent,
                            ParentName = string.IsNullOrEmpty(parent.Name) ? parent.ModId : parent.Name,
                            ItemType = GetItemType(parent)
                        };
                        groups[key] = family;
                    }
                }
                else if (mode == VariantModes.BaseGame)
                {
                    TemplateDefinition ownTemplate = FindTemplate(child.Template);
                    long targetArchetypeId = 0;
                    if (!string.IsNullOrEmpty(child.VariantParentBaseArchetypeId))
                        long.TryParse(child.VariantParentBaseArchetypeId, out targetArchetypeId);
                    if (targetArchetypeId == 0 && ownTemplate != null)
                        targetArchetypeId = ownTemplate.BaseArchetypeId;

                    TemplateDefinition parentTemplate = FindTemplateByBaseArchetypeId(targetArchetypeId) ?? ownTemplate;
                    string archetypeKey = targetArchetypeId != 0
                        ? targetArchetypeId.ToString()
                        : (child.Template ?? GetItemType(child));
                    string key = "base:" + archetypeKey;
                    if (!groups.TryGetValue(key, out family))
                    {
                        family = new VariantFamilyGroup
                        {
                            Key = key,
                            BaseGameParent = true,
                            ParentRecord = null,
                            ParentName = parentTemplate == null
                                ? GetItemType(child)
                                : (parentTemplate.BaseItemName ?? parentTemplate.DisplayName ?? parentTemplate.Key),
                            ItemType = parentTemplate == null ? GetItemType(child) : GetItemTypeForTemplate(parentTemplate)
                        };
                        groups[key] = family;
                    }
                }

                if (family != null)
                    family.Children.Add(child);
            }

            // BL-022: a Décor Pack is a shared package, not a variant relationship.
            // Present it with the same collapsed parent/child hierarchy so pack membership is clear.
            Dictionary<string, List<ModProjectRecord>> decorMembers = new Dictionary<string, List<ModProjectRecord>>(StringComparer.Ordinal);
            for (int i = 0; i < records.Count; i++)
            {
                ModProjectRecord record = records[i];
                if (record == null || string.IsNullOrEmpty(record.ModId) ||
                    BuildPackageModes.Normalise(record.LastBuildPackageMode) != BuildPackageModes.DecorPack ||
                    string.IsNullOrEmpty(record.LastBuiltFamilyKey))
                    continue;
                List<ModProjectRecord> list;
                if (!decorMembers.TryGetValue(record.LastBuiltFamilyKey, out list))
                {
                    list = new List<ModProjectRecord>();
                    decorMembers[record.LastBuiltFamilyKey] = list;
                }
                list.Add(record);
            }
            foreach (KeyValuePair<string, List<ModProjectRecord>> pair in decorMembers)
            {
                List<ModProjectRecord> list = pair.Value;
                if (list == null || list.Count < 1)
                    continue;
                list.Sort(delegate(ModProjectRecord a, ModProjectRecord b)
                {
                    return string.Compare(a == null ? "" : a.Name, b == null ? "" : b.Name, StringComparison.OrdinalIgnoreCase);
                });
                ModProjectRecord lead = list[0];
                VariantFamilyGroup pack = new VariantFamilyGroup
                {
                    Key = pair.Key,
                    BaseGameParent = false,
                    DecorPack = true,
                    ParentRecord = lead,
                    ParentName = string.IsNullOrWhiteSpace(lead.LastBuiltFamilyName) ? "Wallpaper Pack" : lead.LastBuiltFamilyName,
                    ItemType = "Décor"
                };
                for (int i = 1; i < list.Count; i++)
                    pack.Children.Add(list[i]);
                groups["pack:" + pair.Key] = pack;
            }

            foreach (VariantFamilyGroup family in groups.Values)
            {
                if (family == null || (!family.DecorPack && family.Children.Count == 0))
                    continue;

                family.Children.Sort(delegate(ModProjectRecord a, ModProjectRecord b)
                {
                    return string.Compare(a == null ? "" : a.Name, b == null ? "" : b.Name, StringComparison.OrdinalIgnoreCase);
                });

                if (family.ParentRecord != null && !string.IsNullOrEmpty(family.ParentRecord.ModId))
                    _familyByMemberId[family.ParentRecord.ModId] = family;
                for (int i = 0; i < family.Children.Count; i++)
                {
                    ModProjectRecord child = family.Children[i];
                    if (child != null && !string.IsNullOrEmpty(child.ModId))
                        _familyByMemberId[child.ModId] = family;
                }
            }
        }

        private void AddFamilyRows(VariantFamilyGroup family, List<ModProjectRecord> filtered)
        {
            if (family == null)
                return;

            HashSet<string> visibleIds = new HashSet<string>(StringComparer.Ordinal);
            if (filtered != null)
            {
                for (int i = 0; i < filtered.Count; i++)
                {
                    ModProjectRecord record = filtered[i];
                    if (record != null && !string.IsNullOrEmpty(record.ModId))
                        visibleIds.Add(record.ModId);
                }
            }

            if (family.BaseGameParent)
                AddBaseGameFamilyHeaderRow(family);
            else if (family.ParentRecord != null)
                AddGridRow(family.ParentRecord, family, true, false);

            bool collapsed = _collapsedFamilyKeys.Contains(family.Key);
            for (int i = 0; i < family.Children.Count; i++)
            {
                ModProjectRecord child = family.Children[i];
                if (child == null || string.IsNullOrEmpty(child.ModId) || !visibleIds.Contains(child.ModId))
                    continue;
                DataGridViewRow childRow = AddGridRow(child, family, false, true);
                if (childRow != null)
                    childRow.Visible = !collapsed;
            }
        }

        private void AddBaseGameFamilyHeaderRow(VariantFamilyGroup family)
        {
            int index = _grid.Rows.Add();
            DataGridViewRow row = _grid.Rows[index];
            row.Tag = new VariantFamilyHeaderTag { Family = family };
            row.Height = 58;
            row.ReadOnly = true;
            row.DefaultCellStyle.BackColor = Color.FromArgb(244, 222, 176);
            row.DefaultCellStyle.ForeColor = Color.FromArgb(70, 62, 48);
            // Return a full preview-cell canvas with the Item Type artwork centred at a
            // deliberately smaller size. DataGridViewImageCellLayout.Zoom otherwise enlarges
            // a small bitmap back up to the cell bounds.
            row.Cells["Preview"].Value = LoadItemTypeFamilyPreview(family.ItemType, 74, 50);
            row.Cells["ModName"].Value = (_collapsedFamilyKeys.Contains(family.Key) ? "\u25b6 " : "\u25bc ") +
                (family.ParentName ?? "Base Game Item");
            row.Cells["ModName"].ToolTipText = "Base-game parent - click this cell to expand/collapse " + family.Children.Count +
                " variant" + (family.Children.Count == 1 ? "" : "s") + ".";
            row.Cells["ItemType"].Value = family.ItemType ?? "";
            row.Cells["ItemOptions"].Value = "Base Game Parent";
            row.Cells["Recolourable"].Value = "\u2014";
            row.Cells["LastBuilt"].Value = "\u2014";
            row.Cells["Installed"].Value = "\u2014";
            row.Cells["Workshop"].Value = "\u2014";
        }

        private VariantFamilyGroup GetFamilyForRow(DataGridViewRow row)
        {
            if (row == null)
                return null;
            VariantFamilyHeaderTag header = row.Tag as VariantFamilyHeaderTag;
            if (header != null)
                return header.Family;
            ModProjectRecord record = row.Tag as ModProjectRecord;
            if (record == null || string.IsNullOrEmpty(record.ModId))
                return null;
            VariantFamilyGroup family;
            return _familyByMemberId.TryGetValue(record.ModId, out family) ? family : null;
        }

        private static bool IsFamilyParentRow(DataGridViewRow row, VariantFamilyGroup family)
        {
            if (row == null || family == null)
                return false;
            if (row.Tag is VariantFamilyHeaderTag)
                return true;
            ModProjectRecord record = row.Tag as ModProjectRecord;
            return record != null && family.ParentRecord != null &&
                string.Equals(record.ModId ?? "", family.ParentRecord.ModId ?? "", StringComparison.Ordinal);
        }

        private void ApplyDefaultCollapsedFamilyState()
        {
            HashSet<string> currentKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (VariantFamilyGroup family in _familyByMemberId.Values)
            {
                if (family == null || string.IsNullOrEmpty(family.Key) || !currentKeys.Add(family.Key))
                    continue;
                if (_knownFamilyKeys.Add(family.Key))
                    _collapsedFamilyKeys.Add(family.Key);
            }

            List<string> staleKeys = new List<string>();
            foreach (string knownKey in _knownFamilyKeys)
                if (!currentKeys.Contains(knownKey))
                    staleKeys.Add(knownKey);
            for (int i = 0; i < staleKeys.Count; i++)
            {
                _knownFamilyKeys.Remove(staleKeys[i]);
                _collapsedFamilyKeys.Remove(staleKeys[i]);
            }
        }

        private void SetAllFamiliesCollapsed(bool collapse)
        {
            if (_grid == null)
                return;

            HashSet<string> familyKeys = GetVisibleFamilyKeys();
            if (familyKeys.Count == 0)
            {
                UpdateFamilyExpandCollapseButton();
                return;
            }

            foreach (string key in familyKeys)
            {
                _knownFamilyKeys.Add(key);
                if (collapse)
                    _collapsedFamilyKeys.Add(key);
                else
                    _collapsedFamilyKeys.Remove(key);
            }

            VariantFamilyGroup familyToReselect = null;
            _suppressSelectionEvents = true;
            _grid.SuspendLayout();
            try
            {
                if (collapse && _grid.CurrentRow != null)
                {
                    VariantFamilyGroup currentFamily = GetFamilyForRow(_grid.CurrentRow);
                    if (currentFamily != null && !IsFamilyParentRow(_grid.CurrentRow, currentFamily))
                    {
                        familyToReselect = currentFamily;
                        _grid.CurrentCell = null;
                    }
                }

                foreach (DataGridViewRow row in _grid.Rows)
                {
                    VariantFamilyGroup family = GetFamilyForRow(row);
                    if (family == null || string.IsNullOrEmpty(family.Key) || !familyKeys.Contains(family.Key))
                        continue;

                    if (IsFamilyParentRow(row, family))
                    {
                        if (row.Tag is VariantFamilyHeaderTag)
                            row.Cells["ModName"].Value = (collapse ? "▶ " : "▼ ") + (family.ParentName ?? "Base Game Item");
                        else
                        {
                            row.Cells["ModName"].Value = (collapse ? "▶ " : "▼ ") + GetGroupedParentDisplayName(family);
                        }
                        continue;
                    }

                    if (collapse && row.Selected)
                        row.Selected = false;
                    row.Visible = !collapse;
                }

                if (collapse && familyToReselect != null)
                {
                    _grid.ClearSelection();
                    SelectVisibleFamilyParentRow(familyToReselect);
                }
            }
            finally
            {
                _grid.ResumeLayout();
                _suppressSelectionEvents = false;
            }

            UpdateFamilyExpandCollapseButton();
            SyncGridScrollBar();
            QueueSelectionSync();
        }

        private HashSet<string> GetVisibleFamilyKeys()
        {
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            if (_grid == null)
                return keys;

            foreach (DataGridViewRow row in _grid.Rows)
            {
                VariantFamilyGroup family = GetFamilyForRow(row);
                if (family != null && !string.IsNullOrEmpty(family.Key) && IsFamilyParentRow(row, family))
                    keys.Add(family.Key);
            }
            return keys;
        }

        private bool AnyVisibleFamilyExpanded()
        {
            HashSet<string> keys = GetVisibleFamilyKeys();
            foreach (string key in keys)
                if (!_collapsedFamilyKeys.Contains(key))
                    return true;
            return false;
        }

        private void UpdateFamilyExpandCollapseButton()
        {
            if (_familyExpandCollapseButton == null)
                return;

            HashSet<string> visibleFamilyKeys = GetVisibleFamilyKeys();
            bool hasFamilies = visibleFamilyKeys.Count > 0;
            bool anyExpanded = false;
            if (hasFamilies)
            {
                foreach (string key in visibleFamilyKeys)
                {
                    if (!_collapsedFamilyKeys.Contains(key))
                    {
                        anyExpanded = true;
                        break;
                    }
                }
            }

            _familyExpandCollapseButton.Visible = hasFamilies;
            _familyExpandCollapseButton.Enabled = hasFamilies;
            _familyExpandCollapseButton.Text = anyExpanded ? "Collapse All" : "Expand All";
            SetUiTip(_familyExpandCollapseButton, anyExpanded
                ? "Collapse all grouped Variant Families and Décor Packs currently shown in My Mods."
                : "Expand all grouped Variant Families and Décor Packs currently shown in My Mods.");
        }

        private void ToggleFamily(VariantFamilyGroup family)
        {
            if (family == null || string.IsNullOrEmpty(family.Key) || _grid == null)
                return;

            bool collapse = !_collapsedFamilyKeys.Contains(family.Key);
            if (collapse)
                _collapsedFamilyKeys.Add(family.Key);
            else
                _collapsedFamilyKeys.Remove(family.Key);

            // Family children remain resident in the grid and are shown/hidden in place so expand/collapse
            // does not reload every preview image on larger libraries.
            _suppressSelectionEvents = true;
            _grid.SuspendLayout();
            try
            {
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    VariantFamilyGroup rowFamily = GetFamilyForRow(row);
                    if (rowFamily == null || !string.Equals(rowFamily.Key, family.Key, StringComparison.Ordinal))
                        continue;

                    if (IsFamilyParentRow(row, family))
                    {
                        if (row.Tag is VariantFamilyHeaderTag)
                            row.Cells["ModName"].Value = (collapse ? "▶ " : "▼ ") + (family.ParentName ?? "Base Game Item");
                        else
                        {
                            row.Cells["ModName"].Value = (collapse ? "▶ " : "▼ ") + GetGroupedParentDisplayName(family);
                        }
                        continue;
                    }

                    if (collapse && row.Selected)
                        row.Selected = false;
                    row.Visible = !collapse;
                }
            }
            finally
            {
                _grid.ResumeLayout();
                _suppressSelectionEvents = false;
            }

            UpdateFamilyExpandCollapseButton();
            SyncGridScrollBar();
            QueueSelectionSync();
        }

        private void ManageSelectedFamily()
        {
            ModProjectRecord selected = GetSingleSelectedRecord();
            if (selected == null || _projects == null)
                return;
            if (IsWallpaperRecord(selected))
            {
                MessageBox.Show(this, "Wallpaper room visuals cannot be members or parents of variant families.",
                    "Variant Families", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_detailsDirty)
            {
                MessageBox.Show(this,
                    "Save or discard the current My Mods edits before changing variant-family membership.",
                    "Manage Variant Family",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            BuildVariantFamilyIndex(_projects);
            VariantFamilyGroup family = null;
            if (!string.IsNullOrEmpty(selected.ModId))
                _familyByMemberId.TryGetValue(selected.ModId, out family);

            bool baseGameFamily = family != null && family.BaseGameParent;
            ModProjectRecord localParent = null;
            string familyKey = "";
            string familyName = "";
            string description = "";

            if (baseGameFamily)
            {
                familyKey = family.Key ?? GetBaseGameFamilyKey(selected);
                familyName = (family.ParentName ?? "Base Game Item") + " family";
                description = "Manage variants of the base-game parent. Current children and eligible standalone mods are shown together so several can be added or removed in one step.";
            }
            else
            {
                localParent = family != null && family.ParentRecord != null ? family.ParentRecord : selected;
                if (localParent == null || string.IsNullOrEmpty(localParent.ModId))
                    return;
                if (string.IsNullOrEmpty(localParent.LastBuiltUtc))
                {
                    MessageBox.Show(this,
                        "Build '" + (localParent.Name ?? "this mod") + "' before using it as the parent of a variant family.",
                        "Manage Variant Family",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
                familyKey = "mod:" + localParent.ModId;
                familyName = (localParent.Name ?? "Parent Mod") + " family";
                description = "Manage variants of this Memento Maker parent. Current children and eligible standalone mods are shown together so several can be added or removed in one step.";
            }

            HashSet<string> currentIds = new HashSet<string>(StringComparer.Ordinal);
            if (family != null)
            {
                for (int i = 0; i < family.Children.Count; i++)
                {
                    ModProjectRecord child = family.Children[i];
                    if (child != null && !string.IsNullOrEmpty(child.ModId))
                        currentIds.Add(child.ModId);
                }
            }

            List<ModProjectRecord> candidates = new List<ModProjectRecord>();
            for (int i = 0; i < _projects.Count; i++)
            {
                ModProjectRecord candidate = _projects[i];
                if (candidate == null || string.IsNullOrEmpty(candidate.ModId))
                    continue;
                if (localParent != null && string.Equals(candidate.ModId, localParent.ModId, StringComparison.Ordinal))
                    continue;

                bool current = currentIds.Contains(candidate.ModId);
                if (current)
                {
                    candidates.Add(candidate);
                    continue;
                }

                if (VariantModes.Normalise(candidate.VariantMode) != VariantModes.Standalone)
                    continue;
                if (IsWallpaperRecord(candidate))
                    continue;

                VariantFamilyGroup candidateFamily = null;
                _familyByMemberId.TryGetValue(candidate.ModId, out candidateFamily);
                if (candidateFamily != null && candidateFamily.ParentRecord != null &&
                    string.Equals(candidateFamily.ParentRecord.ModId ?? "", candidate.ModId, StringComparison.Ordinal))
                    continue;

                candidates.Add(candidate);
            }

            Dictionary<string, string> candidateItemTypes = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < candidates.Count; i++)
            {
                ModProjectRecord candidate = candidates[i];
                if (candidate != null && !string.IsNullOrEmpty(candidate.ModId))
                    candidateItemTypes[candidate.ModId] = GetItemType(candidate);
            }

            using (VariantFamilyManagerForm dialog = new VariantFamilyManagerForm(
                familyName, description, candidates, currentIds, candidateItemTypes))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                HashSet<string> wantedIds = dialog.SelectedModIds ?? new HashSet<string>(StringComparer.Ordinal);
                List<ModProjectRecord> removals = new List<ModProjectRecord>();
                List<ModProjectRecord> additions = new List<ModProjectRecord>();

                for (int i = 0; i < candidates.Count; i++)
                {
                    ModProjectRecord candidate = candidates[i];
                    if (candidate == null || string.IsNullOrEmpty(candidate.ModId))
                        continue;
                    bool current = currentIds.Contains(candidate.ModId);
                    bool wanted = wantedIds.Contains(candidate.ModId);
                    if (current && !wanted)
                        removals.Add(candidate);
                    else if (!current && wanted)
                        additions.Add(candidate);
                }

                if (removals.Count == 0 && additions.Count == 0)
                    return;

                string targetMode = baseGameFamily ? VariantModes.BaseGame : VariantModes.Modded;
                string targetParentId = baseGameFamily || localParent == null ? "" : localParent.ModId;
                string targetBaseArchetypeId = "";
                if (baseGameFamily && familyKey.StartsWith("base:", StringComparison.Ordinal))
                    targetBaseArchetypeId = familyKey.Substring(5);

                try
                {
                    // Validate the full batch before writing anything so a bad candidate does
                    // not leave the family half-updated.
                    for (int i = 0; i < removals.Count; i++)
                        _library.ValidateVariantState(removals[i], VariantModes.Standalone, "");
                    for (int i = 0; i < additions.Count; i++)
                        _library.ValidateVariantState(additions[i], targetMode, targetParentId);

                    for (int i = 0; i < removals.Count; i++)
                        _library.SaveVariantFamilyMembership(removals[i], VariantModes.Standalone, "", "");
                    for (int i = 0; i < additions.Count; i++)
                        _library.SaveVariantFamilyMembership(additions[i], targetMode, targetParentId, targetBaseArchetypeId);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Manage Variant Family", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string selectedId = localParent != null ? localParent.ModId : selected.ModId;
                LoadProjects(selectedId);
            }
        }

        private string GetBaseGameFamilyKey(ModProjectRecord record)
        {
            if (record == null)
                return "base:";
            if (!string.IsNullOrEmpty(record.VariantParentBaseArchetypeId))
                return "base:" + record.VariantParentBaseArchetypeId;
            TemplateDefinition template = FindTemplate(record.Template);
            string archetypeKey = template != null && template.BaseArchetypeId != 0
                ? template.BaseArchetypeId.ToString()
                : (record.Template ?? GetItemType(record));
            return "base:" + archetypeKey;
        }

        private Bitmap LoadItemTypeFamilyPreview(string itemType, int canvasWidth, int canvasHeight)
        {
            string fileName = GetItemTypeIconFile(itemType);
            if (string.IsNullOrEmpty(fileName))
                return null;

            using (Image source = TwoPointTheme.LoadThemeImage(fileName))
            {
                if (source == null)
                    return null;

                // Keep base-game parent icons noticeably smaller than generated mod previews.
                // The transparent canvas is intentionally the same aspect as a normal preview
                // cell so DataGridView's Zoom layout cannot enlarge the inner icon again.
                int safeCanvasWidth = Math.Max(1, canvasWidth);
                int safeCanvasHeight = Math.Max(1, canvasHeight);
                int innerMaxWidth = Math.Min(42, Math.Max(1, safeCanvasWidth - 12));
                int innerMaxHeight = Math.Min(30, Math.Max(1, safeCanvasHeight - 12));
                float scale = Math.Min((float)innerMaxWidth / Math.Max(1, source.Width),
                    (float)innerMaxHeight / Math.Max(1, source.Height));
                scale = Math.Min(1F, scale);
                int width = Math.Max(1, (int)Math.Round(source.Width * scale));
                int height = Math.Max(1, (int)Math.Round(source.Height * scale));
                int x = (safeCanvasWidth - width) / 2;
                int y = (safeCanvasHeight - height) / 2;

                Bitmap result = new Bitmap(safeCanvasWidth, safeCanvasHeight);
                using (Graphics g = Graphics.FromImage(result))
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.DrawImage(source, new Rectangle(x, y, width, height));
                }
                return result;
            }
        }

        private static string GetItemTypeIconFile(string itemType)
        {
            if (string.Equals(itemType, "Decor", StringComparison.OrdinalIgnoreCase) || string.Equals(itemType, "Décor", StringComparison.OrdinalIgnoreCase)) return "item_decor.png";
            if (string.Equals(itemType, "Poster", StringComparison.OrdinalIgnoreCase)) return "item_poster.png";
            if (string.Equals(itemType, "Mural", StringComparison.OrdinalIgnoreCase)) return "item_mural.png";
            if (string.Equals(itemType, "Small Rug", StringComparison.OrdinalIgnoreCase)) return "item_small_rug.png";
            if (string.Equals(itemType, "Large Rug", StringComparison.OrdinalIgnoreCase)) return "item_large_rug.png";
            if (string.Equals(itemType, "Banner", StringComparison.OrdinalIgnoreCase)) return "item_single_banner.png";
            if (string.Equals(itemType, "Double Banner", StringComparison.OrdinalIgnoreCase)) return "item_double_banner.png";
            if (string.Equals(itemType, "Hanging Sign", StringComparison.OrdinalIgnoreCase)) return "item_hanging_sign.png";
            if (string.Equals(itemType, "Wall Sign", StringComparison.OrdinalIgnoreCase)) return "item_wall_sign.png";
            return "";
        }

        private int CompareRecords(ModProjectRecord a, ModProjectRecord b)
        {
            string sort = _sortBy.SelectedItem as string ?? "Last Built";
            if (string.Equals(sort, "Mod Name", StringComparison.OrdinalIgnoreCase))
                return string.Compare(a == null ? "" : a.Name, b == null ? "" : b.Name, StringComparison.OrdinalIgnoreCase);
            if (string.Equals(sort, "Item Type", StringComparison.OrdinalIgnoreCase))
                return string.Compare(GetItemType(a), GetItemType(b), StringComparison.OrdinalIgnoreCase);
            DateTime ad = ParseDate(a == null ? null : a.LastBuiltUtc);
            DateTime bd = ParseDate(b == null ? null : b.LastBuiltUtc);
            return ad.CompareTo(bd);
        }

        private DataGridViewRow AddGridRow(ModProjectRecord record)
        {
            return AddGridRow(record, null, false, false);
        }

        private static string GetGroupedParentDisplayName(VariantFamilyGroup family)
        {
            if (family == null) return "";
            ModProjectRecord parent = family.ParentRecord;
            string parentName = parent == null ? (family.ParentName ?? "") : (parent.Name ?? family.ParentName ?? "");
            if (family.DecorPack)
                return (family.ParentName ?? "Wallpaper Pack") + " · " + parentName;
            return parentName;
        }

        private DataGridViewRow AddGridRow(ModProjectRecord record, VariantFamilyGroup family, bool isParent, bool isChild)
        {
            int index = _grid.Rows.Add();
            DataGridViewRow row = _grid.Rows[index];
            row.Tag = record;
            row.Height = 58;
            if (!_skipGridPreviewImages)
                row.Cells["Preview"].Value = TryGetCachedGridPreview(record, 74, 50);
            string modName = record.Name ?? "";
            if (family != null && isParent)
            {
                modName = GetGroupedParentDisplayName(family);
                modName = (_collapsedFamilyKeys.Contains(family.Key) ? "▶ " : "▼ ") + modName;
            }
            else if (family != null && isChild)
                modName = "    └─ " + modName;
            row.Cells["ModName"].Value = modName;
            if (family != null && isParent)
            {
                row.Cells["ModName"].ToolTipText = family.DecorPack
                    ? "Décor Pack - click to expand/collapse " + (family.Children.Count + 1) + " wallpapers."
                    : "Variant family parent - click this cell to expand/collapse " + family.Children.Count +
                        " variant" + (family.Children.Count == 1 ? "" : "s") + ".";
                row.DefaultCellStyle.BackColor = Color.FromArgb(244, 222, 176);
            }
            else if (family != null && isChild)
            {
                row.Cells["ModName"].Style.Font = TwoPointTheme.BodyFont(8.5F);
                row.Cells["ModName"].ToolTipText = family.DecorPack
                    ? "Member of Décor Pack: " + (family.ParentName ?? "Wallpaper Pack")
                    : (family.BaseGameParent
                        ? "Variant of base-game item: " + (family.ParentName ?? "Base Game Item")
                        : "Variant of: " + (family.ParentName ?? "Parent Mod"));
                row.DefaultCellStyle.BackColor = Color.FromArgb(250, 237, 210);
            }
            row.Cells["ItemType"].Value = GetItemType(record);
            row.Cells["ItemOptions"].Value = GetItemOptionsText(record);
            string itemOptionsText = GetItemOptionsText(record);
            if (!string.IsNullOrEmpty(itemOptionsText))
            {
                string itemOptionsTip = itemOptionsText;
                if (itemOptionsTip.EndsWith(" Banner", StringComparison.OrdinalIgnoreCase))
                    itemOptionsTip = itemOptionsTip.Substring(0, itemOptionsTip.Length - " Banner".Length);
                row.Cells["ItemOptions"].ToolTipText = itemOptionsTip;
            }
            row.Cells["Recolourable"].Value = GetRecolourableStatus(record);
            row.Cells["LastBuilt"].Value = FormatDateTwoLines(record.LastBuiltUtc);
            string installedStatus = GetViewInstalledStatus(record);
            row.Cells["Installed"].Value = FormatInstalledDisplay(installedStatus);

            string workshopStatus = GetViewWorkshopStatus(record);
            row.Cells["Workshop"].Value = workshopStatus;
            string workshopDisplayId = record.WorkshopPublishedFileId ?? "";
            if (family != null && BuildPackageModes.IsCombined(record.LastBuildPackageMode))
            {
                WorkshopFamilyState familyWorkshopState = GetViewWorkshopFamilyState(family.Key);
                if (familyWorkshopState != null && !string.IsNullOrEmpty(familyWorkshopState.PublishedFileId))
                    workshopDisplayId = familyWorkshopState.PublishedFileId;
            }
            if (!string.IsNullOrEmpty(workshopDisplayId))
            {
                string workshopTip = "Workshop ID: " + workshopDisplayId;
                if (string.Equals(workshopStatus, "Published (old)", StringComparison.Ordinal))
                {
                    if (record.WorkshopLinkNeedsUpdate)
                        workshopTip += Environment.NewLine + "This Workshop item was relinked and needs one successful update before it is considered current.";
                    else
                        workshopTip += Environment.NewLine + "The local mod has changed since its last successful Steam Workshop update.";
                }
                row.Cells["Workshop"].ToolTipText = workshopTip;
            }
            else if (string.Equals(record.WorkshopLinkState, "Missing", StringComparison.OrdinalIgnoreCase) &&
                     !string.IsNullOrEmpty(record.WorkshopPreviousPublishedFileId))
                row.Cells["Workshop"].ToolTipText = "Previous Workshop ID: " + record.WorkshopPreviousPublishedFileId;
            else
                row.Cells["Workshop"].ToolTipText = "This mod has not been published to Steam Workshop.";
            return row;
        }

        private void GridCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            string column = _grid.Columns[e.ColumnIndex].Name;
            if (column != "ItemOptions" && column != "Recolourable" && column != "Installed" && column != "Workshop")
                return;

            ModProjectRecord record = _grid.Rows[e.RowIndex].Tag as ModProjectRecord;
            if (record == null)
                return;

            e.PaintBackground(e.ClipBounds, true);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (column == "ItemOptions")
            {
                string shape = GetShape(record);
                if (!string.IsNullOrEmpty(shape))
                {
                    Rectangle bounds = new Rectangle(e.CellBounds.X + 4, e.CellBounds.Y + 4,
                        Math.Max(12, e.CellBounds.Width - 8), Math.Max(12, e.CellBounds.Height - 8));
                    DrawShapeIcon(g, bounds, shape);
                }
                else
                {
                    string iconFile = IsWallpaperRecord(record) ? "item_option_wallpaper.png" : GetPosterSizeIconFile(record);
                    if (string.IsNullOrEmpty(iconFile))
                        iconFile = GetHangingSignSizeIconFile(record);
                    if (string.IsNullOrEmpty(iconFile))
                        iconFile = GetWallSignSizeIconFile(record);
                    if (string.IsNullOrEmpty(iconFile))
                        iconFile = GetBannerThemeIconFile(record);
                    if (!string.IsNullOrEmpty(iconFile))
                    {
                        using (Image icon = TwoPointTheme.LoadThemeImage(iconFile))
                        {
                            if (icon != null)
                            {
                                Rectangle target = FitImageBounds(icon, e.CellBounds, 42, 42);
                                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                g.DrawImage(icon, target);
                            }
                        }
                    }
                }
                e.Handled = true;
                return;
            }

            if (column == "Recolourable")
            {
                string status = GetRecolourableStatus(record);
                Rectangle iconBounds = new Rectangle(
                    e.CellBounds.X + ((e.CellBounds.Width - 22) / 2),
                    e.CellBounds.Y + ((e.CellBounds.Height - 22) / 2), 22, 22);
                DrawStatusIcon(g, iconBounds, status == "N/A" ? "neutral" : (status.StartsWith("\u2713") ? "yes" : "no"));
                e.Handled = true;
                return;
            }

            if (column == "Workshop")
            {
                string workshop = GetWorkshopStatus(record);
                string workshopKind = workshop == "Published" ? "yes" :
                    (workshop == "Published (old)" ? "old" : (workshop == "Missing" ? "no" : "neutral"));
                Rectangle workshopIcon = new Rectangle(e.CellBounds.X + 5,
                    e.CellBounds.Y + ((e.CellBounds.Height - 20) / 2), 20, 20);
                DrawStatusIcon(g, workshopIcon, workshopKind);
                Color workshopColor = workshopKind == "yes" ? Color.FromArgb(76, 164, 26) :
                    (workshopKind == "old" ? Color.FromArgb(217, 145, 16) :
                    (workshopKind == "no" ? TwoPointTheme.DangerRed : Color.FromArgb(128, 126, 119)));
                Rectangle workshopText = new Rectangle(e.CellBounds.X + 31, e.CellBounds.Y,
                    Math.Max(1, e.CellBounds.Width - 34), e.CellBounds.Height);
                TextRenderer.DrawText(g, workshop, TwoPointTheme.BoldFont(8.1F), workshopText, workshopColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                e.Handled = true;
                return;
            }

            string installed = GetInstalledStatus(record);
            string kind = installed == "Installed" ? "yes" : (installed == "Installed (old)" ? "old" : "neutral");
            Rectangle installedIcon = new Rectangle(e.CellBounds.X + 5,
                e.CellBounds.Y + ((e.CellBounds.Height - 20) / 2), 20, 20);
            DrawStatusIcon(g, installedIcon, kind);
            Color textColor = kind == "yes" ? Color.FromArgb(76, 164, 26) :
                (kind == "old" ? Color.FromArgb(217, 145, 16) : Color.FromArgb(128, 126, 119));
            Rectangle textBounds = new Rectangle(e.CellBounds.X + 31, e.CellBounds.Y,
                Math.Max(1, e.CellBounds.Width - 34), e.CellBounds.Height);
            TextRenderer.DrawText(g, installed, TwoPointTheme.BoldFont(8.1F), textBounds, textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            e.Handled = true;
        }

        private static void DrawStatusIcon(Graphics g, Rectangle bounds, string kind)
        {
            Color fill = Color.FromArgb(145, 141, 132);
            if (kind == "yes") fill = Color.FromArgb(93, 174, 25);
            else if (kind == "old") fill = Color.FromArgb(235, 157, 16);
            else if (kind == "no") fill = Color.FromArgb(213, 59, 49);

            using (SolidBrush brush = new SolidBrush(fill))
                g.FillEllipse(brush, bounds);

            using (Pen pen = new Pen(Color.White, Math.Max(1.5F, bounds.Width / 10F)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                if (kind == "yes" || kind == "old")
                {
                    g.DrawLine(pen, bounds.Left + (int)(bounds.Width * 0.25F), bounds.Top + (int)(bounds.Height * 0.52F),
                        bounds.Left + (int)(bounds.Width * 0.43F), bounds.Top + (int)(bounds.Height * 0.70F));
                    g.DrawLine(pen, bounds.Left + (int)(bounds.Width * 0.43F), bounds.Top + (int)(bounds.Height * 0.70F),
                        bounds.Left + (int)(bounds.Width * 0.76F), bounds.Top + (int)(bounds.Height * 0.32F));
                }
                else if (kind == "no")
                {
                    g.DrawLine(pen, bounds.Left + (int)(bounds.Width * 0.30F), bounds.Top + (int)(bounds.Height * 0.30F),
                        bounds.Left + (int)(bounds.Width * 0.70F), bounds.Top + (int)(bounds.Height * 0.70F));
                    g.DrawLine(pen, bounds.Left + (int)(bounds.Width * 0.70F), bounds.Top + (int)(bounds.Height * 0.30F),
                        bounds.Left + (int)(bounds.Width * 0.30F), bounds.Top + (int)(bounds.Height * 0.70F));
                }
                else if (kind == "neutral")
                {
                    g.DrawLine(pen, bounds.Left + (int)(bounds.Width * 0.28F), bounds.Top + (bounds.Height / 2),
                        bounds.Left + (int)(bounds.Width * 0.72F), bounds.Top + (bounds.Height / 2));
                }
            }

        }

        private static void DrawShapeIcon(Graphics g, Rectangle bounds, string shape)
        {
            string iconFile = GetRugShapeIconFileName(shape);
            if (!string.IsNullOrEmpty(iconFile))
            {
                using (Image themed = TwoPointTheme.LoadThemeImage(iconFile))
                {
                    if (themed != null)
                    {
                        Rectangle target = FitImageRectangle(themed.Width, themed.Height, Rectangle.Inflate(bounds, -1, -1), false);
                        g.DrawImage(themed, target);
                        return;
                    }
                }
            }

            int size = Math.Max(10, Math.Min(24, Math.Min(bounds.Width - 2, bounds.Height - 2)));
            int cx = bounds.Left + bounds.Width / 2;
            int cy = bounds.Top + bounds.Height / 2;
            Rectangle icon = new Rectangle(cx - size / 2, cy - size / 2, size, size);
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(78, 69, 54)))
            {
                if (shape == "Circle")
                {
                    g.FillEllipse(brush, icon);
                }
                else if (shape == "Square")
                {
                    g.FillRectangle(brush, icon);
                }
                else if (shape == "Rectangle")
                {
                    Rectangle r = new Rectangle(cx - size / 2, cy - Math.Max(4, size / 4), size, Math.Max(8, size / 2));
                    g.FillRectangle(brush, r);
                }
                else if (shape == "Octagon")
                {
                    int inset = Math.Max(3, size / 4);
                    Point[] points = new Point[]
                    {
                        new Point(icon.Left + inset, icon.Top), new Point(icon.Right - inset, icon.Top),
                        new Point(icon.Right, icon.Top + inset), new Point(icon.Right, icon.Bottom - inset),
                        new Point(icon.Right - inset, icon.Bottom), new Point(icon.Left + inset, icon.Bottom),
                        new Point(icon.Left, icon.Bottom - inset), new Point(icon.Left, icon.Top + inset)
                    };
                    g.FillPolygon(brush, points);
                }
            }
        }

        private static Rectangle FitImageRectangle(int sourceWidth, int sourceHeight, Rectangle target, bool allowUpscale)
        {
            if (sourceWidth <= 0 || sourceHeight <= 0)
                return target;

            double scale = Math.Min((double)target.Width / sourceWidth, (double)target.Height / sourceHeight);
            if (!allowUpscale)
                scale = Math.Min(1.0, scale);

            int width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
            int height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
            int x = target.X + (target.Width - width) / 2;
            int y = target.Y + (target.Height - height) / 2;
            return new Rectangle(x, y, width, height);
        }

        private static string GetRugShapeIconFileName(string shape)
        {
            if (string.Equals(shape, "Circle", StringComparison.OrdinalIgnoreCase)) return "rug_shape_circle.png";
            if (string.Equals(shape, "Square", StringComparison.OrdinalIgnoreCase)) return "rug_shape_square.png";
            if (string.Equals(shape, "Rectangle", StringComparison.OrdinalIgnoreCase)) return "rug_shape_rectangle.png";
            if (string.Equals(shape, "Octagon", StringComparison.OrdinalIgnoreCase)) return "rug_shape_octagon.png";
            return null;
        }

        private void GridCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            string column = _grid.Columns[e.ColumnIndex].Name;
            string value = Convert.ToString(e.Value) ?? "";

            if (string.Equals(column, "Recolourable", StringComparison.Ordinal))
            {
                if (value.StartsWith("\u2713"))
                    e.CellStyle.ForeColor = Color.FromArgb(76, 164, 26);
                else if (value.StartsWith("\u2715"))
                    e.CellStyle.ForeColor = TwoPointTheme.DangerRed;
                else
                    e.CellStyle.ForeColor = Color.FromArgb(145, 141, 132);
            }
            else if (string.Equals(column, "Installed", StringComparison.Ordinal))
            {
                if (value.IndexOf("Installed (old)", StringComparison.OrdinalIgnoreCase) >= 0)
                    e.CellStyle.ForeColor = Color.FromArgb(217, 145, 16);
                else if (value.IndexOf("Installed", StringComparison.OrdinalIgnoreCase) >= 0 && value.IndexOf("Not", StringComparison.OrdinalIgnoreCase) < 0)
                    e.CellStyle.ForeColor = Color.FromArgb(76, 164, 26);
                else
                    e.CellStyle.ForeColor = Color.FromArgb(128, 126, 119);
            }
            else if (string.Equals(column, "ItemOptions", StringComparison.Ordinal))
            {
                e.CellStyle.ForeColor = Color.FromArgb(79, 70, 55);
            }
        }

        private void QueueSelectionSync()
        {
            if (_suppressSelectionEvents || IsDisposed || _selectionSyncPending)
                return;

            if (!IsHandleCreated)
            {
                HandleSelectionChanged();
                return;
            }

            _selectionSyncPending = true;
            try
            {
                BeginInvoke((Action)delegate
                {
                    _selectionSyncPending = false;
                    if (!IsDisposed)
                        HandleSelectionChanged();
                });
            }
            catch
            {
                _selectionSyncPending = false;
            }
        }

        private ModProjectRecord GetSingleSelectedRecord()
        {
            if (_grid == null)
                return null;

            // Synthetic base-game family headers may be selected transiently by WinForms.
            // Count only real project rows so a header can never block or masquerade as the
            // one selected mod.
            List<ModProjectRecord> selectedProjects = SelectedProjects();
            if (selectedProjects.Count == 1)
                return selectedProjects[0];
            if (selectedProjects.Count > 1)
                return null;

            // WinForms can briefly report SelectedRows as empty while a full-row selection
            // is settling, particularly after an embedded form is removed/re-added to its
            // parent. CurrentRow remains reliable in that small window.
            if (_grid.CurrentRow != null && _grid.CurrentRow.Selected)
                return _grid.CurrentRow.Tag as ModProjectRecord;

            return null;
        }

        private VariantFamilyGroup GetSelectedBaseGameFamilyHeader()
        {
            if (_grid == null)
                return null;

            foreach (DataGridViewRow row in _grid.SelectedRows)
            {
                VariantFamilyHeaderTag tag = row.Tag as VariantFamilyHeaderTag;
                if (tag != null && tag.Family != null)
                    return tag.Family;
            }

            if (_grid.CurrentRow != null && _grid.CurrentRow.Selected)
            {
                VariantFamilyHeaderTag current = _grid.CurrentRow.Tag as VariantFamilyHeaderTag;
                if (current != null)
                    return current.Family;
            }
            return null;
        }

        private void ShowBaseGameFamilyDetails(VariantFamilyGroup family)
        {
            _selectedRecord = null;
            _detailsDirty = false;
            string name = family == null || string.IsNullOrEmpty(family.ParentName) ? "Base Game Parent" : family.ParentName;
            string type = family == null || string.IsNullOrEmpty(family.ItemType) ? "" : family.ItemType;
            int children = family == null ? 0 : family.Children.Count;
            string summary = name + "\nBase Game Parent" +
                (string.IsNullOrEmpty(type) ? "" : " · " + type) +
                " · " + children + " variant" + (children == 1 ? "" : "s") +
                "\n\nThis parent is supplied by Two Point Museum and has no editable Memento Maker mod details. " +
                "Select a child variant to view or edit its mod settings.";
            SetMultiSelectionMode(true, summary);
        }

        private void HandleSelectionChanged()
        {
            if (_suppressSelectionEvents)
                return;

            int count = SelectedProjects().Count;
            ModProjectRecord next = GetSingleSelectedRecord();
            if (count == 0 && next != null)
                count = 1;

            VariantFamilyGroup selectedBaseGameFamily = count == 0 ? GetSelectedBaseGameFamilyHeader() : null;

            if (_detailsDirty && _selectedRecord != null && next != _selectedRecord)
            {
                DialogResult result = TwoPointTheme.ShowMessage(this,
                    "You have unsaved changes for '" + (_selectedRecord.Name ?? "this mod") + "'.\n\n" +
                    "If you change selection now, those edits will be discarded and cannot be recovered.\n\n" +
                    "Discard the changes and select another mod?",
                    "Unsaved Changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes)
                {
                    // Restore the previous grid selection without reloading the saved record.
                    // Reloading here would overwrite the unsaved values the user explicitly
                    // chose to keep editing.
                    ReselectRecordPreservingEdits(_selectedRecord.ModId);
                    return;
                }
            }

            if (selectedBaseGameFamily != null)
            {
                ShowBaseGameFamilyDetails(selectedBaseGameFamily);
            }
            else if (count == 1 && next != null)
            {
                SetMultiSelectionMode(false, null);
                LoadDetails(next);
            }
            else if (count > 1)
            {
                _selectedRecord = null;
                _detailsDirty = false;
                List<ModProjectRecord> selectedForMessage = SelectedProjects();
                bool wallpaperPackSelection = selectedForMessage.Count > 1 && AllWallpaperRecords(selectedForMessage);
                SetMultiSelectionMode(true, wallpaperPackSelection
                    ? count + " wallpapers selected. Rebuild Decor Pack installs them together in one mod."
                    : count + " mods selected. Rebuild builds each selected mod in one queue.");
            }
            else
            {
                _selectedRecord = null;
                SetMultiSelectionMode(false, null);
                ClearDetails("Select a mod to view its details.");
            }

            UpdateButtons();
        }

        private void SetMultiSelectionMode(bool enabled, string message)
        {
            if (_detailsContent != null)
                _detailsContent.Visible = !enabled;

            if (_multiSelectionMessage != null)
            {
                _multiSelectionMessage.Text = message ?? "";
                _multiSelectionMessage.Visible = enabled;
                if (enabled)
                    _multiSelectionMessage.BringToFront();
            }

            if (_detailsScrollBar != null)
            {
                if (enabled)
                {
                    _detailsScrollBar.Visible = false;
                    _detailsScrollBar.Value = 0;
                    if (_detailsContent != null)
                        _detailsContent.Top = 0;
                }
                else
                {
                    UpdateDetailsScrollBar();
                }
            }
        }

        private void LoadDetails(ModProjectRecord record)
        {
            SetMultiSelectionMode(false, null);
            _loadingDetails = true;
            try
            {
                _selectedRecord = record;
                _pendingArtworkPath = null;
                _pendingSecondaryArtworkPath = null;
                _editRightDoubleBanner = false;
                _detailsDirty = false;
                _nameBox.Text = record.Name ?? "";
                _descriptionBox.Text = record.Description ?? "";
                _itemTypeBox.Text = GetItemType(record);
                _modIdBox.Text = record.ModId ?? "";
                if (_costValue != null)
                    _costValue.Value = Math.Max(_costValue.Minimum, Math.Min(_costValue.Maximum, record.ItemCost));
                if (_kudoshValue != null)
                    _kudoshValue.Value = Math.Max(_kudoshValue.Minimum, Math.Min(_kudoshValue.Maximum, record.KudoshCost));
                if (_costSlider != null)
                    _costSlider.Value = Math.Max(_costSlider.Minimum, Math.Min(_costSlider.Maximum, record.ItemCost));
                if (_kudoshSlider != null)
                    _kudoshSlider.Value = Math.Max(_kudoshSlider.Minimum, Math.Min(_kudoshSlider.Maximum, record.KudoshCost));
                UpdateReadOnlyOptions(record);
                ApplyDetailApplicability(record);
                if (_recolourableToggle != null)
                    _recolourableToggle.Checked = (record.Template ?? "").IndexOf("Staff", StringComparison.OrdinalIgnoreCase) >= 0;
                PopulateVariantChoices(record);
                if (IsWallpaperRecord(record))
                    ApplyWallpaperDecorPackRules();
                else
                    ApplyVariantRulesToDetails();
                SetMyModsDoubleBannerSideSelectorVisible(IsDualArtworkRecord(record));
                SetPreviewImage(LoadDetailPreview(record));
                UpdateDetailState(record);
            }
            finally
            {
                _loadingDetails = false;
            }
        }

        private void ClearDetails(string message)
        {
            SetMultiSelectionMode(false, null);
            _loadingDetails = true;
            try
            {
                _pendingArtworkPath = null;
                _pendingSecondaryArtworkPath = null;
                _editRightDoubleBanner = false;
                _detailsDirty = false;
                _nameBox.Text = "";
                _descriptionBox.Text = "";
                _itemTypeBox.Text = "";
                _modIdBox.Text = "";
                _detailShape = "";
                _detailOptionIconFile = "";
                ClearOptionPanel();
                if (_itemOptionsPanel != null)
                    _itemOptionsPanel.Invalidate();
                if (_costValue != null) _costValue.Value = 0;
                if (_kudoshValue != null) _kudoshValue.Value = 0;
                if (_costSlider != null) _costSlider.Value = 0;
                if (_kudoshSlider != null) _kudoshSlider.Value = 0;
                if (_recolourableToggle != null) _recolourableToggle.Checked = false;
                if (_recolourablePanel != null) _recolourablePanel.Visible = false;
                if (_variantParentBox != null)
                {
                    _variantParentBox.Items.Clear();
                    _variantParentBox.Enabled = false;
                }
                if (_variantHelpLabel != null) _variantHelpLabel.Text = "";
                if (_itemOptionsHolder != null) _itemOptionsHolder.Visible = false;
                SetMyModsDoubleBannerSideSelectorVisible(false);
                SetPreviewImage(null);
                _detailStateLabel.Text = message ?? "";
            }
            finally
            {
                _loadingDetails = false;
            }
        }

        private void UpdateReadOnlyOptions(ModProjectRecord record)
        {
            _detailShape = GetShape(record);
            _detailOptionIconFile = IsWallpaperRecord(record) ? "item_option_wallpaper.png" : GetPosterSizeIconFile(record);
            if (string.IsNullOrEmpty(_detailOptionIconFile))
                _detailOptionIconFile = GetHangingSignSizeIconFile(record);
            if (string.IsNullOrEmpty(_detailOptionIconFile))
                _detailOptionIconFile = GetWallSignSizeIconFile(record);
            if (string.IsNullOrEmpty(_detailOptionIconFile))
                _detailOptionIconFile = GetBannerThemeIconFile(record);
            ClearOptionPanel();
            if (_itemOptionsPanel != null)
                _itemOptionsPanel.Invalidate();
        }

        private void ClearOptionPanel()
        {
            if (_itemOptionsPanel == null)
                return;
            _itemOptionsPanel.Controls.Clear();
        }

        private void ApplyDetailApplicability(ModProjectRecord record)
        {
            string key = record == null ? "" : (record.Template ?? "");
            bool wallpaper = string.Equals(key, "Wallpaper", StringComparison.OrdinalIgnoreCase);
            bool poster = IsPosterTemplateKey(key);
            bool rug = key.IndexOf("Rug", StringComparison.OrdinalIgnoreCase) >= 0;
            bool banner = key.EndsWith(" Banner", StringComparison.OrdinalIgnoreCase);
            bool hangingSign = key.EndsWith(" Hanging Sign", StringComparison.OrdinalIgnoreCase);
            bool wallSign = key.EndsWith(" Wall Sign", StringComparison.OrdinalIgnoreCase);
            if (_recolourablePanel != null)
                _recolourablePanel.Visible = rug;
            if (_itemOptionsHolder != null)
                _itemOptionsHolder.Visible = wallpaper || poster || rug || banner || hangingSign || wallSign;
            if (_variantOptionsPanel != null)
                _variantOptionsPanel.Visible = true;
            if (_variantOptionsTitleLabel != null)
                _variantOptionsTitleLabel.Text = wallpaper ? "Décor Pack Options" : "Variant Options";
            if (_costHolder != null)
                _costHolder.Enabled = !wallpaper;
            if (_kudoshHolder != null)
                _kudoshHolder.Enabled = !wallpaper;
            if (wallpaper)
            {
                if (_costValue != null && _costValue.Value != 0) _costValue.Value = 0;
                if (_kudoshValue != null && _kudoshValue.Value != 0) _kudoshValue.Value = 0;
                if (_costSlider != null) _costSlider.Enabled = false;
                if (_kudoshSlider != null) _kudoshSlider.Enabled = false;
            }
            else
            {
                if (_costSlider != null) _costSlider.Enabled = true;
            }
        }

        private void PopulateVariantChoices(ModProjectRecord record)
        {
            if (_variantParentBox == null || record == null)
                return;

            if (IsWallpaperRecord(record))
            {
                PopulateWallpaperDecorPackChoices(record);
                return;
            }

            _variantParentBox.MaxDropDownItems = 12;
            _variantParentBox.IntegralHeight = true;
            if (_variantOptionsTitleLabel != null)
                _variantOptionsTitleLabel.Text = "Variant Options";
            SetUiTip(_variantParentBox, "Choose whether this item is standalone, a variant of its base-game item, or a child of another built Memento Maker mod.");

            string desiredMode = VariantModes.Normalise(record.VariantMode);
            string desiredParentId = record.VariantParentModId ?? "";
            List<ModProjectRecord> children = _library.FindChildren(record.ModId);
            bool lockedAsParent = children.Count > 0;
            if (lockedAsParent)
            {
                desiredMode = VariantModes.Standalone;
                desiredParentId = "";
            }

            _variantParentBox.Items.Clear();
            TemplateDefinition template = FindTemplate(record.Template);
            TemplateDefinition selectedBaseTemplate = template;
            long selectedBaseArchetypeId = 0;
            if (!string.IsNullOrEmpty(record.VariantParentBaseArchetypeId))
                long.TryParse(record.VariantParentBaseArchetypeId, out selectedBaseArchetypeId);
            if (selectedBaseArchetypeId != 0)
                selectedBaseTemplate = FindTemplateByBaseArchetypeId(selectedBaseArchetypeId) ?? template;
            _variantParentBox.Items.Add(new VariantParentChoice
            {
                Mode = VariantModes.Standalone,
                Name = "Standalone Item / Parent Item"
            });
            _variantParentBox.Items.Add(new VariantParentChoice
            {
                Mode = VariantModes.BaseGame,
                Name = selectedBaseTemplate == null ? GetItemType(record) : (selectedBaseTemplate.BaseItemName ?? selectedBaseTemplate.DisplayName ?? selectedBaseTemplate.Key)
            });
            _variantParentBox.Items.Add(new VariantParentChoice
            {
                IsSeparator = true
            });

            List<ModProjectRecord> parents = _library.GetEligibleVariantParents(record.ModId);
            bool desiredFound = false;
            for (int i = 0; i < parents.Count; i++)
            {
                ModProjectRecord parent = parents[i];
                VariantParentChoice choice = new VariantParentChoice
                {
                    Mode = VariantModes.Modded,
                    ModId = parent.ModId,
                    Name = string.IsNullOrEmpty(parent.Name) ? parent.ModId : parent.Name,
                    ItemType = GetItemType(parent)
                };
                _variantParentBox.Items.Add(choice);
                if (!string.IsNullOrEmpty(desiredParentId) &&
                    string.Equals(parent.ModId, desiredParentId, StringComparison.Ordinal))
                    desiredFound = true;
            }

            if (desiredMode == VariantModes.Modded && !desiredFound && !string.IsNullOrEmpty(desiredParentId))
            {
                ModProjectRecord missingParent = _library.Load(desiredParentId);
                _variantParentBox.Items.Add(new VariantParentChoice
                {
                    Mode = VariantModes.Modded,
                    ModId = desiredParentId,
                    Name = missingParent == null ? "Missing parent - " + desiredParentId :
                        (string.IsNullOrEmpty(missingParent.Name) ? desiredParentId : missingParent.Name),
                    ItemType = missingParent == null ? "" : GetItemType(missingParent)
                });
            }

            int selected = -1;
            for (int i = 0; i < _variantParentBox.Items.Count; i++)
            {
                VariantParentChoice choice = _variantParentBox.Items[i] as VariantParentChoice;
                if (choice == null || choice.IsSeparator || VariantModes.Normalise(choice.Mode) != desiredMode)
                    continue;
                if (desiredMode == VariantModes.Modded &&
                    !string.Equals(choice.ModId ?? "", desiredParentId, StringComparison.Ordinal))
                    continue;
                selected = i;
                break;
            }

            _variantParentBox.SelectedIndex = selected >= 0 ? selected : 0;
            _variantParentBox.Enabled = !lockedAsParent;

            if (_variantHelpLabel != null && lockedAsParent)
            {
                _variantHelpLabel.Text = "Parent of " + children.Count + " variant" + (children.Count == 1 ? "" : "s") +
                    ". Use Manage... to add/remove several at once.";
            }
        }

        private VariantParentChoice GetSelectedVariantChoice()
        {
            return _variantParentBox == null ? null : _variantParentBox.SelectedItem as VariantParentChoice;
        }

        private DecorPackChoice GetSelectedWallpaperDecorPackChoice()
        {
            return _variantParentBox == null ? null : _variantParentBox.SelectedItem as DecorPackChoice;
        }

        private void PopulateWallpaperDecorPackChoices(ModProjectRecord record)
        {
            if (_variantParentBox == null)
                return;

            // Keep the two fixed actions plus the first existing pack visible immediately.
            // OwnerDrawVariable combos calculate their popup from the base ItemHeight rather
            // than the measured row height, so MaxDropDownItems alone can still render only
            // two rows. Give the Wallpaper selector an explicit three-row popup height.
            _variantParentBox.MaxDropDownItems = 3;
            _variantParentBox.IntegralHeight = false;
            int wallpaperPackRowHeight = Math.Max(24, _variantParentBox.Font == null ? 24 : _variantParentBox.Font.Height + 10);
            _variantParentBox.DropDownHeight = (wallpaperPackRowHeight * 3) + 4;

            string desiredKey = BuildPackageModes.Normalise(record == null ? "" : record.LastBuildPackageMode) == BuildPackageModes.DecorPack
                ? (record.LastBuiltFamilyKey ?? "")
                : "";

            Dictionary<string, DecorPackChoice> packs = new Dictionary<string, DecorPackChoice>(StringComparer.Ordinal);
            List<ModProjectRecord> records = _library.LoadAll();
            for (int i = 0; i < records.Count; i++)
            {
                ModProjectRecord candidate = records[i];
                if (!IsWallpaperRecord(candidate) ||
                    BuildPackageModes.Normalise(candidate.LastBuildPackageMode) != BuildPackageModes.DecorPack ||
                    string.IsNullOrEmpty(candidate.LastBuiltFamilyKey))
                    continue;

                DecorPackChoice pack;
                if (!packs.TryGetValue(candidate.LastBuiltFamilyKey, out pack))
                {
                    pack = new DecorPackChoice();
                    pack.PackKey = candidate.LastBuiltFamilyKey;
                    pack.PackName = string.IsNullOrWhiteSpace(candidate.LastBuiltFamilyName) ? "Wallpaper Pack" : candidate.LastBuiltFamilyName.Trim();
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

            try
            {
                _variantParentBox.Items.Clear();
                _variantParentBox.Items.Add(new DecorPackChoice { Standalone = true, PackName = "Standalone Wallpaper" });
                _variantParentBox.Items.Add(new DecorPackChoice { NewPack = true, PackName = "New Décor Pack" });
                for (int i = 0; i < orderedPacks.Count; i++)
                    _variantParentBox.Items.Add(orderedPacks[i]);

                int selected = 0;
                if (!string.IsNullOrEmpty(desiredKey))
                {
                    for (int i = 0; i < _variantParentBox.Items.Count; i++)
                    {
                        DecorPackChoice choice = _variantParentBox.Items[i] as DecorPackChoice;
                        if (choice != null && string.Equals(choice.PackKey ?? "", desiredKey, StringComparison.Ordinal))
                        {
                            selected = i;
                            break;
                        }
                    }
                }
                if (_variantParentBox.Items.Count > 0)
                    _variantParentBox.SelectedIndex = selected;
                _variantParentBox.Enabled = true;
            }
            finally
            {
            }

            if (_manageFamilyButton != null)
                _manageFamilyButton.Visible = false;
            ApplyWallpaperDecorPackRules();
        }

        private void ApplyWallpaperDecorPackRules()
        {
            if (_variantOptionsTitleLabel != null)
                _variantOptionsTitleLabel.Text = "Décor Pack Options";
            SetUiTip(_variantParentBox, "Choose whether this Wallpaper stays standalone, starts a new Décor Pack, or joins an existing Décor Pack.");
            if (_variantHelpLabel == null)
                return;

            DecorPackChoice choice = GetSelectedWallpaperDecorPackChoice();
            if (choice == null || choice.Standalone)
                _variantHelpLabel.Text = "Standalone Wallpaper. Saving keeps this wallpaper separate from Décor Packs.";
            else if (choice.NewPack)
                _variantHelpLabel.Text = "Starts a new Décor Pack. You will be asked for the pack name when you save.";
            else
                _variantHelpLabel.Text = "Adds this wallpaper to the selected Décor Pack. Saving marks the affected wallpapers Installed (old) until rebuilt.";
        }

        private bool TryResolveWallpaperDecorPackSelection(ModProjectRecord record, out string packKey, out string packName)
        {
            packKey = "";
            packName = "";
            DecorPackChoice choice = GetSelectedWallpaperDecorPackChoice();
            if (choice == null || choice.Standalone)
                return true;

            if (choice.NewPack)
            {
                string suggestedName = BuildPackageModes.Normalise(record == null ? "" : record.LastBuildPackageMode) == BuildPackageModes.DecorPack
                    ? (record.LastBuiltFamilyName ?? "")
                    : "";
                string entered = PromptForDecorPackName(suggestedName);
                if (entered == null)
                    return false;
                packKey = ProjectLibraryService.CreateGeneratedDecorPackKey();
                packName = entered.Trim();
                return true;
            }

            packKey = choice.PackKey ?? "";
            packName = string.IsNullOrWhiteSpace(choice.PackName) ? "Wallpaper Pack" : choice.PackName.Trim();
            return true;
        }

        private string PromptForDecorPackName(string currentValue)
        {
            using (Form dialog = new Form())
            {
                dialog.Text = "New Décor Pack";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.ClientSize = new Size(420, 146);
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.BackColor = TwoPointTheme.ContentBackground;
                dialog.ForeColor = TwoPointTheme.BodyText;
                dialog.Font = TwoPointTheme.BodyFont(9F);

                Label prompt = new Label();
                prompt.AutoSize = false;
                prompt.Location = new Point(18, 18);
                prompt.Size = new Size(384, 34);
                prompt.Text = "Enter a name for the new Décor Pack. It will be used in My Mods and for the combined build.";
                prompt.ForeColor = TwoPointTheme.BodyText;
                prompt.Font = TwoPointTheme.BodyFont(8.8F);
                dialog.Controls.Add(prompt);

                TextBox box = new TextBox();
                box.Location = new Point(21, 60);
                box.Size = new Size(378, 24);
                box.Text = string.IsNullOrWhiteSpace(currentValue) ? "Wallpaper Pack" : currentValue.Trim();
                box.BackColor = TwoPointTheme.FieldBackground;
                box.ForeColor = TwoPointTheme.BodyText;
                box.Font = TwoPointTheme.BodyFont(9F);
                dialog.Controls.Add(box);

                Label validation = new Label();
                validation.AutoSize = false;
                validation.Location = new Point(21, 88);
                validation.Size = new Size(378, 18);
                validation.ForeColor = Color.FromArgb(166, 66, 66);
                validation.Font = TwoPointTheme.BodyFont(8F);
                dialog.Controls.Add(validation);

                Button ok = new Button();
                ok.Text = "OK";
                ok.Size = new Size(88, 30);
                ok.Location = new Point(221, 108);
                TwoPointTheme.StyleButton(ok);
                dialog.Controls.Add(ok);

                Button cancel = new Button();
                cancel.Text = "Cancel";
                cancel.Size = new Size(88, 30);
                cancel.Location = new Point(315, 108);
                TwoPointTheme.StyleButton(cancel);
                dialog.Controls.Add(cancel);

                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;
                cancel.DialogResult = DialogResult.Cancel;

                ok.Click += delegate
                {
                    string value = (box.Text ?? "").Trim();
                    if (string.IsNullOrEmpty(value))
                    {
                        validation.Text = "Enter a Décor Pack name.";
                        box.Focus();
                        return;
                    }
                    dialog.DialogResult = DialogResult.OK;
                    dialog.Close();
                };

                dialog.Shown += delegate
                {
                    box.Focus();
                    box.SelectAll();
                };

                return dialog.ShowDialog(this) == DialogResult.OK ? (box.Text ?? "").Trim() : null;
            }
        }

        private void ApplyVariantRulesToDetails()
        {
            if (IsWallpaperRecord(_selectedRecord))
            {
                ApplyWallpaperDecorPackRules();
                return;
            }

            RemoveDuplicateKudoshNote();
            bool wallpaper = IsWallpaperRecord(_selectedRecord);
            VariantParentChoice choice = GetSelectedVariantChoice();
            string mode = wallpaper ? VariantModes.Standalone : (choice == null ? VariantModes.Standalone : VariantModes.Normalise(choice.Mode));
            bool isVariant = mode != VariantModes.Standalone;

            if (isVariant && _kudoshValue != null && _kudoshValue.Value != 0)
                _kudoshValue.Value = 0;
            if (_kudoshHolder != null)
                _kudoshHolder.Enabled = !isVariant && !wallpaper;
            else
            {
                if (_kudoshValue != null)
                    _kudoshValue.Enabled = !isVariant && !wallpaper;
                if (_kudoshSlider != null)
                    _kudoshSlider.Enabled = !isVariant && !wallpaper;
            }

            if (_variantHelpLabel == null || (_selectedRecord != null && _library.FindChildren(_selectedRecord.ModId).Count > 0))
                return;

            if (mode == VariantModes.BaseGame)
                _variantHelpLabel.Text = "Variant of the base-game item. Kudosh is fixed at 0.";
            else if (mode == VariantModes.Modded)
                _variantHelpLabel.Text = "Variant of this built Memento Maker mod. Kudosh is fixed at 0.";
            else
                _variantHelpLabel.Text = "Standalone item. It can be used as a parent for other variants.";
        }

        private TemplateDefinition FindTemplate(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;
            if (string.Equals(key, "Poster", StringComparison.OrdinalIgnoreCase))
                key = "Standard Poster";
            for (int i = 0; i < _templates.Count; i++)
            {
                TemplateDefinition template = _templates[i];
                if (template != null && string.Equals(template.Key, key, StringComparison.OrdinalIgnoreCase))
                    return template;
            }
            return null;
        }

        private TemplateDefinition FindTemplateByBaseArchetypeId(long baseArchetypeId)
        {
            if (baseArchetypeId == 0)
                return null;
            for (int i = 0; i < _templates.Count; i++)
            {
                TemplateDefinition template = _templates[i];
                if (template != null && template.BaseArchetypeId == baseArchetypeId)
                    return template;
            }
            return null;
        }

        private string GetItemTypeForTemplate(TemplateDefinition template)
        {
            if (template == null)
                return "";
            string key = template.Key ?? "";
            if (string.Equals(key, "Wallpaper", StringComparison.OrdinalIgnoreCase)) return "Décor";
            if (key.IndexOf("Poster", StringComparison.OrdinalIgnoreCase) >= 0) return "Poster";
            if (string.Equals(key, "Mural", StringComparison.OrdinalIgnoreCase)) return "Mural";
            if (key.IndexOf("Large", StringComparison.OrdinalIgnoreCase) >= 0 && key.IndexOf("Rug", StringComparison.OrdinalIgnoreCase) >= 0) return "Large Rug";
            if (key.IndexOf("Rug", StringComparison.OrdinalIgnoreCase) >= 0) return "Small Rug";
            if (key.IndexOf("Double", StringComparison.OrdinalIgnoreCase) >= 0 && key.IndexOf("Banner", StringComparison.OrdinalIgnoreCase) >= 0) return "Double Banner";
            if (key.IndexOf("Banner", StringComparison.OrdinalIgnoreCase) >= 0) return "Banner";
            if (key.IndexOf("Hanging Sign", StringComparison.OrdinalIgnoreCase) >= 0) return "Hanging Sign";
            if (key.IndexOf("Wall Sign", StringComparison.OrdinalIgnoreCase) >= 0) return "Wall Sign";
            return template.DisplayName ?? key;
        }

        private string GetTemplateForRecolourable(ModProjectRecord record, bool recolourable)
        {
            if (record == null)
                return "";

            string current = record.Template ?? "";
            if (current.IndexOf("Rug", StringComparison.OrdinalIgnoreCase) < 0)
                return current;

            string shape = GetShape(record);
            if (string.IsNullOrEmpty(shape))
                return current;

            bool large = string.Equals(GetItemType(record), "Large Rug", StringComparison.OrdinalIgnoreCase);
            string key = (large ? "Large " : "") + (recolourable ? "Staff " : "Marketing ") + shape + " Rug";
            foreach (TemplateDefinition template in _templates)
            {
                if (string.Equals(template.Key, key, StringComparison.OrdinalIgnoreCase))
                    return template.Key;
            }
            return current;
        }

        private void UpdateDetailState(ModProjectRecord record)
        {
            if (record == null)
            {
                _detailStateLabel.Text = "";
                return;
            }

            string localLine = "";
            string installed = GetInstalledStatus(record);
            if (string.Equals(installed, "Installed (old)", StringComparison.OrdinalIgnoreCase))
            {
                _detailStateLabel.ForeColor = Color.FromArgb(217, 145, 16);
                localLine = "\u2713 Installed (old) - rebuild before publishing the latest changes.";
            }
            else if (_detailsDirty)
            {
                _detailStateLabel.ForeColor = Color.FromArgb(217, 145, 16);
                localLine = "Unsaved changes.";
            }
            else if (string.Equals(record.WorkshopLinkState, "Missing", StringComparison.OrdinalIgnoreCase))
            {
                _detailStateLabel.ForeColor = Color.FromArgb(217, 145, 16);
                localLine = "\u26A0 Linked Steam Workshop item is missing.";
            }
            else
            {
                _detailStateLabel.ForeColor = string.Equals(GetWorkshopStatus(record), "Published (old)", StringComparison.OrdinalIgnoreCase)
                    ? Color.FromArgb(217, 145, 16)
                    : TwoPointTheme.BodyText;
            }

            string workshopLine = BuildWorkshopDetailLine(record);
            if (!string.IsNullOrEmpty(localLine) && !string.IsNullOrEmpty(workshopLine))
                _detailStateLabel.Text = localLine + Environment.NewLine + workshopLine;
            else
                _detailStateLabel.Text = localLine + workshopLine;
        }

        private string BuildWorkshopDetailLine(ModProjectRecord record)
        {
            if (record == null)
                return "";

            string status = GetWorkshopStatus(record);
            if (string.Equals(status, "Not Published", StringComparison.OrdinalIgnoreCase))
                return "Steam Workshop: Not Published";

            if (string.Equals(status, "Missing", StringComparison.OrdinalIgnoreCase))
            {
                string previousId = record.WorkshopPreviousPublishedFileId ?? "";
                return string.IsNullOrEmpty(previousId)
                    ? "Steam Workshop: Missing"
                    : "Steam Workshop: Missing  |  Previous ID " + previousId;
            }

            string workshopPackageMode = BuildPackageModes.Normalise(record.WorkshopPackageMode);
            string workshopPrefix = workshopPackageMode == BuildPackageModes.DecorPack
                ? "Steam Workshop Décor Pack: "
                : (workshopPackageMode == BuildPackageModes.Family ? "Steam Workshop Family: " : "Steam Workshop: ");
            string line = workshopPrefix + status;
            if (!string.IsNullOrEmpty(record.WorkshopPublishedFileId))
                line += "  |  ID " + record.WorkshopPublishedFileId;
            if (!string.IsNullOrEmpty(record.WorkshopVisibility))
            {
                string visibility = string.Equals(record.WorkshopVisibility, "Private", StringComparison.OrdinalIgnoreCase)
                    ? "Hidden"
                    : record.WorkshopVisibility;
                line += "  |  " + visibility;
            }

            DateTime updated = ParseDate(record.WorkshopLastUpdatedUtc);
            if (record.WorkshopLinkNeedsUpdate)
                line += "  |  Relinked - update required";
            else if (updated != DateTime.MinValue)
                line += "  |  Updated " + updated.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

            return line;
        }

        private void MarkDetailsDirty()
        {
            if (_loadingDetails || _selectedRecord == null)
                return;
            _detailsDirty = true;
            UpdateDetailState(_selectedRecord);
            UpdateButtons();
        }

        // Double Banner My Mods preview/editing uses display-side semantics so the
        // visible Left side matches the in-game left banner, while the stored
        // project artwork order remains unchanged for texture baking.
        private bool IsDisplayRightSideSelected()
        {
            return _editRightDoubleBanner;
        }

        private string GetPendingDisplayArtworkPath(ModProjectRecord record)
        {
            bool swapped = IsDoubleBannerRecord(record);
            bool primarySlot = swapped ? IsDisplayRightSideSelected() : !IsDisplayRightSideSelected();
            if (primarySlot)
                return !string.IsNullOrEmpty(_pendingArtworkPath) ? _pendingArtworkPath : ResolveArtworkPath(record);
            return !string.IsNullOrEmpty(_pendingSecondaryArtworkPath) ? _pendingSecondaryArtworkPath : ResolveSecondaryArtworkPath(record);
        }

        private void ChangeArtwork()
        {
            if (_selectedRecord == null || GetSingleSelectedRecord() == null)
                return;

            bool dualArtwork = IsDualArtworkRecord(_selectedRecord);
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Image files|*.png;*.jpg;*.jpeg|PNG files|*.png|JPEG files|*.jpg;*.jpeg";
                string sideName = IsTwoSidedSignRecord(_selectedRecord)
                    ? (_editRightDoubleBanner ? "Back" : "Front")
                    : (_editRightDoubleBanner ? "Right Banner" : "Left Banner");
                dialog.Title = dualArtwork
                    ? "Choose new " + sideName + " artwork / image"
                    : "Choose new artwork / image";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                if (dualArtwork)
                {
                    bool swapped = IsDoubleBannerRecord(_selectedRecord);
                    bool primarySlot = swapped ? _editRightDoubleBanner : !_editRightDoubleBanner;
                    if (primarySlot)
                        _pendingArtworkPath = dialog.FileName;
                    else
                        _pendingSecondaryArtworkPath = dialog.FileName;
                }
                else
                {
                    _pendingArtworkPath = dialog.FileName;
                }
                _detailsDirty = true;
                SetPreviewImage(LoadDetailPreview(_selectedRecord));
                UpdateDetailState(_selectedRecord);
                UpdateButtons();
            }
        }

        private void SetMyModsDoubleBannerSide(bool editRight)
        {
            if (_selectedRecord == null || !IsDualArtworkRecord(_selectedRecord))
                return;
            _editRightDoubleBanner = editRight;
            StyleMyModsDoubleBannerSideButtons();
            SetPreviewImage(LoadDetailPreview(_selectedRecord));
        }

        private void StyleMyModsDoubleBannerSideButtons()
        {
            if (_doubleBannerLeftButton != null)
                TwoPointTheme.StyleSegmentButton(_doubleBannerLeftButton, !_editRightDoubleBanner);
            if (_doubleBannerRightButton != null)
                TwoPointTheme.StyleSegmentButton(_doubleBannerRightButton, _editRightDoubleBanner);
        }

        private void SetMyModsDoubleBannerSideSelectorVisible(bool visible)
        {
            bool twoSidedSign = visible && IsTwoSidedSignRecord(_selectedRecord);
            if (_doubleBannerSideLabel != null)
            {
                _doubleBannerSideLabel.Text = twoSidedSign ? "Editing Face" : "Editing Side";
                _doubleBannerSideLabel.Visible = visible;
            }
            if (_doubleBannerLeftButton != null)
            {
                _doubleBannerLeftButton.Text = twoSidedSign ? "Front" : "Left";
                _doubleBannerLeftButton.Visible = visible;
            }
            if (_doubleBannerRightButton != null)
            {
                _doubleBannerRightButton.Text = twoSidedSign ? "Back" : "Right";
                _doubleBannerRightButton.Visible = visible;
            }
            if (visible) StyleMyModsDoubleBannerSideButtons();
        }

        private static bool IsHangingSignRecord(ModProjectRecord record)
        {
            string key = record == null ? "" : (record.Template ?? "");
            return key.EndsWith(" Hanging Sign", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWallSignRecord(ModProjectRecord record)
        {
            string key = record == null ? "" : (record.Template ?? "");
            return key.EndsWith(" Wall Sign", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTwoSidedSignRecord(ModProjectRecord record)
        {
            return IsHangingSignRecord(record) || IsWallSignRecord(record);
        }

        private static bool IsDualArtworkRecord(ModProjectRecord record)
        {
            return IsDoubleBannerRecord(record) || IsTwoSidedSignRecord(record);
        }

        private static bool IsDoubleBannerRecord(ModProjectRecord record)
        {
            string key = record == null ? "" : (record.Template ?? "");
            return key.EndsWith(" Double Banner", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRugRecord(ModProjectRecord record)
        {
            string key = record == null ? "" : (record.Template ?? "");
            return key.IndexOf("Rug", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void SaveChanges()
        {
            if (_selectedRecord == null)
                return;
            if (!_detailsDirty)
            {
                UpdateDetailState(_selectedRecord);
                return;
            }

            string name = (_nameBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(name))
            {
                TwoPointTheme.ShowMessage(this, "Please enter a mod name before saving changes.", "Mod Name Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string id = _selectedRecord.ModId;
                int itemCost = _costValue == null ? _selectedRecord.ItemCost : Decimal.ToInt32(_costValue.Value);
                int kudoshCost = _kudoshValue == null ? _selectedRecord.KudoshCost : Decimal.ToInt32(_kudoshValue.Value);
                string templateKey = GetTemplateForRecolourable(_selectedRecord, _recolourableToggle != null && _recolourableToggle.Checked);

                if (IsWallpaperRecord(_selectedRecord))
                {
                    string targetPackKey;
                    string targetPackName;
                    if (!TryResolveWallpaperDecorPackSelection(_selectedRecord, out targetPackKey, out targetPackName))
                        return;

                    string currentPackKey = BuildPackageModes.Normalise(_selectedRecord.LastBuildPackageMode) == BuildPackageModes.DecorPack
                        ? (_selectedRecord.LastBuiltFamilyKey ?? "")
                        : "";
                    string currentPackName = string.IsNullOrWhiteSpace(_selectedRecord.LastBuiltFamilyName)
                        ? "Wallpaper Pack"
                        : _selectedRecord.LastBuiltFamilyName.Trim();
                    bool membershipChanged = !string.Equals(currentPackKey, targetPackKey ?? "", StringComparison.Ordinal);
                    if (membershipChanged)
                    {
                        string message;
                        string title = "Update Décor Pack Membership";
                        if (string.IsNullOrEmpty(targetPackKey))
                        {
                            message = "Save this wallpaper as Standalone?\n\n" +
                                "It will be removed from its current Décor Pack and the affected wallpapers will be marked Installed (old) until rebuilt.";
                        }
                        else if (string.IsNullOrEmpty(currentPackKey))
                        {
                            message = "Add this wallpaper to the Décor Pack '" + targetPackName + "'?\n\n" +
                                "The affected wallpapers will be marked Installed (old) until the pack is rebuilt.";
                        }
                        else
                        {
                            message = "Move this wallpaper from '" + currentPackName + "' to '" + targetPackName + "'?\n\n" +
                                "Both affected Décor Packs will be marked Installed (old) until rebuilt.";
                        }

                        if (TwoPointTheme.ShowMessage(this, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                            return;
                    }

                    _selectedRecord = _library.SaveChanges(_selectedRecord, name, _descriptionBox.Text ?? "", _pendingArtworkPath,
                        itemCost, kudoshCost, templateKey, VariantModes.Standalone, "", _pendingSecondaryArtworkPath);
                    _library.SaveDecorPackMembership(_selectedRecord, targetPackKey, targetPackName);
                }
                else
                {
                    VariantParentChoice variantChoice = GetSelectedVariantChoice();
                    string variantMode = variantChoice == null ? VariantModes.Standalone : VariantModes.Normalise(variantChoice.Mode);
                    string variantParentModId = variantChoice == null ? "" : (variantChoice.ModId ?? "");
                    _selectedRecord = _library.SaveChanges(_selectedRecord, name, _descriptionBox.Text ?? "", _pendingArtworkPath,
                        itemCost, kudoshCost, templateKey, variantMode, variantParentModId, _pendingSecondaryArtworkPath);
                }

                _detailsDirty = false;
                _pendingArtworkPath = null;
                _pendingSecondaryArtworkPath = null;
                LoadProjects(id);
            }
            catch (Exception ex)
            {
                TwoPointTheme.ShowMessage(this, ex.Message, "Save Changes Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void QueueRebuild()
        {
            List<ModProjectRecord> selected = SelectedProjects();
            VariantFamilyGroup baseHeaderFamily = selected.Count == 0 ? GetSelectedBaseGameFamilyHeader() : null;
            if (selected.Count == 0 && baseHeaderFamily == null)
                return;

            if (_detailsDirty)
            {
                DialogResult save = TwoPointTheme.ShowMessage(this,
                    "The current mod has changes that have not been saved yet.\n\n" +
                    "Save them before rebuilding? Choosing Don't Save will rebuild the last saved version instead.",
                    "Unsaved Changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (save == DialogResult.Cancel)
                    return;
                if (save == DialogResult.Yes)
                    SaveChanges();
            }

            selected = SelectedProjects();

            // Wallpaper RoomVisuals are not variants, but the SDK can export several of
            // them into one package. Multi-selecting Wallpapers therefore uses a Decor Pack
            // build instead of the normal per-mod rebuild queue.
            if (selected.Count > 1 && AllWallpaperRecords(selected))
            {
                DialogResult packChoice = TwoPointTheme.ShowMessage(this,
                    "Build " + selected.Count + " selected wallpapers as one Decor Pack?\n\n" +
                    "They will install together in a single mod folder.",
                    "Rebuild Decor Pack", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (packChoice != DialogResult.Yes)
                    return;

                Selection.Action = "RebuildDecorPack";
                Selection.Projects = selected;
                Selection.Project = selected[0];
                Selection.FamilyKey = null;
                Selection.FamilyName = "Wallpaper Pack";
                RaiseSelectionRequested();
                return;
            }

            if (selected.Count == 1 && selected[0] != null &&
                BuildPackageModes.Normalise(selected[0].LastBuildPackageMode) == BuildPackageModes.DecorPack)
            {
                List<ModProjectRecord> packMembers = GetDecorPackMembers(selected[0]);
                if (packMembers.Count == 0)
                    packMembers.Add(selected[0]);

                string packName = string.IsNullOrWhiteSpace(selected[0].LastBuiltFamilyName)
                    ? "Wallpaper Pack"
                    : selected[0].LastBuiltFamilyName;
                string question = packMembers.Count > 1
                    ? "Rebuild the complete Decor Pack with " + packMembers.Count + " wallpapers?\n\n" +
                      "Choose No to rebuild only '" + (selected[0].Name ?? "this wallpaper") + "' as a standalone wallpaper."
                    : "Rebuild '" + (selected[0].Name ?? "this wallpaper") + "' as the Décor Pack '" + packName + "'?\n\n" +
                      "Choose No to rebuild only this wallpaper as a standalone wallpaper.";
                DialogResult packChoice = TwoPointTheme.ShowMessage(this,
                    question,
                    "Rebuild Decor Pack", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (packChoice == DialogResult.Cancel)
                    return;
                if (packChoice == DialogResult.Yes)
                {
                    Selection.Action = "RebuildDecorPack";
                    Selection.Projects = packMembers;
                    Selection.Project = selected[0];
                    Selection.FamilyKey = selected[0].LastBuiltFamilyKey ?? "";
                    Selection.FamilyName = packName;
                    RaiseSelectionRequested();
                    return;
                }
            }

            VariantFamilyGroup family = baseHeaderFamily;
            if (family == null && selected.Count == 1)
                TryGetFamily(selected[0], out family);

            if (family != null && (selected.Count <= 1 || baseHeaderFamily != null))
            {
                List<ModProjectRecord> members = GetFamilyMembers(family);
                if (members.Count > 0)
                {
                    bool alreadyCombined = false;
                    for (int i = 0; i < members.Count; i++)
                    {
                        ModProjectRecord member = members[i];
                        if (member != null && BuildPackageModes.Normalise(member.LastBuildPackageMode) == BuildPackageModes.Family &&
                            string.Equals(member.LastBuiltFamilyKey ?? "", family.Key ?? "", StringComparison.Ordinal))
                        {
                            alreadyCombined = true;
                            break;
                        }
                    }

                    DialogResult familyChoice;
                    if (baseHeaderFamily != null || alreadyCombined)
                    {
                        familyChoice = TwoPointTheme.ShowMessage(this,
                            "Rebuild all " + members.Count + " members of '" + (family.ParentName ?? "Variant") + "' as one combined mod?",
                            "Rebuild Family", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (familyChoice != DialogResult.Yes)
                            return;
                    }
                    else
                    {
                        familyChoice = TwoPointTheme.ShowMessage(this,
                            "Rebuild all " + members.Count + " members of '" + (family.ParentName ?? "Variant") + "' together?\n\n" +
                            "Choose No to rebuild only the selected item.",
                            "Rebuild Family", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                        if (familyChoice == DialogResult.Cancel)
                            return;
                        if (familyChoice == DialogResult.No)
                        {
                            Selection.Action = "RebuildQueue";
                            Selection.Projects = selected;
                            Selection.Project = selected.Count == 1 ? selected[0] : null;
                            Selection.FamilyKey = null;
                            Selection.FamilyName = null;
                            RaiseSelectionRequested();
                            return;
                        }
                    }

                    Selection.Action = "RebuildFamily";
                    Selection.Projects = members;
                    Selection.Project = family.ParentRecord ?? (members.Count > 0 ? members[0] : null);
                    Selection.FamilyKey = family.Key ?? "";
                    Selection.FamilyName = family.ParentName ?? "Variant Family";
                    RaiseSelectionRequested();
                    return;
                }
            }

            if (selected.Count == 1 && selected[0] != null &&
                BuildPackageModes.Normalise(selected[0].LastBuildPackageMode) == BuildPackageModes.Family)
            {
                DialogResult split = TwoPointTheme.ShowMessage(this,
                    "Rebuild this item separately from its combined family?\n\nOther family members will need their family rebuilt.",
                    "Split Family Package", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (split != DialogResult.Yes)
                    return;
            }
            else if (selected.Count == 1 && selected[0] != null &&
                BuildPackageModes.Normalise(selected[0].LastBuildPackageMode) == BuildPackageModes.DecorPack)
            {
                DialogResult split = TwoPointTheme.ShowMessage(this,
                    "Rebuild this wallpaper as a standalone mod?\n\nThe other wallpapers in its Decor Pack will no longer share this installed package.",
                    "Split Decor Pack", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (split != DialogResult.Yes)
                    return;
            }

            Selection.Action = "RebuildQueue";
            Selection.Projects = selected;
            Selection.Project = selected.Count == 1 ? selected[0] : null;
            Selection.FamilyKey = null;
            Selection.FamilyName = null;
            RaiseSelectionRequested();
        }

        private void RequestSelectionAction(string action)
        {
            ModProjectRecord record = SelectedProject();
            if (record == null)
                return;

            ModProjectRecord saved = _library.Load(record.ModId) ?? record;
            VariantFamilyGroup family = null;
            string buildMode = BuildPackageModes.Normalise(saved.LastBuildPackageMode);
            bool combinedPackage = BuildPackageModes.IsCombined(buildMode) &&
                TryGetFamily(saved, out family) && family != null &&
                string.Equals(saved.LastBuiltFamilyKey ?? "", family.Key ?? "", StringComparison.Ordinal);

            // Workshop identity is shared across every member of a combined Family / Décor Pack.
            // Resolve Workshop actions from the saved Workshop family identity first, rather than
            // treating another member carrying the same PublishedFileId as an unrelated duplicate.
            List<ModProjectRecord> workshopFamilyMembers = null;
            string workshopFamilyKey = null;
            string workshopFamilyName = null;
            string workshopMode = BuildPackageModes.Normalise(saved.WorkshopPackageMode);
            bool workshopAction = !string.IsNullOrEmpty(action) &&
                action.StartsWith("Workshop", StringComparison.OrdinalIgnoreCase);
            if (workshopAction && BuildPackageModes.IsCombined(workshopMode) && !string.IsNullOrEmpty(saved.WorkshopFamilyKey))
            {
                VariantFamilyGroup workshopFamily = null;
                if (TryGetFamily(saved, out workshopFamily) && workshopFamily != null &&
                    string.Equals(saved.WorkshopFamilyKey ?? "", workshopFamily.Key ?? "", StringComparison.Ordinal))
                {
                    workshopFamilyMembers = GetFamilyMembers(workshopFamily);
                    workshopFamilyKey = workshopFamily.Key ?? saved.WorkshopFamilyKey ?? "";
                    workshopFamilyName = workshopFamily.ParentName ?? saved.WorkshopFamilyName ??
                        (workshopMode == BuildPackageModes.DecorPack ? "Wallpaper Pack" : "Variant Family");
                }
                else
                {
                    workshopFamilyMembers = GetSavedWorkshopFamilyMembers(saved);
                    workshopFamilyKey = saved.WorkshopFamilyKey ?? "";
                    workshopFamilyName = string.IsNullOrWhiteSpace(saved.WorkshopFamilyName)
                        ? (workshopMode == BuildPackageModes.DecorPack ? "Wallpaper Pack" : "Variant Family")
                        : saved.WorkshopFamilyName;
                }
            }

            Selection.Action = action;
            Selection.Project = saved;
            Selection.PackageMode = BuildPackageModes.Single;
            if (workshopFamilyMembers != null && workshopFamilyMembers.Count > 0)
            {
                Selection.Projects = workshopFamilyMembers;
                Selection.FamilyKey = workshopFamilyKey;
                Selection.FamilyName = workshopFamilyName;
                Selection.PackageMode = workshopMode;
            }
            else if (combinedPackage)
            {
                Selection.Projects = GetFamilyMembers(family);
                Selection.FamilyKey = family.Key ?? "";
                Selection.FamilyName = family.ParentName ?? (buildMode == BuildPackageModes.DecorPack ? "Wallpaper Pack" : "Variant Family");
                Selection.PackageMode = buildMode;
            }
            else
            {
                Selection.Projects = null;
                Selection.FamilyKey = null;
                Selection.FamilyName = null;
            }
            RaiseSelectionRequested();
        }

        private void Choose(string action)
        {
            ModProjectRecord record = SelectedProject();
            if (record == null)
                return;

            if (_detailsDirty)
            {
                DialogResult result = TwoPointTheme.ShowMessage(this,
                    "The current mod has changes that have not been saved yet.\n\n" +
                    "Save them before opening the full editor? Choosing Don't Save will open the last saved version.",
                    "Unsaved Changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (result == DialogResult.Cancel)
                    return;
                if (result == DialogResult.Yes)
                    SaveChanges();
                record = _library.Load(record.ModId) ?? record;
            }

            // The main Workshop button uses the same family-aware selection path as Workshop Tools.
            // Every member intentionally mirrors the shared family Workshop ID.
            if (string.Equals(action, "Workshop", StringComparison.OrdinalIgnoreCase))
            {
                RequestSelectionAction(action);
                return;
            }

            Selection.Action = action;
            Selection.Project = record;
            Selection.Projects = null;
            Selection.FamilyKey = null;
            Selection.FamilyName = null;
            RaiseSelectionRequested();
        }

        private void RaiseSelectionRequested()
        {
            if (_embeddedMode)
            {
                if (SelectionRequested != null)
                    SelectionRequested(this, EventArgs.Empty);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        private void OpenInstalled()
        {
            ModProjectRecord record = SelectedProject();
            if (record != null && !string.IsNullOrEmpty(record.LastBuiltOutputPath) && Directory.Exists(record.LastBuiltOutputPath))
                Process.Start("explorer.exe", "\"" + record.LastBuiltOutputPath + "\"");
        }

        private void DeleteSelected()
        {
            List<ModProjectRecord> selected = SelectedProjects();
            if (selected.Count == 0)
            {
                ModProjectRecord fallback = SelectedProject();
                if (fallback != null)
                    selected.Add(fallback);
            }
            if (selected.Count == 0)
                return;

            if (selected.Count == 1)
            {
                ModProjectRecord record = selected[0];
                List<ModProjectRecord> children = _library.FindChildren(record.ModId);
                string message =
                    "Delete the saved project record for '" + record.Name + "'?\n\n" +
                    "This removes the project record and its saved source images from Memento Maker.\n" +
                    "It does NOT delete the currently installed mod from Two Point Museum.";

                if (children.Count > 0)
                {
                    message += "\n\nThis mod is the parent of " + children.Count + " variant" + (children.Count == 1 ? "" : "s") + ":";
                    int shown = Math.Min(children.Count, 6);
                    for (int i = 0; i < shown; i++)
                        message += "\n- " + (string.IsNullOrEmpty(children[i].Name) ? children[i].ModId : children[i].Name);
                    if (children.Count > shown)
                        message += "\n- ...and " + (children.Count - shown) + " more";

                    message += "\n\nIf you continue, these child projects will be converted to Standalone Item / Parent Item and marked as needing a rebuild. " +
                        "Their already-built game files are not rewritten silently. If a child is published, update it on Steam after rebuilding so its old Required Item relationship is removed.";
                }

                DialogResult result = TwoPointTheme.ShowMessage(this,
                    message,
                    children.Count > 0 ? "Delete Parent Mod" : "Delete Mod",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                    return;

                try
                {
                    List<ModProjectRecord> converted = _library.DeleteRecordAndConvertChildren(record);
                    _selectedRecord = null;
                    _detailsDirty = false;
                    LoadProjects(null);

                    if (converted.Count > 0)
                    {
                        TwoPointTheme.ShowMessage(this,
                            "The parent project was deleted. " + converted.Count + " child variant" + (converted.Count == 1 ? " was" : "s were") +
                            " converted to Standalone and marked Installed (old). Rebuild " + (converted.Count == 1 ? "it" : "them") +
                            " to apply the change to the installed mod files.",
                            "Child Variants Converted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    TwoPointTheme.ShowMessage(this, ex.Message, "Delete Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }

            HashSet<string> selectedIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i] != null && !string.IsNullOrEmpty(selected[i].ModId))
                    selectedIds.Add(selected[i].ModId);
            }

            List<ModProjectRecord> affectedChildren = new List<ModProjectRecord>();
            HashSet<string> affectedChildIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < selected.Count; i++)
            {
                ModProjectRecord parent = selected[i];
                if (parent == null || string.IsNullOrEmpty(parent.ModId))
                    continue;
                List<ModProjectRecord> children = _library.FindChildren(parent.ModId);
                for (int c = 0; c < children.Count; c++)
                {
                    ModProjectRecord child = children[c];
                    if (child == null || string.IsNullOrEmpty(child.ModId) || selectedIds.Contains(child.ModId))
                        continue;
                    if (affectedChildIds.Add(child.ModId))
                        affectedChildren.Add(child);
                }
            }

            string bulkMessage =
                "Delete the " + selected.Count + " selected saved project records?\n\n" +
                "This removes their project records and saved source images from Memento Maker.\n" +
                "It does NOT delete the currently installed mods from Two Point Museum.";

            int selectedShown = Math.Min(selected.Count, 8);
            bulkMessage += "\n\nSelected mods:";
            for (int i = 0; i < selectedShown; i++)
                bulkMessage += "\n- " + (string.IsNullOrEmpty(selected[i].Name) ? selected[i].ModId : selected[i].Name);
            if (selected.Count > selectedShown)
                bulkMessage += "\n- ...and " + (selected.Count - selectedShown) + " more";

            if (affectedChildren.Count > 0)
            {
                bulkMessage += "\n\n" + affectedChildren.Count + " child variant" + (affectedChildren.Count == 1 ? " is" : "s are") +
                    " linked to parent mods in this selection but are not themselves selected for deletion." +
                    " If you continue, " + (affectedChildren.Count == 1 ? "it will" : "they will") +
                    " be converted to Standalone and marked as needing a rebuild.";
            }

            DialogResult bulkResult = TwoPointTheme.ShowMessage(this,
                bulkMessage,
                "Delete " + selected.Count + " Mods",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (bulkResult != DialogResult.Yes)
                return;

            try
            {
                List<ModProjectRecord> converted = _library.DeleteRecordsAndConvertExternalChildren(selected);
                _selectedRecord = null;
                _detailsDirty = false;
                LoadProjects(null);

                if (converted.Count > 0)
                {
                    TwoPointTheme.ShowMessage(this,
                        selected.Count + " projects were deleted. " + converted.Count + " unselected child variant" +
                        (converted.Count == 1 ? " was" : "s were") +
                        " converted to Standalone and marked Installed (old). Rebuild " +
                        (converted.Count == 1 ? "it" : "them") + " to apply the change to the installed mod files.",
                        "Child Variants Converted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                TwoPointTheme.ShowMessage(this, ex.Message, "Bulk Delete Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private ModProjectRecord SelectedProject()
        {
            return GetSingleSelectedRecord();
        }

        private List<ModProjectRecord> SelectedProjects()
        {
            List<ModProjectRecord> selected = new List<ModProjectRecord>();
            foreach (DataGridViewRow row in _grid.SelectedRows)
            {
                ModProjectRecord record = row.Tag as ModProjectRecord;
                if (record != null)
                    selected.Add(record);
            }
            return selected;
        }

        private void UpdateButtons()
        {
            int count = SelectedProjects().Count;
            ModProjectRecord record = GetSingleSelectedRecord();
            if (count == 0 && record != null)
                count = 1;
            VariantFamilyGroup selectedBaseGameFamily = count == 0 ? GetSelectedBaseGameFamilyHeader() : null;
            VariantFamilyGroup selectedFamily = null;
            if (record != null)
                TryGetFamily(record, out selectedFamily);

            _editButton.Visible = count <= 1 && selectedBaseGameFamily == null;
            _editButton.Enabled = count == 1 && record != null && !_batchBusy;
            _rebuildButton.Enabled = (count > 0 || selectedBaseGameFamily != null) && !_batchBusy;
            List<ModProjectRecord> selectedForButtons = SelectedProjects();
            bool multiWallpaperPack = selectedForButtons.Count > 1 && AllWallpaperRecords(selectedForButtons);
            bool selectedDecorPack = record != null && BuildPackageModes.Normalise(record.LastBuildPackageMode) == BuildPackageModes.DecorPack;
            if (multiWallpaperPack || (count == 1 && selectedDecorPack))
                _rebuildButton.Text = "Rebuild Decor Pack";
            else if (count > 1)
                _rebuildButton.Text = "Rebuild " + count + " Mods";
            else if (selectedBaseGameFamily != null || selectedFamily != null)
                _rebuildButton.Text = "Rebuild Family";
            else
                _rebuildButton.Text = "Rebuild Mod";

            // Derive action availability from the same installed-state calculation used by the grid
            // so the action bar cannot drift out of sync with the Installed column.
            string selectedInstalledStatus = record == null ? "Not Installed" : GetInstalledStatus(record);
            bool selectedModInstalled = !string.Equals(selectedInstalledStatus, "Not Installed", StringComparison.OrdinalIgnoreCase);
            bool selectedModCurrent = string.Equals(selectedInstalledStatus, "Installed", StringComparison.OrdinalIgnoreCase);

            // Hide Open Installed when there is no installed output rather than leaving an inactive action.
            _openButton.Visible = count == 1 && record != null && selectedModInstalled;
            _openButton.Enabled = _openButton.Visible && !_batchBusy;

            // Workshop publishing/updating is only valid for the CURRENT installed build.
            // If the grid says Installed (old), the local output is stale and must be rebuilt
            // before anything can be sent to Steam. This applies to both Publish and Update,
            // regardless of the current Workshop status. The Workshop column itself is unchanged.
            bool hasWorkshopLink = record != null && !string.IsNullOrEmpty(record.WorkshopPublishedFileId);
            bool workshopLinkMissing = record != null && string.Equals(record.WorkshopLinkState, "Missing", StringComparison.OrdinalIgnoreCase);
            bool combinedFamilyPackage = record != null && BuildPackageModes.IsCombined(record.LastBuildPackageMode);
            bool familyWorkshopLink = false;
            bool familyWorkshopMissing = false;
            if (combinedFamilyPackage && selectedFamily != null)
            {
                WorkshopFamilyState familyWorkshopState = _library.LoadWorkshopFamilyState(selectedFamily.Key);
                if (familyWorkshopState != null)
                {
                    familyWorkshopLink = !string.IsNullOrEmpty(familyWorkshopState.PublishedFileId) &&
                        !string.Equals(familyWorkshopState.LinkState, "Missing", StringComparison.OrdinalIgnoreCase);
                    familyWorkshopMissing = string.Equals(familyWorkshopState.LinkState, "Missing", StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    List<ModProjectRecord> familyMembersForWorkshop = GetFamilyMembers(selectedFamily);
                    for (int i = 0; i < familyMembersForWorkshop.Count; i++)
                    {
                        ModProjectRecord familyMember = familyMembersForWorkshop[i];
                        if (familyMember == null || BuildPackageModes.Normalise(familyMember.WorkshopPackageMode) != BuildPackageModes.Normalise(record.LastBuildPackageMode) ||
                            !string.Equals(familyMember.WorkshopFamilyKey ?? "", selectedFamily.Key ?? "", StringComparison.Ordinal))
                            continue;
                        if (!string.IsNullOrEmpty(familyMember.WorkshopPublishedFileId) &&
                            !string.Equals(familyMember.WorkshopLinkState, "Missing", StringComparison.OrdinalIgnoreCase))
                            familyWorkshopLink = true;
                        if (string.Equals(familyMember.WorkshopLinkState, "Missing", StringComparison.OrdinalIgnoreCase))
                            familyWorkshopMissing = true;
                    }
                }
            }

            _workshopButton.Visible = count == 1 && record != null && selectedModCurrent;
            _workshopButton.Enabled = _workshopButton.Visible && !_batchBusy && !record.PendingChanges;
            // Keep the primary Workshop action wording consistent regardless of whether
            // the selected project is standalone or part of a combined family package.
            // Missing/stale links have no valid active target, so their primary action remains
            // Publish to Workshop; Refresh/Relink/Unlink stay available under Workshop Tools.
            bool activeWorkshopLink = combinedFamilyPackage ? familyWorkshopLink : (hasWorkshopLink && !workshopLinkMissing);
            _workshopButton.Text = activeWorkshopLink ? "Update Workshop" : "Publish to Workshop";
            FitWorkshopActionButtonText();

            // Combined family packages now have one shared Workshop identity. Workshop Tools
            // operate on that family identity rather than on an individual child link.
            _workshopToolsButton.Visible = count == 1 && record != null;
            _workshopToolsButton.Enabled = _workshopToolsButton.Visible && !_batchBusy;
            if (_openWorkshopPageMenuItem != null)
                _openWorkshopPageMenuItem.Enabled = combinedFamilyPackage
                    ? familyWorkshopLink
                    : (hasWorkshopLink && !workshopLinkMissing);
            if (_refreshWorkshopStatusMenuItem != null)
                _refreshWorkshopStatusMenuItem.Enabled = count == 1 && record != null && !_batchBusy;
            if (_relinkWorkshopMenuItem != null)
                _relinkWorkshopMenuItem.Enabled = count == 1 && record != null && !_batchBusy;
            if (_unlinkWorkshopMenuItem != null)
                _unlinkWorkshopMenuItem.Enabled = count == 1 && record != null && !_batchBusy &&
                    (combinedFamilyPackage
                        ? (familyWorkshopLink || familyWorkshopMissing)
                        : (hasWorkshopLink || workshopLinkMissing));

            _saveButton.Visible = count <= 1;
            _saveButton.Enabled = count == 1 && record != null && !_batchBusy;
            _saveButton.ForeColor = Color.White;

            if (_manageFamilyButton != null)
            {
                bool canManageFamily = count == 1 && record != null && !IsWallpaperRecord(record) &&
                    (selectedFamily == null || !selectedFamily.DecorPack);
                _manageFamilyButton.Visible = canManageFamily;
                _manageFamilyButton.Enabled = canManageFamily && !_batchBusy;
            }

            _deleteButton.Visible = count > 0;
            _deleteButton.Enabled = count > 0 && !_batchBusy;
            _deleteButton.Text = count > 1 ? "Delete " + count + " Mods" : "Delete Mod";
            _changeArtworkButton.Enabled = count == 1 && !_batchBusy;
            _nameBox.ReadOnly = count != 1 || _batchBusy;
            _descriptionBox.ReadOnly = count != 1 || _batchBusy;
        }

        private void ReselectRecordPreservingEdits(string modId)
        {
            if (string.IsNullOrEmpty(modId) || _grid == null)
                return;

            _suppressSelectionEvents = true;
            try
            {
                _grid.ClearSelection();
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    ModProjectRecord record = row.Tag as ModProjectRecord;
                    if (record == null || !string.Equals(record.ModId, modId, StringComparison.Ordinal))
                        continue;

                    if (row.Visible)
                    {
                        row.Selected = true;
                        if (row.Cells.Count > 0)
                            _grid.CurrentCell = row.Cells["ModName"];
                    }
                    else
                    {
                        // A save can move a mod into a family that is currently collapsed.
                        // Never point DataGridView.CurrentCell at the newly hidden child row.
                        // Keep the family collapsed and move selection to its visible header.
                        SelectVisibleFamilyParentRow(GetFamilyForRow(row));
                    }
                    break;
                }
            }
            finally
            {
                _suppressSelectionEvents = false;
            }

            // Deliberately do not call LoadDetails here. The text boxes, artwork
            // selection, sliders and toggles still contain the user's unsaved edits.
            UpdateButtons();
        }

        private bool SelectVisibleFamilyParentRow(VariantFamilyGroup family)
        {
            if (family == null || _grid == null)
                return false;

            foreach (DataGridViewRow candidate in _grid.Rows)
            {
                VariantFamilyGroup candidateFamily = GetFamilyForRow(candidate);
                if (candidateFamily == null || !string.Equals(candidateFamily.Key ?? "", family.Key ?? "", StringComparison.Ordinal))
                    continue;
                if (!candidate.Visible || !IsFamilyParentRow(candidate, family))
                    continue;

                candidate.Selected = true;
                if (candidate.Cells.Count > 0)
                    _grid.CurrentCell = candidate.Cells["ModName"];
                return true;
            }
            return false;
        }

        private void SelectRecordById(string modId)
        {
            if (string.IsNullOrEmpty(modId))
                return;

            ModProjectRecord selectedRecord = null;
            VariantFamilyGroup selectedFamilyHeader = null;
            _suppressSelectionEvents = true;
            try
            {
                _grid.ClearSelection();
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    ModProjectRecord record = row.Tag as ModProjectRecord;
                    if (record == null || !string.Equals(record.ModId, modId, StringComparison.Ordinal))
                        continue;

                    if (row.Visible)
                    {
                        row.Selected = true;
                        selectedRecord = record;
                        if (row.Cells.Count > 0)
                            _grid.CurrentCell = row.Cells["ModName"];
                    }
                    else
                    {
                        VariantFamilyGroup family = GetFamilyForRow(row);
                        if (SelectVisibleFamilyParentRow(family))
                        {
                            DataGridViewRow current = _grid.CurrentRow;
                            selectedRecord = current == null ? null : current.Tag as ModProjectRecord;
                            VariantFamilyHeaderTag header = current == null ? null : current.Tag as VariantFamilyHeaderTag;
                            selectedFamilyHeader = header == null ? null : header.Family;
                        }
                    }
                    break;
                }
            }
            finally
            {
                _suppressSelectionEvents = false;
            }

            // DataGridView.SelectedRows can be empty before the embedded form has created
            // its handle. Populate the details directly so first opening My Mods is reliable.
            if (selectedRecord != null)
                LoadDetails(selectedRecord);
            else if (selectedFamilyHeader != null)
                ShowBaseGameFamilyDetails(selectedFamilyHeader);
            else
                ClearDetails("Select a mod to view its details.");
            UpdateButtons();
        }

        private static bool IsWallpaperRecord(ModProjectRecord record)
        {
            return record != null && string.Equals(record.Template ?? "", "Wallpaper", StringComparison.OrdinalIgnoreCase);
        }

        private static bool AllWallpaperRecords(List<ModProjectRecord> records)
        {
            if (records == null || records.Count == 0)
                return false;
            for (int i = 0; i < records.Count; i++)
                if (!IsWallpaperRecord(records[i]))
                    return false;
            return true;
        }

        private List<ModProjectRecord> GetDecorPackMembers(ModProjectRecord record)
        {
            List<ModProjectRecord> members = new List<ModProjectRecord>();
            if (record == null || BuildPackageModes.Normalise(record.LastBuildPackageMode) != BuildPackageModes.DecorPack)
                return members;

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            string packKey = record.LastBuiltFamilyKey ?? "";

            // The current saved pack key is authoritative. Reconstruct membership from all
            // projects first so a stale member snapshot cannot strand a pack after moving one
            // Wallpaper in or out through My Mods.
            if (!string.IsNullOrEmpty(packKey))
            {
                List<ModProjectRecord> current = _library.LoadAll();
                for (int i = 0; i < current.Count; i++)
                {
                    ModProjectRecord candidate = current[i];
                    if (candidate == null || string.IsNullOrEmpty(candidate.ModId) || !IsWallpaperRecord(candidate) ||
                        BuildPackageModes.Normalise(candidate.LastBuildPackageMode) != BuildPackageModes.DecorPack ||
                        !string.Equals(candidate.LastBuiltFamilyKey ?? "", packKey, StringComparison.Ordinal) ||
                        !seen.Add(candidate.ModId))
                        continue;
                    members.Add(candidate);
                }
            }

            // Fall back to the saved snapshot for older or partially-migrated records.
            List<string> ids = record.LastBuiltFamilyMemberIds == null
                ? new List<string>()
                : new List<string>(record.LastBuiltFamilyMemberIds);
            if (!string.IsNullOrEmpty(record.ModId) && !ids.Contains(record.ModId))
                ids.Add(record.ModId);
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                if (string.IsNullOrEmpty(id) || !seen.Add(id))
                    continue;
                ModProjectRecord member = _library.Load(id);
                if (member != null && IsWallpaperRecord(member))
                    members.Add(member);
            }

            members.Sort(delegate(ModProjectRecord a, ModProjectRecord b)
            {
                return string.Compare(a == null ? "" : a.Name, b == null ? "" : b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return members;
        }

        private string GetItemType(ModProjectRecord record)
        {
            string key = record == null ? "" : (record.Template ?? "");
            if (string.Equals(key, "Wallpaper", StringComparison.OrdinalIgnoreCase))
                return "Décor";
            if (IsPosterTemplateKey(key))
                return "Poster";
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

        private string GetItemOptionsText(ModProjectRecord record)
        {
            if (IsWallpaperRecord(record))
                return "Wallpaper";
            string posterSize = GetPosterSizeName(record);
            if (!string.IsNullOrEmpty(posterSize))
                return posterSize;
            string shape = GetShape(record);
            if (!string.IsNullOrEmpty(shape))
                return shape;
            string hangingSignSize = GetHangingSignSizeName(record);
            if (!string.IsNullOrEmpty(hangingSignSize))
                return hangingSignSize;
            string wallSignSize = GetWallSignSizeName(record);
            if (!string.IsNullOrEmpty(wallSignSize))
                return wallSignSize;
            return GetBannerThemeName(record);
        }


        private string GetHangingSignSizeName(ModProjectRecord record)
        {
            string key = record == null ? "" : (record.Template ?? "");
            if (string.Equals(key, "Small Hanging Sign", StringComparison.OrdinalIgnoreCase)) return "Small";
            if (string.Equals(key, "Large Hanging Sign", StringComparison.OrdinalIgnoreCase)) return "Large";
            return "";
        }

        private string GetHangingSignSizeIconFile(ModProjectRecord record)
        {
            string size = GetHangingSignSizeName(record);
            if (string.Equals(size, "Small", StringComparison.OrdinalIgnoreCase)) return "hanging_sign_size_small.png";
            if (string.Equals(size, "Large", StringComparison.OrdinalIgnoreCase)) return "hanging_sign_size_large.png";
            return "";
        }

        private string GetWallSignSizeName(ModProjectRecord record)
        {
            string key = record == null ? "" : (record.Template ?? "");
            if (string.Equals(key, "Small Wall Sign", StringComparison.OrdinalIgnoreCase)) return "Small";
            if (string.Equals(key, "Large Wall Sign", StringComparison.OrdinalIgnoreCase)) return "Large";
            return "";
        }

        private string GetWallSignSizeIconFile(ModProjectRecord record)
        {
            string size = GetWallSignSizeName(record);
            if (string.Equals(size, "Small", StringComparison.OrdinalIgnoreCase)) return "wall_sign_size_small.png";
            if (string.Equals(size, "Large", StringComparison.OrdinalIgnoreCase)) return "wall_sign_size_large.png";
            return "";
        }

        private string GetPosterSizeName(ModProjectRecord record)
        {
            string key = record == null ? "" : (record.Template ?? "");
            if (string.Equals(key, "Small Poster", StringComparison.OrdinalIgnoreCase)) return "Small";
            if (string.Equals(key, "Tall Poster", StringComparison.OrdinalIgnoreCase)) return "Tall";
            if (string.Equals(key, "Standard Poster", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Poster", StringComparison.OrdinalIgnoreCase)) return "Standard";
            return "";
        }

        private string GetPosterSizeIconFile(ModProjectRecord record)
        {
            string size = GetPosterSizeName(record);
            if (string.Equals(size, "Small", StringComparison.OrdinalIgnoreCase)) return "poster_size_small.png";
            if (string.Equals(size, "Tall", StringComparison.OrdinalIgnoreCase)) return "poster_size_tall.png";
            if (string.Equals(size, "Standard", StringComparison.OrdinalIgnoreCase)) return "poster_size_standard.png";
            return "";
        }

        private static bool IsPosterTemplateKey(string key)
        {
            return string.Equals(key, "Poster", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Small Poster", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Standard Poster", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Tall Poster", StringComparison.OrdinalIgnoreCase);
        }

        private string GetBannerThemeName(ModProjectRecord record)
        {
            string key = record == null ? "" : (record.Template ?? "");
            if (key.EndsWith(" Double Banner", StringComparison.OrdinalIgnoreCase))
                return key.Substring(0, key.Length - " Double Banner".Length) + " Banner";
            return key.EndsWith(" Banner", StringComparison.OrdinalIgnoreCase) ? key : "";
        }

        private string GetBannerThemeIconFile(ModProjectRecord record)
        {
            string key = GetBannerThemeName(record);
            if (string.IsNullOrEmpty(key)) return "";
            if (string.Equals(key, "Prehistory Banner", StringComparison.OrdinalIgnoreCase)) return "banner_01_Prehistory.png";
            if (string.Equals(key, "Botany Banner", StringComparison.OrdinalIgnoreCase)) return "banner_02_Botany.png";
            if (string.Equals(key, "Marine Life Banner", StringComparison.OrdinalIgnoreCase)) return "banner_03_Marine.png";
            if (string.Equals(key, "Supernatural Banner", StringComparison.OrdinalIgnoreCase)) return "banner_04_Supernatural.png";
            if (string.Equals(key, "Space Banner", StringComparison.OrdinalIgnoreCase)) return "banner_05_Space.png";
            if (string.Equals(key, "Science Banner", StringComparison.OrdinalIgnoreCase)) return "banner_06_Science.png";
            if (string.Equals(key, "Fantasy Banner", StringComparison.OrdinalIgnoreCase)) return "banner_07_Fantasy.png";
            if (string.Equals(key, "Digiverse Banner", StringComparison.OrdinalIgnoreCase)) return "banner_08_Digiverse.png";
            if (string.Equals(key, "Wildlife Banner", StringComparison.OrdinalIgnoreCase)) return "banner_09_Wildlife.png";
            if (string.Equals(key, "Art Banner", StringComparison.OrdinalIgnoreCase)) return "banner_10_Art.png";
            if (string.Equals(key, "General Banner", StringComparison.OrdinalIgnoreCase)) return "banner_99_General.png";
            return "";
        }

        private static Rectangle FitImageBounds(Image image, Rectangle bounds, int maxWidth, int maxHeight)
        {
            if (image == null || image.Width <= 0 || image.Height <= 0)
                return bounds;
            int availableWidth = Math.Max(1, Math.Min(maxWidth, bounds.Width));
            int availableHeight = Math.Max(1, Math.Min(maxHeight, bounds.Height));
            float scale = Math.Min((float)availableWidth / image.Width, (float)availableHeight / image.Height);
            int width = Math.Max(1, (int)Math.Round(image.Width * scale));
            int height = Math.Max(1, (int)Math.Round(image.Height * scale));
            return new Rectangle(bounds.Left + ((bounds.Width - width) / 2), bounds.Top + ((bounds.Height - height) / 2), width, height);
        }

        private string GetShape(ModProjectRecord record)
        {
            string key = record == null ? "" : (record.Template ?? "");
            if (key.IndexOf("Rug", StringComparison.OrdinalIgnoreCase) < 0)
                return "";
            if (key.IndexOf("Rectangle", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Rectangle";
            if (key.IndexOf("Square", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Square";
            if (key.IndexOf("Octagon", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Octagon";
            if (key.IndexOf("Circle", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Circle";
            return "";
        }
        private string GetRecolourableStatus(ModProjectRecord record)
        {
            string key = record == null ? "" : (record.Template ?? "");
            if (key.IndexOf("Rug", StringComparison.OrdinalIgnoreCase) < 0)
                return "N/A";
            if (key.IndexOf("Staff", StringComparison.OrdinalIgnoreCase) >= 0)
                return "\u2713";
            return "\u2715";
        }

        private List<ModProjectRecord> GetFamilyMembers(VariantFamilyGroup family)
        {
            List<ModProjectRecord> members = new List<ModProjectRecord>();
            if (family == null)
                return members;
            if (family.ParentRecord != null)
                members.Add(family.ParentRecord);
            for (int i = 0; i < family.Children.Count; i++)
            {
                ModProjectRecord child = family.Children[i];
                if (child != null)
                    members.Add(child);
            }
            return members;
        }

        private bool TryGetFamily(ModProjectRecord record, out VariantFamilyGroup family)
        {
            family = null;
            if (record == null || string.IsNullOrEmpty(record.ModId))
                return false;
            return _familyByMemberId.TryGetValue(record.ModId, out family) && family != null;
        }

        private List<ModProjectRecord> GetSavedWorkshopFamilyMembers(ModProjectRecord record)
        {
            List<ModProjectRecord> members = new List<ModProjectRecord>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            if (record == null)
                return members;

            if (record.WorkshopFamilyMemberIds != null)
            {
                for (int i = 0; i < record.WorkshopFamilyMemberIds.Count; i++)
                {
                    string id = record.WorkshopFamilyMemberIds[i];
                    if (string.IsNullOrEmpty(id) || !seen.Add(id))
                        continue;
                    ModProjectRecord member = _library.Load(id);
                    if (member != null)
                        members.Add(member);
                }
            }

            if (!string.IsNullOrEmpty(record.ModId) && seen.Add(record.ModId))
                members.Add(_library.Load(record.ModId) ?? record);
            return members;
        }

        private string GetInstalledStatus(ModProjectRecord record)
        {
            bool installed = record != null && !string.IsNullOrEmpty(record.LastBuiltOutputPath) && Directory.Exists(record.LastBuiltOutputPath);
            if (!installed)
                return "Not Installed";
            if (record.PendingChanges)
                return "Installed (old)";

            if (BuildPackageModes.Normalise(record.LastBuildPackageMode) == BuildPackageModes.DecorPack)
            {
                string packKey = record.LastBuiltFamilyKey ?? "";
                List<string> memberIds = record.LastBuiltFamilyMemberIds == null
                    ? new List<string>()
                    : new List<string>(record.LastBuiltFamilyMemberIds);
                if (string.IsNullOrEmpty(packKey) || memberIds.Count < 1)
                    return "Installed (old)";

                for (int i = 0; i < memberIds.Count; i++)
                {
                    ModProjectRecord member = _library.Load(memberIds[i]);
                    if (member == null || member.PendingChanges ||
                        BuildPackageModes.Normalise(member.LastBuildPackageMode) != BuildPackageModes.DecorPack ||
                        !string.Equals(member.LastBuiltFamilyKey ?? "", packKey, StringComparison.Ordinal))
                        return "Installed (old)";
                }
            }

            // Family-wide freshness only applies after BL-020 has actually built this record
            // inside one combined family package. Separately built families keep the legacy
            // per-project behaviour until their first combined rebuild.
            if (BuildPackageModes.Normalise(record.LastBuildPackageMode) == BuildPackageModes.Family)
            {
                VariantFamilyGroup family;
                if (!TryGetFamily(record, out family) || family == null ||
                    !string.Equals(record.LastBuiltFamilyKey ?? "", family.Key ?? "", StringComparison.Ordinal))
                    return "Installed (old)";

                List<ModProjectRecord> members = GetFamilyMembers(family);
                List<string> currentIds = new List<string>();
                for (int i = 0; i < members.Count; i++)
                {
                    ModProjectRecord member = members[i];
                    if (member == null || string.IsNullOrEmpty(member.ModId))
                        continue;
                    currentIds.Add(member.ModId);
                    if (member.PendingChanges)
                        return "Installed (old)";
                }
                currentIds.Sort(StringComparer.Ordinal);

                List<string> builtIds = record.LastBuiltFamilyMemberIds == null
                    ? new List<string>()
                    : new List<string>(record.LastBuiltFamilyMemberIds);
                builtIds.Sort(StringComparer.Ordinal);
                if (currentIds.Count != builtIds.Count)
                    return "Installed (old)";
                for (int i = 0; i < currentIds.Count; i++)
                    if (!string.Equals(currentIds[i], builtIds[i], StringComparison.Ordinal))
                        return "Installed (old)";
            }

            return "Installed";
        }

        private string GetWorkshopStatus(ModProjectRecord record)
        {
            if (record == null)
                return "Not Published";

            string currentPackageMode = BuildPackageModes.Normalise(record.LastBuildPackageMode);
            if (currentPackageMode == BuildPackageModes.Family || currentPackageMode == BuildPackageModes.DecorPack)
            {
                VariantFamilyGroup family;
                if (TryGetFamily(record, out family) && family != null &&
                    string.Equals(record.LastBuiltFamilyKey ?? "", family.Key ?? "", StringComparison.Ordinal))
                {
                    List<ModProjectRecord> members = GetFamilyMembers(family);

                    // The family-level registry is authoritative so stale per-project mirrors cannot split a
                    // successfully published family into inconsistent Workshop states.
                    WorkshopFamilyState registry = GetViewWorkshopFamilyState(family.Key);
                    if (registry != null && BuildPackageModes.Normalise(registry.PackageMode) == currentPackageMode)
                    {
                        if (string.Equals(registry.LinkState, "Missing", StringComparison.OrdinalIgnoreCase) &&
                            string.IsNullOrEmpty(registry.PublishedFileId))
                            return "Missing";
                        if (!string.IsNullOrEmpty(registry.PublishedFileId))
                        {
                            if (registry.LinkNeedsUpdate)
                                return "Published (old)";

                            List<string> currentIds = new List<string>();
                            for (int i = 0; i < members.Count; i++)
                            {
                                ModProjectRecord member = members[i];
                                if (member == null || string.IsNullOrEmpty(member.ModId)) continue;
                                currentIds.Add(member.ModId);
                                if (member.PendingChanges)
                                    return "Published (old)";
                            }
                            currentIds.Sort(StringComparer.Ordinal);
                            List<string> publishedIds = registry.MemberIds == null ? new List<string>() : new List<string>(registry.MemberIds);
                            publishedIds.Sort(StringComparer.Ordinal);
                            if (currentIds.Count != publishedIds.Count)
                                return "Published (old)";
                            for (int i = 0; i < currentIds.Count; i++)
                                if (!string.Equals(currentIds[i], publishedIds[i], StringComparison.Ordinal))
                                    return "Published (old)";

                            DateTime familyWorkshopUpdated = ParseDate(registry.LastUpdatedUtc);
                            for (int i = 0; i < members.Count; i++)
                            {
                                ModProjectRecord member = members[i];
                                if (member == null) continue;
                                DateTime memberBuilt = ParseDate(member.LastBuiltUtc);
                                DateTime memberEdited = ParseDate(member.LastEditedUtc);
                                if ((memberBuilt != DateTime.MinValue && memberBuilt > familyWorkshopUpdated) ||
                                    (memberEdited != DateTime.MinValue && memberEdited > familyWorkshopUpdated))
                                    return "Published (old)";
                            }
                            return "Published";
                        }
                    }

                    // Compatibility fallback for older project mirrors without a recovered family registry.
                    ModProjectRecord familyPublication = null;
                    bool familyLinkMissing = false;
                    for (int i = 0; i < members.Count; i++)
                    {
                        ModProjectRecord member = members[i];
                        if (member == null || BuildPackageModes.Normalise(member.WorkshopPackageMode) != currentPackageMode ||
                            !string.Equals(member.WorkshopFamilyKey ?? "", family.Key ?? "", StringComparison.Ordinal))
                            continue;
                        if (string.Equals(member.WorkshopLinkState, "Missing", StringComparison.OrdinalIgnoreCase))
                            familyLinkMissing = true;
                        if (familyPublication == null && !string.IsNullOrEmpty(member.WorkshopPublishedFileId))
                            familyPublication = member;
                    }
                    if (familyPublication != null || familyLinkMissing)
                    {
                        if (familyLinkMissing && familyPublication == null) return "Missing";
                        string sharedPublishedId = familyPublication == null ? "" : (familyPublication.WorkshopPublishedFileId ?? "");
                        if (string.IsNullOrEmpty(sharedPublishedId)) return "Missing";
                        return "Published (old)";
                    }
                }

                if (string.Equals(record.WorkshopLinkState, "Missing", StringComparison.OrdinalIgnoreCase))
                    return "Missing";
                if (string.IsNullOrEmpty(record.WorkshopPublishedFileId))
                    return "Not Published";
                return "Published (old)";
            }

            if (string.Equals(record.WorkshopLinkState, "Missing", StringComparison.OrdinalIgnoreCase))
                return "Missing";
            if (string.IsNullOrEmpty(record.WorkshopPublishedFileId))
                return "Not Published";
            if (record.WorkshopLinkNeedsUpdate || record.PendingChanges)
                return "Published (old)";

            DateTime workshopUpdated = ParseDate(record.WorkshopLastUpdatedUtc);
            if (workshopUpdated != DateTime.MinValue)
            {
                DateTime lastBuilt = ParseDate(record.LastBuiltUtc);
                DateTime lastEdited = ParseDate(record.LastEditedUtc);
                if ((lastBuilt != DateTime.MinValue && lastBuilt > workshopUpdated) ||
                    (lastEdited != DateTime.MinValue && lastEdited > workshopUpdated))
                    return "Published (old)";
            }
            return "Published";
        }

        private static string FormatInstalledDisplay(string status)
        {
            if (status == "Installed") return "\u2713 Installed";
            if (status == "Installed (old)") return "\u2713 Installed (old)";
            return "\u2014 Not Installed";
        }

        private string FormatDateTwoLines(string value)
        {
            DateTime date;
            if (DateTime.TryParse(value, out date))
            {
                date = date.ToLocalTime();
                return date.ToString("dd/MM/yyyy") + "\n" + date.ToString("HH:mm");
            }
            return "";
        }

        private static DateTime ParseDate(string value)
        {
            DateTime date;
            if (DateTime.TryParse(value, out date))
                return date;
            return DateTime.MinValue;
        }

        private Image TryGetCachedGridPreview(ModProjectRecord record, int maxWidth, int maxHeight)
        {
            if (record == null || string.IsNullOrEmpty(record.ModId))
                return null;
            string key = record.ModId + "|" + maxWidth + "x" + maxHeight;
            lock (_gridPreviewCacheLock)
            {
                Bitmap cached;
                if (_gridPreviewCache.TryGetValue(key, out cached) && cached != null)
                    return new Bitmap(cached);
            }
            return null;
        }

        private Image LoadAndCacheGridPreview(ModProjectRecord record, int maxWidth, int maxHeight, int generation)
        {
            Image generated = LoadPreviewImage(record, maxWidth, maxHeight);
            if (generated == null || record == null || string.IsNullOrEmpty(record.ModId) || generation != _previewLoadGeneration)
                return generated;

            string key = record.ModId + "|" + maxWidth + "x" + maxHeight;
            Bitmap snapshot = new Bitmap(generated);
            lock (_gridPreviewCacheLock)
            {
                if (generation != _previewLoadGeneration)
                {
                    snapshot.Dispose();
                    return generated;
                }
                Bitmap old;
                if (_gridPreviewCache.TryGetValue(key, out old) && old != null)
                    old.Dispose();
                _gridPreviewCache[key] = snapshot;
            }
            return generated;
        }

        private void ClearGridPreviewCache()
        {
            lock (_gridPreviewCacheLock)
            {
                foreach (Bitmap image in _gridPreviewCache.Values)
                    if (image != null) image.Dispose();
                _gridPreviewCache.Clear();
            }
        }

        private async Task LoadGridPreviewsProgressivelyAsync(int generation)
        {
            if (_grid == null || _grid.IsDisposed)
                return;

            // Snapshot row/record pairs because rows can be rebuilt by a filter, build or
            // family-management action while previews are being prepared. Each image is
            // generated off the UI thread and committed only if the row is still the same.
            List<Tuple<DataGridViewRow, ModProjectRecord>> pending = new List<Tuple<DataGridViewRow, ModProjectRecord>>();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                ModProjectRecord record = row == null ? null : row.Tag as ModProjectRecord;
                if (record != null && row.Cells["Preview"].Value == null)
                    pending.Add(Tuple.Create(row, record));
            }

            for (int i = 0; i < pending.Count; i++)
            {
                if (IsDisposed || generation != _previewLoadGeneration)
                    return;

                DataGridViewRow row = pending[i].Item1;
                ModProjectRecord record = pending[i].Item2;
                Image preview = TryGetCachedGridPreview(record, 74, 50);
                if (preview == null)
                {
                    preview = await Task.Run(delegate { return LoadAndCacheGridPreview(record, 74, 50, generation); });
                }

                if (IsDisposed || generation != _previewLoadGeneration)
                {
                    if (preview != null)
                        preview.Dispose();
                    return;
                }

                if (row != null && row.DataGridView == _grid && ReferenceEquals(row.Tag, record))
                {
                    Image old = row.Cells["Preview"].Value as Image;
                    row.Cells["Preview"].Value = preview;
                    if (old != null && !ReferenceEquals(old, preview))
                        old.Dispose();
                }
                else if (preview != null)
                {
                    preview.Dispose();
                }

                // Allow painting/selection between previews instead of delivering a burst of
                // completed images that can still momentarily monopolise the UI thread.
                await Task.Yield();
            }
        }

        private Image LoadDetailPreview(ModProjectRecord record)
        {
            string path;
            if (IsDualArtworkRecord(record))
                path = GetPendingDisplayArtworkPath(record);
            else
                path = !string.IsNullOrEmpty(_pendingArtworkPath) ? _pendingArtworkPath : ResolveArtworkPath(record);
            return LoadImageSafe(path, 560, 330);
        }

        private string ResolveArtworkPath(ModProjectRecord record)
        {
            if (record == null)
                return "";

            string path = _library.GetArtworkPath(record);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                return path;

            // Older project records can occasionally have a missing/stale ArtworkFileName
            // even though the saved artwork file is still present in the project folder.
            // Recover it by looking for the canonical artwork.* source file.
            try
            {
                string directory = _library.GetProjectDirectory(record.ModId);
                if (Directory.Exists(directory))
                {
                    string[] matches = Directory.GetFiles(directory, "artwork.*", SearchOption.TopDirectoryOnly);
                    if (matches.Length > 0)
                        return matches[0];
                }
            }
            catch { }

            return "";
        }

        private string ResolveSecondaryArtworkPath(ModProjectRecord record)
        {
            if (record == null) return "";
            string path = _library.GetSecondaryArtworkPath(record);
            return !string.IsNullOrEmpty(path) && File.Exists(path) ? path : "";
        }

        private Image LoadPreviewImage(ModProjectRecord record, int maxWidth, int maxHeight)
        {
            // Avoid regenerating every icon whenever the My Mods tab is opened. Most clean
            // installed records can use the built ItemIcon directly. Rugs and dual-artwork
            // signs are regenerated from the saved project state so My Mods always reflects
            // the current canonical preview/mask pipeline after editor-side mapping changes.
            if (record != null && !record.PendingChanges && !IsDualArtworkRecord(record) && !IsRugRecord(record))
            {
                string builtIcon = FindBuiltIcon(record);
                if (!string.IsNullOrEmpty(builtIcon))
                {
                    Image installedPreview = LoadImageSafe(builtIcon, maxWidth, maxHeight);
                    if (installedPreview != null)
                        return installedPreview;
                }
            }

            try
            {
                TemplateDefinition template = FindTemplateDefinition(record == null ? null : record.Template);
                if (template != null && record != null)
                {
                    ImagePlacementState placement = new ImagePlacementState();
                    placement.Zoom = record.Zoom <= 0 ? 1.0F : record.Zoom;
                    placement.OffsetX = record.OffsetX;
                    placement.OffsetY = record.OffsetY;
                    placement.RotationDegrees = record.RotationDegrees;
                    placement.GuideColorName = string.IsNullOrEmpty(record.GuideColor) ? "Yellow" : record.GuideColor;
                    placement.ReferenceWidth = record.ReferenceWidth;
                    placement.ReferenceHeight = record.ReferenceHeight;
                    string fitMode = string.IsNullOrEmpty(record.FitMode) ? (template.DefaultFitMode ?? "Fill") : record.FitMode;
                    string iconMode = string.IsNullOrEmpty(record.IconMode) ? "Original Artwork" : record.IconMode;
                    Bitmap preview;
                    if (IsDualArtworkRecord(record))
                    {
                        ImagePlacementState secondaryPlacement = new ImagePlacementState();
                        secondaryPlacement.Zoom = record.SecondaryZoom <= 0 ? 1.0F : record.SecondaryZoom;
                        secondaryPlacement.OffsetX = record.SecondaryOffsetX;
                        secondaryPlacement.OffsetY = record.SecondaryOffsetY;
                        secondaryPlacement.RotationDegrees = record.SecondaryRotationDegrees;
                        secondaryPlacement.GuideColorName = string.IsNullOrEmpty(record.SecondaryGuideColor) ? "Yellow" : record.SecondaryGuideColor;
                        secondaryPlacement.ReferenceWidth = record.SecondaryReferenceWidth;
                        secondaryPlacement.ReferenceHeight = record.SecondaryReferenceHeight;
                        string secondaryFitMode = string.IsNullOrEmpty(record.SecondaryFitMode) ? (template.DefaultFitMode ?? "Fill") : record.SecondaryFitMode;
                        preview = _imageProcessor.CreateDoubleBannerIconPreview(
                            ResolveArtworkPath(record), ResolveSecondaryArtworkPath(record), _library.GetCustomIconPath(record), template,
                            fitMode, placement, secondaryFitMode, secondaryPlacement, iconMode, maxWidth, maxHeight);
                    }
                    else
                    {
                        preview = _imageProcessor.CreateIconPreview(
                            ResolveArtworkPath(record), _library.GetCustomIconPath(record), template,
                            fitMode, placement, iconMode, maxWidth, maxHeight);
                    }
                    if (preview != null)
                        return preview;
                }
            }
            catch { }

            string iconPath = FindBuiltIcon(record);
            if (!string.IsNullOrEmpty(iconPath))
                return LoadImageSafe(iconPath, maxWidth, maxHeight);
            return null;
        }

        private List<TemplateDefinition> LoadTemplateDefinitions()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "templates.json");
                TemplateCatalog catalog = JsonFile.Read<TemplateCatalog>(path);
                return catalog == null || catalog.Templates == null ? new List<TemplateDefinition>() : catalog.Templates;
            }
            catch
            {
                return new List<TemplateDefinition>();
            }
        }

        private TemplateDefinition FindTemplateDefinition(string key)
        {
            if (string.Equals(key, "Poster", StringComparison.OrdinalIgnoreCase))
                key = "Standard Poster";

            if (string.Equals(key, "Rug", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Rectangle Rug", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "RectangleRug", StringComparison.OrdinalIgnoreCase))
                key = "Staff Rectangle Rug";

            foreach (TemplateDefinition template in _templates)
            {
                if (template != null && string.Equals(template.Key, key, StringComparison.OrdinalIgnoreCase))
                    return template;
            }
            return null;
        }

        private void SyncGridScrollBar()
        {
            if (_grid == null || _gridScrollBar == null)
                return;
            int displayed = _grid.DisplayedRowCount(false);
            if (displayed <= 0)
                displayed = Math.Max(1, _grid.ClientSize.Height / Math.Max(1, _grid.RowTemplate.Height));
            int visibleRows = GetVisibleGridRowCount();
            int maximum = Math.Max(0, visibleRows - displayed);
            _gridScrollBar.Maximum = maximum;
            _gridScrollBar.LargeChange = Math.Max(1, displayed);
            _gridScrollBar.Visible = maximum > 0;
            if (maximum > 0 && _grid.FirstDisplayedScrollingRowIndex >= 0)
                _gridScrollBar.Value = Math.Min(maximum, GetVisiblePositionForGridRow(_grid.FirstDisplayedScrollingRowIndex));
            else
                _gridScrollBar.Value = 0;
        }

        private int GetVisibleGridRowCount()
        {
            if (_grid == null)
                return 0;
            int count = 0;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Visible)
                    count++;
            }
            return count;
        }

        private int GetVisiblePositionForGridRow(int rowIndex)
        {
            if (_grid == null || _grid.Rows.Count == 0)
                return 0;
            rowIndex = Math.Max(0, Math.Min(rowIndex, _grid.Rows.Count - 1));
            int visiblePosition = 0;
            for (int i = 0; i < rowIndex; i++)
            {
                if (_grid.Rows[i].Visible)
                    visiblePosition++;
            }
            return visiblePosition;
        }

        private int GetGridRowIndexForVisiblePosition(int visiblePosition)
        {
            if (_grid == null || _grid.Rows.Count == 0)
                return -1;
            visiblePosition = Math.Max(0, visiblePosition);
            int current = 0;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (!row.Visible)
                    continue;
                if (current == visiblePosition)
                    return row.Index;
                current++;
            }
            for (int i = _grid.Rows.Count - 1; i >= 0; i--)
            {
                if (_grid.Rows[i].Visible)
                    return i;
            }
            return -1;
        }

        private string FindBuiltIcon(ModProjectRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.LastBuiltOutputPath) || !Directory.Exists(record.LastBuiltOutputPath))
                return "";

            string direct = Path.Combine(record.LastBuiltOutputPath, "Icons", "ItemIcon.png");
            if (File.Exists(direct))
                return direct;

            try
            {
                string[] matches = Directory.GetFiles(record.LastBuiltOutputPath, "ItemIcon.png", SearchOption.AllDirectories);
                if (matches.Length > 0)
                    return matches[0];
            }
            catch { }
            return "";
        }

        private Image LoadImageSafe(string path, int maxWidth, int maxHeight)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;
            try
            {
                using (Image source = Image.FromFile(path))
                {
                    float scale = Math.Min((float)maxWidth / Math.Max(1, source.Width), (float)maxHeight / Math.Max(1, source.Height));
                    scale = Math.Min(1F, scale);
                    int width = Math.Max(1, (int)Math.Round(source.Width * scale));
                    int height = Math.Max(1, (int)Math.Round(source.Height * scale));
                    Bitmap result = new Bitmap(width, height);
                    using (Graphics g = Graphics.FromImage(result))
                    {
                        g.Clear(Color.Transparent);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.DrawImage(source, new Rectangle(0, 0, width, height));
                    }
                    return result;
                }
            }
            catch
            {
                return null;
            }
        }

        private void SetPreviewImage(Image image)
        {
            Image old = _preview.Image;
            _preview.Image = image;
            if (old != null)
                old.Dispose();
        }

        private void DisposeGridImages()
        {
            if (_grid == null)
                return;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Cells.Count == 0)
                    continue;
                Image image = row.Cells["Preview"].Value as Image;
                if (image != null)
                    image.Dispose();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeGridImages();
                ClearGridPreviewCache();
                if (_batchProgressHideTimer != null)
                {
                    _batchProgressHideTimer.Stop();
                    _batchProgressHideTimer.Dispose();
                    _batchProgressHideTimer = null;
                }
                if (_preview != null && _preview.Image != null)
                {
                    _preview.Image.Dispose();
                    _preview.Image = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}

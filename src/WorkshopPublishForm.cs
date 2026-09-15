using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace TPMSimpleModMaker
{
    internal sealed class WorkshopPublishForm : Form
    {
        private readonly ModProjectRecord _record;
        private readonly List<WorkshopItemSummary> _items;
        private TwoPointRadioButton _newItem;
        private TwoPointRadioButton _existingItem;
        private ComboBox _existingList;
        private TextBox _title;
        private TwoPointTextArea _description;
        private ComboBox _visibility;
        private TextBox _preview;
        private ComboBox _familyPreviewLayout;
        private PictureBox _familyPreviewPicture;
        private Label _familyPreviewHint;
        private TwoPointTextArea _changeNote;
        private Button _publishButton;
        private Label _modeHelp;
        private ToolTip _uiToolTip;
        private readonly bool _familyMode;
        private readonly bool _decorPackMode;
        private readonly string _sharedPackageLabel;
        private readonly string _familyName;
        private readonly int _familyMemberCount;
        private readonly List<string> _familyPreviewPaths;
        private readonly ImageProcessingService _imageProcessor;
        private readonly string _defaultFamilyPreviewStyle;
        private string _generatedFamilyPreviewPath;

        public WorkshopPublishJob Job { get; private set; }
        public string SelectedFamilyPreviewStyle { get; private set; }

        public WorkshopPublishForm(ModProjectRecord record, List<WorkshopItemSummary> items, string defaultPreviewPath,
            bool familyMode = false, string familyName = "", int familyMemberCount = 0,
            List<string> familyPreviewPaths = null, string defaultFamilyPreviewStyle = "Simple Grid")
        {
            _record = record;
            _items = items ?? new List<WorkshopItemSummary>();
            _familyMode = familyMode;
            _decorPackMode = familyMode && record != null && (BuildPackageModes.Normalise(record.WorkshopPackageMode) == BuildPackageModes.DecorPack || (record.ModId ?? "").StartsWith("decorpack:", StringComparison.OrdinalIgnoreCase));
            _sharedPackageLabel = _decorPackMode ? "Décor Pack" : "Variant Family";
            _familyName = string.IsNullOrWhiteSpace(familyName) ? (_decorPackMode ? "Wallpaper Pack" : "Variant Family") : familyName.Trim();
            _familyMemberCount = Math.Max(0, familyMemberCount);
            _familyPreviewPaths = familyPreviewPaths == null ? new List<string>() : new List<string>(familyPreviewPaths);
            _imageProcessor = new ImageProcessingService();
            _defaultFamilyPreviewStyle = NormaliseFamilyPreviewStyle(defaultFamilyPreviewStyle);
            SelectedFamilyPreviewStyle = _defaultFamilyPreviewStyle;
            Text = familyMode ? "Memento Maker - Publish " + _sharedPackageLabel + " to Steam Workshop" : "Memento Maker - Publish to Steam Workshop";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(790, familyMode ? 728 : 650);
            MinimumSize = new Size(720, familyMode ? 690 : 610);
            BackColor = TwoPointTheme.ContentBackground;
            Font = TwoPointTheme.BodyFont(9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TwoPointTheme.ApplyApplicationIcon(this);
            _uiToolTip = new ToolTip { AutoPopDelay = 8000, InitialDelay = 550, ReshowDelay = 120, ShowAlways = true };
            BuildLayout(defaultPreviewPath);
        }

        private void BuildLayout(string defaultPreviewPath)
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.BackColor = TwoPointTheme.ContentBackground;
            root.Padding = new Padding(14, 12, 14, 14);
            root.ColumnCount = 1;
            root.RowCount = 10;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, _familyMode ? 138F : 60F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            Controls.Add(root);

            Label header = new Label();
            header.Text = _familyMode ? "Publish " + _sharedPackageLabel + " to Steam Workshop" : "Publish to Steam Workshop";
            header.Location = new Point(0, 0);
            header.Size = new Size(250, 29);
            header.Margin = new Padding(0, 0, 0, 5);
            TwoPointTheme.StyleSectionHeader(header);
            root.Controls.Add(header, 0, 0);

            Panel mode = NewFieldPanel("Publish Target");
            root.Controls.Add(mode, 0, 1);

            _newItem = new TwoPointRadioButton();
            _newItem.Text = _familyMode ? "Publish " + (_decorPackMode ? "pack" : "family") + " as a new Workshop item (Recommended)" : "Publish as a new Workshop item";
            _newItem.Location = new Point(8, 28);
            _newItem.Size = new Size(235, 24);
            mode.Controls.Add(_newItem);
            _uiToolTip.SetToolTip(_newItem, _familyMode ? "Create one Steam Workshop item containing the complete " + _sharedPackageLabel + "." : "Create a new Steam Workshop item for this mod.");

            _existingItem = new TwoPointRadioButton();
            _existingItem.Text = _familyMode ? "Use an existing Workshop item for this " + (_decorPackMode ? "pack" : "family") : "Update or relink an existing Workshop item";
            _existingItem.Location = new Point(8, 55);
            _existingItem.Size = new Size(285, 24);
            mode.Controls.Add(_existingItem);
            _uiToolTip.SetToolTip(_existingItem, "Update or relink this local mod to an existing Workshop item you own.");

            _existingList = NewCombo();
            _existingList.Location = new Point(295, 53);
            _existingList.Size = new Size(425, 27);
            _existingList.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            mode.Controls.Add(_existingList);
            _uiToolTip.SetToolTip(_existingList, "Choose the exact existing Workshop item to update or relink. Memento Maker never guesses this association.");
            foreach (WorkshopItemSummary item in _items)
                if (item != null) _existingList.Items.Add(new WorkshopChoice(item));
            if (_existingList.Items.Count > 0) _existingList.SelectedIndex = 0;

            _modeHelp = new Label();
            _modeHelp.Location = new Point(295, 25);
            _modeHelp.Size = new Size(425, 24);
            _modeHelp.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _modeHelp.ForeColor = TwoPointTheme.BodyText;
            _modeHelp.Font = TwoPointTheme.BodyFont(8F);
            _modeHelp.AutoEllipsis = true;
            mode.Controls.Add(_modeHelp);

            _title = AddSingleLineField(root, 2, "Workshop Title");
            _description = AddTextAreaField(root, 3, "Workshop Description");

            Panel vis = NewFieldPanel("Visibility");
            root.Controls.Add(vis, 0, 4);
            _visibility = NewCombo();
            _visibility.Items.AddRange(new object[] { "Hidden", "Public", "Friends Only", "Unlisted" });
            _visibility.Location = new Point(8, 27);
            _visibility.Size = new Size(210, 27);
            vis.Controls.Add(_visibility);
            _uiToolTip.SetToolTip(_visibility, "Choose who can see the Workshop item. New uploads default to Hidden.");

            Panel preview = NewFieldPanel(_familyMode ? "Workshop Preview" : "Preview Image");
            root.Controls.Add(preview, 0, 5);
            _preview = NewTextBox();
            _preview.ReadOnly = true;
            preview.Controls.Add(_preview);

            Button browse = new Button();
            browse.Text = "Browse...";
            browse.Size = new Size(110, 31);
            browse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            TwoPointTheme.StyleButton(browse);
            browse.Click += delegate { BrowsePreview(); };
            preview.Controls.Add(browse);
            _uiToolTip.SetToolTip(browse, "Choose a custom Workshop preview image. For combined uploads this overrides the generated layout until you choose another layout.");
            _uiToolTip.SetToolTip(_preview, "Preview image that will be sent to Steam Workshop.");

            if (_familyMode)
            {
                Label layoutLabel = new Label();
                layoutLabel.Text = _decorPackMode ? "Pack Layout" : "Family Layout";
                layoutLabel.Location = new Point(8, 29);
                layoutLabel.Size = new Size(165, 19);
                layoutLabel.Font = TwoPointTheme.BoldFont(8.3F);
                layoutLabel.ForeColor = TwoPointTheme.PrimaryText;
                preview.Controls.Add(layoutLabel);

                _familyPreviewLayout = NewCombo();
                _familyPreviewLayout.Location = new Point(8, 50);
                _familyPreviewLayout.Size = new Size(180, 27);
                _familyPreviewLayout.Items.AddRange(new object[]
                {
                    "Simple Grid",
                    "Hero Grid",
                    "Staggered Mosaic",
                    "Central Hero",
                    "Framed Focus",
                    "Left Hero Stack",
                    "Quadrant Mix",
                    "Double Feature"
                });
                _familyPreviewLayout.SelectedItem = _defaultFamilyPreviewStyle;
                if (_familyPreviewLayout.SelectedIndex < 0)
                    _familyPreviewLayout.SelectedIndex = 0;
                preview.Controls.Add(_familyPreviewLayout);
                _uiToolTip.SetToolTip(_familyPreviewLayout, "Choose how the package member icons are arranged in the transparent main Workshop preview.");

                _familyPreviewPicture = new PictureBox();
                _familyPreviewPicture.Location = new Point(200, 27);
                _familyPreviewPicture.Size = new Size(102, 102);
                _familyPreviewPicture.SizeMode = PictureBoxSizeMode.Zoom;
                _familyPreviewPicture.BackColor = Color.FromArgb(48, 48, 48);
                _familyPreviewPicture.BorderStyle = BorderStyle.FixedSingle;
                preview.Controls.Add(_familyPreviewPicture);
                _uiToolTip.SetToolTip(_familyPreviewPicture, "Live preview. The dark area is only the viewer background; transparent pixels remain transparent in the PNG sent to Steam.");

                _preview.Location = new Point(315, 50);
                _preview.Size = new Size(288, 25);
                _preview.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                browse.Location = new Point(610, 46);

                _familyPreviewHint = new Label();
                _familyPreviewHint.Text = GetFamilyPreviewStyleHint(_defaultFamilyPreviewStyle);
                _familyPreviewHint.Location = new Point(315, 80);
                _familyPreviewHint.Size = new Size(405, 37);
                _familyPreviewHint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                _familyPreviewHint.ForeColor = Color.FromArgb(100, 90, 74);
                _familyPreviewHint.Font = TwoPointTheme.BodyFont(7.8F);
                preview.Controls.Add(_familyPreviewHint);

                _familyPreviewLayout.SelectedIndexChanged += delegate { RegenerateFamilyPreview(); };
            }
            else
            {
                _preview.Location = new Point(8, 28);
                _preview.Size = new Size(595, 25);
                _preview.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                browse.Location = new Point(610, 24);
            }

            _changeNote = AddTextAreaField(root, 6, "Change Notes");

            Label content = new Label();
            content.Dock = DockStyle.Fill;
            content.ForeColor = Color.FromArgb(100, 90, 74);
            content.Font = TwoPointTheme.BodyFont(8.2F);
            content.Text = (_familyMode ? (_decorPackMode ? "Pack content" : "Family content") + (_familyMemberCount > 0 ? " (" + _familyMemberCount + " items)" : "") : "Content") +
                ": " + (_record == null ? "" : (_record.LastBuiltOutputPath ?? ""));
            content.TextAlign = ContentAlignment.MiddleLeft;
            content.AutoEllipsis = true;
            root.Controls.Add(content, 0, 7);

            Label legal = new Label();
            legal.Dock = DockStyle.Fill;
            legal.ForeColor = TwoPointTheme.BodyText;
            legal.Font = TwoPointTheme.BodyFont(8.5F);
            legal.Text = (_familyMode
                ? "Publishing sends the one combined " + _sharedPackageLabel + " folder, Workshop title, description, Items tag and preview image to Steam Workshop. " +
                    "Memento Maker also attaches the individual member previews as additional Workshop gallery images. " +
                    "All members are contained in the same Workshop item. "
                : "Publishing sends the built mod folder, Workshop title, description, Items tag and preview image to Steam Workshop. ") +
                "Steam may require you to accept the Workshop Legal Agreement before a new public upload can be completed. " +
                "Memento Maker never guesses which Workshop item to update - if you relink an older item, you must choose it explicitly.";
            root.Controls.Add(legal, 0, 8);

            TableLayoutPanel buttons = new TableLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.ColumnCount = 3;
            buttons.RowCount = 1;
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttons.Padding = new Padding(0, 6, 0, 0);
            root.Controls.Add(buttons, 0, 9);

            _publishButton = new Button();
            _publishButton.Size = new Size(185, 36);
            _publishButton.Margin = new Padding(0);
            TwoPointTheme.StylePrimaryButton(_publishButton);
            _publishButton.Click += delegate { Accept(); };
            buttons.Controls.Add(_publishButton, 0, 0);
            _uiToolTip.SetToolTip(_publishButton, "Submit the selected Workshop changes to Steam.");

            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.Size = new Size(110, 36);
            cancel.Margin = new Padding(0);
            TwoPointTheme.StyleDangerButton(cancel);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            buttons.Controls.Add(cancel, 2, 0);
            CancelButton = cancel;

            _title.Text = !string.IsNullOrEmpty(_record.WorkshopTitle) ? _record.WorkshopTitle :
                (_familyMode ? _familyName : (_record.Name ?? ""));
            _uiToolTip.SetToolTip(_title, "Title shown on the Steam Workshop page.");
            _uiToolTip.SetToolTip(_description, "Description shown on the Steam Workshop page.");
            _uiToolTip.SetToolTip(_changeNote, "Change note recorded with this Workshop update.");

            _description.Text = !string.IsNullOrEmpty(_record.WorkshopDescription) ? _record.WorkshopDescription : (_record.Description ?? "");
            _preview.Text = defaultPreviewPath ?? "";
            UpdatePreviewThumbnail();
            if (_familyMode && _familyPreviewPaths.Count > 0)
                RegenerateFamilyPreview();
            string visibility = string.IsNullOrEmpty(_record.WorkshopVisibility) ? "Hidden" : ToDisplayVisibility(_record.WorkshopVisibility);
            _visibility.SelectedItem = visibility;
            if (_visibility.SelectedIndex < 0) _visibility.SelectedIndex = 0;
            bool missingLink = string.Equals(_record.WorkshopLinkState, "Missing", StringComparison.OrdinalIgnoreCase);
            bool hasStoredLinkId = !missingLink && !string.IsNullOrEmpty(_record.WorkshopPublishedFileId);
            WorkshopChoice matchingStoredLink = hasStoredLinkId ? FindChoice(_record.WorkshopPublishedFileId) : null;
            bool storedLinkAvailable = matchingStoredLink != null;
            _changeNote.Text = storedLinkAvailable ? "Updated with Memento Maker" :
                ((missingLink || hasStoredLinkId) ? "Re-published with Memento Maker" : "Initial release");

            if (storedLinkAvailable)
            {
                // Only lock to Update when the exact saved Workshop item is present in the
                // currently synced list. A stale/local PublishedFileId must never force an
                // update target that Steam cannot currently resolve.
                _existingItem.Checked = true;
                _newItem.Enabled = false;
                _existingItem.Enabled = false;
                _existingList.SelectedItem = matchingStoredLink;
                _existingList.Enabled = false;
                _modeHelp.Text = "Linked Workshop item ID: " + _record.WorkshopPublishedFileId;
            }
            else if (hasStoredLinkId)
            {
                // The saved ID could not be found in the synced Workshop list. Default to a
                // brand-new upload and leave explicit relinking available through the list.
                _newItem.Checked = true;
                _newItem.Enabled = true;
                _existingItem.Enabled = _items.Count > 0;
                _modeHelp.Text = "Linked Workshop item " + _record.WorkshopPublishedFileId +
                    " could not be found in the synced Workshop list. Publish as new or explicitly choose another item to relink.";
            }
            else if (missingLink)
            {
                _newItem.Checked = true;
                _newItem.Enabled = true;
                _existingItem.Enabled = _items.Count > 0;
                string oldId = _record.WorkshopPreviousPublishedFileId ?? "";
                _modeHelp.Text = "The previous Workshop item" + (string.IsNullOrEmpty(oldId) ? "" : " " + oldId) +
                    " no longer exists. Publish as new or explicitly relink another item.";
            }
            else
            {
                _newItem.Checked = true;
                _newItem.Enabled = true;
                _existingItem.Enabled = _items.Count > 0;
                _modeHelp.Text = _items.Count == 0
                    ? "No recent Workshop list is cached. Publish will create a new item; use Workshop Tools → Relink Workshop Item to choose an existing item."
                    : (_familyMode ? _items.Count + " recently verified Workshop items are available if you want to link this " + (_decorPackMode ? "pack" : "family") + " to one of them." :
                    _items.Count + " recently verified Workshop items are available to relink if needed.");
            }

            _newItem.CheckedChanged += delegate { UpdateMode(); };
            _existingItem.CheckedChanged += delegate { UpdateMode(); };
            _existingList.SelectedIndexChanged += delegate { ExistingSelectionChanged(); };
            UpdateMode();
        }

        private Panel NewFieldPanel(string title)
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(0, 0, 0, 4);
            panel.BackColor = TwoPointTheme.PanelLight;

            Label label = new Label();
            label.Text = title;
            label.Location = new Point(7, 4);
            label.Size = new Size(220, 20);
            label.Font = TwoPointTheme.BoldFont(9F);
            label.ForeColor = TwoPointTheme.PrimaryText;
            panel.Controls.Add(label);
            return panel;
        }

        private TextBox AddSingleLineField(TableLayoutPanel root, int row, string label)
        {
            Panel panel = NewFieldPanel(label);
            root.Controls.Add(panel, 0, row);
            TextBox box = NewTextBox();
            box.Location = new Point(8, 27);
            box.Size = new Size(Math.Max(100, panel.Width - 16), 26);
            box.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            panel.Controls.Add(box);
            return box;
        }

        private TwoPointTextArea AddTextAreaField(TableLayoutPanel root, int row, string label)
        {
            Panel panel = NewFieldPanel(label);
            root.Controls.Add(panel, 0, row);
            TwoPointTextArea area = new TwoPointTextArea();
            area.Location = new Point(8, 27);
            area.Size = new Size(Math.Max(100, panel.Width - 16), Math.Max(42, panel.Height - 32));
            area.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;
            area.Font = TwoPointTheme.BodyFont(9.25F);
            panel.Controls.Add(area);
            return area;
        }

        private TextBox NewTextBox()
        {
            TextBox box = new TextBox();
            box.BackColor = TwoPointTheme.FieldBackground;
            box.ForeColor = TwoPointTheme.BodyText;
            box.Font = TwoPointTheme.BodyFont(9.25F);
            return box;
        }

        private ComboBox NewCombo()
        {
            ComboBox combo = new ComboBox();
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.FlatStyle = FlatStyle.Flat;
            combo.BackColor = TwoPointTheme.FieldBackground;
            combo.ForeColor = TwoPointTheme.BodyText;
            combo.Font = TwoPointTheme.BodyFont(9.25F);
            return combo;
        }

        private void UpdateMode()
        {
            bool missingLink = _record != null && string.Equals(_record.WorkshopLinkState, "Missing", StringComparison.OrdinalIgnoreCase);
            bool hasStoredId = _record != null && !missingLink && !string.IsNullOrEmpty(_record.WorkshopPublishedFileId);
            bool storedTargetAvailable = hasStoredId && FindChoice(_record.WorkshopPublishedFileId) != null;
            _existingList.Enabled = !storedTargetAvailable && _existingItem.Checked && _existingList.Items.Count > 0;
            _publishButton.Text = (_existingItem.Checked || storedTargetAvailable) ? "Update Workshop" : "Publish to Workshop";
        }

        private void ExistingSelectionChanged()
        {
            if (!_existingItem.Checked || _existingList.SelectedItem == null)
                return;
            WorkshopChoice choice = _existingList.SelectedItem as WorkshopChoice;
            if (choice == null || choice.Item == null)
                return;

            if (!string.IsNullOrEmpty(choice.Item.Visibility))
            {
                _visibility.SelectedItem = ToDisplayVisibility(choice.Item.Visibility);
                if (_visibility.SelectedIndex < 0) _visibility.SelectedIndex = 0;
            }
            _modeHelp.Text = "Selected Workshop item ID: " + (choice.Item.PublishedFileId ?? "");
        }

        private WorkshopChoice FindChoice(string id)
        {
            foreach (object obj in _existingList.Items)
            {
                WorkshopChoice choice = obj as WorkshopChoice;
                if (choice != null && choice.Item != null && string.Equals(choice.Item.PublishedFileId, id, StringComparison.Ordinal))
                    return choice;
            }
            return null;
        }

        private void BrowsePreview()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Choose Steam Workshop preview image";
                dialog.Filter = "Image Files|*.png;*.jpg;*.jpeg|All Files|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _preview.Text = dialog.FileName;
                    UpdatePreviewThumbnail();
                    if (_familyPreviewHint != null)
                        _familyPreviewHint.Text = "Custom main preview selected · member icons will still be uploaded as gallery images";
                }
            }
        }

        private void RegenerateFamilyPreview()
        {
            if (!_familyMode || _familyPreviewLayout == null || _familyPreviewPaths == null || _familyPreviewPaths.Count == 0)
                return;

            string style = NormaliseFamilyPreviewStyle(_familyPreviewLayout.SelectedItem as string);
            try
            {
                string path = _imageProcessor.CreateWorkshopFamilyCompositePreview(_familyPreviewPaths, style);
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return;

                string oldGenerated = _generatedFamilyPreviewPath;
                _generatedFamilyPreviewPath = path;
                SelectedFamilyPreviewStyle = style;
                _preview.Text = path;
                UpdatePreviewThumbnail();
                if (_familyPreviewHint != null)
                    _familyPreviewHint.Text = GetFamilyPreviewStyleHint(style);

                if (!string.IsNullOrEmpty(oldGenerated) && !string.Equals(oldGenerated, path, StringComparison.OrdinalIgnoreCase))
                {
                    try { if (File.Exists(oldGenerated)) File.Delete(oldGenerated); } catch { }
                }
            }
            catch
            {
                // Keep the previously generated/default image if one layout render fails.
            }
        }

        private void UpdatePreviewThumbnail()
        {
            if (_familyPreviewPicture == null)
                return;

            Image old = _familyPreviewPicture.Image;
            _familyPreviewPicture.Image = null;
            if (old != null)
                old.Dispose();

            string path = _preview == null ? "" : (_preview.Text ?? "");
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                using (MemoryStream stream = new MemoryStream(bytes))
                using (Image image = Image.FromStream(stream))
                    _familyPreviewPicture.Image = new Bitmap(image);
            }
            catch { }
        }

        private static string GetFamilyPreviewStyleHint(string style)
        {
            string normalised = NormaliseFamilyPreviewStyle(style);
            if (string.Equals(normalised, "Hero Grid", StringComparison.OrdinalIgnoreCase))
                return "Parent/main item is shown larger with supporting member icons beside it · transparent PNG\r\nIndividual member icons are uploaded as gallery images";
            if (string.Equals(normalised, "Staggered Mosaic", StringComparison.OrdinalIgnoreCase))
                return "Mixed-size member tiles create a more dynamic mosaic · transparent PNG\r\nIndividual member icons are uploaded as gallery images";
            if (string.Equals(normalised, "Central Hero", StringComparison.OrdinalIgnoreCase))
                return "Parent/main item is centred and surrounded by supporting member icons · transparent PNG\r\nIndividual member icons are uploaded as gallery images";
            if (string.Equals(normalised, "Framed Focus", StringComparison.OrdinalIgnoreCase))
                return "A large centre feature is framed by smaller member icons · transparent PNG\r\nIndividual member icons are uploaded as gallery images";
            if (string.Equals(normalised, "Left Hero Stack", StringComparison.OrdinalIgnoreCase))
                return "Parent/main item is featured on the left with a compact stack on the right · transparent PNG\r\nIndividual member icons are uploaded as gallery images";
            if (string.Equals(normalised, "Quadrant Mix", StringComparison.OrdinalIgnoreCase))
                return "Member icons are grouped into four balanced visual sections · transparent PNG\r\nIndividual member icons are uploaded as gallery images";
            if (string.Equals(normalised, "Double Feature", StringComparison.OrdinalIgnoreCase))
                return "Two larger feature icons are supported by smaller member tiles · transparent PNG\r\nIndividual member icons are uploaded as gallery images";
            return "Member icons use an equal-size grid arrangement · transparent PNG\r\nIndividual member icons are uploaded as gallery images";
        }

        private static string NormaliseFamilyPreviewStyle(string style)
        {
            string value = (style ?? "").Trim();
            if (string.Equals(value, "Hero Grid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Hero + Grid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Hero+Grid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "HeroGrid", StringComparison.OrdinalIgnoreCase))
                return "Hero Grid";
            if (string.Equals(value, "Staggered Mosaic", StringComparison.OrdinalIgnoreCase))
                return "Staggered Mosaic";
            if (string.Equals(value, "Central Hero", StringComparison.OrdinalIgnoreCase))
                return "Central Hero";
            if (string.Equals(value, "Framed Focus", StringComparison.OrdinalIgnoreCase))
                return "Framed Focus";
            if (string.Equals(value, "Left Hero Stack", StringComparison.OrdinalIgnoreCase))
                return "Left Hero Stack";
            if (string.Equals(value, "Quadrant Mix", StringComparison.OrdinalIgnoreCase))
                return "Quadrant Mix";
            if (string.Equals(value, "Double Feature", StringComparison.OrdinalIgnoreCase))
                return "Double Feature";
            // Preview 37 removes the old Strip/Fan choices. Existing saved values fall back
            // to Simple Grid rather than leaving a no-longer-selectable layout active.
            return "Simple Grid";
        }

        private static string ToDisplayVisibility(string visibility)
        {
            return string.Equals(visibility, "Private", StringComparison.OrdinalIgnoreCase) ? "Hidden" : visibility;
        }

        private static string ToSteamVisibility(string visibility)
        {
            return string.Equals(visibility, "Hidden", StringComparison.OrdinalIgnoreCase) ? "Private" : visibility;
        }

        private void Accept()
        {
            if (string.IsNullOrWhiteSpace(_title.Text))
            {
                TwoPointTheme.ShowMessage(this, "Please enter a Workshop title.", "Workshop Title Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(_preview.Text) || !File.Exists(_preview.Text))
            {
                TwoPointTheme.ShowMessage(this, "Please choose a valid Workshop preview image.", "Preview Image Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // The radio selection is authoritative. Choosing "Publish as new" must always
            // clear any stale/saved PublishedFileId; choosing Existing must use the exact item
            // selected in the synced Workshop list.
            string publishedId = "";
            if (_existingItem.Checked)
            {
                WorkshopChoice choice = _existingList.SelectedItem as WorkshopChoice;
                if (choice == null || choice.Item == null || string.IsNullOrEmpty(choice.Item.PublishedFileId))
                {
                    TwoPointTheme.ShowMessage(this, "Choose the existing Workshop item you want to update.", "Workshop Item Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                publishedId = choice.Item.PublishedFileId;
            }

            string action = string.IsNullOrEmpty(publishedId) ? "publish a NEW Workshop item" : "update Workshop item " + publishedId;
            string confirmationMessage =
                "Memento Maker is ready to " + action + (_familyMode ? " for the complete " + _sharedPackageLabel : "") + ".\n\n" +
                "Workshop title: " + _title.Text.Trim() + "\n" +
                "Visibility: " + (_visibility.SelectedItem as string ?? "Hidden") +
                (string.IsNullOrEmpty(publishedId) ? "" : "\nWorkshop ID: " + publishedId) +
                (_familyMode && _familyMemberCount > 0 ? "\n" + (_decorPackMode ? "Wallpapers" : "Family members") + ": " + _familyMemberCount : "") +
                "\n\nThe current " + (_familyMode ? _sharedPackageLabel : "built mod") + " folder, title, description, " + (_decorPackMode ? "Wallpapers" : "Items") + " tag and preview image will be sent to Steam Workshop." +
                "\n\nThis does not rebuild the local mod. Continue?";
            DialogResult confirm = TwoPointTheme.ShowMessage(this,
                confirmationMessage,
                "Confirm Steam Workshop Upload", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return;

            Job = new WorkshopPublishJob();
            Job.PublishedFileId = publishedId;
            Job.Title = _title.Text.Trim();
            Job.Description = _description.Text ?? "";
            Job.ContentPath = _record == null ? "" : (_record.LastBuiltOutputPath ?? "");
            Job.PreviewPath = _preview.Text;
            Job.Visibility = ToSteamVisibility(_visibility.SelectedItem as string ?? "Hidden");
            Job.Tags = new List<string>(new[] { _decorPackMode ? "Wallpapers" : "Items" });
            Job.ChangeNote = _changeNote.Text ?? "";
            Job.MementoModId = _record == null ? "" : (_record.ModId ?? "");
            if (_familyMode && _familyPreviewLayout != null)
                SelectedFamilyPreviewStyle = NormaliseFamilyPreviewStyle(_familyPreviewLayout.SelectedItem as string);
            DialogResult = DialogResult.OK;
            Close();
        }

        private sealed class WorkshopChoice
        {
            public WorkshopItemSummary Item { get; private set; }

            public WorkshopChoice(WorkshopItemSummary item)
            {
                Item = item;
            }

            public override string ToString()
            {
                if (Item == null) return "";
                return (Item.Title ?? "Untitled") + "  [" + (Item.PublishedFileId ?? "") + "]";
            }
        }
    }
}

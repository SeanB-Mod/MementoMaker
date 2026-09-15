using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace TPMSimpleModMaker
{
    internal sealed class WorkshopRelinkForm : Form
    {
        private readonly ModProjectRecord _record;
        private readonly List<WorkshopItemSummary> _items;
        private ComboBox _itemList;
        private TextBox _searchBox;
        private Label _details;
        private readonly string _initialSearch;
        private ToolTip _uiToolTip;

        public WorkshopItemSummary SelectedWorkshopItem { get; private set; }

        public WorkshopRelinkForm(ModProjectRecord record, List<WorkshopItemSummary> items, string initialSearch = null)
        {
            _record = record;
            _items = items ?? new List<WorkshopItemSummary>();
            _initialSearch = initialSearch ?? "";

            Text = "Memento Maker - Relink Steam Workshop Item";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(640, 355);
            MinimumSize = new Size(600, 345);
            BackColor = TwoPointTheme.ContentBackground;
            Font = TwoPointTheme.BodyFont(9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TwoPointTheme.ApplyApplicationIcon(this);
            _uiToolTip = new ToolTip { AutoPopDelay = 8000, InitialDelay = 550, ReshowDelay = 120, ShowAlways = true };

            BuildLayout();
        }

        private void BuildLayout()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(14, 12, 14, 14);
            root.ColumnCount = 1;
            root.RowCount = 6;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            Controls.Add(root);

            Label header = new Label();
            header.Text = "Relink Steam Workshop Item";
            header.Size = new Size(280, 29);
            header.Margin = new Padding(0, 0, 0, 5);
            TwoPointTheme.StyleSectionHeader(header);
            root.Controls.Add(header, 0, 0);

            Panel current = NewPanel("Current Memento Maker Link");
            root.Controls.Add(current, 0, 1);
            Label currentValue = new Label();
            currentValue.Location = new Point(8, 27);
            currentValue.Size = new Size(590, 25);
            currentValue.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            currentValue.ForeColor = TwoPointTheme.BodyText;
            currentValue.Font = TwoPointTheme.BodyFont(8.8F);
            string currentId = _record == null ? "" : (_record.WorkshopPublishedFileId ?? "");
            string previousId = _record == null ? "" : (_record.WorkshopPreviousPublishedFileId ?? "");
            currentValue.Text = !string.IsNullOrEmpty(currentId) ? "PublishedFileId " + currentId :
                (!string.IsNullOrEmpty(previousId) ? "No active link (previous item " + previousId + ")" : "No active Workshop link");
            current.Controls.Add(currentValue);

            Panel search = NewPanel("Search Workshop Items");
            root.Controls.Add(search, 0, 2);
            _searchBox = new TextBox();
            _searchBox.Location = new Point(8, 24);
            _searchBox.Size = new Size(590, 24);
            _searchBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _searchBox.BackColor = TwoPointTheme.FieldBackground;
            _searchBox.ForeColor = TwoPointTheme.BodyText;
            _searchBox.Font = TwoPointTheme.BodyFont(9F);
            search.Controls.Add(_searchBox);
            _searchBox.TextChanged += delegate { PopulateItems(); };

            Panel choose = NewPanel("Choose Existing Workshop Item");
            root.Controls.Add(choose, 0, 3);
            _itemList = new ComboBox();
            _itemList.DropDownStyle = ComboBoxStyle.DropDownList;
            _itemList.FlatStyle = FlatStyle.Flat;
            _itemList.BackColor = TwoPointTheme.FieldBackground;
            _itemList.ForeColor = TwoPointTheme.BodyText;
            _itemList.Font = TwoPointTheme.BodyFont(9F);
            _itemList.Location = new Point(8, 27);
            _itemList.Size = new Size(590, 27);
            _itemList.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            choose.Controls.Add(_itemList);
            _uiToolTip.SetToolTip(_itemList, "Choose the existing Steam Workshop item that should be associated with this local Memento Maker mod.");

            _itemList.SelectedIndexChanged += delegate { UpdateDetails(); };

            _details = new Label();
            _details.Dock = DockStyle.Fill;
            _details.Margin = new Padding(4, 8, 4, 4);
            _details.ForeColor = TwoPointTheme.BodyText;
            _details.Font = TwoPointTheme.BodyFont(8.7F);
            _details.TextAlign = ContentAlignment.TopLeft;
            root.Controls.Add(_details, 0, 4);

            TableLayoutPanel buttons = new TableLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.ColumnCount = 3;
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttons.Padding = new Padding(0, 6, 0, 0);
            root.Controls.Add(buttons, 0, 5);

            Button relink = new Button();
            relink.Text = "Relink Workshop Item";
            relink.Size = new Size(185, 36);
            relink.Margin = new Padding(0);
            TwoPointTheme.StylePrimaryButton(relink);
            relink.Click += delegate { Accept(); };
            buttons.Controls.Add(relink, 0, 0);
            _uiToolTip.SetToolTip(relink, "Change only Memento Maker's local Workshop association. No files are uploaded until you run Update Workshop.");

            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.Size = new Size(110, 36);
            cancel.Margin = new Padding(0);
            TwoPointTheme.StyleDangerButton(cancel);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            buttons.Controls.Add(cancel, 2, 0);
            CancelButton = cancel;

            // Use the family name as a ranking/selection hint rather than a hard initial filter because
            // the Workshop title may have been customised at publish time.
            _searchBox.Text = "";
            PopulateItems();
            SelectInitialCandidate();
            UpdateDetails();
        }

        private void PopulateItems()
        {
            if (_itemList == null) return;
            string filter = _searchBox == null ? "" : (_searchBox.Text ?? "").Trim();
            WorkshopChoice previous = _itemList.SelectedItem as WorkshopChoice;
            string previousId = previous == null || previous.Item == null ? "" : (previous.Item.PublishedFileId ?? "");
            _itemList.BeginUpdate();
            try
            {
                _itemList.Items.Clear();
                List<WorkshopItemSummary> ordered = new List<WorkshopItemSummary>(_items);
                ordered.Sort(delegate(WorkshopItemSummary a, WorkshopItemSummary b)
                {
                    bool aExact = a != null && string.Equals((a.Title ?? "").Trim(), filter, StringComparison.OrdinalIgnoreCase);
                    bool bExact = b != null && string.Equals((b.Title ?? "").Trim(), filter, StringComparison.OrdinalIgnoreCase);
                    if (aExact != bExact) return aExact ? -1 : 1;
                    return string.Compare(a == null ? "" : a.Title, b == null ? "" : b.Title, StringComparison.OrdinalIgnoreCase);
                });
                for (int i = 0; i < ordered.Count; i++)
                {
                    WorkshopItemSummary item = ordered[i];
                    if (item == null || string.IsNullOrEmpty(item.PublishedFileId)) continue;
                    if (!string.IsNullOrEmpty(filter) &&
                        (item.Title ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                        (item.PublishedFileId ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    WorkshopChoice choice = new WorkshopChoice(item);
                    _itemList.Items.Add(choice);
                    if (!string.IsNullOrEmpty(previousId) && string.Equals(item.PublishedFileId, previousId, StringComparison.Ordinal))
                        _itemList.SelectedItem = choice;
                }
                if (_itemList.SelectedIndex < 0 && _itemList.Items.Count > 0)
                    _itemList.SelectedIndex = 0;
            }
            finally { _itemList.EndUpdate(); }
            UpdateDetails();
        }

        private void SelectInitialCandidate()
        {
            if (_itemList == null || _itemList.Items.Count == 0 || string.IsNullOrWhiteSpace(_initialSearch))
                return;

            string hint = _initialSearch.Trim();
            int containsIndex = -1;
            for (int i = 0; i < _itemList.Items.Count; i++)
            {
                WorkshopChoice choice = _itemList.Items[i] as WorkshopChoice;
                WorkshopItemSummary item = choice == null ? null : choice.Item;
                if (item == null) continue;
                string title = (item.Title ?? "").Trim();
                if (string.Equals(title, hint, StringComparison.OrdinalIgnoreCase))
                {
                    _itemList.SelectedIndex = i;
                    return;
                }
                if (containsIndex < 0 && title.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                    containsIndex = i;
            }
            if (containsIndex >= 0)
                _itemList.SelectedIndex = containsIndex;
        }

        private Panel NewPanel(string title)
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(0, 0, 0, 4);
            panel.BackColor = TwoPointTheme.PanelLight;

            Label label = new Label();
            label.Text = title;
            label.Location = new Point(7, 4);
            label.Size = new Size(260, 20);
            label.Font = TwoPointTheme.BoldFont(9F);
            label.ForeColor = TwoPointTheme.PrimaryText;
            panel.Controls.Add(label);
            return panel;
        }

        private void UpdateDetails()
        {
            WorkshopChoice choice = _itemList == null ? null : _itemList.SelectedItem as WorkshopChoice;
            if (choice == null || choice.Item == null)
            {
                string filter = _searchBox == null ? "" : (_searchBox.Text ?? "").Trim();
                if (_items.Count > 0 && !string.IsNullOrEmpty(filter))
                    _details.Text = "No Workshop items match the current search. Clear or change the search to see the available authored Workshop items.";
                else
                    _details.Text = "No existing Workshop items are available to relink.";
                return;
            }

            WorkshopItemSummary item = choice.Item;
            _details.Text = "Workshop ID: " + (item.PublishedFileId ?? "") + Environment.NewLine +
                "Title: " + (item.Title ?? "") + Environment.NewLine +
                "Visibility: " + (string.IsNullOrEmpty(item.Visibility) ? "Unknown" : item.Visibility) +
                "    Tags: " + (string.IsNullOrEmpty(item.Tags) ? "None" : item.Tags) + Environment.NewLine +
                "Relinking changes only Memento Maker's local association. It does not upload or modify anything on Steam. " +
                "The mod will show Published (old) until you perform one successful Workshop update.";
        }

        private void Accept()
        {
            WorkshopChoice choice = _itemList == null ? null : _itemList.SelectedItem as WorkshopChoice;
            if (choice == null || choice.Item == null)
            {
                TwoPointTheme.ShowMessage(this, "Select an existing Steam Workshop item first.", "Workshop Item Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SelectedWorkshopItem = choice.Item;
            DialogResult = DialogResult.OK;
            Close();
        }

        private sealed class WorkshopChoice
        {
            public WorkshopItemSummary Item { get; private set; }
            public WorkshopChoice(WorkshopItemSummary item) { Item = item; }
            public override string ToString()
            {
                return (Item == null ? "" : (Item.Title ?? "")) + " [" + (Item == null ? "" : (Item.PublishedFileId ?? "")) + "]";
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace TPMSimpleModMaker
{
    internal sealed class VariantFamilyManagerForm : Form
    {
        private sealed class MemberItem
        {
            public ModProjectRecord Record;
            public string ItemType;

            public override string ToString()
            {
                if (Record == null)
                    return "";
                string name = string.IsNullOrEmpty(Record.Name) ? Record.ModId : Record.Name;
                string type = string.IsNullOrEmpty(ItemType) ? (Record.Template ?? "") : ItemType;
                return string.IsNullOrEmpty(type) ? (name ?? "") : ((name ?? "") + "   [" + type + "]");
            }
        }

        private readonly CheckedListBox _members;
        private readonly TextBox _searchBox;
        private readonly ComboBox _typeFilter;
        private readonly List<MemberItem> _allItems = new List<MemberItem>();
        private readonly HashSet<string> _checkedIds = new HashSet<string>(StringComparer.Ordinal);
        private bool _rebuildingList;

        public HashSet<string> SelectedModIds { get; private set; }

        public VariantFamilyManagerForm(string familyName, string description,
            List<ModProjectRecord> candidates, HashSet<string> currentMemberIds,
            Dictionary<string, string> itemTypes)
        {
            SelectedModIds = new HashSet<string>(StringComparer.Ordinal);
            Text = "Manage Variant Family";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(590, 575);
            MinimumSize = new Size(500, 460);
            BackColor = TwoPointTheme.ContentBackground;
            Font = TwoPointTheme.BodyFont(9F);

            if (currentMemberIds != null)
            {
                foreach (string id in currentMemberIds)
                {
                    if (!string.IsNullOrEmpty(id))
                        _checkedIds.Add(id);
                }
            }

            if (candidates != null)
            {
                candidates.Sort(delegate(ModProjectRecord a, ModProjectRecord b)
                {
                    return string.Compare(a == null ? "" : a.Name, b == null ? "" : b.Name, StringComparison.OrdinalIgnoreCase);
                });

                for (int i = 0; i < candidates.Count; i++)
                {
                    ModProjectRecord record = candidates[i];
                    if (record == null || string.IsNullOrEmpty(record.ModId))
                        continue;
                    string type = "";
                    if (itemTypes != null)
                        itemTypes.TryGetValue(record.ModId, out type);
                    _allItems.Add(new MemberItem { Record = record, ItemType = type ?? "" });
                }
            }

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(16);
            root.ColumnCount = 1;
            root.RowCount = 6;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            Label title = new Label();
            title.AutoSize = true;
            title.Text = string.IsNullOrEmpty(familyName) ? "Variant Family" : familyName;
            title.ForeColor = TwoPointTheme.PrimaryText;
            title.Font = TwoPointTheme.BoldFont(12F);
            title.Margin = new Padding(0, 0, 0, 5);
            root.Controls.Add(title, 0, 0);

            Label help = new Label();
            help.AutoSize = true;
            help.MaximumSize = new Size(550, 0);
            help.Text = (description ?? "") +
                "\n\nTick the mods that should belong to this family. Changes are saved together and affected mods will be marked as needing a rebuild.";
            help.ForeColor = TwoPointTheme.BodyText;
            help.Font = TwoPointTheme.BodyFont(8.5F);
            help.Margin = new Padding(0, 0, 0, 10);
            root.Controls.Add(help, 0, 1);

            TableLayoutPanel filters = new TableLayoutPanel();
            filters.Dock = DockStyle.Top;
            filters.AutoSize = true;
            filters.ColumnCount = 2;
            filters.RowCount = 2;
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64F));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));
            filters.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            filters.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            filters.Margin = new Padding(0, 0, 0, 9);
            root.Controls.Add(filters, 0, 2);

            Label searchLabel = new Label();
            searchLabel.Text = "Search";
            searchLabel.AutoSize = true;
            searchLabel.ForeColor = TwoPointTheme.PrimaryText;
            searchLabel.Font = TwoPointTheme.BoldFont(8.2F);
            searchLabel.Margin = new Padding(0, 0, 0, 3);
            filters.Controls.Add(searchLabel, 0, 0);

            Label typeLabel = new Label();
            typeLabel.Text = "Item Type";
            typeLabel.AutoSize = true;
            typeLabel.ForeColor = TwoPointTheme.PrimaryText;
            typeLabel.Font = TwoPointTheme.BoldFont(8.2F);
            typeLabel.Margin = new Padding(8, 0, 0, 3);
            filters.Controls.Add(typeLabel, 1, 0);

            _searchBox = new TextBox();
            _searchBox.Dock = DockStyle.Fill;
            _searchBox.Margin = new Padding(0, 0, 6, 0);
            _searchBox.BackColor = TwoPointTheme.FieldBackground;
            _searchBox.ForeColor = TwoPointTheme.BodyText;
            _searchBox.Font = TwoPointTheme.BodyFont(9F);
            filters.Controls.Add(_searchBox, 0, 1);

            _typeFilter = new ComboBox();
            _typeFilter.Dock = DockStyle.Fill;
            _typeFilter.Margin = new Padding(2, 0, 0, 0);
            _typeFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _typeFilter.BackColor = TwoPointTheme.FieldBackground;
            _typeFilter.ForeColor = TwoPointTheme.BodyText;
            _typeFilter.Font = TwoPointTheme.BodyFont(8.8F);
            filters.Controls.Add(_typeFilter, 1, 1);
            PopulateTypeFilter();

            _members = new CheckedListBox();
            _members.Dock = DockStyle.Fill;
            _members.CheckOnClick = true;
            _members.IntegralHeight = false;
            _members.BackColor = TwoPointTheme.FieldBackground;
            _members.ForeColor = TwoPointTheme.BodyText;
            _members.BorderStyle = BorderStyle.FixedSingle;
            _members.Font = TwoPointTheme.BodyFont(9F);
            root.Controls.Add(_members, 0, 3);

            _members.ItemCheck += delegate(object sender, ItemCheckEventArgs e)
            {
                if (_rebuildingList || e.Index < 0 || e.Index >= _members.Items.Count)
                    return;
                MemberItem item = _members.Items[e.Index] as MemberItem;
                if (item == null || item.Record == null || string.IsNullOrEmpty(item.Record.ModId))
                    return;
                if (e.NewValue == CheckState.Checked)
                    _checkedIds.Add(item.Record.ModId);
                else
                    _checkedIds.Remove(item.Record.ModId);
            };

            _searchBox.TextChanged += delegate { ApplyFilter(); };
            _typeFilter.SelectedIndexChanged += delegate { ApplyFilter(); };
            ApplyFilter();

            FlowLayoutPanel quick = new FlowLayoutPanel();
            quick.AutoSize = true;
            quick.WrapContents = false;
            quick.FlowDirection = FlowDirection.LeftToRight;
            quick.Margin = new Padding(0, 10, 0, 8);
            root.Controls.Add(quick, 0, 4);

            Button selectAll = new Button();
            selectAll.Text = "Select All Shown";
            selectAll.Size = new Size(118, 30);
            TwoPointTheme.StyleButton(selectAll);
            selectAll.Click += delegate
            {
                _rebuildingList = true;
                try
                {
                    for (int i = 0; i < _members.Items.Count; i++)
                    {
                        MemberItem item = _members.Items[i] as MemberItem;
                        if (item != null && item.Record != null && !string.IsNullOrEmpty(item.Record.ModId))
                        {
                            _checkedIds.Add(item.Record.ModId);
                            _members.SetItemChecked(i, true);
                        }
                    }
                }
                finally { _rebuildingList = false; }
            };
            quick.Controls.Add(selectAll);

            Button clear = new Button();
            clear.Text = "Clear Shown";
            clear.Size = new Size(100, 30);
            TwoPointTheme.StyleButton(clear);
            clear.Click += delegate
            {
                _rebuildingList = true;
                try
                {
                    for (int i = 0; i < _members.Items.Count; i++)
                    {
                        MemberItem item = _members.Items[i] as MemberItem;
                        if (item != null && item.Record != null && !string.IsNullOrEmpty(item.Record.ModId))
                        {
                            _checkedIds.Remove(item.Record.ModId);
                            _members.SetItemChecked(i, false);
                        }
                    }
                }
                finally { _rebuildingList = false; }
            };
            quick.Controls.Add(clear);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.AutoSize = true;
            actions.Dock = DockStyle.Right;
            actions.WrapContents = false;
            actions.FlowDirection = FlowDirection.LeftToRight;
            actions.Margin = new Padding(0);
            root.Controls.Add(actions, 0, 5);

            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.Size = new Size(100, 34);
            TwoPointTheme.StyleButton(cancel);
            cancel.DialogResult = DialogResult.Cancel;
            actions.Controls.Add(cancel);

            Button save = new Button();
            save.Text = "Save Family";
            save.Size = new Size(120, 34);
            TwoPointTheme.StylePrimaryButton(save);
            save.Click += delegate
            {
                SelectedModIds.Clear();
                foreach (string id in _checkedIds)
                    SelectedModIds.Add(id);
                DialogResult = DialogResult.OK;
                Close();
            };
            actions.Controls.Add(save);

            AcceptButton = save;
            CancelButton = cancel;
        }

        private void PopulateTypeFilter()
        {
            _typeFilter.Items.Clear();
            _typeFilter.Items.Add("All Types");
            SortedSet<string> types = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _allItems.Count; i++)
            {
                string type = _allItems[i] == null ? "" : (_allItems[i].ItemType ?? "");
                if (!string.IsNullOrEmpty(type))
                    types.Add(type);
            }
            foreach (string type in types)
                _typeFilter.Items.Add(type);
            _typeFilter.SelectedIndex = 0;
        }

        private void ApplyFilter()
        {
            if (_members == null || _searchBox == null || _typeFilter == null)
                return;

            string search = (_searchBox.Text ?? "").Trim();
            string type = _typeFilter.SelectedItem as string ?? "All Types";
            _rebuildingList = true;
            try
            {
                _members.BeginUpdate();
                _members.Items.Clear();
                for (int i = 0; i < _allItems.Count; i++)
                {
                    MemberItem item = _allItems[i];
                    if (item == null || item.Record == null || string.IsNullOrEmpty(item.Record.ModId))
                        continue;
                    string name = item.Record.Name ?? "";
                    string itemType = item.ItemType ?? "";
                    bool searchMatch = string.IsNullOrEmpty(search) ||
                        name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        itemType.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (item.Record.Template ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
                    bool typeMatch = string.Equals(type, "All Types", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(itemType, type, StringComparison.OrdinalIgnoreCase);
                    if (!searchMatch || !typeMatch)
                        continue;
                    _members.Items.Add(item, _checkedIds.Contains(item.Record.ModId));
                }
            }
            finally
            {
                _members.EndUpdate();
                _rebuildingList = false;
            }
        }
    }
}

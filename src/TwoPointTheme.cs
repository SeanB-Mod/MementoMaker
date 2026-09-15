using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TPMSimpleModMaker
{
    internal static class TwoPointTheme
    {
        public static readonly Color Backdrop = Color.FromArgb(8, 47, 61);
        public static readonly Color FolderMain = Color.FromArgb(243, 211, 143);
        public static readonly Color FolderHighlight = Color.FromArgb(255, 229, 166);
        public static readonly Color ContentBackground = Color.FromArgb(246, 229, 195);
        public static readonly Color PanelLight = Color.FromArgb(250, 237, 210);
        public static readonly Color FieldBackground = Color.FromArgb(255, 249, 236);
        public static readonly Color Border = Color.FromArgb(214, 179, 110);
        public static readonly Color PrimaryText = Color.FromArgb(23, 90, 122);
        public static readonly Color BodyText = Color.FromArgb(73, 66, 56);
        public static readonly Color SectionBlue = Color.FromArgb(8, 121, 168);
        public static readonly Color SelectionBlue = Color.FromArgb(7, 150, 208);
        public static readonly Color PrimaryGreen = Color.FromArgb(120, 201, 0);
        public static readonly Color PrimaryGreenDark = Color.FromArgb(82, 149, 0);
        public static readonly Color DangerRed = Color.FromArgb(216, 73, 44);
        public static readonly Color DangerRedDark = Color.FromArgb(164, 50, 32);
        public static readonly Color LogBackground = Color.FromArgb(7, 58, 73);
        public static readonly Color LogText = Color.FromArgb(231, 233, 228);
        public static readonly Color SecondaryButton = Color.FromArgb(243, 205, 132);
        public static readonly Color SecondaryButtonHover = Color.FromArgb(250, 218, 155);
        public static readonly Color SecondaryButtonPressed = Color.FromArgb(218, 174, 92);
        public static readonly Color TabIconOrange = Color.FromArgb(184, 103, 24);
        public static readonly Size BottomActionButtonSize = new Size(150, 40);
        public const float BottomActionFontSize = 9F;

        public static Font BodyFont(float size)
        {
            return new Font("Trebuchet MS", size, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static Font BoldFont(float size)
        {
            return new Font("Trebuchet MS", size, FontStyle.Bold, GraphicsUnit.Point);
        }

        public static void InstallShell(Form form, string activeTab, EventHandler createAction, EventHandler modsAction, EventHandler settingsAction)
        {
            form.BackColor = Backdrop;
            form.Font = BodyFont(9.25F);

            FolderBackdropPanel backdrop = new FolderBackdropPanel();
            backdrop.Name = "TwoPointFolderBackdrop";
            backdrop.Location = new Point(10, 10);
            backdrop.Size = new Size(Math.Max(100, form.ClientSize.Width - 20), Math.Max(100, form.ClientSize.Height - 20));
            backdrop.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            form.Controls.Add(backdrop);
            backdrop.SendToBack();

            ApplyApplicationIcon(form);

            Image headerLogo = LoadThemeImage("MM_Logo.png");
            if (headerLogo != null)
            {
                PictureBox logo = new PictureBox();
                logo.Name = "TwoPointShellLogo";
                logo.BackColor = Color.Transparent;
                logo.Image = headerLogo;
                logo.SizeMode = PictureBoxSizeMode.Zoom;
                logo.Size = new Size(270, 92);
                logo.Location = new Point(Math.Max(20, (backdrop.ClientSize.Width - logo.Width) / 2), 4);
                logo.Anchor = AnchorStyles.Top;
                backdrop.Controls.Add(logo);
                logo.BringToFront();
                backdrop.Resize += delegate
                {
                    logo.Left = Math.Max(20, (backdrop.ClientSize.Width - logo.Width) / 2);
                };
            }
            else
            {
                Label title = new Label();
                title.Name = "TwoPointShellTitle";
                title.Text = "Memento Maker";
                title.ForeColor = PrimaryText;
                title.BackColor = Color.Transparent;
                title.Font = BoldFont(16.5F);
                title.AutoSize = false;
                title.Location = new Point(20, 7);
                title.Size = new Size(Math.Max(100, backdrop.ClientSize.Width - 40), 34);
                title.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                title.TextAlign = ContentAlignment.MiddleCenter;
                backdrop.Controls.Add(title);
                title.BringToFront();
            }

            // Tabs are children of the folder backdrop rather than transparent sibling Buttons.
            // This gives their anti-aliased shoulders the correct cream/gold parent background and
            // lets the lower edge sit directly over the folder content baseline without seams.
            int tabWidth = 152;
            int tabHeight = 48;
            int tabX = 24;
            int tabY = 95 - tabHeight + 1;

            FolderTabButton create = NewTab("Create Mod", "tab_create.png", string.Equals(activeTab, "Create Mod", StringComparison.OrdinalIgnoreCase));
            create.Location = new Point(tabX, tabY);
            create.Size = new Size(tabWidth, tabHeight);
            if (createAction != null)
                create.Click += createAction;
            backdrop.Controls.Add(create);

            FolderTabButton mods = NewTab("My Mods", "tab_mods.png", string.Equals(activeTab, "My Mods", StringComparison.OrdinalIgnoreCase));
            mods.Location = new Point(tabX + tabWidth - 2, tabY);
            mods.Size = new Size(tabWidth, tabHeight);
            if (modsAction != null)
                mods.Click += modsAction;
            backdrop.Controls.Add(mods);

            FolderTabButton settings = NewTab("Settings", "tab_settings.png", string.Equals(activeTab, "Settings", StringComparison.OrdinalIgnoreCase));
            settings.Location = new Point(tabX + ((tabWidth - 2) * 2), tabY);
            settings.Size = new Size(tabWidth, tabHeight);
            if (settingsAction != null)
                settings.Click += settingsAction;
            backdrop.Controls.Add(settings);

            create.BringToFront();
            mods.BringToFront();
            settings.BringToFront();

            ApplyControlTree(form);
        }

        private static FolderTabButton NewTab(string text, string imageName, bool active)
        {
            FolderTabButton tab = new FolderTabButton();
            tab.Text = text;
            tab.Name = "TwoPointTab_" + text.Replace(" ", "");
            tab.IsActive = active;
            tab.TabImage = LoadThemeImage(imageName);
            tab.Cursor = Cursors.Hand;
            tab.TabStop = !active;
            return tab;
        }

        private static IEnumerable<FolderTabButton> EnumerateTabs(Control parent)
        {
            if (parent == null)
                yield break;

            foreach (Control control in parent.Controls)
            {
                FolderTabButton tab = control as FolderTabButton;
                if (tab != null)
                    yield return tab;

                foreach (FolderTabButton nested in EnumerateTabs(control))
                    yield return nested;
            }
        }

        public static void SetActiveTab(Form form, string activeTab)
        {
            if (form == null)
                return;

            foreach (FolderTabButton tab in EnumerateTabs(form))
            {
                tab.IsActive = string.Equals(tab.Text, activeTab, StringComparison.OrdinalIgnoreCase);
                tab.TabStop = !tab.IsActive;
                tab.Invalidate();
            }

            BringShellToFront(form);
        }

        public static void BringShellToFront(Form form)
        {
            if (form == null)
                return;

            foreach (FolderTabButton tab in EnumerateTabs(form))
                tab.BringToFront();
        }

        public static void ApplyApplicationIcon(Form form)
        {
            if (form == null)
                return;

            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Theme", "MM_Icon.ico");
                if (File.Exists(iconPath))
                    form.Icon = new Icon(iconPath);
            }
            catch
            {
                // Keep the executable's default icon if the optional themed icon cannot be loaded.
            }
        }

        public static Image LoadThemeImage(string fileName)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Theme", fileName);
                if (!File.Exists(path))
                    return null;

                using (Image source = Image.FromFile(path))
                    return new Bitmap(source);
            }
            catch
            {
                return null;
            }
        }

        public static void ShiftDirectControls(Form form, Control excluded, int dx, int dy)
        {
            List<Control> controls = new List<Control>();
            foreach (Control control in form.Controls)
            {
                if (control != excluded)
                    controls.Add(control);
            }

            foreach (Control control in controls)
            {
                control.Left += dx;
                control.Top += dy;
            }
        }

        private static Bitmap CreateSectionRibbon(int width, int height)
        {
            width = Math.Max(40, width);
            height = Math.Max(18, height);
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                int tip = Math.Max(9, Math.Min(16, height / 2));
                // Original Memento Maker ribbon: an inward cut/notch on the right.
                Point[] points = new Point[]
                {
                    new Point(0, 1),
                    new Point(width - 2, 1),
                    new Point(width - tip - 2, height / 2),
                    new Point(width - 2, height - 2),
                    new Point(0, height - 2)
                };

                Rectangle gradientBounds = new Rectangle(0, 0, width, height);
                using (LinearGradientBrush fill = new LinearGradientBrush(gradientBounds,
                    Color.FromArgb(48, 174, 222), SectionBlue, LinearGradientMode.Vertical))
                using (Pen border = new Pen(Color.FromArgb(5, 103, 150), 1F))
                {
                    g.FillPolygon(fill, points);
                    g.DrawLines(border, points);
                    g.DrawLine(border, points[points.Length - 1], points[0]);
                }

                // Carry the white highlight all the way to the outer end of the ribbon.
                // Previously it stopped before the right-hand notch, leaving a visibly unfinished strip.
                using (Pen highlight = new Pen(Color.FromArgb(110, 255, 255, 255), 1F))
                    g.DrawLine(highlight, 1, 2, Math.Max(1, width - 3), 2);
            }
            return bitmap;
        }

        public static void StyleSectionHeader(Label label)
        {
            if (label == null)
                return;

            label.AutoSize = false;
            label.Size = new Size(Math.Max(165, label.Width + 36), 29);
            // Let the transparent ribbon silhouette sit directly on the cream panel.
            // A solid blue label background masks the pointed ribbon edges.
            label.BackColor = Color.Transparent;
            if (label.BackgroundImage != null)
            {
                Image previous = label.BackgroundImage;
                label.BackgroundImage = null;
                previous.Dispose();
            }
            label.BackgroundImage = CreateSectionRibbon(label.Width, label.Height);
            label.BackgroundImageLayout = ImageLayout.None;
            label.ForeColor = Color.White;
            label.Font = BoldFont(10.5F);
            label.TextAlign = ContentAlignment.MiddleLeft;
            // Section headings use literal ampersands (e.g. "Build & Output"), not WinForms mnemonic markers.
            label.UseMnemonic = false;
            label.Padding = new Padding(10, 0, 8, 0);
        }

        public static void StyleSelectableButton(Button button, bool selected)
        {
            if (button == null)
                return;

            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.BackColor = selected ? Color.FromArgb(255, 250, 236) : Color.FromArgb(248, 231, 194);
            button.ForeColor = PrimaryText;
            button.Font = BoldFont(Math.Max(9F, button.Font.Size));
            button.FlatAppearance.BorderSize = selected ? 2 : 1;
            button.FlatAppearance.BorderColor = selected ? SelectionBlue : Border;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 244, 216);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(239, 215, 168);
            button.TextImageRelation = TextImageRelation.ImageAboveText;
            button.ImageAlign = ContentAlignment.MiddleCenter;
            button.TextAlign = ContentAlignment.BottomCenter;
            button.Padding = new Padding(4);
            button.Cursor = Cursors.Hand;
        }

        public static void StyleSegmentButton(Button button, bool selected)
        {
            if (button == null)
                return;

            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.BackColor = selected ? SelectionBlue : FieldBackground;
            button.ForeColor = selected ? Color.White : PrimaryText;
            button.Font = BoldFont(9F);
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = selected ? Color.FromArgb(5, 112, 158) : Border;
            button.FlatAppearance.MouseOverBackColor = selected ? Color.FromArgb(18, 166, 220) : Color.FromArgb(255, 244, 218);
            button.FlatAppearance.MouseDownBackColor = selected ? Color.FromArgb(5, 112, 158) : Color.FromArgb(235, 213, 171);
            button.Cursor = Cursors.Hand;
        }


        public static void ApplyBottomActionFont(Button button)
        {
            if (button == null)
                return;
            button.Font = BoldFont(BottomActionFontSize);
        }

        public static void StylePrimaryButton(Button button)
        {
            if (button == null)
                return;

            StyleButton(button);
            button.BackColor = PrimaryGreen;
            button.ForeColor = Color.White;
            button.Font = BoldFont(Math.Max(10F, button.Font.Size));
            button.FlatAppearance.BorderColor = PrimaryGreenDark;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(137, 218, 10);
            button.FlatAppearance.MouseDownBackColor = PrimaryGreenDark;
        }

        public static void StyleDangerButton(Button button)
        {
            if (button == null)
                return;

            StyleButton(button);
            button.BackColor = DangerRed;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = DangerRedDark;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(229, 88, 56);
            button.FlatAppearance.MouseDownBackColor = DangerRedDark;
        }

        public static void StyleButton(Button button)
        {
            if (button == null)
                return;

            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.BackColor = SecondaryButton;
            button.ForeColor = BodyText;
            button.Font = BoldFont(Math.Max(8.5F, button.Font.Size));
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(185, 139, 64);
            button.FlatAppearance.MouseOverBackColor = SecondaryButtonHover;
            button.FlatAppearance.MouseDownBackColor = SecondaryButtonPressed;
        }

        public static void StylePanel(Panel panel)
        {
            if (panel == null || panel is FolderBackdropPanel)
                return;

            panel.BackColor = PanelLight;
            panel.BorderStyle = BorderStyle.FixedSingle;
        }

        public static void StyleListView(ListView list)
        {
            if (list == null)
                return;

            list.BackColor = FieldBackground;
            list.ForeColor = PrimaryText;
            list.Font = BodyFont(9.25F);
            list.BorderStyle = BorderStyle.FixedSingle;

            if (string.Equals(list.AccessibleDescription, "TwoPointOwnerDraw", StringComparison.Ordinal))
                return;

            list.AccessibleDescription = "TwoPointOwnerDraw";
            list.OwnerDraw = true;
            list.DrawColumnHeader += delegate(object sender, DrawListViewColumnHeaderEventArgs e)
            {
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(105, 188, 218)))
                    e.Graphics.FillRectangle(fill, e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, BoldFont(9F), e.Bounds, Color.White,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };
            list.DrawItem += delegate(object sender, DrawListViewItemEventArgs e) { };
            list.DrawSubItem += delegate(object sender, DrawListViewSubItemEventArgs e)
            {
                bool selected = e.Item.Selected;
                Color fillColor = selected ? Color.FromArgb(222, 242, 249) : FieldBackground;
                Color textColor = selected ? Color.FromArgb(14, 78, 108) : PrimaryText;
                using (SolidBrush fill = new SolidBrush(fillColor))
                    e.Graphics.FillRectangle(fill, e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, list.Font, e.Bounds, textColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                if (selected && e.ColumnIndex == 0)
                {
                    using (Pen pen = new Pen(SelectionBlue, 2F))
                    {
                        Rectangle row = new Rectangle(e.Item.Bounds.Left + 1, e.Item.Bounds.Top + 1,
                            Math.Max(1, list.ClientSize.Width - 3), Math.Max(1, e.Item.Bounds.Height - 2));
                        e.Graphics.DrawRectangle(pen, row);
                    }
                }
            };
        }

        public static void ApplyControlTree(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is FolderBackdropPanel || control is FolderTabButton ||
                    control is TwoPointTextArea || control is TwoPointRadioButton || control is TwoPointSortDirectionButton)
                    continue;

                Button button = control as Button;
                if (button != null)
                {
                    StyleButton(button);
                }
                else
                {
                    TextBox textBox = control as TextBox;
                    if (textBox != null)
                    {
                        textBox.BackColor = FieldBackground;
                        textBox.ForeColor = BodyText;
                        textBox.Font = BodyFont(9.25F);
                    }
                    else
                    {
                        ComboBox combo = control as ComboBox;
                        if (combo != null)
                        {
                            combo.BackColor = FieldBackground;
                            combo.ForeColor = BodyText;
                            combo.Font = BodyFont(9.25F);
                            combo.FlatStyle = FlatStyle.Flat;
                        }
                        else
                        {
                            NumericUpDown numeric = control as NumericUpDown;
                            if (numeric != null)
                            {
                                numeric.BackColor = FieldBackground;
                                numeric.ForeColor = BodyText;
                                numeric.Font = BodyFont(9.25F);
                            }
                            else
                            {
                                ListView list = control as ListView;
                                if (list != null)
                                {
                                    StyleListView(list);
                                }
                                else
                                {
                                    RichTextBox rich = control as RichTextBox;
                                    if (rich != null)
                                    {
                                        rich.BackColor = LogBackground;
                                        rich.ForeColor = LogText;
                                    }
                                    else
                                    {
                                        Panel panel = control as Panel;
                                        if (panel != null)
                                            StylePanel(panel);
                                        else
                                        {
                                            Label label = control as Label;
                                            if (label != null && label.Name != "TwoPointShellTitle")
                                            {
                                                if (label.ForeColor == Color.DimGray || label.ForeColor == SystemColors.ControlText)
                                                    label.ForeColor = BodyText;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (control.HasChildren)
                    ApplyControlTree(control);
            }
        }

        public static DialogResult ShowMessage(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            using (ThemedMessageDialog dialog = new ThemedMessageDialog(text, caption, buttons, icon))
            {
                ApplyApplicationIcon(dialog);
                return owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
            }
        }

        public static DialogResult ShowMessage(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return ShowMessage(null, text, caption, buttons, icon);
        }

        public static void StyleContextMenu(ContextMenuStrip menu)
        {
            if (menu == null)
                return;

            menu.BackColor = PanelLight;
            menu.ForeColor = BodyText;
            menu.Font = BodyFont(9F);
            menu.Padding = new Padding(3);
            menu.RenderMode = ToolStripRenderMode.Professional;
            menu.Renderer = new ToolStripProfessionalRenderer(new TwoPointMenuColorTable());
        }

        internal static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class ThemedMessageDialog : Form
    {
        private readonly string _message;
        private readonly string _caption;
        private readonly MessageBoxButtons _buttons;
        private readonly MessageBoxIcon _icon;
        private FlowLayoutPanel _buttonPanel;

        public ThemedMessageDialog(string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            _message = message ?? string.Empty;
            _caption = string.IsNullOrWhiteSpace(caption) ? "Memento Maker" : caption;
            _buttons = buttons;
            _icon = icon;

            Text = "Memento Maker";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = TwoPointTheme.ContentBackground;
            Font = TwoPointTheme.BodyFont(9.5F);
            AutoScaleMode = AutoScaleMode.Dpi;

            BuildInterface();
        }

        private void BuildInterface()
        {
            const int width = 560;
            const int headerHeight = 58;
            const int footerHeight = 64;
            const int horizontalPadding = 18;
            const int iconWidth = 58;
            const int messageGap = 16;
            int textWidth = width - (horizontalPadding * 2) - iconWidth - messageGap - 4;

            Size measured = TextRenderer.MeasureText(_message, TwoPointTheme.BodyFont(9.5F), new Size(textWidth, 800),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            int bodyHeight = Math.Max(104, Math.Min(360, measured.Height + 34));
            ClientSize = new Size(width, headerHeight + bodyHeight + footerHeight);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Margin = new Padding(0);
            root.Padding = new Padding(0);
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, headerHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, footerHeight));
            Controls.Add(root);

            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.Margin = new Padding(0);
            header.BackColor = TwoPointTheme.SectionBlue;
            root.Controls.Add(header, 0, 0);

            Label brand = new Label();
            brand.Text = "Memento Maker";
            brand.ForeColor = Color.FromArgb(255, 229, 166);
            brand.Font = TwoPointTheme.BoldFont(9.5F);
            brand.AutoSize = true;
            brand.Location = new Point(16, 7);
            header.Controls.Add(brand);

            Label title = new Label();
            title.Text = _caption;
            title.ForeColor = Color.White;
            title.Font = TwoPointTheme.BoldFont(12F);
            title.AutoEllipsis = true;
            title.Location = new Point(15, 27);
            title.Size = new Size(width - 30, 25);
            header.Controls.Add(title);

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.Margin = new Padding(0);
            body.BackColor = TwoPointTheme.PanelLight;
            root.Controls.Add(body, 0, 1);

            TwoPointMessageIcon icon = new TwoPointMessageIcon();
            icon.MessageIcon = _icon;
            icon.Location = new Point(horizontalPadding, 18);
            icon.Size = new Size(iconWidth, iconWidth);
            body.Controls.Add(icon);

            Label message = new Label();
            message.Text = _message;
            message.ForeColor = TwoPointTheme.BodyText;
            message.Font = TwoPointTheme.BodyFont(9.5F);
            message.Location = new Point(horizontalPadding + iconWidth + messageGap, 17);
            message.Size = new Size(textWidth, Math.Max(68, bodyHeight - 30));
            message.TextAlign = ContentAlignment.TopLeft;
            message.AutoEllipsis = false;
            body.Controls.Add(message);

            Panel footer = new Panel();
            footer.Dock = DockStyle.Fill;
            footer.Margin = new Padding(0);
            footer.BackColor = TwoPointTheme.ContentBackground;
            footer.Padding = new Padding(10);
            root.Controls.Add(footer, 0, 2);

            _buttonPanel = new FlowLayoutPanel();
            _buttonPanel.Dock = DockStyle.Fill;
            _buttonPanel.FlowDirection = FlowDirection.RightToLeft;
            _buttonPanel.WrapContents = false;
            _buttonPanel.Padding = new Padding(0, 5, 2, 0);
            _buttonPanel.BackColor = TwoPointTheme.ContentBackground;
            footer.Controls.Add(_buttonPanel);

            AddButtons();
        }

        private bool IsDestructive()
        {
            string combined = (_caption + " " + _message).ToLowerInvariant();
            return combined.Contains("delete") || combined.Contains("unlink") ||
                   combined.Contains("remove") || combined.Contains("reset") ||
                   combined.Contains("discard");
        }

        private Button AddButton(string text, DialogResult result, bool primary, bool danger)
        {
            Button button = new Button();
            button.Text = text;
            button.DialogResult = result;
            button.Font = TwoPointTheme.BoldFont(9.5F);
            int desiredWidth = Math.Max(112, TextRenderer.MeasureText(text, button.Font).Width + 30);
            button.Size = new Size(desiredWidth, 36);
            button.Margin = new Padding(7, 0, 0, 0);
            if (danger)
                TwoPointTheme.StyleDangerButton(button);
            else if (primary)
                TwoPointTheme.StylePrimaryButton(button);
            else
                TwoPointTheme.StyleButton(button);
            _buttonPanel.Controls.Add(button);
            return button;
        }

        private string AffirmativeText()
        {
            string caption = (_caption ?? string.Empty).ToLowerInvariant();
            string message = (_message ?? string.Empty).ToLowerInvariant();
            if (caption.Contains("unsaved changes"))
                return _buttons == MessageBoxButtons.YesNo ? "Discard Changes" : "Save Changes";
            if (caption.Contains("queue rebuilds") || caption.Contains("rebuild environment")) return "Rebuild";
            if (caption.Contains("confirm steam workshop upload"))
                return message.Contains("update workshop item") ? "Update Workshop" : "Publish";
            if (caption.Contains("start new mod")) return "Start New Mod";
            if (caption.Contains("relink")) return "Relink";
            if (caption.Contains("unlink")) return "Unlink";
            if (caption.Contains("delete")) return "Delete";
            if (caption.Contains("workshop sync warning")) return "Continue";
            if (caption.Contains("workshop") && message.Contains("legal agreement") && message.Contains("open")) return "Open Agreement";
            if (caption.Contains("workshop") && message.Contains("open") && message.Contains("page")) return "Open Page";
            return "Yes";
        }

        private string NegativeText()
        {
            string caption = (_caption ?? string.Empty).ToLowerInvariant();
            string message = (_message ?? string.Empty).ToLowerInvariant();
            if (caption.Contains("unsaved changes"))
                return _buttons == MessageBoxButtons.YesNo ? "Keep Editing" : "Don't Save";
            if (caption.Contains("queue rebuilds") || caption.Contains("rebuild environment") ||
                caption.Contains("confirm steam workshop upload") || caption.Contains("start new mod") || caption.Contains("relink") ||
                caption.Contains("unlink") || caption.Contains("delete") || caption.Contains("workshop sync warning"))
                return "Cancel";
            if (caption.Contains("workshop") && message.Contains("legal agreement") && message.Contains("open")) return "Close";
            if (caption.Contains("workshop") && message.Contains("open") && message.Contains("page")) return "Close";
            return "No";
        }

        private void AddButtons()
        {
            bool destructive = IsDestructive();
            Button defaultButton = null;
            Button cancelButton = null;

            switch (_buttons)
            {
                case MessageBoxButtons.OK:
                    defaultButton = AddButton("OK", DialogResult.OK, true, false);
                    break;
                case MessageBoxButtons.OKCancel:
                    cancelButton = AddButton("Cancel", DialogResult.Cancel, false, true);
                    defaultButton = AddButton("OK", DialogResult.OK, !destructive, destructive);
                    break;
                case MessageBoxButtons.YesNo:
                    AddButton(NegativeText(), DialogResult.No, false, false);
                    defaultButton = AddButton(AffirmativeText(), DialogResult.Yes, !destructive, destructive);
                    break;
                case MessageBoxButtons.YesNoCancel:
                    cancelButton = AddButton("Cancel", DialogResult.Cancel, false, true);
                    AddButton(NegativeText(), DialogResult.No, false, false);
                    defaultButton = AddButton(AffirmativeText(), DialogResult.Yes, !destructive, destructive);
                    break;
                case MessageBoxButtons.RetryCancel:
                    cancelButton = AddButton("Cancel", DialogResult.Cancel, false, true);
                    defaultButton = AddButton("Retry", DialogResult.Retry, true, false);
                    break;
                case MessageBoxButtons.AbortRetryIgnore:
                    AddButton("Ignore", DialogResult.Ignore, false, false);
                    AddButton("Retry", DialogResult.Retry, true, false);
                    defaultButton = AddButton("Abort", DialogResult.Abort, false, true);
                    break;
                default:
                    defaultButton = AddButton("OK", DialogResult.OK, true, false);
                    break;
            }

            if (defaultButton != null)
                AcceptButton = defaultButton;
            if (cancelButton != null)
                CancelButton = cancelButton;
        }
    }

    internal sealed class ModdersNamePromptDialog : Form
    {
        private readonly TextBox _nameBox;
        private readonly Label _validation;

        public string ModdersName { get { return (_nameBox.Text ?? "").Trim(); } }

        public ModdersNamePromptDialog(string currentValue)
        {
            Text = "Memento Maker";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = TwoPointTheme.ContentBackground;
            Font = TwoPointTheme.BodyFont(9.5F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(540, 272);

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 60;
            header.BackColor = TwoPointTheme.SectionBlue;
            Controls.Add(header);

            Label brand = new Label();
            brand.Text = "Memento Maker";
            brand.ForeColor = Color.FromArgb(255, 229, 166);
            brand.Font = TwoPointTheme.BoldFont(9.5F);
            brand.AutoSize = true;
            brand.Location = new Point(16, 7);
            header.Controls.Add(brand);

            Label title = new Label();
            title.Text = "Modders Name Required";
            title.ForeColor = Color.White;
            title.Font = TwoPointTheme.BoldFont(12F);
            title.Location = new Point(15, 28);
            title.Size = new Size(500, 24);
            header.Controls.Add(title);

            Panel body = new Panel();
            body.Location = new Point(0, 60);
            body.Size = new Size(540, 148);
            body.BackColor = TwoPointTheme.PanelLight;
            Controls.Add(body);

            Label message = new Label();
            message.Text = "Enter the name you use for modding. Memento Maker will save it to Settings and continue the build automatically.";
            message.ForeColor = TwoPointTheme.BodyText;
            message.Font = TwoPointTheme.BodyFont(9.5F);
            message.Location = new Point(20, 15);
            message.Size = new Size(500, 42);
            body.Controls.Add(message);

            Label nameLabel = new Label();
            nameLabel.Text = "Modders Name";
            nameLabel.ForeColor = TwoPointTheme.PrimaryText;
            nameLabel.Font = TwoPointTheme.BoldFont(9F);
            nameLabel.Location = new Point(20, 65);
            nameLabel.Size = new Size(116, 24);
            body.Controls.Add(nameLabel);

            _nameBox = new TextBox();
            _nameBox.Text = currentValue ?? "";
            _nameBox.Location = new Point(140, 63);
            _nameBox.Size = new Size(380, 26);
            _nameBox.BorderStyle = BorderStyle.FixedSingle;
            _nameBox.BackColor = Color.White;
            _nameBox.ForeColor = TwoPointTheme.BodyText;
            body.Controls.Add(_nameBox);

            Label hint = new Label();
            hint.Text = "Used in generated .asset filenames. Special symbols/characters are ignored.";
            hint.ForeColor = TwoPointTheme.BodyText;
            hint.Font = TwoPointTheme.BodyFont(8.2F);
            hint.Location = new Point(140, 92);
            hint.Size = new Size(380, 20);
            body.Controls.Add(hint);

            _validation = new Label();
            _validation.Text = "";
            _validation.ForeColor = TwoPointTheme.DangerRed;
            _validation.Font = TwoPointTheme.BodyFont(8.2F);
            _validation.Location = new Point(140, 115);
            _validation.Size = new Size(380, 22);
            body.Controls.Add(_validation);

            Panel footer = new Panel();
            footer.Location = new Point(0, 208);
            footer.Size = new Size(540, 64);
            footer.BackColor = TwoPointTheme.ContentBackground;
            Controls.Add(footer);

            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.Size = new Size(112, 36);
            cancel.Location = new Point(286, 14);
            cancel.DialogResult = DialogResult.Cancel;
            TwoPointTheme.StyleDangerButton(cancel);
            footer.Controls.Add(cancel);
            CancelButton = cancel;

            Button save = new Button();
            save.Text = "Save & Continue";
            save.Size = new Size(122, 36);
            save.Location = new Point(406, 14);
            TwoPointTheme.StylePrimaryButton(save);
            save.Click += delegate
            {
                if (!AssetNamingService.HasUsableModdersName(ModdersName))
                {
                    _validation.Text = "Enter a name containing at least one letter or number.";
                    _nameBox.Focus();
                    _nameBox.SelectAll();
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            };
            footer.Controls.Add(save);
            AcceptButton = save;

            Shown += delegate
            {
                _nameBox.Focus();
                _nameBox.SelectAll();
            };
        }
    }

    internal sealed class TwoPointMessageIcon : Control
    {
        public MessageBoxIcon MessageIcon { get; set; }

        public TwoPointMessageIcon()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(4, 4, Math.Max(12, Width - 9), Math.Max(12, Height - 9));

            Color fill = TwoPointTheme.SectionBlue;
            string glyph = "i";
            bool triangle = false;
            if (MessageIcon == MessageBoxIcon.Warning || MessageIcon == MessageBoxIcon.Exclamation)
            {
                fill = Color.FromArgb(245, 170, 20);
                glyph = "!";
                triangle = true;
            }
            else if (MessageIcon == MessageBoxIcon.Error || MessageIcon == MessageBoxIcon.Hand || MessageIcon == MessageBoxIcon.Stop)
            {
                fill = TwoPointTheme.DangerRed;
                glyph = "×";
            }
            else if (MessageIcon == MessageBoxIcon.Question)
            {
                fill = TwoPointTheme.SelectionBlue;
                glyph = "?";
            }

            if (triangle)
            {
                Point[] pts = new Point[]
                {
                    new Point(r.Left + r.Width / 2, r.Top),
                    new Point(r.Right, r.Bottom),
                    new Point(r.Left, r.Bottom)
                };
                using (SolidBrush b = new SolidBrush(fill))
                    g.FillPolygon(b, pts);
                using (Pen p = new Pen(Color.FromArgb(184, 117, 0), 2F))
                    g.DrawPolygon(p, pts);
            }
            else
            {
                using (SolidBrush b = new SolidBrush(fill))
                    g.FillEllipse(b, r);
                using (Pen p = new Pen(Color.FromArgb(255, 255, 255, 150), 1.5F))
                    g.DrawEllipse(p, r);
            }

            Font font = TwoPointTheme.BoldFont(triangle ? 24F : 22F);
            try
            {
                Rectangle textRect = triangle ? new Rectangle(r.Left, r.Top + 10, r.Width, r.Height - 8) : r;
                TextRenderer.DrawText(g, glyph, font, textRect, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            finally
            {
                font.Dispose();
            }
        }
    }

    internal sealed class TwoPointMenuColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return TwoPointTheme.PanelLight; } }
        public override Color ImageMarginGradientBegin { get { return TwoPointTheme.PanelLight; } }
        public override Color ImageMarginGradientMiddle { get { return TwoPointTheme.PanelLight; } }
        public override Color ImageMarginGradientEnd { get { return TwoPointTheme.PanelLight; } }
        public override Color MenuItemSelected { get { return Color.FromArgb(220, 241, 249); } }
        public override Color MenuItemBorder { get { return TwoPointTheme.SelectionBlue; } }
        public override Color MenuItemPressedGradientBegin { get { return Color.FromArgb(220, 241, 249); } }
        public override Color MenuItemPressedGradientMiddle { get { return Color.FromArgb(220, 241, 249); } }
        public override Color MenuItemPressedGradientEnd { get { return Color.FromArgb(220, 241, 249); } }
        public override Color SeparatorDark { get { return TwoPointTheme.Border; } }
        public override Color SeparatorLight { get { return TwoPointTheme.FolderHighlight; } }
    }

    internal sealed class FolderBackdropPanel : Panel
    {
        public FolderBackdropPanel()
        {
            // ResizeRedraw is important here because the folder shell is custom-painted.
            // Without it, WinForms can leave the previously painted right/bottom borders
            // behind when the window grows, which appears as dark vertical/horizontal seams.
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            UpdateStyles();
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            // Force the entire shell to repaint rather than only the newly exposed strip.
            // This erases the old custom-drawn border/content edge at the previous size.
            Invalidate(true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Paint the complete panel background first. This prevents stale pixels around
            // rounded corners or previously drawn edges during rapid maximise/restore operations.
            e.Graphics.Clear(TwoPointTheme.Backdrop);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle shadowRect = new Rectangle(5, 7, Math.Max(1, Width - 11), Math.Max(1, Height - 13));
            using (GraphicsPath shadowPath = TwoPointTheme.RoundedRectangle(shadowRect, 16))
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
                g.FillPath(shadow, shadowPath);

            Rectangle folderRect = new Rectangle(1, 1, Math.Max(1, Width - 8), Math.Max(1, Height - 9));
            using (GraphicsPath folderPath = TwoPointTheme.RoundedRectangle(folderRect, 16))
            using (LinearGradientBrush fill = new LinearGradientBrush(folderRect, TwoPointTheme.FolderHighlight, TwoPointTheme.FolderMain, LinearGradientMode.Vertical))
            using (Pen border = new Pen(Color.FromArgb(183, 132, 54), 2F))
            {
                g.FillPath(fill, folderPath);
                g.DrawPath(border, folderPath);
            }

            Rectangle contentRect = new Rectangle(6, 95, Math.Max(1, Width - 14), Math.Max(1, Height - 106));
            using (GraphicsPath contentPath = TwoPointTheme.RoundedRectangle(contentRect, 12))
            using (SolidBrush content = new SolidBrush(TwoPointTheme.ContentBackground))
            using (Pen contentBorder = new Pen(Color.FromArgb(220, 184, 113), 1.2F))
            {
                g.FillPath(content, contentPath);
                g.DrawPath(contentBorder, contentPath);
            }

            // Do not draw a separate white top highlight. On some DPI/display combinations
            // it appears as a faint horizontal seam across the folder frame.
        }
    }

    internal sealed class TwoPointToggle : CheckBox
    {
        public TwoPointToggle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            AutoCheck = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(TwoPointTheme.PanelLight);

            Rectangle track = new Rectangle(1, 3, Math.Max(10, Width - 3), Math.Max(18, Height - 7));
            Color trackColor = Checked ? TwoPointTheme.PrimaryGreen : Color.FromArgb(162, 158, 148);
            Color borderColor = Checked ? TwoPointTheme.PrimaryGreenDark : Color.FromArgb(119, 116, 108);
            using (GraphicsPath path = TwoPointTheme.RoundedRectangle(track, Math.Max(8, track.Height / 2)))
            using (SolidBrush fill = new SolidBrush(trackColor))
            using (Pen border = new Pen(borderColor, 1.2F))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }

            int knob = Math.Max(16, track.Height - 4);
            int knobY = track.Top + ((track.Height - knob) / 2);
            int knobX = Checked ? track.Right - knob - 2 : track.Left + 2;
            using (SolidBrush knobFill = new SolidBrush(Color.FromArgb(246, 246, 238)))
            using (Pen knobBorder = new Pen(Color.FromArgb(95, 105, 96), 1F))
            {
                e.Graphics.FillEllipse(knobFill, knobX, knobY, knob, knob);
                e.Graphics.DrawEllipse(knobBorder, knobX, knobY, knob, knob);
            }

            Rectangle textBounds = Checked
                ? new Rectangle(track.Left + 8, track.Top, Math.Max(1, track.Width - knob - 14), track.Height)
                : new Rectangle(track.Left + knob + 7, track.Top, Math.Max(1, track.Width - knob - 12), track.Height);
            TextRenderer.DrawText(e.Graphics, Checked ? "ON" : "OFF", TwoPointTheme.BoldFont(8.5F),
                textBounds, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }


    internal sealed class TwoPointSlider : Control
    {
        private int _minimum;
        private int _maximum = 100;
        private int _value;
        private bool _dragging;

        public event EventHandler Scroll;

        public TwoPointSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            Size = new Size(180, 30);
        }

        public int Minimum
        {
            get { return _minimum; }
            set
            {
                _minimum = value;
                if (_maximum < _minimum)
                    _maximum = _minimum;
                Value = _value;
                Invalidate();
            }
        }

        public int Maximum
        {
            get { return _maximum; }
            set
            {
                _maximum = Math.Max(value, _minimum);
                Value = _value;
                Invalidate();
            }
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int next = Math.Max(_minimum, Math.Min(_maximum, value));
                if (_value == next)
                    return;
                _value = next;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(TwoPointTheme.PanelLight);

            int knobDiameter = Math.Max(18, Math.Min(24, Height - 4));
            int trackLeft = knobDiameter / 2;
            int trackRight = Math.Max(trackLeft + 1, Width - (knobDiameter / 2) - 1);
            int trackWidth = Math.Max(1, trackRight - trackLeft);
            int trackHeight = 9;
            int trackY = (Height - trackHeight) / 2;

            Rectangle track = new Rectangle(trackLeft, trackY, trackWidth, trackHeight);
            Color emptyTrackColor = Enabled ? Color.FromArgb(190, 174, 139) : Color.FromArgb(205, 202, 194);
            Color trackBorderColor = Enabled ? Color.FromArgb(160, 139, 98) : Color.FromArgb(174, 171, 164);
            using (GraphicsPath trackPath = TwoPointTheme.RoundedRectangle(track, trackHeight / 2))
            using (SolidBrush empty = new SolidBrush(emptyTrackColor))
            using (Pen trackBorder = new Pen(trackBorderColor, 1F))
            {
                g.FillPath(empty, trackPath);
                g.DrawPath(trackBorder, trackPath);
            }

            double span = Math.Max(1, _maximum - _minimum);
            double ratio = (_value - _minimum) / span;
            int knobCenterX = trackLeft + (int)Math.Round(trackWidth * ratio);

            if (knobCenterX > trackLeft)
            {
                Rectangle filled = new Rectangle(trackLeft, trackY, Math.Max(1, knobCenterX - trackLeft), trackHeight);
                using (GraphicsPath fillPath = TwoPointTheme.RoundedRectangle(filled, trackHeight / 2))
                using (LinearGradientBrush fill = new LinearGradientBrush(
                    filled,
                    Enabled ? Color.FromArgb(52, 185, 224) : Color.FromArgb(184, 184, 180),
                    Enabled ? TwoPointTheme.SelectionBlue : Color.FromArgb(158, 158, 154),
                    LinearGradientMode.Vertical))
                {
                    g.FillPath(fill, fillPath);
                }
            }

            Rectangle knob = new Rectangle(
                knobCenterX - (knobDiameter / 2),
                (Height - knobDiameter) / 2,
                knobDiameter,
                knobDiameter);

            using (LinearGradientBrush knobFill = new LinearGradientBrush(
                knob,
                Enabled ? Color.FromArgb(255, 221, 116) : Color.FromArgb(220, 219, 214),
                Enabled ? Color.FromArgb(239, 169, 48) : Color.FromArgb(184, 182, 176),
                LinearGradientMode.Vertical))
            using (Pen knobBorder = new Pen(Enabled ? Color.FromArgb(184, 116, 25) : Color.FromArgb(151, 149, 144), 1.4F))
            {
                g.FillEllipse(knobFill, knob);
                g.DrawEllipse(knobBorder, knob);
            }

            if (Focused)
            {
                using (Pen focus = new Pen(TwoPointTheme.SelectionBlue, 1F))
                {
                    focus.DashStyle = DashStyle.Dot;
                    g.DrawRectangle(focus, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            _dragging = true;
            SetValueFromX(e.X, true);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
                SetValueFromX(e.X, true);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int step = Math.Max(1, (_maximum - _minimum) / 100);
            int direction = e.Delta >= 0 ? 1 : -1;
            SetUserValue(_value + (direction * step));
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down)
            {
                SetUserValue(_value - 1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up)
            {
                SetUserValue(_value + 1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Home)
            {
                SetUserValue(_minimum);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.End)
            {
                SetUserValue(_maximum);
                e.Handled = true;
            }
        }

        private void SetValueFromX(int x, bool userInitiated)
        {
            int knobDiameter = Math.Max(18, Math.Min(24, Height - 4));
            int left = knobDiameter / 2;
            int right = Math.Max(left + 1, Width - (knobDiameter / 2) - 1);
            int clamped = Math.Max(left, Math.Min(right, x));
            double ratio = (double)(clamped - left) / Math.Max(1, right - left);
            int next = _minimum + (int)Math.Round((_maximum - _minimum) * ratio);

            if (userInitiated)
                SetUserValue(next);
            else
                Value = next;
        }

        private void SetUserValue(int next)
        {
            int clamped = Math.Max(_minimum, Math.Min(_maximum, next));
            if (_value == clamped)
                return;

            _value = clamped;
            Invalidate();

            EventHandler handler = Scroll;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }


    internal sealed class TwoPointRadioButton : RadioButton
    {
        public TwoPointRadioButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            AutoSize = false;
            BackColor = Color.Transparent;
            ForeColor = TwoPointTheme.BodyText;
            Font = TwoPointTheme.BodyFont(9F);
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color background = Parent == null ? TwoPointTheme.PanelLight : Parent.BackColor;
            using (SolidBrush bg = new SolidBrush(background))
                g.FillRectangle(bg, ClientRectangle);

            int diameter = 16;
            Rectangle circle = new Rectangle(2, Math.Max(2, (Height - diameter) / 2), diameter, diameter);
            Color borderColor = Enabled ? TwoPointTheme.SectionBlue : Color.FromArgb(150, 150, 145);
            using (SolidBrush fill = new SolidBrush(TwoPointTheme.FieldBackground))
            using (Pen border = new Pen(borderColor, 2F))
            {
                g.FillEllipse(fill, circle);
                g.DrawEllipse(border, circle);
            }

            if (Checked)
            {
                Rectangle inner = new Rectangle(circle.X + 4, circle.Y + 4, circle.Width - 8, circle.Height - 8);
                using (SolidBrush selected = new SolidBrush(TwoPointTheme.SectionBlue))
                    g.FillEllipse(selected, inner);
            }

            Rectangle textBounds = new Rectangle(circle.Right + 7, 0, Math.Max(1, Width - circle.Right - 8), Height);
            TextRenderer.DrawText(g, Text ?? "", Font, textBounds,
                Enabled ? TwoPointTheme.BodyText : Color.FromArgb(145, 140, 132),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class TwoPointSortDirectionButton : Button
    {
        private bool _descending = true;

        public bool Descending
        {
            get { return _descending; }
            set
            {
                if (_descending == value)
                    return;
                _descending = value;
                Invalidate();
            }
        }

        public TwoPointSortDirectionButton()
        {
            Text = "";
            TabStop = true;
            Cursor = Cursors.Hand;
            TwoPointTheme.StyleButton(this);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            base.OnPaint(pevent);
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int width = Math.Min(17, Math.Max(11, ClientSize.Width - 14));
            int height = Math.Min(12, Math.Max(8, ClientSize.Height - 15));
            int cx = ClientSize.Width / 2;
            int cy = ClientSize.Height / 2;
            Point[] points;
            if (_descending)
            {
                points = new Point[]
                {
                    new Point(cx - width / 2, cy - height / 2),
                    new Point(cx + width / 2, cy - height / 2),
                    new Point(cx, cy + height / 2)
                };
            }
            else
            {
                points = new Point[]
                {
                    new Point(cx - width / 2, cy + height / 2),
                    new Point(cx + width / 2, cy + height / 2),
                    new Point(cx, cy - height / 2)
                };
            }

            using (SolidBrush arrow = new SolidBrush(TwoPointTheme.SectionBlue))
                g.FillPolygon(arrow, points);
        }
    }

    internal sealed class TwoPointTextArea : UserControl
    {
        private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
        private const int EM_GETLINECOUNT = 0x00BA;
        private const int EM_LINESCROLL = 0x00B6;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private readonly TextBox _box;
        private readonly TwoPointVerticalScrollBar _scrollBar;
        private bool _syncing;

        public TwoPointTextArea()
        {
            BackColor = TwoPointTheme.FieldBackground;
            BorderStyle = BorderStyle.FixedSingle;
            Padding = new Padding(3, 3, 1, 3);
            TabStop = false;

            _box = new TextBox();
            _box.Multiline = true;
            _box.AcceptsReturn = true;
            _box.AcceptsTab = false;
            _box.WordWrap = true;
            _box.ScrollBars = ScrollBars.None;
            _box.BorderStyle = BorderStyle.None;
            _box.Dock = DockStyle.Fill;
            _box.BackColor = TwoPointTheme.FieldBackground;
            _box.ForeColor = TwoPointTheme.BodyText;
            _box.Font = TwoPointTheme.BodyFont(9.25F);
            Controls.Add(_box);

            _scrollBar = new TwoPointVerticalScrollBar();
            _scrollBar.Dock = DockStyle.Right;
            _scrollBar.Width = 18;
            _scrollBar.Margin = new Padding(2, 0, 0, 0);
            _scrollBar.Visible = false;
            Controls.Add(_scrollBar);
            _scrollBar.BringToFront();

            _box.TextChanged += delegate { SyncScrollBar(); };
            _box.KeyUp += delegate { SyncScrollBar(); };
            _box.MouseUp += delegate { SyncScrollBar(); };
            _box.MouseWheel += delegate
            {
                if (IsHandleCreated)
                    BeginInvoke((Action)SyncScrollBar);
            };
            _box.Resize += delegate { SyncScrollBar(); };
            _scrollBar.ValueChanged += delegate { ScrollToBarValue(); };
        }

        public override string Text
        {
            get { return _box == null ? base.Text : _box.Text; }
            set
            {
                if (_box == null)
                    base.Text = value;
                else
                    _box.Text = value ?? "";
            }
        }

        public bool ReadOnly
        {
            get { return _box.ReadOnly; }
            set { _box.ReadOnly = value; }
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            if (_box != null)
                _box.Font = Font;
            SyncScrollBar();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            SyncScrollBar();
        }

        private void SyncScrollBar()
        {
            if (_syncing || _box == null || !_box.IsHandleCreated)
                return;

            _syncing = true;
            try
            {
                int totalLines = SendMessage(_box.Handle, EM_GETLINECOUNT, IntPtr.Zero, IntPtr.Zero).ToInt32();
                int firstVisible = SendMessage(_box.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero).ToInt32();
                int lineHeight = Math.Max(1, _box.Font.Height);
                int visibleLines = Math.Max(1, _box.ClientSize.Height / lineHeight);
                int maximum = Math.Max(0, totalLines - visibleLines);

                _scrollBar.Maximum = maximum;
                _scrollBar.LargeChange = visibleLines;
                _scrollBar.Visible = maximum > 0;
                _scrollBar.Value = Math.Max(0, Math.Min(maximum, firstVisible));
            }
            finally
            {
                _syncing = false;
            }
        }

        private void ScrollToBarValue()
        {
            if (_syncing || _box == null || !_box.IsHandleCreated)
                return;

            int current = SendMessage(_box.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero).ToInt32();
            int delta = _scrollBar.Value - current;
            if (delta == 0)
                return;

            _syncing = true;
            try
            {
                SendMessage(_box.Handle, EM_LINESCROLL, IntPtr.Zero, new IntPtr(delta));
            }
            finally
            {
                _syncing = false;
            }
            _box.Invalidate();
        }
    }

    internal sealed class TwoPointVerticalScrollBar : Control
    {
        private int _maximum;
        private int _largeChange = 1;
        private int _value;
        private bool _dragging;
        private int _dragOffset;

        public event EventHandler ValueChanged;

        public TwoPointVerticalScrollBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            Width = 18;
            Cursor = Cursors.Hand;
            BackColor = TwoPointTheme.PanelLight;
        }

        public int Maximum
        {
            get { return _maximum; }
            set
            {
                _maximum = Math.Max(0, value);
                if (_value > _maximum)
                    _value = _maximum;
                Invalidate();
            }
        }

        public int LargeChange
        {
            get { return _largeChange; }
            set { _largeChange = Math.Max(1, value); Invalidate(); }
        }

        public int Value
        {
            get { return _value; }
            set { SetValue(value, false); }
        }

        private Rectangle TrackRectangle
        {
            get { return new Rectangle(3, 3, Math.Max(6, Width - 7), Math.Max(12, Height - 7)); }
        }

        private Rectangle ThumbRectangle
        {
            get
            {
                Rectangle track = TrackRectangle;
                if (_maximum <= 0)
                    return new Rectangle(track.Left, track.Top, track.Width, track.Height);
                int total = Math.Max(1, _maximum + _largeChange);
                int thumbHeight = Math.Max(28, (int)Math.Round(track.Height * ((double)_largeChange / total)));
                thumbHeight = Math.Min(track.Height, thumbHeight);
                int movable = Math.Max(0, track.Height - thumbHeight);
                int y = track.Top + (int)Math.Round(movable * ((double)_value / Math.Max(1, _maximum)));
                return new Rectangle(track.Left, y, track.Width, thumbHeight);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(TwoPointTheme.PanelLight);

            Rectangle track = TrackRectangle;
            using (GraphicsPath path = TwoPointTheme.RoundedRectangle(track, Math.Max(3, track.Width / 2)))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(231, 211, 171)))
            using (Pen border = new Pen(Color.FromArgb(205, 167, 98), 1F))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            if (_maximum <= 0)
                return;

            Rectangle thumb = ThumbRectangle;
            using (GraphicsPath path = TwoPointTheme.RoundedRectangle(thumb, Math.Max(3, thumb.Width / 2)))
            using (LinearGradientBrush fill = new LinearGradientBrush(thumb,
                Color.FromArgb(55, 184, 223), TwoPointTheme.SectionBlue, LinearGradientMode.Horizontal))
            using (Pen border = new Pen(Color.FromArgb(4, 105, 150), 1F))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_maximum <= 0)
                return;
            Rectangle thumb = ThumbRectangle;
            if (thumb.Contains(e.Location))
            {
                _dragging = true;
                _dragOffset = e.Y - thumb.Top;
                Capture = true;
            }
            else
            {
                SetValue(Value + (e.Y < thumb.Top ? -LargeChange : LargeChange), true);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging || _maximum <= 0)
                return;
            Rectangle track = TrackRectangle;
            Rectangle thumb = ThumbRectangle;
            int movable = Math.Max(1, track.Height - thumb.Height);
            int y = Math.Max(track.Top, Math.Min(track.Bottom - thumb.Height, e.Y - _dragOffset));
            double ratio = (double)(y - track.Top) / movable;
            SetValue((int)Math.Round(ratio * _maximum), true);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
            Capture = false;
        }

        private void SetValue(int value, bool notify)
        {
            int next = Math.Max(0, Math.Min(_maximum, value));
            if (_value == next)
                return;
            _value = next;
            Invalidate();
            if (notify && ValueChanged != null)
                ValueChanged(this, EventArgs.Empty);
        }
    }

    internal sealed class TwoPointHorizontalScrollBar : Control
    {
        private int _maximum;
        private int _largeChange = 1;
        private int _value;
        private bool _dragging;
        private int _dragOffset;

        public event EventHandler ValueChanged;

        public TwoPointHorizontalScrollBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            Height = 14;
            Cursor = Cursors.Hand;
            BackColor = TwoPointTheme.PanelLight;
        }

        public int Maximum
        {
            get { return _maximum; }
            set
            {
                _maximum = Math.Max(0, value);
                if (_value > _maximum)
                    _value = _maximum;
                Invalidate();
            }
        }

        public int LargeChange
        {
            get { return _largeChange; }
            set { _largeChange = Math.Max(1, value); Invalidate(); }
        }

        public int Value
        {
            get { return _value; }
            set { SetValue(value, false); }
        }

        private Rectangle TrackRectangle
        {
            get { return new Rectangle(3, 3, Math.Max(12, Width - 7), Math.Max(6, Height - 7)); }
        }

        private Rectangle ThumbRectangle
        {
            get
            {
                Rectangle track = TrackRectangle;
                if (_maximum <= 0)
                    return new Rectangle(track.Left, track.Top, track.Width, track.Height);
                int total = Math.Max(1, _maximum + _largeChange);
                int thumbWidth = Math.Max(28, (int)Math.Round(track.Width * ((double)_largeChange / total)));
                thumbWidth = Math.Min(track.Width, thumbWidth);
                int movable = Math.Max(0, track.Width - thumbWidth);
                int x = track.Left + (int)Math.Round(movable * ((double)_value / Math.Max(1, _maximum)));
                return new Rectangle(x, track.Top, thumbWidth, track.Height);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(TwoPointTheme.PanelLight);

            Rectangle track = TrackRectangle;
            using (GraphicsPath path = TwoPointTheme.RoundedRectangle(track, Math.Max(3, track.Height / 2)))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(231, 211, 171)))
            using (Pen border = new Pen(Color.FromArgb(205, 167, 98), 1F))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            if (_maximum <= 0)
                return;

            Rectangle thumb = ThumbRectangle;
            using (GraphicsPath path = TwoPointTheme.RoundedRectangle(thumb, Math.Max(3, thumb.Height / 2)))
            using (LinearGradientBrush fill = new LinearGradientBrush(thumb,
                Color.FromArgb(55, 184, 223), TwoPointTheme.SectionBlue, LinearGradientMode.Vertical))
            using (Pen border = new Pen(Color.FromArgb(4, 105, 150), 1F))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_maximum <= 0)
                return;
            Rectangle thumb = ThumbRectangle;
            if (thumb.Contains(e.Location))
            {
                _dragging = true;
                _dragOffset = e.X - thumb.Left;
                Capture = true;
            }
            else
            {
                SetValue(Value + (e.X < thumb.Left ? -LargeChange : LargeChange), true);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging || _maximum <= 0)
                return;
            Rectangle track = TrackRectangle;
            Rectangle thumb = ThumbRectangle;
            int movable = Math.Max(1, track.Width - thumb.Width);
            int x = Math.Max(track.Left, Math.Min(track.Right - thumb.Width, e.X - _dragOffset));
            double ratio = (double)(x - track.Left) / movable;
            SetValue((int)Math.Round(ratio * _maximum), true);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
            Capture = false;
        }

        private void SetValue(int value, bool notify)
        {
            int next = Math.Max(0, Math.Min(_maximum, value));
            if (_value == next)
                return;
            _value = next;
            Invalidate();
            if (notify && ValueChanged != null)
                ValueChanged(this, EventArgs.Empty);
        }
    }

    internal sealed class FolderTabButton : Control
    {
        private bool _hover;
        private bool _pressed;
        private bool _isActive;

        public bool IsActive
        {
            get { return _isActive; }
            set
            {
                if (_isActive == value)
                    return;
                _isActive = value;
                UpdateTabRegion();
                Invalidate();
            }
        }

        public Image TabImage { get; set; }

        public FolderTabButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable |
                     ControlStyles.SupportsTransparentBackColor, true);
            // Use a plain owner-drawn Control rather than Button/ButtonBase.  Native button
            // theming can add state-dependent edge pixels around a custom non-rectangular
            // surface on some Windows/DPI combinations (seen as red/green fringe pixels).
            // A plain Control gives us exclusive ownership of every pixel in the tab.
            BackColor = Color.Transparent;
            Font = TwoPointTheme.BoldFont(10.5F);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateTabRegion();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            _pressed = true;
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                _pressed = true;
                Invalidate();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                _pressed = false;
                Invalidate();
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyUp(e);
        }

        private GraphicsPath CreateTabPath(int inset)
        {
            inset = Math.Max(0, inset);
            int top = 1 + inset;
            // Extend the lower vertices beyond the visible control bounds.  The path is
            // clipped by the control window, which keeps the tab filled right down to the
            // shared folder baseline while moving the closed-path anti-aliased bottom edge
            // off-screen.  This avoids the one-pixel dark/coloured seam that could appear
            // immediately underneath the tabs at some DPI settings.
            int bottom = Math.Max(top + 8, Height + 3);
            int left = inset;
            int right = Math.Max(left + 20, Width - 1 - inset);
            int shoulder = 13;
            int curve = 8;

            GraphicsPath path = new GraphicsPath();
            path.StartFigure();
            path.AddLine(left, bottom, left + shoulder, top + curve);
            path.AddBezier(left + shoulder, top + curve,
                left + shoulder + 2, top + 2,
                left + shoulder + 5, top,
                left + shoulder + curve, top);
            path.AddLine(left + shoulder + curve, top, right - shoulder - curve, top);
            path.AddBezier(right - shoulder - curve, top,
                right - shoulder - 5, top,
                right - shoulder - 2, top + 2,
                right - shoulder, top + curve);
            path.AddLine(right - shoulder, top + curve, right, bottom);
            path.CloseFigure();
            return path;
        }

        private GraphicsPath CreateTabBorderPath(int inset)
        {
            inset = Math.Max(0, inset);
            int top = 1 + inset;
            // Extend the lower vertices beyond the visible control bounds.  The path is
            // clipped by the control window, which keeps the tab filled right down to the
            // shared folder baseline while moving the closed-path anti-aliased bottom edge
            // off-screen.  This avoids the one-pixel dark/coloured seam that could appear
            // immediately underneath the tabs at some DPI settings.
            int bottom = Math.Max(top + 8, Height + 3);
            int left = inset;
            int right = Math.Max(left + 20, Width - 1 - inset);
            int shoulder = 13;
            int curve = 8;

            // Deliberately leave the bottom edge open. The shared folder content baseline is
            // the visual lower edge for all three tabs, preventing duplicate underlines.
            GraphicsPath path = new GraphicsPath();
            path.StartFigure();
            path.AddLine(left, bottom, left + shoulder, top + curve);
            path.AddBezier(left + shoulder, top + curve,
                left + shoulder + 2, top + 2,
                left + shoulder + 5, top,
                left + shoulder + curve, top);
            path.AddLine(left + shoulder + curve, top, right - shoulder - curve, top);
            path.AddBezier(right - shoulder - curve, top,
                right - shoulder - 5, top,
                right - shoulder - 2, top + 2,
                right - shoulder, top + curve);
            path.AddLine(right - shoulder, top + curve, right, bottom);
            return path;
        }

        private void UpdateTabRegion()
        {
            // Deliberately keep the control rectangular.  The transparent background paints
            // the real FolderBackdropPanel underneath first, then the tab is drawn on top.
            // This allows GDI+ anti-aliasing to blend edge pixels against the correct opaque
            // cream/gold parent instead of clipping them through a Win32 HRGN boundary.
            // Region clipping was the other source of coloured fringe pixels on some displays.
            Region previous = Region;
            Region = null;
            if (previous != null)
                previous.Dispose();
        }

        private static void DrawTintedImage(Graphics graphics, Image image, Rectangle bounds, Color tint)
        {
            float r = tint.R / 255F;
            float g = tint.G / 255F;
            float b = tint.B / 255F;

            ColorMatrix matrix = new ColorMatrix(new float[][]
            {
                new float[] { r, 0, 0, 0, 0 },
                new float[] { 0, g, 0, 0, 0 },
                new float[] { 0, 0, b, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { 0, 0, 0, 0, 1 }
            });

            using (ImageAttributes attributes = new ImageAttributes())
            {
                attributes.SetColorMatrix(matrix);
                graphics.DrawImage(image, bounds, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // Let WinForms paint the parent through this transparent control first.  The tab
            // edges are then anti-aliased onto the *actual* folder background, which avoids
            // colour-key/native-button fringe pixels around the custom shape.
            base.OnPaintBackground(pevent);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int top = 1;
            int bottom = Math.Max(top + 8, Height - 2);
            Rectangle rect = new Rectangle(1, top, Math.Max(1, Width - 3), Math.Max(1, bottom - top + 1));

            Color topColor;
            Color bottomColor;
            Color textColor;
            if (IsActive)
            {
                topColor = Color.FromArgb(255, 252, 241);
                bottomColor = TwoPointTheme.PanelLight;
                textColor = TwoPointTheme.PrimaryText;
            }
            else
            {
                topColor = _hover ? Color.FromArgb(255, 231, 173) : Color.FromArgb(249, 216, 145);
                bottomColor = _pressed ? Color.FromArgb(224, 177, 91) : Color.FromArgb(239, 195, 112);
                textColor = Color.FromArgb(111, 73, 27);
            }

            // Keep the outline on whole-pixel geometry.  HighQuality pixel offsets can move a
            // one-pixel custom border onto fractional device pixels and create visible colour
            // fringing after Windows DPI composition.
            g.PixelOffsetMode = PixelOffsetMode.None;
            g.CompositingQuality = CompositingQuality.HighQuality;
            using (GraphicsPath fillPath = CreateTabPath(1))
            using (GraphicsPath borderPath = CreateTabBorderPath(1))
            using (LinearGradientBrush fill = new LinearGradientBrush(rect, topColor, bottomColor, LinearGradientMode.Vertical))
            using (Pen border = new Pen(IsActive ? TwoPointTheme.Border : Color.FromArgb(189, 134, 47), 1F))
            {
                border.LineJoin = LineJoin.Round;
                g.FillPath(fill, fillPath);
                g.DrawPath(border, borderPath);
            }

            int imageSize = TabImage == null ? 0 : 25;
            int contentWidth = imageSize + (imageSize > 0 ? 8 : 0) + TextRenderer.MeasureText(Text, Font).Width;
            int startX = Math.Max(14, (Width - contentWidth) / 2);
            int centerY = rect.Top + (rect.Height / 2) + 1;

            if (TabImage != null)
            {
                float scale = Math.Min((float)imageSize / Math.Max(1, TabImage.Width), (float)imageSize / Math.Max(1, TabImage.Height));
                int drawWidth = Math.Max(1, (int)Math.Round(TabImage.Width * scale));
                int drawHeight = Math.Max(1, (int)Math.Round(TabImage.Height * scale));
                Rectangle imageRect = new Rectangle(startX + ((imageSize - drawWidth) / 2), centerY - (drawHeight / 2), drawWidth, drawHeight);
                DrawTintedImage(g, TabImage, imageRect, TwoPointTheme.TabIconOrange);
                startX += imageSize + 8;
            }

            Rectangle textRect = new Rectangle(startX, rect.Top, Math.Max(1, Width - startX - 12), rect.Height);
            TextRenderer.DrawText(g, Text, Font, textRect, textColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }
}

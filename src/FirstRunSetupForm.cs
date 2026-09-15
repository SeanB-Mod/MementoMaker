using System;
using System.Drawing;
using System.Windows.Forms;

namespace TPMSimpleModMaker
{
    internal sealed class FirstRunSetupForm : Form
    {
        private readonly TextBox _moddersName;

        public bool OpenSettingsRequested { get; private set; }
        public string ModdersName { get { return (_moddersName.Text ?? "").Trim(); } }

        public FirstRunSetupForm(PrerequisiteState state, string currentModdersName)
        {
            Text = "Memento Maker - Welcome";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(760, 660);
            BackColor = TwoPointTheme.ContentBackground;
            Font = TwoPointTheme.BodyFont(9.25F);
            TwoPointTheme.ApplyApplicationIcon(this);

            _moddersName = new TextBox();
            BuildInterface(state ?? new PrerequisiteState(), currentModdersName ?? "");
        }

        private void BuildInterface(PrerequisiteState state, string currentModdersName)
        {
            PictureBox logo = new PictureBox();
            logo.Image = TwoPointTheme.LoadThemeImage("MM_Logo.png");
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.BackColor = Color.Transparent;
            logo.Location = new Point(205, 12);
            logo.Size = new Size(350, 122);
            Controls.Add(logo);

            Label welcome = new Label();
            welcome.Text = "Welcome to Memento Maker";
            welcome.Font = TwoPointTheme.BoldFont(15F);
            welcome.ForeColor = TwoPointTheme.PrimaryText;
            welcome.TextAlign = ContentAlignment.MiddleCenter;
            welcome.Location = new Point(40, 134);
            welcome.Size = new Size(680, 34);
            Controls.Add(welcome);

            Label intro = new Label();
            intro.Text = "Memento Maker has checked the components needed to create Two Point Museum mods. You can change these paths at any time in Settings.";
            intro.ForeColor = TwoPointTheme.BodyText;
            intro.Location = new Point(70, 174);
            intro.Size = new Size(620, 48);
            intro.TextAlign = ContentAlignment.TopCenter;
            Controls.Add(intro);

            Label modderLabel = new Label();
            modderLabel.Text = "Modders Name";
            modderLabel.Font = TwoPointTheme.BoldFont(9F);
            modderLabel.ForeColor = TwoPointTheme.PrimaryText;
            modderLabel.Location = new Point(115, 224);
            modderLabel.Size = new Size(125, 22);
            Controls.Add(modderLabel);

            _moddersName.Text = currentModdersName;
            _moddersName.Location = new Point(240, 220);
            _moddersName.Size = new Size(405, 26);
            _moddersName.BorderStyle = BorderStyle.FixedSingle;
            _moddersName.BackColor = Color.White;
            _moddersName.ForeColor = TwoPointTheme.PrimaryText;
            _moddersName.Font = TwoPointTheme.BodyFont(9F);
            Controls.Add(_moddersName);


            Label modderHint = new Label();
            modderHint.Text = "Used in generated .asset filenames. Special symbols/characters are ignored.";
            modderHint.ForeColor = TwoPointTheme.BodyText;
            modderHint.Font = TwoPointTheme.BodyFont(8.1F);
            modderHint.UseMnemonic = false;
            modderHint.AutoSize = false;
            modderHint.Location = new Point(115, 250);
            modderHint.Size = new Size(575, 38);
            Controls.Add(modderHint);

            Panel status = new Panel();
            status.Location = new Point(55, 289);
            status.Size = new Size(650, 205);
            status.BackColor = TwoPointTheme.PanelLight;
            status.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(status);

            AddStatus(status, 10, "Two Point Museum Modding SDK", state.SdkFound,
                state.SdkFound ? "Installed and detected." : "Not found. Install the Modding SDK from Steam.",
                state.SdkFound ? null : (EventHandler)delegate { PrerequisiteInstallService.PromptInstallSdk(this); });
            AddStatus(status, 58, "Unity Hub", state.UnityHubFound,
                state.UnityHubFound ? "Installed and detected." : "Not found. Install Unity Hub and activate a free Unity licence.",
                state.UnityHubFound ? null : (EventHandler)delegate { PrerequisiteInstallService.PromptInstallUnityHub(this); });
            AddStatus(status, 106, "Unity 2020.3.47f1", state.UnityFound,
                state.UnityFound ? "Supported Unity editor detected." : "Supported Unity version not found.",
                state.UnityFound ? null : (EventHandler)delegate { PrerequisiteInstallService.PromptInstallUnityEditor(this); });
            AddStatus(status, 154, "Private Modding Environment", state.EnvironmentReady,
                state.EnvironmentReady ? "Prepared and ready for builds." : "Will be prepared once Unity and the SDK are available.", null);

            bool ready = state.UnityFound && state.UnityHubFound && state.SdkFound && state.EnvironmentReady;
            Label summary = new Label();
            summary.Text = ready
                ? "✓ Setup complete. Memento Maker is ready to create mods."
                : "Setup needs attention before mods can be built.";
            summary.ForeColor = ready ? Color.FromArgb(58, 139, 37) : Color.FromArgb(178, 92, 24);
            summary.Font = TwoPointTheme.BoldFont(9.2F);
            summary.Location = new Point(55, 510);
            summary.Size = new Size(650, 24);
            Controls.Add(summary);

            Button primary = new Button();
            primary.Text = ready ? "Start Creating" : "Open Settings";
            primary.Size = new Size(145, 38);
            primary.Location = new Point(560, 604);
            TwoPointTheme.StylePrimaryButton(primary);
            primary.Click += delegate
            {
                if (!ValidateModdersName())
                    return;
                OpenSettingsRequested = !ready;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(primary);


            Button secondary = new Button();
            secondary.Text = ready ? "Open Settings" : "Continue for Now";
            secondary.Size = new Size(145, 38);
            secondary.Location = new Point(407, 604);
            TwoPointTheme.StyleButton(secondary);
            secondary.Click += delegate
            {
                if (!ValidateModdersName())
                    return;
                OpenSettingsRequested = ready;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(secondary);

        }

        private bool ValidateModdersName()
        {
            if (AssetNamingService.HasUsableModdersName(ModdersName))
                return true;

            TwoPointTheme.ShowMessage(this,
                "Enter a Modders Name containing at least one letter or number.\n\nMemento Maker uses it in generated .asset filenames so game errors can identify who created the mod.",
                "Modders Name Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _moddersName.Focus();
            return false;
        }

        private static void AddStatus(Panel parent, int y, string titleText, bool ok, string detailText, EventHandler installAction)
        {
            Label mark = new Label();
            mark.Text = ok ? "✓" : "!";
            mark.Font = TwoPointTheme.BoldFont(16F);
            mark.ForeColor = ok ? Color.FromArgb(82, 174, 34) : Color.FromArgb(225, 147, 8);
            mark.TextAlign = ContentAlignment.MiddleCenter;
            mark.Location = new Point(12, y - 3);
            mark.Size = new Size(34, 32);
            parent.Controls.Add(mark);

            Label title = new Label();
            title.Text = titleText;
            title.ForeColor = TwoPointTheme.PrimaryText;
            title.Font = TwoPointTheme.BoldFont(9F);
            title.Location = new Point(52, y);
            title.Size = new Size(250, 20);
            parent.Controls.Add(title);

            Label detail = new Label();
            detail.Text = detailText;
            detail.ForeColor = TwoPointTheme.BodyText;
            detail.Location = new Point(310, y);
            detail.Size = new Size(220, 40);
            parent.Controls.Add(detail);

            if (!ok && installAction != null)
            {
                Button install = new Button();
                install.Text = "Install";
                install.Size = new Size(88, 30);
                install.Location = new Point(540, y - 4);
                TwoPointTheme.StyleButton(install);
                install.Click += installAction;
                parent.Controls.Add(install);
            }
        }
    }
}

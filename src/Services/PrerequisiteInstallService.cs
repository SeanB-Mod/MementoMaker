using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace TPMSimpleModMaker
{
    internal static class PrerequisiteInstallService
    {
        public const string SdkInstallUri = "steam://nav/games/details/3457760";
        public const string UnityHubDownloadUrl = "https://unity3d.com/get-unity/download";
        public const string UnityEditorDownloadUrl = "https://unity.com/releases/editor/whats-new/2020.3.47f1";

        public static void PromptInstallSdk(IWin32Window owner)
        {
            PromptAndLaunch(owner,
                "Install the Modding SDK",
                "Install the Two Point Museum: Modding SDK from Steam.\n\n" + SdkInstallUri,
                "Open Steam now?",
                SdkInstallUri);
        }

        public static void PromptInstallUnityHub(IWin32Window owner)
        {
            PromptAndLaunch(owner,
                "Install the Unity Hub",
                "You’ll need to install the Unity Hub in order to start modding. Install Unity Hub from " + UnityHubDownloadUrl +
                "\n\nOnce you’ve downloaded the Unity Hub, launch it & create an account if necessary. You’ll need a (free) Unity licence in order to make mods in the Unity Editor.",
                "Open the Unity Hub download page now?",
                UnityHubDownloadUrl);
        }

        public static void PromptInstallUnityEditor(IWin32Window owner)
        {
            PromptAndLaunch(owner,
                "Install Unity 2020.3.47f1",
                "You'll need to install the supported Unity version from " + UnityEditorDownloadUrl +
                "\n\nDon't worry, once it's installed, you won't need to launch it yourself.",
                "Open the Unity 2020.3.47f1 download page now?",
                UnityEditorDownloadUrl);
        }

        private static void PromptAndLaunch(IWin32Window owner, string title, string message, string question, string target)
        {
            DialogResult result = TwoPointTheme.ShowMessage(owner,
                message + "\n\n" + question,
                title,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);
            if (result != DialogResult.Yes)
                return;

            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = target;
                info.UseShellExecute = true;
                Process.Start(info);
            }
            catch (Exception ex)
            {
                TwoPointTheme.ShowMessage(owner,
                    "Memento Maker could not open the installer link.\n\n" + target + "\n\n" + ex.Message,
                    "Open Install Link",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }
}

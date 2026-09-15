using System;
using System.Text;

namespace TPMSimpleModMaker
{
    internal static class AssetNamingService
    {
        public const int ItemNameCharacterLimit = 15;

        public static string SanitiseAssetComponent(string value)
        {
            return SanitiseAssetComponent(value, 0);
        }

        public static string SanitiseAssetComponent(string value, int maxCharacters)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            StringBuilder cleaned = new StringBuilder();
            bool previousWasSpace = false;
            foreach (char c in value.Trim())
            {
                if (char.IsLetterOrDigit(c))
                {
                    cleaned.Append(c);
                    previousWasSpace = false;
                }
                else if (char.IsWhiteSpace(c))
                {
                    if (cleaned.Length > 0 && !previousWasSpace)
                    {
                        cleaned.Append(' ');
                        previousWasSpace = true;
                    }
                }
                // All punctuation, symbols and other special characters are deliberately ignored.
            }

            string result = cleaned.ToString().Trim();
            if (maxCharacters > 0 && result.Length > maxCharacters)
                result = result.Substring(0, maxCharacters).Trim();

            result = result.Replace(' ', '_');
            while (result.Contains("__"))
                result = result.Replace("__", "_");
            return result.Trim('_');
        }

        public static bool HasUsableModdersName(string value)
        {
            return !string.IsNullOrEmpty(SanitiseAssetComponent(value));
        }

    }
}

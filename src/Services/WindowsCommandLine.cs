using System;
using System.Text;

namespace TPMSimpleModMaker
{
    internal static class WindowsCommandLine
    {
        // Quote one argument using the escaping rules consumed by CommandLineToArgvW and
        // the Microsoft C runtime. In particular, trailing backslashes must be doubled
        // before the closing quote or a path such as D:\ would swallow the quote.
        public static string QuoteArgument(string value)
        {
            value = value ?? string.Empty;
            StringBuilder builder = new StringBuilder(value.Length + 8);
            builder.Append('"');

            int backslashes = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (c == '"')
                {
                    AppendBackslashes(builder, backslashes * 2 + 1);
                    builder.Append('"');
                    backslashes = 0;
                    continue;
                }

                AppendBackslashes(builder, backslashes);
                backslashes = 0;
                builder.Append(c);
            }

            // Backslashes immediately before the closing quote must be doubled.
            AppendBackslashes(builder, backslashes * 2);
            builder.Append('"');
            return builder.ToString();
        }

        private static void AppendBackslashes(StringBuilder builder, int count)
        {
            for (int i = 0; i < count; i++)
                builder.Append('\\');
        }
    }
}

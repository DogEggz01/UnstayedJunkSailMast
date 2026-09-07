using System;
using System.Text;

namespace UnstayedJunkSailMast
{
    internal static class UnstayedNameRules
    {
        internal static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder result = new StringBuilder(value.Length);
            bool previousWasSpace = false;
            for (int i = 0; i < value.Length; i++)
            {
                char character = char.ToLowerInvariant(value[i]);
                if (char.IsLetterOrDigit(character))
                {
                    result.Append(character);
                    previousWasSpace = false;
                }
                else if (!previousWasSpace)
                {
                    result.Append(' ');
                    previousWasSpace = true;
                }
            }

            return result.ToString().Trim();
        }

        internal static bool IsNamedVariant(
            string normalizedName,
            string normalizedBaseName)
        {
            return normalizedName == normalizedBaseName ||
                   normalizedName.StartsWith(
                       normalizedBaseName + " ",
                       StringComparison.Ordinal);
        }

        internal static bool ContainsWord(string normalizedText, string word)
        {
            return (" " + normalizedText + " ").Contains(
                " " + word + " ");
        }

        internal static bool HasAbsenceToken(string normalizedText)
        {
            return ContainsWord(normalizedText, "no") ||
                   ContainsWord(normalizedText, "none") ||
                   ContainsWord(normalizedText, "without") ||
                   ContainsWord(normalizedText, "empty");
        }
    }
}

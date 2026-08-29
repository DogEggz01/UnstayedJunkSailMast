using System;
using System.Text;

namespace UnstayedJunkSailMast;

internal static class UnstayedNameRules
{
	internal static string Normalize(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder(value.Length);
		bool flag = false;
		for (int i = 0; i < value.Length; i++)
		{
			char c = char.ToLowerInvariant(value[i]);
			if (char.IsLetterOrDigit(c))
			{
				stringBuilder.Append(c);
				flag = false;
			}
			else if (!flag)
			{
				stringBuilder.Append(' ');
				flag = true;
			}
		}
		return stringBuilder.ToString().Trim();
	}

	internal static bool IsNamedVariant(string normalizedName, string normalizedBaseName)
	{
		if (!(normalizedName == normalizedBaseName))
		{
			return normalizedName.StartsWith(normalizedBaseName + " ", StringComparison.Ordinal);
		}
		return true;
	}

	internal static bool ContainsWord(string normalizedText, string word)
	{
		return (" " + normalizedText + " ").Contains(" " + word + " ");
	}

	internal static bool HasAbsenceToken(string normalizedText)
	{
		if (!ContainsWord(normalizedText, "no") && !ContainsWord(normalizedText, "none") && !ContainsWord(normalizedText, "without"))
		{
			return ContainsWord(normalizedText, "empty");
		}
		return true;
	}
}

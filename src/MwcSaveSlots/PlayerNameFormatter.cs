using System.Text;

namespace MwcSaveSlots
{
internal static class PlayerNameFormatter
{
	internal static string Format(string firstName, string lastName, string legacyName)
	{
		string first = Clean(firstName);
		string last = Clean(lastName);
		if (first.Length > 0 && last.Length > 0) return first + " " + last;
		if (first.Length > 0) return first;
		if (last.Length > 0) return last;

		string legacy = Clean(legacyName);
		return legacy.Length > 0 ? legacy : "PLAYER";
	}

	private static string Clean(string value)
	{
		if (string.IsNullOrEmpty(value)) return "";
		StringBuilder result = new StringBuilder(value.Length);
		bool pendingSpace = false;
		for (int i = 0; i < value.Length; i++)
		{
			char character = value[i];
			if (char.IsWhiteSpace(character))
			{
				pendingSpace = result.Length > 0;
				continue;
			}
			if (pendingSpace)
			{
				result.Append(' ');
				pendingSpace = false;
			}
			result.Append(character);
		}
		return result.ToString();
	}
}
}

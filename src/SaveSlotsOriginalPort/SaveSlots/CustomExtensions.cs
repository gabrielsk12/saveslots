namespace SaveSlots
{
internal static class CustomExtensions
{
	public static bool EqualsAny(this string lookIn, params string[] lookFor)
	{
		for (int i = 0; i < lookFor.Length; i++)
		{
			if (lookIn == lookFor[i])
			{
				return true;
			}
		}
		return false;
	}

	public static bool ContainsAny(this string lookIn, params string[] lookFor)
	{
		for (int i = 0; i < lookFor.Length; i++)
		{
			if (lookIn.Contains(lookFor[i]))
			{
				return true;
			}
		}
		return false;
	}
}
}


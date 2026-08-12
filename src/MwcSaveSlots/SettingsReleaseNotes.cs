using System;

namespace MwcSaveSlots
{
internal static class SettingsReleaseNotes
{
	private const string Notes =
		"### 4.0.0\n"
		+ "- Rebuilt Save Slots for My Winter Car.\n"
		+ "- Updated the SAVES menu and made each slot easier to read.\n"
		+ "- Improved backups and recovery if something goes wrong.\n"
		+ "- Existing profiles and settings still work.\n"
		+ "### 3.0.0\n"
		+ "- Added safer profile switching and emergency backups.\n"
		+ "### 2.0.0\n"
		+ "- Added Corris trim information and the SaveSlotsMWC folder.\n"
		+ "### 1.0.0\n"
		+ "- First My Winter Car release.\n";

	internal static string Build()
	{
		string[] lines = Notes.Split('\n');
		for (int i = 0; i < lines.Length; i++)
		{
			string line = lines[i];
			if (line.StartsWith("###", StringComparison.Ordinal))
				lines[i] = "<color=#00DCF4><size=24>" + line.Substring(3).Trim() + "</size></color>";
			else if (line.StartsWith("-", StringComparison.Ordinal))
				lines[i] = "- " + line.Substring(1).TrimStart();
		}
		return string.Join("\n", lines);
	}
}
}

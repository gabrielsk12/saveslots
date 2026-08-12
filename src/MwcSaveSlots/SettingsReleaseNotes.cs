using System;

namespace MwcSaveSlots
{
internal static class SettingsReleaseNotes
{
	private const string Notes =
		"### V 4.0 - Independent MWC Rebuild\n"
		+ "- Rebuilt the mod around an independent MWC-specific codebase while keeping existing profiles and settings compatible.\n"
		+ "- Added a native-looking SAVES entry and refreshed the three-profile menu.\n"
		+ "- Strengthened switching, deletion backups, rollback, and safe-mode recovery.\n"
		+ "- Improved profile information, thumbnails, performance, and diagnostics.\n"
		+ "- Made the save screen clearer and added quiet camera and button feedback.\n"
		+ "- Improved full player-name display, card readability, and native-style menu motion.\n"
		+ "- Added the square ModLoader icon and a monochrome image for saves without screenshots.\n"
		+ "### V 3.0 - Save Safety Update\n"
		+ "- Added verified switching, emergency backups, rollback, and safe mode.\n"
		+ "- Improved empty slots, Continue behaviour, thumbnails, and folder shortcuts.\n"
		+ "### V 2.0 - MWC Compatibility Update\n"
		+ "- Added Corris trim information and dedicated SaveSlotsMWC storage.\n"
		+ "- Improved compatibility, menu behaviour, backups, and diagnostics.\n"
		+ "### V 1.0 - Beta\n"
		+ "- First My Winter Car release with three profiles, thumbnails, shared options, and emergency backups.\n";

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

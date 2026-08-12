using System;
using System.IO;

namespace MwcSaveSlots
{
internal sealed class PendingSaveReceipt
{
	private const string FileName = "PendingSave.txt";
	private readonly string path;

	internal PendingSaveReceipt(string storageRoot)
	{
		path = Path.Combine(storageRoot, FileName);
	}

	internal bool Exists { get { return File.Exists(path); } }
	internal string PathName { get { return path; } }

	internal void Mark(string slotName)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		string value = ProfileRepository.NormalizeSlot(slotName) + Environment.NewLine
			+ DateTime.UtcNow.ToString("O") + Environment.NewLine;
		string temporary = path + ".new";
		File.WriteAllText(temporary, value);
		ReplaceFile(temporary, path);
	}

	internal string ReadSlot()
	{
		if (!File.Exists(path)) return null;
		string[] lines = File.ReadAllLines(path);
		return lines.Length == 0 ? null : ProfileRepository.NormalizeSlot(lines[0]);
	}

	internal void Clear()
	{
		DeleteIfExists(path);
		DeleteIfExists(path + ".new");
		DeleteIfExists(path + ".old");
	}

	private static void ReplaceFile(string temporary, string target)
	{
		string backup = target + ".old";
		DeleteIfExists(backup);
		if (!File.Exists(target))
		{
			File.Move(temporary, target);
			return;
		}
		try
		{
			File.Replace(temporary, target, backup);
			DeleteIfExists(backup);
			return;
		}
		catch (NotSupportedException) { }
		catch (NotImplementedException) { }

		File.Move(target, backup);
		try
		{
			File.Move(temporary, target);
			DeleteIfExists(backup);
		}
		catch
		{
			if (!File.Exists(target) && File.Exists(backup)) File.Move(backup, target);
			throw;
		}
	}

	private static void DeleteIfExists(string file)
	{
		if (!File.Exists(file)) return;
		File.SetAttributes(file, FileAttributes.Normal);
		File.Delete(file);
	}
}
}

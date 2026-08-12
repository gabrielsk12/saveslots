using System;
using System.IO;

namespace MwcSaveSlots
{
internal static class DiagnosticWriter
{
	private const long MaxLogBytes = 1024 * 1024;
	private static readonly object Sync = new object();

	internal static string PathName
	{
		get { return Path.Combine(Path.Combine(RuntimePaths.SaveRoot, ProfileRepository.StorageFolderName), "SaveSlotsDebug.log"); }
	}

	internal static void Write(string area, string message)
	{
		try
		{
			lock (Sync)
			{
				string path = PathName;
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				Rotate(path);
				File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + area + "] " + (message ?? "") + Environment.NewLine);
			}
		}
		catch
		{
		}
	}

	internal static void Exception(string area, Exception ex)
	{
		Write(area, ex == null ? "Exception was null." : ex.ToString());
	}

	private static void Rotate(string path)
	{
		if (!File.Exists(path) || new FileInfo(path).Length < MaxLogBytes)
		{
			return;
		}
		string previous = path + ".old";
		if (File.Exists(previous))
		{
			File.Delete(previous);
		}
		File.Move(path, previous);
	}
}
}

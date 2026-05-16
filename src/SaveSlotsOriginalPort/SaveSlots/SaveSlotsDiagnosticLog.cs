using System;
using System.IO;
using UnityEngine;

namespace SaveSlots
{
internal static class SaveSlotsDiagnosticLog
{
	private const string LogFileName = "SaveSlotsDebug.log";

	private const long MaximumLogBytes = 512 * 1024;

	private static readonly object SyncRoot = new object();

	internal static string LogPath
	{
		get
		{
			string saveRoot = Application.persistentDataPath.Replace(Application.productName, "");
			return Path.Combine(Path.Combine(saveRoot, "SaveSlots"), LogFileName);
		}
	}

	internal static void Log(string action, string details)
	{
		try
		{
			lock (SyncRoot)
			{
				string path = LogPath;
				string directory = Path.GetDirectoryName(path);
				if (!Directory.Exists(directory))
				{
					Directory.CreateDirectory(directory);
				}
				RotateIfNeeded(path);
				string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + action + "] " + (details ?? "") + Environment.NewLine;
				File.AppendAllText(path, line);
			}
		}
		catch
		{
			// Logging must never break save loading or slot switching.
		}
	}

	internal static void LogException(string action, Exception ex)
	{
		Log(action, ex == null ? "Exception was null" : ex.ToString());
	}

	private static void RotateIfNeeded(string path)
	{
		if (!File.Exists(path))
		{
			return;
		}
		FileInfo fileInfo = new FileInfo(path);
		if (fileInfo.Length < MaximumLogBytes)
		{
			return;
		}
		string oldPath = path + ".old";
		if (File.Exists(oldPath))
		{
			File.Delete(oldPath);
		}
		File.Move(path, oldPath);
	}
}
}

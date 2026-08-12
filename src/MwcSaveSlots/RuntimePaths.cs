using System.IO;
using UnityEngine;

namespace MwcSaveSlots
{
internal static class RuntimePaths
{
	internal static string ActiveSavePath
	{
		get { return Path.GetFullPath(Application.persistentDataPath); }
	}

	internal static string SaveRoot
	{
		get
		{
			DirectoryInfo parent = Directory.GetParent(ActiveSavePath);
			return parent == null ? ActiveSavePath : parent.FullName;
		}
	}
}
}

using System.IO;
using System.Xml.Serialization;
using UnityEngine;

namespace SaveSlots
{
internal static class ModSave
{
	public static void Save<T>(string fileName, T data, string ignored)
	{
		string path = ResolvePath(fileName);
		string directory = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
		{
			Directory.CreateDirectory(directory);
		}

		using (FileStream stream = File.Create(path))
		{
			new XmlSerializer(typeof(T)).Serialize(stream, data);
		}
	}

	public static T Load<T>(string fileName, string ignored) where T : new()
	{
		string path = ResolvePath(fileName);
		if (!File.Exists(path))
		{
			return new T();
		}

		using (FileStream stream = File.OpenRead(path))
		{
			return (T)new XmlSerializer(typeof(T)).Deserialize(stream);
		}
	}

	private static string ResolvePath(string fileName)
	{
		string path = fileName.EndsWith(".xml") ? fileName : fileName + ".xml";
		if (Path.IsPathRooted(path))
		{
			return path;
		}
		return Path.Combine(Application.persistentDataPath, path);
	}
}
}

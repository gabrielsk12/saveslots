using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using UnityEngine;

namespace MwcSaveSlots
{
internal sealed class SaveMetadataReader
{
	private static readonly LocationPoint[] KnownLocations =
	{
		new LocationPoint(-1548.43f, 3.726f, 1187.219f, "PERAJARVI"),
		new LocationPoint(-779.5585f, 12.599f, -648.1967f, "LANDFILL"),
		new LocationPoint(-837.2703f, -2.319f, 506.7076f, "COTTAGE"),
		new LocationPoint(1565.755f, 5.349002f, 721.2099f, "REPAIRSHOP"),
		new LocationPoint(-161.1573f, -3.437f, 1025.414f, "CABIN"),
		new LocationPoint(-8.205001f, -0.2180009f, 11.973f, "HOME"),
		new LocationPoint(-654.719f, 4.384f, -1154.57f, "JAIL")
	};

	private readonly Dictionary<string, CacheEntry> cache = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
	private readonly Action<string, string> logger;

	internal SaveMetadataReader(Action<string, string> logger)
	{
		this.logger = logger;
	}

	internal ProfileCardModel ReadFolder(string folder, int number, bool selected, int dateFormat)
	{
		string normalized = Path.GetFullPath(folder);
		string saveFile = Path.Combine(normalized, "savefile.txt");
		if (!File.Exists(saveFile))
		{
			return CreateEmpty(number, ProfileRepository.SlotName(number), normalized, selected, dateFormat);
		}

		long stamp = ComposeStamp(saveFile, Path.Combine(normalized, "carparts.txt"), Path.Combine(normalized, "SaveSlots.xml"));
		CacheEntry existing;
		if (cache.TryGetValue(normalized, out existing) && existing.Stamp == stamp && existing.DateFormat == dateFormat && existing.Selected == selected)
		{
			return Clone(existing.Model);
		}

		ProfileCardModel model = CreateEmpty(number, ProfileRepository.SlotName(number), normalized, selected, dateFormat);
		model.HasSave = true;
		model.PlayerName = "PLAYER";
		model.Trim = ReadCorrisTrim(Path.Combine(normalized, "carparts.txt"));
		model.Location = "SAVED DATA";
		model.LastPlayed = ReadLastPlayed(normalized, File.GetLastWriteTime(saveFile));
		model.LastPlayedText = FormatDate(model.LastPlayed, dateFormat);

		try
		{
			string[] rawTags = ES2.GetTags(saveFile);
			HashSet<string> tags = new HashSet<string>(StringComparer.Ordinal);
			if (rawTags != null)
			{
				for (int i = 0; i < rawTags.Length; i++)
				{
					string tag = rawTags[i];
					if (tag != null)
					{
						int delimiter = tag.IndexOfAny(new[] { '[', '.', '/' });
						tags.Add(delimiter > 0 ? tag.Substring(0, delimiter) : tag);
					}
				}
			}
			ES2Settings settings = new ES2Settings();
			string firstName = LoadValue(saveFile, "PlayerFirstName", settings, tags, "");
			string lastName = LoadValue(saveFile, "PlayerLastName", settings, tags, "");
			string legacyName = LoadValue(saveFile, "PlayerName", settings, tags, "");
			model.PlayerName = PlayerNameFormatter.Format(firstName, lastName, legacyName);
			Log("Metadata", model.SlotName + " nameTags[first=" + tags.Contains("PlayerFirstName")
				+ ",last=" + tags.Contains("PlayerLastName") + ",legacy=" + tags.Contains("PlayerName")
				+ "] resolvedCharacters=" + model.PlayerName.Length + ".");
			model.Money = LoadValue(saveFile, "PlayerMoney", settings, tags, 0f);
			model.Mortal = LoadValue(saveFile, "PlayerPermaDeath", settings, tags, false);
			if (tags.Contains("PlayerTransform"))
			{
				Transform player = ES2.Load<Transform>(saveFile + "?tag=PlayerTransform", settings);
				if (player != null)
				{
					model.Location = ResolveLocation(player.position);
				}
			}
		}
		catch (Exception ex)
		{
			Log("Metadata", normalized + ": " + ex.Message);
		}

		cache[normalized] = new CacheEntry(stamp, dateFormat, selected, Clone(model));
		return model;
	}

	internal void Invalidate()
	{
		cache.Clear();
	}

	internal static string FormatDate(DateTime value, int format)
	{
		DateTime today = DateTime.Today;
		if (value.Date == today)
		{
			return "TODAY";
		}
		if (value.Date == today.AddDays(-1d))
		{
			return "YESTERDAY";
		}
		if (value <= new DateTime(1970, 1, 1))
		{
			return "NEVER";
		}
		switch (format)
		{
			case 1: return value.ToString("MM/dd/yyyy");
			case 2: return value.ToString("yyyy/MM/dd");
			case 3: return value.ToString("MMM dd, yyyy");
			default: return value.ToString("dd/MM/yyyy");
		}
	}

	internal static string ResolveLocation(Vector3 position)
	{
		if (position == Vector3.zero)
		{
			return "UNKNOWN";
		}
		LocationPoint closest = KnownLocations[0];
		float distance = Vector3.Distance(position, closest.Position);
		for (int i = 1; i < KnownLocations.Length; i++)
		{
			float candidate = Vector3.Distance(position, KnownLocations[i].Position);
			if (candidate < distance)
			{
				distance = candidate;
				closest = KnownLocations[i];
			}
		}
		return distance > 750f
			? "X " + Mathf.RoundToInt(position.x) + " / Z " + Mathf.RoundToInt(position.z)
			: closest.Name;
	}

	internal static string ReadCorrisTrim(string path)
	{
		try
		{
			if (!File.Exists(path))
			{
				return "UNKNOWN";
			}
			byte[] bytes;
			using (FileStream stream = File.OpenRead(path))
			{
				int count = (int)Math.Min(131072L, stream.Length);
				bytes = new byte[count];
				int offset = 0;
				while (offset < count)
				{
					int read = stream.Read(bytes, offset, count - offset);
					if (read <= 0) break;
					offset += read;
				}
			}
			string text = Encoding.GetEncoding(28591).GetString(bytes);
			int vin = text.IndexOf("VINGen4", StringComparison.OrdinalIgnoreCase);
			if (vin < 0) return "UNKNOWN";
			int version = text.IndexOf("Version", vin, StringComparison.OrdinalIgnoreCase);
			if (version < 0 || version - vin > 8192) return "UNKNOWN";
			int marker = text.IndexOf("string(", version, StringComparison.OrdinalIgnoreCase);
			if (marker < 0 || marker - version > 2048) return "UNKNOWN";
			int start = marker + 7;
			int end = text.IndexOf(')', start);
			if (end < start || end - start > 64) return "UNKNOWN";
			string raw = text.Substring(start, end - start).Trim(' ', '\0', '\"', '\'', '\r', '\n');
			if (raw.Length == 0) return "UNKNOWN";
			char code = char.ToUpperInvariant(raw[0]);
			switch (code)
			{
				case 'D': return "L";
				case 'E': return "LX";
				case 'G': return "SLX";
				case 'P': return "GT";
				default: return raw.ToUpperInvariant();
			}
		}
		catch
		{
			return "UNKNOWN";
		}
	}

	private static T LoadValue<T>(string file, string tag, ES2Settings settings, HashSet<string> tags, T fallback)
	{
		if (!tags.Contains(tag)) return fallback;
		try
		{
			T value = ES2.Load<T>(file + "?tag=" + tag, settings);
			object boxed = value;
			if (typeof(T) == typeof(string) && string.IsNullOrEmpty(boxed as string)) return fallback;
			return value;
		}
		catch
		{
			return fallback;
		}
	}

	private static DateTime ReadLastPlayed(string folder, DateTime fallback)
	{
		try
		{
			string path = Path.Combine(folder, "SaveSlots.xml");
			if (File.Exists(path))
			{
				XmlSerializer serializer = new XmlSerializer(typeof(LegacyProfileMarker));
				using (FileStream stream = File.OpenRead(path))
				{
					LegacyProfileMarker value = serializer.Deserialize(stream) as LegacyProfileMarker;
					if (value != null && value.lastPlayed > new DateTime(1970, 1, 1)) return value.lastPlayed;
				}
			}
		}
		catch { }
		return fallback;
	}

	private static long ComposeStamp(params string[] paths)
	{
		long value = 17;
		for (int i = 0; i < paths.Length; i++)
		{
			if (!File.Exists(paths[i])) continue;
			FileInfo info = new FileInfo(paths[i]);
			unchecked { value = (value * 397) ^ info.LastWriteTimeUtc.Ticks ^ info.Length; }
		}
		return value;
	}

	private static ProfileCardModel CreateEmpty(int number, string slot, string folder, bool selected, int dateFormat)
	{
		return new ProfileCardModel
		{
			Number = number,
			SlotName = string.IsNullOrEmpty(slot) ? ProfileRepository.SlotName(number) : slot,
			FolderPath = folder,
			IsSelected = selected,
			HasSave = false,
			PlayerName = "PLAYER",
			Trim = "UNKNOWN",
			Location = "UNKNOWN",
			LastPlayed = new DateTime(1970, 1, 1),
			LastPlayedText = FormatDate(new DateTime(1970, 1, 1), dateFormat)
		};
	}

	private static ProfileCardModel Clone(ProfileCardModel source)
	{
		return new ProfileCardModel
		{
			Number = source.Number,
			SlotName = source.SlotName,
			FolderPath = source.FolderPath,
			IsSelected = source.IsSelected,
			HasSave = source.HasSave,
			PlayerName = source.PlayerName,
			Trim = source.Trim,
			Money = source.Money,
			Mortal = source.Mortal,
			Location = source.Location,
			LastPlayed = source.LastPlayed,
			LastPlayedText = source.LastPlayedText
		};
	}

	private void Log(string area, string message)
	{
		if (logger != null) try { logger(area, message); } catch { }
	}

	private sealed class CacheEntry
	{
		internal readonly long Stamp;
		internal readonly int DateFormat;
		internal readonly bool Selected;
		internal readonly ProfileCardModel Model;
		internal CacheEntry(long stamp, int dateFormat, bool selected, ProfileCardModel model)
		{
			Stamp = stamp;
			DateFormat = dateFormat;
			Selected = selected;
			Model = model;
		}
	}

	private struct LocationPoint
	{
		internal readonly Vector3 Position;
		internal readonly string Name;
		internal LocationPoint(float x, float y, float z, string name)
		{
			Position = new Vector3(x, y, z);
			Name = name;
		}
	}
}
}

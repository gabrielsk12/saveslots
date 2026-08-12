using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MwcSaveSlots
{
internal sealed class ThumbnailService : IDisposable
{
	internal const int MaximumCustomImageBytes = 8 * 1024 * 1024;
	private readonly Dictionary<string, LoadedThumbnail> cache = new Dictionary<string, LoadedThumbnail>(StringComparer.OrdinalIgnoreCase);
	private Sprite placeholder;

	internal Sprite ForFolder(string folder)
	{
		return LoadForFolder(folder, true);
	}

	internal Sprite ExistingForFolder(string folder)
	{
		return LoadForFolder(folder, false);
	}

	private Sprite LoadForFolder(string folder, bool createPlaceholder)
	{
		string path = SelectPath(folder);
		if (path == null) return createPlaceholder ? Placeholder() : null;
		FileInfo info = new FileInfo(path);
		long stamp = info.LastWriteTimeUtc.Ticks ^ info.Length;
		LoadedThumbnail current;
		if (cache.TryGetValue(path, out current) && current.Stamp == stamp) return current.Sprite;
		if (current != null)
		{
			Destroy(current);
			cache.Remove(path);
		}
		try
		{
			if (info.Length <= 0 || info.Length > MaximumCustomImageBytes) return createPlaceholder ? Placeholder() : null;
			byte[] data = File.ReadAllBytes(path);
			Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
			if (!texture.LoadImage(data))
			{
				UnityEngine.Object.Destroy(texture);
				return createPlaceholder ? Placeholder() : null;
			}
			texture.name = "MwcSaveThumbnailTexture";
			texture.wrapMode = TextureWrapMode.Clamp;
			Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
			sprite.name = "MwcSaveThumbnailSprite";
			cache[path] = new LoadedThumbnail(stamp, texture, sprite);
			return sprite;
		}
		catch
		{
			return createPlaceholder ? Placeholder() : null;
		}
	}

	internal bool ValidateImage(string path)
	{
		if (!File.Exists(path)) return false;
		FileInfo info = new FileInfo(path);
		if (info.Length <= 0 || info.Length > MaximumCustomImageBytes) return false;
		byte[] bytes = File.ReadAllBytes(path);
		Texture2D probe = new Texture2D(2, 2, TextureFormat.ARGB32, false);
		try { return probe.LoadImage(bytes); }
		finally { UnityEngine.Object.Destroy(probe); }
	}

	internal void InvalidateFolder(string folder)
	{
		string normalized = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		List<string> remove = new List<string>();
		foreach (KeyValuePair<string, LoadedThumbnail> item in cache)
		{
			if (item.Key.StartsWith(normalized, StringComparison.OrdinalIgnoreCase))
			{
				Destroy(item.Value);
				remove.Add(item.Key);
			}
		}
		for (int i = 0; i < remove.Count; i++) cache.Remove(remove[i]);
	}

	public void Dispose()
	{
		foreach (LoadedThumbnail item in cache.Values) Destroy(item);
		cache.Clear();
		if (placeholder != null)
		{
			Texture texture = placeholder.texture;
			UnityEngine.Object.Destroy(placeholder);
			UnityEngine.Object.Destroy(texture);
			placeholder = null;
		}
	}

	private Sprite Placeholder()
	{
		if (placeholder != null) return placeholder;
		Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
		try
		{
			if (!texture.LoadImage(MwcAssetCatalog.FallbackThumbnail)) throw new IOException("Unity could not decode the fallback thumbnail.");
		}
		catch (Exception ex)
		{
			UnityEngine.Object.Destroy(texture);
			DiagnosticWriter.Exception("FallbackThumbnail", ex);
			return ProceduralPlaceholder();
		}
		texture.wrapMode = TextureWrapMode.Clamp;
		texture.name = "MwcSaveFallbackThumbnailTexture";
		placeholder = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
		placeholder.name = "MwcSaveFallbackThumbnailSprite";
		return placeholder;
	}

	private Sprite ProceduralPlaceholder()
	{
		const int width = 320;
		const int height = 180;
		Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
		Color32[] pixels = new Color32[width * height];
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				bool grid = x % 32 == 0 || y % 30 == 0;
				bool horizon = y == 89 || y == 90;
				byte blue = (byte)(38 + ((x + y) % 17));
				pixels[(y * width) + x] = horizon
					? new Color32(0, 220, 244, 255)
					: grid ? new Color32(53, 111, 171, 255) : new Color32(8, 27, blue, 255);
			}
		}
		texture.SetPixels32(pixels);
		texture.Apply(false, true);
		texture.name = "MwcSaveEmergencyPlaceholderTexture";
		placeholder = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), 100f);
		placeholder.name = "MwcSaveEmergencyPlaceholderSprite";
		return placeholder;
	}

	private static string SelectPath(string folder)
	{
		if (string.IsNullOrEmpty(folder)) return null;
		string png = Path.Combine(folder, "screenshot.png");
		if (File.Exists(png)) return png;
		string jpg = Path.Combine(folder, "screenshot.jpg");
		return File.Exists(jpg) ? jpg : null;
	}

	private static void Destroy(LoadedThumbnail thumbnail)
	{
		if (thumbnail.Sprite != null) UnityEngine.Object.Destroy(thumbnail.Sprite);
		if (thumbnail.Texture != null) UnityEngine.Object.Destroy(thumbnail.Texture);
	}

	private sealed class LoadedThumbnail
	{
		internal readonly long Stamp;
		internal readonly Texture2D Texture;
		internal readonly Sprite Sprite;
		internal LoadedThumbnail(long stamp, Texture2D texture, Sprite sprite)
		{
			Stamp = stamp;
			Texture = texture;
			Sprite = sprite;
		}
	}
}
}

using System;
using System.IO;
using System.Reflection;

namespace MwcSaveSlots
{
internal static class MwcAssetCatalog
{
	private const string LogoResource = "MwcSaveSlots.logo.png";
	private const string FallbackThumbnailResource = "MwcSaveSlots.fallback-thumbnail.png";
	private const string TransitionSoundResource = "MwcSaveSlots.transition-camera.wav";
	private const string UiClickSoundResource = "MwcSaveSlots.ui-button-click.wav";
	private static readonly object Sync = new object();
	private static byte[] logo;
	private static byte[] fallbackThumbnail;
	private static byte[] transitionSound;
	private static byte[] uiClickSound;

	internal static byte[] Logo { get { return ReadOnce(ref logo, LogoResource, "mod logo"); } }
	internal static byte[] FallbackThumbnail { get { return ReadOnce(ref fallbackThumbnail, FallbackThumbnailResource, "fallback thumbnail"); } }
	internal static byte[] TransitionSound { get { return ReadOnce(ref transitionSound, TransitionSoundResource, "transition camera sound"); } }
	internal static byte[] UiClickSound { get { return ReadOnce(ref uiClickSound, UiClickSoundResource, "UI click sound"); } }

	private static byte[] ReadOnce(ref byte[] cached, string resourceName, string description)
	{
		if (cached != null) return cached;
		lock (Sync)
		{
			if (cached != null) return cached;
			Assembly assembly = Assembly.GetExecutingAssembly();
			using (Stream stream = assembly.GetManifestResourceStream(resourceName))
			{
				if (stream == null) throw new InvalidOperationException("The author-supplied " + description + " is missing.");
				using (MemoryStream memory = new MemoryStream())
				{
					byte[] buffer = new byte[8192];
					int count;
					while ((count = stream.Read(buffer, 0, buffer.Length)) > 0) memory.Write(buffer, 0, count);
					cached = memory.ToArray();
				}
			}
			if (cached.Length == 0) throw new IOException("The author-supplied " + description + " is empty.");
			return cached;
		}
	}
}
}

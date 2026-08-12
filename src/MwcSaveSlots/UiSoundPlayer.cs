using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace MwcSaveSlots
{
internal sealed class UiSoundPlayer : MonoBehaviour
{
	private const float PlaybackVolume = .14f;
	private const float MinimumInterval = .06f;
	private static UiSoundPlayer current;
	private AudioSource source;
	private AudioClip clip;
	private float lastPlayed = -10f;

	internal static UiSoundPlayer Create()
	{
		if (current != null) return current;
		GameObject host = new GameObject("MwcSaveSlotsUiAudio");
		DontDestroyOnLoad(host);
		current = host.AddComponent<UiSoundPlayer>();
		current.source = host.AddComponent<AudioSource>();
		current.source.playOnAwake = false;
		current.source.volume = PlaybackVolume;
		current.StartCoroutine(current.LoadClip());
		return current;
	}

	internal static void PlayClick()
	{
		if (current == null || current.source == null || current.clip == null) return;
		if (Time.unscaledTime - current.lastPlayed < MinimumInterval) return;
		current.lastPlayed = Time.unscaledTime;
		current.source.PlayOneShot(current.clip);
	}

	private IEnumerator LoadClip()
	{
		string file = Path.Combine(Application.temporaryCachePath, "SaveSlotsMWC-ui-click.wav");
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(file));
			File.WriteAllBytes(file, MwcAssetCatalog.UiClickSound);
		}
		catch (Exception ex)
		{
			DiagnosticWriter.Exception("UiAudio", ex);
			yield break;
		}

		WWW request = null;
		try { request = new WWW("file:///" + Uri.EscapeUriString(Path.GetFullPath(file).Replace('\\', '/'))); }
		catch (Exception ex)
		{
			DiagnosticWriter.Exception("UiAudio", ex);
			yield break;
		}
		yield return request;

		AudioClip loaded = null;
		try
		{
			if (!string.IsNullOrEmpty(request.error)) throw new IOException(request.error);
			loaded = request.GetAudioClip(false, false, AudioType.WAV);
			if (loaded == null) throw new IOException("Unity returned no AudioClip for the UI click WAV.");
			loaded.name = "MwcSaveSlotsUiClick";
			clip = loaded;
			DiagnosticWriter.Write("UiAudio", "Loaded the credited DenielCZ/Pixabay click at volume=" + PlaybackVolume + ".");
		}
		catch (Exception ex)
		{
			if (loaded != null) Destroy(loaded);
			DiagnosticWriter.Exception("UiAudio", ex);
		}
		finally
		{
			if (request != null) request.Dispose();
			try { if (File.Exists(file)) File.Delete(file); } catch { }
		}
	}

	private void OnDestroy()
	{
		if (clip != null) Destroy(clip);
		if (current == this) current = null;
	}
}
}

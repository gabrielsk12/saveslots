using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace MwcSaveSlots
{
internal sealed class ShutterTransition : MonoBehaviour
{
	private RectTransform[] slats;
	private Image flash;
	private AudioSource audioSource;
	private bool running;

	internal void Build(Transform parent)
	{
		slats = new RectTransform[10];
		for (int i = 0; i < slats.Length; i++)
		{
			Image slat = UiPrimitives.Object("MwcTransitionSlat" + i, parent).AddComponent<Image>();
			slat.color = i % 2 == 0 ? UiPrimitives.MwcNavy : UiPrimitives.MwcPanel;
			RectTransform rect = slat.rectTransform;
			bool fromLeft = i % 2 == 0;
			rect.anchorMin = new Vector2(fromLeft ? 0f : 1f, i / 10f);
			rect.anchorMax = new Vector2(fromLeft ? 0f : 1f, (i + 1) / 10f);
			rect.pivot = new Vector2(fromLeft ? 0f : 1f, .5f);
			rect.sizeDelta = new Vector2(0f, 2f);
			rect.anchoredPosition = Vector2.zero;
			slats[i] = rect;
			slat.gameObject.SetActive(false);
		}

		flash = UiPrimitives.Object("MwcTransitionFlash", parent).AddComponent<Image>();
		flash.color = new Color(0f, 220f / 255f, 244f / 255f, 0f);
		UiPrimitives.Stretch(flash.rectTransform, 0f, 0f, 0f, 0f);
		flash.gameObject.SetActive(false);

		audioSource = gameObject.AddComponent<AudioSource>();
		audioSource.playOnAwake = false;
		audioSource.volume = .16f;
		audioSource.clip = CreateProceduralFallback();
		StartCoroutine(LoadCameraSound());
	}

	internal void Play(Action middle, Action completed)
	{
		if (!running) StartCoroutine(Animate(middle, completed));
	}

	private IEnumerator Animate(Action middle, Action completed)
	{
		running = true;
		for (int i = 0; i < slats.Length; i++) slats[i].gameObject.SetActive(true);
		flash.gameObject.SetActive(true);
		if (audioSource != null) audioSource.Play();

		float elapsed = 0f;
		while (elapsed < .34f)
		{
			elapsed += Time.unscaledDeltaTime;
			SetClosingProgress(elapsed / .34f);
			yield return null;
		}
		SetAllWidths(Screen.width + 32f);
		flash.color = new Color(0f, 220f / 255f, 244f / 255f, .14f);
		if (middle != null) middle();
		yield return null;

		elapsed = 0f;
		while (elapsed < .42f)
		{
			elapsed += Time.unscaledDeltaTime;
			SetOpeningProgress(elapsed / .42f);
			float alpha = Mathf.Lerp(.14f, 0f, UiPrimitives.EaseOutCubic(elapsed / .2f));
			flash.color = new Color(0f, 220f / 255f, 244f / 255f, alpha);
			yield return null;
		}

		for (int i = 0; i < slats.Length; i++) slats[i].gameObject.SetActive(false);
		flash.gameObject.SetActive(false);
		running = false;
		if (completed != null) completed();
	}

	private void SetClosingProgress(float overall)
	{
		float fullWidth = Screen.width + 32f;
		for (int i = 0; i < slats.Length; i++)
		{
			float delay = (i % 5) * .045f;
			float progress = Mathf.Clamp01((overall - delay) / (1f - (.045f * 4f)));
			slats[i].sizeDelta = new Vector2(fullWidth * UiPrimitives.EaseOutCubic(progress), 2f);
		}
	}

	private void SetOpeningProgress(float overall)
	{
		float fullWidth = Screen.width + 32f;
		for (int i = 0; i < slats.Length; i++)
		{
			float delay = ((slats.Length - 1 - i) % 5) * .04f;
			float progress = Mathf.Clamp01((overall - delay) / (1f - (.04f * 4f)));
			slats[i].sizeDelta = new Vector2(fullWidth * (1f - UiPrimitives.EaseInCubic(progress)), 2f);
		}
	}

	private void SetAllWidths(float width)
	{
		for (int i = 0; i < slats.Length; i++) slats[i].sizeDelta = new Vector2(width, 2f);
	}

	private IEnumerator LoadCameraSound()
	{
		string file = Path.Combine(Application.temporaryCachePath, "SaveSlotsMWC-transition-camera.wav");
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(file));
			File.WriteAllBytes(file, MwcAssetCatalog.TransitionSound);
		}
		catch (Exception ex)
		{
			DiagnosticWriter.Exception("TransitionAudio", ex);
			yield break;
		}

		WWW request = null;
		try { request = new WWW("file:///" + Uri.EscapeUriString(Path.GetFullPath(file).Replace('\\', '/'))); }
		catch (Exception ex)
		{
			DiagnosticWriter.Exception("TransitionAudio", ex);
			yield break;
		}
		yield return request;

		AudioClip loaded = null;
		try
		{
			if (!string.IsNullOrEmpty(request.error)) throw new IOException(request.error);
			loaded = request.GetAudioClip(false, false, AudioType.WAV);
			if (loaded == null) throw new IOException("Unity returned no AudioClip for the converted camera WAV.");
			loaded.name = "MwcSaveSlotsCameraTransition";
			AudioClip previous = audioSource == null ? null : audioSource.clip;
			if (audioSource != null) audioSource.clip = loaded;
			if (previous != null) Destroy(previous);
			DiagnosticWriter.Write("TransitionAudio", "Loaded the credited Pixabay camera sound at volume=" + (audioSource == null ? 0f : audioSource.volume) + ".");
		}
		catch (Exception ex)
		{
			if (loaded != null) Destroy(loaded);
			DiagnosticWriter.Exception("TransitionAudio", ex);
		}
		finally
		{
			if (request != null) request.Dispose();
			try { if (File.Exists(file)) File.Delete(file); } catch { }
		}
	}

	private static AudioClip CreateProceduralFallback()
	{
		const int sampleRate = 22050;
		const int count = 6615;
		float[] samples = new float[count];
		uint state = 0x4D574353u;
		for (int i = 0; i < count; i++)
		{
			state = (1664525u * state) + 1013904223u;
			uint noiseBits = (state >> 8) & 65535u;
			float noiseLevel = noiseBits / 32767.5f;
			float noise = noiseLevel - 1f;
			float time = i / (float)sampleRate;
			float envelope = Mathf.Exp(-time * 11f);
			float click = 0f;
			if (i < 1050)
			{
				float clickWave = Mathf.Sin(i * .19f);
				float clickProgress = i / 1050f;
				click = clickWave * (1f - clickProgress);
			}
			float low = Mathf.Sin(time * 190f * Mathf.PI * 2f) * Mathf.Exp(-time * 18f);
			samples[i] = ((noise * .16f) + (click * .42f) + (low * .18f)) * envelope;
		}
		AudioClip clip = AudioClip.Create("MwcSaveProceduralTransitionFallback", count, 1, sampleRate, false);
		clip.SetData(samples, 0);
		return clip;
	}

	private void OnDestroy()
	{
		if (audioSource != null && audioSource.clip != null) Destroy(audioSource.clip);
	}
}
}

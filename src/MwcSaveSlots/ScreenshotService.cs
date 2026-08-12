using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace MwcSaveSlots
{
internal sealed class ScreenshotService : MonoBehaviour
{
	private bool captureInProgress;

	internal bool Capture(string targetPath, bool highResolution, Action<bool> completed)
	{
		if (captureInProgress || Camera.main == null) return false;
		StartCoroutine(CaptureFrame(targetPath, highResolution, completed));
		return true;
	}

	internal bool CaptureImmediate(string targetPath, bool highResolution)
	{
		if (captureInProgress || Camera.main == null) return false;
		captureInProgress = true;
		try { return RenderToFile(targetPath, highResolution); }
		finally { captureInProgress = false; }
	}

	private IEnumerator CaptureFrame(string targetPath, bool highResolution, Action<bool> completed)
	{
		captureInProgress = true;
		yield return new WaitForEndOfFrame();
		bool success = false;
		try { success = RenderToFile(targetPath, highResolution); }
		finally
		{
			captureInProgress = false;
			if (completed != null) completed(success);
		}
	}

	private static bool RenderToFile(string targetPath, bool highResolution)
	{
		bool success = false;
		Camera camera = Camera.main;
		GameObject hud = FindHudSurface();
		bool hudWasActive = hud != null && hud.activeSelf;
		RenderTexture previous = null;
		RenderTexture render = null;
		Texture2D pixels = null;
		try
		{
			if (camera == null) throw new InvalidOperationException("No main camera is available.");
			if (hudWasActive) hud.SetActive(false);
			int width = highResolution ? 960 : 320;
			int height = highResolution ? 540 : 180;
			render = new RenderTexture(width, height, 24);
			previous = camera.targetTexture;
			camera.targetTexture = render;
			camera.Render();
			RenderTexture.active = render;
			pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
			pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
			pixels.Apply();
			Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
			File.WriteAllBytes(targetPath, pixels.EncodeToPNG());
			success = true;
		}
		catch (Exception ex)
		{
			DiagnosticWriter.Exception("Screenshot", ex);
		}
		finally
		{
			if (camera != null) camera.targetTexture = previous;
			RenderTexture.active = null;
			if (render != null) render.Release();
			if (render != null) Destroy(render);
			if (pixels != null) Destroy(pixels);
			if (hud != null && hudWasActive) hud.SetActive(true);
		}
		return success;
	}

	private static GameObject FindHudSurface()
	{
		GameObject gui = GameObject.Find("GUI");
		if (gui == null) return null;
		Transform found = gui.transform.Find("Icons/GUITexture");
		return found == null ? null : found.gameObject;
	}
}
}

using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using MSCLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using EmbeddedResources = SaveSlots.Properties.Resources;
using UnityObject = UnityEngine.Object;

namespace SaveSlots
{
public class SaveSlots : Mod
{
	private GameObject prefabShutter;

	private AudioSource shutter;

	private GameObject saveSlotsCanvas;

	private Canvas saveSlotsCanvasComponent;

	private GraphicRaycaster saveSlotsRaycaster;

	public static SettingsCheckBox SynchronizeOptions;

	public static SettingsCheckBox HighResScreenshot;

	public static SettingsCheckBox CreateScreenshotOnEachSave;

	public static SettingsCheckBox CopyMSCEditorBackups;

	public static SettingsSliderInt MaxEmergencyBackups;

	public static SettingsTextBox CustomScreenshotPath;

	public static SettingsSliderInt DateFormat;

	private static SettingsKeybind screenshotKey;

	private static SettingsText screenshotText;

	private bool modStarted;

	private bool gameLoaded;

	private bool updateCheckInProgress;

	private readonly object updateCheckLock = new object();

	private string pendingUpdateTitle;

	private string pendingUpdateMessage;

	public override string ID => "SaveSlots";

	public override string Name => "SAVE SLOTS";

	public override string Author => "Gabriel_SK";

	public override string Version => "1.0";

	public override string Description => "Save slot manager for My Winter Car. Made by: Gabriel_SK.";

	public override byte[] Icon => EmbeddedResources.logo;

	public override Game SupportedGames => Game.MyWinterCar;

	public override void ModSetup()
	{
		SetupFunction(Setup.OnMenuLoad, OnMenuLoad);
		SetupFunction(Setup.OnLoad, OnLoad);
		SetupFunction(Setup.OnSave, OnSave);
		SetupFunction(Setup.Update, Update);
		SetupFunction(Setup.OnModEnabled, OnModEnabled);
		SetupFunction(Setup.OnModDisabled, OnModDisabled);
		SetupFunction(Setup.ModSettings, Mod_Settings);
	}

	private void Mod_Settings()
	{
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Expected O, but got Unknown
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Expected O, but got Unknown
		//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f9: Expected O, but got Unknown
		//IL_0224: Unknown result type (might be due to invalid IL or missing references)
		//IL_0229: Unknown result type (might be due to invalid IL or missing references)
		//IL_022f: Expected O, but got Unknown
		Settings.AddHeader("<color=#52D6FF>SAVE BEHAVIOR</color>");
		SynchronizeOptions = Settings.AddCheckBox("SynchronizeOptions", "Synchronize Game Options".ToUpper(), true);
		CopyMSCEditorBackups = Settings.AddCheckBox("CopyMSCEditorBackups", "Also copy editor backup files".ToUpper(), false);
		MaxEmergencyBackups = Settings.AddSlider("MaxEmergencyBackups", "EMERGENCY BACKUPS KEPT", 1, 10, 5, null, null);
		Settings.AddHeader("<color=#52D6FF>THUMBNAILS</color>");
		HighResScreenshot = Settings.AddCheckBox("HiResScreenshot", "High Resolution Screenshots".ToUpper(), false);
		DateFormat = Settings.AddSlider("DateFormat", "DATE FORMAT", 0, 3, 0, null, new string[4] { "DD/MM/YYYY", "MM/DD/YYYY", "YYYY/MM/DD", "Month D, Yr" });
		CreateScreenshotOnEachSave = Settings.AddCheckBox("CreateScreenshotOnEachSave", "CREATE THUMBNAIL WHILE SAVING", true, UpdateScreenshotSettingVisibility);
		screenshotKey = Keybind.Add("ScreenshotKey", "THUMBNAIL KEY", (KeyCode)291);
		screenshotText = Settings.AddText("If you don't want automatic thumbnails, you can create a thumbnail using <color=#52D6FF>THUMBNAIL KEY</color>.".ToUpper());
		screenshotText.SetVisibility(false);
		CustomScreenshotPath = Settings.AddTextBox("CustomScreenshotPath", "CUSTOM SCREENSHOT PATH", "", "C:\\path\\to\\image.png or .jpg");
		Settings.AddButton("APPLY CUSTOM SCREENSHOT TO CURRENT SAVE", ApplyCustomScreenshotToCurrentSave, new Color32(21, 89, 119, byte.MaxValue), Color.white, SettingsButton.ButtonIcon.Folder);
		Settings.AddText("");
		Settings.AddHeader("LINKS");
		Settings.AddButton("CHECK FOR UPDATES", CheckForUpdates);
		Settings.AddButton("GITHUB RELEASES", OpenGitHubReleases);
		Settings.AddButton("GITHUB REPOSITORY", OpenGitHubRepository);
		Settings.AddText("");
		Settings.AddHeader("CREDITS");
		Settings.AddText("Made by: Gabriel_SK");
		Settings.AddText("Discord: gabriel_sk");
		Settings.AddText("");
		Settings.AddHeader("CHANGELOG");
		Settings.AddText(GetChangelog());
		UpdateScreenshotSettingVisibility();
	}

	private void UpdateScreenshotSettingVisibility()
	{
		bool visible = CreateScreenshotOnEachSave == null || !CreateScreenshotOnEachSave.GetValue();
		if (screenshotKey != null)
		{
			// Current MWC MSCLoader exposes visibility for settings, but not for keybind rows.
		}
		if (screenshotText != null)
		{
			screenshotText.SetVisibility(visible);
		}
	}

	private void OpenGitHubReleases()
	{
		Application.OpenURL("https://github.com/gabrielsk12/saveslots/releases");
	}

	private void OpenGitHubRepository()
	{
		Application.OpenURL("https://github.com/gabrielsk12/saveslots");
	}

	private void CheckForUpdates()
	{
		lock (updateCheckLock)
		{
			if (updateCheckInProgress)
			{
				ModUI.ShowMessage("Update check is already running.", "Save Slots");
				return;
			}
			updateCheckInProgress = true;
		}
		ModUI.ShowMessage("Checking GitHub releases...", "Save Slots");
		ThreadPool.QueueUserWorkItem(delegate
		{
			string message;
			try
			{
				message = BuildUpdateCheckMessage();
			}
			catch (Exception ex)
			{
				ModConsole.LogError("Save Slots update check failed:\n" + ex);
				message = "Could not check GitHub releases. Check output_log.txt or open GitHub Releases manually.";
			}
			lock (updateCheckLock)
			{
				pendingUpdateTitle = "Save Slots";
				pendingUpdateMessage = message;
				updateCheckInProgress = false;
			}
		});
	}

	private string BuildUpdateCheckMessage()
	{
		string url = "https://api.github.com/repos/gabrielsk12/saveslots/releases";
		HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
		request.UserAgent = "SaveSlotsMWC/" + Version;
		request.Accept = "application/vnd.github+json";
		request.Timeout = 10000;
		using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
		using (StreamReader reader = new StreamReader(response.GetResponseStream()))
		{
			string json = reader.ReadToEnd();
			string tagName = ExtractJsonString(json, "tag_name");
			if (string.IsNullOrEmpty(tagName))
			{
				return "GitHub did not return a release version. Open GitHub Releases manually.";
			}
			int compare = CompareVersions(CleanVersion(tagName), CleanVersion(Version));
			if (compare > 0)
			{
				return "New version " + tagName + " is available.\nOpen GitHub Releases and download SaveSlots.dll.";
			}
			return "You are using the latest published version (" + Version + ").";
		}
	}

	private string ExtractJsonString(string json, string key)
	{
		string marker = "\"" + key + "\":";
		int markerIndex = json.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
		if (markerIndex < 0)
		{
			return null;
		}
		int firstQuote = json.IndexOf('"', markerIndex + marker.Length);
		if (firstQuote < 0)
		{
			return null;
		}
		int secondQuote = json.IndexOf('"', firstQuote + 1);
		if (secondQuote < 0)
		{
			return null;
		}
		return json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
	}

	private string CleanVersion(string version)
	{
		if (string.IsNullOrEmpty(version))
		{
			return "0.0";
		}
		version = version.Trim();
		if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
		{
			version = version.Substring(1);
		}
		return version;
	}

	private int CompareVersions(string left, string right)
	{
		string[] leftParts = left.Split('.');
		string[] rightParts = right.Split('.');
		int length = Math.Max(leftParts.Length, rightParts.Length);
		for (int i = 0; i < length; i++)
		{
			int leftValue = ParseVersionPart(leftParts, i);
			int rightValue = ParseVersionPart(rightParts, i);
			if (leftValue != rightValue)
			{
				return leftValue.CompareTo(rightValue);
			}
		}
		return 0;
	}

	private int ParseVersionPart(string[] parts, int index)
	{
		if (index >= parts.Length)
		{
			return 0;
		}
		string part = parts[index];
		int dashIndex = part.IndexOf('-');
		if (dashIndex >= 0)
		{
			part = part.Substring(0, dashIndex);
		}
		int value;
		return int.TryParse(part, out value) ? value : 0;
	}

	private void ApplyCustomScreenshotToCurrentSave()
	{
		string sourcePath = CustomScreenshotPath != null ? CustomScreenshotPath.GetValue() : "";
		if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
		{
			ModUI.ShowMessage("Enter a valid PNG or JPG file path first.", "Save Slots");
			return;
		}
		string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
		if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
		{
			ModUI.ShowMessage("Custom screenshots must be PNG or JPG files.", "Save Slots");
			return;
		}
		FileInfo fileInfo = new FileInfo(sourcePath);
		if (fileInfo.Length > 8 * 1024 * 1024)
		{
			ModUI.ShowMessage("Custom screenshot is too large. Use an image under 8 MB.", "Save Slots");
			return;
		}
		try
		{
			File.Copy(sourcePath, Path.Combine(Application.persistentDataPath, "screenshot.jpg"), overwrite: true);
			if (SlotsManager.Instance != null)
			{
				SlotsManager.Instance.UpdateInfoOfAllSaves();
			}
			ModUI.ShowMessage("Custom screenshot applied to the current save.", "Save Slots");
		}
		catch (Exception ex)
		{
			ModConsole.LogError("Save Slots custom screenshot failed:\n" + ex);
			ModUI.ShowMessage("Could not copy the custom screenshot. Check output_log.txt for details.", "Save Slots");
		}
	}

	public void OnMenuLoad()
	{
		gameLoaded = false;
		string path = Path.Combine(GetGameRoot(), "ModLoaderSettings.ini");
		string text = "Mods";
		if (File.Exists(path))
		{
			string[] array = File.ReadAllLines(path);
			foreach (string text2 in array)
			{
				if (text2.Contains("ModsFolderPath="))
				{
					text = text2.Replace("\\", "/");
				}
			}
		}
		if (text.Contains("AppData/LocalLow/Amistech/My Winter Car"))
		{
			ModPrompt.CreatePrompt("<color=yellow>Save Slots</color> can not work properly, if you have Mods installed in save location.\n\nPlease move your mods to other place.", "SAVE SLOTS: ERROR", null);
		}
		else if (!isDisabled)
		{
			AssetBundle val = LoadAssets.LoadBundle(EmbeddedResources.saveslots);
			if ((UnityObject)(object)val == (UnityObject)null)
			{
				ModConsole.Error("Save Slots failed to load embedded UI assets.");
				return;
			}
			GameObject obj = val.LoadAsset<GameObject>("SaveSlotsCanvas.prefab");
			prefabShutter = val.LoadAsset<GameObject>("SaveSlotsShutter.prefab");
			val.Unload(false);
			if ((UnityObject)(object)obj == (UnityObject)null)
			{
				ModConsole.Error("Save Slots embedded UI is missing SaveSlotsCanvas.prefab.");
				return;
			}
			saveSlotsCanvas = UnityObject.Instantiate<GameObject>(obj);
			saveSlotsCanvas.name = "SaveSlotsCanvasMWC";
			ApplyBlueTheme(saveSlotsCanvas);
			ApplyBlueTextTheme(saveSlotsCanvas);
			saveSlotsCanvasComponent = saveSlotsCanvas.GetComponent<Canvas>();
			if ((UnityObject)(object)saveSlotsCanvasComponent != (UnityObject)null)
			{
				saveSlotsCanvasComponent.overrideSorting = true;
				saveSlotsCanvasComponent.sortingOrder = 900;
			}
			EnsureClickSupport(saveSlotsCanvas, saveSlotsCanvasComponent);
			UpdateMenuVisibility();
		}
	}

	private void EnsureClickSupport(GameObject root, Canvas canvas)
	{
		if ((UnityObject)(object)root == (UnityObject)null)
		{
			return;
		}
		if ((UnityObject)(object)canvas != (UnityObject)null)
		{
			saveSlotsRaycaster = canvas.GetComponent<GraphicRaycaster>();
			if ((UnityObject)(object)saveSlotsRaycaster == (UnityObject)null)
			{
				saveSlotsRaycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
			}
		}
		if ((UnityObject)(object)UnityObject.FindObjectOfType<EventSystem>() == (UnityObject)null)
		{
			GameObject eventSystem = new GameObject("SaveSlotsEventSystem");
			eventSystem.AddComponent<EventSystem>();
			eventSystem.AddComponent<StandaloneInputModule>();
		}
		root.transform.SetAsLastSibling();
	}

	private void UpdateMenuVisibility()
	{
		if ((UnityObject)(object)saveSlotsCanvas == (UnityObject)null)
		{
			return;
		}
		if (gameLoaded)
		{
			if (saveSlotsCanvas.activeSelf)
			{
				saveSlotsCanvas.SetActive(false);
			}
			if (SlotsManager.Instance != null)
			{
				SlotsManager.Instance.HideContinueButton();
			}
			if ((UnityObject)(object)saveSlotsRaycaster != (UnityObject)null)
			{
				saveSlotsRaycaster.enabled = false;
			}
			return;
		}
		if (SlotsManager.Instance != null)
		{
			SlotsManager.Instance.SyncContinueButtonToActiveSave();
		}
		bool flag = IsModLoaderMenuOpen();
		if (!saveSlotsCanvas.activeSelf)
		{
			saveSlotsCanvas.SetActive(true);
		}
		if ((UnityObject)(object)saveSlotsCanvasComponent != (UnityObject)null)
		{
			saveSlotsCanvasComponent.sortingOrder = flag ? -10 : 900;
		}
		if ((UnityObject)(object)saveSlotsRaycaster != (UnityObject)null)
		{
			saveSlotsRaycaster.enabled = !flag;
		}
	}

	private bool IsModLoaderMenuOpen()
	{
		try
		{
			Type modMenuType = typeof(Mod).Assembly.GetType("MSCLoader.ModMenu");
			if (modMenuType == null)
			{
				return false;
			}
			FieldInfo instanceField = modMenuType.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
			object instance = instanceField != null ? instanceField.GetValue(null) : null;
			if (instance == null)
			{
				return false;
			}
			FieldInfo uiField = modMenuType.GetField("UI", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			GameObject ui = uiField != null ? uiField.GetValue(instance) as GameObject : null;
			if ((UnityObject)(object)ui == (UnityObject)null)
			{
				return false;
			}
			Component[] components = ui.GetComponentsInChildren<Component>(true);
			foreach (Component component in components)
			{
				if ((UnityObject)(object)component == (UnityObject)null || component.GetType().FullName != "MSCLoader.ModMenuButton")
				{
					continue;
				}
				FieldInfo openedField = component.GetType().GetField("opened", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				if (openedField != null)
				{
					object opened = openedField.GetValue(component);
					if (opened is bool)
					{
						return (bool)opened;
					}
				}
			}
			if (ui.transform.childCount > 0)
			{
				GameObject menuRoot = ui.transform.GetChild(0).gameObject;
				return menuRoot.activeInHierarchy;
			}
			return ui.activeInHierarchy;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private void ApplyBlueTheme(GameObject root)
	{
		if ((UnityObject)(object)root == (UnityObject)null)
		{
			return;
		}
		Image[] componentsInChildren = root.GetComponentsInChildren<Image>(true);
		foreach (Image image in componentsInChildren)
		{
			Color color = ((Graphic)image).color;
			if (!ShouldThemeImage(color))
			{
				continue;
			}
			Color themedColor = BlueFor(color);
			themedColor.a = color.a;
			((Graphic)image).color = themedColor;
		}
	}

	private bool ShouldThemeImage(Color color)
	{
		if (color.a <= 0.05f)
		{
			return false;
		}
		if (color.r > 0.85f && color.g > 0.85f && color.b > 0.85f)
		{
			return false;
		}
		if (color.b > color.r && color.b > color.g)
		{
			return false;
		}
		return color.r > color.b + 0.08f && color.r >= color.g - 0.05f;
	}

	private void ApplyBlueTextTheme(GameObject root)
	{
		if ((UnityObject)(object)root == (UnityObject)null)
		{
			return;
		}
		Text[] componentsInChildren = root.GetComponentsInChildren<Text>(true);
		foreach (Text text in componentsInChildren)
		{
			if ((UnityObject)(object)text == (UnityObject)null)
			{
				continue;
			}
			Color color = ((Graphic)text).color;
			if (color.r > 0.8f && color.g > 0.7f && color.b < 0.35f)
			{
				byte alpha = color.a >= 0.5f ? byte.MaxValue : (byte)128;
				((Graphic)text).color = new Color32(82, 214, 255, alpha);
			}
		}
	}

	private Color BlueFor(Color color)
	{
		float brightness = (color.r + color.g + color.b) / 3f;
		if (brightness > 0.55f)
		{
			return new Color32(64, 139, 202, byte.MaxValue);
		}
		if (brightness > 0.35f)
		{
			return new Color32(25, 91, 145, byte.MaxValue);
		}
		return new Color32(13, 58, 101, byte.MaxValue);
	}

	public void OnModDisabled()
	{
		if (!modStarted)
		{
			modStarted = true;
		}
		else
		{
			ModPrompt.CreatePrompt("Save Slots will be disabled after the next game restart.", "Save Slots".ToUpper(), null);
		}
	}

	public void OnModEnabled()
	{
		if (!modStarted)
		{
			modStarted = true;
		}
		else
		{
			ModPrompt.CreatePrompt("Save Slots will be enabled after the next game restart.", "Save Slots".ToUpper(), null);
		}
	}

	public void OnLoad()
	{
		gameLoaded = true;
		if (SlotsManager.Instance != null)
		{
			SlotsManager.Instance.HideContinueButton();
		}
		if ((UnityObject)(object)saveSlotsCanvas != (UnityObject)null)
		{
			saveSlotsCanvas.SetActive(false);
		}
		if ((UnityObject)(object)prefabShutter != (UnityObject)null)
		{
			shutter = UnityObject.Instantiate<GameObject>(prefabShutter).GetComponent<AudioSource>();
		}
	}

	public void OnSave()
	{
		if (CreateScreenshotOnEachSave != null && CreateScreenshotOnEachSave.GetValue())
		{
			TakeScreenshot(enableGUI: false);
		}
		if (SlotsManager.Instance != null)
		{
			SlotsManager.Instance.PersistActiveSaveNow();
		}
	}

	public void Update()
	{
		ShowPendingUpdateMessage();
		if (SlotsManager.Instance != null)
		{
			SlotsManager.Instance.SetContinueRefreshEnabled(!gameLoaded);
		}
		UpdateMenuVisibility();
		if (CreateScreenshotOnEachSave != null && !CreateScreenshotOnEachSave.GetValue() && screenshotKey != null && screenshotKey.GetKeybindDown())
		{
			TakeScreenshot(enableGUI: true);
			if ((UnityObject)(object)shutter != (UnityObject)null)
			{
				shutter.Play();
			}
		}
	}

	private void ShowPendingUpdateMessage()
	{
		string title = null;
		string message = null;
		lock (updateCheckLock)
		{
			if (!string.IsNullOrEmpty(pendingUpdateMessage))
			{
				title = pendingUpdateTitle;
				message = pendingUpdateMessage;
				pendingUpdateTitle = null;
				pendingUpdateMessage = null;
			}
		}
		if (!string.IsNullOrEmpty(message))
		{
			ModUI.ShowMessage(message, string.IsNullOrEmpty(title) ? "Save Slots" : title);
		}
	}

	private void TakeScreenshot(bool enableGUI)
	{
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Expected O, but got Unknown
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		GameObject gameObject = null;
		GameObject gui = GameObject.Find("GUI");
		if ((UnityObject)(object)gui != (UnityObject)null && (UnityObject)(object)gui.transform.Find("Icons/GUITexture") != (UnityObject)null)
		{
			gameObject = ((Component)gui.transform.Find("Icons/GUITexture")).gameObject;
			gameObject.SetActive(false);
		}
		int num = 190;
		int num2 = 108;
		if (HighResScreenshot != null && HighResScreenshot.GetValue())
		{
			num *= 3;
			num2 *= 3;
		}
		if ((UnityObject)(object)Camera.main == (UnityObject)null)
		{
			return;
		}
		RenderTexture val = new RenderTexture(num, num2, 24);
		Camera.main.targetTexture = val;
		Texture2D val2 = new Texture2D(num, num2, (TextureFormat)3, false);
		Camera.main.Render();
		RenderTexture.active = val;
		val2.ReadPixels(new Rect(0f, 0f, (float)num, (float)num2), 0, 0);
		Camera.main.targetTexture = null;
		RenderTexture.active = null;
		UnityObject.Destroy((UnityObject)(object)val);
		byte[] bytes = val2.EncodeToPNG();
		File.WriteAllBytes(Path.Combine(Application.persistentDataPath, "screenshot.jpg"), bytes);
		if (enableGUI && (UnityObject)(object)gameObject != (UnityObject)null)
		{
			gameObject.SetActive(true);
		}
	}

	private string GetGameRoot()
	{
		return Directory.GetParent(Application.dataPath).FullName;
	}

	private string GetChangelog()
	{
		string portChanges = "### V 1.0\n- Mod published to Beta\n";
		string[] array = portChanges.Split('\n');
		string text = "";
		for (int i = 0; i < array.Length; i++)
		{
			string text2 = array[i];
			if (text2.StartsWith("###"))
			{
				text2 = text2.Replace("###", "");
				text2 = "<color=#52D6FF><size=24>" + text2 + "</size></color>";
			}
			if (text2.StartsWith("-"))
			{
				text2 = text2.Substring(1);
				text2 = "- " + text2.TrimStart();
			}
			if (text2.StartsWith("  -"))
			{
				text2 = text2.Substring(3);
				text2 = "  - " + text2.TrimStart();
			}
			if (text2.Contains("(Beta)"))
			{
				text2 = text2.Replace("(Beta)", "<color=orange>Beta: </color>");
			}
			if (text2.Contains("Rule Files API:"))
			{
				text2 = text2.Replace("Rule Files API:", "<color=cyan>Rule Files API:</color>");
			}
			text = text + text2 + "\n";
		}
		return text;
	}
}
}


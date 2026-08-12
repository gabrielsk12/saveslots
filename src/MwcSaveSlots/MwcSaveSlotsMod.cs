using System;
using System.IO;
using MSCLoader;
using UnityEngine;

namespace MwcSaveSlots
{
public sealed class MwcSaveSlotsMod : Mod
{
	internal const string PluginId = "SaveSlotsMWC";
	internal const string DisplayName = "SAVE SLOTS";
	internal const string ReleaseVersion = "4.0.0";

	private ProfileCoordinator coordinator;
	private SettingsCheckBox synchronizeOptions;
	private SettingsCheckBox copyEditorBackups;
	private SettingsSliderInt maximumBackups;
	private SettingsCheckBox highResolution;
	private SettingsSliderInt dateFormat;
	private SettingsCheckBox automaticThumbnail;
	private SettingsKeybind screenshotKey;
	private SettingsTextBox customScreenshotPath;
	private SettingsText manualThumbnailHint;
	private bool started;
	private bool consoleCommandRegistered;
	private DateTime nextConsoleRegistrationUtc = DateTime.MinValue;

	public override string ID { get { return PluginId; } }
	public override string Name { get { return DisplayName; } }
	public override string Author { get { return "Gabriel_SK"; } }
	public override string Version { get { return ReleaseVersion; } }
	public override string Description { get { return "Three save slots for My Winter Car."; } }
	public override byte[] Icon { get { return MwcAssetCatalog.Logo; } }
	public override Game SupportedGames { get { return Game.MyWinterCar; } }

	internal bool SynchronizeOptionsValue { get { return synchronizeOptions == null || synchronizeOptions.GetValue(); } }
	internal bool CopyEditorBackupsValue { get { return copyEditorBackups != null && copyEditorBackups.GetValue(); } }
	internal int MaximumBackupsValue { get { return maximumBackups == null ? 5 : maximumBackups.GetValue(); } }
	internal bool HighResolutionValue { get { return highResolution != null && highResolution.GetValue(); } }
	internal int DateFormatValue { get { return dateFormat == null ? 0 : dateFormat.GetValue(); } }
	internal bool AutomaticThumbnailValue { get { return automaticThumbnail == null || automaticThumbnail.GetValue(); } }

	public override void ModSetup()
	{
		DiagnosticWriter.Write("Session", "============================================================");
		DiagnosticWriter.Write("ModSetup", "Starting independent Save Slots " + ReleaseVersion + ".");
		DiagnosticWriter.Write("Runtime", "CLR=" + Environment.Version
			+ " UnityAssembly=" + typeof(Application).Assembly.GetName().Version
			+ " persistentDataPath=" + Application.persistentDataPath
			+ " dataPath=" + Application.dataPath);
		SetupFunction(Setup.OnMenuLoad, OnMenuLoad);
		SetupFunction(Setup.OnLoad, OnLoad);
		SetupFunction(Setup.OnSave, OnSave);
		SetupFunction(Setup.Update, OnUpdate);
		SetupFunction(Setup.ModSettings, BuildSettings);
		SetupFunction(Setup.OnModEnabled, OnModEnabled);
		SetupFunction(Setup.OnModDisabled, OnModDisabled);
	}

	private void BuildSettings()
	{
		Settings.AddHeader("<color=#00DCF4>SAVE BEHAVIOR</color>");
		synchronizeOptions = Settings.AddCheckBox("SynchronizeOptions", "SYNCHRONIZE GAME OPTIONS", true);
		copyEditorBackups = Settings.AddCheckBox("CopyMSCEditorBackups", "ALSO COPY EDITOR BACKUP FILES", false);
		maximumBackups = Settings.AddSlider("MaxEmergencyBackups", "EMERGENCY BACKUPS KEPT", 1, 10, 5, null, null);

		Settings.AddHeader("<color=#00DCF4>THUMBNAILS</color>");
		highResolution = Settings.AddCheckBox("HiResScreenshot", "HIGH RESOLUTION SCREENSHOTS", false);
		dateFormat = Settings.AddSlider("DateFormat", "DATE FORMAT", 0, 3, 0, null, new[] { "DD/MM/YYYY", "MM/DD/YYYY", "YYYY/MM/DD", "Month D, Yr" });
		automaticThumbnail = Settings.AddCheckBox("CreateScreenshotOnEachSave", "CREATE THUMBNAIL WHILE SAVING", true, UpdateThumbnailSettingVisibility);
		screenshotKey = Keybind.Add("ScreenshotKey", "THUMBNAIL KEY", (KeyCode)291);
		manualThumbnailHint = Settings.AddText("IF YOU DON'T WANT AUTOMATIC THUMBNAILS, YOU CAN CREATE ONE USING <color=#00DCF4>THUMBNAIL KEY</color>.");
		customScreenshotPath = Settings.AddTextBox("CustomScreenshotPath", "CUSTOM SCREENSHOT PATH", "", "C:\\path\\to\\image.png or .jpg");
		Settings.AddButton("APPLY CUSTOM SCREENSHOT TO CURRENT SAVE", ApplyCustomScreenshot, UiPrimitives.MwcPanel, Color.white, SettingsButton.ButtonIcon.Folder);

		Settings.AddText("");
		Settings.AddHeader("SAVE FOLDERS");
		Settings.AddButton("OPEN ACTIVE SAVE FOLDER", OpenActiveFolder, UiPrimitives.MwcPanel, Color.white, SettingsButton.ButtonIcon.Folder);
		Settings.AddButton("OPEN SAVE SLOTS FOLDER", OpenStorageFolder, UiPrimitives.MwcPanel, Color.white, SettingsButton.ButtonIcon.Folder);
		Settings.AddButton("OPEN DELETED SAVE BACKUPS", OpenDeletedBackupsFolder, UiPrimitives.MwcPanel, Color.white, SettingsButton.ButtonIcon.Folder);
		Settings.AddText("");
		Settings.AddHeader("LINKS");
		Settings.AddButton("GITHUB RELEASES", delegate { Application.OpenURL("https://github.com/gabrielsk12/saveslots/releases"); });
		Settings.AddButton("GITHUB REPOSITORY", delegate { Application.OpenURL("https://github.com/gabrielsk12/saveslots"); });
		Settings.AddText("");
		Settings.AddHeader("CREDITS");
		Settings.AddText("Made by: Gabriel_SK");
		Settings.AddText("Camera transition sound: irinairinafomicheva / Pixabay");
		Settings.AddText("Button sound: DenielCZ / Pixabay");
		Settings.AddText("Discord: gabriel_sk");
		Settings.AddText("");
		Settings.AddHeader("CHANGELOG");
		Settings.AddText(SettingsReleaseNotes.Build());
		UpdateThumbnailSettingVisibility();
	}

	private void OnMenuLoad()
	{
		try
		{
			DiagnosticWriter.Write("OnMenuLoad", "Creating the Save Slots menu.");
			EnsureCoordinator();
			coordinator.EnterMenu();
			TryRegisterConsoleCommand();
			DiagnosticWriter.Write("OnMenuLoad", "Save Slots menu is ready.");
		}
		catch (Exception ex) { Report("Menu initialization", ex); }
	}

	private void OnLoad()
	{
		try
		{
			EnsureCoordinator();
			coordinator.EnterGame();
		}
		catch (Exception ex) { Report("Game initialization", ex); }
	}

	private void OnSave()
	{
		try
		{
			EnsureCoordinator();
			coordinator.ScheduleGameSave();
		}
		catch (Exception ex) { Report("Save event", ex); }
	}

	private void OnUpdate()
	{
		if (!consoleCommandRegistered && DateTime.UtcNow >= nextConsoleRegistrationUtc) TryRegisterConsoleCommand();
		if (coordinator != null) coordinator.Tick();
		if (!AutomaticThumbnailValue && coordinator != null && screenshotKey != null && screenshotKey.GetKeybindDown())
		{
			coordinator.CaptureManualThumbnail();
		}
	}

	private void OnModEnabled()
	{
		if (started) ModUI.ShowMessage("Save Slots will be enabled after the next game restart.", "Save Slots");
		started = true;
	}

	private void OnModDisabled()
	{
		if (started) ModUI.ShowMessage("Save Slots will be disabled after the next game restart.", "Save Slots");
		started = true;
	}

	private void UpdateThumbnailSettingVisibility()
	{
		if (manualThumbnailHint != null) manualThumbnailHint.SetVisibility(!AutomaticThumbnailValue);
	}

	private void ApplyCustomScreenshot()
	{
		try
		{
			string source = customScreenshotPath == null ? "" : customScreenshotPath.GetValue();
			if (string.IsNullOrEmpty(source) || !File.Exists(source))
			{
				ModUI.ShowMessage("Enter a valid PNG or JPG file path first.", "Save Slots");
				return;
			}
			string extension = Path.GetExtension(source);
			if (!string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) && !string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) && !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
			{
				ModUI.ShowMessage("Custom screenshots must be PNG or JPG files.", "Save Slots");
				return;
			}
			EnsureCoordinator();
			ModUI.ShowMessage(coordinator.ApplyCustomThumbnail(source)
				? "Custom screenshot applied to the current save."
				: "The image could not be read or is larger than 8 MB.", "Save Slots");
		}
		catch (Exception ex) { Report("Custom screenshot", ex); }
	}

	private void OpenActiveFolder()
	{
		OpenFolder(RuntimePaths.ActiveSavePath);
	}

	private void OpenStorageFolder()
	{
		EnsureCoordinator();
		coordinator.EnsureStorage();
		OpenFolder(coordinator.StorageRoot);
	}

	private void OpenDeletedBackupsFolder()
	{
		EnsureCoordinator();
		coordinator.EnsureStorage();
		OpenFolder(coordinator.DeletedBackupsRoot);
	}

	private static void OpenFolder(string path)
	{
		Directory.CreateDirectory(path);
		Application.OpenURL("file:///" + Uri.EscapeUriString(Path.GetFullPath(path).Replace('\\', '/')));
	}

	private void EnsureCoordinator()
	{
		if (coordinator == null) coordinator = new ProfileCoordinator(this);
	}

	internal void RunConsoleCommand(string[] args)
	{
		try
		{
			string action = args == null || args.Length == 0 ? "status" : (args[0] ?? "").Trim().ToLowerInvariant();
			EnsureCoordinator();
			switch (action)
			{
				case "status":
					PrintStatus();
					break;
				case "show":
					coordinator.ForceUi(false);
					ModConsole.Print("Save Slots: SAVES button forced visible. Run 'saveslots status' for details.");
					break;
				case "open":
					coordinator.ForceUi(true);
					ModConsole.Print("Save Slots: save-slot panel opened.");
					break;
				case "close":
					coordinator.ClosePanel();
					ModConsole.Print("Save Slots: save-slot panel closed.");
					break;
				case "refresh":
					coordinator.RefreshFromConsole();
					ModConsole.Print("Save Slots: profiles and Continue button refreshed.");
					break;
				case "log":
					OpenDiagnosticLog();
					break;
				case "backups":
					OpenDeletedBackupsFolder();
					ModConsole.Print("Save Slots: opened verified deleted-profile backups.");
					break;
				case "help":
				case "?":
					PrintConsoleHelp();
					break;
				default:
					ModConsole.LogWarning("Save Slots: unknown command '" + action + "'.");
					PrintConsoleHelp();
					break;
			}
		}
		catch (Exception ex)
		{
			Report("Console command", ex);
		}
	}

	private void PrintStatus()
	{
		string state = coordinator.DescribeState();
		DiagnosticWriter.Write("ConsoleStatus", state);
		ModConsole.Print("Save Slots " + ReleaseVersion + ": " + state);
		ModConsole.Print("Diagnostic log: " + DiagnosticWriter.PathName);
	}

	private static void PrintConsoleHelp()
	{
		ModConsole.Print("Save Slots commands: saveslots status | show | open | close | refresh | backups | log | help (alias: ss)");
	}

	private static void OpenDiagnosticLog()
	{
		DiagnosticWriter.Write("Console", "Opening diagnostic log.");
		string path = DiagnosticWriter.PathName;
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		if (!File.Exists(path)) File.WriteAllText(path, "Save Slots diagnostic log" + Environment.NewLine);
		Application.OpenURL("file:///" + Uri.EscapeUriString(Path.GetFullPath(path).Replace('\\', '/')));
		ModConsole.Print("Save Slots diagnostic log: " + path);
	}

	private void TryRegisterConsoleCommand()
	{
		if (consoleCommandRegistered) return;
		nextConsoleRegistrationUtc = DateTime.UtcNow.AddSeconds(1d);
		try
		{
			ConsoleCommand.Add(new SaveSlotsConsoleCommand(this));
			consoleCommandRegistered = true;
			DiagnosticWriter.Write("Console", "Registered 'saveslots' command with alias 'ss'.");
			ModConsole.Log("Save Slots diagnostics ready. Type 'saveslots help' in the console.");
		}
		catch (Exception ex)
		{
			DiagnosticWriter.Write("Console", "Registration is not ready yet: " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	private static void Report(string area, Exception ex)
	{
		DiagnosticWriter.Exception(area, ex);
		ModConsole.LogError("Save Slots " + area.ToLowerInvariant() + " failed. Check SaveSlotsDebug.log.\n" + ex);
	}
}
}

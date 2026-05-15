using SaveSlotsMWC.Core;

var tests = new List<(string Name, Action Body)>
{
    ("defaults to Save1 when metadata is missing", DefaultsToSave1),
    ("switching to a new slot backs up active files and preserves shared options", SwitchToNewSlotCreatesBackupAndCopiesOptions),
    ("switching to an existing slot copies slot files and skips editor backups by default", SwitchToExistingSlotSkipsBackups),
    ("switching with backup copy enabled includes editor backups", SwitchWithBackupsEnabledIncludesBackups),
    ("built mod does not reference forbidden non-MSCLoader assemblies", BuiltModDoesNotReferenceForbiddenLoaders),
    ("ported original SaveSlots DLL is MSCLoader-only", PortedOriginalSaveSlotsIsMSCLoaderOnly),
    ("ported original initializes current empty slot on click", PortedOriginalInitializesCurrentEmptySlotOnClick),
    ("ported original preserves existing saves on first install", PortedOriginalPreservesExistingSavesOnFirstInstall),
    ("ported original keeps whole-folder safety backups", PortedOriginalKeepsWholeFolderSafetyBackups),
    ("ported original reads My Winter Car save metadata", PortedOriginalReadsMyWinterCarSaveMetadata),
    ("ported original switches saves without auto-loading", PortedOriginalSwitchesSavesWithoutAutoLoading),
    ("ported original hides Continue for empty saves and loading", PortedOriginalHidesContinueForEmptySavesAndLoading),
    ("ported original keeps delete confirmation above save UI", PortedOriginalKeepsDeleteConfirmationAboveSaveUi),
    ("ported original does not persist empty profile junk", PortedOriginalDoesNotPersistEmptyProfileJunk),
    ("ported original deletes active and inactive profiles completely", PortedOriginalDeletesProfilesCompletely),
    ("ported original applies MWC blue save slot theme", PortedOriginalAppliesBlueTheme),
    ("ported original save slot UI remains visible and avoids MSCLoader menu overlay", PortedOriginalUiAvoidsModLoaderMenuOverlay),
    ("ported original exposes GitHub release metadata safely", PortedOriginalExposesGitHubReleaseMetadataSafely),
    ("ported original checks GitHub releases safely from settings", PortedOriginalChecksGitHubReleasesSafely),
    ("ported original public branding is Gabriel_SK-only", PortedOriginalPublicBrandingIsGabrielOnly),
    ("ported original keeps normal console logging quiet", PortedOriginalKeepsNormalConsoleLoggingQuiet),
    ("ported original avoids shell execution and self-modifying update code", PortedOriginalAvoidsUnsafeUpdaterBehavior),
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL {test.Name}");
        Console.WriteLine(ex);
    }
}

return failures == 0 ? 0 : 1;

static void DefaultsToSave1()
{
    using var temp = new TempSaveTree();
    var manager = new SaveSlotManager(temp.ActivePath, temp.SaveRoot);

    AssertEqual("Save1", manager.GetCurrentSlotName());
}

static void SwitchToNewSlotCreatesBackupAndCopiesOptions()
{
    using var temp = new TempSaveTree();
    File.WriteAllText(Path.Combine(temp.ActivePath, "savefile.txt"), "active save");
    File.WriteAllText(Path.Combine(temp.ActivePath, "options.txt"), "active options");
    File.WriteAllText(Path.Combine(temp.ActivePath, "defaultES2File_backup01.bak"), "skip");
    Directory.CreateDirectory(Path.Combine(temp.ActivePath, "nested"));
    File.WriteAllText(Path.Combine(temp.ActivePath, "nested", "part.txt"), "nested");

    Directory.CreateDirectory(temp.OptionsPath);
    File.WriteAllText(Path.Combine(temp.OptionsPath, "options.txt"), "shared options");

    var manager = new SaveSlotManager(temp.ActivePath, temp.SaveRoot);
    var result = manager.SwitchToSlot("Save2", new SaveSlotOptions { SynchronizeOptions = true });

    AssertFalse(result.ContinueAvailable, "new slot should disable Continue");
    AssertFile(Path.Combine(temp.SlotsPath, "Save1", "savefile.txt"), "active save");
    AssertFile(Path.Combine(temp.SlotsPath, "Save1", "nested", "part.txt"), "nested");
    AssertFalse(File.Exists(Path.Combine(temp.SlotsPath, "Save1", "defaultES2File_backup01.bak")), "backup file should be skipped");
    AssertFile(Path.Combine(temp.SaveRoot, "SAVE SLOTS BACKUP", "savefile.txt"), "active save");
    AssertFile(Path.Combine(temp.ActivePath, "options.txt"), "active options");
    AssertEqual("Save2", manager.GetCurrentSlotName());
}

static void SwitchToExistingSlotSkipsBackups()
{
    using var temp = new TempSaveTree();
    SaveSlotMetadataStore.Save(Path.Combine(temp.ActivePath, SaveSlotManager.MetadataFileName), new SaveSlotMetadata("Save1", DateTime.Now));
    File.WriteAllText(Path.Combine(temp.ActivePath, "savefile.txt"), "save one");
    Directory.CreateDirectory(Path.Combine(temp.SlotsPath, "Save2"));
    File.WriteAllText(Path.Combine(temp.SlotsPath, "Save2", "savefile.txt"), "save two");
    File.WriteAllText(Path.Combine(temp.SlotsPath, "Save2", "defaultES2File_backup01.bak"), "do not copy");
    Directory.CreateDirectory(Path.Combine(temp.SlotsPath, "Save2", "sub"));
    File.WriteAllText(Path.Combine(temp.SlotsPath, "Save2", "sub", "item.txt"), "slot nested");

    var manager = new SaveSlotManager(temp.ActivePath, temp.SaveRoot);
    var result = manager.SwitchToSlot("Save2", new SaveSlotOptions());

    AssertTrue(result.ContinueAvailable, "existing MWC savefile should allow Continue");
    AssertFile(Path.Combine(temp.ActivePath, "savefile.txt"), "save two");
    AssertFalse(File.Exists(Path.Combine(temp.ActivePath, "defaultES2File_backup01.bak")), "backup file should be skipped");
    AssertFile(Path.Combine(temp.ActivePath, "sub", "item.txt"), "slot nested");
    AssertEqual("Save2", manager.GetCurrentSlotName());
}

static void SwitchWithBackupsEnabledIncludesBackups()
{
    using var temp = new TempSaveTree();
    Directory.CreateDirectory(Path.Combine(temp.SlotsPath, "Save2"));
    File.WriteAllText(Path.Combine(temp.SlotsPath, "Save2", "savefile.txt"), "save two");
    File.WriteAllText(Path.Combine(temp.SlotsPath, "Save2", "defaultES2File_backup01.bak"), "copy me");

    var manager = new SaveSlotManager(temp.ActivePath, temp.SaveRoot);
    manager.SwitchToSlot("Save2", new SaveSlotOptions { CopyEditorBackups = true });

    AssertFile(Path.Combine(temp.ActivePath, "defaultES2File_backup01.bak"), "copy me");
}

static void BuiltModDoesNotReferenceForbiddenLoaders()
{
    var root = FindWorkspaceRoot();
    var dll = Path.Combine(root, "dist", "SaveSlotsMWC.dll");
    if (!File.Exists(dll))
    {
        throw new Exception("Build the mod before running this check: " + dll);
    }

    var bytes = File.ReadAllBytes(dll);
    AssertFalse(ContainsAscii(bytes, "MWCLoader"), "mod DLL must not reference MWCLoader");
    AssertFalse(ContainsAscii(bytes, "MelonLoader"), "mod DLL must not reference MelonLoader");
    AssertFalse(ContainsAscii(bytes, "BepInEx"), "mod DLL must not reference BepInEx");
    AssertFalse(ContainsAscii(bytes, "LightspeedModLoader"), "mod DLL must not reference LightspeedModLoader");
    AssertTrue(ContainsAscii(bytes, "MSCLoader"), "mod DLL should reference MSCLoader");
}

static void PortedOriginalSaveSlotsIsMSCLoaderOnly()
{
    var root = FindWorkspaceRoot();
    var dll = Path.Combine(root, "dist", "SaveSlots.dll");
    if (!File.Exists(dll))
    {
        throw new Exception("Build the original SaveSlots port before running this check: " + dll);
    }

    var bytes = File.ReadAllBytes(dll);
    AssertFalse(ContainsAscii(bytes, "MWCLoader"), "ported SaveSlots.dll must not reference MWCLoader");
    AssertFalse(ContainsAscii(bytes, "MelonLoader"), "ported SaveSlots.dll must not reference MelonLoader");
    AssertFalse(ContainsAscii(bytes, "BepInEx"), "ported SaveSlots.dll must not reference BepInEx");
    AssertFalse(ContainsAscii(bytes, "LightspeedModLoader"), "ported SaveSlots.dll must not reference LightspeedModLoader");
    AssertTrue(ContainsAscii(bytes, "MSCLoader"), "ported SaveSlots.dll should reference MSCLoader");
    var decompiled = RunIlSpy(dll, "SaveSlots.SaveSlots");
    AssertTrue(decompiled.Contains("SupportedGames => (Game)2") || decompiled.Contains("SupportedGames => Game.MyWinterCar"), "ported SaveSlots.dll should declare MyWinterCar support");
}

static void PortedOriginalInitializesCurrentEmptySlotOnClick()
{
    var root = FindWorkspaceRoot();
    var dll = Path.Combine(root, "dist", "SaveSlots.dll");
    if (!File.Exists(dll))
    {
        throw new Exception("Build the original SaveSlots port before running this check: " + dll);
    }

    var decompiled = RunIlSpy(dll, "SaveSlots.SlotsManager");
    var slotBehaviour = RunIlSpy(dll, "SaveSlots.SlotBehaviour");
    AssertTrue(decompiled.Contains("InitializeCurrentEmptySlot"), "clicking the current empty slot should initialize SaveSlots.xml instead of returning early");
    var onClickStart = slotBehaviour.IndexOf("private void OnButtonClick()", StringComparison.Ordinal);
    var onClickEnd = slotBehaviour.IndexOf("internal void SetColor", StringComparison.Ordinal);
    AssertTrue(onClickStart >= 0 && onClickEnd > onClickStart, "decompiled SlotBehaviour should contain OnButtonClick");
    var onClickBody = slotBehaviour.Substring(onClickStart, onClickEnd - onClickStart);
    AssertTrue(onClickBody.Contains("SlotsManager.Instance.LoadSave(this);") && !onClickBody.Contains("if ("), "slot click should always enter LoadSave so the current empty slot can be initialized");
}

static void PortedOriginalPreservesExistingSavesOnFirstInstall()
{
    var root = FindWorkspaceRoot();
    var dll = Path.Combine(root, "dist", "SaveSlots.dll");
    if (!File.Exists(dll))
    {
        throw new Exception("Build the original SaveSlots port before running this check: " + dll);
    }

    var decompiled = RunIlSpy(dll, "SaveSlots.SlotsManager");
    AssertTrue(decompiled.Contains("EnsureFirstInstallProfile"), "first install should create SaveSlots metadata for an existing active save");
    AssertTrue(decompiled.Contains("HasActiveSaveData"), "first install should detect existing player save data before creating metadata");
    AssertTrue(decompiled.Contains("Save1") && decompiled.Contains("DirectoryCopy(Application.persistentDataPath"), "first install profile creation should preserve the active player save in place");
}

static void PortedOriginalKeepsWholeFolderSafetyBackups()
{
    var root = FindWorkspaceRoot();
    var dll = Path.Combine(root, "dist", "SaveSlots.dll");
    if (!File.Exists(dll))
    {
        throw new Exception("Build the original SaveSlots port before running this check: " + dll);
    }

    var decompiled = RunIlSpy(dll, "SaveSlots.SlotsManager");
    AssertTrue(decompiled.Contains("NormalizeSlotName"), "SaveSlots.xml slot names should be normalized before they become folder paths");
    AssertTrue(decompiled.Contains("DirectoryCopy") && decompiled.Contains("copySubDirs: true"), "save switching should copy complete folder trees, not just defaultES2File.txt");
    AssertTrue(decompiled.Contains("EmergencyBackups") && decompiled.Contains("PruneEmergencyBackups"), "emergency backups should be retained in one managed folder instead of spamming the save root");
    AssertTrue(decompiled.Contains("MigrateLegacyRootBackups"), "old timestamped root backup folders should be migrated into managed emergency backups");
}

static void PortedOriginalReadsMyWinterCarSaveMetadata()
{
    var root = FindWorkspaceRoot();
    var dll = Path.Combine(root, "dist", "SaveSlots.dll");
    if (!File.Exists(dll))
    {
        throw new Exception("Build the original SaveSlots port before running this check: " + dll);
    }

    var slotsManager = RunIlSpy(dll, "SaveSlots.SlotsManager");
    var slotBehaviour = RunIlSpy(dll, "SaveSlots.SlotBehaviour");
    AssertTrue(slotsManager.Contains("savefile.txt"), "Continue visibility should use My Winter Car savefile.txt");
    AssertTrue(slotBehaviour.Contains("savefile.txt"), "slot cards should read My Winter Car savefile.txt");
    AssertTrue(slotBehaviour.Contains("PlayerMoney") && slotBehaviour.Contains("PlayerName") && slotBehaviour.Contains("PlayerTransform"), "slot cards should read MWC player money/name/location tags");
    AssertFalse(slotBehaviour.Contains("defaultES2File.txt"), "slot cards should not depend on the old MSC defaultES2File.txt");
}

static void PortedOriginalSwitchesSavesWithoutAutoLoading()
{
    var root = FindWorkspaceRoot();
    var dll = Path.Combine(root, "dist", "SaveSlots.dll");
    if (!File.Exists(dll))
    {
        throw new Exception("Build the original SaveSlots port before running this check: " + dll);
    }

    var slotsManager = RunIlSpy(dll, "SaveSlots.SlotsManager");
    var saveSlots = RunIlSpy(dll, "SaveSlots.SaveSlots");
    AssertTrue(slotsManager.Contains("HasPlayableSave") && slotsManager.Contains("UpdateContinueButton"), "slot switching should still expose Continue when the selected slot has a save");
    AssertTrue(slotsManager.Contains("isSwitchingSave"), "slot switching should be reentrancy guarded to avoid duplicate backup spam");
    AssertFalse(slotsManager.Contains("StartGameFromContinue"), "selecting a save should not auto-click Continue");
    AssertFalse(slotsManager.Contains("LoadingGameRequested"), "auto-load state should be removed so the Saves button cannot disappear from this path");
    AssertFalse(slotsManager.Contains("onClick") && slotsManager.Contains("Invoke"), "slot switching should not invoke the Continue button");
    AssertFalse(saveSlots.Contains("AutoLoadSelectedSave"), "settings should not contain the immediate-load option");
    AssertFalse(saveSlots.Contains("Load selected save immediately"), "settings should not show the immediate-load label");
}

static void PortedOriginalHidesContinueForEmptySavesAndLoading()
{
    var root = FindWorkspaceRoot();
    var dll = Path.Combine(root, "dist", "SaveSlots.dll");
    if (!File.Exists(dll))
    {
        throw new Exception("Build the original SaveSlots port before running this check: " + dll);
    }

    var slotsManager = RunIlSpy(dll, "SaveSlots.SlotsManager");
    var saveSlots = RunIlSpy(dll, "SaveSlots.SaveSlots");
    var loadingBehaviour = RunIlSpy(dll, "SaveSlots.LoadingBehaviour");
    AssertTrue(slotsManager.Contains("SetContinueVisible") && slotsManager.Contains("UpdateContinueButton"), "Continue visibility should go through one helper");
    AssertTrue(slotsManager.Contains("FindContinueButtons") && slotsManager.Contains("foreach (GameObject"), "all Continue button candidates should be updated, not only one cached object");
    AssertTrue(saveSlots.Contains("SyncContinueButtonToActiveSave"), "menu update should resync Continue visibility from active savefile.txt");
    AssertTrue(slotsManager.Contains("SetContinueVisible(HasPlayableSave(Application.persistentDataPath))"), "clicking the current slot should refresh Continue based on active savefile.txt");
    AssertTrue(slotsManager.Contains("HideContinueButton"), "SlotsManager should expose a loading-safe Continue hide helper");
    AssertTrue(loadingBehaviour.Contains("HideContinueButton"), "loading screen should hide the game Continue button as well as the Save Slots canvas");
}

static void PortedOriginalKeepsDeleteConfirmationAboveSaveUi()
{
    var root = FindWorkspaceRoot();
    var dll = Path.Combine(root, "dist", "SaveSlots.dll");
    if (!File.Exists(dll))
    {
        throw new Exception("Build the original SaveSlots port before running this check: " + dll);
    }

    var deleteButton = RunIlSpy(dll, "SaveSlots.DeleteSaveButton");
    var slotsManager = RunIlSpy(dll, "SaveSlots.SlotsManager");
    AssertTrue(slotsManager.Contains("HideSaveSlotsCanvasForPrompt"), "SlotsManager should expose a helper to hide Save Slots before MSCLoader modal prompts");
    AssertTrue(deleteButton.Contains("HideSaveSlotsCanvasForPrompt"), "delete confirmation should hide the Save Slots canvas before opening the confirmation modal");
}

static void PortedOriginalDoesNotPersistEmptyProfileJunk()
{
    var root = FindWorkspaceRoot();
    var dll = Path.Combine(root, "dist", "SaveSlots.dll");
    if (!File.Exists(dll))
    {
        throw new Exception("Build the original SaveSlots port before running this check: " + dll);
    }

    var slotsManager = RunIlSpy(dll, "SaveSlots.SlotsManager");
    AssertTrue(slotsManager.Contains("ActiveSlot.txt") && slotsManager.Contains("WriteActiveSlotMarker"), "selected empty slots should be remembered outside the active save folder");
    AssertTrue(slotsManager.Contains("PersistActiveSaveIfChanged") && slotsManager.Contains("ReplaceSlotFolderFromActive"), "new game saves should be mirrored into the selected slot after savefile.txt appears");
    AssertTrue(slotsManager.Contains("DeleteEmptySlotFolder") && slotsManager.Contains("DeleteProfileContents"), "empty slots should not keep Mods.txt, steam_autocloud.vdf, or SaveSlots.xml as fake saves");
    AssertTrue(slotsManager.Contains("HasPlayableSave(Application.persistentDataPath)") && slotsManager.Contains("SetContinueVisible(HasPlayableSave(Application.persistentDataPath))"), "Continue should be recalculated from the real active savefile.txt");
}

static void PortedOriginalDeletesProfilesCompletely()
{
    var root = FindWorkspaceRoot();
    var dll = Path.Combine(root, "dist", "SaveSlots.dll");
    if (!File.Exists(dll))
    {
        throw new Exception("Build the original SaveSlots port before running this check: " + dll);
    }

    var slotsManager = RunIlSpy(dll, "SaveSlots.SlotsManager");
    var deleteButton = RunIlSpy(dll, "SaveSlots.DeleteSaveButton");
    AssertTrue(slotsManager.Contains("DeleteCurrentActiveSlot"), "deleting the selected slot should clear the active profile instead of refusing");
    AssertTrue(slotsManager.Contains("DeleteDirectorySafe") && slotsManager.Contains("DeleteFileSafe"), "delete should unlock and remove complete profile folders/files");
    AssertTrue(slotsManager.Contains("MoveActiveSaveToEmergencyBackup") && slotsManager.Contains("CopyOptionsToActiveSave"), "deleting an active real save should keep an emergency backup and preserve shared options");
    AssertFalse(deleteButton.Contains("Can't delete currently active save."), "current active slot should be deletable after confirmation");
}

static void PortedOriginalAppliesBlueTheme()
{
    var root = FindWorkspaceRoot();
    var dll = Path.Combine(root, "dist", "SaveSlots.dll");
    if (!File.Exists(dll))
    {
        throw new Exception("Build the original SaveSlots port before running this check: " + dll);
    }

    var saveSlots = RunIlSpy(dll, "SaveSlots.SaveSlots");
    var slotsManager = RunIlSpy(dll, "SaveSlots.SlotsManager");
    AssertTrue(saveSlots.Contains("ApplyBlueTheme"), "original UI prefab should be recolored to MWC blue at runtime");
    AssertTrue(saveSlots.Contains("ApplyBlueTextTheme"), "yellow original UI text should be recolored to MWC blue at runtime");
    AssertTrue(slotsManager.Contains("BlueActive") && slotsManager.Contains("BlueInactive"), "save slot active/inactive colors should be blue");
}

static void PortedOriginalUiAvoidsModLoaderMenuOverlay()
{
    var root = FindWorkspaceRoot();
    var dll = Path.Combine(root, "dist", "SaveSlots.dll");
    if (!File.Exists(dll))
    {
        throw new Exception("Build the original SaveSlots port before running this check: " + dll);
    }

    var decompiled = RunIlSpy(dll, "SaveSlots.SaveSlots");
    AssertTrue(decompiled.Contains("EnsureClickSupport"), "Save Slots canvas should install raycast/event-system support for clickable slot cards");
    AssertTrue(decompiled.Contains("UpdateMenuVisibility") && decompiled.Contains("IsModLoaderMenuOpen"), "Save Slots canvas should respond to MSCLoader menu visibility");
    AssertTrue(decompiled.Contains("SetActive(value: true)") || decompiled.Contains("SetActive(true)"), "Save Slots menu canvas should remain visible in the game main menu");
    AssertTrue(decompiled.Contains("sortingOrder =") && decompiled.Contains("GraphicRaycaster") && decompiled.Contains("enabled = !flag"), "Save Slots should lower priority and disable raycasts while MSCLoader menu is open");
}

static void PortedOriginalExposesGitHubReleaseMetadataSafely()
{
    var root = FindWorkspaceRoot();
    var dll = Path.Combine(root, "dist", "SaveSlots.dll");
    if (!File.Exists(dll))
    {
        throw new Exception("Build the original SaveSlots port before running this check: " + dll);
    }

    var saveSlots = RunIlSpy(dll, "SaveSlots.SaveSlots");
    var bytes = File.ReadAllBytes(dll);
    AssertTrue(saveSlots.Contains("github.com/gabrielsk12/saveslots/releases"), "mod settings should point players to the new GitHub releases page");
    AssertTrue(saveSlots.Contains("gabriel_sk"), "mod settings should include the Discord contact");
    AssertFalse(saveSlots.Contains("discord.com/users/gabriel_sk"), "Discord contact button should not open an invalid Discord user URL");
    AssertFalse(saveSlots.Contains("Settings.AddButton(\"<color=#52D6FF>gabriel_sk</color>"), "Discord contact should not be a settings button");
    AssertTrue(ContainsUtf16(bytes, "github.com/gabrielsk12/saveslots"), "compiled mod should contain the new repository link");
}

static void PortedOriginalChecksGitHubReleasesSafely()
{
    var root = FindWorkspaceRoot();
    var dll = Path.Combine(root, "dist", "SaveSlots.dll");
    if (!File.Exists(dll))
    {
        throw new Exception("Build the original SaveSlots port before running this check: " + dll);
    }

    var saveSlots = RunIlSpy(dll, "SaveSlots.SaveSlots");
    var bytes = File.ReadAllBytes(dll);
    AssertTrue(saveSlots.Contains("CHECK FOR UPDATES") && saveSlots.Contains("CheckForUpdates"), "settings should include a ModLoader button to check GitHub releases");
    AssertTrue(saveSlots.Contains("api.github.com/repos/gabrielsk12/saveslots/releases"), "update check should read GitHub release metadata including beta releases");
    AssertTrue(saveSlots.Contains("ThreadPool.QueueUserWorkItem"), "network update checks should not freeze the game menu thread");
    AssertFalse(ContainsAscii(bytes, "DownloadFile"), "update check must not download or replace DLLs automatically");
}

static void PortedOriginalPublicBrandingIsGabrielOnly()
{
    var root = FindWorkspaceRoot();
    var dll = Path.Combine(root, "dist", "SaveSlots.dll");
    if (!File.Exists(dll))
    {
        throw new Exception("Build the original SaveSlots port before running this check: " + dll);
    }

    var decompiled = RunIlSpy(dll, "SaveSlots.SaveSlots");
    AssertTrue(decompiled.Contains("V 1.0"), "changelog should include the public release label");
    AssertTrue(decompiled.Contains("Mod published to Beta"), "changelog should mention the beta publishing state");
    AssertFalse(decompiled.Contains("Existing player saves get a Save1 profile"), "settings changelog should stay short enough for MSCLoader's text renderer");
    AssertFalse(decompiled.Contains("Athlon007"), "public mod branding should not mention Athlon007");
    AssertFalse(decompiled.Contains("Original Save Slots 1.0.2"), "public changelog should not include the old original mod changelog");
    AssertFalse(decompiled.Contains("â€¢"), "changelog should not contain mojibake bullet characters");
}

static void PortedOriginalKeepsNormalConsoleLoggingQuiet()
{
    var root = FindWorkspaceRoot();
    var dll = Path.Combine(root, "dist", "SaveSlots.dll");
    if (!File.Exists(dll))
    {
        throw new Exception("Build the original SaveSlots port before running this check: " + dll);
    }

    var slotsManager = RunIlSpy(dll, "SaveSlots.SlotsManager");
    AssertFalse(slotsManager.Contains("Loading save \" + sender.SlotFileName"), "normal save switching should not log every slot click");
    AssertFalse(slotsManager.Contains("[BACKUP] Archived previous emergency backup"), "normal emergency backup rotation should not spam the console");
    AssertFalse(slotsManager.Contains("[BACKUP] Moved old root backup folder"), "legacy backup migration should stay quiet unless it fails");
}

static void PortedOriginalAvoidsUnsafeUpdaterBehavior()
{
    var root = FindWorkspaceRoot();
    var dll = Path.Combine(root, "dist", "SaveSlots.dll");
    if (!File.Exists(dll))
    {
        throw new Exception("Build the original SaveSlots port before running this check: " + dll);
    }

    var bytes = File.ReadAllBytes(dll);
    AssertFalse(ContainsAscii(bytes, "ProcessStartInfo"), "public mod should not spawn external processes");
    AssertFalse(ContainsAscii(bytes, "cmd.exe"), "public mod should not invoke cmd.exe");
    AssertFalse(ContainsAscii(bytes, "powershell.exe"), "public mod should not invoke PowerShell");
    AssertFalse(ContainsAscii(bytes, "System.IO.Compression.ZipFile"), "GitHub update support should not self-extract or replace DLLs inside the game");
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new Exception($"Expected {expected}, got {actual}");
    }
}

static void AssertTrue(bool value, string message)
{
    if (!value) throw new Exception(message);
}

static void AssertFalse(bool value, string message)
{
    if (value) throw new Exception(message);
}

static void AssertFile(string path, string expected)
{
    if (!File.Exists(path)) throw new Exception($"Missing file: {path}");
    AssertEqual(expected, File.ReadAllText(path));
}

static string FindWorkspaceRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory != null && !File.Exists(Path.Combine(directory.FullName, "SaveSlotsMWC.sln")))
    {
        directory = directory.Parent;
    }

    if (directory == null)
    {
        throw new Exception("Could not locate workspace root.");
    }

    return directory.FullName;
}

static bool ContainsAscii(byte[] haystack, string needle)
{
    var bytes = System.Text.Encoding.ASCII.GetBytes(needle);
    for (var i = 0; i <= haystack.Length - bytes.Length; i++)
    {
        var match = true;
        for (var j = 0; j < bytes.Length; j++)
        {
            if (haystack[i + j] != bytes[j])
            {
                match = false;
                break;
            }
        }

        if (match)
        {
            return true;
        }
    }

    return false;
}

static bool ContainsUtf16(byte[] haystack, string needle)
{
    var bytes = System.Text.Encoding.Unicode.GetBytes(needle);
    for (var i = 0; i <= haystack.Length - bytes.Length; i++)
    {
        var match = true;
        for (var j = 0; j < bytes.Length; j++)
        {
            if (haystack[i + j] != bytes[j])
            {
                match = false;
                break;
            }
        }

        if (match)
        {
            return true;
        }
    }

    return false;
}

static string RunIlSpy(string assemblyPath, string typeName)
{
    var startInfo = new System.Diagnostics.ProcessStartInfo
    {
        FileName = "dotnet",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    startInfo.ArgumentList.Add("ilspycmd");
    startInfo.ArgumentList.Add("-t");
    startInfo.ArgumentList.Add(typeName);
    startInfo.ArgumentList.Add(assemblyPath);

    using var process = System.Diagnostics.Process.Start(startInfo);
    if (process == null)
    {
        throw new Exception("Could not start ilspycmd.");
    }

    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        throw new Exception("ilspycmd failed: " + error);
    }

    return output;
}

internal sealed class TempSaveTree : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "SaveSlotsMWCTests", Guid.NewGuid().ToString("N"));
    public string SaveRoot => Path.Combine(Root, "Amistech");
    public string ActivePath => Path.Combine(SaveRoot, "My Winter Car");
    public string SlotsPath => Path.Combine(SaveRoot, "SaveSlots");
    public string OptionsPath => Path.Combine(SlotsPath, "Options");

    public TempSaveTree()
    {
        Directory.CreateDirectory(ActivePath);
        Directory.CreateDirectory(SlotsPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, true);
        }
    }
}

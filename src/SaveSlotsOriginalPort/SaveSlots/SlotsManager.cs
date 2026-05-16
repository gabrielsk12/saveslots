using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSCLoader;
using UnityEngine;
using UnityEngine.UI;
using UnityObject = UnityEngine.Object;

namespace SaveSlots
{
internal class SlotsManager : MonoBehaviour
{
	private static readonly Color BlueActive = new Color32(64, 139, 202, byte.MaxValue);

	private static readonly Color BlueInactive = new Color32(13, 58, 101, byte.MaxValue);

	private const string MwcSaveFileName = "savefile.txt";

	private const string ActiveSlotFileName = "ActiveSlot.txt";

	private const int DefaultMaximumEmergencyBackups = 5;

	private static SlotsManager instance;

	private readonly List<GameObject> continueButtonCache = new List<GameObject>();

	public Color colorActive;

	public Color colorInnactive;

	public List<SlotBehaviour> slotBehaviours;

	private const string SlotFolderNameTemplate = "Save{0}";

	public const string SaveDataFileName = "SaveSlots";

	private GameObject buttonContinue;

	private bool isSwitchingSave;

	private bool continueRefreshEnabled;

	private bool selectedSlotRestoreFailed;

	private bool applicationQuitting;

	private string selectedSlotName;

	private DateTime nextContinueButtonSearchUtc = DateTime.MinValue;

	private DateTime lastPersistedActiveSaveWriteUtc = DateTime.MinValue;

	private readonly Dictionary<Vector3, string> saves = new Dictionary<Vector3, string>
	{
		{
			new Vector3(-1548.43f, 3.726f, 1187.219f),
			"PERAJARVI"
		},
		{
			new Vector3(-779.5585f, 12.599f, -648.1967f),
			"LANDFILL"
		},
		{
			new Vector3(-837.2703f, -2.319f, 506.7076f),
			"COTTAGE"
		},
		{
			new Vector3(1565.755f, 5.349002f, 721.2099f),
			"REPAIRSHOP"
		},
		{
			new Vector3(-161.1573f, -3.437f, 1025.414f),
			"CABIN"
		},
		{
			new Vector3(-8.205001f, -0.2180009f, 11.973f),
			"HOME"
		},
		{
			new Vector3(-654.719f, 4.384f, -1154.57f),
			"JAIL"
		}
	};

	public static SlotsManager Instance => instance;

	public string SaveRoot => Application.persistentDataPath.Replace(Application.productName, "");

	public string SaveSlotsFolder => Path.Combine(SaveRoot, "SaveSlots");

	public string OptionsFolder => Path.Combine(SaveSlotsFolder, "Options");

	private string BackupFolder => Path.Combine(SaveRoot, "SAVE SLOTS BACKUP");

	private string EmergencyBackupsFolder => Path.Combine(SaveSlotsFolder, "EmergencyBackups");

	private string ActiveSlotMarkerPath => Path.Combine(SaveSlotsFolder, ActiveSlotFileName);

	private void Awake()
	{
		instance = this;
		slotBehaviours = new List<SlotBehaviour>();
		colorActive = BlueActive;
		colorInnactive = BlueInactive;
		if (!Directory.Exists(SaveSlotsFolder))
		{
			Directory.CreateDirectory(SaveSlotsFolder);
		}
		if (!Directory.Exists(OptionsFolder))
		{
			Directory.CreateDirectory(OptionsFolder);
		}
		if (!Directory.Exists(EmergencyBackupsFolder))
		{
			Directory.CreateDirectory(EmergencyBackupsFolder);
		}
		MigrateLegacyRootBackups();
		EnsureFirstInstallProfile();
		GameObject loading = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault((GameObject g) => ((UnityObject)g).name == "Loading" && (UnityObject)(object)g.transform.parent == (UnityObject)null);
		if ((UnityObject)(object)loading != (UnityObject)null && (UnityObject)(object)loading.GetComponent<LoadingBehaviour>() == (UnityObject)null)
		{
			loading.AddComponent<LoadingBehaviour>();
		}
		GameObject interfaceObject = GameObject.Find("Interface");
		buttonContinue = LocateContinueButton(interfaceObject);
		ReconcileActiveProfileWithSelectedSlot();
		SyncContinueButtonToActiveSave();
		GameObject licence = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault((GameObject g) => ((UnityObject)g).name == "Licence");
		if ((UnityObject)(object)licence != (UnityObject)null && (UnityObject)(object)licence.GetComponent<SaveSlotsLicenceBehaviour>() == (UnityObject)null)
		{
			licence.AddComponent<SaveSlotsLicenceBehaviour>();
		}
	}

	private void Start()
	{
		((Component)this).gameObject.SetActive(false);
	}

	private void LateUpdate()
	{
		if (!applicationQuitting && continueRefreshEnabled)
		{
			RefreshContinueButtonFromSelectedSlot();
		}
	}

	internal void SetContinueRefreshEnabled(bool enabled)
	{
		if (applicationQuitting)
		{
			return;
		}
		if (!enabled)
		{
			if (continueRefreshEnabled)
			{
				HideContinueButton();
			}
			continueRefreshEnabled = false;
			return;
		}
		continueRefreshEnabled = true;
		RefreshContinueButtonFromSelectedSlot();
	}

	internal void Add(SlotBehaviour slotBehaviour)
	{
		slotBehaviours.Add(slotBehaviour);
		slotBehaviour.SlotFileName = "Save" + slotBehaviours.Count;
	}

	internal void UpdateSelectedButtons(SlotBehaviour sender)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		for (int i = 0; i < slotBehaviours.Count; i++)
		{
			slotBehaviours[i].SetColor(((UnityObject)(object)slotBehaviours[i] == (UnityObject)(object)sender) ? colorActive : colorInnactive);
		}
	}

	internal void UpdateInfoOfAllSaves()
	{
		for (int i = 0; i < slotBehaviours.Count; i++)
		{
			slotBehaviours[i].LoadSaveData();
		}
	}

	internal SlotBehaviour CurrentSaveLoaded()
	{
		return slotBehaviours.FirstOrDefault((SlotBehaviour s) => s.SlotFileName == CurrentSaveLoadedName());
	}

	internal string CurrentSaveLoadedName()
	{
		if (!string.IsNullOrEmpty(selectedSlotName))
		{
			return NormalizeSlotName(selectedSlotName);
		}
		string markerSlotName = LoadActiveSlotMarker();
		if (!string.IsNullOrEmpty(markerSlotName))
		{
			selectedSlotName = markerSlotName;
			return markerSlotName;
		}
		if (!File.Exists(Path.Combine(Application.persistentDataPath, "SaveSlots.xml")))
		{
			return string.Format("Save{0}", "1");
		}
		try
		{
			SaveData saveData = ModSave.Load<SaveData>("SaveSlots", "");
			if (saveData != null && !string.IsNullOrEmpty(saveData.slotName))
			{
				selectedSlotName = NormalizeSlotName(saveData.slotName);
				return selectedSlotName;
			}
		}
		catch (Exception ex)
		{
			ModConsole.LogError("Save Slots could not read SaveSlots.xml. Falling back to Save1.\n" + ex);
		}
		return string.Format("Save{0}", "1");
	}

	private string NormalizeSlotName(string slotName)
	{
		if (string.IsNullOrEmpty(slotName))
		{
			return "Save1";
		}
		if (!slotName.StartsWith("Save", StringComparison.OrdinalIgnoreCase))
		{
			return "Save1";
		}
		int slotNumber;
		if (!int.TryParse(slotName.Substring(4), out slotNumber) || slotNumber < 1 || slotNumber > 3)
		{
			return "Save1";
		}
		return "Save" + slotNumber;
	}

	private string LoadActiveSlotMarker()
	{
		try
		{
			if (!File.Exists(ActiveSlotMarkerPath))
			{
				return null;
			}
			return NormalizeSlotName(File.ReadAllText(ActiveSlotMarkerPath).Trim());
		}
		catch (Exception ex)
		{
			ModConsole.LogError("Save Slots could not read active slot marker.\n" + ex);
			return null;
		}
	}

	private void WriteActiveSlotMarker(string slotName)
	{
		try
		{
			Directory.CreateDirectory(SaveSlotsFolder);
			selectedSlotName = NormalizeSlotName(slotName);
			File.WriteAllText(ActiveSlotMarkerPath, selectedSlotName);
		}
		catch (Exception ex)
		{
			ModConsole.LogError("Save Slots could not write active slot marker.\n" + ex);
		}
	}

	internal void LoadSave(SlotBehaviour sender)
	{
		if (isSwitchingSave)
		{
			return;
		}
		isSwitchingSave = true;
		try
		{
			LoadSaveInternal(sender);
		}
		finally
		{
			isSwitchingSave = false;
		}
	}

	private void LoadSaveInternal(SlotBehaviour sender)
	{
		string activePath = Application.persistentDataPath;
		string currentSlotName = CurrentSaveLoadedName();
		selectedSlotRestoreFailed = false;
		if (currentSlotName == sender.SlotFileName)
		{
			ReconcileActiveProfileWithSelectedSlot();
			sender.LoadSaveData();
			UpdateContinueButton(ShouldShowContinueForSelectedSlot());
			return;
		}
		try
		{
			if (HasPlayableSave(activePath))
			{
				string activeProfileSlotName = GetActiveProfileSlotName();
				StoreActiveProfileInSlot(!string.IsNullOrEmpty(activeProfileSlotName) ? activeProfileSlotName : currentSlotName);
			}
			else
			{
				DeleteProfileContents(activePath, preserveSharedOptions: true);
				Directory.CreateDirectory(activePath);
				CopyOptionsToActiveSave(activePath);
			}
		}
		catch (Exception ex)
		{
			FailSafe("SAVE SLOTS STORE CURRENT", activePath, ex);
			return;
		}
		string targetSlotPath = Path.Combine(SaveSlotsFolder, sender.SlotFileName);
		bool targetHasSave = HasPlayableSave(targetSlotPath);
		try
		{
			if (!targetHasSave)
			{
				DeleteEmptySlotFolder(targetSlotPath);
			}
			PrepareActiveProfileForSlotSwitch();
			if (targetHasSave)
			{
				DirectoryCopy(targetSlotPath, activePath, copySubDirs: true);
				if (SaveSlots.SynchronizeOptions.GetValue())
				{
					CopyOptionsToActiveSave(activePath);
				}
			}
			else
			{
				CopyOptionsToActiveSave(activePath);
			}
			WriteActiveSlotMarker(sender.SlotFileName);
			if (targetHasSave)
			{
				ModSave.Save<SaveData>("SaveSlots", new SaveData(sender.SlotFileName, DateTime.Now), (string)null);
			}
			else
			{
				DeleteFileSafe(Path.Combine(activePath, "SaveSlots.xml"));
			}
			sender.LoadSaveData();
			UpdateContinueButton(ShouldShowContinueForSelectedSlot());
		}
		catch (Exception ex2)
		{
			FailSafe("SAVE SLOTS LOAD TARGET", targetSlotPath, ex2);
		}
	}

	private bool InitializeCurrentEmptySlot(SlotBehaviour sender)
	{
		string saveSlotsData = Path.Combine(Application.persistentDataPath, "SaveSlots.xml");
		string defaultSaveData = Path.Combine(Application.persistentDataPath, MwcSaveFileName);
		WriteActiveSlotMarker(sender.SlotFileName);
		if (File.Exists(saveSlotsData) && HasPlayableSave(Application.persistentDataPath))
		{
			UpdateContinueButton(ShouldShowContinueForSelectedSlot());
			return false;
		}
		if (HasActiveSaveData())
		{
			bool result = EnsureFirstInstallProfile();
			if (result)
			{
				sender.LoadSaveData();
				UpdateContinueButton(ShouldShowContinueForSelectedSlot());
			}
			return result;
		}
		if (!Directory.Exists(Application.persistentDataPath))
		{
			Directory.CreateDirectory(Application.persistentDataPath);
		}
		DeleteProfileContents(Application.persistentDataPath, preserveSharedOptions: true);
		CopyOptionsToActiveSave(Application.persistentDataPath);
		sender.UpdateInfoData(isActive: true, "?", 0f, mortal: false, "UNKNOWN", "NEVER");
		DeleteFileSafe(saveSlotsData);
		SetContinueVisible(false);
		return true;
	}

	private bool EnsureFirstInstallProfile()
	{
		string saveSlotsData = Path.Combine(Application.persistentDataPath, "SaveSlots.xml");
		if (File.Exists(saveSlotsData) || !HasPlayableSave(Application.persistentDataPath))
		{
			return false;
		}
		try
		{
			if (!Directory.Exists(Application.persistentDataPath))
			{
				Directory.CreateDirectory(Application.persistentDataPath);
			}
			SaveData saveData = new SaveData("Save1", DateTime.Now);
			WriteActiveSlotMarker(saveData.slotName);
			ModSave.Save<SaveData>("SaveSlots", saveData, (string)null);
			CopyActiveOptionsToSharedFolder();
			string save1Folder = Path.Combine(SaveSlotsFolder, "Save1");
			if (!Directory.Exists(save1Folder) || IsDirectoryEmpty(save1Folder))
			{
				Directory.CreateDirectory(save1Folder);
				DirectoryCopy(Application.persistentDataPath, save1Folder, copySubDirs: true);
			}
			return true;
		}
		catch (Exception ex)
		{
			ModPrompt.CreatePrompt("Save Slots detected an existing save, but could not create a Save1 profile.\nYour active save was left in place and was not moved or deleted.\n\nAn exception has been saved into output_log.txt. Please send it to the mod author.", "Save Slots - First Install Safety", null);
			ModConsole.LogError("SAVE SLOTS FIRST INSTALL\n" + ex);
			return false;
		}
	}

	private bool HasActiveSaveData()
	{
		return HasPlayableSave(Application.persistentDataPath);
	}

	private bool IsDirectoryEmpty(string path)
	{
		return Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length == 0;
	}

	private void CopyActiveOptionsToSharedFolder()
	{
		foreach (string fileName in new string[2] { "options.txt", "calibrator.cfg" })
		{
			string sourcePath = Path.Combine(Application.persistentDataPath, fileName);
			if (File.Exists(sourcePath))
			{
				File.Copy(sourcePath, Path.Combine(OptionsFolder, fileName), overwrite: true);
			}
		}
	}

	private void SaveCurrentMetadata(string slotName)
	{
		string normalizedSlotName = NormalizeSlotName(slotName);
		WriteActiveSlotMarker(normalizedSlotName);
		if (!Directory.Exists(Application.persistentDataPath))
		{
			return;
		}
		if (HasPlayableSave(Application.persistentDataPath))
		{
			ModSave.Save<SaveData>("SaveSlots", new SaveData(normalizedSlotName, DateTime.Now), (string)null);
		}
	}

	private void StoreActiveProfileInSlot(string slotName)
	{
		string normalizedSlotName = NormalizeSlotName(slotName);
		WriteActiveSlotMarker(normalizedSlotName);
		CopyActiveOptionsToSharedFolder();
		string slotPath = Path.Combine(SaveSlotsFolder, normalizedSlotName);
		if (HasPlayableSave(Application.persistentDataPath))
		{
			SaveCurrentMetadata(normalizedSlotName);
			ReplaceSlotFolderFromActive(slotPath);
			return;
		}
		DeleteEmptySlotFolder(slotPath);
		DeleteProfileContents(Application.persistentDataPath, preserveSharedOptions: true);
		CopyOptionsToActiveSave(Application.persistentDataPath);
	}

	private void ReplaceSlotFolderFromActive(string slotPath)
	{
		string tempSlotPath = NextAvailableDirectoryName(slotPath + ".tmp");
		try
		{
			DirectoryCopy(Application.persistentDataPath, tempSlotPath, copySubDirs: true);
			if (Directory.Exists(slotPath))
			{
				DeleteDirectorySafe(slotPath);
			}
			Directory.Move(tempSlotPath, slotPath);
		}
		catch
		{
			if (Directory.Exists(tempSlotPath))
			{
				DeleteDirectorySafe(tempSlotPath);
			}
			throw;
		}
	}

	private void DeleteEmptySlotFolder(string slotPath)
	{
		if (Directory.Exists(slotPath) && !HasPlayableSave(slotPath))
		{
			DeleteDirectorySafe(slotPath);
		}
	}

	private void PrepareActiveProfileForSlotSwitch()
	{
		if (HasPlayableSave(Application.persistentDataPath))
		{
			MoveActiveSaveToEmergencyBackup();
		}
		else
		{
			DeleteProfileContents(Application.persistentDataPath, preserveSharedOptions: true);
		}
		Directory.CreateDirectory(Application.persistentDataPath);
	}

	private void CopyActiveSaveToSlot(string activePath, string slotPath)
	{
		DirectoryInfo activeDirectory = new DirectoryInfo(activePath);
		foreach (FileInfo fileInfo in activeDirectory.GetFiles())
		{
			if (!ShouldCopy(fileInfo.Name))
			{
				continue;
			}
			UnlockFile(fileInfo);
			if (fileInfo.Name.EqualsAny("options.txt", "calibrator.cfg"))
			{
				fileInfo.CopyTo(Path.Combine(OptionsFolder, fileInfo.Name), overwrite: true);
			}
			else
			{
				fileInfo.CopyTo(Path.Combine(slotPath, fileInfo.Name), overwrite: true);
			}
		}
		foreach (DirectoryInfo directoryInfo in activeDirectory.GetDirectories())
		{
			DirectoryCopy(directoryInfo.FullName, Path.Combine(slotPath, directoryInfo.Name), copySubDirs: true);
		}
	}

	private void CopyOptionsToActiveSave(string activePath)
	{
		if (!Directory.Exists(OptionsFolder))
		{
			return;
		}
		foreach (FileInfo fileInfo in new DirectoryInfo(OptionsFolder).GetFiles())
		{
			UnlockFile(fileInfo);
			fileInfo.CopyTo(Path.Combine(activePath, fileInfo.Name), overwrite: true);
		}
	}

	private void MoveActiveSaveToEmergencyBackup()
	{
		ArchiveExistingBackupFolder();
		if (Directory.Exists(BackupFolder))
		{
			throw new IOException("Emergency backup folder is still present after archive rotation: " + BackupFolder);
		}
		Directory.Move(Application.persistentDataPath, BackupFolder);
		PruneEmergencyBackups();
	}

	private void ArchiveExistingBackupFolder()
	{
		if (!Directory.Exists(BackupFolder))
		{
			return;
		}
		Directory.CreateDirectory(EmergencyBackupsFolder);
		string archivePath = NextAvailableDirectoryName(Path.Combine(EmergencyBackupsFolder, DateTime.Now.ToString("yyyyMMdd_HHmmss")));
		Directory.Move(BackupFolder, archivePath);
	}

	private void MigrateLegacyRootBackups()
	{
		try
		{
			Directory.CreateDirectory(EmergencyBackupsFolder);
			foreach (string legacyBackup in Directory.GetDirectories(SaveRoot, "SAVE SLOTS BACKUP_*"))
			{
				string name = Path.GetFileName(legacyBackup);
				string target = NextAvailableDirectoryName(Path.Combine(EmergencyBackupsFolder, name));
				Directory.Move(legacyBackup, target);
			}
			PruneEmergencyBackups();
		}
		catch (Exception ex)
		{
			ModConsole.LogError("Save Slots could not migrate old backup folders.\n" + ex);
		}
	}

	private void PruneEmergencyBackups()
	{
		if (!Directory.Exists(EmergencyBackupsFolder))
		{
			return;
		}
		int maximumBackups = GetMaximumEmergencyBackups();
		if (maximumBackups < 1)
		{
			return;
		}
		DirectoryInfo[] backups = new DirectoryInfo(EmergencyBackupsFolder).GetDirectories()
			.Where((DirectoryInfo backup) => !backup.Name.StartsWith("SAVE SLOTS BACKUP_", StringComparison.OrdinalIgnoreCase))
			.ToArray();
		Array.Sort(backups, (DirectoryInfo left, DirectoryInfo right) => right.CreationTimeUtc.CompareTo(left.CreationTimeUtc));
		for (int i = maximumBackups; i < backups.Length; i++)
		{
			backups[i].Delete(recursive: true);
		}
	}

	private int GetMaximumEmergencyBackups()
	{
		if (SaveSlots.MaxEmergencyBackups == null)
		{
			return DefaultMaximumEmergencyBackups;
		}
		int value = SaveSlots.MaxEmergencyBackups.GetValue();
		return value < 1 ? DefaultMaximumEmergencyBackups : value;
	}

	private string NextAvailableDirectoryName(string preferredPath)
	{
		if (!Directory.Exists(preferredPath))
		{
			return preferredPath;
		}
		int suffix = 2;
		string candidate;
		do
		{
			candidate = preferredPath + "_" + suffix;
			suffix++;
		}
		while (Directory.Exists(candidate));
		return candidate;
	}

	private bool ShouldCopy(string fileName)
	{
		return SaveSlots.CopyMSCEditorBackups.GetValue() || !fileName.ContainsAny("_backup");
	}

	private bool IsSharedOptionsFile(string fileName)
	{
		return fileName.Equals("options.txt", StringComparison.OrdinalIgnoreCase) || fileName.Equals("calibrator.cfg", StringComparison.OrdinalIgnoreCase);
	}

	private bool HasPlayableSave(string path)
	{
		return Directory.Exists(path) && File.Exists(Path.Combine(path, MwcSaveFileName));
	}

	private void UpdateContinueButton(bool visible)
	{
		SetContinueVisible(visible);
	}

	internal void PersistActiveSaveNow()
	{
		PersistActiveSave(force: true);
	}

	internal void PersistActiveSaveIfChanged()
	{
		PersistActiveSave(force: false);
	}

	private void PersistActiveSave(bool force)
	{
		string currentSlotName = CurrentSaveLoadedName();
		WriteActiveSlotMarker(currentSlotName);
		string saveFile = Path.Combine(Application.persistentDataPath, MwcSaveFileName);
		if (!File.Exists(saveFile))
		{
			lastPersistedActiveSaveWriteUtc = DateTime.MinValue;
			return;
		}
		DateTime writeTimeUtc = File.GetLastWriteTimeUtc(saveFile);
		if (!force && writeTimeUtc == lastPersistedActiveSaveWriteUtc)
		{
			return;
		}
		SaveCurrentMetadata(currentSlotName);
		ReplaceSlotFolderFromActive(Path.Combine(SaveSlotsFolder, currentSlotName));
		lastPersistedActiveSaveWriteUtc = writeTimeUtc;
	}

	internal void SyncContinueButtonToActiveSave()
	{
		RefreshContinueButtonFromSelectedSlot();
	}

	internal void RefreshContinueButtonFromSelectedSlot()
	{
		SetContinueVisible(ShouldShowContinueForSelectedSlot());
	}

	private bool ShouldShowContinueForSelectedSlot()
	{
		return HasPlayableSave(Application.persistentDataPath) && ActiveProfileMatchesSelectedSlot(CurrentSaveLoadedName());
	}

	private bool ActiveProfileMatchesSelectedSlot(string selectedSlot)
	{
		string activeProfileSlotName = GetActiveProfileSlotName();
		return !string.IsNullOrEmpty(activeProfileSlotName) && string.Equals(activeProfileSlotName, NormalizeSlotName(selectedSlot), StringComparison.OrdinalIgnoreCase);
	}

	private string GetActiveProfileSlotName()
	{
		try
		{
			if (!File.Exists(Path.Combine(Application.persistentDataPath, "SaveSlots.xml")))
			{
				return null;
			}
			SaveData saveData = ModSave.Load<SaveData>("SaveSlots", "");
			return saveData != null && !string.IsNullOrEmpty(saveData.slotName) ? NormalizeSlotName(saveData.slotName) : null;
		}
		catch (Exception ex)
		{
			ModConsole.LogError("Save Slots could not read active profile metadata.\n" + ex);
			return null;
		}
	}

	private bool ShouldSynchronizeOptions()
	{
		return SaveSlots.SynchronizeOptions == null || SaveSlots.SynchronizeOptions.GetValue();
	}

	private void ReconcileActiveProfileWithSelectedSlot()
	{
		if (selectedSlotRestoreFailed)
		{
			return;
		}
		string currentSlotName = CurrentSaveLoadedName();
		string slotPath = Path.Combine(SaveSlotsFolder, currentSlotName);
		try
		{
			string activeProfileSlotName = GetActiveProfileSlotName();
			bool activeProfileHasSave = HasPlayableSave(Application.persistentDataPath);
			bool activeProfileMatchesSelectedSlot = activeProfileHasSave && ActiveProfileMatchesSelectedSlot(currentSlotName);
			bool selectedSlotHasSave = HasPlayableSave(slotPath) || activeProfileMatchesSelectedSlot;
			if (selectedSlotHasSave)
			{
				if (activeProfileHasSave && !ActiveProfileMatchesSelectedSlot(currentSlotName))
				{
					if (!string.IsNullOrEmpty(activeProfileSlotName) && !string.Equals(activeProfileSlotName, currentSlotName, StringComparison.OrdinalIgnoreCase))
					{
						StoreActiveProfileInSlot(activeProfileSlotName);
					}
					else
					{
						CopyActiveOptionsToSharedFolder();
						MoveActiveSaveToEmergencyBackup();
						Directory.CreateDirectory(Application.persistentDataPath);
					}
				}
				if (!HasPlayableSave(Application.persistentDataPath) || !ActiveProfileMatchesSelectedSlot(currentSlotName))
				{
					CopyActiveOptionsToSharedFolder();
					DeleteProfileContents(Application.persistentDataPath, preserveSharedOptions: true);
					Directory.CreateDirectory(Application.persistentDataPath);
					DirectoryCopy(slotPath, Application.persistentDataPath, copySubDirs: true);
					if (ShouldSynchronizeOptions())
					{
						CopyOptionsToActiveSave(Application.persistentDataPath);
					}
					ModSave.Save<SaveData>("SaveSlots", new SaveData(currentSlotName, DateTime.Now), (string)null);
					lastPersistedActiveSaveWriteUtc = File.GetLastWriteTimeUtc(Path.Combine(Application.persistentDataPath, MwcSaveFileName));
				}
				WriteActiveSlotMarker(currentSlotName);
				return;
			}
			if (activeProfileHasSave)
			{
				if (!string.IsNullOrEmpty(activeProfileSlotName) && !string.Equals(activeProfileSlotName, currentSlotName, StringComparison.OrdinalIgnoreCase))
				{
					StoreActiveProfileInSlot(activeProfileSlotName);
				}
				else
				{
					CopyActiveOptionsToSharedFolder();
					MoveActiveSaveToEmergencyBackup();
					Directory.CreateDirectory(Application.persistentDataPath);
				}
			}
			DeleteEmptySlotFolder(slotPath);
			if (!Directory.Exists(Application.persistentDataPath))
			{
				Directory.CreateDirectory(Application.persistentDataPath);
			}
			DeleteProfileContents(Application.persistentDataPath, preserveSharedOptions: true);
			CopyOptionsToActiveSave(Application.persistentDataPath);
			DeleteFileSafe(Path.Combine(Application.persistentDataPath, "SaveSlots.xml"));
			WriteActiveSlotMarker(currentSlotName);
		}
		catch (Exception ex)
		{
			selectedSlotRestoreFailed = true;
			FailSafe("SAVE SLOTS RESTORE SELECTED", slotPath, ex);
		}
	}

	internal void HideContinueButton()
	{
		SetContinueVisible(false);
	}

	private void SetContinueVisible(bool visible)
	{
		List<GameObject> continueButtons = FindContinueButtons();
		foreach (GameObject continueButton in continueButtons)
		{
			continueButton.SetActive(visible);
		}
	}

	private void FindContinueButton()
	{
		List<GameObject> continueButtons = FindContinueButtons();
		buttonContinue = continueButtons.Count > 0 ? continueButtons[0] : null;
	}

	private List<GameObject> FindContinueButtons()
	{
		List<GameObject> continueButtons = new List<GameObject>();
		for (int i = continueButtonCache.Count - 1; i >= 0; i--)
		{
			GameObject cachedButton = continueButtonCache[i];
			if ((UnityObject)(object)cachedButton == (UnityObject)null)
			{
				continueButtonCache.RemoveAt(i);
			}
			else
			{
				AddContinueCandidate(continueButtons, cachedButton);
			}
		}
		if (continueButtons.Count > 0)
		{
			buttonContinue = continueButtons[0];
			return continueButtons;
		}
		if (DateTime.UtcNow < nextContinueButtonSearchUtc)
		{
			return continueButtons;
		}
		nextContinueButtonSearchUtc = DateTime.UtcNow.AddSeconds(1.0);
		GameObject interfaceObject = GameObject.Find("Interface");
		AddContinueCandidate(continueButtons, LocateContinueButton(interfaceObject));
		if ((UnityObject)(object)interfaceObject != (UnityObject)null)
		{
			Button[] buttons = interfaceObject.GetComponentsInChildren<Button>(true);
			foreach (Button button in buttons)
			{
				GameObject buttonObject = ((Component)button).gameObject;
				string searchText = GetButtonSearchText(buttonObject);
				if (searchText.Contains("BUTTONCONTINUE") || searchText.Contains("CONTINUE") || searchText.Contains("POKRA"))
				{
					AddContinueCandidate(continueButtons, buttonObject);
				}
			}
		}
		Button[] allButtons = Resources.FindObjectsOfTypeAll<Button>();
		foreach (Button button in allButtons)
		{
			GameObject buttonObject = ((Component)button).gameObject;
			string searchText = GetButtonSearchText(buttonObject);
			if (searchText.Contains("BUTTONCONTINUE") || searchText.Contains("CONTINUE") || searchText.Contains("POKRA"))
			{
				AddContinueCandidate(continueButtons, buttonObject);
			}
		}
		buttonContinue = continueButtons.Count > 0 ? continueButtons[0] : null;
		foreach (GameObject continueButton in continueButtons)
		{
			AddContinueCandidate(continueButtonCache, continueButton);
		}
		return continueButtons;
	}

	private void AddContinueCandidate(List<GameObject> continueButtons, GameObject candidate)
	{
		if ((UnityObject)(object)candidate == (UnityObject)null || continueButtons.Contains(candidate))
		{
			return;
		}
		continueButtons.Add(candidate);
		AttachContinueLoadingGuard(candidate);
	}

	private void AttachContinueLoadingGuard(GameObject buttonObject)
	{
		if ((UnityObject)(object)buttonObject == (UnityObject)null || (UnityObject)(object)buttonObject.GetComponent<ContinueLoadButtonGuard>() != (UnityObject)null)
		{
			return;
		}
		buttonObject.AddComponent<ContinueLoadButtonGuard>();
	}

	private GameObject LocateContinueButton(GameObject interfaceObject)
	{
		if ((UnityObject)(object)interfaceObject == (UnityObject)null)
		{
			return null;
		}
		Transform continueTransform = interfaceObject.transform.Find("Buttons/ButtonContinue");
		if ((UnityObject)(object)continueTransform != (UnityObject)null)
		{
			return ((Component)continueTransform).gameObject;
		}
		Button[] buttons = interfaceObject.GetComponentsInChildren<Button>(true);
		foreach (Button button in buttons)
		{
			string searchText = GetButtonSearchText(((Component)button).gameObject);
			if (searchText.Contains("BUTTONCONTINUE") || searchText.Contains("CONTINUE") || searchText.Contains("POKRA"))
			{
				return ((Component)button).gameObject;
			}
		}
		return null;
	}

	private string GetButtonSearchText(GameObject buttonObject)
	{
		if ((UnityObject)(object)buttonObject == (UnityObject)null)
		{
			return string.Empty;
		}
		string text = buttonObject.name;
		Transform parent = buttonObject.transform.parent;
		while ((UnityObject)(object)parent != (UnityObject)null)
		{
			text = text + " " + parent.name;
			parent = parent.parent;
		}
		foreach (Text label in buttonObject.GetComponentsInChildren<Text>(true))
		{
			text = text + " " + label.text;
		}
		return text.ToUpperInvariant();
	}

	private void FailSafe(string stage, string path, Exception ex)
	{
		ModPrompt.CreatePrompt("Save Slots stopped switching saves to prevent data loss.\nYour previous save is still in the SaveSlots folder or emergency backup folder.\n\nAn exception has been saved into output_log.txt. Please send it to Gabriel_SK.", "Save Slots - Fatal Error", null);
		ModConsole.LogError(stage + "\n" + path + "\n\n" + ex);
	}

	internal void DeleteSave(SlotBehaviour slotBehaviour)
	{
		if ((UnityObject)(object)slotBehaviour == (UnityObject)(object)CurrentSaveLoaded())
		{
			DeleteCurrentActiveSlot(slotBehaviour);
			return;
		}
		string path = Path.Combine(SaveSlotsFolder, slotBehaviour.SlotFileName);
		if (Directory.Exists(path))
		{
			DeleteDirectorySafe(path);
		}
		slotBehaviour.UpdateInfoData(isActive: false, "?", 0f, mortal: false, "UNKNOWN", "NEVER");
		UpdateInfoOfAllSaves();
		UpdateContinueButton(ShouldShowContinueForSelectedSlot());
	}

	private void DeleteCurrentActiveSlot(SlotBehaviour slotBehaviour)
	{
		string slotName = NormalizeSlotName(slotBehaviour.SlotFileName);
		try
		{
			CopyActiveOptionsToSharedFolder();
			if (HasPlayableSave(Application.persistentDataPath))
			{
				MoveActiveSaveToEmergencyBackup();
				Directory.CreateDirectory(Application.persistentDataPath);
			}
			else
			{
				DeleteProfileContents(Application.persistentDataPath, preserveSharedOptions: true);
				Directory.CreateDirectory(Application.persistentDataPath);
			}
			DeleteDirectorySafe(Path.Combine(SaveSlotsFolder, slotName));
			CopyOptionsToActiveSave(Application.persistentDataPath);
			WriteActiveSlotMarker(slotName);
			DeleteFileSafe(Path.Combine(Application.persistentDataPath, "SaveSlots.xml"));
			slotBehaviour.UpdateInfoData(isActive: false, "?", 0f, mortal: false, "UNKNOWN", "NEVER");
			UpdateInfoOfAllSaves();
			UpdateSelectedButtons(slotBehaviour);
			UpdateContinueButton(false);
		}
		catch (Exception ex)
		{
			FailSafe("SAVE SLOTS DELETE CURRENT", slotName, ex);
		}
	}

	public string GetClosestLocation(Vector3 position)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		KeyValuePair<Vector3, string> keyValuePair = new KeyValuePair<Vector3, string>(new Vector3(999999f, 999999f), "UNKNOWN");
		foreach (KeyValuePair<Vector3, string> safe in saves)
		{
			if (Vector3.Distance(position, safe.Key) < Vector3.Distance(position, keyValuePair.Key))
			{
				keyValuePair = safe;
			}
		}
		if (position == Vector3.zero)
		{
			return "UNKNOWN";
		}
		if (Vector3.Distance(position, keyValuePair.Key) > 750f)
		{
			return "X " + Mathf.RoundToInt(position.x) + " / Z " + Mathf.RoundToInt(position.z);
		}
		return keyValuePair.Value;
	}

	public GameObject Canvas()
	{
		return ((Component)((Component)this).transform.root).gameObject;
	}

	internal void HideSaveSlotsCanvasForPrompt()
	{
		GameObject canvas = Canvas();
		if ((UnityObject)(object)canvas != (UnityObject)null)
		{
			canvas.SetActive(false);
		}
	}

	private void OnApplicationFocus(bool hasFocus)
	{
		UpdateInfoOfAllSaves();
	}

	private void OnApplicationQuit()
	{
		applicationQuitting = true;
		continueRefreshEnabled = false;
	}

	private void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
	{
		DirectoryInfo directoryInfo = new DirectoryInfo(sourceDirName);
		if (!directoryInfo.Exists)
		{
			throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + sourceDirName);
		}
		DirectoryInfo[] directories = directoryInfo.GetDirectories();
		Directory.CreateDirectory(destDirName);
		FileInfo[] files = directoryInfo.GetFiles();
		foreach (FileInfo fileInfo in files)
		{
			string destFileName = Path.Combine(destDirName, fileInfo.Name);
			UnlockFile(fileInfo);
			if (File.Exists(destFileName))
			{
				File.SetAttributes(destFileName, File.GetAttributes(destFileName) & ~FileAttributes.ReadOnly);
			}
			fileInfo.CopyTo(destFileName, overwrite: true);
		}
		if (copySubDirs)
		{
			DirectoryInfo[] array = directories;
			foreach (DirectoryInfo directoryInfo2 in array)
			{
				string destDirName2 = Path.Combine(destDirName, directoryInfo2.Name);
				DirectoryCopy(directoryInfo2.FullName, destDirName2, copySubDirs);
			}
		}
	}

	private void UnlockFile(FileInfo fi)
	{
		File.SetAttributes(fi.FullName, File.GetAttributes(fi.FullName) & ~FileAttributes.ReadOnly);
	}

	private void DeleteProfileContents(string path, bool preserveSharedOptions)
	{
		if (!Directory.Exists(path))
		{
			return;
		}
		foreach (FileInfo fileInfo in new DirectoryInfo(path).GetFiles())
		{
			if (preserveSharedOptions && IsSharedOptionsFile(fileInfo.Name))
			{
				continue;
			}
			DeleteFileSafe(fileInfo.FullName);
		}
		foreach (DirectoryInfo directoryInfo in new DirectoryInfo(path).GetDirectories())
		{
			DeleteDirectorySafe(directoryInfo.FullName);
		}
	}

	private void DeleteDirectorySafe(string path)
	{
		if (!Directory.Exists(path))
		{
			return;
		}
		DirectoryInfo directoryInfo = new DirectoryInfo(path);
		foreach (FileInfo fileInfo in directoryInfo.GetFiles())
		{
			DeleteFileSafe(fileInfo.FullName);
		}
		foreach (DirectoryInfo childDirectory in directoryInfo.GetDirectories())
		{
			DeleteDirectorySafe(childDirectory.FullName);
		}
		UnlockPath(path);
		Directory.Delete(path, recursive: false);
	}

	private void DeleteFileSafe(string path)
	{
		if (!File.Exists(path))
		{
			return;
		}
		UnlockPath(path);
		File.Delete(path);
	}

	private void UnlockPath(string path)
	{
		FileAttributes attributes = File.GetAttributes(path);
		attributes &= ~FileAttributes.ReadOnly;
		attributes &= ~FileAttributes.Hidden;
		attributes &= ~FileAttributes.System;
		File.SetAttributes(path, attributes);
	}
}
}



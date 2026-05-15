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

	private const int DefaultMaximumEmergencyBackups = 5;

	private static SlotsManager instance;

	public Color colorActive;

	public Color colorInnactive;

	public List<SlotBehaviour> slotBehaviours;

	private const string SlotFolderNameTemplate = "Save{0}";

	public const string SaveDataFileName = "SaveSlots";

	private GameObject buttonContinue;

	private bool isSwitchingSave;

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
		if (!File.Exists(Path.Combine(Application.persistentDataPath, "SaveSlots.xml")))
		{
			return string.Format("Save{0}", "1");
		}
		try
		{
			SaveData saveData = ModSave.Load<SaveData>("SaveSlots", "");
			if (saveData != null && !string.IsNullOrEmpty(saveData.slotName))
			{
				return NormalizeSlotName(saveData.slotName);
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
		if (currentSlotName == sender.SlotFileName)
		{
			InitializeCurrentEmptySlot(sender);
			return;
		}
		SaveCurrentMetadata(currentSlotName);
		string currentSlotPath = Path.Combine(SaveSlotsFolder, currentSlotName);
		Directory.CreateDirectory(currentSlotPath);
		try
		{
			CopyActiveSaveToSlot(activePath, currentSlotPath);
		}
		catch (Exception ex)
		{
			FailSafe("SAVE SLOTS BACKUP CURRENT", activePath, ex);
			return;
		}
		string targetSlotPath = Path.Combine(SaveSlotsFolder, sender.SlotFileName);
		bool targetHasSave = HasPlayableSave(targetSlotPath);
		try
		{
			MoveActiveSaveToEmergencyBackup();
			Directory.CreateDirectory(activePath);
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
			ModSave.Save<SaveData>("SaveSlots", new SaveData(sender.SlotFileName, DateTime.Now), (string)null);
			sender.LoadSaveData();
			UpdateContinueButton(targetHasSave);
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
		if (File.Exists(saveSlotsData))
		{
			SetContinueVisible(HasPlayableSave(Application.persistentDataPath));
			return false;
		}
		if (HasActiveSaveData())
		{
			bool result = EnsureFirstInstallProfile();
			if (result)
			{
				sender.LoadSaveData();
				SetContinueVisible(File.Exists(defaultSaveData));
			}
			return result;
		}
		if (!Directory.Exists(Application.persistentDataPath))
		{
			Directory.CreateDirectory(Application.persistentDataPath);
		}
		sender.UpdateInfoData(isActive: true, "?", 0f, mortal: false, "UNKNOWN", "NEVER");
		ModSave.Save<SaveData>("SaveSlots", sender.GetSaveData(), (string)null);
		SetContinueVisible(false);
		return true;
	}

	private bool EnsureFirstInstallProfile()
	{
		string saveSlotsData = Path.Combine(Application.persistentDataPath, "SaveSlots.xml");
		if (File.Exists(saveSlotsData) || !HasActiveSaveData())
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
		if (!Directory.Exists(Application.persistentDataPath))
		{
			return false;
		}
		foreach (string file in Directory.GetFiles(Application.persistentDataPath))
		{
			string fileName = Path.GetFileName(file);
			if (!fileName.Equals("SaveSlots.xml", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return Directory.GetDirectories(Application.persistentDataPath).Length != 0;
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
		if (!Directory.Exists(Application.persistentDataPath))
		{
			return;
		}
		ModSave.Save<SaveData>("SaveSlots", new SaveData(slotName, DateTime.Now), (string)null);
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

	private bool HasPlayableSave(string path)
	{
		return Directory.Exists(path) && File.Exists(Path.Combine(path, MwcSaveFileName));
	}

	private void UpdateContinueButton(bool visible)
	{
		SetContinueVisible(visible);
	}

	internal void HideContinueButton()
	{
		SetContinueVisible(false);
	}

	private void SetContinueVisible(bool visible)
	{
		FindContinueButton();
		if ((UnityObject)(object)buttonContinue != (UnityObject)null)
		{
			buttonContinue.SetActive(visible);
		}
	}

	private void FindContinueButton()
	{
		if ((UnityObject)(object)buttonContinue != (UnityObject)null && (UnityObject)(object)buttonContinue.GetComponent<Button>() != (UnityObject)null)
		{
			return;
		}
		GameObject interfaceObject = GameObject.Find("Interface");
		buttonContinue = LocateContinueButton(interfaceObject);
	}

	private GameObject LocateContinueButton(GameObject interfaceObject)
	{
		if ((UnityObject)(object)interfaceObject == (UnityObject)null)
		{
			return null;
		}
		Transform continueTransform = interfaceObject.transform.Find("Buttons/ButtonContinue");
		GameObject directButton = ResolveButtonObject((UnityObject)(object)continueTransform != (UnityObject)null ? ((Component)continueTransform).gameObject : null);
		if ((UnityObject)(object)directButton != (UnityObject)null)
		{
			return directButton;
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

	private GameObject ResolveButtonObject(GameObject candidate)
	{
		if ((UnityObject)(object)candidate == (UnityObject)null)
		{
			return null;
		}
		Button button = candidate.GetComponent<Button>();
		if ((UnityObject)(object)button != (UnityObject)null)
		{
			return candidate;
		}
		button = candidate.GetComponentInParent<Button>();
		if ((UnityObject)(object)button != (UnityObject)null)
		{
			return ((Component)button).gameObject;
		}
		button = candidate.GetComponentsInChildren<Button>(true).FirstOrDefault();
		return (UnityObject)(object)button != (UnityObject)null ? ((Component)button).gameObject : null;
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
		string path = Path.Combine(SaveSlotsFolder, slotBehaviour.SlotFileName);
		if (Directory.Exists(path))
		{
			Directory.Delete(path, recursive: true);
			slotBehaviour.UpdateInfoData(isActive: false, "?", 0f, mortal: false, "UNKNOWN", "NEVER");
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

	private void OnApplicationFocus(bool hasFocus)
	{
		UpdateInfoOfAllSaves();
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
}
}



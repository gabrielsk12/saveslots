using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace MwcSaveSlots
{
[XmlRoot("SaveData")]
public sealed class LegacyProfileMarker
{
	public string slotName;
	public DateTime lastPlayed;

	public LegacyProfileMarker()
	{
		slotName = "Save1";
		lastPlayed = DateTime.Now;
	}

	public LegacyProfileMarker(string slotName, DateTime lastPlayed)
	{
		this.slotName = slotName;
		this.lastPlayed = lastPlayed;
	}
}

internal sealed class ProfileOverview
{
	internal string SlotName;
	internal bool IsSelected;
	internal bool HasSave;
	internal DateTime LastWriteLocal;
	internal int FileCount;
	internal long TotalBytes;
}

internal sealed class ProfileRepository
{
	internal const int ProfileCount = 3;
	internal const string StorageFolderName = "SaveSlotsMWC";
	private const string LegacyStorageFolderName = "SaveSlots";
	private const string ActiveMarkerName = "ActiveSlot.txt";
	private const string SafeModeName = "SafeMode.txt";
	private const string LegacyMetadataName = "SaveSlots.xml";
	private const string ManifestName = "SaveSlotsManifest.txt";
	private static readonly string[] SharedOptionNames = { "options.txt", "calibrator.cfg" };

	private readonly object operationLock = new object();
	private readonly Action<string, string> logger;
	private readonly SnapshotTransaction transaction;
	private bool copyEditorBackups;
	private bool initialized;
	private int configuredBackupRetention = -1;
	private ProfileOverview[] cachedOverviews;
	private DateTime overviewCacheExpiryUtc = DateTime.MinValue;

	internal ProfileRepository(string activeSavePath, string saveRoot, Action<string, string> logger, Action<TransactionCheckpoint> checkpoint)
	{
		ActiveSavePath = Normalize(activeSavePath);
		SaveRoot = Normalize(saveRoot);
		this.logger = logger;
		transaction = new SnapshotTransaction(StagingRoot, logger, checkpoint);
	}

	internal string ActiveSavePath { get; private set; }
	internal string SaveRoot { get; private set; }
	internal string StorageRoot { get { return Path.Combine(SaveRoot, StorageFolderName); } }
	internal string OptionsRoot { get { return Path.Combine(StorageRoot, "Options"); } }
	internal string EmergencyBackupsRoot { get { return Path.Combine(StorageRoot, "EmergencyBackups"); } }
	internal string DeletedBackupsRoot { get { return Path.Combine(EmergencyBackupsRoot, "DeletedProfiles"); } }
	internal string StagingRoot { get { return Path.Combine(StorageRoot, "Staging"); } }
	internal string ImmediateBackupRoot { get { return Path.Combine(SaveRoot, "SAVE SLOTS BACKUP"); } }
	internal string ActiveMarkerPath { get { return Path.Combine(StorageRoot, ActiveMarkerName); } }
	internal string SafeModePath { get { return Path.Combine(StorageRoot, SafeModeName); } }

	internal bool SafeModeActive { get { return File.Exists(SafeModePath); } }

	internal void Initialize(int retainedBackups, bool copyBackups)
	{
		lock (operationLock)
		{
			copyEditorBackups = copyBackups;
			if (initialized)
			{
				if (configuredBackupRetention != retainedBackups)
				{
					PruneBackups(retainedBackups);
					PruneAllDeletionBackups(retainedBackups);
					configuredBackupRetention = retainedBackups;
				}
				return;
			}
			ValidatePaths();
			transaction.RecoverMissingTarget(ActiveSavePath);
			Directory.CreateDirectory(StorageRoot);
			Directory.CreateDirectory(OptionsRoot);
			Directory.CreateDirectory(EmergencyBackupsRoot);
			Directory.CreateDirectory(DeletedBackupsRoot);
			Directory.CreateDirectory(StagingRoot);
			for (int i = 1; i <= ProfileCount; i++) transaction.RecoverMissingTarget(SlotPath(SlotName(i)));
			transaction.RecoverMissingTarget(ImmediateBackupRoot);
			transaction.CleanupOrphanStages();
			ImportLegacyStorageByCopy();
			EnsureFirstInstallProfile();
			PruneBackups(retainedBackups);
			PruneAllDeletionBackups(retainedBackups);
			configuredBackupRetention = retainedBackups;
			initialized = true;
		}
	}

	internal string SelectedSlot()
	{
		string marker = ReadActiveMarker();
		if (marker != null)
		{
			return marker;
		}
		LegacyProfileMarker legacy = ReadLegacyMarker(Path.Combine(ActiveSavePath, LegacyMetadataName));
		return legacy == null ? "Save1" : NormalizeSlot(legacy.slotName);
	}

	internal ProfileOverview[] ReadOverviews()
	{
		if (cachedOverviews != null && DateTime.UtcNow < overviewCacheExpiryUtc)
		{
			return CloneOverviews(cachedOverviews);
		}
		string selected = SelectedSlot();
		ProfileOverview[] result = new ProfileOverview[ProfileCount];
		for (int i = 1; i <= ProfileCount; i++)
		{
			string slot = SlotName(i);
			string path = SlotPath(slot);
			DirectoryTotals totals = Scan(path);
			result[i - 1] = new ProfileOverview
			{
				SlotName = slot,
				IsSelected = string.Equals(slot, selected, StringComparison.OrdinalIgnoreCase),
				HasSave = HasPlayableSave(path),
				LastWriteLocal = totals.LastWriteLocal,
				FileCount = totals.FileCount,
				TotalBytes = totals.TotalBytes
			};
		}
		cachedOverviews = result;
		overviewCacheExpiryUtc = DateTime.UtcNow.AddSeconds(3d);
		return CloneOverviews(result);
	}

	internal void SwitchTo(int profileNumber, bool synchronizeOptions, bool copyBackups, int retainedBackups)
	{
		lock (operationLock)
		{
			RequireProfile(profileNumber);
			Initialize(retainedBackups, copyBackups);
			if (SafeModeActive)
			{
				throw new InvalidOperationException("Safe mode is active. Recover the backup and remove SafeMode.txt before switching again.");
			}

			string requested = SlotName(profileNumber);
			string previous = SelectedSlot();
			string previousMarkerText = ReadTextIfExists(ActiveMarkerPath);
			try
			{
				if (HasPlayableSave(ActiveSavePath))
				{
					StoreActiveInSlot(previous, copyBackups);
				}

				if (DirectoryHasContent(ActiveSavePath))
				{
					CreateImmediateBackup(previous, copyBackups);
				}

				string requestedPath = SlotPath(requested);
				bool targetHasSave = HasPlayableSave(requestedPath);
				string prepared = targetHasSave
					? transaction.PrepareCopy(requestedPath, "activate-" + requested, IncludeCurrentEntry, true)
					: transaction.CreateEmptyStage("activate-empty-" + requested);

				if (targetHasSave && synchronizeOptions)
				{
					CopySharedOptions(prepared);
				}
				if (!targetHasSave)
				{
					CopySharedOptions(prepared);
					DeleteIfExists(Path.Combine(prepared, LegacyMetadataName));
				}
				else
				{
					WriteLegacyMarker(Path.Combine(prepared, LegacyMetadataName), requested);
				}
				DeleteIfExists(Path.Combine(prepared, ManifestName));

				transaction.CommitPreparedDirectory(prepared, ActiveSavePath, delegate
				{
					WriteActiveMarker(requested);
				}, delegate
				{
					RestoreMarker(previousMarkerText);
				});

				PruneBackups(retainedBackups);
				InvalidateCache();
				Log("Switch", previous + " -> " + requested + "; playable=" + targetHasSave);
			}
			catch (Exception ex)
			{
				WriteSafeMode("Switch to " + requested + " failed: " + ex.Message);
				throw;
			}
		}
	}

	internal bool TryPersistActive(string reason, bool copyBackups)
	{
		lock (operationLock)
		{
			if (SafeModeActive)
			{
				Log("Persist", "Skipped because safe mode is active.");
				return false;
			}
			copyEditorBackups = copyBackups;
			if (!HasPlayableSave(ActiveSavePath))
			{
				return false;
			}
			string selected = SelectedSlot();
			try
			{
				StoreActiveInSlot(selected, copyBackups);
				InvalidateCache();
				Log("Persist", selected + "; reason=" + reason);
				return true;
			}
			catch (Exception ex)
			{
				WriteSafeMode("Persist " + selected + " failed: " + ex.Message);
				throw;
			}
		}
	}

	internal string DeleteProfile(int profileNumber, bool copyBackups, int retainedBackups)
	{
		lock (operationLock)
		{
			RequireProfile(profileNumber);
			Initialize(retainedBackups, copyBackups);
			if (SafeModeActive)
			{
				throw new InvalidOperationException("Safe mode is active. Recover the backup and remove SafeMode.txt before deleting a profile.");
			}
			string slot = SlotName(profileNumber);
			string slotPath = SlotPath(slot);
			bool selected = string.Equals(slot, SelectedSlot(), StringComparison.OrdinalIgnoreCase);
			string backupSource = selected && DirectoryHasContent(ActiveSavePath) ? ActiveSavePath : slotPath;
			string deletionBackup = null;
			try
			{
				if (DirectoryHasContent(backupSource))
				{
					deletionBackup = CreateDeletionBackup(backupSource, slot, copyBackups);
				}

				string empty = null;
				string previousMarkerText = null;
				if (selected)
				{
					CopyActiveOptionsToShared();
					if (DirectoryHasContent(ActiveSavePath))
					{
						CreateImmediateBackup(slot, copyBackups);
					}
					empty = transaction.CreateEmptyStage("delete-active-" + slot);
					CopySharedOptions(empty);
					previousMarkerText = ReadTextIfExists(ActiveMarkerPath);
				}

				transaction.MoveToTrash(slotPath, "profile-" + slot);
				if (selected)
				{
					transaction.CommitPreparedDirectory(empty, ActiveSavePath, delegate { WriteActiveMarker(slot); }, delegate { RestoreMarker(previousMarkerText); });
				}
				PruneBackups(retainedBackups);
				PruneDeletionBackups(slot, retainedBackups);
				InvalidateCache();
				Log("Delete", slot + "; verifiedBackup=" + (deletionBackup ?? "<no profile data>"));
				return deletionBackup;
			}
			catch (Exception ex)
			{
				WriteSafeMode("Delete " + slot + " failed: " + ex.Message);
				throw;
			}
		}
	}

	private string CreateDeletionBackup(string source, string slotName, bool copyBackups)
	{
		copyEditorBackups = copyBackups;
		string slot = NormalizeSlot(slotName);
		string destination = UniqueDeletionBackupPath(slot);
		bool playable = HasPlayableSave(source);
		transaction.ReplaceSnapshot(source, destination, "delete-backup-" + slot, IncludeDeletionEntry, playable, delegate(string prepared)
		{
			WriteManifest(prepared, slot, "verified deletion backup");
		});
		transaction.VerifyTree(source, destination, IncludeDeletionEntry, playable);
		Log("DeleteBackup", slot + " -> " + destination + "; playable=" + playable);
		return destination;
	}

	internal string SlotPath(string slotName)
	{
		return Path.Combine(StorageRoot, NormalizeSlot(slotName));
	}

	internal static string SlotName(int number)
	{
		RequireProfile(number);
		return "Save" + number;
	}

	internal static string NormalizeSlot(string value)
	{
		if (string.IsNullOrEmpty(value) || !value.StartsWith("Save", StringComparison.OrdinalIgnoreCase))
		{
			return "Save1";
		}
		int number;
		if (!int.TryParse(value.Substring(4), out number) || number < 1 || number > ProfileCount)
		{
			return "Save1";
		}
		return "Save" + number;
	}

	private void StoreActiveInSlot(string slotName, bool copyBackups)
	{
		copyEditorBackups = copyBackups;
		string normalized = NormalizeSlot(slotName);
		CopyActiveOptionsToShared();
		WriteLegacyMarker(Path.Combine(ActiveSavePath, LegacyMetadataName), normalized);
		transaction.ReplaceSnapshot(ActiveSavePath, SlotPath(normalized), "store-" + normalized, IncludeCurrentEntry, true, delegate(string prepared)
		{
			WriteManifest(prepared, normalized, "store active profile");
		});
		WriteActiveMarker(normalized);
	}

	private void CreateImmediateBackup(string slotName, bool copyBackups)
	{
		copyEditorBackups = copyBackups;
		if (Directory.Exists(ImmediateBackupRoot))
		{
			Directory.CreateDirectory(EmergencyBackupsRoot);
			string archive = UniqueBackupPath();
			Directory.Move(ImmediateBackupRoot, archive);
		}
		transaction.ReplaceSnapshot(ActiveSavePath, ImmediateBackupRoot, "emergency-backup", IncludeCurrentEntry, HasPlayableSave(ActiveSavePath), delegate(string prepared)
		{
			WriteManifest(prepared, NormalizeSlot(slotName), "emergency backup");
		});
	}

	private void EnsureFirstInstallProfile()
	{
		if (!HasPlayableSave(ActiveSavePath))
		{
			return;
		}
		string selected = ReadActiveMarker();
		LegacyProfileMarker legacy = ReadLegacyMarker(Path.Combine(ActiveSavePath, LegacyMetadataName));
		if (selected == null)
		{
			selected = legacy == null ? "Save1" : NormalizeSlot(legacy.slotName);
			WriteActiveMarker(selected);
		}
		if (!HasPlayableSave(SlotPath(selected)))
		{
			StoreActiveInSlot(selected, copyEditorBackups);
			Log("FirstInstall", "Imported existing active save into " + selected);
		}
	}

	private void ImportLegacyStorageByCopy()
	{
		string legacyRoot = Path.Combine(SaveRoot, LegacyStorageFolderName);
		if (!Directory.Exists(legacyRoot) || string.Equals(Normalize(legacyRoot), Normalize(StorageRoot), StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		for (int i = 1; i <= ProfileCount; i++)
		{
			string slot = SlotName(i);
			string source = Path.Combine(legacyRoot, slot);
			string target = SlotPath(slot);
			if (!HasPlayableSave(source) || File.Exists(Path.Combine(source, "defaultES2File.txt")) || Directory.Exists(target))
			{
				continue;
			}
			transaction.ReplaceSnapshot(source, target, "legacy-import-" + slot, IncludeCurrentEntry, true, delegate(string prepared)
			{
				WriteManifest(prepared, slot, "legacy copy import");
			});
		}
		string legacyOptions = Path.Combine(legacyRoot, "Options");
		if (Directory.Exists(legacyOptions))
		{
			for (int i = 0; i < SharedOptionNames.Length; i++)
			{
				string source = Path.Combine(legacyOptions, SharedOptionNames[i]);
				string target = Path.Combine(OptionsRoot, SharedOptionNames[i]);
				if (File.Exists(source) && !File.Exists(target))
				{
					File.Copy(source, target, false);
				}
			}
		}
		string legacyMarker = Path.Combine(legacyRoot, ActiveMarkerName);
		if (File.Exists(legacyMarker) && !File.Exists(ActiveMarkerPath))
		{
			WriteActiveMarker(NormalizeSlot(File.ReadAllText(legacyMarker).Trim()));
		}
	}

	private void CopyActiveOptionsToShared()
	{
		Directory.CreateDirectory(OptionsRoot);
		for (int i = 0; i < SharedOptionNames.Length; i++)
		{
			string source = Path.Combine(ActiveSavePath, SharedOptionNames[i]);
			if (File.Exists(source))
			{
				File.Copy(source, Path.Combine(OptionsRoot, SharedOptionNames[i]), true);
			}
		}
	}

	private void CopySharedOptions(string target)
	{
		Directory.CreateDirectory(target);
		for (int i = 0; i < SharedOptionNames.Length; i++)
		{
			string source = Path.Combine(OptionsRoot, SharedOptionNames[i]);
			if (File.Exists(source))
			{
				File.Copy(source, Path.Combine(target, SharedOptionNames[i]), true);
			}
		}
	}

	private bool IncludeCurrentEntry(string name)
	{
		return copyEditorBackups || name.IndexOf("_backup", StringComparison.OrdinalIgnoreCase) < 0;
	}

	private bool IncludeDeletionEntry(string name)
	{
		return !string.Equals(name, ManifestName, StringComparison.OrdinalIgnoreCase) && IncludeCurrentEntry(name);
	}

	private string ReadActiveMarker()
	{
		try
		{
			if (!File.Exists(ActiveMarkerPath))
			{
				return null;
			}
			string raw = File.ReadAllText(ActiveMarkerPath).Trim();
			string normalized = NormalizeSlot(raw);
			return string.Equals(raw, normalized, StringComparison.OrdinalIgnoreCase) ? normalized : null;
		}
		catch (Exception ex)
		{
			Log("Marker", ex.Message);
			return null;
		}
	}

	private void WriteActiveMarker(string slotName)
	{
		WriteTextAtomic(ActiveMarkerPath, NormalizeSlot(slotName));
	}

	private void RestoreMarker(string previousText)
	{
		if (previousText == null)
		{
			DeleteIfExists(ActiveMarkerPath);
		}
		else
		{
			WriteTextAtomic(ActiveMarkerPath, previousText);
		}
	}

	private static LegacyProfileMarker ReadLegacyMarker(string path)
	{
		try
		{
			if (!File.Exists(path))
			{
				return null;
			}
			XmlSerializer serializer = new XmlSerializer(typeof(LegacyProfileMarker));
			using (FileStream stream = File.OpenRead(path))
			{
				return serializer.Deserialize(stream) as LegacyProfileMarker;
			}
		}
		catch
		{
			return null;
		}
	}

	private static void WriteLegacyMarker(string path, string slotName)
	{
		string temporary = path + ".new";
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		XmlSerializer serializer = new XmlSerializer(typeof(LegacyProfileMarker));
		using (FileStream stream = File.Create(temporary))
		{
			serializer.Serialize(stream, new LegacyProfileMarker(NormalizeSlot(slotName), DateTime.Now));
		}
		ReplaceFile(temporary, path);
	}

	private static void WriteTextAtomic(string path, string value)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		string temporary = path + ".new";
		File.WriteAllText(temporary, value);
		ReplaceFile(temporary, path);
	}

	private static void ReplaceFile(string temporary, string target)
	{
		string backup = target + ".old";
		DeleteIfExists(backup);
		if (File.Exists(target))
		{
			try
			{
				File.Replace(temporary, target, backup);
				DeleteIfExists(backup);
				return;
			}
			catch (NotSupportedException) { }
			catch (NotImplementedException) { }
			File.Move(target, backup);
			try
			{
				File.Move(temporary, target);
				DeleteIfExists(backup);
				return;
			}
			catch
			{
				if (!File.Exists(target) && File.Exists(backup))
				{
					File.Move(backup, target);
				}
				throw;
			}
		}
		File.Move(temporary, target);
	}

	private void WriteManifest(string folder, string slotName, string operation)
	{
		DirectoryTotals totals = Scan(folder);
		string save = Path.Combine(folder, "savefile.txt");
		string body = "slot=" + NormalizeSlot(slotName) + Environment.NewLine
			+ "operation=" + operation + Environment.NewLine
			+ "writtenUtc=" + DateTime.UtcNow.ToString("O") + Environment.NewLine
			+ "version=4.0" + Environment.NewLine
			+ "files=" + totals.FileCount + Environment.NewLine
			+ "bytes=" + totals.TotalBytes + Environment.NewLine
			+ "hasSavefile=" + File.Exists(save) + Environment.NewLine
			+ "savefileSha256=" + (File.Exists(save) ? SnapshotTransaction.Sha256(save) : "") + Environment.NewLine;
		File.WriteAllText(Path.Combine(folder, ManifestName), body);
	}

	private void WriteSafeMode(string reason)
	{
		try
		{
			Directory.CreateDirectory(StorageRoot);
			File.WriteAllText(SafeModePath, DateTime.UtcNow.ToString("O") + Environment.NewLine + reason + Environment.NewLine);
		}
		catch (Exception ex)
		{
			Log("SafeMode", ex.Message);
		}
	}

	private void PruneBackups(int retainedBackups)
	{
		if (!Directory.Exists(EmergencyBackupsRoot))
		{
			return;
		}
		int keep = Math.Max(1, retainedBackups);
		DirectoryInfo[] candidates = new DirectoryInfo(EmergencyBackupsRoot).GetDirectories();
		List<DirectoryInfo> regular = new List<DirectoryInfo>();
		for (int i = 0; i < candidates.Length; i++)
		{
			if (!string.Equals(candidates[i].Name, "DeletedProfiles", StringComparison.OrdinalIgnoreCase)) regular.Add(candidates[i]);
		}
		DirectoryInfo[] backups = regular.ToArray();
		Array.Sort(backups, delegate(DirectoryInfo left, DirectoryInfo right)
		{
			return right.CreationTimeUtc.CompareTo(left.CreationTimeUtc);
		});
		for (int i = keep; i < backups.Length; i++)
		{
			try { transaction.DeleteTree(backups[i].FullName); }
			catch (Exception ex) { Log("Prune", backups[i].FullName + ": " + ex.Message); }
		}
	}

	private void PruneAllDeletionBackups(int retainedBackups)
	{
		for (int i = 1; i <= ProfileCount; i++) PruneDeletionBackups(SlotName(i), retainedBackups);
	}

	private void PruneDeletionBackups(string slotName, int retainedBackups)
	{
		string root = Path.Combine(DeletedBackupsRoot, NormalizeSlot(slotName));
		if (!Directory.Exists(root)) return;
		int keep = Math.Max(1, retainedBackups);
		DirectoryInfo[] backups = new DirectoryInfo(root).GetDirectories();
		Array.Sort(backups, delegate(DirectoryInfo left, DirectoryInfo right)
		{
			return right.CreationTimeUtc.CompareTo(left.CreationTimeUtc);
		});
		for (int i = keep; i < backups.Length; i++)
		{
			try { transaction.DeleteTree(backups[i].FullName); }
			catch (Exception ex) { Log("PruneDelete", backups[i].FullName + ": " + ex.Message); }
		}
	}

	private string UniqueBackupPath()
	{
		string stem = Path.Combine(EmergencyBackupsRoot, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
		string candidate = stem;
		int suffix = 2;
		while (Directory.Exists(candidate))
		{
			candidate = stem + "_" + suffix;
			suffix++;
		}
		return candidate;
	}

	private string UniqueDeletionBackupPath(string slotName)
	{
		string root = Path.Combine(DeletedBackupsRoot, NormalizeSlot(slotName));
		Directory.CreateDirectory(root);
		string stem = Path.Combine(root, DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
		string candidate = stem;
		int suffix = 2;
		while (Directory.Exists(candidate))
		{
			candidate = stem + "_" + suffix;
			suffix++;
		}
		return candidate;
	}

	private void ValidatePaths()
	{
		string active = Normalize(ActiveSavePath);
		string root = Normalize(SaveRoot);
		string storage = Normalize(StorageRoot);
		if (active.Length < 10 || root.Length < 4 || string.Equals(active, root, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Save paths failed the safety check.");
		}
		if (!active.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
			|| !string.Equals(Normalize(Path.GetDirectoryName(active)), root, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Active save must be a direct child of the save root.");
		}
		if (!storage.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(storage, active, StringComparison.OrdinalIgnoreCase)
			|| storage.StartsWith(active + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Save slot storage is not safely separated from the active save.");
		}
	}

	private static bool HasPlayableSave(string path)
	{
		return Directory.Exists(path) && File.Exists(Path.Combine(path, "savefile.txt"));
	}

	private static bool DirectoryHasContent(string path)
	{
		return Directory.Exists(path) && (Directory.GetFiles(path).Length > 0 || Directory.GetDirectories(path).Length > 0);
	}

	private static DirectoryTotals Scan(string path)
	{
		DirectoryTotals totals = new DirectoryTotals();
		if (!Directory.Exists(path))
		{
			totals.LastWriteLocal = new DateTime(1970, 1, 1);
			return totals;
		}
		foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
		{
			FileInfo info = new FileInfo(file);
			totals.FileCount++;
			totals.TotalBytes += info.Length;
			if (info.LastWriteTime > totals.LastWriteLocal)
			{
				totals.LastWriteLocal = info.LastWriteTime;
			}
		}
		if (totals.LastWriteLocal == DateTime.MinValue)
		{
			totals.LastWriteLocal = new DateTime(1970, 1, 1);
		}
		return totals;
	}

	private static ProfileOverview[] CloneOverviews(ProfileOverview[] source)
	{
		ProfileOverview[] result = new ProfileOverview[source.Length];
		for (int i = 0; i < source.Length; i++)
		{
			ProfileOverview item = source[i];
			result[i] = new ProfileOverview
			{
				SlotName = item.SlotName,
				IsSelected = item.IsSelected,
				HasSave = item.HasSave,
				LastWriteLocal = item.LastWriteLocal,
				FileCount = item.FileCount,
				TotalBytes = item.TotalBytes
			};
		}
		return result;
	}

	private void InvalidateCache()
	{
		cachedOverviews = null;
		overviewCacheExpiryUtc = DateTime.MinValue;
	}

	private static string ReadTextIfExists(string path)
	{
		return File.Exists(path) ? File.ReadAllText(path) : null;
	}

	private static void DeleteIfExists(string path)
	{
		if (File.Exists(path))
		{
			File.SetAttributes(path, FileAttributes.Normal);
			File.Delete(path);
		}
	}

	private void Log(string area, string message)
	{
		if (logger != null)
		{
			try { logger(area, message); } catch { }
		}
	}

	private static string Normalize(string path)
	{
		return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
	}

	private static void RequireProfile(int profileNumber)
	{
		if (profileNumber < 1 || profileNumber > ProfileCount)
		{
			throw new ArgumentOutOfRangeException("profileNumber");
		}
	}

	private struct DirectoryTotals
	{
		internal int FileCount;
		internal long TotalBytes;
		internal DateTime LastWriteLocal;
	}
}
}

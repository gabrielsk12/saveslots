using System;
using System.Collections.Generic;
using System.IO;
using MwcSaveSlots;

internal static class Program
{
	private static int passed;

	private static int Main()
	{
		Run("first install and all three profiles", FirstInstallAndProfiles);
		Run("MWC menu keeps three distinct card positions", MenuLayoutHasDistinctCards);
		Run("MWC first and last names are combined safely", PlayerNamesAreReadable);
		Run("native SAVES entry follows MWC menu spacing", NativeMenuEntryUsesGameSpacing);
		Run("nested data and editor backup filtering", NestedDataAndFiltering);
		Run("synchronized and per-profile options", OptionModes);
		Run("empty slot and delete flows", EmptyAndDelete);
		Run("deletion backup failure preserves profile", DeletionBackupFailurePreservesProfile);
		Run("deletion move failure restores profile", DeletionMoveFailureRestoresProfile);
		Run("active deletion commit failure preserves playable save", ActiveDeletionFailurePreservesPlayableSave);
		Run("deletion backups retain recent copies per slot", DeletionBackupRetention);
		Run("copy-only legacy migration", LegacyMigration);
		Run("emergency backup pruning", BackupPruning);
		Run("interrupted swap recovery", InterruptedRecovery);
		Run("interrupted stored-profile recovery", InterruptedProfileRecovery);
		Run("delayed first-save capture gate", DelayedFirstSave);
		Run("pending save receipt survives menu recreation", PendingSaveReceiptSurvivesReload);
		Run("stage failure preserves active save", delegate { FailurePreservesPrevious(TransactionCheckpoint.StageCreated, 3); });
		Run("stage copy failure preserves active save", delegate { FailurePreservesPrevious(TransactionCheckpoint.StageCopied, 3); });
		Run("verification failure preserves active save", delegate { FailurePreservesPrevious(TransactionCheckpoint.StageVerified, 3); });
		Run("backup failure preserves active save", delegate { FailurePreservesPrevious(TransactionCheckpoint.StageVerified, 2); });
		Run("active move failure restores active save", delegate { FailurePreservesPrevious(TransactionCheckpoint.PreparedTargetMoved, 3); });
		Run("marker commit failure restores active save", delegate { FailurePreservesPrevious(TransactionCheckpoint.MetadataCommitted, 1); });
		Run("cleanup failure is nonfatal after commit", CleanupFailureIsNonfatal);
		Console.WriteLine("PASS: " + passed + " backend scenarios");
		return 0;
	}

	private static void FirstInstallAndProfiles()
	{
		using (Sandbox box = Sandbox.Create())
		{
			box.WriteActiveSave("FIRST");
			ProfileRepository repository = box.Repository(null);
			repository.Initialize(5, false);
			Equal("FIRST", box.Read(Path.Combine(repository.SlotPath("Save1"), "savefile.txt")), "existing game was imported to Save1");
			Equal("Save1", File.ReadAllText(repository.ActiveMarkerPath), "first marker");
			ProfileOverview[] firstOverview = repository.ReadOverviews();
			True(firstOverview.Length == 3, "all three save cards have backend state");
			True(firstOverview[0].IsSelected && firstOverview[0].HasSave, "existing save is visible as selected Save1");
			False(firstOverview[1].HasSave || firstOverview[2].HasSave, "unused Save2 and Save3 begin empty");

			repository.SwitchTo(2, true, false, 5);
			False(File.Exists(Path.Combine(box.Active, "savefile.txt")), "Save2 starts empty");
			box.WriteActiveSave("SECOND");
			repository.TryPersistActive("test", false);
			repository.SwitchTo(3, true, false, 5);
			box.WriteActiveSave("THIRD");
			repository.TryPersistActive("test", false);
			repository.SwitchTo(1, true, false, 5);
			Equal("FIRST", box.Read(Path.Combine(box.Active, "savefile.txt")), "Save1 restored");
			True(File.Exists(Path.Combine(repository.SlotPath("Save2"), "savefile.txt")), "Save2 stored");
			True(File.Exists(Path.Combine(repository.SlotPath("Save3"), "savefile.txt")), "Save3 stored");
		}
	}

	private static void MenuLayoutHasDistinctCards()
	{
		Equal("-416", MwcMenuLayout.CardX(0).ToString(System.Globalization.CultureInfo.InvariantCulture), "Save1 is on the left");
		Equal("0", MwcMenuLayout.CardX(1).ToString(System.Globalization.CultureInfo.InvariantCulture), "Save2 is centered");
		Equal("416", MwcMenuLayout.CardX(2).ToString(System.Globalization.CultureInfo.InvariantCulture), "Save3 is on the right");
		True(MwcMenuLayout.CardWidth < MwcMenuLayout.CardSpacing, "card spacing prevents overlap");
		True((MwcMenuLayout.CardSpacing * 2f) + MwcMenuLayout.CardWidth < MwcMenuLayout.FrameWidth, "all three cards fit inside the smaller frame");
		True(MwcMenuLayout.MenuButtonOffsetX < 0f && MwcMenuLayout.MenuButtonOffsetY < 0f, "SAVES button stays inside the MWC menu bounds");
	}

	private static void PlayerNamesAreReadable()
	{
		Equal("Gabriel Dodu", PlayerNameFormatter.Format(" Gabriel ", "Dodu", "OLD"), "first and last name are combined");
		Equal("Marty McFly", PlayerNameFormatter.Format("Marty", "  McFly  ", ""), "outer whitespace is removed");
		Equal("Biff Tannen", PlayerNameFormatter.Format("  Biff\t", "\r\nTannen ", ""), "whitespace is normalized");
		Equal("Gabriel", PlayerNameFormatter.Format("Gabriel", "", ""), "a first name works alone");
		Equal("Dodu", PlayerNameFormatter.Format("", "Dodu", ""), "a last name works alone");
		Equal("Legacy Player", PlayerNameFormatter.Format("", "", " Legacy   Player "), "legacy name remains supported");
		Equal("PLAYER", PlayerNameFormatter.Format("", "", ""), "empty metadata has a clear fallback");
	}

	private static void NativeMenuEntryUsesGameSpacing()
	{
		Equal("120", MwcMenuLayout.PreviousMenuCoordinate(100f, 80f).ToString(System.Globalization.CultureInfo.InvariantCulture), "entry continues the same vertical step before Continue");
		Equal("42", MwcMenuLayout.PreviousMenuCoordinate(40f, 38f).ToString(System.Globalization.CultureInfo.InvariantCulture), "entry continues the game's horizontal menu alignment");
	}

	private static void NestedDataAndFiltering()
	{
		using (Sandbox box = Sandbox.Create())
		{
			box.WriteActiveSave("NESTED");
			box.Write(Path.Combine(box.Active, "world", "vehicles", "state.bin"), "nested");
			box.Write(Path.Combine(box.Active, "editor_backup", "ignored.bin"), "backup");
			ProfileRepository repository = box.Repository(null);
			repository.Initialize(5, false);
			True(File.Exists(Path.Combine(repository.SlotPath("Save1"), "world", "vehicles", "state.bin")), "nested file copied");
			False(Directory.Exists(Path.Combine(repository.SlotPath("Save1"), "editor_backup")), "_backup directory excluded");
			repository.TryPersistActive("include backups", true);
			True(File.Exists(Path.Combine(repository.SlotPath("Save1"), "editor_backup", "ignored.bin")), "_backup directory included when enabled");
		}
	}

	private static void OptionModes()
	{
		using (Sandbox box = Sandbox.Create())
		{
			box.WriteActiveSave("ONE");
			box.Write(Path.Combine(box.Active, "options.txt"), "OPTIONS-ONE");
			ProfileRepository repository = box.Repository(null);
			repository.Initialize(5, false);
			box.Write(Path.Combine(repository.SlotPath("Save2"), "savefile.txt"), "TWO");
			box.Write(Path.Combine(repository.SlotPath("Save2"), "options.txt"), "OPTIONS-TWO");

			repository.SwitchTo(2, false, false, 5);
			Equal("OPTIONS-TWO", box.Read(Path.Combine(box.Active, "options.txt")), "per-profile option retained");
			repository.SwitchTo(1, true, false, 5);
			Equal("OPTIONS-TWO", box.Read(Path.Combine(box.Active, "options.txt")), "shared options overlaid when synchronized");

			repository.SwitchTo(3, false, false, 5);
			Equal("OPTIONS-TWO", box.Read(Path.Combine(box.Active, "options.txt")), "empty profile always receives shared options");
		}
	}

	private static void EmptyAndDelete()
	{
		using (Sandbox box = Sandbox.Create())
		{
			box.WriteActiveSave("ONE");
			ProfileRepository repository = box.Repository(null);
			repository.Initialize(5, false);
			box.Write(Path.Combine(repository.SlotPath("Save2"), "savefile.txt"), "TWO");
			string inactiveBackup = repository.DeleteProfile(2, false, 5);
			False(Directory.Exists(repository.SlotPath("Save2")), "inactive profile deleted");
			True(Directory.Exists(inactiveBackup), "inactive deletion backup retained");
			Equal("TWO", box.Read(Path.Combine(inactiveBackup, "savefile.txt")), "inactive deletion backup verified");

			string activeBackup = repository.DeleteProfile(1, false, 5);
			False(File.Exists(Path.Combine(box.Active, "savefile.txt")), "active profile becomes empty");
			False(Directory.Exists(repository.SlotPath("Save1")), "active stored profile deleted");
			True(Directory.Exists(repository.ImmediateBackupRoot), "playable backup retained");
			Equal("ONE", box.Read(Path.Combine(activeBackup, "savefile.txt")), "active deletion backup contains current playable data");
		}
	}

	private static void DeletionBackupFailurePreservesProfile()
	{
		using (Sandbox box = Sandbox.Create())
		{
			FailureGate gate = new FailureGate();
			ProfileRepository repository = box.Repository(gate.Visit);
			box.WriteActiveSave("ONE");
			repository.Initialize(5, false);
			box.Write(Path.Combine(repository.SlotPath("Save2"), "savefile.txt"), "TWO");
			gate.Arm(TransactionCheckpoint.StageVerified, 1);
			Throws(delegate { repository.DeleteProfile(2, false, 5); }, "injected deletion backup failure");
			Equal("TWO", box.Read(Path.Combine(repository.SlotPath("Save2"), "savefile.txt")), "profile remains after backup failure");
			True(repository.SafeModeActive, "backup failure activates safe mode");
		}
	}

	private static void DeletionMoveFailureRestoresProfile()
	{
		using (Sandbox box = Sandbox.Create())
		{
			FailureGate gate = new FailureGate();
			ProfileRepository repository = box.Repository(gate.Visit);
			box.WriteActiveSave("ONE");
			repository.Initialize(5, false);
			box.Write(Path.Combine(repository.SlotPath("Save2"), "savefile.txt"), "TWO");
			gate.Arm(TransactionCheckpoint.SourceMovedForDelete, 1);
			Throws(delegate { repository.DeleteProfile(2, false, 5); }, "injected deletion move failure");
			Equal("TWO", box.Read(Path.Combine(repository.SlotPath("Save2"), "savefile.txt")), "moved profile restored automatically");
			True(Directory.GetDirectories(Path.Combine(repository.DeletedBackupsRoot, "Save2")).Length == 1, "verified deletion backup also remains");
			True(repository.SafeModeActive, "move failure activates safe mode");
		}
	}

	private static void ActiveDeletionFailurePreservesPlayableSave()
	{
		using (Sandbox box = Sandbox.Create())
		{
			FailureGate gate = new FailureGate();
			ProfileRepository repository = box.Repository(gate.Visit);
			box.WriteActiveSave("ACTIVE-ONE");
			repository.Initialize(5, false);
			gate.Arm(TransactionCheckpoint.MetadataCommitted, 1);
			Throws(delegate { repository.DeleteProfile(1, false, 5); }, "injected active deletion commit failure");
			Equal("ACTIVE-ONE", box.Read(Path.Combine(box.Active, "savefile.txt")), "active playable save restored");
			Equal("Save1", repository.SelectedSlot(), "active marker restored");
			True(Directory.GetDirectories(Path.Combine(repository.DeletedBackupsRoot, "Save1")).Length == 1, "active deletion backup remains recoverable");
			True(repository.SafeModeActive, "active deletion failure activates safe mode");
		}
	}

	private static void DeletionBackupRetention()
	{
		using (Sandbox box = Sandbox.Create())
		{
			box.WriteActiveSave("ONE");
			ProfileRepository repository = box.Repository(null);
			repository.Initialize(2, false);
			for (int i = 0; i < 4; i++)
			{
				box.Write(Path.Combine(repository.SlotPath("Save2"), "savefile.txt"), "DELETE-" + i);
				repository.DeleteProfile(2, false, 2);
			}
			string[] backups = Directory.GetDirectories(Path.Combine(repository.DeletedBackupsRoot, "Save2"));
			True(backups.Length == 2, "retention keeps two deletion backups for Save2");
			bool latestFound = false;
			for (int i = 0; i < backups.Length; i++)
			{
				if (box.Read(Path.Combine(backups[i], "savefile.txt")) == "DELETE-3") latestFound = true;
			}
			True(latestFound, "newest deletion backup retained");
		}
	}

	private static void LegacyMigration()
	{
		using (Sandbox box = Sandbox.Create())
		{
			string legacy = Path.Combine(box.Root, "SaveSlots", "Save2");
			box.Write(Path.Combine(legacy, "savefile.txt"), "LEGACY");
			box.Write(Path.Combine(box.Root, "SaveSlots", "Options", "options.txt"), "LEGACY-OPTIONS");
			ProfileRepository repository = box.Repository(null);
			repository.Initialize(5, false);
			Equal("LEGACY", box.Read(Path.Combine(repository.SlotPath("Save2"), "savefile.txt")), "legacy profile copied");
			Equal("LEGACY", box.Read(Path.Combine(legacy, "savefile.txt")), "legacy source retained");
			Equal("LEGACY-OPTIONS", box.Read(Path.Combine(repository.OptionsRoot, "options.txt")), "legacy options copied");
		}
	}

	private static void BackupPruning()
	{
		using (Sandbox box = Sandbox.Create())
		{
			box.WriteActiveSave("ONE");
			ProfileRepository repository = box.Repository(null);
			repository.Initialize(2, false);
			for (int i = 0; i < 5; i++)
			{
				int target = (i % 2) + 2;
				if (!File.Exists(Path.Combine(repository.SlotPath(ProfileRepository.SlotName(target)), "savefile.txt")))
					box.Write(Path.Combine(repository.SlotPath(ProfileRepository.SlotName(target)), "savefile.txt"), "SLOT-" + target);
				repository.SwitchTo(target, true, false, 2);
			}
			string[] emergencyFolders = Directory.GetDirectories(repository.EmergencyBackupsRoot);
			int regularBackups = 0;
			for (int i = 0; i < emergencyFolders.Length; i++)
			{
				if (!string.Equals(Path.GetFileName(emergencyFolders[i]), "DeletedProfiles", StringComparison.OrdinalIgnoreCase)) regularBackups++;
			}
			True(regularBackups <= 2, "rotating backups pruned");
			True(File.Exists(Path.Combine(repository.ImmediateBackupRoot, "savefile.txt")), "immediate rollback retained");
		}
	}

	private static void InterruptedRecovery()
	{
		using (Sandbox box = Sandbox.Create())
		{
			string rollback = box.Active + ".mwcslots-rollback-interrupted";
			box.Write(Path.Combine(rollback, "savefile.txt"), "RECOVERED");
			ProfileRepository repository = box.Repository(null);
			repository.Initialize(5, false);
			Equal("RECOVERED", box.Read(Path.Combine(box.Active, "savefile.txt")), "rollback recovered");
		}
	}

	private static void DelayedFirstSave()
	{
		DelayedSaveGate gate = new DelayedSaveGate();
		DateTime start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		gate.Schedule(start);
		True(gate.Poll(start.AddMilliseconds(900), false) == DelayedSaveState.Waiting, "waits for file flush delay");
		True(gate.Poll(start.AddSeconds(1.1), false) == DelayedSaveState.Waiting, "retries missing first save");
		True(gate.Poll(start.AddSeconds(2), true) == DelayedSaveState.Ready, "captures once savefile appears");
		True(gate.Poll(start.AddSeconds(3), true) == DelayedSaveState.Idle, "fires once");
		gate.Schedule(start);
		True(gate.Poll(start.AddSeconds(16), false) == DelayedSaveState.Expired, "bounded retry expires");
	}

	private static void PendingSaveReceiptSurvivesReload()
	{
		using (Sandbox box = Sandbox.Create())
		{
			string storage = Path.Combine(box.Root, "SaveSlotsMWC");
			PendingSaveReceipt firstInstance = new PendingSaveReceipt(storage);
			firstInstance.Mark("Save2");
			True(firstInstance.Exists, "save callback leaves a durable receipt");

			PendingSaveReceipt menuInstance = new PendingSaveReceipt(storage);
			Equal("Save2", menuInstance.ReadSlot(), "new menu instance reads the saved slot");
			menuInstance.Mark("Save3");
			Equal("Save3", firstInstance.ReadSlot(), "later save atomically replaces the receipt");
			menuInstance.Clear();
			False(firstInstance.Exists, "receipt clears only after persistence succeeds");
		}
	}

	private static void InterruptedProfileRecovery()
	{
		using (Sandbox box = Sandbox.Create())
		{
			string storage = Path.Combine(box.Root, "SaveSlotsMWC");
			string rollback = Path.Combine(storage, "Save2.rollback-interrupted");
			box.Write(Path.Combine(rollback, "savefile.txt"), "PROFILE-RECOVERED");
			box.Write(Path.Combine(storage, "Staging", "orphan", "partial.tmp"), "partial");
			ProfileRepository repository = box.Repository(null);
			repository.Initialize(5, false);
			Equal("PROFILE-RECOVERED", box.Read(Path.Combine(repository.SlotPath("Save2"), "savefile.txt")), "stored slot rollback recovered");
			False(Directory.Exists(Path.Combine(repository.StagingRoot, "orphan")), "orphan stage cleaned");
		}
	}

	private static void FailurePreservesPrevious(TransactionCheckpoint target, int occurrence)
	{
		using (Sandbox box = Sandbox.Create())
		{
			FailureGate gate = new FailureGate();
			ProfileRepository repository = box.Repository(gate.Visit);
			box.WriteActiveSave("ACTIVE-PLAYABLE");
			repository.Initialize(5, false);
			box.Write(Path.Combine(repository.SlotPath("Save2"), "savefile.txt"), "REQUESTED");
			gate.Arm(target, occurrence);
			Throws(delegate { repository.SwitchTo(2, true, false, 5); }, "injected switch failure");
			Equal("ACTIVE-PLAYABLE", box.Read(Path.Combine(box.Active, "savefile.txt")), "previous active content recovered");
			Equal("Save1", repository.SelectedSlot(), "marker recovered");
			True(repository.SafeModeActive, "safe mode enabled");
			True(File.Exists(Path.Combine(repository.SlotPath("Save1"), "savefile.txt")), "stored previous profile remains playable");
		}
	}

	private static void CleanupFailureIsNonfatal()
	{
		using (Sandbox box = Sandbox.Create())
		{
			FailureGate gate = new FailureGate();
			ProfileRepository repository = box.Repository(gate.Visit);
			box.WriteActiveSave("ONE");
			repository.Initialize(5, false);
			box.Write(Path.Combine(repository.SlotPath("Save2"), "savefile.txt"), "TWO");
			gate.Arm(TransactionCheckpoint.CleanupStarted, 1);
			repository.SwitchTo(2, true, false, 5);
			Equal("TWO", box.Read(Path.Combine(box.Active, "savefile.txt")), "commit survives cleanup callback");
			False(repository.SafeModeActive, "cleanup issue does not activate safe mode");
			True(File.Exists(Path.Combine(repository.ImmediateBackupRoot, "savefile.txt")), "previous playable save remains in the immediate backup");
		}
	}

	private static void Run(string name, Action test)
	{
		try
		{
			test();
			passed++;
			Console.WriteLine("ok  " + name);
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine("FAIL " + name + Environment.NewLine + ex);
			Environment.Exit(1);
		}
	}

	private static void True(bool value, string message) { if (!value) throw new Exception("Expected true: " + message); }
	private static void False(bool value, string message) { if (value) throw new Exception("Expected false: " + message); }
	private static void Equal(string expected, string actual, string message) { if (!string.Equals(expected, actual, StringComparison.Ordinal)) throw new Exception(message + ": expected [" + expected + "] actual [" + actual + "]"); }
	private static void Throws(Action action, string message)
	{
		try { action(); }
		catch (IOException) { return; }
		throw new Exception("Expected IOException: " + message);
	}

	private sealed class FailureGate
	{
		private TransactionCheckpoint target;
		private int occurrence;
		private int seen;
		private bool armed;

		internal void Arm(TransactionCheckpoint value, int hit)
		{
			target = value;
			occurrence = hit;
			seen = 0;
			armed = true;
		}

		internal void Visit(TransactionCheckpoint value)
		{
			if (!armed || value != target) return;
			seen++;
			if (seen == occurrence) throw new IOException("Injected failure at " + value + " occurrence " + occurrence);
		}
	}

	private sealed class Sandbox : IDisposable
	{
		internal string Root { get; private set; }
		internal string Active { get; private set; }

		internal static Sandbox Create()
		{
			string root = Path.Combine(Path.GetTempPath(), "MwcSaveSlotsTests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(root);
			return new Sandbox { Root = root, Active = Path.Combine(root, "Default") };
		}

		internal ProfileRepository Repository(Action<TransactionCheckpoint> checkpoint)
		{
			return new ProfileRepository(Active, Root, delegate { }, checkpoint);
		}

		internal void WriteActiveSave(string value)
		{
			Write(Path.Combine(Active, "savefile.txt"), value);
		}

		internal void Write(string path, string value)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllText(path, value);
		}

		internal string Read(string path) { return File.ReadAllText(path); }

		public void Dispose()
		{
			if (!Directory.Exists(Root)) return;
			foreach (string file in Directory.GetFiles(Root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
			Directory.Delete(Root, true);
		}
	}
}

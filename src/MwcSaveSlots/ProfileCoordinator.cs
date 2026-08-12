using System;
using System.IO;
using MSCLoader;
using UnityEngine;

namespace MwcSaveSlots
{
internal sealed class ProfileCoordinator : IDisposable
{
	private readonly MwcSaveSlotsMod mod;
	private readonly ProfileRepository repository;
	private readonly SaveMetadataReader metadata;
	private readonly ThumbnailService thumbnails;
	private readonly GameMenuBridge menuBridge;
	private readonly GameObject runtimeHost;
	private readonly ScreenshotService screenshots;
	private readonly PendingSaveReceipt pendingSave;
	private static readonly object PendingSaveSync = new object();
	private SaveSlotsMenuView view;
	private bool menuScene;
	private bool operationRunning;
	private readonly DelayedSaveGate delayedSave = new DelayedSaveGate();
	private DateTime nextPanelRefreshUtc = DateTime.MinValue;
	private bool safeModeReported;
	private string lastProfileSummary;

	internal ProfileCoordinator(MwcSaveSlotsMod mod)
	{
		this.mod = mod;
		repository = new ProfileRepository(RuntimePaths.ActiveSavePath, RuntimePaths.SaveRoot, DiagnosticWriter.Write, null);
		metadata = new SaveMetadataReader(DiagnosticWriter.Write);
		thumbnails = new ThumbnailService();
		menuBridge = new GameMenuBridge(RuntimePaths.ActiveSavePath, DiagnosticWriter.Write);
		runtimeHost = new GameObject("MwcSaveSlotsRuntimeHost");
		UnityEngine.Object.DontDestroyOnLoad(runtimeHost);
		screenshots = runtimeHost.AddComponent<ScreenshotService>();
		pendingSave = new PendingSaveReceipt(repository.StorageRoot);
	}

	internal string StorageRoot { get { return repository.StorageRoot; } }
	internal string DeletedBackupsRoot { get { return repository.DeletedBackupsRoot; } }

	internal void EnterMenu()
	{
		menuScene = true;
		EnsureReady();
		menuBridge.BeginMenuSession();
		CompletePendingSave("save completed before menu reload");
		bool blocked = menuBridge.InteractionSuppressed();
		view.SetMenuVisible(!blocked);
		view.SetBlocked(blocked);
		RefreshCards();
		menuBridge.ForceContinueRefresh();
		DiagnosticWriter.Write("Coordinator", "Entered main menu. " + DescribeState());
	}

	internal void EnterGame()
	{
		menuScene = false;
		menuBridge.EndMenuSession();
		if (view != null) view.SetMenuVisible(false);
	}

	internal void ScheduleGameSave()
	{
		EnsureReady();
		lock (PendingSaveSync) pendingSave.Mark(repository.SelectedSlot());
		bool captured = false;
		if (mod.AutomaticThumbnailValue)
		{
			string target = Path.Combine(RuntimePaths.ActiveSavePath, "screenshot.png");
			captured = screenshots.CaptureImmediate(target, mod.HighResolutionValue);
		}
		delayedSave.Schedule(DateTime.UtcNow);
		DiagnosticWriter.Write("SaveCapture", "Save receipt recorded; immediate thumbnail=" + captured + ". Waiting for the game's save files to finish writing.");
	}

	internal void Tick()
	{
		if (menuScene && view != null)
		{
			bool blocked = menuBridge.InteractionSuppressed();
			view.SetMenuVisible(!blocked);
			view.SetBlocked(blocked || operationRunning);
			if (!blocked)
			{
				menuBridge.SynchronizeContinueButton();
				if (view.PanelVisible && DateTime.UtcNow >= nextPanelRefreshUtc) RefreshCards();
			}
		}
		ProcessPendingSave();
	}

	internal void TogglePanel()
	{
		EnsureReady();
		bool show = !view.PanelVisible;
		view.SetPanelVisible(show);
		if (show) RefreshCards();
	}

	internal void ClosePanel()
	{
		if (view != null) view.SetPanelVisible(false);
	}

	internal void ForceUi(bool openPanel)
	{
		menuScene = true;
		EnsureReady();
		view.ForceVisible(openPanel);
		view.SetBlocked(false);
		if (openPanel) RefreshCards();
		DiagnosticWriter.Write("Console", "UI force request completed. " + DescribeState());
	}

	internal void RefreshFromConsole()
	{
		EnsureReady();
		RefreshCards();
		menuBridge.ForceContinueRefresh();
		DiagnosticWriter.Write("Console", "Manual refresh completed. " + DescribeState());
	}

	internal string DescribeState()
	{
		string selected;
		string safeMode;
		string activeSave;
		try
		{
			selected = repository.SelectedSlot();
			safeMode = repository.SafeModeActive.ToString();
			activeSave = File.Exists(Path.Combine(RuntimePaths.ActiveSavePath, "savefile.txt")).ToString();
		}
		catch (Exception ex)
		{
			selected = "<error:" + ex.GetType().Name + ">";
			safeMode = "<unknown>";
			activeSave = "<unknown>";
		}
		return "menuScene=" + menuScene
			+ " operationRunning=" + operationRunning
			+ " selected=" + selected
			+ " activeSave=" + activeSave
			+ " safeMode=" + safeMode
			+ " bridge{" + menuBridge.DescribeState() + "}"
			+ " view{" + (view == null ? "<not created>" : view.DescribeState()) + "}";
	}

	internal void SelectProfile(int number)
	{
		if (operationRunning) return;
		string requested = ProfileRepository.SlotName(number);
		if (string.Equals(requested, repository.SelectedSlot(), StringComparison.OrdinalIgnoreCase))
		{
			view.SetPanelVisible(false);
			return;
		}
		operationRunning = true;
		view.SetStatus("SWITCHING TO SAVE " + number + "...");
		view.PlayShutter(delegate
		{
			try
			{
				repository.SwitchTo(number, mod.SynchronizeOptionsValue, mod.CopyEditorBackupsValue, mod.MaximumBackupsValue);
				metadata.Invalidate();
				thumbnails.InvalidateFolder(repository.SlotPath(requested));
				menuBridge.ForceContinueRefresh();
				view.SetStatus(File.Exists(Path.Combine(RuntimePaths.ActiveSavePath, "savefile.txt"))
					? "SAVE " + number + " IS READY."
					: "EMPTY SAVE " + number + " SELECTED - USE NEW GAME.");
				RefreshCards();
			}
			catch (Exception ex)
			{
				HandleFailure("Profile switch", ex);
			}
		}, delegate
		{
			operationRunning = false;
			view.SetPanelVisible(false);
		});
	}

	internal void RequestDelete(int number)
	{
		if (operationRunning) return;
		view.AskDelete(number, delegate { DeleteProfile(number); });
	}

	internal bool ApplyCustomThumbnail(string source)
	{
		EnsureReady();
		if (!thumbnails.ValidateImage(source)) return false;
		string selected = repository.SelectedSlot();
		string slot = repository.SlotPath(selected);
		Directory.CreateDirectory(slot);
		bool jpg = string.Equals(Path.GetExtension(source), ".jpg", StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetExtension(source), ".jpeg", StringComparison.OrdinalIgnoreCase);
		string fileName = jpg ? "screenshot.jpg" : "screenshot.png";
		string otherName = jpg ? "screenshot.png" : "screenshot.jpg";
		string destination = Path.Combine(slot, fileName);
		File.Copy(source, destination, true);
		DeleteFile(Path.Combine(slot, otherName));
		if (string.Equals(selected, repository.SelectedSlot(), StringComparison.OrdinalIgnoreCase))
		{
			Directory.CreateDirectory(RuntimePaths.ActiveSavePath);
			File.Copy(source, Path.Combine(RuntimePaths.ActiveSavePath, fileName), true);
			DeleteFile(Path.Combine(RuntimePaths.ActiveSavePath, otherName));
		}
		thumbnails.InvalidateFolder(slot);
		RefreshCards();
		return true;
	}

	internal void CaptureManualThumbnail()
	{
		if (!File.Exists(Path.Combine(RuntimePaths.ActiveSavePath, "savefile.txt"))) return;
		CaptureThenPersist("manual thumbnail");
	}

	internal void EnsureStorage()
	{
		repository.Initialize(mod.MaximumBackupsValue, mod.CopyEditorBackupsValue);
	}

	public void Dispose()
	{
		thumbnails.Dispose();
		if (view != null) view.Destroy();
		if (runtimeHost != null) UnityEngine.Object.Destroy(runtimeHost);
	}

	private void EnsureReady()
	{
		repository.Initialize(mod.MaximumBackupsValue, mod.CopyEditorBackupsValue);
		if (view == null) view = new SaveSlotsMenuView(this);
		if (repository.SafeModeActive && !safeModeReported)
		{
			safeModeReported = true;
			ModUI.ShowMessage("SAVE SLOTS SAFE MODE IS ACTIVE. Your previous save was preserved. Check SafeMode.txt and SaveSlotsDebug.log before switching again.", "Save Slots");
		}
	}

	private void RefreshCards()
	{
		if (view == null) return;
		ProfileOverview[] overviews = repository.ReadOverviews();
		string summary = BuildProfileSummary(overviews);
		if (!string.Equals(summary, lastProfileSummary, StringComparison.Ordinal))
		{
			lastProfileSummary = summary;
			DiagnosticWriter.Write("Profiles", summary);
		}
		ProfileCardModel[] cards = new ProfileCardModel[overviews.Length];
		for (int i = 0; i < overviews.Length; i++)
		{
			cards[i] = metadata.ReadFolder(repository.SlotPath(overviews[i].SlotName), i + 1, overviews[i].IsSelected, mod.DateFormatValue);
		}
		view.Bind(cards, thumbnails);
		nextPanelRefreshUtc = DateTime.UtcNow.AddSeconds(3d);
	}

	private static string BuildProfileSummary(ProfileOverview[] profiles)
	{
		string result = "";
		for (int i = 0; i < profiles.Length; i++)
		{
			ProfileOverview profile = profiles[i];
			if (i > 0) result += "; ";
			result += profile.SlotName
				+ "[selected=" + profile.IsSelected
				+ ",playable=" + profile.HasSave
				+ ",files=" + profile.FileCount
				+ ",bytes=" + profile.TotalBytes + "]";
		}
		return result;
	}

	private void ProcessPendingSave()
	{
		DelayedSaveState state = delayedSave.Poll(DateTime.UtcNow, File.Exists(Path.Combine(RuntimePaths.ActiveSavePath, "savefile.txt")));
		if (state == DelayedSaveState.Idle || state == DelayedSaveState.Waiting) return;
		if (state == DelayedSaveState.Expired)
		{
			DiagnosticWriter.Write("SaveCapture", "Timed out waiting for savefile.txt; the next save event will retry.");
			return;
		}
		CompletePendingSave(mod.AutomaticThumbnailValue ? "automatic save thumbnail" : "game save event");
	}

	private void CompletePendingSave(string reason)
	{
		lock (PendingSaveSync)
		{
			if (!pendingSave.Exists) return;
			if (!File.Exists(Path.Combine(RuntimePaths.ActiveSavePath, "savefile.txt")))
			{
				DiagnosticWriter.Write("SaveCapture", "Pending receipt retained because savefile.txt is not ready yet.");
				return;
			}
			string receiptSlot = pendingSave.ReadSlot() ?? "<unknown>";
			bool persisted = repository.TryPersistActive(reason, mod.CopyEditorBackupsValue);
			if (!persisted)
			{
				DiagnosticWriter.Write("SaveCapture", "Pending receipt retained because the active save could not be persisted.");
				return;
			}
			metadata.Invalidate();
			thumbnails.InvalidateFolder(repository.SlotPath(repository.SelectedSlot()));
			pendingSave.Clear();
			DiagnosticWriter.Write("SaveCapture", "Completed pending save for " + receiptSlot + "; screenshot=" + File.Exists(Path.Combine(RuntimePaths.ActiveSavePath, "screenshot.png")) + ".");
		}
	}

	private void CaptureThenPersist(string reason)
	{
		string target = Path.Combine(RuntimePaths.ActiveSavePath, "screenshot.png");
		bool started = screenshots.Capture(target, mod.HighResolutionValue, delegate(bool captured)
		{
			PersistActive(reason + (captured ? "" : " (thumbnail unavailable)"));
		});
		if (!started) PersistActive(reason + " (capture busy or no camera)");
	}

	private void PersistActive(string reason)
	{
		try
		{
			repository.TryPersistActive(reason, mod.CopyEditorBackupsValue);
			metadata.Invalidate();
			thumbnails.InvalidateFolder(repository.SlotPath(repository.SelectedSlot()));
		}
		catch (Exception ex)
		{
			HandleFailure("Save capture", ex);
		}
	}

	private void DeleteProfile(int number)
	{
		operationRunning = true;
		try
		{
			string backup = repository.DeleteProfile(number, mod.CopyEditorBackupsValue, mod.MaximumBackupsValue);
			metadata.Invalidate();
			thumbnails.InvalidateFolder(repository.SlotPath(ProfileRepository.SlotName(number)));
			menuBridge.ForceContinueRefresh();
			view.SetStatus(backup == null
				? "SAVE " + number + " WAS ALREADY EMPTY."
				: "SAVE " + number + " DELETED - RECOVERY BACKUP KEPT.");
			RefreshCards();
		}
		catch (Exception ex)
		{
			HandleFailure("Delete profile", ex);
		}
		finally { operationRunning = false; }
	}

	private static void HandleFailure(string area, Exception ex)
	{
		DiagnosticWriter.Exception(area, ex);
		ModConsole.LogError("Save Slots " + area.ToLowerInvariant() + " failed. The previous save was preserved; check SaveSlotsDebug.log and SafeMode.txt.\n" + ex);
		ModUI.ShowMessage(area + " failed. The previous playable save was preserved and safe mode is now active.", "Save Slots");
	}

	private static void DeleteFile(string path)
	{
		if (!File.Exists(path)) return;
		File.SetAttributes(path, FileAttributes.Normal);
		File.Delete(path);
	}
}
}

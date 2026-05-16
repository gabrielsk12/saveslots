using UnityEngine;

namespace SaveSlots
{
internal class LoadingBehaviour : MonoBehaviour
{
	private void OnEnable()
	{
		SaveSlotsDiagnosticLog.Log("LoadingBehaviour.OnEnable", "MWC Loading object enabled.");
		SaveSlots.NotifyLoadingStarted();
		if (SlotsManager.Instance == null)
		{
			SaveSlotsDiagnosticLog.Log("LoadingBehaviour.OnEnable", "SlotsManager.Instance is null.");
			return;
		}
		SlotBehaviour currentSave = SlotsManager.Instance.CurrentSaveLoaded();
		if ((Object)(object)currentSave != (Object)null)
		{
			SaveSlotsDiagnosticLog.Log("LoadingBehaviour.OnEnable", "Updating last played time for " + currentSave.SlotFileName);
			currentSave.UpdateTime();
		}
		else
		{
			SaveSlotsDiagnosticLog.Log("LoadingBehaviour.OnEnable", "CurrentSaveLoaded returned null.");
		}
		GameObject canvas = SlotsManager.Instance.Canvas();
		if ((Object)(object)canvas != (Object)null)
		{
			canvas.SetActive(false);
			SaveSlotsDiagnosticLog.Log("LoadingBehaviour.OnEnable", "Save Slots canvas hidden by loading behaviour.");
		}
	}
}
}


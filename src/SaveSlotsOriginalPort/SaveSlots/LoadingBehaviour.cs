using UnityEngine;

namespace SaveSlots
{
internal class LoadingBehaviour : MonoBehaviour
{
	private void OnEnable()
	{
		SlotBehaviour currentSave = SlotsManager.Instance.CurrentSaveLoaded();
		if ((Object)(object)currentSave != (Object)null)
		{
			currentSave.UpdateTime();
		}
		GameObject canvas = SlotsManager.Instance.Canvas();
		if ((Object)(object)canvas != (Object)null)
		{
			canvas.SetActive(false);
		}
	}
}
}


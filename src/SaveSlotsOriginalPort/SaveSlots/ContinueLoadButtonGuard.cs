using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SaveSlots
{
internal class ContinueLoadButtonGuard : MonoBehaviour
{
	private void Awake()
	{
		Button button = ((Component)this).GetComponent<Button>();
		if ((Object)(object)button != (Object)null)
		{
			SaveSlotsDiagnosticLog.Log("ContinueGuard.Awake", "Attached to " + ((Object)((Component)this).gameObject).name);
			((UnityEvent)button.onClick).AddListener(new UnityAction(OnContinueClicked));
		}
	}

	private void OnContinueClicked()
	{
		SaveSlotsDiagnosticLog.Log("ContinueGuard.OnContinueClicked", "Continue button clicked; entering loading mode.");
		SaveSlots.NotifyLoadingStarted();
	}
}
}

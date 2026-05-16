using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SaveSlots
{
internal class ButtonSaves : MonoBehaviour
{
	public GameObject saveUI = null;

	private void Awake()
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected O, but got Unknown
		((UnityEvent)((Component)this).GetComponent<Button>().onClick).AddListener(new UnityAction(ToggleUI));
	}

	private void ToggleUI()
	{
		SaveSlotsDiagnosticLog.Log("ButtonSaves.ToggleUI", "Clicked Saves button.");
		if ((Object)(object)saveUI == (Object)null)
		{
			SaveSlotsDiagnosticLog.Log("ButtonSaves.ToggleUI", "Ignored because saveUI is null.");
			return;
		}
		if (SaveSlots.MenuInteractionBlocked())
		{
			saveUI.SetActive(false);
			SaveSlotsDiagnosticLog.Log("ButtonSaves.ToggleUI", "Blocked because menu interaction is disabled during loading/gameplay.");
			return;
		}
		saveUI.SetActive(!saveUI.activeSelf);
		SaveSlotsDiagnosticLog.Log("ButtonSaves.ToggleUI", "Save UI active=" + saveUI.activeSelf);
		if (saveUI.activeSelf && SlotsManager.Instance != null)
		{
			SlotsManager.Instance.UpdateInfoOfAllSaves();
		}
	}
}
}


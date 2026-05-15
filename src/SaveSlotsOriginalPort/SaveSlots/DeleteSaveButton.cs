using MSCLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SaveSlots
{
internal class DeleteSaveButton : MonoBehaviour
{
	private SlotBehaviour slotBehaviour;

	private ModPrompt currentPrompt;

	public void Initialize(SlotBehaviour slotBehaviour)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Expected O, but got Unknown
		this.slotBehaviour = slotBehaviour;
		((UnityEvent)((Component)this).GetComponent<Button>().onClick).AddListener(new UnityAction(OnButtonClick));
	}

	private void OnButtonClick()
	{
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected O, but got Unknown
		if ((Object)(object)currentPrompt != (Object)null)
		{
			return;
		}
		SlotsManager.Instance.HideSaveSlotsCanvasForPrompt();
		currentPrompt = ModPrompt.CreateYesNoPrompt("You will <color=red>permamently</color> delete this save file!\n\nAre you sure you want to continue?", "Save Slots: Warning", (UnityAction)delegate
		{
			SlotsManager.Instance.DeleteSave(slotBehaviour);
		}, (UnityAction)null, (UnityAction)null);
	}
}
}


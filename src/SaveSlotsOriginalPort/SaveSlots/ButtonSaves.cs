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
		if ((Object)(object)saveUI == (Object)null)
		{
			return;
		}
		saveUI.SetActive(!saveUI.activeSelf);
		if (saveUI.activeSelf && SlotsManager.Instance != null)
		{
			SlotsManager.Instance.UpdateInfoOfAllSaves();
		}
	}
}
}


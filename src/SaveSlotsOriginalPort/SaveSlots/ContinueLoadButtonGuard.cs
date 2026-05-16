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
			((UnityEvent)button.onClick).AddListener(new UnityAction(SaveSlots.NotifyLoadingStarted));
		}
	}
}
}

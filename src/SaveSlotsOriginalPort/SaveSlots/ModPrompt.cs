using MSCLoader;
using UnityEngine;
using UnityEngine.Events;

namespace SaveSlots
{
internal class ModPrompt : MonoBehaviour
{
	public static ModPrompt CreatePrompt(string message, string title, UnityAction onClose)
	{
		ModUI.ShowMessage(message, title);
		if (onClose != null)
		{
			onClose();
		}
		return null;
	}

	public static ModPrompt CreateYesNoPrompt(string message, string title, UnityAction onYes, UnityAction onNo, UnityAction onClose)
	{
		ModUI.ShowYesNoMessage(message, title, delegate
		{
			if (onYes != null)
			{
				onYes();
			}
			if (onClose != null)
			{
				onClose();
			}
		});
		return null;
	}

	public void Close()
	{
		Destroy(gameObject);
	}
}
}

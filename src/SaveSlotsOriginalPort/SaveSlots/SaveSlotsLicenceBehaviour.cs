using UnityEngine;

namespace SaveSlots
{
internal class SaveSlotsLicenceBehaviour : MonoBehaviour
{
	private void OnEnable()
	{
		GameObject canvas = SlotsManager.Instance.Canvas();
		if ((Object)(object)canvas != (Object)null)
		{
			canvas.SetActive(false);
		}
	}

	private void OnDisable()
	{
		GameObject canvas = SlotsManager.Instance.Canvas();
		if ((Object)(object)canvas != (Object)null)
		{
			canvas.SetActive(true);
		}
	}
}
}


using UnityEngine;
using UnityEngine.EventSystems;

namespace SaveSlots
{
public class ResizeOnHover : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler
{
	public RectTransform element;

	public Vector3 hoverScale = new Vector3(0.9f, 0.9f, 0.9f);

	public Vector3 normalScale = Vector3.one;

	public void OnPointerEnter(PointerEventData eventData)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		normalScale = ((Transform)element).localScale;
		((Transform)element).localScale = hoverScale;
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		((Transform)element).localScale = normalScale;
	}

	private void OnDisable()
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)element != (Object)null)
		{
			((Transform)element).localScale = normalScale;
		}
	}
}
}


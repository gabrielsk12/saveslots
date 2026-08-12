using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MwcSaveSlots
{
internal static class UiPrimitives
{
	private static Font menuFont;
	private static bool fontResolved;
	internal static readonly Color32 BevelLight = new Color32(205, 236, 243, 255);
	internal static readonly Color32 BevelShadow = new Color32(0, 13, 25, 255);
	internal static readonly Color32 MwcNavy = new Color32(8, 27, 51, 255);
	internal static readonly Color32 MwcPanel = new Color32(17, 63, 105, 255);
	internal static readonly Color32 MwcPanelLight = new Color32(53, 111, 171, 255);
	internal static readonly Color32 MwcIce = new Color32(216, 237, 244, 255);
	internal static readonly Color32 MwcCyan = new Color32(0, 220, 244, 255);
	internal static readonly Color32 MwcOrange = new Color32(255, 112, 0, 255);
	internal static readonly Color32 MwcYellow = new Color32(255, 232, 0, 255);

	internal static GameObject Object(string name, Transform parent)
	{
		GameObject result = new GameObject(name, typeof(RectTransform));
		if (parent != null) result.transform.SetParent(parent, false);
		return result;
	}

	internal static Text Text(Transform parent, string name, string value, int size, TextAnchor anchor, Color color)
	{
		GameObject node = Object(name, parent);
		Text text = node.AddComponent<Text>();
		text.font = ResolveFont();
		text.text = value;
		text.fontSize = size;
		text.alignment = anchor;
		text.color = color;
		text.supportRichText = true;
		text.horizontalOverflow = HorizontalWrapMode.Wrap;
		text.verticalOverflow = VerticalWrapMode.Truncate;
		return text;
	}

	internal static Button Button(Transform parent, string name, string label, Color background, int fontSize, UnityAction action)
	{
		GameObject node = Object(name, parent);
		Image image = node.AddComponent<Image>();
		image.color = background;
		Button button = node.AddComponent<Button>();
		button.targetGraphic = image;
		button.transition = Selectable.Transition.ColorTint;
		ColorBlock colors = button.colors;
		colors.normalColor = Color.white;
		colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
		colors.pressedColor = new Color(.72f, .9f, 1f, 1f);
		colors.disabledColor = new Color(.44f, .52f, .58f, .72f);
		colors.colorMultiplier = 1f;
		colors.fadeDuration = .08f;
		button.colors = colors;
		button.onClick.AddListener(UiSoundPlayer.PlayClick);
		if (action != null) button.onClick.AddListener(action);
		Text caption = Text(node.transform, "Caption", label, fontSize, TextAnchor.MiddleCenter, Color.white);
		caption.fontStyle = FontStyle.Bold;
		Stretch(caption.rectTransform, 0f, 0f, 0f, 0f);
		return button;
	}

	internal static Outline Outline(GameObject target, Color color, Vector2 distance)
	{
		Outline outline = target.AddComponent<Outline>();
		outline.effectColor = color;
		outline.effectDistance = distance;
		outline.useGraphicAlpha = true;
		return outline;
	}

	internal static Image Band(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 position)
	{
		Image image = Object(name, parent).AddComponent<Image>();
		image.color = color;
		Rect(image.rectTransform, anchorMin, anchorMax, size, position);
		return image;
	}

	internal static void Bevel(Transform target, float thickness)
	{
		Band(target, "BevelTop", BevelLight, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, thickness), new Vector2(0f, -thickness * .5f));
		Band(target, "BevelLeft", BevelLight, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(thickness, 0f), new Vector2(thickness * .5f, 0f));
		Band(target, "BevelBottom", BevelShadow, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, thickness), new Vector2(0f, thickness * .5f));
		Band(target, "BevelRight", BevelShadow, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(thickness, 0f), new Vector2(-thickness * .5f, 0f));
	}

	internal static void Border(Transform target, string name, Color color, float thickness, float inset)
	{
		float edge = inset + (thickness * .5f);
		Band(target, name + "Top", color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-inset * 2f, thickness), new Vector2(0f, -edge));
		Band(target, name + "Bottom", color, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-inset * 2f, thickness), new Vector2(0f, edge));
		Band(target, name + "Left", color, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(thickness, -inset * 2f), new Vector2(edge, 0f));
		Band(target, name + "Right", color, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(thickness, -inset * 2f), new Vector2(-edge, 0f));
	}

	internal static void TextShadow(Text text)
	{
		Shadow shadow = text.gameObject.AddComponent<Shadow>();
		shadow.effectColor = new Color(0f, 0f, 0f, .9f);
		shadow.effectDistance = new Vector2(3f, -3f);
		shadow.useGraphicAlpha = true;
	}

	internal static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = new Vector2(left, bottom);
		rect.offsetMax = new Vector2(-right, -top);
	}

	internal static void Rect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 position)
	{
		rect.anchorMin = anchorMin;
		rect.anchorMax = anchorMax;
		rect.sizeDelta = size;
		rect.anchoredPosition = position;
	}

	internal static float EaseOutCubic(float value)
	{
		float t = Mathf.Clamp01(value);
		float remaining = 1f - t;
		return 1f - (remaining * remaining * remaining);
	}

	internal static float EaseInCubic(float value)
	{
		float t = Mathf.Clamp01(value);
		return t * t * t;
	}

	private static Font ResolveFont()
	{
		if (fontResolved) return menuFont;
		fontResolved = true;
		Font[] fonts = Resources.FindObjectsOfTypeAll<Font>();
		for (int i = 0; i < fonts.Length; i++)
		{
			Font candidate = fonts[i];
			if (candidate == null) continue;
			string name = candidate.name ?? "";
			if (!string.Equals(name, "FugazOne-Regular", StringComparison.OrdinalIgnoreCase)
				&& !string.Equals(name, "Fugaz One", StringComparison.OrdinalIgnoreCase)) continue;
			menuFont = candidate;
			DiagnosticWriter.Write("MenuFont", "Using MWC's loaded " + name + " font; no font bytes are embedded by Save Slots.");
			return menuFont;
		}
		menuFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
		DiagnosticWriter.Write("MenuFont", "MWC Fugaz font was not loaded; using Unity's built-in Arial fallback.");
		return menuFont;
	}
}

internal sealed class CardHoverMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
	private RectTransform rect;
	private CanvasGroup group;
	private Outline outline;
	private Vector2 restingPosition;
	private Color restingGlow = new Color(0f, 220f / 255f, 244f / 255f, .2f);
	private Color hoverGlow = new Color(1f, 112f / 255f, 0f, .72f);
	private float displayedScale = 1f;
	private float entranceScale = 1f;
	private float entranceDelay;
	private float entranceElapsed;
	private bool entranceRunning;
	private bool pointerInside;
	private bool pointerDown;

	internal void Configure(Outline cardOutline)
	{
		rect = GetComponent<RectTransform>();
		group = GetComponent<CanvasGroup>();
		if (group == null) group = gameObject.AddComponent<CanvasGroup>();
		outline = cardOutline;
		restingPosition = rect.anchoredPosition;
		if (outline != null) outline.effectColor = restingGlow;
	}

	internal void PlayEntrance(float delay)
	{
		EnsureConfigured();
		restingPosition = rect.anchoredPosition;
		entranceDelay = Mathf.Max(0f, delay);
		entranceElapsed = 0f;
		entranceScale = .94f;
		entranceRunning = true;
		group.alpha = 0f;
		rect.anchoredPosition = restingPosition + new Vector2(0f, -34f);
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		pointerInside = true;
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		pointerInside = false;
		pointerDown = false;
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		pointerDown = true;
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		pointerDown = false;
	}

	private void Update()
	{
		EnsureConfigured();
		float delta = Time.unscaledDeltaTime;
		if (entranceRunning)
		{
			entranceElapsed += delta;
			float progress = (entranceElapsed - entranceDelay) / .32f;
			if (progress > 0f)
			{
				float eased = UiPrimitives.EaseOutCubic(progress);
				group.alpha = eased;
				entranceScale = .94f + (.06f * eased);
				rect.anchoredPosition = restingPosition + new Vector2(0f, -34f * (1f - eased));
				if (progress >= 1f)
				{
					entranceRunning = false;
					group.alpha = 1f;
					entranceScale = 1f;
					rect.anchoredPosition = restingPosition;
				}
			}
		}

		float targetScale = pointerDown ? .985f : pointerInside ? 1.025f : 1f;
		float response = 1f - Mathf.Exp(-delta * 14f);
		displayedScale = Mathf.Lerp(displayedScale, targetScale, response);
		transform.localScale = new Vector3(displayedScale * entranceScale, displayedScale * entranceScale, 1f);
		if (outline != null) outline.effectColor = Color.Lerp(outline.effectColor, pointerInside ? hoverGlow : restingGlow, response);
	}

	private void OnDisable()
	{
		if (rect != null) rect.anchoredPosition = restingPosition;
		if (group != null) group.alpha = 1f;
		transform.localScale = Vector3.one;
		displayedScale = 1f;
		entranceScale = 1f;
		entranceRunning = false;
		pointerInside = false;
		pointerDown = false;
	}

	private void EnsureConfigured()
	{
		if (rect != null) return;
		Configure(GetComponent<Outline>());
	}
}

internal sealed class MenuButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
	private RectTransform rect;
	private CanvasGroup group;
	private Outline outline;
	private Vector2 restingPosition;
	private float entranceElapsed;
	private float displayedScale = 1f;
	private bool entranceRunning;
	private bool hover;
	private bool pressed;

	internal void Configure(Outline buttonOutline)
	{
		rect = GetComponent<RectTransform>();
		group = GetComponent<CanvasGroup>();
		if (group == null) group = gameObject.AddComponent<CanvasGroup>();
		outline = buttonOutline;
		restingPosition = rect.anchoredPosition;
	}

	internal void PlayEntrance()
	{
		if (rect == null) Configure(GetComponent<Outline>());
		if (entranceRunning) rect.anchoredPosition = restingPosition;
		else restingPosition = rect.anchoredPosition;
		entranceElapsed = 0f;
		entranceRunning = true;
		group.alpha = 0f;
		rect.anchoredPosition = restingPosition + new Vector2(48f, 0f);
	}

	public void OnPointerEnter(PointerEventData eventData) { hover = true; }
	public void OnPointerExit(PointerEventData eventData) { hover = false; pressed = false; }
	public void OnPointerDown(PointerEventData eventData) { pressed = true; }
	public void OnPointerUp(PointerEventData eventData) { pressed = false; }

	private void Update()
	{
		if (rect == null) Configure(GetComponent<Outline>());
		float delta = Time.unscaledDeltaTime;
		if (entranceRunning)
		{
			entranceElapsed += delta;
			float eased = UiPrimitives.EaseOutCubic(entranceElapsed / .42f);
			group.alpha = eased;
			rect.anchoredPosition = restingPosition + new Vector2(48f * (1f - eased), 0f);
			if (entranceElapsed >= .42f)
			{
				entranceRunning = false;
				group.alpha = 1f;
				rect.anchoredPosition = restingPosition;
			}
		}

		float targetScale = pressed ? .96f : hover ? 1.06f : 1f;
		float response = 1f - Mathf.Exp(-delta * 16f);
		displayedScale = Mathf.Lerp(displayedScale, targetScale, response);
		transform.localScale = new Vector3(displayedScale, displayedScale, 1f);
		if (outline != null)
		{
			float pulse = hover ? .72f + (Mathf.Sin(Time.unscaledTime * 7f) * .15f) : .38f;
			Color target = hover
				? new Color(1f, 112f / 255f, 0f, pulse)
				: new Color(0f, 220f / 255f, 244f / 255f, pulse);
			outline.effectColor = Color.Lerp(outline.effectColor, target, response);
		}
	}

	private void OnDisable()
	{
		if (rect != null) rect.anchoredPosition = restingPosition;
		if (group != null) group.alpha = 1f;
		transform.localScale = Vector3.one;
		displayedScale = 1f;
		entranceRunning = false;
		hover = false;
		pressed = false;
	}
}

internal sealed class UiPanelAnimator : MonoBehaviour
{
	private CanvasGroup group;
	private RectTransform frame;
	private CardHoverMotion[] cards;
	private Coroutine transition;
	private bool shown;
	private bool inputBlocked;

	internal bool IsShown { get { return shown; } }

	internal void Configure(CanvasGroup canvasGroup, RectTransform animatedFrame, CardHoverMotion[] cardAnimations)
	{
		group = canvasGroup;
		frame = animatedFrame;
		cards = cardAnimations;
	}

	internal void SetInputBlocked(bool blocked)
	{
		inputBlocked = blocked;
		if (group == null) return;
		group.interactable = shown && !blocked;
		group.blocksRaycasts = shown && !blocked;
	}

	internal void Show()
	{
		if (shown && gameObject.activeSelf) return;
		shown = true;
		gameObject.SetActive(true);
		StopTransition();
		group.alpha = 0f;
		group.interactable = false;
		group.blocksRaycasts = false;
		frame.localScale = new Vector3(.93f, .93f, 1f);
		if (cards != null)
		{
			for (int i = 0; i < cards.Length; i++) if (cards[i] != null) cards[i].PlayEntrance(.06f + (i * .065f));
		}
		transition = StartCoroutine(OpenAnimation());
	}

	internal void Hide()
	{
		shown = false;
		if (!gameObject.activeSelf) return;
		StopTransition();
		group.interactable = false;
		group.blocksRaycasts = false;
		transition = StartCoroutine(CloseAnimation());
	}

	internal void HideImmediate()
	{
		shown = false;
		StopTransition();
		if (group != null)
		{
			group.alpha = 0f;
			group.interactable = false;
			group.blocksRaycasts = false;
		}
		if (frame != null) frame.localScale = Vector3.one;
		if (gameObject.activeSelf) gameObject.SetActive(false);
	}

	private IEnumerator OpenAnimation()
	{
		float elapsed = 0f;
		while (elapsed < .3f)
		{
			elapsed += Time.unscaledDeltaTime;
			float eased = UiPrimitives.EaseOutCubic(elapsed / .3f);
			group.alpha = eased;
			float scale = .93f + (.07f * eased);
			frame.localScale = new Vector3(scale, scale, 1f);
			yield return null;
		}
		group.alpha = 1f;
		frame.localScale = Vector3.one;
		group.interactable = !inputBlocked;
		group.blocksRaycasts = !inputBlocked;
		transition = null;
	}

	private IEnumerator CloseAnimation()
	{
		float startAlpha = group.alpha;
		Vector3 startScale = frame.localScale;
		float elapsed = 0f;
		while (elapsed < .2f)
		{
			elapsed += Time.unscaledDeltaTime;
			float eased = UiPrimitives.EaseInCubic(elapsed / .2f);
			group.alpha = Mathf.Lerp(startAlpha, 0f, eased);
			frame.localScale = Vector3.Lerp(startScale, new Vector3(.965f, .965f, 1f), eased);
			yield return null;
		}
		group.alpha = 0f;
		frame.localScale = Vector3.one;
		transition = null;
		gameObject.SetActive(false);
	}

	private void StopTransition()
	{
		if (transition == null) return;
		StopCoroutine(transition);
		transition = null;
	}
}
}

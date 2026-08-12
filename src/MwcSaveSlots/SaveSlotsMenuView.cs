using System;
using MSCLoader;
using UnityEngine;
using UnityEngine.UI;

namespace MwcSaveSlots
{
internal sealed class SaveSlotsMenuView
{
	private readonly ProfileCoordinator coordinator;
	private readonly ProfileCardView[] cards = new ProfileCardView[ProfileRepository.ProfileCount];
	private GameObject root;
	private GameObject panel;
	private GameObject menuButtonContainer;
	private Button menuButton;
	private Canvas canvas;
	private GraphicRaycaster raycaster;
	private CanvasGroup panelGroup;
	private RectTransform frameRect;
	private UiPanelAnimator panelAnimator;
	private MenuButtonMotion menuButtonMotion;
	private NativeMenuClickTarget nativeClickTarget;
	private Text status;
	private ShutterTransition shutter;
	private UiSoundPlayer uiSound;
	private bool promptOpen;
	private bool? lastMenuVisibility;
	private bool? lastPanelVisibility;
	private bool nativeMenuButton;
	private string menuButtonSource = "<not created>";
	private DateTime nextNativeButtonSearchUtc = DateTime.MinValue;

	internal SaveSlotsMenuView(ProfileCoordinator coordinator)
	{
		this.coordinator = coordinator;
		BuildIndependentCanvas();
		DiagnosticWriter.Write("Menu", "Built the independent runtime Save Slots canvas. No MSC UI bundle is loaded or embedded.");
	}

	internal bool PanelVisible { get { return panelAnimator != null && panelAnimator.IsShown; } }

	internal void SetMenuVisible(bool visible)
	{
		bool changed = !lastMenuVisibility.HasValue || lastMenuVisibility.Value != visible;
		if (promptOpen && visible)
		{
			SetMenuButtonActive(false);
			if (root != null && root.activeSelf) root.SetActive(false);
			return;
		}

		if (visible)
		{
			EnsureButtonHierarchy();
			if (changed && menuButtonMotion != null) menuButtonMotion.PlayEntrance();
		}
		else
		{
			if (panelAnimator != null) panelAnimator.HideImmediate();
			SetMenuButtonActive(false);
			if (root != null && root.activeSelf) root.SetActive(false);
		}

		if (changed)
		{
			lastMenuVisibility = visible;
			DiagnosticWriter.Write("MenuVisibility", "requested=" + visible + " " + DescribeState());
		}
	}

	internal void SetPanelVisible(bool visible)
	{
		if (visible) EnsureButtonHierarchy();
		if (panelAnimator != null)
		{
			if (visible) panelAnimator.Show();
			else panelAnimator.Hide();
		}
		if (!lastPanelVisibility.HasValue || lastPanelVisibility.Value != visible)
		{
			lastPanelVisibility = visible;
			DiagnosticWriter.Write("PanelVisibility", "requested=" + visible + " " + DescribeState());
		}
	}

	internal void ForceVisible(bool openPanel)
	{
		promptOpen = false;
		EnsureButtonHierarchy();
		if (menuButton != null) menuButton.interactable = true;
		if (nativeClickTarget != null) nativeClickTarget.SetBlocked(false);
		if (menuButtonMotion != null) menuButtonMotion.PlayEntrance();
		SetPanelVisible(openPanel);
		DiagnosticWriter.Write("Console", "Forced Save Slots UI visible. " + DescribeState());
	}

	internal string DescribeState()
	{
		string rootState = root == null ? "<missing>" : "self=" + root.activeSelf + ",hierarchy=" + root.activeInHierarchy;
		string canvasState = canvas == null ? "<missing>" : "enabled=" + canvas.enabled + ",mode=" + canvas.renderMode + ",sorting=" + canvas.sortingOrder;
		string raycasterState = raycaster == null ? "<missing>" : "enabled=" + raycaster.enabled;
		string containerState = menuButtonContainer == null ? "<missing>" : "self=" + menuButtonContainer.activeSelf + ",hierarchy=" + menuButtonContainer.activeInHierarchy;
		string buttonState = menuButton != null
			? "unityButton self=" + menuButton.gameObject.activeSelf + ",hierarchy=" + menuButton.gameObject.activeInHierarchy + ",interactable=" + menuButton.interactable
			: nativeClickTarget != null ? "nativeClickTarget blocked=" + nativeClickTarget.IsBlocked : "<missing>";
		RectTransform rect = menuButtonContainer == null ? null : menuButtonContainer.GetComponent<RectTransform>();
		string rectState = rect != null
			? "anchor=" + rect.anchoredPosition + ",size=" + rect.sizeDelta + ",world=" + rect.position
			: menuButtonContainer == null ? "<missing>" : "local=" + menuButtonContainer.transform.localPosition + ",world=" + menuButtonContainer.transform.position;
		string frameState = frameRect == null ? "<missing>" : "anchor=" + frameRect.anchoredPosition + ",size=" + frameRect.sizeDelta + ",world=" + frameRect.position;
		string cardState = "";
		for (int i = 0; i < cards.Length; i++) cardState += (i == 0 ? "" : ",") + "Save" + (i + 1) + "=" + (cards[i] == null ? "<missing>" : cards[i].LayoutState);
		return "ui=independent-runtime"
			+ " menuEntry=" + (nativeMenuButton ? "native-clone" : "fallback")
			+ " source=\"" + menuButtonSource + "\""
			+ " root{" + rootState + "}"
			+ " canvas{" + canvasState + "}"
			+ " raycaster{" + raycasterState + "}"
			+ " container{" + containerState + "}"
			+ " button{" + buttonState + "}"
			+ " rect{" + rectState + "}"
			+ " frame{" + frameState + "}"
			+ " cards{" + cardState + "}"
			+ " panelActive=" + (panel == null ? "<missing>" : panel.activeSelf.ToString())
			+ " panelShown=" + PanelVisible
			+ " promptOpen=" + promptOpen;
	}

	internal void SetBlocked(bool blocked)
	{
		if (menuButton != null) menuButton.interactable = !blocked;
		if (nativeClickTarget != null) nativeClickTarget.SetBlocked(blocked);
		if (panelAnimator != null) panelAnimator.SetInputBlocked(blocked);
	}

	internal void Bind(ProfileCardModel[] models, ThumbnailService thumbnails)
	{
		for (int i = 0; i < cards.Length && i < models.Length; i++)
		{
			cards[i].Bind(models[i], thumbnails.ForFolder(models[i].FolderPath));
		}
	}

	internal void SetStatus(string message)
	{
		if (status != null) status.text = message ?? "";
		DiagnosticWriter.Write("Menu", message ?? "");
	}

	internal void PlayShutter(Action middle, Action complete)
	{
		shutter.Play(middle, complete);
	}

	internal void AskDelete(int profileNumber, Action accepted)
	{
		promptOpen = true;
		SetMenuButtonActive(false);
		if (root != null) root.SetActive(false);
		MsgBoxBtn yes = ModUI.CreateMessageBoxBtn("YES", delegate
		{
			try { if (accepted != null) accepted(); }
			finally { RestoreAfterPrompt(); }
		}, false);
		MsgBoxBtn no = ModUI.CreateMessageBoxBtn("NO", RestoreAfterPrompt, false);
		ModUI.ShowCustomMessage(
			"Delete Save" + profileNumber + " from the menu?\n\nA verified recovery copy will be kept under EmergencyBackups\\DeletedProfiles before any save data is removed.",
			"Save Slots: Warning",
			new[] { yes, no });
	}

	internal void Destroy()
	{
		if (nativeMenuButton && menuButtonContainer != null) UnityEngine.Object.Destroy(menuButtonContainer);
		if (uiSound != null) UnityEngine.Object.Destroy(uiSound.gameObject);
		if (root != null) UnityEngine.Object.Destroy(root);
		menuButton = null;
		nativeClickTarget = null;
		menuButtonContainer = null;
		root = null;
	}

	private void BuildIndependentCanvas()
	{
		root = UiPrimitives.Object("MwcSaveSlotsCanvasV4", null);
		UnityEngine.Object.DontDestroyOnLoad(root);
		uiSound = UiSoundPlayer.Create();
		canvas = root.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.overrideSorting = true;
		canvas.sortingOrder = 900;
		CanvasScaler scaler = root.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = .5f;
		raycaster = root.AddComponent<GraphicRaycaster>();

		EnsureMenuButton();

		panel = UiPrimitives.Object("MwcSaveSlotsPanelLayer", root.transform);
		UiPrimitives.Stretch(panel.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
		Image backdrop = panel.AddComponent<Image>();
		backdrop.color = new Color(2f / 255f, 10f / 255f, 25f / 255f, .68f);
		panelGroup = panel.AddComponent<CanvasGroup>();

		GameObject frame = UiPrimitives.Object("PanelFrame", panel.transform);
		Image frameImage = frame.AddComponent<Image>();
		frameImage.color = UiPrimitives.MwcNavy;
		frameRect = frame.GetComponent<RectTransform>();
		UiPrimitives.Rect(frameRect, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(MwcMenuLayout.FrameWidth, MwcMenuLayout.FrameHeight), Vector2.zero);
		UiPrimitives.Border(frame.transform, "FrameOuter", UiPrimitives.MwcIce, 4f, 4f);
		UiPrimitives.Border(frame.transform, "FrameInner", new Color(0f, 220f / 255f, 244f / 255f, .72f), 2f, 12f);
		UiPrimitives.Outline(frame, new Color(0f, 0f, 0f, .82f), new Vector2(7f, -7f));

		Image header = UiPrimitives.Band(frame.transform, "Header", UiPrimitives.MwcPanel,
			new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-28f, 58f), new Vector2(0f, -40f));
		UiPrimitives.Border(header.transform, "HeaderBorder", UiPrimitives.MwcIce, 3f, 1f);
		Text title = UiPrimitives.Text(header.transform, "Title", "SAVES", 36, TextAnchor.MiddleCenter, UiPrimitives.MwcCyan);
		title.fontStyle = FontStyle.Bold;
		UiPrimitives.Stretch(title.rectTransform, 10f, 10f, 0f, 0f);
		UiPrimitives.TextShadow(title);

		CardHoverMotion[] cardAnimations = new CardHoverMotion[cards.Length];
		for (int i = 0; i < cards.Length; i++)
		{
			cards[i] = new ProfileCardView(frame.transform, i + 1, coordinator,
				new Vector2(MwcMenuLayout.CardX(i), MwcMenuLayout.CardY));
			cardAnimations[i] = cards[i].Motion;
		}

		status = UiPrimitives.Text(frame.transform, "Status", "SELECT A SAVE  //  EMPTY SLOTS START A NEW GAME", 19, TextAnchor.MiddleCenter, UiPrimitives.MwcYellow);
		status.fontStyle = FontStyle.Bold;
		status.verticalOverflow = VerticalWrapMode.Overflow;
		UiPrimitives.Rect(status.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-48f, 30f), new Vector2(0f, 68f));
		UiPrimitives.TextShadow(status);

		Button close = UiPrimitives.Button(frame.transform, "Close", "CLOSE", UiPrimitives.MwcPanel, 28, coordinator.ClosePanel);
		UiPrimitives.Rect(close.GetComponent<RectTransform>(), new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(1268f, 48f), new Vector2(0f, 31f));
		UiPrimitives.Border(close.transform, "CloseBorder", UiPrimitives.MwcIce, 3f, 1f);
		Text closeCaption = close.GetComponentInChildren<Text>();
		if (closeCaption != null)
		{
			closeCaption.color = UiPrimitives.MwcCyan;
			UiPrimitives.TextShadow(closeCaption);
		}
		Outline closeOutline = UiPrimitives.Outline(close.gameObject, new Color(0f, 220f / 255f, 244f / 255f, .25f), new Vector2(1f, -1f));
		CardHoverMotion closeMotion = close.gameObject.AddComponent<CardHoverMotion>();
		closeMotion.Configure(closeOutline);

		panelAnimator = panel.AddComponent<UiPanelAnimator>();
		panelAnimator.Configure(panelGroup, frameRect, cardAnimations);
		panelAnimator.HideImmediate();

		shutter = root.AddComponent<ShutterTransition>();
		shutter.Build(root.transform);
		EnsureButtonHierarchy();
		if (menuButtonMotion != null) menuButtonMotion.PlayEntrance();
		DiagnosticWriter.Write("MenuBuild", "Independent canvas created from Unity UI primitives. screen=" + Screen.width + "x" + Screen.height + " scale=" + canvas.scaleFactor + " " + DescribeState());
	}

	private void EnsureButtonHierarchy()
	{
		if (canvas != null) canvas.enabled = true;
		if (raycaster != null) raycaster.enabled = true;
		if (root != null && !root.activeSelf) root.SetActive(true);
		EnsureMenuButton();
		SetMenuButtonActive(true);
	}

	private void EnsureMenuButton()
	{
		if (nativeMenuButton && menuButtonContainer != null && nativeClickTarget != null)
		{
			SetNativeCaption();
			return;
		}

		if (nativeMenuButton && (menuButtonContainer == null || nativeClickTarget == null))
		{
			nativeMenuButton = false;
			menuButtonContainer = null;
			nativeClickTarget = null;
			menuButtonSource = "<scene button was destroyed>";
		}

		if (!nativeMenuButton && DateTime.UtcNow >= nextNativeButtonSearchUtc)
		{
			nextNativeButtonSearchUtc = DateTime.UtcNow.AddSeconds(1d);
			GameObject nativeContainer;
			NativeMenuClickTarget clickTarget;
			string source;
			if (TryCreateNativeMenuButton(out nativeContainer, out clickTarget, out source))
			{
				if (menuButtonContainer != null) UnityEngine.Object.Destroy(menuButtonContainer);
				menuButton = null;
				menuButtonContainer = nativeContainer;
				nativeClickTarget = clickTarget;
				nativeMenuButton = true;
				menuButtonMotion = null;
				menuButtonSource = source;
				DiagnosticWriter.Write("MenuEntry", "Cloned MWC's Continue-button presentation for SAVES. source=" + source + " transform=" + DescribeTransform(menuButtonContainer.transform));
				return;
			}
		}

		if (menuButton == null) BuildFallbackMenuButton();
	}

	private bool TryCreateNativeMenuButton(out GameObject createdContainer, out NativeMenuClickTarget createdClickTarget, out string source)
	{
		createdContainer = null;
		createdClickTarget = null;
		source = "<not found>";
		GameObject template = FindGameMenuObject("continue");
		if (template == null || template.transform.parent == null) return false;

		GameObject clone = UnityEngine.Object.Instantiate(template) as GameObject;
		if (clone == null) return false;
		clone.name = "MwcSaveSlotsNativeButton";
		clone.transform.SetParent(template.transform.parent, false);
		DisableCopiedGameLogic(clone);

		RectTransform templateRect = template.GetComponent<RectTransform>();
		RectTransform cloneRect = clone.GetComponent<RectTransform>();
		if (templateRect != null && cloneRect != null)
		{
			GameObject nextButton = FindSiblingMenuObject(template, "new");
			Vector2 entryPosition = templateRect.anchoredPosition + new Vector2(0f, 64f);
			if (nextButton != null)
			{
				RectTransform nextRect = nextButton.GetComponent<RectTransform>();
				if (nextRect != null)
				{
					entryPosition = new Vector2(
						MwcMenuLayout.PreviousMenuCoordinate(templateRect.anchoredPosition.x, nextRect.anchoredPosition.x),
						MwcMenuLayout.PreviousMenuCoordinate(templateRect.anchoredPosition.y, nextRect.anchoredPosition.y));
				}
			}
			cloneRect.anchoredPosition = entryPosition;
		}
		else
		{
			GameObject nextButton = FindSiblingMenuObject(template, "new");
			Vector3 templatePosition = template.transform.localPosition;
			clone.transform.localPosition = nextButton == null
				? templatePosition + new Vector3(0f, .08f, 0f)
				: templatePosition + (templatePosition - nextButton.transform.localPosition);
		}

		Collider clickCollider = FirstEnabledCollider(clone);
		if (clickCollider == null)
		{
			DiagnosticWriter.Write("MenuEntry", "The cloned Continue object had no enabled collider; keeping the styled fallback.");
			UnityEngine.Object.Destroy(clone);
			return false;
		}
		NativeMenuClickTarget clickTarget = clickCollider.gameObject.AddComponent<NativeMenuClickTarget>();
		clickTarget.Configure(coordinator.TogglePanel, clone.transform);

		createdContainer = clone;
		createdClickTarget = clickTarget;
		source = HierarchyPath(template.transform) + " components=" + ComponentSummary(template);
		SetCaption(clone, "SAVES");
		clone.SetActive(true);
		return true;
	}

	private void BuildFallbackMenuButton()
	{
		menuButton = UiPrimitives.Button(root.transform, "MwcSavesMenuButtonFallback", "SAVES", Color.clear, 36, coordinator.TogglePanel);
		menuButtonContainer = menuButton.gameObject;
		nativeClickTarget = null;
		UiPrimitives.Rect(menuButton.GetComponent<RectTransform>(), new Vector2(1f, .5f), new Vector2(1f, .5f),
			new Vector2(MwcMenuLayout.MenuButtonWidth, MwcMenuLayout.MenuButtonHeight),
			new Vector2(MwcMenuLayout.MenuButtonOffsetX, MwcMenuLayout.MenuButtonOffsetY));
		Text caption = menuButton.GetComponentInChildren<Text>();
		if (caption != null)
		{
			caption.color = UiPrimitives.MwcCyan;
			UiPrimitives.TextShadow(caption);
		}
		menuButtonMotion = menuButton.gameObject.AddComponent<MenuButtonMotion>();
		menuButtonMotion.Configure(null);
		menuButtonSource = "independent text fallback";
		DiagnosticWriter.Write("MenuEntry", "MWC Continue button was not available yet; using the text fallback and retrying the native lookup.");
	}

	private void SetNativeCaption()
	{
		if (nativeMenuButton && menuButtonContainer != null) SetCaption(menuButtonContainer, "SAVES");
	}

	private void SetMenuButtonActive(bool active)
	{
		if (menuButtonContainer != null && menuButtonContainer.activeSelf != active) menuButtonContainer.SetActive(active);
	}

	private static void SetCaption(GameObject target, string value)
	{
		Text[] labels = target.GetComponentsInChildren<Text>(true);
		for (int i = 0; i < labels.Length; i++) labels[i].text = value;
		TextMesh[] meshes = target.GetComponentsInChildren<TextMesh>(true);
		for (int i = 0; i < meshes.Length; i++) meshes[i].text = value;
	}

	private static void DisableCopiedGameLogic(GameObject clone)
	{
		MonoBehaviour[] behaviours = clone.GetComponentsInChildren<MonoBehaviour>(true);
		for (int i = 0; i < behaviours.Length; i++)
		{
			MonoBehaviour behaviour = behaviours[i];
			if (behaviour == null) continue;
			behaviour.enabled = false;
		}
		Button[] buttons = clone.GetComponentsInChildren<Button>(true);
		for (int i = 0; i < buttons.Length; i++) buttons[i].onClick.RemoveAllListeners();
	}

	private static GameObject FindGameMenuObject(string namePart)
	{
		string[] paths =
		{
			"Interface/Buttons/ButtonContinue",
			"Interface/Buttons/Continue",
			"Interface/ButtonContinue"
		};
		for (int i = 0; i < paths.Length; i++)
		{
			GameObject exact = GameObject.Find(paths[i]);
			if (exact != null) return exact;
		}
		GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
		for (int i = 0; i < objects.Length; i++)
		{
			GameObject candidate = objects[i];
			if (candidate == null || candidate.name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) < 0) continue;
			if (candidate.name.IndexOf("MwcSaveSlots", StringComparison.OrdinalIgnoreCase) >= 0) continue;
			if (candidate.transform.parent == null || !string.Equals(candidate.transform.parent.name, "Buttons", StringComparison.OrdinalIgnoreCase)) continue;
			return candidate;
		}
		return null;
	}

	private static GameObject FindSiblingMenuObject(GameObject template, string namePart)
	{
		Transform parent = template.transform.parent;
		for (int i = 0; i < parent.childCount; i++)
		{
			Transform child = parent.GetChild(i);
			if (child == null || child.gameObject == template) continue;
			if (child.name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0) return child.gameObject;
		}
		return null;
	}

	private static Collider FirstEnabledCollider(GameObject target)
	{
		Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
		for (int i = 0; i < colliders.Length; i++)
		{
			if (colliders[i] != null && colliders[i].enabled) return colliders[i];
		}
		return null;
	}

	private static string ComponentSummary(GameObject target)
	{
		Component[] components = target.GetComponents<Component>();
		string value = "";
		for (int i = 0; i < components.Length; i++)
		{
			if (components[i] == null) continue;
			value += (value.Length == 0 ? "" : ",") + components[i].GetType().Name;
		}
		return value;
	}

	private static string DescribeRect(RectTransform rect)
	{
		return rect == null ? "<missing>" : "anchor=" + rect.anchoredPosition + ",size=" + rect.sizeDelta + ",world=" + rect.position;
	}

	private static string DescribeTransform(Transform transform)
	{
		RectTransform rect = transform as RectTransform;
		return rect != null ? DescribeRect(rect) : "local=" + transform.localPosition + ",world=" + transform.position + ",scale=" + transform.localScale;
	}

	private static string HierarchyPath(Transform transform)
	{
		string path = transform.name;
		while (transform.parent != null)
		{
			transform = transform.parent;
			path = transform.name + "/" + path;
		}
		return path;
	}

	private void RestoreAfterPrompt()
	{
		promptOpen = false;
		if (root != null) root.SetActive(true);
		SetMenuButtonActive(true);
		if (menuButtonMotion != null) menuButtonMotion.PlayEntrance();
	}
}
}

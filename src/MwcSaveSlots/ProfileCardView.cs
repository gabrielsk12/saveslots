using UnityEngine;
using UnityEngine.UI;

namespace MwcSaveSlots
{
internal sealed class ProfileCardView
{
	private readonly ProfileCoordinator coordinator;
	private readonly Image background;
	private readonly Image thumbnail;
	private readonly Text playerName;
	private readonly Text details;
	private readonly Text lastPlayed;
	private readonly Text emptyMessage;
	private readonly Button deleteButton;
	private readonly Text title;
	private readonly Text trimLine;
	private readonly Image detailsDivider;
	private readonly CardHoverMotion motion;
	private ProfileCardModel model;
	private string boundSignature;
	private Sprite boundScreenshot;

	internal GameObject Root { get; private set; }
	internal CardHoverMotion Motion { get { return motion; } }
	internal string LayoutState
	{
		get
		{
			RectTransform rect = Root == null ? null : Root.GetComponent<RectTransform>();
			return rect == null ? "<missing>" : rect.anchoredPosition + "/" + rect.sizeDelta;
		}
	}

	internal ProfileCardView(Transform parent, int number, ProfileCoordinator coordinator, Vector2 position)
	{
		this.coordinator = coordinator;
		Root = UiPrimitives.Object("MwcProfileCard" + number, parent);
		UiPrimitives.Rect(Root.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
			new Vector2(MwcMenuLayout.CardWidth, MwcMenuLayout.CardHeight), position);
		background = Root.AddComponent<Image>();
		background.color = UiPrimitives.MwcPanel;
		UiPrimitives.Border(Root.transform, "OuterBorder", UiPrimitives.MwcIce, 3f, 3f);
		UiPrimitives.Border(Root.transform, "InnerBorder", new Color(0f, 220f / 255f, 244f / 255f, .65f), 1f, 9f);
		Outline cardOutline = UiPrimitives.Outline(Root, new Color(0f, 220f / 255f, 244f / 255f, .2f), new Vector2(2f, -2f));

		Button selectButton = Root.AddComponent<Button>();
		selectButton.targetGraphic = background;
		selectButton.onClick.AddListener(UiSoundPlayer.PlayClick);
		selectButton.onClick.AddListener(SelectCurrentModel);
		motion = Root.AddComponent<CardHoverMotion>();
		motion.Configure(cardOutline);

		title = UiPrimitives.Text(Root.transform, "SlotTitle", "SAVE " + number, 22, TextAnchor.MiddleLeft, Color.white);
		title.fontStyle = FontStyle.Bold;
		title.resizeTextForBestFit = true;
		title.resizeTextMinSize = 18;
		title.resizeTextMaxSize = 22;
		UiPrimitives.Rect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-66f, 34f), new Vector2(-17f, -24f));
		UiPrimitives.TextShadow(title);

		deleteButton = UiPrimitives.Button(Root.transform, "Delete", "X", UiPrimitives.MwcOrange, 28, RequestDelete);
		UiPrimitives.Rect(deleteButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(40f, 38f), new Vector2(-26f, -25f));
		UiPrimitives.Border(deleteButton.transform, "DeleteBorder", UiPrimitives.MwcIce, 2f, 1f);
		Text deleteCaption = deleteButton.GetComponentInChildren<Text>();
		if (deleteCaption != null)
		{
			deleteCaption.text = "X";
			deleteCaption.color = UiPrimitives.MwcNavy;
			deleteCaption.fontStyle = FontStyle.Bold;
			deleteCaption.verticalOverflow = VerticalWrapMode.Overflow;
			UiPrimitives.TextShadow(deleteCaption);
		}

		Image thumbnailFrame = UiPrimitives.Object("ThumbnailFrame", Root.transform).AddComponent<Image>();
		thumbnailFrame.color = UiPrimitives.MwcNavy;
		UiPrimitives.Rect(thumbnailFrame.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-24f, 180f), new Vector2(0f, -141f));
		UiPrimitives.Border(thumbnailFrame.transform, "ImageBorder", UiPrimitives.MwcIce, 3f, 0f);

		thumbnail = UiPrimitives.Object("Thumbnail", thumbnailFrame.transform).AddComponent<Image>();
		thumbnail.color = Color.white;
		thumbnail.preserveAspect = true;
		UiPrimitives.Stretch(thumbnail.rectTransform, 5f, 5f, 5f, 5f);

		playerName = UiPrimitives.Text(Root.transform, "PlayerName", "", 27, TextAnchor.MiddleCenter, UiPrimitives.MwcCyan);
		playerName.fontStyle = FontStyle.Bold;
		playerName.resizeTextForBestFit = true;
		playerName.resizeTextMinSize = 18;
		playerName.resizeTextMaxSize = 27;
		playerName.verticalOverflow = VerticalWrapMode.Overflow;
		UiPrimitives.Rect(playerName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-28f, 40f), new Vector2(0f, -252f));
		UiPrimitives.TextShadow(playerName);

		trimLine = UiPrimitives.Text(Root.transform, "Trim", "", 18, TextAnchor.MiddleCenter, Color.white);
		UiPrimitives.Rect(trimLine.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-28f, 28f), new Vector2(0f, -286f));
		UiPrimitives.TextShadow(trimLine);

		detailsDivider = UiPrimitives.Band(Root.transform, "DetailsDivider", new Color(1f, 1f, 1f, .45f),
			new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-30f, 2f), new Vector2(0f, -306f));

		details = UiPrimitives.Text(Root.transform, "Details", "", 21, TextAnchor.UpperCenter, Color.white);
		UiPrimitives.Rect(details.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-28f, 128f), new Vector2(0f, -376f));
		UiPrimitives.TextShadow(details);

		lastPlayed = UiPrimitives.Text(Root.transform, "LastPlayed", "", 20, TextAnchor.LowerCenter, Color.white);
		lastPlayed.verticalOverflow = VerticalWrapMode.Overflow;
		UiPrimitives.Rect(lastPlayed.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 70f), new Vector2(0f, 48f));
		UiPrimitives.TextShadow(lastPlayed);

		emptyMessage = UiPrimitives.Text(Root.transform, "EmptyProfile", "EMPTY SLOT\nSELECT TO USE\nNEW GAME", 27, TextAnchor.MiddleCenter, UiPrimitives.MwcYellow);
		emptyMessage.fontStyle = FontStyle.Bold;
		UiPrimitives.Stretch(emptyMessage.rectTransform, 24f, 24f, 58f, 12f);
		UiPrimitives.TextShadow(emptyMessage);

		// Keep the destructive control above the thumbnail and other card content.
		deleteButton.transform.SetAsLastSibling();
		if (deleteCaption != null) deleteCaption.transform.SetAsLastSibling();
	}

	internal void Bind(ProfileCardModel value, Sprite screenshot)
	{
		string signature = Signature(value);
		if (string.Equals(boundSignature, signature, System.StringComparison.Ordinal) && boundScreenshot == screenshot)
		{
			model = value;
			return;
		}
		boundSignature = signature;
		boundScreenshot = screenshot;
		model = value;
		background.color = value.IsSelected ? UiPrimitives.MwcPanelLight : UiPrimitives.MwcPanel;
		title.text = value.IsSelected
			? "SAVE " + value.Number + "  //  <color=#FF7000>CURRENT</color>"
			: "SAVE " + value.Number;
		ShowSaveDetails(value.HasSave);
		if (!value.HasSave)
		{
			emptyMessage.text = value.IsSelected
				? "<color=#FF7000>CURRENT EMPTY SLOT</color>\n<color=#FFE800>START A NEW GAME</color>"
				: "<color=#FFE800>EMPTY SLOT\nSELECT TO USE\nNEW GAME</color>";
			return;
		}

		thumbnail.sprite = screenshot;
		playerName.text = "<color=#00DCF4>" + Escape(value.PlayerName.ToUpperInvariant()) + "</color>";
		string money = value.Money > 999999f ? ":)" : Mathf.FloorToInt(value.Money).ToString();
		trimLine.text = "CORRIS TRIM: <color=#00DCF4>" + Escape(value.Trim.ToUpperInvariant()) + "</color>";
		details.text = BuildDetails(money, value);
		lastPlayed.text = "LAST PLAYED:\n<color=#00DCF4>" + Escape(value.LastPlayedText.ToUpperInvariant()) + "</color>";
	}

	private static string Signature(ProfileCardModel value)
	{
		return value.Number + "|" + value.IsSelected + "|" + value.HasSave + "|"
			+ value.PlayerName + "|" + value.Trim + "|" + value.Money + "|" + value.Mortal + "|"
			+ value.Location + "|" + value.LastPlayed.Ticks + "|" + value.LastPlayedText;
	}

	private static string BuildDetails(string money, ProfileCardModel value)
	{
		return "MONEY: <color=#00DCF4>" + money + "</color>\n"
			+ "MORTAL: <color=#00DCF4>" + (value.Mortal ? "YES" : "NO") + "</color>\n"
			+ "LOCATION:\n<color=#00DCF4>" + Escape(value.Location.ToUpperInvariant()) + "</color>";
	}

	private void ShowSaveDetails(bool visible)
	{
		thumbnail.transform.parent.gameObject.SetActive(visible);
		playerName.gameObject.SetActive(visible);
		details.gameObject.SetActive(visible);
		lastPlayed.gameObject.SetActive(visible);
		deleteButton.gameObject.SetActive(visible);
		trimLine.gameObject.SetActive(visible);
		detailsDivider.gameObject.SetActive(visible);
		emptyMessage.gameObject.SetActive(!visible);
	}

	private void SelectCurrentModel()
	{
		if (model != null) coordinator.SelectProfile(model.Number);
	}

	private void RequestDelete()
	{
		if (model != null) coordinator.RequestDelete(model.Number);
	}

	private static string Escape(string value)
	{
		return (value ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
	}
}
}

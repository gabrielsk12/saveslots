using System;
using System.IO;
using System.Linq;
using MSCLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityObject = UnityEngine.Object;

namespace SaveSlots
{
internal class SlotBehaviour : MonoBehaviour
{
	private Image background;

	private Sprite dummyScreenshot;

	private Image lastScreenshot;

	private GameObject deleteButton;

	private Text playerName;

	private Text info;

	private Text lastPlayed;

	private GameObject newSaveInfo;

	private string slotFileName;

	private DateTime lastPlayedTime;

	private Texture2D ssTexture;

	private const string MwcSaveFileName = "savefile.txt";

	internal string SlotFileName
	{
		get
		{
			return slotFileName;
		}
		set
		{
			slotFileName = value;
		}
	}

	private string ThisSavePath
	{
		get
		{
			if (!(SlotsManager.Instance.CurrentSaveLoadedName() == slotFileName))
			{
				return Path.Combine(SlotsManager.Instance.SaveSlotsFolder, slotFileName);
			}
			return Application.persistentDataPath;
		}
	}

	private void Awake()
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected O, but got Unknown
		SlotsManager.Instance.Add(this);
		((UnityEvent)((Component)this).GetComponent<Button>().onClick).AddListener(new UnityAction(OnButtonClick));
		background = ((Component)this).GetComponent<Image>();
		lastScreenshot = ((Component)((Component)this).transform.Find("Image")).GetComponent<Image>();
		dummyScreenshot = lastScreenshot.sprite;
		deleteButton = ((Component)((Component)this).transform.Find("DeleteButton")).gameObject;
		playerName = ((Component)((Component)this).transform.Find("Name")).GetComponent<Text>();
		info = ((Component)((Component)this).transform.Find("Info")).GetComponent<Text>();
		lastPlayed = ((Component)((Component)this).transform.Find("LastPlayed")).GetComponent<Text>();
		newSaveInfo = ((Component)((Component)this).transform.Find("NewSaveInfo")).gameObject;
		deleteButton.AddComponent<DeleteSaveButton>().Initialize(this);
		LoadSaveData();
		SlotsManager.Instance.UpdateSelectedButtons(SlotsManager.Instance.CurrentSaveLoaded());
	}

	private void OnButtonClick()
	{
		SaveSlotsDiagnosticLog.Log("SlotBehaviour.OnButtonClick", "Clicked " + slotFileName);
		SlotsManager.Instance.UpdateSelectedButtons(this);
		SlotsManager.Instance.LoadSave(this);
	}

	internal void SetColor(Color color)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		((Graphic)background).color = color;
	}

	internal SaveData GetSaveData()
	{
		return new SaveData(slotFileName, lastPlayedTime);
	}

	internal void UpdateInfoData(bool isActive, string playerName, float money, bool mortal, string location, string lastPlayed)
	{
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		newSaveInfo.SetActive(!isActive);
		((Component)lastScreenshot).gameObject.SetActive(isActive);
		deleteButton.gameObject.SetActive(isActive);
		((Component)this.playerName).gameObject.SetActive(isActive);
		((Component)info).gameObject.SetActive(isActive);
		((Component)this.lastPlayed).gameObject.SetActive(isActive);
		this.playerName.text = "<color=#52D6FF>" + playerName.ToUpper() + "</color>";
		info.text = "MONEY: <color=#52D6FF>" + ((money > 999999f) ? ":)" : Mathf.FloorToInt(money).ToString()) + "</color>\nMORTAL: <color=#52D6FF>" + (mortal ? "YES" : "NO") + "</color>\nLOCATION:\n<color=#52D6FF>" + location.ToUpper() + "</color>";
		this.lastPlayed.text = "LAST PLAYED:\n<color=#52D6FF>" + lastPlayed.ToUpper() + "</color>";
		if (!isActive)
		{
			lastScreenshot.sprite = dummyScreenshot;
			return;
		}
		string text = Path.Combine(ThisSavePath, "screenshot.jpg");
		if (File.Exists(text))
		{
			if ((UnityObject)(object)ssTexture != (UnityObject)null)
			{
				UnityObject.Destroy((UnityObject)(object)ssTexture);
			}
			ssTexture = LoadTexture(text);
			lastScreenshot.sprite = Sprite.Create(ssTexture, new Rect(0f, 0f, (float)((Texture)ssTexture).width, (float)((Texture)ssTexture).height), new Vector2(0f, 0f), 100f, 2u);
		}
	}

	internal void LoadSaveData()
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Expected O, but got Unknown
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		string text = Path.Combine(ThisSavePath, MwcSaveFileName);
		if (!Directory.Exists(ThisSavePath) || !File.Exists(text))
		{
			SaveSlotsDiagnosticLog.Log("SlotBehaviour.LoadSaveData", slotFileName + " is empty. path=" + ThisSavePath);
			UpdateInfoData(isActive: false, "?", 0f, mortal: false, "UNKNOWN", "NEVER");
			return;
		}
		ES2Settings val = new ES2Settings();
		if (!HasTag(text, "PlayerTransform"))
		{
			lastPlayedTime = File.GetLastWriteTime(text);
			UpdateInfoData(isActive: true, "PLAYER", 0f, mortal: false, "SAVED DATA", DayToWords(lastPlayedTime));
		}
		else
		{
			bool mortal = LoadBool(text, "PlayerPermaDeath", val, defaultValue: false);
			string text2 = LoadString(text, "PlayerName", val, "PLAYER");
			float money = LoadFloat(text, "PlayerMoney", val, 0f);
			Vector3 position = LoadPlayerPosition(text, val);
			string closestLocation = SlotsManager.Instance.GetClosestLocation(position);
			SaveData saveData = LoadSaveDataInfo(text);
			lastPlayedTime = saveData.lastPlayed;
			string text3 = DayToWords(saveData.lastPlayed);
			UpdateInfoData(isActive: true, text2, money, mortal, closestLocation, text3);
			val = null;
			saveData = null;
		}
	}

	private bool HasTag(string saveFile, string tag)
	{
		try
		{
			return (from t in ES2.GetTags(saveFile)
				where t == tag || t.StartsWith(tag, StringComparison.Ordinal)
				select t).Count() != 0;
		}
		catch (Exception ex)
		{
			SaveSlotsDiagnosticLog.LogException("SlotBehaviour.HasTag " + saveFile + " tag=" + tag, ex);
			ModConsole.LogError("Save Slots could not read tags from " + saveFile + "\n" + ex);
			return false;
		}
	}

	private bool LoadBool(string saveFile, string tag, ES2Settings settings, bool defaultValue)
	{
		try
		{
			return HasTag(saveFile, tag) ? ES2.Load<bool>(saveFile + "?tag=" + tag, settings) : defaultValue;
		}
		catch
		{
			return defaultValue;
		}
	}

	private float LoadFloat(string saveFile, string tag, ES2Settings settings, float defaultValue)
	{
		try
		{
			return HasTag(saveFile, tag) ? ES2.Load<float>(saveFile + "?tag=" + tag, settings) : defaultValue;
		}
		catch
		{
			return defaultValue;
		}
	}

	private string LoadString(string saveFile, string tag, ES2Settings settings, string defaultValue)
	{
		try
		{
			string value = HasTag(saveFile, tag) ? ES2.Load<string>(saveFile + "?tag=" + tag, settings) : defaultValue;
			return string.IsNullOrEmpty(value) ? defaultValue : value;
		}
		catch
		{
			return defaultValue;
		}
	}

	private Vector3 LoadPlayerPosition(string saveFile, ES2Settings settings)
	{
		try
		{
			Transform transform = ES2.Load<Transform>(saveFile + "?tag=PlayerTransform", settings);
			if ((UnityObject)(object)transform != (UnityObject)null)
			{
				return transform.position;
			}
		}
		catch (Exception ex)
		{
			SaveSlotsDiagnosticLog.LogException("SlotBehaviour.LoadPlayerPosition " + saveFile, ex);
			ModConsole.LogError("Save Slots could not read PlayerTransform from " + saveFile + "\n" + ex);
		}
		return Vector3.zero;
	}

	private SaveData LoadSaveDataInfo(string saveFile)
	{
		try
		{
			SaveData saveData = ModSave.Load<SaveData>(Path.Combine(ThisSavePath, "SaveSlots"), "");
			if (saveData != null && saveData.lastPlayed > new DateTime(1970, 1, 1))
			{
				return saveData;
			}
		}
		catch
		{
			SaveSlotsDiagnosticLog.Log("SlotBehaviour.LoadSaveDataInfo", "Could not read SaveSlots metadata for " + slotFileName + "; using savefile timestamp.");
		}
		return new SaveData(slotFileName, File.GetLastWriteTime(saveFile));
	}

	private string DayToWords(DateTime day)
	{
		DateTime date = DateTime.Now.Date;
		day = day.Date;
		if (date == day)
		{
			return "TODAY";
		}
		if (date.AddDays(-1.0) == day)
		{
			return "YESTERDAY";
		}
		if (day <= new DateTime(1970, 1, 1))
		{
			return "NEVER";
		}
		int format = SaveSlots.DateFormat != null ? SaveSlots.DateFormat.GetValue() : 0;
		if (format == 1)
		{
			return day.ToString("MM/dd/yyyy");
		}
		if (format == 2)
		{
			return day.ToString("yyyy/MM/dd");
		}
		if (format == 3)
		{
			return day.ToString("MMM dd, yyyy");
		}
		return day.ToString("dd/MM/yyyy");
	}

	private Texture2D LoadTexture(string path)
	{
		Texture2D texture = new Texture2D(1, 1);
		texture.LoadImage(File.ReadAllBytes(path));
		return texture;
	}

	internal void UpdateTime()
	{
		lastPlayedTime = DateTime.Now;
		ModSave.Save<SaveData>("SaveSlots", GetSaveData(), (string)null);
	}
}
}


using System;
using System.IO;
using MSCLoader;
using SaveSlotsMWC.Core;
using UnityEngine;

namespace SaveSlotsMWC.Mod
{
    public sealed class SaveSlotsMod : global::MSCLoader.Mod
    {
        private const int SlotCount = 4;
        private static readonly string[] DateFormats =
        {
            "DD/MM/YYYY",
            "MM/DD/YYYY",
            "YYYY/MM/DD",
            "Month D, Yr"
        };

        private SaveSlotManager manager;
        private SettingsCheckBox synchronizeOptions;
        private SettingsCheckBox highResScreenshot;
        private SettingsCheckBox createScreenshotOnEachSave;
        private SettingsCheckBox copyEditorBackups;
        private SettingsSliderInt dateFormat;
        private bool showWindow = true;
        private Rect windowRect = new Rect(20f, 80f, 360f, 430f);
        private string status = "";
        private GUIStyle windowStyle;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle statusStyle;
        private GUIStyle buttonStyle;
        private GUIStyle slotStyle;
        private GUIStyle activeSlotStyle;
        private Texture2D windowTexture;
        private Texture2D slotTexture;
        private Texture2D activeSlotTexture;
        private Texture2D buttonTexture;
        private Texture2D buttonHoverTexture;

        public override string ID { get { return "SaveSlotsMWC"; } }
        public override string Name { get { return "SAVE SLOTS MWC"; } }
        public override string Author { get { return "Gabriel_SK"; } }
        public override string Version { get { return "1.0"; } }
        public override string Description { get { return "Save slot manager for My Winter Car. Made by: Gabriel_SK."; } }
        public override Game SupportedGames { get { return Game.MyWinterCar; } }

        public override void ModSetup()
        {
            SetupFunction(Setup.OnLoad, OnGameLoad);
            SetupFunction(Setup.OnSave, OnGameSave);
            SetupFunction(Setup.Update, OnModUpdate);
            SetupFunction(Setup.OnGUI, OnModGUI);
            SetupFunction(Setup.ModSettings, ConfigureSettings);
            SetupFunction(Setup.OnModEnabled, OnEnabledChanged);
            SetupFunction(Setup.OnModDisabled, OnEnabledChanged);
        }

        private void ConfigureSettings()
        {
            Settings.AddHeader("SAVE SLOTS MWC");
            synchronizeOptions = Settings.AddCheckBox("SynchronizeOptions", "SYNCHRONIZE GAME OPTIONS", true);
            copyEditorBackups = Settings.AddCheckBox("CopyEditorBackups", "ALSO COPY EDITOR BACKUP FILES", false);
            highResScreenshot = Settings.AddCheckBox("HighResScreenshot", "HIGH RESOLUTION SCREENSHOTS", false);
            createScreenshotOnEachSave = Settings.AddCheckBox("CreateScreenshotOnEachSave", "CREATE THUMBNAIL WHILE SAVING", true);
            dateFormat = Settings.AddSlider("DateFormat", "DATE FORMAT", 0, 3, 0, null, DateFormats);
            Settings.AddText("Made by: Gabriel_SK");
            Settings.AddText("Discord: gabriel_sk");
        }

        private void OnGameLoad()
        {
            manager = CreateManager();
            status = "Ready";
            ModConsole.Log("[SaveSlotsMWC] Loaded. Made by Gabriel_SK.");
        }

        private void OnGameSave()
        {
            EnsureManager();
            SaveCurrentSlotTime();
            if (GetBool(createScreenshotOnEachSave, true))
            {
                CaptureThumbnail();
            }
        }

        private void OnModUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F7))
            {
                showWindow = !showWindow;
            }

            if (!GetBool(createScreenshotOnEachSave, true) && Input.GetKeyDown(KeyCode.F8))
            {
                CaptureThumbnail();
                status = "Thumbnail captured";
            }
        }

        private void OnModGUI()
        {
            EnsureStyles();

            Color previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = new Color32(20, 105, 138, 255);
            if (GUI.Button(new Rect(20f, 40f, 150f, 30f), "SAVE SLOTS", buttonStyle))
            {
                showWindow = !showWindow;
            }
            GUI.backgroundColor = previousBackground;

            if (showWindow)
            {
                windowRect = GUILayout.Window(8247, windowRect, DrawWindow, "SAVE SLOTS MWC", windowStyle);
            }
        }

        private void DrawWindow(int id)
        {
            EnsureManager();

            GUILayout.Label("Made by: Gabriel_SK", labelStyle);
            GUILayout.Label("Discord: gabriel_sk", labelStyle);
            GUILayout.Space(6f);
            GUILayout.Label("Current slot: " + manager.GetCurrentSlotName(), titleStyle);
            if (!string.IsNullOrEmpty(status))
            {
                GUILayout.Label(status, statusStyle);
            }

            GUILayout.Space(8f);
            for (int i = 1; i <= SlotCount; i++)
            {
                DrawSlot("Save" + i);
            }

            GUILayout.Space(8f);
            GUILayout.Label("F7 toggles this window. F8 captures a thumbnail when automatic thumbnails are off.", labelStyle);
            GUI.DragWindow();
        }

        private void DrawSlot(string slotName)
        {
            string current = manager.GetCurrentSlotName();
            string slotPath = string.Equals(current, slotName, StringComparison.OrdinalIgnoreCase)
                ? Application.persistentDataPath
                : Path.Combine(manager.SaveSlotsPath, slotName);
            SaveSlotMetadata metadata = SaveSlotMetadataStore.Load(Path.Combine(slotPath, SaveSlotManager.MetadataFileName));
            bool hasSave = Directory.Exists(slotPath) && File.Exists(Path.Combine(slotPath, "savefile.txt"));

            bool active = string.Equals(current, slotName, StringComparison.OrdinalIgnoreCase);

            GUILayout.BeginVertical(active ? activeSlotStyle : slotStyle);
            GUILayout.Label(slotName + (active ? "  ACTIVE" : ""), titleStyle);
            GUILayout.Label(hasSave ? "Last played: " + FormatDate(metadata.LastPlayed) : "New save", labelStyle);

            GUILayout.BeginHorizontal();
            GUI.enabled = !active;
            if (GUILayout.Button(hasSave ? "LOAD" : "CREATE", buttonStyle))
            {
                SwitchSlot(slotName);
            }

            GUI.enabled = hasSave && !active;
            if (GUILayout.Button("DELETE", buttonStyle))
            {
                DeleteSlot(slotName);
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void SwitchSlot(string slotName)
        {
            try
            {
                SaveSlotSwitchResult result = manager.SwitchToSlot(slotName, new SaveSlotOptions
                {
                    SynchronizeOptions = GetBool(synchronizeOptions, true),
                    CopyEditorBackups = GetBool(copyEditorBackups, false)
                });
                status = result.ContinueAvailable
                    ? "Loaded " + slotName + ". Use the game's Continue button."
                    : "Created " + slotName + ". Start a new game for this slot.";
                ModConsole.Log("[SaveSlotsMWC] Switched to " + slotName);
            }
            catch (Exception ex)
            {
                status = "Switch failed. See console.";
                ModConsole.Error("[SaveSlotsMWC] Switch failed: " + ex);
                ModUI.ShowMessage("Save Slots MWC stopped switching saves to prevent data loss.\n\n" + ex.Message, "Save Slots MWC");
            }
        }

        private void DeleteSlot(string slotName)
        {
            string path = Path.Combine(manager.SaveSlotsPath, slotName);
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                    status = "Deleted " + slotName;
                }
            }
            catch (Exception ex)
            {
                status = "Delete failed. See console.";
                ModConsole.Error("[SaveSlotsMWC] Delete failed: " + ex);
            }
        }

        private void SaveCurrentSlotTime()
        {
            SaveSlotMetadataStore.Save(
                Path.Combine(Application.persistentDataPath, SaveSlotManager.MetadataFileName),
                new SaveSlotMetadata(manager.GetCurrentSlotName(), DateTime.Now));
        }

        private void CaptureThumbnail()
        {
            try
            {
                int scale = GetBool(highResScreenshot, false) ? 3 : 1;
                Application.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "screenshot.jpg"), scale);
            }
            catch (Exception ex)
            {
                ModConsole.Error("[SaveSlotsMWC] Thumbnail failed: " + ex.Message);
            }
        }

        private string FormatDate(DateTime date)
        {
            if (date <= new DateTime(1970, 1, 1))
            {
                return "NEVER";
            }

            DateTime today = DateTime.Now.Date;
            if (date.Date == today)
            {
                return "TODAY";
            }

            if (date.Date == today.AddDays(-1))
            {
                return "YESTERDAY";
            }

            int format = dateFormat != null ? dateFormat.GetValue() : 0;
            if (format == 1) return date.ToString("MM/dd/yyyy");
            if (format == 2) return date.ToString("yyyy/MM/dd");
            if (format == 3) return date.ToString("MMM dd, yyyy");
            return date.ToString("dd/MM/yyyy");
        }

        private bool GetBool(SettingsCheckBox setting, bool fallback)
        {
            return setting != null ? setting.GetValue() : fallback;
        }

        private void EnsureManager()
        {
            if (manager == null)
            {
                manager = CreateManager();
            }
        }

        private SaveSlotManager CreateManager()
        {
            string active = Application.persistentDataPath;
            string root = Directory.GetParent(active) != null
                ? Directory.GetParent(active).FullName
                : active.Replace(Application.productName, "");
            return new SaveSlotManager(active, root);
        }

        private void OnEnabledChanged()
        {
            ModUI.ShowMessage("Save Slots MWC enable/disable changes are safest after restarting the game.", "Save Slots MWC");
        }

        private void EnsureStyles()
        {
            if (windowStyle != null)
            {
                return;
            }

            windowTexture = MakeTexture(new Color32(9, 78, 101, 244));
            slotTexture = MakeTexture(new Color32(12, 93, 122, 238));
            activeSlotTexture = MakeTexture(new Color32(17, 126, 164, 248));
            buttonTexture = MakeTexture(new Color32(18, 112, 148, 255));
            buttonHoverTexture = MakeTexture(new Color32(34, 151, 193, 255));

            windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.normal.background = windowTexture;
            windowStyle.onNormal.background = windowTexture;
            windowStyle.normal.textColor = Color.white;
            windowStyle.fontStyle = FontStyle.Bold;
            windowStyle.fontSize = 15;
            windowStyle.padding = new RectOffset(12, 12, 24, 12);

            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.normal.textColor = new Color32(255, 238, 60, 255);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 13;

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.normal.textColor = Color.white;
            labelStyle.fontSize = 12;
            labelStyle.wordWrap = true;

            statusStyle = new GUIStyle(labelStyle);
            statusStyle.normal.textColor = new Color32(130, 225, 255, 255);
            statusStyle.fontStyle = FontStyle.Bold;

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.normal.background = buttonTexture;
            buttonStyle.hover.background = buttonHoverTexture;
            buttonStyle.active.background = activeSlotTexture;
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.white;
            buttonStyle.fontStyle = FontStyle.Bold;

            slotStyle = new GUIStyle(GUI.skin.box);
            slotStyle.normal.background = slotTexture;
            slotStyle.normal.textColor = Color.white;
            slotStyle.padding = new RectOffset(8, 8, 6, 6);
            slotStyle.margin = new RectOffset(2, 2, 4, 4);

            activeSlotStyle = new GUIStyle(slotStyle);
            activeSlotStyle.normal.background = activeSlotTexture;
        }

        private Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}

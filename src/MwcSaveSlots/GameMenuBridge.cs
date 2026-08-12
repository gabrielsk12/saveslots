using System;
using System.IO;
using System.Reflection;
using MSCLoader;
using UnityEngine;
using UnityEngine.UI;

namespace MwcSaveSlots
{
internal sealed class GameMenuBridge
{
	private readonly string activeSavePath;
	private readonly Action<string, string> logger;
	private GameObject continueObject;
	private DateTime nextSearchUtc = DateTime.MinValue;
	private DateTime nextVisibilityUtc = DateTime.MinValue;
	private DateTime nextSuppressionScanUtc = DateTime.MinValue;
	private DateTime nextLoadingSearchUtc = DateTime.MinValue;
	private DateTime nextModMenuSearchUtc = DateTime.MinValue;
	private GameObject loadingRoot;
	private Component modMenuButton;
	private FieldInfo modMenuOpenedField;
	private bool lastSuppressed;
	private bool loadingTransitionLatched;
	private string lastSuppressionReason = "not scanned";
	private bool suppressionInitialized;
	private bool reflectionFailureLogged;

	internal GameMenuBridge(string activeSavePath, Action<string, string> logger)
	{
		this.activeSavePath = activeSavePath;
		this.logger = logger;
	}

	internal bool InteractionSuppressed()
	{
		if (loadingTransitionLatched) return SetSuppression(true, "menu transition is still loading");
		if (DateTime.UtcNow < nextSuppressionScanUtc) return lastSuppressed;
		nextSuppressionScanUtc = DateTime.UtcNow.AddMilliseconds(300d);
		if (IsModMenuOpen()) return SetSuppression(true, "MSCLoader mod menu is open");
		if (loadingRoot != null && loadingRoot.activeInHierarchy)
		{
			loadingTransitionLatched = true;
			return SetSuppression(true, "active loading object at " + HierarchyPath(loadingRoot.transform));
		}
		if (DateTime.UtcNow >= nextLoadingSearchUtc)
		{
			nextLoadingSearchUtc = DateTime.UtcNow.AddSeconds(2d);
			GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
			for (int i = 0; i < all.Length; i++)
			{
				GameObject node = all[i];
				if (node == null || node.transform.parent != null) continue;
				if (!string.Equals(node.name ?? "", "Loading", StringComparison.OrdinalIgnoreCase)) continue;
				loadingRoot = node;
				if (node.activeInHierarchy)
				{
					loadingTransitionLatched = true;
					return SetSuppression(true, "active loading object at " + HierarchyPath(node.transform));
				}
				break;
			}
		}
		return SetSuppression(false, "main menu is interactive");
	}

	internal void BeginMenuSession()
	{
		loadingTransitionLatched = false;
		nextSuppressionScanUtc = DateTime.MinValue;
		SetSuppression(false, "new main-menu session");
	}

	internal void EndMenuSession()
	{
		loadingTransitionLatched = true;
		SetSuppression(true, "gameplay scene is active");
	}

	internal string DescribeState()
	{
		return "suppressed=" + lastSuppressed
			+ " reason=\"" + lastSuppressionReason + "\""
			+ " loadingLatch=" + loadingTransitionLatched
			+ " continue=" + (continueObject == null ? "<not found>" : HierarchyPath(continueObject.transform));
	}

	internal void SynchronizeContinueButton()
	{
		if (DateTime.UtcNow < nextVisibilityUtc) return;
		nextVisibilityUtc = DateTime.UtcNow.AddMilliseconds(450d);
		LocateContinueButton();
		if (continueObject != null)
		{
			bool shouldShow = File.Exists(Path.Combine(activeSavePath, "savefile.txt"));
			if (continueObject.activeSelf != shouldShow) continueObject.SetActive(shouldShow);
		}
	}

	internal void ForceContinueRefresh()
	{
		nextVisibilityUtc = DateTime.MinValue;
		SynchronizeContinueButton();
	}

	private void LocateContinueButton()
	{
		if (continueObject != null) return;
		if (DateTime.UtcNow < nextSearchUtc) return;
		nextSearchUtc = DateTime.UtcNow.AddSeconds(1d);
		string[] paths =
		{
			"Interface/Buttons/ButtonContinue",
			"Interface/Buttons/Continue",
			"Interface/ButtonContinue"
		};
		for (int i = 0; i < paths.Length; i++)
		{
			GameObject exact = GameObject.Find(paths[i]);
			if (exact != null)
			{
				continueObject = exact;
				return;
			}
		}
		Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();
		for (int i = 0; i < buttons.Length; i++)
		{
			Button button = buttons[i];
			if (button == null) continue;
			string name = button.gameObject.name ?? "";
			if (name.IndexOf("continue", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				continueObject = button.gameObject;
				Log("MenuBridge", "Continue button located at " + HierarchyPath(button.transform));
				return;
			}
		}
	}

	private bool IsModMenuOpen()
	{
		try
		{
			if (modMenuButton == null || modMenuOpenedField == null)
			{
				if (DateTime.UtcNow < nextModMenuSearchUtc) return false;
				nextModMenuSearchUtc = DateTime.UtcNow.AddSeconds(2d);
				FindModMenuButton();
			}
			if (modMenuButton == null || modMenuOpenedField == null) return false;
			object opened = modMenuOpenedField.GetValue(modMenuButton);
			return opened is bool && (bool)opened;
		}
		catch (Exception ex)
		{
			if (!reflectionFailureLogged)
			{
				reflectionFailureLogged = true;
				Log("MenuBridge", "Could not inspect MSCLoader menu state: " + ex);
			}
			return false;
		}
	}

	private void FindModMenuButton()
	{
		Type menuType = typeof(Mod).Assembly.GetType("MSCLoader.ModMenu");
		if (menuType == null) return;
		FieldInfo instanceField = menuType.GetField("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		object instance = instanceField == null ? null : instanceField.GetValue(null);
		if (instance == null) return;
		FieldInfo uiField = menuType.GetField("UI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		GameObject ui = uiField == null ? null : uiField.GetValue(instance) as GameObject;
		if (ui == null) return;
		Component[] components = ui.GetComponentsInChildren<Component>(true);
		for (int i = 0; i < components.Length; i++)
		{
			Component component = components[i];
			if (component == null || !string.Equals(component.GetType().FullName, "MSCLoader.ModMenuButton", StringComparison.Ordinal)) continue;
			FieldInfo openedField = component.GetType().GetField("opened", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (openedField == null) continue;
			modMenuButton = component;
			modMenuOpenedField = openedField;
			Log("MenuBridge", "Cached MSCLoader menu state component.");
			return;
		}
	}

	private bool SetSuppression(bool suppressed, string reason)
	{
		if (!suppressionInitialized || suppressed != lastSuppressed || !string.Equals(reason, lastSuppressionReason, StringComparison.Ordinal))
		{
			suppressionInitialized = true;
			lastSuppressed = suppressed;
			lastSuppressionReason = reason;
			Log("MenuVisibility", "suppressed=" + suppressed + " reason=" + reason);
		}
		return lastSuppressed;
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

	private void Log(string area, string value)
	{
		if (logger != null) try { logger(area, value); } catch { }
	}
}
}

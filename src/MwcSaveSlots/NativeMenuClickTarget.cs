using System;
using UnityEngine;

namespace MwcSaveSlots
{
internal sealed class NativeMenuClickTarget : MonoBehaviour
{
	private Action clicked;
	private Transform visual;
	private Vector3 restingScale;
	private Vector3 targetScale;
	private bool blocked;
	private bool pointerInside;
	private bool pressed;

	internal bool IsBlocked { get { return blocked; } }

	internal void Configure(Action clicked, Transform visual)
	{
		this.clicked = clicked;
		this.visual = visual == null ? transform : visual;
		restingScale = this.visual.localScale;
		targetScale = restingScale;
	}

	internal void SetBlocked(bool value)
	{
		blocked = value;
		if (blocked)
		{
			pointerInside = false;
			pressed = false;
			targetScale = restingScale;
		}
	}

	private void OnMouseEnter()
	{
		if (blocked) return;
		pointerInside = true;
		RefreshTargetScale();
	}

	private void OnMouseExit()
	{
		pointerInside = false;
		pressed = false;
		RefreshTargetScale();
	}

	private void OnMouseDown()
	{
		if (blocked) return;
		pressed = true;
		RefreshTargetScale();
	}

	private void OnMouseUp()
	{
		pressed = false;
		RefreshTargetScale();
	}

	private void OnMouseUpAsButton()
	{
		if (blocked) return;
		UiSoundPlayer.PlayClick();
		if (clicked != null) clicked();
	}

	private void Update()
	{
		if (visual == null) return;
		float response = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 16f);
		visual.localScale = Vector3.Lerp(visual.localScale, targetScale, response);
	}

	private void RefreshTargetScale()
	{
		targetScale = restingScale * (pressed ? .96f : pointerInside && !blocked ? 1.06f : 1f);
	}

	private void OnDisable()
	{
		pointerInside = false;
		pressed = false;
		targetScale = restingScale;
		if (visual != null) visual.localScale = restingScale;
	}
}
}

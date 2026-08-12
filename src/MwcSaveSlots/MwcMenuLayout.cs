using System;

namespace MwcSaveSlots
{
internal static class MwcMenuLayout
{
	internal const float FrameWidth = 1320f;
	internal const float FrameHeight = 730f;
	internal const float CardWidth = 394f;
	internal const float CardHeight = 540f;
	internal const float CardSpacing = 416f;
	internal const float CardY = -5f;
	internal const float MenuButtonOffsetX = -384f;
	internal const float MenuButtonOffsetY = -172f;
	internal const float MenuButtonWidth = 220f;
	internal const float MenuButtonHeight = 58f;

	internal static float CardX(int zeroBasedIndex)
	{
		if (zeroBasedIndex < 0 || zeroBasedIndex >= ProfileRepository.ProfileCount)
		{
			throw new ArgumentOutOfRangeException("zeroBasedIndex");
		}
		return (zeroBasedIndex - 1) * CardSpacing;
	}

	internal static float PreviousMenuCoordinate(float current, float next)
	{
		return current + (current - next);
	}
}
}

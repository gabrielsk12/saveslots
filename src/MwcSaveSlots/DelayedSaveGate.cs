using System;

namespace MwcSaveSlots
{
internal enum DelayedSaveState
{
	Idle,
	Waiting,
	Ready,
	Expired
}

internal sealed class DelayedSaveGate
{
	private bool pending;
	private DateTime dueUtc;
	private DateTime deadlineUtc;

	internal void Schedule(DateTime nowUtc)
	{
		pending = true;
		dueUtc = nowUtc.AddSeconds(1d);
		deadlineUtc = nowUtc.AddSeconds(15d);
	}

	internal DelayedSaveState Poll(DateTime nowUtc, bool playableSaveExists)
	{
		if (!pending) return DelayedSaveState.Idle;
		if (nowUtc < dueUtc) return DelayedSaveState.Waiting;
		if (playableSaveExists)
		{
			pending = false;
			return DelayedSaveState.Ready;
		}
		if (nowUtc >= deadlineUtc)
		{
			pending = false;
			return DelayedSaveState.Expired;
		}
		dueUtc = nowUtc.AddMilliseconds(500d);
		return DelayedSaveState.Waiting;
	}
}
}

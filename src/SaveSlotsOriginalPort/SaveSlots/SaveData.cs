using System;

namespace SaveSlots
{
public class SaveData
{
	public string slotName;

	public DateTime lastPlayed;

	public SaveData()
	{
		slotName = "Save1";
		lastPlayed = new DateTime(1970, 1, 1);
	}

	public SaveData(string slotName, DateTime lastPlayed)
	{
		this.slotName = slotName;
		this.lastPlayed = lastPlayed;
	}
}
}


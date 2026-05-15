using System;
using System.Xml.Serialization;

namespace SaveSlotsMWC.Core
{
    public sealed class SaveSlotMetadata
    {
        [XmlElement("slotName")]
        public string SlotName { get; set; }

        [XmlElement("lastPlayed")]
        public DateTime LastPlayed { get; set; }

        public SaveSlotMetadata()
        {
            SlotName = "Save1";
            LastPlayed = new DateTime(1970, 1, 1);
        }

        public SaveSlotMetadata(string slotName, DateTime lastPlayed)
        {
            SlotName = slotName;
            LastPlayed = lastPlayed;
        }
    }
}

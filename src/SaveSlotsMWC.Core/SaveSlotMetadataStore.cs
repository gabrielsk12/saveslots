using System;
using System.IO;
using System.Xml.Serialization;

namespace SaveSlotsMWC.Core
{
    public static class SaveSlotMetadataStore
    {
        public static SaveSlotMetadata Load(string path)
        {
            if (!File.Exists(path))
            {
                return new SaveSlotMetadata();
            }

            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var serializer = new XmlSerializer(typeof(SaveSlotMetadata));
                    return (SaveSlotMetadata)serializer.Deserialize(stream);
                }
            }
            catch (InvalidOperationException)
            {
                var text = File.ReadAllText(path).Trim();
                return string.IsNullOrEmpty(text)
                    ? new SaveSlotMetadata()
                    : new SaveSlotMetadata(text, new DateTime(1970, 1, 1));
            }
        }

        public static void Save(string path, SaveSlotMetadata metadata)
        {
            var parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            using (var stream = File.Create(path))
            {
                var serializer = new XmlSerializer(typeof(SaveSlotMetadata));
                serializer.Serialize(stream, metadata ?? new SaveSlotMetadata());
            }
        }
    }
}

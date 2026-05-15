using System;
using System.IO;

namespace SaveSlotsMWC.Core
{
    public sealed class SaveSlotManager
    {
        public const string MetadataFileName = "SaveSlots.xml";
        public const string SaveSlotsFolderName = "SaveSlots";
        public const string OptionsFolderName = "Options";
        public const string BackupFolderName = "SAVE SLOTS BACKUP";

        private readonly string activeSavePath;
        private readonly string saveRoot;

        public string SaveSlotsPath { get { return Path.Combine(saveRoot, SaveSlotsFolderName); } }
        public string OptionsPath { get { return Path.Combine(SaveSlotsPath, OptionsFolderName); } }
        public string BackupPath { get { return Path.Combine(saveRoot, BackupFolderName); } }

        public SaveSlotManager(string activeSavePath, string saveRoot)
        {
            this.activeSavePath = Path.GetFullPath(activeSavePath);
            this.saveRoot = Path.GetFullPath(saveRoot);
        }

        public string GetCurrentSlotName()
        {
            return SaveSlotMetadataStore.Load(Path.Combine(activeSavePath, MetadataFileName)).SlotName;
        }

        public SaveSlotSwitchResult SwitchToSlot(string slotName, SaveSlotOptions options)
        {
            if (string.IsNullOrEmpty(slotName) || slotName.Trim().Length == 0)
            {
                throw new ArgumentException("Slot name is required.", nameof(slotName));
            }

            options = options ?? new SaveSlotOptions();
            EnsureLayout();

            var currentSlotName = GetCurrentSlotName();
            if (string.Equals(currentSlotName, slotName, StringComparison.OrdinalIgnoreCase))
            {
                return new SaveSlotSwitchResult { ContinueAvailable = HasContinueFile(activeSavePath) };
            }

            var currentSlotPath = Path.Combine(SaveSlotsPath, currentSlotName);
            Directory.CreateDirectory(currentSlotPath);
            CopyActiveSaveToSlot(currentSlotPath, options.CopyEditorBackups);

            var targetSlotPath = Path.Combine(SaveSlotsPath, slotName);
            MoveActiveSaveToBackup();
            Directory.CreateDirectory(activeSavePath);

            var targetExists = Directory.Exists(targetSlotPath);
            if (targetExists)
            {
                CopyDirectoryContents(targetSlotPath, activeSavePath, options.CopyEditorBackups);
                if (options.SynchronizeOptions)
                {
                    CopyDirectoryContents(OptionsPath, activeSavePath, true);
                }
            }
            else
            {
                CopyDirectoryContents(OptionsPath, activeSavePath, true);
            }

            SaveSlotMetadataStore.Save(
                Path.Combine(activeSavePath, MetadataFileName),
                new SaveSlotMetadata(slotName, DateTime.Now));

            return new SaveSlotSwitchResult { ContinueAvailable = targetExists && HasContinueFile(activeSavePath) };
        }

        private void EnsureLayout()
        {
            Directory.CreateDirectory(saveRoot);
            Directory.CreateDirectory(activeSavePath);
            Directory.CreateDirectory(SaveSlotsPath);
            Directory.CreateDirectory(OptionsPath);
        }

        private void CopyActiveSaveToSlot(string slotPath, bool copyEditorBackups)
        {
            foreach (var file in new DirectoryInfo(activeSavePath).GetFiles())
            {
                if (!ShouldCopy(file.Name, copyEditorBackups))
                {
                    continue;
                }

                Unlock(file.FullName);
                if (IsSharedOptionsFile(file.Name))
                {
                    file.CopyTo(Path.Combine(OptionsPath, file.Name), true);
                }
                else
                {
                    file.CopyTo(Path.Combine(slotPath, file.Name), true);
                }
            }

            foreach (var directory in new DirectoryInfo(activeSavePath).GetDirectories())
            {
                CopyDirectory(directory.FullName, Path.Combine(slotPath, directory.Name), copyEditorBackups);
            }
        }

        private void MoveActiveSaveToBackup()
        {
            if (Directory.Exists(BackupPath))
            {
                Directory.Delete(BackupPath, true);
            }

            Directory.Move(activeSavePath, BackupPath);
        }

        private static void CopyDirectoryContents(string sourcePath, string destinationPath, bool copyEditorBackups)
        {
            if (!Directory.Exists(sourcePath))
            {
                return;
            }

            foreach (var file in new DirectoryInfo(sourcePath).GetFiles())
            {
                if (!ShouldCopy(file.Name, copyEditorBackups))
                {
                    continue;
                }

                Unlock(file.FullName);
                Directory.CreateDirectory(destinationPath);
                file.CopyTo(Path.Combine(destinationPath, file.Name), true);
            }

            foreach (var directory in new DirectoryInfo(sourcePath).GetDirectories())
            {
                CopyDirectory(directory.FullName, Path.Combine(destinationPath, directory.Name), copyEditorBackups);
            }
        }

        private static void CopyDirectory(string sourcePath, string destinationPath, bool copyEditorBackups)
        {
            Directory.CreateDirectory(destinationPath);

            foreach (var file in new DirectoryInfo(sourcePath).GetFiles())
            {
                if (!ShouldCopy(file.Name, copyEditorBackups))
                {
                    continue;
                }

                Unlock(file.FullName);
                file.CopyTo(Path.Combine(destinationPath, file.Name), true);
            }

            foreach (var directory in new DirectoryInfo(sourcePath).GetDirectories())
            {
                CopyDirectory(directory.FullName, Path.Combine(destinationPath, directory.Name), copyEditorBackups);
            }
        }

        private static bool ShouldCopy(string fileName, bool copyEditorBackups)
        {
            return copyEditorBackups || fileName.IndexOf("_backup", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool IsSharedOptionsFile(string fileName)
        {
            return string.Equals(fileName, "options.txt", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "calibrator.cfg", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasContinueFile(string path)
        {
            return File.Exists(Path.Combine(path, "savefile.txt"));
        }

        private static void Unlock(string path)
        {
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
        }
    }
}

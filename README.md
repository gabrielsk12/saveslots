[![CodeFactor](https://www.codefactor.io/repository/github/gabrielsk12/saveslots/badge)](https://www.codefactor.io/repository/github/gabrielsk12/saveslots)

# Save Slots for My Winter Car

Save Slots lets you use three different My Winter Car save profiles without manually copying, renaming, or moving save files.

## Features

- Three save profiles: Save1, Save2, and Save3.
- A `SAVES` button on the My Winter Car main menu.
- Screenshots and basic player information for every occupied slot.
- Empty slots start a new game.
- Existing saves are imported into Save1 on first installation.
- Automatic backups before profile changes.
- A recovery backup is created before deleting a save.
- Shared or separate game options for each profile.

## Install

1. Download `SaveSlots.dll` from the [latest GitHub release](https://github.com/gabrielsk12/saveslots/releases/latest) or Nexus Mods.
2. Copy `SaveSlots.dll` into the My Winter Car `Mods` folder.
3. Start the game and select `SAVES` from the main menu.

Do not install version 3 and version 4 at the same time. When upgrading, replace the old DLL but keep the `SaveSlotsMWC` folder.

## Save locations

Profiles and backups are stored under:

```text
AppData\LocalLow\Amistech\SaveSlotsMWC
```

Deleted-save recovery copies are stored under:

```text
AppData\LocalLow\Amistech\SaveSlotsMWC\EmergencyBackups\DeletedProfiles
```

## Version 4.0

Version 4.0 rebuilds the mod for My Winter Car, improves the save screen, and strengthens switching, backup, deletion, and recovery behaviour. Existing Save1-Save3 profiles and settings remain compatible.

See [CHANGELOG.md](CHANGELOG.md) for the player-facing changes and [ORIGINALITY_REPORT.md](ORIGINALITY_REPORT.md) for the detailed rewrite and asset audit.

## Support

If you find a bug or console error, create a [GitHub issue](https://github.com/gabrielsk12/saveslots/issues) or contact `gabriel_sk` on Discord.

Made by Gabriel_SK.

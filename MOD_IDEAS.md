# Save Slots MWC - realistic future ideas

These are practical improvements, not promised features. Save compatibility and recovery safety stay more important than adding more controls.

## Good next additions

- Optional profile names stored in a small sidecar file, without editing the game's save data.
- A recovery screen that lists verified deleted-profile backups with date, size, slot, and thumbnail before restoring one.
- Keyboard and controller navigation for opening the menu, moving between cards, confirming, and closing.
- A thumbnail display option for `Fit` or `Fill`, useful for custom images with different aspect ratios.
- Clear on-screen safe-mode guidance with direct buttons for opening the log and backup folders.
- Localised Save Slots labels using small translation files, while keeping English as the fallback.

## Useful maintenance work

- A read-only backup integrity check that verifies manifests and critical save hashes from the settings page.
- An automatic compatibility smoke test for new MWC/MSCLoader versions that checks callbacks, menu hierarchy, and save paths without switching profiles.
- A support-report button that copies a privacy-safe status summary: mod version, selected slot, safe-mode state, file counts, and recent error categories.
- Better thumbnail timing after a save so the captured image reflects the moment the game finished writing.

## Later, only with strong testing

- Export and import one profile as a verified archive for moving saves between computers.
- Optional extra profiles beyond Save1-Save3, while leaving the original three folders fully compatible.
- A transactional backup restore wizard with preview, verification, rollback, and no automatic source deletion.

## Ideas to avoid

- Cloud sync inside the mod: it adds account, network, conflict, and privacy risks that do not belong in a save-slot utility.
- Automatic cleanup of unknown or legacy folders: recovery storage should favour keeping data.
- Editing gameplay values from the save-card screen: Save Slots should manage profiles, not become a save editor.

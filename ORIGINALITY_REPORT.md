# Save Slots MWC 4.0 Originality and Nexus Review Report

## References and preservation

The old MWC v3 DLL remains an immutable behavioral and storage-compatibility reference:

- Path: `C:\Users\Administrator\Desktop\saveslots OLD\SaveSlots.dll`
- Version: `3.0.0.0`
- SHA-256: `64A948742B6FA1FA97580FDCA5129AFF094CEE81837B228073258B63E868CD0A`

The user-supplied MSC Save Slots 1.1 archive was used only for exclusion auditing:

- Archive: `Save Slots 1.1 (for MSCLoader)-707-1-1-1644862523.zip`
- Archive SHA-256: `FF46A96C464159A42A0B8EEB03216BC8E82E7B3DB5C55C37FF2CC636D3EBBAD7`
- Contained DLL SHA-256: `0C33AADC683CDA55F8829C45740409ADB5F1EDBA7CC7403B16933D75306CE6D8`
- Nexus author: Athlon / Athlon007

Neither reference DLL nor the archive is modified or included in the release.

## Legacy bundle finding and remediation

A Unity object-level comparison found that the old MWC v3 `saveslots` bundle was not sufficiently independent from MSC 1.1. The MSC bundle contained 242 serialized objects and the MWC v3 bundle 226; 25 raw serialized objects matched exactly. Exact named matches included both FugazOne fonts, font textures/materials, the dummy texture/sprite, shutter audio, and serialized script references such as `SlotsManager`, `SlotBehaviour`, `ButtonSaves`, and `ResizeOnHover`. Twenty-one GameObject names were also shared.

Because the MSC Nexus page currently forbids asset reuse, modification, and conversion to another game, the complete legacy UI bundle was removed. None of those objects is compiled or packaged in v4.

## Independent v4 UI and assets

The v4 profile canvas is created at runtime by `SaveSlotsMenuView`, `UiPrimitives`, `UiPanelAnimator`, `MenuButtonMotion`, `CardHoverMotion`, `ProfileCardView`, and `ShutterTransition`. The main-menu `SAVES` entry clones the presentation of MWC's installed Continue button at runtime, removes its original click listeners and game logic, and attaches the Save Slots action. No game menu object or texture is embedded.

- UI geometry, deep navy/cold-blue panels, ice-white borders, cyan/orange state colors, cards, and animation timing are code-defined.
- A supplied monochrome image is used when a profile has no screenshot; a procedural image remains as an emergency decoder fallback only.
- The transition uses the camera sound supplied by the uploader from Pixabay creator `irinairinafomicheva`, played at 16% source volume. A deterministic procedural transition sound remains only as a decoder fallback.
- Buttons use the separate UI click supplied by the uploader from Pixabay creator `DenielCZ`, played at 14% source volume with a short repeat guard.
- FugazOne is requested from My Winter Car's already loaded game resources. No font bytes are embedded.
- No Unity asset bundle or prefab is loaded by v4.
- Former MSC implementation type names and serialized script references are rejected by the binary audit.

The embedded-resource allowlist contains two images and two credited audio files:

- Manifest name: `MwcSaveSlots.logo.png`
- Size: 2,111,107 bytes
- SHA-256: `E940EB397C88D3E366288C66B57B2D0E5EDEE3C2D6051B59A267D885209D20D0`
- It differs from both the MSC 1.1 logo and the MWC v3 floppy logo.
- It was square-formatted with OpenAI image assistance so MSCLoader does not distort its aspect ratio.

- Manifest name: `MwcSaveSlots.fallback-thumbnail.png`
- Size: 287,253 bytes
- SHA-256: `B473BF07C78242FF22F8320D44D16CBA91693F6FD77F38A411C73E91F75F0462`
- It was supplied by the uploader for saves without screenshots and differs from the audited MSC/MWC reference resources.
- The uploader must retain evidence that they own this image or have permission to distribute it.

- Manifest name: `MwcSaveSlots.transition-camera.wav`
- Size: 17,950 bytes
- SHA-256: `0A63859FE51750083889EA84DFA05525CFFAD20D4977C7EE178552FE630AAE86`
- Source MP3 SHA-256: `2CFB785EB275971C91C25E9775B1A6EA728BE16296FEEF821541C9DCD99CF77E`
- Creator: `irinairinafomicheva`
- Source: `https://pixabay.com/sound-effects/technology-camera-13695/`
- License summary: `https://pixabay.com/service/license-summary/`
- It was converted to 22.05 kHz, 16-bit mono PCM because MWC's Unity 5 runtime does not decode the supplied MP3 reliably. It is not extracted from either audited Save Slots reference and is credited even though attribution is not required by the Pixabay Content License.

- Manifest name: `MwcSaveSlots.ui-button-click.wav`
- Size: 1,678 bytes
- SHA-256: `49B6FC5C7F1ED45029D55D03FFCA131DB9F378D0365A51B9A5657D59AA89440C`
- Source MP3 SHA-256: `8D81CBFE9A05B30DA6730F03C1976E59143CB425F8FA4D9E4129DF6F65643B7A`
- Creator: `DenielCZ`
- Source: `https://pixabay.com/sound-effects/immersivecontrol-button-click-sound-463065/`
- License summary: `https://pixabay.com/service/license-summary/`
- It was converted to 22.05 kHz, 16-bit mono PCM for reliable Unity 5 playback. It is distinct from the transition sound and both audited Save Slots resource sets.

## AI disclosure

The compact-disc icon was square-formatted with OpenAI image assistance and the v4 code was developed with Codex assistance. The Nexus page must keep this disclosure so its authorship claims remain accurate under the general File Submission Guidelines. This report and `NEXUS_DESCRIPTION.txt` state that provenance directly.

This release must not be submitted or tagged for the August-September 2026 `Nexus Mods Turns 25` event. The event rules prohibit generative-AI code and assets. General file rules: `https://help.nexusmods.com/article/28-file-submission-guidelines`. Event rules: `https://help.nexusmods.com/article/175-nexus-mods-25th-anniversary-mod-drive-guidelines`.

## Independent architecture

The new source namespace is `MwcSaveSlots`. Responsibilities are separated into a thin MSCLoader entrypoint, profile coordinator, repository, snapshot transaction service, metadata reader, thumbnail/screenshot services, menu bridge, runtime views/animations, persistent pending-save receipt, delayed-save gate, and diagnostic writer.

The former type names `SlotsManager`, `SlotBehaviour`, `CustomExtensions`, `ButtonSaves`, `DeleteSaveButton`, `LoadingBehaviour`, `ModPrompt`, `ModSave`, `ResizeOnHover`, and the former root `SaveSlots.SaveSlots` type are absent.

## Compatibility retained by design

Public identity and user data remain compatible: `SaveSlots.dll`, ID `SaveSlotsMWC`, name `SAVE SLOTS`, author `Gabriel_SK`, Save1-Save3 folders, settings IDs, options, backups, staging, markers, XML metadata, manifests, and PNG/JPG thumbnails. Legacy data migration is verified copy-only and never deletes the source.

## Verification evidence

- Final v4 `SaveSlots.dll` SHA-256: `04501B0B4C5DE36C9510E1E8B961DED87A18E52ABDC1FF98F4A523215A2411A8`.
- Release build targets installed MWC build 23268598, MSCLoader 1.4.2.410, Unity UI, ES2, and Unity Mono 2.0.5 framework assemblies with warnings treated as errors.
- Twenty-four automated scenarios cover first-install profile visibility, MWC first/last-name formatting, distinct card geometry, native menu-entry spacing, nested data, option modes, backup filtering, verified deletion backups, failed deletion restoration, active-delete rollback, per-slot retention, migration, delayed capture, pending-save recovery across the menu reload, pruning, interrupted recovery, and injected failures across transaction stages.
- The binary audit requires the two-image/two-audio resource allowlist, expected MWC identity/dependencies, independent v4 types, absence of former symbols/prefab names, exact media hashes, different MWC/MSC reference hashes, and exactly five approved package files.
- Live verification in MWC at 1920x1080 confirmed the native `Interface/Buttons/ButtonContinue` clone and click target, the smaller 1320x730 frame, distinct three-card layout, full first-and-last names, unclipped last-played dates and footer guidance, visible delete `X`, divider-free empty-slot instructions, Fugaz font resolution, ModLoader-menu suppression, loading-screen suppression, and undistorted square icon. Real save cycles created PNG thumbnails before the gameplay-to-menu reload, then completed the pending profile snapshot after reload. A tested deletion created its verified slot-specific recovery backup before removal. The final WAV build logged successful UI-click loading at volume `0.14` and transition-audio loading at volume `0.16`. Ultrawide layout remains a release-checklist item rather than a claim in this report.

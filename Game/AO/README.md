# AO

VR rhythm action project built with Unity for Meta Quest 3.

AO is a healing underwater rhythm game where the player protects oxygen by hitting bubble notes and gently stroking fish notes in time with the music. The core interaction uses a D-Variant structure: the user's controller motion is transformed into the character's hand targets in front of the player, so rhythm judgement happens through the character rather than directly at the controller.

## Portfolio Snapshot

This folder is a public portfolio snapshot based on Plastic SCM `/main@cs:36` (`2026-06-23`, "최종 제출본").

Included:

- Gameplay, rhythm, judgement, UI, audio, state, character, and editor C# scripts
- Beatmap JSON samples and beatmap generation tools
- Main scene YAML snapshots for Title, Lobby, Gameplay, and Result
- ScriptableObject settings and Unity package metadata
- Links to the final presentation PDF and gameplay preview

Excluded:

- Unity `Library`, `Logs`, `Temp`, `obj`, and generated build folders
- Full build binaries and APK/EXE packages
- Original audio files, imported art/model packs, VRM model binaries, and other large or license-sensitive runtime assets

Because runtime media assets are intentionally excluded, this snapshot is not intended to be cloned as a complete playable Unity project. It is meant to show implementation structure, contribution scope, and project evidence without turning the repository into a heavy binary archive.

## Links

- [Planning document](AO%20기획서.pdf)
- [Final presentation PDF](AO_최종ppt.pdf)
- [Gameplay preview video](../assets/AO_Play-Preview.mp4)

## Main Implementation Areas

- `Source/Assets/_Project/Scripts/Rhythm`: DSP-time based song timing, beatmap loading, note spawning, and lane layout
- `Source/Assets/_Project/Scripts/Notes`: bubble and fish note behaviour
- `Source/Assets/_Project/Scripts/Judgement`: hand tracking, judgement hand targets, debug pads, and hit range visualization
- `Source/Assets/_Project/Scripts/State`: oxygen, combo, score, fever, and run statistics
- `Source/Assets/_Project/Scripts/Character`: D-Variant rider rig, visual hand targets, upper-body behaviour, and character helpers
- `Source/Assets/_Project/Scripts/UI`: lobby, HUD, result, settings, and controller UI interaction
- `Source/Assets/_Project/Scripts/Editor`: Unity editor setup and validation utilities
- `Source/Tools/Beatmap`: beatmap extraction, preview/audio preparation helpers, and catalog validation

## Environment

- Unity: `6000.3.14f1`
- Render pipeline: URP `17.3.0`
- XR target: Meta Quest 3 / OpenXR
- Main input: Quest Touch Plus controllers through XR Interaction Toolkit and OpenXR

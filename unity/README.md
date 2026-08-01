# 🦄 Ellies Enhörningsspel — Unity Port

A Unity port of the JS game in `public/`, living side by side with it.
The gameplay logic is a **1:1 port of `public/js/game.js` + `config.js`** —
every tuning value (spawn curves, speeds, challenge timings) is identical,
because the game logic runs in the same "480-pixel-wide" coordinate space
as the web version.

## What's special about this port

- **Zero assets required.** Everything is generated from code:
  - Sprites are drawn procedurally into textures (`SpriteFactory.cs`)
  - All sound effects and music (including the party EDM tune) are
    synthesized into AudioClips at startup (`SynthAudio.cs`)
  - The scene, camera, HUD and all objects are built at runtime
    (`GameManager.cs` bootstraps itself via `RuntimeInitializeOnLoadMethod`)
- **No scene setup needed.** Open the project, press **Play** in any empty
  scene. That's it.

## Getting started

1. Install **Unity Hub** and any **Unity 6** editor (the project was
   authored against `6000.0.x`; Unity will offer to open it with whatever
   6.x you have installed).
2. In Unity Hub: **Add → Add project from disk** → select
   `unity/UnicornGame`.
3. Open the project and press **Play**. The game boots into the menu.

The playfield is a fixed **480×854 portrait field** that is letterboxed to
fit any window, so proportions are correct whatever aspect the Game view is
set to. For the nicest fit (no side bars) pick a portrait aspect such as
**9:16** in the Game view dropdown.

## Controls

| Action | Input |
|---|---|
| Pick difficulty | `B` = Barn, `V` = Vuxen (or tap left/right half of screen) |
| Move | Arrow keys / `A` `D` / drag with finger or mouse |
| Sound on/off | `M` |
| Quit round | `Esc` |
| Play again | `B` / `V` on the game-over screen |

## What's ported

- Both difficulties with identical curves (lives, speeds, spawn rates)
- Streak → multiplier system, rounds with perfect bonuses, level themes
- All 11 challenge rounds via the same shuffle-bag: rainbow, giant stars,
  double spawn, gold rush, party ×3 (with EDM music), **mirror controls**,
  **meteor-storm survival**, **golden crown chase**, **blackout**
- Power-ups with +5s stacking: magnet, slow-mo (Vuxen only), rainbow trail
- Synth audio: all SFX + 4 looping tunes, ported note-for-note
- Local top-10 per difficulty (PlayerPrefs)
- Global toplist submission to the same Express/Postgres backend

## Connecting to your server

Open `Assets/Scripts/ScoreApi.cs` and set:

```csharp
public const string ServerBase = "http://<your-unraid-ip>:3000";
```

Scores then post to the same `/api/scores` endpoint the web game uses,
so Unity players and web players share one global toplist. Leave it empty
to play fully offline.

## Building for WebGL (host it next to the JS game)

1. **File → Build Profiles** → select **Web** → Switch Platform
2. Build to a folder, e.g. `webgl-build/`
3. Copy the build output into `public/unity/` on the server and it will be
   served at `http://<server>:3000/unity/` alongside the original game.

## Swapping in real art

`SpriteFactory.cs` returns a `Sprite` per shape name ("star", "skull",
"crescent", "heart", "crown", "gift", "unicorn"...). To use real artwork,
import your sprites and change `SpriteFactory.Get()` to return them —
nothing else in the code needs to change. Kenney.nl has fitting CC0 packs.

## Project layout

```
unity/UnicornGame/
├── Assets/Scripts/
│   ├── Playfield.cs      ← fixed 480×854 portrait field + camera letterboxing
│   ├── GameConfig.cs     ← config.js port (difficulties, sprites, challenges, curves)
│   ├── GameManager.cs    ← game.js port (state machine, spawning, catches, challenges)
│   ├── SpriteFactory.cs  ← procedural, antialiased shape textures
│   ├── Background.cs     ← gradient sky per level, twinkling stars, hills
│   ├── SynthAudio.cs     ← audio.js port (synth SFX + tunes)
│   ├── FxPool.cs         ← pooled particles (bursts, sparkle tails, rainbow trail)
│   ├── Hud.cs            ← code-built HUD (self-measuring text + sprite bars)
│   └── ScoreApi.cs       ← api.js port (UnityWebRequest → Express backend)
├── Packages/manifest.json
└── ProjectSettings/ProjectVersion.txt
```

# Changelog — Ellies Enhörningsspel

## v1.0 — 2026-04-11

### Game mechanics

- **Unified difficulty system** — Barn and Vuxen now share the same core mechanics (bad sprites, no miss-life penalty). Difficulty is controlled by per-difficulty config values (speed, spawn rates, catch radius) rather than separate code paths.
- **Lives** — Barn starts with 3/3 lives; Vuxen starts with 1/3 lives (max 3).
- **No miss penalty** — missing a sprite costs nothing; only catching bad sprites has consequences.
- **Speed cap** — maximum speed capped at 70 % of original values (Barn 5.6, Vuxen 9.8) so the game stays playable at high levels.
- **No speed reset on bad catch** — catching a skull or moon no longer resets speed to base, preventing easy farming deep into a run.
- **Uniform points** — all collectable sprites award 5 points each for simplicity.

### Difficulty tuning

- **Barn level 12+ hard ramp** — skull and moon spawn rates rise sharply from level 12, with multi-spawn starting at level 12 and triple-spawn at level 18.
- **Two-phase spawn curves** — `skullChance` and `moonChance` use a gentle phase (levels 1–11) and an aggressive phase (levels 12+) on Barn; Vuxen always uses the steeper curve.
- **Moon penalty** increased to −15 points (was −8).
- **Vuxen extra-life chance** increased (base 0.020, floor 0.008).

### Bonus / Challenge rounds

- **Party mode** added — all sprites turn into birthday/celebration emojis (🎂🍰🎉🎊🥳🎈🎁🎀) with a rainbow glitter glow animation. Party EDM music (square-wave synth at 130 BPM) plays during the round.
- **Party weight 3/7** — party mode occupies 3 of 7 challenge slots (~43 %) so it appears frequently on both difficulties.
- **Challenge gap halved** — formula changed from `roundSize×7 + level×8` to `roundSize×4 + level×4`, meaning bonus rounds appear roughly twice as often.
- **Clock power-up removed**; replaced by party mode.
- **Slow-mo (⏱️)** kept for Vuxen only.
- **Bonus round frequency** now scales correctly with level (longer gaps at higher levels, minimum 8 s duration).

### Power-ups

- **Powerup stacking** — catching a power-up that is already active adds 5 s to the remaining time instead of resetting. Banner shows `+5s!`.
- **All power-up types can re-spawn** while active (previously suppressed).
- **Slow-mo** restricted to Vuxen difficulty via `onlyDiff` config flag.

### Audio

- **iOS audio fix** — unlock buffer now connects directly to `ac.destination` (not `masterGain`), resolving the silent-on-iPhone bug.
- **Party EDM music** — square-wave synth riff, loops correctly through level-ups without interruption.
- **Level-up music** — preserves party EDM during an active party round instead of switching back to birthday tune.
- **Sound toggle button** (🔊/🔇) always visible, persisted to `localStorage`.

### Visual effects

- **Star sparkle animation** — falling good stars play a subtle `starSparkle` keyframe (scale + rotate).
- **Sparkle tail** — falling stars emit coloured dot particles as a trailing effect (8 px dots every 30 ms).
- **Party glitter** — party-mode sprites cycle through a full rainbow `drop-shadow` animation at 0.5 s.
- **Moon evil glow** — 🌑 sprites cycle through dark red/purple `moonEvil` animation to signal danger.
- **Skull danger pulse** — 💀 sprites pulse with a red `skullDanger` glow animation.
- **Combo ring** — diameter now matches the actual catch radius (Barn 110 px, Vuxen 72 px) as an accurate hitbox guide.
- **Unicorn power-up states** — magnet (electric shake), slow-mo (purple pulse), rainbow (full rainbow spin glow).

### Bug fixes

- Fixed **"Alla" toplist tab** not rendering (stale `"global"` string reference).
- Fixed **multiplier on score pop** showing new multiplier instead of the one used to calculate the points.
- Fixed **speed slowdown at level 18+** — miss handler was subtracting 0.3 from speed each miss, causing a net slowdown when triple-spawn produced multiple misses per cycle.
- Fixed **position-change guard** — unicorn and combo-ring DOM writes are skipped when position has not changed (performance).

### Landing page & RSVP

- Header updated to **"Födelsedagskalas"** with rainbow shimmer.
- Party description updated with venue and activity details.
- **Mandatory phone number** field added to RSVP form (stored in database).
- Form hides after successful submission; calendar links and map shown.
- **Difficulty label** changed to "Jag vill spela som:".

### Infrastructure

- Node.js + Express backend with PostgreSQL (`pg`).
- Docker / Compose setup for Unraid deployment.
- `update.sh` script for pull-and-restart without a local git install.
- Admin page with guest list and CSV export (includes phone numbers).
- `ADMIN_KEY` env-var authentication for admin routes.

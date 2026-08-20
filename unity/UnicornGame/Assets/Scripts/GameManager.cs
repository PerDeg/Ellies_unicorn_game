using System.Collections.Generic;
using UnityEngine;

namespace UnicornGame {

// ══════════════════════════════════════
//  GAME MANAGER — port of public/js/game.js
//
//  Coordinate system: gameplay runs in "JS pixels" on the fixed
//  480×854 portrait playfield (identical numbers to the web game, so
//  every tuning value carries over 1:1). y is measured downward from
//  the top like in the DOM. See Playfield.cs for the world mapping.
// ══════════════════════════════════════
public class GameManager : MonoBehaviour {

    // ── Bootstrap: no scene setup needed, just press Play ──────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() {
        if (FindFirstObjectByType<GameManager>() != null) return;
        var go = new GameObject("UnicornGame");
        go.AddComponent<GameManager>();
    }

    enum State { Menu, Playing, GameOver }

    // ── Unity objects ──────────────────────────────────────────
    Camera cam;
    SynthAudio audioSys;
    Fx fx;
    Hud hud;
    Background bg;
    GameObject unicorn;
    SpriteRenderer unicornSr, ringSr, uniGlowSr;
    TrailRenderer uniTrail;
    Vector2Int lastScreen;

    // ── Game state (mirrors game.js) ───────────────────────────
    State state = State.Menu;
    DifficultyConfig cfg;
    bool isVuxen;
    readonly SeededRng rng = new SeededRng();

    int score, level, streak, maxStreak, multiplier, maxMulti;
    int misses, totalCaught, perfectRounds;
    int roundCaught, roundMissed, roundNum;
    float currentSpeed;
    float uxJs;                                   // unicorn x in JS px
    float spawnTimerMs;
    string playerName = "Ellie";

    ChallengeDef activeChallenge;
    readonly List<ChallengeDef> challengeBag = new List<ChallengeDef>();
    int nextChallengeAt;
    float challengeTimeLeft;
    int meteorHits;
    bool crownActive;

    readonly Dictionary<string, float> powerups = new Dictionary<string, float>(); // id → seconds left
    readonly List<Vector2> rainbowTrail = new List<Vector2>();                     // JS-px positions
    const int RainbowTrailLen = 22;
    float rainbowFxTimer;

    class Star {
        public GameObject go;
        public SpriteRenderer sr;
        public SpriteDef def;
        public SpriteKind kind;
        public PowerupDef pu;
        public float xJs, yJs, speed, trailEmit, glitterT;
        public float zigBaseX, zigPhase, zigAmp, zigFreq; public bool zig;
        public Transform glow;          // halo / danger aura
        public SpriteRenderer glowSr;
        public GameObject trailGo;      // detached on death so it can fade out
        public TrailRenderer trail;
        public float spin, worldSize;
    }
    readonly List<Star> stars = new List<Star>();
    readonly List<float> pendingSpawns = new List<float>(); // delayed extra-spawn timers (ms)

    // ── Coordinate helpers (fixed 480×854 field, see Playfield.cs) ──
    const float JsW = Playfield.JsW;
    const float JsH = Playfield.JsH;
    static Vector3 WorldFromJs(float xJs, float yJs) => Playfield.FromJs(xJs, yJs);

    void Awake() {
        // Camera (created if the scene doesn't have one)
        cam = Camera.main;
        if (cam == null) {
            var cgo = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = cgo.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0f, 0.12f);   // letterbox colour
        cam.transform.position = new Vector3(0, 0, -10);
        if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
        Playfield.FitCamera(cam);
        lastScreen = new Vector2Int(Screen.width, Screen.height);

        audioSys = gameObject.AddComponent<SynthAudio>();
        bg  = new GameObject("Background").AddComponent<Background>();
        fx  = new GameObject("Fx").AddComponent<Fx>();
        fx.Init(cam);
        hud = new GameObject("Hud").AddComponent<Hud>();
        hud.Init();

        // Unicorn + catch-radius ring
        unicorn = new GameObject("Unicorn");
        unicornSr = unicorn.AddComponent<SpriteRenderer>();
        unicornSr.sprite = SpriteFactory.Get("unicorn");
        unicornSr.color = Color.white;
        unicornSr.sortingOrder = 10;
        var ring = new GameObject("Ring");
        ring.transform.SetParent(unicorn.transform, false);
        ringSr = ring.AddComponent<SpriteRenderer>();
        ringSr.sprite = SpriteFactory.Get("circle");
        ringSr.color = new Color(1f, 1f, 1f, 0.09f);
        ringSr.sortingOrder = 5;

        // Soft aura around the unicorn
        var uglow = new GameObject("UniGlow");
        uglow.transform.SetParent(unicorn.transform, false);
        uniGlowSr = uglow.AddComponent<SpriteRenderer>();
        uniGlowSr.sprite = SpriteFactory.Get("glow");
        uniGlowSr.color = new Color(1f, 0.8f, 1f, 0.30f);
        uniGlowSr.sortingOrder = 6;
        uglow.transform.localScale = Vector3.one * 1.7f;

        // Ribbon trail that follows the unicorn as it runs
        var utrail = new GameObject("UniTrail");
        utrail.transform.SetParent(unicorn.transform, false);
        uniTrail = utrail.AddComponent<TrailRenderer>();
        uniTrail.material = Fx.SpriteMaterial(SpriteFactory.GetTexture("glow"));
        uniTrail.sortingOrder = 9;
        uniTrail.numCapVertices = 4;
        uniTrail.minVertexDistance = 0.02f;
        uniTrail.time = 0.35f;
        uniTrail.startWidth = 0.30f;
        uniTrail.endWidth = 0f;

        ShowMenu();
    }

    // ══════════════════════════════════════
    //  MENU / GAME OVER
    // ══════════════════════════════════════
    void ShowMenu() {
        state = State.Menu;
        hud.SetPlayingVisible(false);
        hud.SetMenu(
            "GRATTIS PÅ FÖDELSEDAGEN\nELLIE!\n\n" +
            "Fånga stjärnorna\noch samla poäng!\n\n" +
            "[B]  eller vänster halva  =  Barn\n" +
            "[V]  eller höger halva  =  Vuxen\n\n" +
            "Styr med piltangenter, A / D\neller dra fingret\n\n" +
            "[M] ljud på/av");
        unicorn.SetActive(false);
        bg.SetBlackout(false);
        bg.SetTheme(1);
        audioSys.StopMusic();
    }

    void ShowGameOver() {
        state = State.GameOver;
        unicorn.SetActive(false);
        hud.SetPlayingVisible(false);
        bg.SetBlackout(false);
        audioSys.StopMusic();
        audioSys.PlayGameOver();

        var top = Toplist.Add(cfg.Label.ToLower(), playerName, score, maxStreak);
        int rank = top.FindIndex(e => e.name == playerName && e.score == score) + 1;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("SPELET SLUT!");
        sb.AppendLine();
        sb.AppendLine($"{score} poäng  -  plats {rank} ({cfg.Label})");
        sb.AppendLine($"Fångade {totalCaught}   Bästa streak {maxStreak}");
        sb.AppendLine($"Högsta x{maxMulti}   Perfekta {perfectRounds}");
        sb.AppendLine();
        sb.AppendLine("- TOPPLISTA -");
        for (int i = 0; i < top.Count && i < 5; i++)
            sb.AppendLine($"{i + 1}.  {top[i].name}   {top[i].score}");
        sb.AppendLine();
        sb.AppendLine("[B] Barn      [V] Vuxen");
        hud.SetMenu(sb.ToString());

        if (ScoreApi.Enabled) {
            StartCoroutine(ScoreApi.Submit(new ScoreApi.ScorePayload {
                name = playerName, score = score, difficulty = cfg.Label.ToLower(),
                maxStreak = maxStreak, maxMulti = maxMulti,
                perfectRounds = perfectRounds, caught = totalCaught,
            }));
        }
    }

    // ══════════════════════════════════════
    //  START GAME — port of startGame()
    // ══════════════════════════════════════
    void StartGame(bool vuxen) {
        isVuxen = vuxen;
        cfg = vuxen ? Config.Vuxen : Config.Barn;

        foreach (var s in stars) ReleaseStar(s);
        stars.Clear();
        pendingSpawns.Clear();
        powerups.Clear();
        rainbowTrail.Clear();

        score = 0; level = 1; streak = 0; maxStreak = 0;
        multiplier = 1; maxMulti = 1;
        misses = cfg.StartMisses; totalCaught = 0; perfectRounds = 0;
        roundCaught = 0; roundMissed = 0; roundNum = 1;
        activeChallenge = null; challengeBag.Clear();
        nextChallengeAt = cfg.RoundSize * 2;   // first bonus arrives early
        meteorHits = 0; crownActive = false;
        currentSpeed = cfg.BaseSpeed;
        rng.Reset();
        spawnTimerMs = GetSpawnInterval();

        uxJs = JsW * 0.5f;
        unicorn.SetActive(true);
        float uniPx = vuxen ? 46f : 66f;
        unicorn.transform.localScale = Vector3.one * (uniPx * Playfield.PX);
        // ring is a child, so its world size = parent scale × this scale
        ringSr.transform.localScale = Vector3.one * (cfg.CatchRadius * 2f / uniPx);

        bg.SetBlackout(false);
        bg.SetTheme(1);
        hud.SetMenu("");
        hud.SetPlayingVisible(true);
        hud.Layout();
        hud.SetTop(0, 1, 0, 1);
        hud.SetHearts(misses, cfg.MaxMisses);
        hud.SetSpeed(0f);
        hud.SetRound(0, 0, GetRoundSize());
        hud.SetPowerups("");

        state = State.Playing;
        audioSys.PlayTune("birthday");
    }

    // ══════════════════════════════════════
    //  UPDATE LOOP
    // ══════════════════════════════════════
    void Update() {
        if (Input.GetKeyDown(KeyCode.M)) audioSys.ToggleSound();

        // Re-fit the letterbox when the window changes shape
        if (Screen.width != lastScreen.x || Screen.height != lastScreen.y) {
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            Playfield.FitCamera(cam);
            hud.Layout();
        }

        switch (state) {
            case State.Menu:
            case State.GameOver:
                MenuInput();
                break;
            case State.Playing:
                if (Input.GetKeyDown(KeyCode.Escape)) { EndGame(); return; }
                Step(Time.deltaTime);
                break;
        }
    }

    void MenuInput() {
        if (Input.GetKeyDown(KeyCode.B)) { StartGame(false); return; }
        if (Input.GetKeyDown(KeyCode.V)) { StartGame(true); return; }
        // touch / click: left half = Barn, right half = Vuxen
        if (Input.GetMouseButtonDown(0)) {
            StartGame(Input.mousePosition.x > Screen.width / 2f);
        }
    }

    void Step(float dt) {
        float dtMs = Mathf.Min(dt * 1000f, 50f);
        hud.Layout();

        // ── Unicorn movement (mirror round reverses input) ─────
        bool mirror = activeChallenge != null && activeChallenge.Mirror;
        float spd = 7f * (dtMs / 16f);                       // UNICORN_SPEED
        bool goL = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
        bool goR = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
        if (mirror) { var t = goL; goL = goR; goR = t; }
        if (goL) uxJs = Mathf.Max(36f, uxJs - spd);
        if (goR) uxJs = Mathf.Min(JsW - 36f, uxJs + spd);

        // touch / mouse drag: unicorn follows the pointer, mapped through
        // the letterbox so it lines up with the playfield, not the window
        if (Input.GetMouseButton(0)) {
            var wp = cam.ScreenToWorldPoint(Input.mousePosition);
            float px = (wp.x + Playfield.W * 0.5f) / Playfield.PX;   // → JS px
            if (mirror) px = JsW - px;
            uxJs = Mathf.Clamp(px, 36f, JsW - 36f);
        }

        float uyJs = JsH * 0.70f;
        unicorn.transform.position = WorldFromJs(uxJs, uyJs);
        unicornSr.flipX = mirror;

        // Trail + aura react to the active power-up
        Color aura;
        if (powerups.ContainsKey("rainbow")) aura = Color.HSVToRGB((Time.time * 1.2f) % 1f, 0.75f, 1f);
        else if (powerups.ContainsKey("magnet")) aura = new Color(0.4f, 0.91f, 0.98f);
        else if (powerups.ContainsKey("slowmo")) aura = new Color(0.77f, 0.71f, 0.99f);
        else aura = new Color(1f, 0.72f, 0.95f);
        bool boosted = powerups.Count > 0;
        uniTrail.startColor = new Color(aura.r, aura.g, aura.b, boosted ? 0.75f : 0.40f);
        uniTrail.endColor   = new Color(aura.r, aura.g, aura.b, 0f);
        uniTrail.startWidth = boosted ? 0.42f : 0.26f;
        float uniPulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
        uniGlowSr.color = new Color(aura.r, aura.g, aura.b,
            (boosted ? 0.34f : 0.20f) + 0.14f * uniPulse);

        // ── Powerup timers (stacking handled at catch) ─────────
        var expired = new List<string>();
        var keys = new List<string>(powerups.Keys);
        foreach (var k in keys) {
            powerups[k] -= dt;
            if (powerups[k] <= 0) expired.Add(k);
        }
        foreach (var k in expired) { powerups.Remove(k); if (k == "rainbow") rainbowTrail.Clear(); }
        UpdateUnicornTint();

        // rainbow trail bookkeeping + fx
        if (powerups.ContainsKey("rainbow")) {
            rainbowTrail.Add(new Vector2(uxJs, uyJs));
            if (rainbowTrail.Count > RainbowTrailLen) rainbowTrail.RemoveAt(0);
            rainbowFxTimer += dt;
            if (rainbowFxTimer > 0.03f) {
                rainbowFxTimer = 0;
                var col = Fx.RainbowColors[Random.Range(0, Fx.RainbowColors.Length)];
                fx.Sparkle(WorldFromJs(uxJs, uyJs), col, 2, 0.8f);
            }
        }

        // ── Challenge countdown ────────────────────────────────
        if (activeChallenge != null) {
            challengeTimeLeft -= dt;
            if (challengeTimeLeft <= 0) EndChallenge();
        }

        // ── Spawning ───────────────────────────────────────────
        spawnTimerMs -= dtMs;
        if (spawnTimerMs <= 0) {
            SpawnWave();
            spawnTimerMs = GetSpawnInterval();
        }
        for (int i = pendingSpawns.Count - 1; i >= 0; i--) {
            float remain = pendingSpawns[i] - dtMs;
            if (remain <= 0) {
                pendingSpawns.RemoveAt(i);
                SpawnOne();
            } else pendingSpawns[i] = remain;
        }

        // ── Move stars, detect catch/miss ──────────────────────
        float speedMult = powerups.ContainsKey("slowmo") ? 0.4f : 1.0f;
        float cr = cfg.CatchRadius + (activeChallenge != null && activeChallenge.SizeBoost > 0 ? 12f : 0f);
        float cr2 = cr * cr;

        for (int i = stars.Count - 1; i >= 0; i--) {
            var s = stars[i];
            s.yJs += s.speed * speedMult * (dtMs / 16f);

            if (s.zig) {
                s.zigPhase += dtMs * s.zigFreq;
                s.xJs = Mathf.Clamp(s.zigBaseX + Mathf.Sin(s.zigPhase) * s.zigAmp, 24f, JsW - 24f);
            }

            ApplyMagnet(s, dt);
            s.go.transform.position = WorldFromJs(s.xJs, s.yJs);

            s.glitterT += dt;

            if (s.kind == SpriteKind.Bad) {
                // Dangers: pulsing red aura, menacing wobble, smoke trail
                float pulse = 0.5f + 0.5f * Mathf.Sin(s.glitterT * (s.def.TakesLife ? 9f : 5f));
                s.glowSr.color = new Color(1f, 0.10f, 0.10f, 0.30f + 0.35f * pulse);
                s.glow.localScale = Vector3.one * (2.2f + 0.45f * pulse);
                s.go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(s.glitterT * 3f) * 9f);
                if (s.yJs > 0f) {
                    s.trailEmit += dtMs;
                    if (s.trailEmit > 110f) {
                        s.trailEmit = 0f;
                        fx.Smoke(s.go.transform.position, 1, 0.25f, s.worldSize * 0.75f);
                    }
                }
            } else {
                // Catchables: slow spin, breathing halo, occasional glint
                if (s.spin != 0f)
                    s.go.transform.rotation = Quaternion.Euler(0f, 0f, s.glitterT * s.spin);

                Color baseCol = s.def.Color;
                if (activeChallenge != null && activeChallenge.PartyMode) {
                    baseCol = Color.HSVToRGB((s.glitterT * 0.8f) % 1f, 0.65f, 1f);
                    if (!SpriteFactory.IsPreColoured(s.def.Shape)) s.sr.color = baseCol;
                }
                float breathe = 0.5f + 0.5f * Mathf.Sin(s.glitterT * 4f);
                s.glowSr.color = new Color(baseCol.r, baseCol.g, baseCol.b, 0.38f + 0.26f * breathe);
                s.glow.localScale = Vector3.one * (1.9f + 0.30f * breathe);
                if (s.trail != null) {
                    s.trail.startColor = new Color(baseCol.r, baseCol.g, baseCol.b, 0.70f);
                    s.trail.endColor   = new Color(baseCol.r, baseCol.g, baseCol.b, 0f);
                }
                if (s.yJs > 0f) {
                    s.trailEmit += dtMs;
                    if (s.trailEmit > 150f) {
                        s.trailEmit = 0f;
                        fx.Sparkle(s.go.transform.position, baseCol, 1, 0.5f);
                    }
                }
            }

            float dx = s.xJs - uxJs, dy = s.yJs - uyJs;
            bool caught = dx * dx + dy * dy < cr2 || RainbowCatches(s);
            if (caught) {
                if (s.def.Crown) crownActive = false;
                Vector3 pos = s.go.transform.position;
                ReleaseStar(s); stars.RemoveAt(i);
                switch (s.kind) {
                    case SpriteKind.Bad:     OnBadCatch(s.def, pos); break;
                    case SpriteKind.Life:    OnLifeCatch(pos); break;
                    case SpriteKind.Powerup: ActivatePowerup(s.pu); fx.Confetti(pos, s.pu.Color, 16, 2.6f); fx.Sparkle(pos, s.pu.Color, 14, 1.8f); break;
                    default:                 OnCatch(s.def, pos); break;
                }
                if (state != State.Playing) return; // game may have ended
            } else if (s.yJs > JsH + 50f) {
                ReleaseStar(s); stars.RemoveAt(i);
                if (s.def.Crown) crownActive = false;      // escaped crown ≠ round miss
                else if (s.kind == SpriteKind.Good) OnMiss(s.xJs);
            }
        }

        hud.SetTop(score, level, streak, multiplier);
        hud.SetSpeed((currentSpeed - cfg.BaseSpeed) / (cfg.MaxSpeed - cfg.BaseSpeed));

        // active power-up readout
        if (powerups.Count == 0) hud.SetPowerups("");
        else {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in powerups) {
                if (sb.Length > 0) sb.Append("     ");
                sb.Append(Config.PowerupLabel(kv.Key)).Append(' ')
                  .Append(Mathf.CeilToInt(kv.Value)).Append('s');
            }
            hud.SetPowerups(sb.ToString());
        }
    }

    // ══════════════════════════════════════
    //  SPAWNING — port of spawnOneStar/spawnStar
    // ══════════════════════════════════════
    float GetSpawnInterval() => Mathf.Max(cfg.SpawnMin, cfg.SpawnBase - level * cfg.SpawnLevel);
    int GetRoundSize() => Mathf.Min(cfg.RoundSize + Mathf.FloorToInt((level - 1) * 1.5f), 25);
    int GetRoundBonus() => cfg.PerfBonus + Mathf.FloorToInt((level - 1) * 0.8f);

    (SpriteKind, SpriteDef) PickNormalSpawn() {
        float r = rng.Next();
        float lifeP = Config.LifeChance(isVuxen, totalCaught);
        float skulP = Config.SkullChance(isVuxen, level);
        float moonP = Config.MoonChance(isVuxen, level);
        if (r < lifeP) return (SpriteKind.Life, Config.LifeTypes[rng.Range(Config.LifeTypes.Length)]);
        if (r < lifeP + skulP) return (SpriteKind.Bad, Config.Skull);
        if (r < lifeP + skulP + moonP) return (SpriteKind.Bad, Config.Moon);
        return (SpriteKind.Good, rng.PickStarType());
    }

    void SpawnOne() {
        if (state != State.Playing) return;
        SpriteKind kind; SpriteDef def;

        if (activeChallenge != null && activeChallenge.MeteorMode) {
            kind = SpriteKind.Bad;
            def = rng.Next() < 0.45f ? Config.Skull : Config.Moon;
        } else if (activeChallenge != null && activeChallenge.Blackout) {
            (kind, def) = PickNormalSpawn();
        } else if (activeChallenge != null) {
            kind = SpriteKind.Good;
            if (activeChallenge.PartyMode)
                def = Config.PartyTypes[rng.Range(Config.PartyTypes.Length)];
            else if (activeChallenge.ForceType >= 0)
                def = Config.StarTypes[activeChallenge.ForceType];
            else
                def = rng.PickStarType();
        } else {
            (kind, def) = PickNormalSpawn();
        }

        float size = def.Size + (kind == SpriteKind.Good && activeChallenge != null ? activeChallenge.SizeBoost : 0f);
        float x = 28f + Random.value * (JsW - 60f);
        MakeStar(def, kind, x, size, currentSpeed + (Random.value * 0.3f - 0.15f), null);
    }

    void SpawnPowerup() {
        if (Random.value > Config.PowerupSpawnChance) return;
        var avail = new List<PowerupDef>();
        foreach (var p in Config.Powerups)
            if (!p.VuxenOnly || isVuxen) avail.Add(p);
        if (avail.Count == 0) return;
        var pu = avail[Random.Range(0, avail.Count)];
        float x = 28f + Random.value * (JsW - 60f);
        MakeStar(new SpriteDef(pu.Shape, 0, 46, pu.Color), SpriteKind.Powerup, x, 46f, currentSpeed * 0.7f, pu);
    }

    void SpawnCrown() {
        if (state != State.Playing || crownActive) return;
        crownActive = true;
        var def = isVuxen ? Config.CrownVuxen : Config.CrownBarn;
        float baseX = 100f + Random.value * (JsW - 200f);
        var s = MakeStar(def, SpriteKind.Good, baseX, def.Size, currentSpeed * (isVuxen ? 0.8f : 0.65f), null);
        s.zig = true;
        s.zigBaseX = baseX;
        s.zigPhase = Random.value * 6f;
        s.zigAmp = isVuxen ? 110f : 60f;
        s.zigFreq = isVuxen ? 0.005f : 0.0035f;
        s.sr.sortingOrder = 9;
    }

    Star MakeStar(SpriteDef def, SpriteKind kind, float xJs, float sizePx, float speed, PowerupDef pu) {
        var go = new GameObject("star");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Get(def.Shape);
        sr.color = SpriteFactory.IsPreColoured(def.Shape) ? Color.white : def.Color;
        sr.sortingOrder = 8;
        float world = sizePx * Playfield.PX;
        go.transform.localScale = Vector3.one * world;

        var s = new Star {
            go = go, sr = sr, def = def, kind = kind, pu = pu,
            xJs = xJs, yJs = -55f, speed = speed, worldSize = world,
            spin = kind == SpriteKind.Good ? Random.Range(-40f, 40f) : 0f,
        };

        bool danger = kind == SpriteKind.Bad;

        // ── Halo: catchables glow warm, dangers pulse red ──────
        var g = new GameObject("glow");
        g.transform.SetParent(go.transform, false);
        var gsr = g.AddComponent<SpriteRenderer>();
        gsr.sprite = SpriteFactory.Get("glow");
        gsr.sortingOrder = 7;
        Color halo = danger ? new Color(1f, 0.12f, 0.12f)
                            : (SpriteFactory.IsPreColoured(def.Shape) ? new Color(1f, 0.92f, 0.6f) : def.Color);
        gsr.color = new Color(halo.r, halo.g, halo.b, danger ? 0.42f : 0.5f);
        g.transform.localScale = Vector3.one * (danger ? 2.3f : 2.0f);
        s.glow = g.transform; s.glowSr = gsr;

        // ── Trail: bright ribbon for catchables, smoke for dangers ──
        var tgo = new GameObject("trail");
        tgo.transform.SetParent(go.transform, false);
        var tr = tgo.AddComponent<TrailRenderer>();
        tr.material = Fx.SpriteMaterial(SpriteFactory.GetTexture("glow"));
        tr.sortingOrder = 6;
        tr.numCapVertices = 4;
        tr.minVertexDistance = 0.02f;
        tr.time = danger ? 0.45f : 0.30f;
        tr.startWidth = world * (danger ? 0.85f : 0.55f);
        tr.endWidth = 0f;
        Color tc = danger ? new Color(0.25f, 0.10f, 0.28f) : halo;
        tr.startColor = new Color(tc.r, tc.g, tc.b, danger ? 0.55f : 0.70f);
        tr.endColor = new Color(tc.r, tc.g, tc.b, 0f);
        s.trailGo = tgo; s.trail = tr;

        go.transform.position = WorldFromJs(xJs, s.yJs);
        stars.Add(s);
        return s;
    }

    /// Cuts the trail loose so it fades out instead of vanishing with the sprite.
    void ReleaseStar(Star s) {
        if (s.trailGo != null) {
            s.trailGo.transform.SetParent(null, true);
            Destroy(s.trailGo, s.trail != null ? s.trail.time + 0.1f : 0.5f);
        }
        Destroy(s.go);
    }

    void SpawnWave() {
        SpawnOne();
        if (activeChallenge != null && activeChallenge.GoldenChase) SpawnCrown();

        if (activeChallenge != null && activeChallenge.DoubleSpawn) {
            pendingSpawns.Add(200f);
        } else if (activeChallenge != null && activeChallenge.PartyMode) {
            pendingSpawns.Add(200f);
            if (isVuxen || level >= 8) pendingSpawns.Add(380f);
        } else if (isVuxen) {
            pendingSpawns.Add(180f);
            if (level >= 4) pendingSpawns.Add(360f);
        } else if (level >= 12) {
            pendingSpawns.Add(220f);
            if (level >= 18) pendingSpawns.Add(440f);
        }
        SpawnPowerup();
    }

    // ══════════════════════════════════════
    //  CATCH HANDLERS — port of onCatch/onMiss/onBadCatch/onLifeCatch
    // ══════════════════════════════════════
    int GetMultiplier(int str) {
        var t = cfg.MultiThresh;
        for (int i = t.Length - 1; i >= 0; i--) if (str >= t[i]) return i + 1;
        return 1;
    }

    void OnCatch(SpriteDef def, Vector3 pos) {
        int pts = def.Pts * multiplier;
        score += pts;
        streak++; totalCaught++; roundCaught++;
        if (streak > maxStreak) maxStreak = streak;
        currentSpeed = Mathf.Min(currentSpeed + cfg.SpeedInc, cfg.MaxSpeed);

        int newM = GetMultiplier(streak);
        if (newM > multiplier) audioSys.PlayMultiUp();
        multiplier = newM;
        if (newM > maxMulti) maxMulti = newM;

        int nl = 1 + totalCaught / 10;
        if (nl > level) { level = nl; DoLevelUp(); }

        fx.Confetti(pos, def.Color, 14);
        fx.Sparkle(pos, def.Color, 8);
        if (def.Crown) {
            fx.Confetti(pos, new Color(1f, 0.84f, 0.2f), 30, 3.2f);
            fx.Sparkle(pos, Color.white, 20, 2.4f);
            fx.Flash(new Color(1f, 0.85f, 0.3f), 0.35f);
            audioSys.PlayWow();
            hud.ShowBanner($"GYLLENE STJÄRNAN! +{pts}p", 2.2f);
        }

        var mt = cfg.MultiThresh;
        if (streak == mt[1]) audioSys.PlayStreak();
        else if (streak == mt[2] || streak == mt[3] || streak == mt[4] || streak == mt[5]) audioSys.PlayWow();
        audioSys.PlayCatch(1f + streak * 0.006f);

        hud.SetRound(roundCaught, roundMissed, GetRoundSize());
        CheckRoundEnd();
        if (totalCaught >= nextChallengeAt && activeChallenge == null) ActivateChallenge();
    }

    void OnMiss(float xJs) {
        // Missing a sprite has no penalty — only catching bad sprites costs anything.
        fx.Smoke(WorldFromJs(xJs, JsH - 20f), 4, 0.5f, 0.20f);
        roundMissed++; totalCaught++;
        hud.SetRound(roundCaught, roundMissed, GetRoundSize());
        CheckRoundEnd();
        audioSys.PlayMiss();
    }

    void OnBadCatch(SpriteDef def, Vector3 pos) {
        if (activeChallenge != null && activeChallenge.MeteorMode) meteorHits++;
        streak = 0; multiplier = 1;

        if (def.TakesLife) {
            misses++;
            hud.SetHearts(misses, cfg.MaxMisses);
            audioSys.PlayBadCatch(true);
            hud.ShowBanner("DÖSKALLE!  -1 liv, streak bruten!", 2f);
            hud.FlashCentre("-1 LIV!", 1.2f);
            fx.Smoke(pos, 16, 1.6f, 0.42f);
            fx.Confetti(pos, new Color(0.9f, 0.15f, 0.2f), 10, 2.6f);
            fx.Shake(0.30f);
            fx.Flash(new Color(1f, 0f, 0.05f), 0.55f);
            if (misses >= cfg.MaxMisses) { EndGame(); return; }
        } else {
            audioSys.PlayBadCatch(false);
            score = Mathf.Max(0, score + def.Pts);
            hud.ShowBanner($"MÅNE  {def.Pts}p, streak bruten!", 1.4f);
            fx.Smoke(pos, 10, 1.2f, 0.34f);
            fx.Shake(0.16f);
            fx.Flash(new Color(0.8f, 0f, 0.35f), 0.3f);
        }
    }

    void OnLifeCatch(Vector3 pos) {
        if (misses > 0) { misses--; hud.SetHearts(misses, cfg.MaxMisses); }
        audioSys.PlayMultiUp();
        hud.ShowBanner("+1 LIV!", 1.5f);
        fx.Sparkle(pos, new Color(0.4f, 1f, 0.6f), 18, 1.8f);
        fx.Confetti(pos, new Color(0.4f, 1f, 0.6f), 10);
        fx.Flash(new Color(0.3f, 1f, 0.5f), 0.18f);
    }

    // ══════════════════════════════════════
    //  ROUNDS — port of checkRoundEnd()
    // ══════════════════════════════════════
    void CheckRoundEnd() {
        int rs = GetRoundSize();
        if (roundCaught + roundMissed < rs) return;
        if (roundMissed == 0) {
            perfectRounds++;
            int bonus = GetRoundBonus() * multiplier;
            score += bonus;
            audioSys.PlayPerfect();
            hud.ShowBanner($"PERFEKT omgång {roundNum}!  +{bonus}p", 2.8f);
            for (int i = 0; i < 4; i++) {
                var p = WorldFromJs(70f + Random.value * (JsW - 140f), JsH * (0.18f + Random.value * 0.22f));
                fx.Confetti(p, Fx.TrailColors[Random.Range(0, Fx.TrailColors.Length)], 22, 3f);
                fx.Sparkle(p, Color.white, 12, 2f);
            }
            fx.Flash(new Color(1f, 0.9f, 0.4f), 0.22f);
        } else {
            hud.ShowBanner($"Omgång {roundNum}: {roundCaught}/{rs}", 1.8f);
        }
        roundNum++; roundCaught = 0; roundMissed = 0;
        hud.SetRound(0, 0, GetRoundSize());
    }

    // ══════════════════════════════════════
    //  CHALLENGES — shuffle bag, same as game.js
    // ══════════════════════════════════════
    ChallengeDef NextChallenge() {
        if (challengeBag.Count == 0) {
            challengeBag.AddRange(Config.Challenges);
            for (int i = challengeBag.Count - 1; i > 0; i--) {
                int j = rng.Range(i + 1);
                (challengeBag[i], challengeBag[j]) = (challengeBag[j], challengeBag[i]);
            }
        }
        var c = challengeBag[challengeBag.Count - 1];
        challengeBag.RemoveAt(challengeBag.Count - 1);
        return c;
    }

    void ActivateChallenge() {
        var c = NextChallenge();
        activeChallenge = c;
        meteorHits = 0; crownActive = false;

        hud.ShowBanner("BONUS: " + c.Label, 3.2f);
        hud.FlashCentre("BONUSRUNDA!\n" + c.Label, 1.8f);
        audioSys.PlayWow();
        audioSys.PlayTune(c.Music);
        fx.Flash(c.MeteorMode ? new Color(1f, 0.2f, 0.2f) : new Color(0.7f, 0.5f, 1f), 0.3f);
        for (int i = 0; i < 6; i++)
            fx.Confetti(WorldFromJs(60f + Random.value * (JsW - 120f), JsH * 0.25f),
                        Fx.TrailColors[Random.Range(0, Fx.TrailColors.Length)], 12, 2.6f);
        if (c.Blackout) bg.SetBlackout(true);

        challengeTimeLeft = c.MeteorMode
            ? (isVuxen ? 10f : 8f)
            : Mathf.Max(18f - level * 0.2f, 8f);

        nextChallengeAt = totalCaught + cfg.RoundSize * 3 + level * 2 + Mathf.FloorToInt(rng.Next() * 10);
    }

    void EndChallenge() {
        bool survived = activeChallenge.MeteorMode && meteorHits == 0;
        bool wasBlackout = activeChallenge.Blackout;
        activeChallenge = null;
        if (wasBlackout) bg.SetBlackout(false);
        if (survived) {
            int bonus = (isVuxen ? 40 : 25) + level * 2;
            score += bonus;
            audioSys.PlayPerfect();
            hud.ShowBanner($"ÖVERLEVDE METEORREGNET! +{bonus}p", 2.6f);
            for (int i = 0; i < 3; i++)
                fx.Confetti(WorldFromJs(80f + Random.value * (JsW - 160f), JsH * 0.3f), Color.white, 20, 3f);
        }
        audioSys.PlayTune("birthday");
    }

    // ══════════════════════════════════════
    //  POWERUPS — +5s stacking, port of activatePowerup()
    // ══════════════════════════════════════
    void ActivatePowerup(PowerupDef pu) {
        if (powerups.ContainsKey(pu.Id)) {
            powerups[pu.Id] += Config.PowerupExtendSec;
            hud.ShowBanner(pu.Label + " +5s!", 1.6f);
        } else {
            powerups[pu.Id] = pu.DurSec;
            hud.ShowBanner(pu.Label, 1.6f);
        }
        audioSys.PlayMultiUp();
    }

    void UpdateUnicornTint() {
        if (powerups.ContainsKey("rainbow"))
            unicornSr.color = Color.HSVToRGB((Time.time * 1.5f) % 1f, 0.5f, 1f);
        else if (powerups.ContainsKey("magnet"))
            unicornSr.color = new Color(0.6f, 0.95f, 1f);
        else if (powerups.ContainsKey("slowmo"))
            unicornSr.color = new Color(0.8f, 0.72f, 1f);
        else
            unicornSr.color = Color.white;
    }

    void ApplyMagnet(Star s, float dt) {
        if (!powerups.ContainsKey("magnet") || s.kind != SpriteKind.Good) return;
        float uyJs = JsH * 0.70f;
        float dx = uxJs - s.xJs, dy = uyJs - s.yJs;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);
        if (dist < 320f && dist > 1f) {
            float force = 8f * (1f - dist / 320f) * (dt * 62.5f); // same px/frame as JS
            s.xJs += dx / dist * force;
            s.yJs += dy / dist * force;
        }
    }

    bool RainbowCatches(Star s) {
        if (!powerups.ContainsKey("rainbow") || rainbowTrail.Count == 0) return false;
        const float r2 = 40f * 40f;
        foreach (var p in rainbowTrail) {
            float dx = s.xJs - p.x, dy = s.yJs - p.y;
            if (dx * dx + dy * dy < r2) return true;
        }
        return false;
    }

    // ══════════════════════════════════════
    //  LEVEL UP / END
    // ══════════════════════════════════════
    void DoLevelUp() {
        audioSys.PlayLevelUp();
        bg.SetTheme(level);
        for (int i = 0; i < 10; i++)
            fx.Sparkle(WorldFromJs(Random.value * JsW, Random.value * JsH * 0.8f),
                       Fx.TrailColors[Random.Range(0, Fx.TrailColors.Length)], 2, 1.2f);
        hud.ShowBanner($"Nivå {level}!", 2f);
        // music keeps playing — party tune survives level-ups (AudioSource loops)
    }

    void EndGame() {
        if (state != State.Playing) return;
        foreach (var s in stars) ReleaseStar(s);
        stars.Clear();
        pendingSpawns.Clear();
        powerups.Clear();
        rainbowTrail.Clear();
        activeChallenge = null;
        ShowGameOver();
    }
}

// ══════════════════════════════════════
//  LOCAL TOPLIST — PlayerPrefs, mirrors the localStorage toplist
// ══════════════════════════════════════
public static class Toplist {
    [System.Serializable] public class Entry { public string name; public int score; public int maxStreak; }
    [System.Serializable] class Wrapper { public List<Entry> list = new List<Entry>(); }

    public static List<Entry> Load(string diff) {
        var json = PlayerPrefs.GetString("unicorn_top_" + diff, "");
        if (string.IsNullOrEmpty(json)) return new List<Entry>();
        try { return JsonUtility.FromJson<Wrapper>(json).list; }
        catch { return new List<Entry>(); }
    }

    public static List<Entry> Add(string diff, string name, int score, int maxStreak) {
        var l = Load(diff);
        l.Add(new Entry { name = name, score = score, maxStreak = maxStreak });
        l.Sort((a, b) => b.score.CompareTo(a.score));
        if (l.Count > 10) l.RemoveRange(10, l.Count - 10);
        PlayerPrefs.SetString("unicorn_top_" + diff, JsonUtility.ToJson(new Wrapper { list = l }));
        return l;
    }
}
}

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
    FxPool fx;
    Hud hud;
    Background bg;
    GameObject unicorn;
    SpriteRenderer unicornSr, ringSr;
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
        fx  = new GameObject("FxPool").AddComponent<FxPool>();
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

        foreach (var s in stars) Destroy(s.go);
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
                var col = FxPool.RainbowColors[Random.Range(0, FxPool.RainbowColors.Length)];
                fx.Spawn(WorldFromJs(uxJs, uyJs), col, 0.35f, 0.35f, Vector2.zero);
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

            // visual pulses: party glitter, skull danger, moon evil
            if (activeChallenge != null && activeChallenge.PartyMode && s.kind == SpriteKind.Good) {
                s.glitterT += dt * 6f;
                s.sr.color = Color.HSVToRGB(s.glitterT % 1f, 0.7f, 1f);
            } else if (s.kind == SpriteKind.Bad) {
                s.glitterT += dt * (s.def.TakesLife ? 8f : 4f);
                float pulse = (Mathf.Sin(s.glitterT * Mathf.PI) + 1f) / 2f;
                s.sr.color = s.def.TakesLife
                    ? Color.Lerp(s.def.Color, new Color(1f, 0.2f, 0.2f), pulse)
                    : Color.Lerp(s.def.Color, new Color(0.5f, 0.1f, 0.3f), pulse);
            }

            // sparkle tail for plain good stars
            if (s.kind == SpriteKind.Good && (activeChallenge == null || !activeChallenge.PartyMode) && s.yJs > 0) {
                s.trailEmit += dtMs;
                if (s.trailEmit > 60f) {
                    s.trailEmit = 0;
                    var col = FxPool.TrailColors[Random.Range(0, FxPool.TrailColors.Length)];
                    fx.Spawn(WorldFromJs(s.xJs, s.yJs), col, 0.10f, 0.4f, Vector2.zero);
                }
            }

            float dx = s.xJs - uxJs, dy = s.yJs - uyJs;
            bool caught = dx * dx + dy * dy < cr2 || RainbowCatches(s);
            if (caught) {
                if (s.def.Crown) crownActive = false;
                Vector3 pos = s.go.transform.position;
                Destroy(s.go); stars.RemoveAt(i);
                switch (s.kind) {
                    case SpriteKind.Bad:     OnBadCatch(s.def, pos); break;
                    case SpriteKind.Life:    OnLifeCatch(pos); break;
                    case SpriteKind.Powerup: ActivatePowerup(s.pu); fx.Burst(pos, 8); break;
                    default:                 OnCatch(s.def, pos); break;
                }
                if (state != State.Playing) return; // game may have ended
            } else if (s.yJs > JsH + 50f) {
                Destroy(s.go); stars.RemoveAt(i);
                if (s.def.Crown) crownActive = false;      // escaped crown ≠ round miss
                else if (s.kind == SpriteKind.Good) OnMiss();
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
        var s = MakeStar(new SpriteDef("bolt", 0, 40, pu.Color), SpriteKind.Powerup, x, 40f, currentSpeed * 0.7f, pu);
        s.sr.color = pu.Color;
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
        go.transform.localScale = Vector3.one * (sizePx * Playfield.PX);
        var s = new Star { go = go, sr = sr, def = def, kind = kind, pu = pu, xJs = xJs, yJs = -55f, speed = speed };
        go.transform.position = WorldFromJs(xJs, s.yJs);
        stars.Add(s);
        return s;
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

        fx.Burst(pos, 10);
        if (def.Crown) {
            fx.Burst(pos, 16, 0.18f);
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

    void OnMiss() {
        // Missing a sprite has no penalty — only catching bad sprites costs anything.
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
            fx.Burst(pos, 12, 0.16f);
            if (misses >= cfg.MaxMisses) { EndGame(); return; }
        } else {
            audioSys.PlayBadCatch(false);
            score = Mathf.Max(0, score + def.Pts);
            hud.ShowBanner($"MÅNE  {def.Pts}p, streak bruten!", 1.4f);
            fx.Burst(pos, 8, 0.14f);
        }
    }

    void OnLifeCatch(Vector3 pos) {
        if (misses > 0) { misses--; hud.SetHearts(misses, cfg.MaxMisses); }
        audioSys.PlayMultiUp();
        hud.ShowBanner("+1 LIV!", 1.5f);
        fx.Burst(pos, 10);
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
            for (int i = 0; i < 3; i++)
                fx.Burst(WorldFromJs(80f + Random.value * (JsW - 160f), JsH * 0.28f), 12, 0.16f);
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
        hud.ShowBanner($"Nivå {level}!", 2f);
        // music keeps playing — party tune survives level-ups (AudioSource loops)
    }

    void EndGame() {
        if (state != State.Playing) return;
        foreach (var s in stars) Destroy(s.go);
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

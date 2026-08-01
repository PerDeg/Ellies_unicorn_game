using UnityEngine;

namespace UnicornGame {

// ══════════════════════════════════════
//  CONFIG — 1:1 port of public/js/config.js
//  All sizes/speeds are in "JS pixels" on the fixed 480×854 playfield;
//  Playfield.cs converts those to world units.
// ══════════════════════════════════════

public enum SpriteKind { Good, Bad, Life, Powerup }

public class DifficultyConfig {
    public string Label;
    public int MaxMisses, StartMisses;
    public float BaseSpeed, SpeedInc, MaxSpeed;      // px per 16ms frame
    public float SpawnBase, SpawnMin, SpawnLevel;    // ms
    public int[] MultiThresh;
    public int RoundSize, PerfBonus;
    public float CatchRadius;                        // px
}

public class SpriteDef {
    public string Shape;       // key into SpriteFactory
    public int Pts;
    public float Size;         // px diameter
    public Color Color;
    public bool TakesLife, Crown;
    public SpriteDef(string shape, int pts, float size, Color color,
                     bool takesLife = false, bool crown = false) {
        Shape = shape; Pts = pts; Size = size; Color = color;
        TakesLife = takesLife; Crown = crown;
    }
}

public class PowerupDef {
    public string Id, Label;
    public float DurSec;
    public Color Color;
    public bool VuxenOnly;
    public PowerupDef(string id, string label, float durSec, Color color, bool vuxenOnly = false) {
        Id = id; Label = label; DurSec = durSec; Color = color; VuxenOnly = vuxenOnly;
    }
}

public class ChallengeDef {
    public string Id, Label, Music;
    public bool PartyMode, DoubleSpawn, Mirror, MeteorMode, GoldenChase, Blackout;
    public float SizeBoost;
    public int ForceType = -1;
}

public static class Config {

    public static readonly DifficultyConfig Barn = new DifficultyConfig {
        Label = "Barn", MaxMisses = 3, StartMisses = 0,
        BaseSpeed = 1.2f, SpeedInc = 0.08f, MaxSpeed = 5.6f,
        SpawnBase = 1200, SpawnMin = 550, SpawnLevel = 20,
        MultiThresh = new[] { 0, 8, 15, 25, 35, 50 },
        RoundSize = 8, PerfBonus = 3, CatchRadius = 55,
    };

    public static readonly DifficultyConfig Vuxen = new DifficultyConfig {
        Label = "Vuxen", MaxMisses = 3, StartMisses = 2,
        BaseSpeed = 2.5f, SpeedInc = 0.22f, MaxSpeed = 9.8f,
        SpawnBase = 1000, SpawnMin = 360, SpawnLevel = 14,
        MultiThresh = new[] { 0, 5, 10, 15, 20, 25 },
        RoundSize = 10, PerfBonus = 5, CatchRadius = 36,
    };

    // ── Sprite types ──────────────────────────────────────────
    public static readonly SpriteDef[] StarTypes = {
        new SpriteDef("star",   5, 38, new Color(1.00f, 0.88f, 0.40f)),
        new SpriteDef("star",   5, 36, new Color(0.75f, 0.90f, 1.00f)),
        new SpriteDef("circle", 5, 40, new Color(1.00f, 0.62f, 0.89f)),
        new SpriteDef("star",   5, 42, new Color(1.00f, 0.80f, 0.20f)),
        new SpriteDef("star",   5, 38, new Color(1.00f, 1.00f, 0.85f)),
    };
    public static readonly int[] StarWeights = { 40, 25, 18, 12, 5 };

    public static readonly SpriteDef Skull = new SpriteDef("skull", 0,   36, new Color(0.92f, 0.92f, 0.92f), takesLife: true);
    public static readonly SpriteDef Moon  = new SpriteDef("crescent", -15, 38, new Color(0.35f, 0.30f, 0.52f));

    public static readonly SpriteDef[] LifeTypes = {
        new SpriteDef("heart", 0, 34, new Color(0.42f, 0.93f, 0.60f)),
        new SpriteDef("heart", 0, 34, new Color(0.55f, 0.85f, 1.00f)),
        new SpriteDef("heart", 0, 34, new Color(1.00f, 0.55f, 0.75f)),
    };

    public static readonly PowerupDef[] Powerups = {
        new PowerupDef("magnet",  "Magnet!",   6f, new Color(0.40f, 0.91f, 0.98f)),
        new PowerupDef("slowmo",  "Slow-mo!",  5f, new Color(0.77f, 0.71f, 0.99f), vuxenOnly: true),
        new PowerupDef("rainbow", "Regnbåge!", 7f, new Color(1.00f, 0.62f, 0.89f)),
    };
    public const float PowerupSpawnChance = 0.05f;
    public const float PowerupExtendSec   = 5f;

    // Party-mode sprites — festive shapes, all 5 pts
    public static readonly SpriteDef[] PartyTypes = {
        new SpriteDef("gift",   5, 40, new Color(1.00f, 0.45f, 0.55f)),
        new SpriteDef("gift",   5, 38, new Color(0.55f, 0.75f, 1.00f)),
        new SpriteDef("circle", 5, 38, new Color(1.00f, 0.85f, 0.30f)),
        new SpriteDef("heart",  5, 38, new Color(1.00f, 0.50f, 0.80f)),
        new SpriteDef("star",   5, 40, new Color(0.60f, 1.00f, 0.60f)),
        new SpriteDef("circle", 5, 36, new Color(0.90f, 0.55f, 1.00f)),
        new SpriteDef("gift",   5, 38, new Color(0.45f, 0.95f, 0.85f)),
        new SpriteDef("heart",  5, 36, new Color(1.00f, 0.70f, 0.40f)),
    };

    // Golden-chase crown, per difficulty
    public static readonly SpriteDef CrownBarn  = new SpriteDef("crown", 30, 46, new Color(1f, 0.84f, 0.0f), crown: true);
    public static readonly SpriteDef CrownVuxen = new SpriteDef("crown", 50, 40, new Color(1f, 0.84f, 0.0f), crown: true);

    // ── Challenges (party weighted 3×, drawn via shuffle bag) ──
    public static readonly ChallengeDef[] Challenges = {
        new ChallengeDef { Id = "rainbow",  Label = "Regnbågsläge!",                Music = "birthday" },
        new ChallengeDef { Id = "giant",    Label = "Jättestjärnor!",               Music = "golden", SizeBoost = 20 },
        new ChallengeDef { Id = "double",   Label = "Dubbla stjärnor!",             Music = "challenge", DoubleSpawn = true },
        new ChallengeDef { Id = "golden",   Label = "Guldrusning!",                 Music = "golden", ForceType = 4 },
        new ChallengeDef { Id = "party",    Label = "Festläge!",                    Music = "party", PartyMode = true },
        new ChallengeDef { Id = "party",    Label = "Festläge!",                    Music = "party", PartyMode = true },
        new ChallengeDef { Id = "party",    Label = "Festläge!",                    Music = "party", PartyMode = true },
        new ChallengeDef { Id = "mirror",   Label = "Spegelläge - omvänd styrning!", Music = "challenge", Mirror = true },
        new ChallengeDef { Id = "meteor",   Label = "Meteorregn - undvik allt!",    Music = "challenge", MeteorMode = true },
        new ChallengeDef { Id = "crown",    Label = "Gyllene stjärnan!",            Music = "golden", GoldenChase = true },
        new ChallengeDef { Id = "blackout", Label = "Mörkerläge!",                  Music = "challenge", Blackout = true },
    };

    // Level themes live in Background.cs (gradient stops per level band).

    public static string PowerupLabel(string id) {
        foreach (var p in Powerups) if (p.Id == id) return p.Label;
        return id;
    }

    // ── Spawn rate curves — identical maths to config.js ──────
    public static float LifeChance(bool vuxen, int totalCaught) {
        if (vuxen) return Mathf.Max(0.020f - totalCaught * 0.0001f,  0.008f);
        return          Mathf.Max(0.015f - totalCaught * 0.00005f, 0.008f);
    }
    public static float SkullChance(bool vuxen, int level) {
        if (vuxen) return Mathf.Min(0.07f + level * 0.015f, 0.20f);
        if (level >= 12) return Mathf.Min(0.08f + (level - 12) * 0.015f, 0.22f);
        return 0.02f + level * 0.005f;
    }
    public static float MoonChance(bool vuxen, int level) {
        if (vuxen) return Mathf.Min(0.14f + level * 0.022f, 0.36f);
        if (level >= 12) return Mathf.Min(0.15f + (level - 12) * 0.020f, 0.35f);
        return 0.04f + level * 0.009f;
    }
}

// ── Seeded RNG — same LCG as config.js, deterministic per run ──
public class SeededRng {
    uint seed = 42;
    public void Reset() { seed = 42; }
    public float Next() {
        seed = seed * 1664525u + 1013904223u;
        return seed / (float)uint.MaxValue;
    }
    // Random index in [0, n) — guards the Next()==1.0 edge case
    public int Range(int n) {
        int i = (int)(Next() * n);
        return i >= n ? n - 1 : i;
    }
    public SpriteDef PickStarType() {
        float r = Next() * 100f;
        float cum = 0;
        for (int i = 0; i < Config.StarWeights.Length; i++) {
            cum += Config.StarWeights[i];
            if (r < cum) return Config.StarTypes[i];
        }
        return Config.StarTypes[0];
    }
}
}

using UnityEngine;

namespace UnicornGame {

// ══════════════════════════════════════
//  FX — particle systems, trails, screen shake and damage flash.
//
//  Effects carry meaning, not just polish:
//    catchables → bright confetti + sparkles
//    dangers    → dark smoke puffs + screen shake + red flash
//  so the player reads the board even in a crowded moment.
// ══════════════════════════════════════
public class Fx : MonoBehaviour {

    public static readonly Color[] TrailColors = {
        new Color(1f, 0.88f, 0.40f), new Color(1f, 0.62f, 0.89f), new Color(0.65f, 0.55f, 0.98f),
        new Color(0.40f, 0.91f, 0.98f), new Color(0.53f, 0.94f, 0.67f), new Color(0.99f, 0.83f, 0.30f),
    };
    public static readonly Color[] RainbowColors = {
        new Color(1f,0.27f,0.27f), new Color(1f,0.53f,0f), new Color(1f,0.93f,0f),
        new Color(0.27f,0.87f,0.27f), new Color(0.27f,0.67f,1f), new Color(0.67f,0.27f,1f),
        new Color(1f,0.27f,0.8f),
    };

    ParticleSystem confetti, sparkles, smoke, ambient;
    Camera cam;
    SpriteRenderer flash;
    float shake, flashLevel;
    Color flashColor = Color.red;

    static Material spriteMat;
    /// Shared alpha-blended material for particles and trails.
    public static Material SpriteMaterial(Texture2D tex) {
        if (spriteMat == null) {
            var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            spriteMat = new Material(sh);
        }
        var m = new Material(spriteMat) { mainTexture = tex };
        return m;
    }

    public void Init(Camera camera) {
        cam = camera;

        confetti = MakePS("confetti", "square", 24, gravity: 0.9f,  maxParticles: 400, grow: false);
        sparkles = MakePS("sparkles", "glow",   25, gravity: -0.1f, maxParticles: 400, grow: false);
        smoke    = MakePS("smoke",    "glow",   22, gravity: -0.25f, maxParticles: 300, grow: true);
        ambient  = MakePS("ambient",  "glow",   -60, gravity: -0.05f, maxParticles: 60, grow: false);

        // Slow drifting motes for atmosphere
        var em = ambient.emission;
        em.rateOverTime = 5f;
        var sh2 = ambient.shape;
        sh2.enabled = true;
        sh2.shapeType = ParticleSystemShapeType.Box;
        sh2.scale = new Vector3(Playfield.W, Playfield.H * 0.9f, 0.1f);
        var main = ambient.main;
        main.startLifetime = 6f;
        main.startSpeed = 0f;          // negative gravity does the drifting
        main.startSize = 0.06f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 1f, 1f, 0.30f), new Color(0.8f, 0.9f, 1f, 0.14f));

        // Full-field damage flash
        var fgo = new GameObject("flash");
        fgo.transform.SetParent(transform, false);
        flash = fgo.AddComponent<SpriteRenderer>();
        flash.sprite = SpriteFactory.Get("square");
        flash.color = new Color(1f, 0f, 0f, 0f);
        flash.sortingOrder = 60;
        fgo.transform.localScale = new Vector3(Playfield.W, Playfield.H, 1f);
    }

    ParticleSystem MakePS(string name, string shape, int order, float gravity, int maxParticles, bool grow) {
        var go = new GameObject("ps_" + name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;                    // velocity comes from EmitParams
        main.startLifetime = 0.7f;
        main.startSize = 0.12f;
        main.gravityModifier = gravity;
        main.maxParticles = maxParticles;
        main.playOnAwake = false;

        var em = ps.emission;
        em.rateOverTime = 0f;                    // manual Emit()

        var sh = ps.shape;
        sh.enabled = false;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.45f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, grow
            ? new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.8f))    // smoke expands
            : new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.15f)));   // sparks shrink

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.material = SpriteMaterial(SpriteFactory.GetTexture(shape));
        r.sortingOrder = order;
        r.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Play();
        return ps;
    }

    // ── Emitters ──────────────────────────────────────────────

    /// Bright confetti pop — catching something good.
    public void Confetti(Vector3 pos, Color tint, int count = 14, float power = 2.2f) {
        for (int i = 0; i < count; i++) {
            float ang = (i / (float)count) * Mathf.PI * 2f + Random.value * 0.4f;
            float spd = power * (0.55f + Random.value * 0.8f);
            var c = Random.value < 0.45f ? tint : TrailColors[Random.Range(0, TrailColors.Length)];
            confetti.Emit(new ParticleSystem.EmitParams {
                position = pos,
                velocity = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * spd,
                startColor = c,
                startSize = 0.07f + Random.value * 0.08f,
                startLifetime = 0.6f + Random.value * 0.4f,
                rotation = Random.value * 360f,
            }, 1);
        }
    }

    /// Soft glints — power-ups, lives, crown, ambient shimmer.
    public void Sparkle(Vector3 pos, Color tint, int count = 10, float power = 1.4f) {
        for (int i = 0; i < count; i++) {
            float ang = Random.value * Mathf.PI * 2f;
            float spd = power * (0.3f + Random.value * 0.9f);
            sparkles.Emit(new ParticleSystem.EmitParams {
                position = pos + (Vector3)(Random.insideUnitCircle * 0.08f),
                velocity = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * spd,
                startColor = tint,
                startSize = 0.10f + Random.value * 0.14f,
                startLifetime = 0.45f + Random.value * 0.45f,
            }, 1);
        }
    }

    /// Dark smoke — dangers, misses, meteor trails.
    public void Smoke(Vector3 pos, int count = 6, float spread = 0.6f, float size = 0.24f) {
        for (int i = 0; i < count; i++) {
            float ang = Random.value * Mathf.PI * 2f;
            smoke.Emit(new ParticleSystem.EmitParams {
                position = pos + (Vector3)(Random.insideUnitCircle * 0.1f),
                velocity = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * spread * Random.value,
                startColor = new Color(0.22f + Random.value * 0.12f, 0.12f, 0.26f, 0.75f),
                startSize = size * (0.7f + Random.value * 0.7f),
                startLifetime = 0.7f + Random.value * 0.6f,
            }, 1);
        }
    }

    public void Shake(float amount) => shake = Mathf.Max(shake, amount);

    public void Flash(Color c, float strength) {
        flashColor = c;
        flashLevel = Mathf.Max(flashLevel, strength);
    }

    void LateUpdate() {
        float dt = Time.deltaTime;

        if (shake > 0.0005f) {
            var off = Random.insideUnitCircle * shake;
            cam.transform.position = new Vector3(off.x, off.y, -10f);
            shake = Mathf.Max(0f, shake - dt * 1.4f);
            if (shake <= 0.0005f) cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        if (flashLevel > 0.001f) {
            flashLevel = Mathf.Max(0f, flashLevel - dt * 2.2f);
            flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashLevel);
        } else if (flash.color.a != 0f) {
            flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
        }
    }
}
}

using System.Collections.Generic;
using UnityEngine;

namespace UnicornGame {

// ══════════════════════════════════════
//  FX POOL — pooled circle sprites for particle bursts,
//  star sparkle tails and the rainbow trail.
//  (Port of the pooled tail-dot / burst system in game.js/effects.js)
// ══════════════════════════════════════
public class FxPool : MonoBehaviour {

    const int POOL = 96;

    class Dot {
        public GameObject go;
        public SpriteRenderer sr;
        public Vector2 vel;
        public float life, maxLife, startScale;
        public bool active;
    }

    readonly List<Dot> dots = new List<Dot>();
    int next;

    public static readonly Color[] TrailColors = {
        new Color(1f, 0.88f, 0.4f), new Color(1f, 0.62f, 0.89f), new Color(0.65f, 0.55f, 0.98f),
        new Color(0.4f, 0.91f, 0.98f), new Color(0.53f, 0.94f, 0.67f), new Color(0.99f, 0.83f, 0.30f),
    };
    public static readonly Color[] RainbowColors = {
        new Color(1f,0.27f,0.27f), new Color(1f,0.53f,0f), new Color(1f,0.93f,0f),
        new Color(0.27f,0.87f,0.27f), new Color(0.27f,0.67f,1f), new Color(0.67f,0.27f,1f),
        new Color(1f,0.27f,0.8f),
    };

    void Awake() {
        for (int i = 0; i < POOL; i++) {
            var go = new GameObject("fx");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Get("circle");
            sr.sortingOrder = 20;
            go.SetActive(false);
            dots.Add(new Dot { go = go, sr = sr });
        }
    }

    public void Spawn(Vector3 pos, Color color, float scale, float lifeSec, Vector2 vel) {
        var d = dots[next]; next = (next + 1) % POOL;
        d.go.SetActive(true);
        d.go.transform.position = pos;
        d.go.transform.localScale = Vector3.one * scale;
        d.sr.color = color;
        d.vel = vel;
        d.life = d.maxLife = lifeSec;
        d.startScale = scale;
        d.active = true;
    }

    public void Burst(Vector3 pos, int count, float scale = 0.12f) {
        for (int i = 0; i < count; i++) {
            float ang = (i / (float)count) * Mathf.PI * 2f;
            float spd = 1.2f + Random.value * 1.2f;
            var col = TrailColors[Random.Range(0, TrailColors.Length)];
            Spawn(pos, col, scale * (0.7f + Random.value * 0.6f), 0.4f,
                  new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd);
        }
    }

    void Update() {
        float dt = Time.deltaTime;
        foreach (var d in dots) {
            if (!d.active) continue;
            d.life -= dt;
            if (d.life <= 0) { d.active = false; d.go.SetActive(false); continue; }
            float frac = d.life / d.maxLife;
            d.go.transform.position += (Vector3)(d.vel * dt);
            d.go.transform.localScale = Vector3.one * (d.startScale * frac);
            var c = d.sr.color; c.a = frac; d.sr.color = c;
        }
    }
}
}

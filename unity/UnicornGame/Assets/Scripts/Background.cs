using System.Collections.Generic;
using UnityEngine;

namespace UnicornGame {

// ══════════════════════════════════════
//  BACKGROUND — gradient sky, twinkling stars and hill silhouettes,
//  filling exactly the 480×854 playfield (port of the web game's
//  CSS gradients + .bg-star + #hills).
// ══════════════════════════════════════
public class Background : MonoBehaviour {

    // Vertical gradient stops per theme (top → bottom), from LEVEL_THEMES
    static readonly Color[][] Themes = {
        new[] { Hex(0x0d1b6e), Hex(0x7c1fa8), Hex(0xd45fbd), Hex(0xffd6f0) }, // Natt
        new[] { Hex(0x1a0550), Hex(0xc2185b), Hex(0xff7043), Hex(0xffe0b2) }, // Gryning
        new[] { Hex(0x0277bd), Hex(0x29b6f6), Hex(0x81d4fa), Hex(0xe1f5fe) }, // Dag
        new[] { Hex(0x311b92), Hex(0xe65100), Hex(0xff8f00), Hex(0xffcc02) }, // Solnedgång
        new[] { Hex(0x000000), Hex(0x1a0550), Hex(0x2d1b69), Hex(0x3d1c8f) }, // Rymden
    };
    static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

    const int GRAD_H = 256;

    SpriteRenderer sky;
    Texture2D skyTex;
    readonly List<SpriteRenderer> bgStars = new List<SpriteRenderer>();
    readonly List<float> starPhase = new List<float>();
    readonly List<SpriteRenderer> hills = new List<SpriteRenderer>();
    readonly List<Color> hillBase = new List<Color>();

    int themeIdx = -1;
    float dim = 1f, dimTarget = 1f;

    void Awake() {
        float W = Playfield.W, H = Playfield.H;

        // ── Sky gradient quad ─────────────────────────────────
        skyTex = new Texture2D(4, GRAD_H, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        var skyGo = new GameObject("Sky");
        skyGo.transform.SetParent(transform, false);
        sky = skyGo.AddComponent<SpriteRenderer>();
        sky.sprite = Sprite.Create(skyTex, new Rect(0, 0, 4, GRAD_H), new Vector2(0.5f, 0.5f), 1f);
        sky.sortingOrder = -100;
        // sprite is 4 × GRAD_H units at ppu 1 → scale to the playfield
        skyGo.transform.localScale = new Vector3(W / 4f, H / GRAD_H, 1f);

        // ── Twinkling background stars (upper ~65%) ───────────
        for (int i = 0; i < 34; i++) {
            var go = new GameObject("bgstar");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Get("circle");
            sr.color = Color.white;
            sr.sortingOrder = -90;
            float sz = (1.5f + Random.value * 3f) * Playfield.PX;
            go.transform.localScale = Vector3.one * sz;
            go.transform.position = Playfield.FromJs(
                Random.value * Playfield.JsW,
                Random.value * Playfield.JsH * 0.65f);
            bgStars.Add(sr);
            starPhase.Add(Random.value * 6.28f);
        }

        // ── Hill silhouettes along the bottom ─────────────────
        AddHill(0.15f, 0.60f, 0.30f, new Color(0.42f, 0.13f, 0.66f, 0.60f));
        AddHill(0.50f, 0.80f, 0.36f, new Color(0.49f, 0.13f, 0.81f, 0.50f));
        AddHill(0.88f, 0.64f, 0.28f, new Color(0.35f, 0.11f, 0.53f, 0.60f));

        SetTheme(1);
    }

    void AddHill(float cxFrac, float wFrac, float hFrac, Color col) {
        var go = new GameObject("hill");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Get("circle");
        sr.color = col;
        sr.sortingOrder = -80;
        float w = Playfield.W * wFrac, h = Playfield.H * hFrac;
        go.transform.localScale = new Vector3(w, h, 1f);
        // anchor the ellipse so only its top bulge shows above the bottom edge
        go.transform.position = new Vector3(
            (cxFrac - 0.5f) * Playfield.W,
            -Playfield.H * 0.5f - h * 0.30f,
            0f);
        hills.Add(sr);
        hillBase.Add(col);
    }

    /// Applies the theme for a level (same thresholds as getTheme()).
    public void SetTheme(int level) {
        int idx = level <= 2 ? 0 : level <= 4 ? 1 : level <= 6 ? 2 : level <= 8 ? 3 : 4;
        if (idx == themeIdx) return;
        themeIdx = idx;
        Repaint();
    }

    void Repaint() {
        var stops = Themes[themeIdx];
        var px = new Color[4 * GRAD_H];
        for (int y = 0; y < GRAD_H; y++) {
            // texture y=0 is the bottom of the sprite → invert for top-down stops
            float t = 1f - (y / (float)(GRAD_H - 1));
            float scaled = t * (stops.Length - 1);
            int i = Mathf.Min((int)scaled, stops.Length - 2);
            Color c = Color.Lerp(stops[i], stops[i + 1], scaled - i);
            for (int x = 0; x < 4; x++) px[y * 4 + x] = c;
        }
        skyTex.SetPixels(px);
        skyTex.Apply();
    }

    /// Blackout challenge — fades the whole backdrop to near-black.
    public void SetBlackout(bool on) => dimTarget = on ? 0.10f : 1f;

    void Update() {
        dim = Mathf.MoveTowards(dim, dimTarget, Time.deltaTime * 3f);
        sky.color = new Color(dim, dim, dim, 1f);
        for (int i = 0; i < hills.Count; i++) {
            var b = hillBase[i];
            hills[i].color = new Color(b.r * dim, b.g * dim, b.b * dim, b.a);
        }
        for (int i = 0; i < bgStars.Count; i++) {
            float tw = 0.15f + 0.6f * (0.5f + 0.5f * Mathf.Sin(Time.time * 1.6f + starPhase[i]));
            bgStars[i].color = new Color(1f, 1f, 1f, tw * dim);
        }
    }
}
}

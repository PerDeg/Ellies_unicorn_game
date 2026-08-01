using System.Collections.Generic;
using UnityEngine;

namespace UnicornGame {

// ══════════════════════════════════════
//  SPRITE FACTORY
//  Draws every game shape procedurally into a 128×128 texture with 3×3
//  supersampled antialiasing — the project needs zero image assets.
//
//  Two kinds of shape:
//   • Tintable (star, circle, heart, crescent, gift, square) — drawn
//     white, coloured at runtime via SpriteRenderer.color.
//   • Pre-coloured (unicorn, skull, crown) — full colour baked in;
//     render them with a white tint to keep their colours.
// ══════════════════════════════════════
public static class SpriteFactory {

    const int SIZE = 128;
    const int SS   = 3;      // supersample grid per texel

    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    static readonly HashSet<string> preColoured = new HashSet<string> { "unicorn", "skull", "crown" };
    public static bool IsPreColoured(string shape) => preColoured.Contains(shape);

    public static Sprite Get(string shape) {
        if (cache.TryGetValue(shape, out var s)) return s;
        s = Build(shape);
        cache[shape] = s;
        return s;
    }

    static Sprite Build(string shape) {
        var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        var px = new Color[SIZE * SIZE];
        float inv = 1f / (SS * SS);

        for (int y = 0; y < SIZE; y++) {
            for (int x = 0; x < SIZE; x++) {
                // Supersample: average alpha, and average RGB weighted by alpha
                // so edges don't pick up dark fringes.
                float aSum = 0f;
                Vector3 rgbSum = Vector3.zero;
                for (int sy = 0; sy < SS; sy++) {
                    for (int sx = 0; sx < SS; sx++) {
                        float u = (x + (sx + 0.5f) / SS) / SIZE;
                        float v = (y + (sy + 0.5f) / SS) / SIZE;
                        Color c = Sample(shape, u, v);
                        aSum += c.a;
                        rgbSum += new Vector3(c.r, c.g, c.b) * c.a;
                    }
                }
                float a = aSum * inv;
                Vector3 rgb = aSum > 0.0001f ? rgbSum / aSum : Vector3.one;
                px[y * SIZE + x] = new Color(rgb.x, rgb.y, rgb.z, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        // pixelsPerUnit = SIZE → sprite is exactly 1 world unit wide at scale 1
        return Sprite.Create(tex, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), SIZE);
    }

    // ── Palette ───────────────────────────────────────────────
    static readonly Color BONE     = new Color(0.96f, 0.96f, 0.93f);
    static readonly Color SOCKET   = new Color(0.10f, 0.08f, 0.14f);
    static readonly Color GOLD     = new Color(1.00f, 0.84f, 0.20f);
    static readonly Color GOLD_DK  = new Color(0.85f, 0.62f, 0.08f);
    static readonly Color MANE_A   = new Color(1.00f, 0.55f, 0.85f);
    static readonly Color MANE_B   = new Color(0.66f, 0.55f, 0.99f);
    static readonly Color EYE      = new Color(0.15f, 0.10f, 0.22f);
    static readonly Color CLEAR    = new Color(1f, 1f, 1f, 0f);

    // Returns colour + coverage at uv (0..1, y up).
    static Color Sample(string shape, float u, float v) {
        switch (shape) {
            case "circle":   return Mono(Circle(u, v, 0.5f, 0.5f, 0.46f));
            case "square":   return Mono(Rect(u, v, 0.02f, 0.02f, 0.98f, 0.98f));
            case "star":     return Mono(Star(u, v));
            case "heart":    return Mono(Heart(u, v));
            case "crescent": return Mono(Mathf.Clamp01(Circle(u, v, 0.46f, 0.5f, 0.45f)
                                                     - Circle(u, v, 0.64f, 0.58f, 0.42f)));
            case "gift":     return Mono(Gift(u, v));
            case "bolt":     return Mono(InPoly(BOLT, u, v));
            case "skull":    return Skull(u, v);
            case "crown":    return Crown(u, v);
            case "unicorn":  return Unicorn(u, v);
            default:         return Mono(Circle(u, v, 0.5f, 0.5f, 0.46f));
        }
    }

    static Color Mono(float a) => new Color(1f, 1f, 1f, a);

    // ── Pre-coloured shapes ───────────────────────────────────

    static Color Unicorn(float u, float v) {
        // Facing left: muzzle bottom-left, horn up, mane flowing right.
        float eye = Circle(u, v, 0.37f, 0.50f, 0.038f);
        if (eye > 0) return new Color(EYE.r, EYE.g, EYE.b, eye);

        float nostril = Circle(u, v, 0.22f, 0.32f, 0.022f);
        if (nostril > 0) return new Color(EYE.r, EYE.g, EYE.b, nostril);

        float horn = InPoly(HORN, u, v);
        if (horn > 0) {
            // banded gold so the horn reads as a spiral
            float band = Mathf.Repeat((v - 0.68f) * 9f, 1f) < 0.5f ? 1f : 0f;
            Color c = Color.Lerp(GOLD, GOLD_DK, band);
            return new Color(c.r, c.g, c.b, horn);
        }

        float ear = InPoly(EAR, u, v);
        if (ear > 0) return new Color(1f, 0.97f, 0.99f, ear);

        float head   = Circle(u, v, 0.47f, 0.46f, 0.26f);
        float muzzle = Ellipse(u, v, 0.28f, 0.35f, 0.16f, 0.12f);
        float body   = Mathf.Max(head, muzzle);
        if (body > 0) return new Color(1f, 0.98f, 1f, body);

        // Mane behind the head, pink→purple down the neck
        float m1 = Circle(u, v, 0.68f, 0.62f, 0.17f);
        float m2 = Circle(u, v, 0.74f, 0.42f, 0.16f);
        float m3 = Circle(u, v, 0.71f, 0.22f, 0.14f);
        float mane = Mathf.Max(m1, Mathf.Max(m2, m3));
        if (mane > 0) return Lerp(MANE_A, MANE_B, 1f - v, mane);

        return CLEAR;
    }

    static Color Skull(float u, float v) {
        float cranium = Circle(u, v, 0.5f, 0.58f, 0.34f);
        float jaw     = RoundRect(u, v, 0.34f, 0.12f, 0.66f, 0.38f, 0.06f);
        float body    = Mathf.Max(cranium, jaw);
        if (body <= 0) return CLEAR;

        float socketL = Circle(u, v, 0.38f, 0.62f, 0.10f);
        float socketR = Circle(u, v, 0.62f, 0.62f, 0.10f);
        float nose    = InTri(u, v, 0.50f, 0.54f, 0.445f, 0.42f, 0.555f, 0.42f);
        float dark    = Mathf.Max(socketL, Mathf.Max(socketR, nose));
        // teeth gaps in the jaw
        float teeth = 0f;
        if (v > 0.13f && v < 0.26f) {
            float t = Mathf.Repeat((u - 0.36f) * 12f, 1f);
            if (t < 0.16f) teeth = 1f;
        }
        dark = Mathf.Max(dark, teeth * jaw);

        Color c = Color.Lerp(BONE, SOCKET, Mathf.Clamp01(dark));
        return new Color(c.r, c.g, c.b, body);
    }

    static Color Crown(float u, float v) {
        float shape = InPoly(CROWN, u, v);
        if (shape <= 0) return CLEAR;

        // jewels on the band
        float j1 = Circle(u, v, 0.28f, 0.26f, 0.055f);
        float j2 = Circle(u, v, 0.50f, 0.26f, 0.065f);
        float j3 = Circle(u, v, 0.72f, 0.26f, 0.055f);
        if (j1 > 0) return new Color(0.35f, 0.70f, 1.00f, j1 * shape);
        if (j2 > 0) return new Color(1.00f, 0.30f, 0.45f, j2 * shape);
        if (j3 > 0) return new Color(0.45f, 0.95f, 0.55f, j3 * shape);

        // band slightly darker than the points
        Color c = v < 0.36f ? GOLD_DK : Color.Lerp(GOLD, Color.white, Mathf.Clamp01((v - 0.5f) * 0.8f));
        return new Color(c.r, c.g, c.b, shape);
    }

    static Color Lerp(Color a, Color b, float t, float alpha) {
        Color c = Color.Lerp(a, b, Mathf.Clamp01(t));
        return new Color(c.r, c.g, c.b, alpha);
    }

    // ── Coverage primitives (return 0..1) ─────────────────────

    static float Circle(float u, float v, float cx, float cy, float r) {
        float d = Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy));
        return d <= r ? 1f : 0f;
    }

    static float Ellipse(float u, float v, float cx, float cy, float rx, float ry) {
        float dx = (u - cx) / rx, dy = (v - cy) / ry;
        return dx * dx + dy * dy <= 1f ? 1f : 0f;
    }

    static float Rect(float u, float v, float x0, float y0, float x1, float y1) =>
        (u >= x0 && u <= x1 && v >= y0 && v <= y1) ? 1f : 0f;

    static float RoundRect(float u, float v, float x0, float y0, float x1, float y1, float r) {
        float cx = Mathf.Clamp(u, x0 + r, x1 - r);
        float cy = Mathf.Clamp(v, y0 + r, y1 - r);
        float dx = u - cx, dy = v - cy;
        return (dx * dx + dy * dy) <= r * r ? 1f : 0f;
    }

    static float Heart(float u, float v) {
        float x = (u - 0.5f) * 3.1f;
        float y = (v - 0.40f) * 3.1f;
        float f = Mathf.Pow(x * x + y * y - 1f, 3f) - x * x * y * y * y;
        return f < 0f ? 1f : 0f;
    }

    static float Star(float u, float v) => InPoly(StarPoints(5, 0.48f, 0.20f), u, v);

    static float Gift(float u, float v) {
        float box = RoundRect(u, v, 0.16f, 0.10f, 0.84f, 0.68f, 0.04f);
        float lid = RoundRect(u, v, 0.10f, 0.68f, 0.90f, 0.84f, 0.04f);
        float a = Mathf.Max(box, lid);
        if (a <= 0) return 0f;
        // cut thin ribbon grooves so the shape reads as a present when tinted
        if (u > 0.465f && u < 0.535f && v < 0.68f) return 0f;
        if (v > 0.655f && v < 0.695f) return 0f;
        return a;
    }

    static float InTri(float px, float py, float ax, float ay, float bx, float by, float cx, float cy) {
        float d1 = Sign(px, py, ax, ay, bx, by);
        float d2 = Sign(px, py, bx, by, cx, cy);
        float d3 = Sign(px, py, cx, cy, ax, ay);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0;
        bool pos = d1 > 0 || d2 > 0 || d3 > 0;
        return (neg && pos) ? 0f : 1f;
    }
    static float Sign(float px, float py, float ax, float ay, float bx, float by) =>
        (px - bx) * (ay - by) - (ax - bx) * (py - by);

    static Vector2[] StarPoints(int points, float outerR, float innerR) {
        var pts = new Vector2[points * 2];
        for (int i = 0; i < points * 2; i++) {
            float r = (i % 2 == 0) ? outerR : innerR;
            float ang = Mathf.PI / 2f + i * Mathf.PI / points;
            pts[i] = new Vector2(0.5f + Mathf.Cos(ang) * r, 0.5f + Mathf.Sin(ang) * r);
        }
        return pts;
    }

    static readonly Vector2[] CROWN = {
        new Vector2(0.10f, 0.14f), new Vector2(0.90f, 0.14f), new Vector2(0.90f, 0.38f),
        new Vector2(0.82f, 0.86f), new Vector2(0.66f, 0.46f), new Vector2(0.50f, 0.94f),
        new Vector2(0.34f, 0.46f), new Vector2(0.18f, 0.86f), new Vector2(0.10f, 0.38f),
    };

    static readonly Vector2[] BOLT = {
        new Vector2(0.58f, 0.96f), new Vector2(0.26f, 0.46f), new Vector2(0.46f, 0.46f),
        new Vector2(0.38f, 0.04f), new Vector2(0.74f, 0.54f), new Vector2(0.54f, 0.54f),
    };

    static readonly Vector2[] HORN = {
        new Vector2(0.40f, 0.66f), new Vector2(0.31f, 1.00f), new Vector2(0.52f, 0.68f),
    };

    static readonly Vector2[] EAR = {
        new Vector2(0.55f, 0.64f), new Vector2(0.62f, 0.90f), new Vector2(0.69f, 0.62f),
    };

    static float InPoly(Vector2[] poly, float u, float v) {
        bool inside = false;
        int n = poly.Length;
        for (int i = 0, j = n - 1; i < n; j = i++) {
            if ((poly[i].y > v) != (poly[j].y > v) &&
                u < (poly[j].x - poly[i].x) * (v - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        }
        return inside ? 1f : 0f;
    }
}
}

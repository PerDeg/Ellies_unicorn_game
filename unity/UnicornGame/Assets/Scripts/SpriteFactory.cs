using System.Collections.Generic;
using UnityEngine;

namespace UnicornGame {

// ══════════════════════════════════════
//  SPRITE FACTORY
//  Every shape is drawn procedurally into a 160×160 texture with 3×3
//  supersampled antialiasing, then given a baked contour outline so it
//  reads clearly against any background.
//
//  VISUAL GRAMMAR — the player must tell categories apart at a glance:
//    ⭐ catch  → star family, warm/bright fill, dark outline, glow halo
//    💀 danger → skull / crescent, dark or bone fill, RED outline, smoke
//    💚 life   → heart, green-pink fill
//    ⚡ power  → distinct badge shape per power-up
// ══════════════════════════════════════
public static class SpriteFactory {

    const int SIZE = 160;
    const int SS   = 3;           // supersample grid per texel
    const int OUTLINE_PX = 7;     // contour thickness in texels

    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    // Shapes whose colours are baked in — render these with a white tint.
    static readonly HashSet<string> preColoured = new HashSet<string> {
        "unicorn", "skull", "crown", "magnet", "hourglass", "rainbowarc",
    };
    public static bool IsPreColoured(string shape) => preColoured.Contains(shape);

    // Shapes that get a contour. Utility shapes (glow, circle, square) must not.
    static readonly HashSet<string> outlined = new HashSet<string> {
        "star", "sparkle4", "heart", "crescent", "gift", "skull", "crown",
        "unicorn", "magnet", "hourglass", "rainbowarc",
    };

    static readonly Color OUTLINE_DARK   = new Color(0.06f, 0.02f, 0.14f);
    static readonly Color OUTLINE_DANGER = new Color(0.72f, 0.04f, 0.10f);

    static Color OutlineFor(string shape) =>
        (shape == "skull" || shape == "crescent") ? OUTLINE_DANGER : OUTLINE_DARK;

    public static Sprite Get(string shape) {
        if (cache.TryGetValue(shape, out var s)) return s;
        s = Build(shape);
        cache[shape] = s;
        return s;
    }

    public static Texture2D GetTexture(string shape) => Get(shape).texture;

    static Sprite Build(string shape) {
        var px = new Color[SIZE * SIZE];
        float inv = 1f / (SS * SS);

        // 1 ─ rasterise with supersampled AA
        for (int y = 0; y < SIZE; y++) {
            for (int x = 0; x < SIZE; x++) {
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
                // gentle top-lit shading for depth
                if (a > 0f && outlined.Contains(shape)) {
                    float shade = 0.86f + 0.26f * ((y + 0.5f) / SIZE);
                    rgb *= shade;
                }
                px[y * SIZE + x] = new Color(rgb.x, rgb.y, rgb.z, a);
            }
        }

        // 2 ─ bake a contour so the silhouette pops on any background
        if (outlined.Contains(shape)) px = AddOutline(px, OutlineFor(shape), OUTLINE_PX);

        var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), SIZE);
    }

    // Dilates the alpha mask outward and fills the new ring with the outline colour.
    static Color[] AddOutline(Color[] src, Color outline, int width) {
        var dst = new Color[src.Length];
        int w2 = width * width;
        for (int y = 0; y < SIZE; y++) {
            for (int x = 0; x < SIZE; x++) {
                int i = y * SIZE + x;
                float a = src[i].a;
                float expanded = a;
                if (a < 0.999f) {
                    for (int dy = -width; dy <= width && expanded < 0.999f; dy++) {
                        int yy = y + dy;
                        if (yy < 0 || yy >= SIZE) continue;
                        for (int dx = -width; dx <= width; dx++) {
                            if (dx * dx + dy * dy > w2) continue;
                            int xx = x + dx;
                            if (xx < 0 || xx >= SIZE) continue;
                            float na = src[yy * SIZE + xx].a;
                            if (na > expanded) { expanded = na; if (expanded >= 0.999f) break; }
                        }
                    }
                }
                float newA = Mathf.Max(a, expanded);
                if (newA <= 0.0001f) { dst[i] = new Color(1f, 1f, 1f, 0f); continue; }
                Color rgb = Color.Lerp(outline, new Color(src[i].r, src[i].g, src[i].b), a / newA);
                dst[i] = new Color(rgb.r, rgb.g, rgb.b, newA);
            }
        }
        return dst;
    }

    // ── Palette ───────────────────────────────────────────────
    static readonly Color BONE    = new Color(0.97f, 0.96f, 0.90f);
    static readonly Color SOCKET  = new Color(0.07f, 0.05f, 0.10f);
    static readonly Color GOLD    = new Color(1.00f, 0.84f, 0.20f);
    static readonly Color GOLD_DK = new Color(0.82f, 0.58f, 0.06f);
    static readonly Color MANE_A  = new Color(1.00f, 0.50f, 0.83f);
    static readonly Color MANE_B  = new Color(0.60f, 0.52f, 0.99f);
    static readonly Color EYE     = new Color(0.13f, 0.08f, 0.20f);
    static readonly Color VOID_A  = new Color(0.13f, 0.07f, 0.20f);
    static readonly Color VOID_B  = new Color(0.30f, 0.06f, 0.22f);
    static readonly Color CLEAR   = new Color(1f, 1f, 1f, 0f);

    static Color Sample(string shape, float u, float v) {
        switch (shape) {
            case "circle":     return Mono(Circle(u, v, 0.5f, 0.5f, 0.46f));
            case "square":     return Mono(Rect(u, v, 0.02f, 0.02f, 0.98f, 0.98f));
            case "glow":       return Glow(u, v);
            case "star":       return Mono(InPoly(StarPoints(5, 0.46f, 0.19f), u, v));
            case "sparkle4":   return Mono(Sparkle4(u, v));
            case "heart":      return Mono(Heart(u, v));
            case "crescent":   return Crescent(u, v);
            case "gift":       return Mono(Gift(u, v));
            case "skull":      return Skull(u, v);
            case "crown":      return Crown(u, v);
            case "unicorn":    return Unicorn(u, v);
            case "magnet":     return Magnet(u, v);
            case "hourglass":  return Hourglass(u, v);
            case "rainbowarc": return RainbowArc(u, v);
            default:           return Mono(Circle(u, v, 0.5f, 0.5f, 0.46f));
        }
    }

    static Color Mono(float a) => new Color(1f, 1f, 1f, a);

    // Soft radial falloff — halos, sparkles and smoke all use this.
    static Color Glow(float u, float v) {
        float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) / 0.5f;
        float a = Mathf.Clamp01(1f - d);
        return new Color(1f, 1f, 1f, a * a);
    }

    // ── Danger shapes ─────────────────────────────────────────

    // Dark crescent: a void with a faint magenta inner edge.
    static Color Crescent(float u, float v) {
        float a = Mathf.Clamp01(Circle(u, v, 0.46f, 0.5f, 0.45f) - Circle(u, v, 0.66f, 0.58f, 0.43f));
        if (a <= 0f) return CLEAR;
        float t = Mathf.Clamp01((u - 0.10f) / 0.55f);
        Color c = Color.Lerp(VOID_A, VOID_B, t);
        return new Color(c.r, c.g, c.b, a);
    }

    static Color Skull(float u, float v) {
        float cranium = Circle(u, v, 0.5f, 0.58f, 0.33f);
        float jaw     = RoundRect(u, v, 0.35f, 0.13f, 0.65f, 0.38f, 0.06f);
        float body    = Mathf.Max(cranium, jaw);
        if (body <= 0f) return CLEAR;

        float dark = Mathf.Max(Circle(u, v, 0.38f, 0.62f, 0.105f),
                      Mathf.Max(Circle(u, v, 0.62f, 0.62f, 0.105f),
                                InTri(u, v, 0.50f, 0.55f, 0.44f, 0.42f, 0.56f, 0.42f)));
        if (v > 0.14f && v < 0.27f && Mathf.Repeat((u - 0.36f) * 12f, 1f) < 0.18f)
            dark = Mathf.Max(dark, jaw);

        Color c = Color.Lerp(BONE, SOCKET, Mathf.Clamp01(dark));
        return new Color(c.r, c.g, c.b, body);
    }

    // ── Pre-coloured shapes ───────────────────────────────────

    static Color Unicorn(float u, float v) {
        float eye = Circle(u, v, 0.37f, 0.50f, 0.038f);
        if (eye > 0f) return new Color(EYE.r, EYE.g, EYE.b, eye);
        float nostril = Circle(u, v, 0.22f, 0.32f, 0.022f);
        if (nostril > 0f) return new Color(EYE.r, EYE.g, EYE.b, nostril);

        float horn = InPoly(HORN, u, v);
        if (horn > 0f) {
            float band = Mathf.Repeat((v - 0.68f) * 9f, 1f) < 0.5f ? 1f : 0f;
            Color c = Color.Lerp(GOLD, GOLD_DK, band);
            return new Color(c.r, c.g, c.b, horn);
        }
        float ear = InPoly(EAR, u, v);
        if (ear > 0f) return new Color(1f, 0.96f, 0.99f, ear);

        float body = Mathf.Max(Circle(u, v, 0.47f, 0.46f, 0.26f),
                               Ellipse(u, v, 0.28f, 0.35f, 0.16f, 0.12f));
        if (body > 0f) return new Color(1f, 0.98f, 1f, body);

        float mane = Mathf.Max(Circle(u, v, 0.68f, 0.62f, 0.17f),
                      Mathf.Max(Circle(u, v, 0.74f, 0.42f, 0.16f),
                                Circle(u, v, 0.71f, 0.22f, 0.14f)));
        if (mane > 0f) return Tinted(Color.Lerp(MANE_A, MANE_B, 1f - v), mane);
        return CLEAR;
    }

    static Color Crown(float u, float v) {
        float shape = InPoly(CROWN, u, v);
        if (shape <= 0f) return CLEAR;
        float j1 = Circle(u, v, 0.28f, 0.26f, 0.055f);
        float j2 = Circle(u, v, 0.50f, 0.26f, 0.065f);
        float j3 = Circle(u, v, 0.72f, 0.26f, 0.055f);
        if (j1 > 0f) return new Color(0.35f, 0.70f, 1.00f, j1 * shape);
        if (j2 > 0f) return new Color(1.00f, 0.30f, 0.45f, j2 * shape);
        if (j3 > 0f) return new Color(0.45f, 0.95f, 0.55f, j3 * shape);
        Color c = v < 0.36f ? GOLD_DK : Color.Lerp(GOLD, Color.white, Mathf.Clamp01((v - 0.5f) * 0.8f));
        return new Color(c.r, c.g, c.b, shape);
    }

    // 🧲 Horseshoe magnet — red body, silver poles, open at the bottom
    static Color Magnet(float u, float v) {
        float ring = Mathf.Clamp01(Circle(u, v, 0.5f, 0.46f, 0.42f)
                                 - Circle(u, v, 0.5f, 0.46f, 0.20f));
        float arc  = v >= 0.46f ? ring : 0f;
        float legs = Mathf.Max(Rect(u, v, 0.08f, 0.10f, 0.30f, 0.46f),
                               Rect(u, v, 0.70f, 0.10f, 0.92f, 0.46f));
        float body = Mathf.Max(arc, legs);
        if (body <= 0f) return CLEAR;
        if (v < 0.22f) return new Color(0.86f, 0.88f, 0.92f, body);   // silver tips
        return new Color(0.90f, 0.16f, 0.22f, body);
    }

    // ⏱️ Hourglass — glass frame with falling sand
    static Color Hourglass(float u, float v) {
        float cap = Mathf.Max(Rect(u, v, 0.16f, 0.86f, 0.84f, 0.96f),
                              Rect(u, v, 0.16f, 0.04f, 0.84f, 0.14f));
        if (cap > 0f) return new Color(0.85f, 0.62f, 0.30f, cap);
        // two triangles meeting at the waist
        float top = InTri(u, v, 0.20f, 0.86f, 0.80f, 0.86f, 0.50f, 0.50f);
        float bot = InTri(u, v, 0.20f, 0.14f, 0.80f, 0.14f, 0.50f, 0.50f);
        float glass = Mathf.Max(top, bot);
        if (glass <= 0f) return CLEAR;
        bool sand = (v > 0.60f && v < 0.86f) || (v > 0.14f && v < 0.30f)
                    || (Mathf.Abs(u - 0.5f) < 0.02f);
        return sand ? new Color(1.00f, 0.80f, 0.28f, glass)
                    : new Color(0.72f, 0.90f, 0.98f, glass * 0.75f);
    }

    // 🌈 Rainbow arc — six colour bands
    static Color RainbowArc(float u, float v) {
        float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.16f) * (v - 0.16f));
        if (v < 0.16f || d > 0.46f || d < 0.18f) return CLEAR;
        float t = Mathf.Clamp01((0.46f - d) / 0.28f);
        Color[] bands = {
            new Color(1.00f, 0.27f, 0.27f), new Color(1.00f, 0.55f, 0.15f),
            new Color(1.00f, 0.88f, 0.20f), new Color(0.35f, 0.85f, 0.40f),
            new Color(0.25f, 0.60f, 1.00f), new Color(0.68f, 0.35f, 1.00f),
        };
        int i = Mathf.Clamp((int)(t * bands.Length), 0, bands.Length - 1);
        return new Color(bands[i].r, bands[i].g, bands[i].b, 1f);
    }

    static Color Tinted(Color c, float a) => new Color(c.r, c.g, c.b, a);

    // ── Coverage primitives ───────────────────────────────────
    static float Circle(float u, float v, float cx, float cy, float r) =>
        (u - cx) * (u - cx) + (v - cy) * (v - cy) <= r * r ? 1f : 0f;

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
        return dx * dx + dy * dy <= r * r ? 1f : 0f;
    }

    static float Heart(float u, float v) {
        float x = (u - 0.5f) * 3.0f;
        float y = (v - 0.40f) * 3.0f;
        float f = Mathf.Pow(x * x + y * y - 1f, 3f) - x * x * y * y * y;
        return f < 0f ? 1f : 0f;
    }

    // Four-pointed sparkle (concave diamond) — reads as "shiny", same family as ⭐
    static float Sparkle4(float u, float v) {
        float x = Mathf.Abs(u - 0.5f) / 0.48f;
        float y = Mathf.Abs(v - 0.5f) / 0.48f;
        return Mathf.Sqrt(x) + Mathf.Sqrt(y) <= 1f ? 1f : 0f;
    }

    static float Gift(float u, float v) {
        float box = RoundRect(u, v, 0.16f, 0.10f, 0.84f, 0.68f, 0.04f);
        float lid = RoundRect(u, v, 0.10f, 0.68f, 0.90f, 0.84f, 0.04f);
        float a = Mathf.Max(box, lid);
        if (a <= 0f) return 0f;
        if (u > 0.465f && u < 0.535f && v < 0.68f) return 0f;
        if (v > 0.655f && v < 0.695f) return 0f;
        return a;
    }

    static float InTri(float px, float py, float ax, float ay, float bx, float by, float cx, float cy) {
        float d1 = Sign(px, py, ax, ay, bx, by);
        float d2 = Sign(px, py, bx, by, cx, cy);
        float d3 = Sign(px, py, cx, cy, ax, ay);
        bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
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
    static readonly Vector2[] HORN = {
        new Vector2(0.40f, 0.66f), new Vector2(0.31f, 0.98f), new Vector2(0.52f, 0.68f),
    };
    static readonly Vector2[] EAR = {
        new Vector2(0.55f, 0.64f), new Vector2(0.62f, 0.88f), new Vector2(0.69f, 0.62f),
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

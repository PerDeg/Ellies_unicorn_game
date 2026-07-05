using System.Collections.Generic;
using UnityEngine;

namespace UnicornGame {

// ══════════════════════════════════════
//  SPRITE FACTORY
//  Draws all game shapes procedurally into 64×64 textures —
//  the project needs zero image assets. Shapes are white;
//  tint them via SpriteRenderer.color.
//  Swap for real art later by replacing Get() lookups.
// ══════════════════════════════════════
public static class SpriteFactory {

    const int SIZE = 64;
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Sprite Get(string shape) {
        if (cache.TryGetValue(shape, out var s)) return s;
        s = Build(shape);
        cache[shape] = s;
        return s;
    }

    static Sprite Build(string shape) {
        var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[SIZE * SIZE];
        for (int y = 0; y < SIZE; y++) {
            for (int x = 0; x < SIZE; x++) {
                // uv in [0,1], y up
                float u = (x + 0.5f) / SIZE, v = (y + 0.5f) / SIZE;
                float a = Alpha(shape, u, v);
                px[y * SIZE + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        // pixelsPerUnit = SIZE → sprite is exactly 1 world unit across at scale 1
        return Sprite.Create(tex, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), SIZE);
    }

    // Returns 1 inside the shape, 0 outside (soft-edged where cheap).
    static float Alpha(string shape, float u, float v) {
        switch (shape) {
            case "circle":   return Circle(u, v, 0.5f, 0.5f, 0.46f);
            case "star":     return InPoly(StarPoints(5, 0.48f, 0.20f), u, v) ? 1f : 0f;
            case "heart":    return Heart(u, v);
            case "crescent": return Mathf.Clamp01(Circle(u, v, 0.5f, 0.5f, 0.44f) - Circle(u, v, 0.66f, 0.58f, 0.40f));
            case "skull":    return Skull(u, v);
            case "crown":    return InPoly(CROWN, u, v) ? 1f : 0f;
            case "gift":     return Gift(u, v);
            case "unicorn":  return Unicorn(u, v);
            case "bolt":     return InPoly(BOLT, u, v) ? 1f : 0f;
            case "square":   return Rect(u, v, 0.01f, 0.01f, 0.99f, 0.99f);
            default:         return Circle(u, v, 0.5f, 0.5f, 0.46f);
        }
    }

    static float Circle(float u, float v, float cx, float cy, float r) {
        float d = Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy));
        return Mathf.Clamp01((r - d) / 0.02f);
    }

    static float Heart(float u, float v) {
        // implicit heart curve: (x²+y²−1)³ − x²y³ < 0
        float x = (u - 0.5f) * 3.2f;
        float y = (v - 0.42f) * 3.2f;
        float f = Mathf.Pow(x * x + y * y - 1f, 3f) - x * x * y * y * y;
        return f < 0 ? 1f : 0f;
    }

    static float Skull(float u, float v) {
        float head = Circle(u, v, 0.5f, 0.58f, 0.34f);
        float jaw  = Rect(u, v, 0.36f, 0.14f, 0.64f, 0.36f);
        float baseA = Mathf.Max(head, jaw);
        if (baseA <= 0) return 0;
        // punch out eyes + nose
        if (Circle(u, v, 0.38f, 0.62f, 0.09f) > 0) return 0;
        if (Circle(u, v, 0.62f, 0.62f, 0.09f) > 0) return 0;
        if (InTriangle(u, v, 0.5f, 0.52f, 0.44f, 0.40f, 0.56f, 0.40f)) return 0;
        return baseA;
    }

    static float Gift(float u, float v) {
        float box = Rect(u, v, 0.14f, 0.10f, 0.86f, 0.70f);
        float lid = Rect(u, v, 0.10f, 0.70f, 0.90f, 0.86f);
        float a = Mathf.Max(box, lid);
        if (a <= 0) return 0;
        // ribbon: brighter is not possible with alpha only — cut thin slits instead
        if (u > 0.47f && u < 0.53f) return a;               // vertical ribbon stays
        if (v > 0.74f && v < 0.82f) return a;               // lid band stays
        return a;
    }

    static float Unicorn(float u, float v) {
        float head = Circle(u, v, 0.46f, 0.44f, 0.30f);
        float ear  = InTriangle(u, v, 0.30f, 0.68f, 0.40f, 0.90f, 0.48f, 0.70f) ? 1f : 0f;
        float horn = InTriangle(u, v, 0.55f, 0.68f, 0.85f, 0.98f, 0.68f, 0.62f) ? 1f : 0f;
        float a = Mathf.Max(head, Mathf.Max(ear, horn));
        if (a <= 0) return 0;
        if (Circle(u, v, 0.56f, 0.48f, 0.045f) > 0) return 0; // eye
        return a;
    }

    static float Rect(float u, float v, float x0, float y0, float x1, float y1) {
        return (u >= x0 && u <= x1 && v >= y0 && v <= y1) ? 1f : 0f;
    }

    static bool InTriangle(float px, float py, float ax, float ay, float bx, float by, float cx, float cy) {
        float d1 = Sign(px, py, ax, ay, bx, by);
        float d2 = Sign(px, py, bx, by, cx, cy);
        float d3 = Sign(px, py, cx, cy, ax, ay);
        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        return !(hasNeg && hasPos);
    }
    static float Sign(float px, float py, float ax, float ay, float bx, float by) {
        return (px - bx) * (ay - by) - (ax - bx) * (py - by);
    }

    // 5-point star as a 10-vertex polygon around (0.5, 0.5)
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
        new Vector2(0.08f, 0.15f), new Vector2(0.92f, 0.15f), new Vector2(0.92f, 0.40f),
        new Vector2(0.80f, 0.85f), new Vector2(0.65f, 0.45f), new Vector2(0.50f, 0.92f),
        new Vector2(0.35f, 0.45f), new Vector2(0.20f, 0.85f), new Vector2(0.08f, 0.40f),
    };

    static readonly Vector2[] BOLT = {
        new Vector2(0.55f, 0.95f), new Vector2(0.25f, 0.45f), new Vector2(0.45f, 0.45f),
        new Vector2(0.38f, 0.05f), new Vector2(0.75f, 0.55f), new Vector2(0.52f, 0.55f),
    };

    // Even-odd point-in-polygon
    static bool InPoly(Vector2[] poly, float u, float v) {
        bool inside = false;
        int n = poly.Length;
        for (int i = 0, j = n - 1; i < n; j = i++) {
            if ((poly[i].y > v) != (poly[j].y > v) &&
                u < (poly[j].x - poly[i].x) * (v - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        }
        return inside;
    }
}
}

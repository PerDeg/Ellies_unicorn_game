using System.Collections.Generic;
using UnityEngine;

namespace UnicornGame {

// ══════════════════════════════════════
//  HUD — score line, hearts, speed/round bars, banners and menu.
//  Everything is anchored to the fixed 480×854 playfield, so the layout
//  is identical on every screen shape.
//
//  Text is sized by measuring the rendered mesh and scaling it to a target
//  world height — that way it looks right regardless of which fallback
//  font the editor supplies.
// ══════════════════════════════════════
public class Hud : MonoBehaviour {

    class FitText {
        public TextMesh tm;
        public float targetLineHeight;
        public bool dirty;
    }

    readonly List<FitText> fitted = new List<FitText>();

    FitText topLine, banner, centreFlash, menu, powerLine;
    SpriteRenderer speedBg, speedFill, roundBg, roundFill, panel;
    readonly SpriteRenderer[] hearts = new SpriteRenderer[3];

    float bannerTimer, flashTimer;

    static Font UiFont() {
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
        if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 64);
        return f;
    }

    FitText MakeText(string name, float lineHeight, Color color, int order = 45) {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var tm = go.AddComponent<TextMesh>();
        var font = UiFont();
        if (font != null) {
            tm.font = font;
            go.GetComponent<MeshRenderer>().material = font.material;
        }
        tm.fontSize = 72;                 // high res; world size comes from scaling
        tm.characterSize = 0.1f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        tm.richText = false;
        go.GetComponent<MeshRenderer>().sortingOrder = order;
        var ft = new FitText { tm = tm, targetLineHeight = lineHeight, dirty = true };
        fitted.Add(ft);
        return ft;
    }

    SpriteRenderer MakeQuad(string name, Color color, int order) {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Get("square");
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    public void Init() {
        panel       = MakeQuad("menuPanel", new Color(0.04f, 0f, 0.10f, 0.82f), 41);
        topLine     = MakeText("topline", 0.30f, Color.white);
        banner      = MakeText("banner",  0.26f, new Color(1f, 0.88f, 0.40f));
        centreFlash = MakeText("flash",   0.46f, new Color(1f, 0.88f, 0.40f));
        powerLine   = MakeText("power",   0.22f, new Color(0.40f, 0.91f, 0.98f));
        menu        = MakeText("menu",    0.30f, Color.white, 46);

        speedBg   = MakeQuad("speedBg",   new Color(1f, 1f, 1f, 0.14f), 42);
        speedFill = MakeQuad("speedFill", new Color(0.65f, 0.55f, 0.98f), 43);
        roundBg   = MakeQuad("roundBg",   new Color(1f, 1f, 1f, 0.16f), 42);
        roundFill = MakeQuad("roundFill", new Color(0.53f, 0.94f, 0.67f), 43);

        for (int i = 0; i < hearts.Length; i++) {
            var go = new GameObject("heart" + i);
            go.transform.SetParent(transform, false);
            hearts[i] = go.AddComponent<SpriteRenderer>();
            hearts[i].sprite = SpriteFactory.Get("heart");
            hearts[i].color = new Color(1f, 0.32f, 0.42f);
            hearts[i].sortingOrder = 45;
        }

        SetText(topLine, ""); SetText(banner, ""); SetText(centreFlash, "");
        SetText(powerLine, ""); SetText(menu, "");
        Layout();
    }

    // ── Layout, all in playfield coordinates ──────────────────
    public void Layout() {
        float W = Playfield.W, H = Playfield.H;
        float top = H * 0.5f, bottom = -H * 0.5f;

        topLine.tm.transform.position     = new Vector3(0f, top - 0.32f, 0f);
        PlaceQuad(speedBg, 0f, top - 0.60f, W * 0.62f, 0.05f);
        banner.tm.transform.position      = new Vector3(0f, top - 1.35f, 0f);
        centreFlash.tm.transform.position = new Vector3(0f, H * 0.14f, 0f);
        powerLine.tm.transform.position   = new Vector3(0f, bottom + 1.15f, 0f);
        PlaceQuad(roundBg, 0f, bottom + 0.55f, W * 0.72f, 0.12f);

        menu.tm.transform.position  = Vector3.zero;
        panel.transform.position    = Vector3.zero;
        panel.transform.localScale  = new Vector3(W * 0.94f, H * 0.62f, 1f);

        for (int i = 0; i < hearts.Length; i++) {
            hearts[i].transform.position   = new Vector3(-W * 0.5f + 0.34f + i * 0.34f, top - 0.90f, 0f);
            hearts[i].transform.localScale = Vector3.one * 0.28f;
        }
    }

    void PlaceQuad(SpriteRenderer q, float cx, float cy, float w, float h) {
        q.transform.position = new Vector3(cx, cy, 0f);
        q.transform.localScale = new Vector3(w, h, 1f);
    }

    // Left-anchored fill: keeps the left edge fixed while the width grows.
    void SetFill(SpriteRenderer fill, SpriteRenderer bg, float frac, float fullW, float h) {
        frac = Mathf.Clamp01(frac);
        float w = fullW * frac;
        fill.enabled = w > 0.001f;
        fill.transform.localScale = new Vector3(w, h, 1f);
        fill.transform.position = bg.transform.position - new Vector3((fullW - w) * 0.5f, 0f, 0f);
    }

    // ── Public setters ────────────────────────────────────────
    public void SetTop(int score, int level, int streak, int multi) =>
        SetText(topLine, $"{score} p     Nivå {level}     Streak {streak}     x{multi}");

    public void SetHearts(int misses, int maxMisses) {
        for (int i = 0; i < hearts.Length; i++) {
            hearts[i].gameObject.SetActive(i < maxMisses);
            bool alive = i < maxMisses - misses;
            hearts[i].color = alive ? new Color(1f, 0.32f, 0.42f) : new Color(1f, 0.32f, 0.42f, 0.18f);
            hearts[i].transform.localScale = Vector3.one * (alive ? 0.28f : 0.20f);
        }
    }

    public void SetSpeed(float frac) {
        SetFill(speedFill, speedBg, frac, Playfield.W * 0.62f, 0.05f);
        speedFill.color = frac > 0.75f ? new Color(1f, 0.35f, 0.35f)
                        : frac > 0.45f ? new Color(1f, 0.50f, 1f)
                                       : new Color(0.65f, 0.55f, 0.98f);
    }

    public void SetRound(int caught, int missed, int size) {
        float frac = size > 0 ? (caught + missed) / (float)size : 0f;
        SetFill(roundFill, roundBg, frac, Playfield.W * 0.72f, 0.12f);
        roundFill.color = missed == 0 ? new Color(0.53f, 0.94f, 0.67f)
                                      : new Color(0.99f, 0.45f, 0.65f);
    }

    public void SetPowerups(string txt) => SetText(powerLine, txt);

    public void ShowBanner(string txt, float dur = 2f) { SetText(banner, txt); bannerTimer = dur; }
    public void FlashCentre(string txt, float dur = 1.4f) { SetText(centreFlash, txt); flashTimer = dur; }

    public void SetMenu(string txt) {
        SetText(menu, txt);
        bool show = !string.IsNullOrEmpty(txt);
        panel.enabled = show;
    }

    /// Hides the in-game HUD elements while a menu is up.
    public void SetPlayingVisible(bool on) {
        topLine.tm.gameObject.SetActive(on);
        powerLine.tm.gameObject.SetActive(on);
        speedBg.enabled = on;
        roundBg.enabled = on;
        if (!on) { speedFill.enabled = false; roundFill.enabled = false; }
        foreach (var h in hearts) h.gameObject.SetActive(on);
    }

    void SetText(FitText ft, string txt) {
        if (ft.tm.text == txt) return;
        ft.tm.text = txt;
        ft.dirty = true;
        Fit(ft);              // fit now…
    }

    // …and again in LateUpdate, since TextMesh bounds can lag a frame.
    void LateUpdate() {
        float dt = Time.deltaTime;
        if (bannerTimer > 0f) { bannerTimer -= dt; if (bannerTimer <= 0f) SetText(banner, ""); }
        if (flashTimer  > 0f) { flashTimer  -= dt; if (flashTimer  <= 0f) SetText(centreFlash, ""); }
        foreach (var ft in fitted) if (ft.dirty) Fit(ft);
    }

    void Fit(FitText ft) {
        if (string.IsNullOrEmpty(ft.tm.text)) { ft.dirty = false; return; }
        var r = ft.tm.GetComponent<Renderer>();
        if (r == null) return;
        ft.tm.transform.localScale = Vector3.one;
        int lines = 1;
        foreach (char c in ft.tm.text) if (c == '\n') lines++;
        float measured = r.bounds.size.y / lines;
        if (measured <= 0.0001f) return;                  // mesh not built yet — retry next frame
        float scale = ft.targetLineHeight / measured;

        // never let a long line spill outside the playfield
        float maxW = Playfield.W * 0.94f;
        float widthAtScale = r.bounds.size.x * scale;
        if (widthAtScale > maxW) scale *= maxW / widthAtScale;

        ft.tm.transform.localScale = Vector3.one * scale;
        ft.dirty = false;
    }
}
}

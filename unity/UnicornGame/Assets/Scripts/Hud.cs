using UnityEngine;

namespace UnicornGame {

// ══════════════════════════════════════
//  HUD — score line, hearts, speed/round bars, banners.
//  Built entirely from code with classic TextMesh + SpriteRenderers
//  so no scene setup, prefabs or UI packages are needed.
// ══════════════════════════════════════
public class Hud : MonoBehaviour {

    Camera cam;
    TextMesh topLine, banner, centreFlash, menuText;
    SpriteRenderer speedFill, speedBg, roundFill, roundBg;
    SpriteRenderer[] hearts = new SpriteRenderer[3];
    float bannerTimer, flashTimer;

    static Font UiFont() {
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
        return f;
    }

    public static TextMesh MakeText(Transform parent, string name, int fontSize, Color color,
                                    TextAnchor anchor = TextAnchor.MiddleCenter, int order = 40) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tm = go.AddComponent<TextMesh>();
        var font = UiFont();
        if (font != null) {
            tm.font = font;
            go.GetComponent<MeshRenderer>().material = font.material;
        }
        tm.fontSize = fontSize;
        tm.characterSize = 0.045f;
        tm.anchor = anchor;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        go.GetComponent<MeshRenderer>().sortingOrder = order;
        return tm;
    }

    SpriteRenderer MakeBar(string name, Color color, int order) {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Get("square");
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    public void Init(Camera camera) {
        cam = camera;
        topLine     = MakeText(transform, "topline", 44, Color.white);
        banner      = MakeText(transform, "banner", 40, new Color(1f, 0.88f, 0.4f));
        centreFlash = MakeText(transform, "flash", 54, new Color(1f, 0.88f, 0.4f));
        menuText    = MakeText(transform, "menu", 40, Color.white);
        speedBg   = MakeBar("speedBg",   new Color(1, 1, 1, 0.12f), 30);
        speedFill = MakeBar("speedFill", new Color(0.65f, 0.55f, 0.98f), 31);
        roundBg   = MakeBar("roundBg",   new Color(1, 1, 1, 0.15f), 30);
        roundFill = MakeBar("roundFill", new Color(0.53f, 0.94f, 0.67f), 31);
        for (int i = 0; i < hearts.Length; i++) {
            var go = new GameObject("heart" + i);
            go.transform.SetParent(transform, false);
            hearts[i] = go.AddComponent<SpriteRenderer>();
            hearts[i].sprite = SpriteFactory.Get("heart");
            hearts[i].color = new Color(1f, 0.3f, 0.4f);
            hearts[i].sortingOrder = 40;
        }
        banner.text = ""; centreFlash.text = ""; menuText.text = "";
        Layout();
    }

    float W => 2f * cam.orthographicSize * cam.aspect;
    float H => 2f * cam.orthographicSize;

    public void Layout() {
        float top = H / 2f;
        topLine.transform.position = new Vector3(0, top - 0.45f, 0);
        banner.transform.position = new Vector3(0, top - 1.6f, 0);
        centreFlash.transform.position = new Vector3(0, H * 0.10f, 0);
        menuText.transform.position = new Vector3(0, 0, 0);

        // speed strip under the top line
        PlaceBar(speedBg, 0, top - 0.85f, W * 0.5f, 0.06f);
        // round bar near the bottom
        PlaceBar(roundBg, 0, -H / 2f + 1.5f, W * 0.6f, 0.14f);

        for (int i = 0; i < hearts.Length; i++) {
            hearts[i].transform.position = new Vector3(-W / 2f + 0.5f + i * 0.5f, top - 1.0f, 0);
            hearts[i].transform.localScale = Vector3.one * 0.4f;
        }
    }

    void PlaceBar(SpriteRenderer bar, float cx, float cy, float w, float h) {
        bar.transform.position = new Vector3(cx, cy, 0);
        bar.transform.localScale = new Vector3(w, h, 1);
    }

    public void SetTop(int score, int level, int streak, int multi) {
        topLine.text = $"P {score}    Niv {level}    Streak {streak}    x{multi}";
    }

    public void SetHearts(int misses, int maxMisses) {
        for (int i = 0; i < hearts.Length; i++) {
            bool have = i < maxMisses - misses;
            hearts[i].gameObject.SetActive(i < maxMisses);
            var c = hearts[i].color; c.a = have ? 1f : 0.15f; hearts[i].color = c;
        }
    }

    public void SetSpeed(float frac) {
        frac = Mathf.Clamp01(frac);
        float w = W * 0.5f * frac;
        speedFill.transform.localScale = new Vector3(w, 0.06f, 1);
        speedFill.transform.position = speedBg.transform.position - new Vector3((W * 0.5f - w) / 2f, 0, 0);
        speedFill.color = frac > 0.75f ? new Color(1f, 0.3f, 0.3f)
                        : frac > 0.45f ? new Color(1f, 0.5f, 1f)
                        : new Color(0.65f, 0.55f, 0.98f);
    }

    public void SetRound(int caught, int missed, int size, int roundNum) {
        float frac = Mathf.Clamp01((caught + missed) / (float)size);
        float w = W * 0.6f * frac;
        roundFill.transform.localScale = new Vector3(w, 0.14f, 1);
        roundFill.transform.position = roundBg.transform.position - new Vector3((W * 0.6f - w) / 2f, 0, 0);
        roundFill.color = missed == 0 ? new Color(0.53f, 0.94f, 0.67f) : new Color(0.99f, 0.45f, 0.65f);
    }

    public void ShowBanner(string txt, float dur = 2f) { banner.text = txt; bannerTimer = dur; }
    public void FlashCentre(string txt, float dur = 1.4f) { centreFlash.text = txt; flashTimer = dur; }
    public void SetMenu(string txt) { menuText.text = txt; }

    void Update() {
        if (bannerTimer > 0) { bannerTimer -= Time.deltaTime; if (bannerTimer <= 0) banner.text = ""; }
        if (flashTimer > 0) { flashTimer -= Time.deltaTime; if (flashTimer <= 0) centreFlash.text = ""; }
    }
}
}

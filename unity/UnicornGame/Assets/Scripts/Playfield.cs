using UnityEngine;

namespace UnicornGame {

// ══════════════════════════════════════
//  PLAYFIELD — fixed 480×854 portrait field (9:16), exactly like the
//  web game's phone-shaped #game-wrap. Gameplay coordinates are always
//  these "JS pixels" regardless of window shape; the camera letterboxes
//  around the field so proportions never change.
// ══════════════════════════════════════
public static class Playfield {

    public const float JsW = 480f;
    public const float JsH = 854f;
    public const float PX  = 0.01f;            // world units per JS pixel

    public static float W => JsW * PX;         // 4.80 world units
    public static float H => JsH * PX;         // 8.54 world units

    /// JS pixel coords (x right, y down from top) → world position.
    public static Vector3 FromJs(float xJs, float yJs) =>
        new Vector3(xJs * PX - W * 0.5f, H * 0.5f - yJs * PX, 0f);

    /// Sizes the camera so the whole field fits, whatever the window shape.
    public static void FitCamera(Camera cam) {
        float aspect = Mathf.Max(cam.aspect, 0.0001f);
        float needForHeight = H * 0.5f;
        float needForWidth  = (W * 0.5f) / aspect;
        cam.orthographicSize = Mathf.Max(needForHeight, needForWidth);
    }
}
}

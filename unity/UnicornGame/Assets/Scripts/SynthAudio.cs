using System.Collections.Generic;
using UnityEngine;

namespace UnicornGame {

// ══════════════════════════════════════
//  SYNTH AUDIO — port of public/js/audio.js
//  All SFX and tunes are synthesized into AudioClips at startup;
//  no audio assets required.
// ══════════════════════════════════════
public class SynthAudio : MonoBehaviour {

    const int SR = 44100;

    AudioSource sfx;
    AudioSource music;
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    public bool SoundEnabled { get; private set; } = true;

    struct Note { public float f, delay, dur, vol; public bool square;
        public Note(float f, float delay, float dur, float vol, bool square = false) {
            this.f = f; this.delay = delay; this.dur = dur; this.vol = vol; this.square = square;
        }
    }

    void Awake() {
        sfx = gameObject.AddComponent<AudioSource>();
        music = gameObject.AddComponent<AudioSource>();
        music.loop = true;
        SoundEnabled = PlayerPrefs.GetInt("soundEnabled", 1) == 1;
        AudioListener.volume = SoundEnabled ? 1f : 0f;
        BuildAll();
    }

    public void ToggleSound() {
        SoundEnabled = !SoundEnabled;
        PlayerPrefs.SetInt("soundEnabled", SoundEnabled ? 1 : 0);
        AudioListener.volume = SoundEnabled ? 1f : 0f;
    }

    // ── Public API (names mirror audio.js) ─────────────────────
    public void PlayCatch(float pitch = 1f) { sfx.pitch = pitch; sfx.PlayOneShot(clips["catch"]); sfx.pitch = 1f; }
    public void PlayStreak()   => sfx.PlayOneShot(clips["streak"]);
    public void PlayWow()      => sfx.PlayOneShot(clips["wow"]);
    public void PlayPerfect()  => sfx.PlayOneShot(clips["perfect"]);
    public void PlayMultiUp()  => sfx.PlayOneShot(clips["multiup"]);
    public void PlayLevelUp()  => sfx.PlayOneShot(clips["levelup"]);
    public void PlayMiss()     => sfx.PlayOneShot(clips["miss"]);
    public void PlayBadCatch(bool losesLife) => sfx.PlayOneShot(clips[losesLife ? "badlife" : "badpts"]);
    public void PlayGameOver() => sfx.PlayOneShot(clips["gameover"]);

    public void PlayTune(string name) {
        if (!clips.ContainsKey("tune_" + name)) name = "birthday";
        var clip = clips["tune_" + name];
        if (music.clip == clip && music.isPlaying) return;
        music.clip = clip;
        music.Play();
    }
    public void StopMusic() => music.Stop();

    // ── Synthesis ──────────────────────────────────────────────
    void BuildAll() {
        clips["catch"]    = Render(Stagger(new[] { 523f, 659f, 784f, 1047f }, 0.045f, 0.12f, 0.10f));
        clips["streak"]   = Render(Stagger(new[] { 523f, 659f, 784f, 1047f, 1319f, 1568f }, 0.06f, 0.18f, 0.10f));
        clips["wow"]      = Render(Wow());
        clips["perfect"]  = Render(Stagger(new[] { 523f, 659f, 784f, 1047f, 1319f, 1568f, 2093f }, 0.06f, 0.26f, 0.09f));
        clips["multiup"]  = Render(Stagger(new[] { 523f, 659f, 784f, 1047f, 1568f }, 0.05f, 0.18f, 0.08f));
        clips["levelup"]  = Render(Stagger(new[] { 523f, 587f, 659f, 698f, 784f, 880f, 988f, 1047f }, 0.048f, 0.16f, 0.09f));
        clips["miss"]     = Render(new List<Note> { new Note(200, 0f, 0.28f, 0.07f), new Note(160, 0.06f, 0.22f, 0.05f) });
        clips["badlife"]  = Render(Stagger(new[] { 300f, 250f, 200f, 150f }, 0.08f, 0.30f, 0.10f));
        clips["badpts"]   = Render(new List<Note> { new Note(280, 0f, 0.2f, 0.08f), new Note(220, 0.07f, 0.15f, 0.06f) });
        clips["gameover"] = Render(Stagger(new[] { 400f, 340f, 280f, 230f, 180f }, 0.12f, 0.32f, 0.09f));

        clips["tune_birthday"]  = RenderTune(120, 0.040f, false, BIRTHDAY);
        clips["tune_challenge"] = RenderTune(136, 0.040f, false, CHALLENGE);
        clips["tune_golden"]    = RenderTune(88,  0.040f, false, GOLDEN);
        clips["tune_party"]     = RenderTune(130, 0.018f, true,  PARTY);
    }

    static List<Note> Stagger(float[] freqs, float step, float dur, float vol) {
        var l = new List<Note>();
        for (int i = 0; i < freqs.Length; i++) l.Add(new Note(freqs[i], i * step, dur, vol));
        return l;
    }

    static List<Note> Wow() {
        var l = new List<Note>();
        float[][] pairs = { new[] { 523f, 659f, 784f }, new[] { 659f, 830f, 988f } };
        for (int i = 0; i < 2; i++) {
            l.Add(new Note(pairs[i][0], i * 0.08f, 0.4f, 0.08f));
            l.Add(new Note(pairs[i][1], i * 0.08f, 0.4f, 0.06f));
        }
        float[] tail = { 1047f, 1319f, 1568f, 2093f };
        for (int i = 0; i < tail.Length; i++) l.Add(new Note(tail[i], 0.22f + i * 0.05f, 0.22f, 0.05f));
        return l;
    }

    AudioClip Render(List<Note> notes) {
        float end = 0;
        foreach (var n in notes) end = Mathf.Max(end, n.delay + Mathf.Max(n.dur, 0.05f) + 0.06f);
        int len = Mathf.CeilToInt(end * SR);
        var buf = new float[len];
        foreach (var n in notes) AddNote(buf, n);
        var clip = AudioClip.Create("sfx", len, 1, SR, false);
        clip.SetData(buf, 0);
        return clip;
    }

    // Tune data: pairs of (frequency, beats)
    AudioClip RenderTune(float bpm, float vol, bool square, float[,] notes) {
        float beat = 60f / bpm;
        float total = 0;
        for (int i = 0; i < notes.GetLength(0); i++) total += notes[i, 1];
        int len = Mathf.CeilToInt((total * beat + 0.2f) * SR);
        var buf = new float[len];
        float t = 0.05f;
        for (int i = 0; i < notes.GetLength(0); i++) {
            float f = notes[i, 0], b = notes[i, 1];
            AddNote(buf, new Note(f, t, b * beat * 0.7f, vol, square));
            t += b * beat;
        }
        var clip = AudioClip.Create("tune", len, 1, SR, false);
        clip.SetData(buf, 0);
        return clip;
    }

    static void AddNote(float[] buf, Note n) {
        float dur = Mathf.Max(n.dur, 0.05f);
        int start = Mathf.FloorToInt(n.delay * SR);
        int count = Mathf.FloorToInt((dur + 0.06f) * SR);
        float attack = 0.01f;
        for (int i = 0; i < count && start + i < buf.Length; i++) {
            float t = i / (float)SR;
            float env = t < attack ? t / attack : Mathf.Exp(-5f * (t - attack) / dur);
            float phase = 2f * Mathf.PI * n.f * t;
            float w = n.square ? Mathf.Sign(Mathf.Sin(phase)) * 0.7f : Mathf.Sin(phase);
            buf[start + i] += w * env * n.vol;
        }
    }

    // ── Tune scores (freq, beats) — from audio.js TUNES ────────
    static readonly float[,] BIRTHDAY = {
        {392,0.75f},{392,0.25f},{440,1},{392,1},{523,1},{494,2},
        {392,0.75f},{392,0.25f},{440,1},{392,1},{587,1},{523,2},
        {392,0.75f},{392,0.25f},{784,1},{659,1},{523,1},{494,1},{440,2},
        {698,0.75f},{698,0.25f},{659,1},{523,1},{587,1},{523,2},
    };
    static readonly float[,] CHALLENGE = {
        {784,0.5f},{880,0.5f},{988,0.5f},{1047,0.5f},{988,0.5f},{880,0.5f},{784,1},
        {698,0.5f},{784,0.5f},{880,0.5f},{988,0.5f},{1047,1},
        {988,0.5f},{880,0.5f},{784,0.5f},{659,0.5f},{698,0.5f},{784,0.5f},{880,1},{784,2},
    };
    static readonly float[,] GOLDEN = {
        {523,1},{659,1},{784,1},{1047,2},{880,1},{784,1},{698,1},{659,2},
        {587,0.5f},{659,0.5f},{698,1},{784,1},{880,2},{1047,1},{784,3},
    };
    static readonly float[,] PARTY = {
        {880,0.5f},{880,0.5f},{1047,0.5f},{880,0.5f},{784,0.5f},{784,0.5f},{880,0.5f},{784,0.5f},
        {659,0.5f},{659,0.5f},{784,0.5f},{659,0.5f},{523,0.5f},{587,0.5f},{659,0.5f},{784,0.5f},
        {880,0.75f},{1047,0.25f},{1319,0.5f},{1047,0.5f},{880,0.5f},{784,0.5f},
        {880,0.25f},{880,0.25f},{1047,0.25f},{880,0.25f},{784,0.5f},{659,0.5f},
        {784,0.5f},{880,0.5f},{1047,0.5f},{1319,0.5f},{1047,0.5f},{880,0.5f},
        {784,0.25f},{659,0.25f},{523,0.5f},{659,0.5f},{784,0.5f},{880,1},
    };
}
}

"use strict";
// ══════════════════════════════════════
//  AUDIO ENGINE
// ══════════════════════════════════════
const AC = window.AudioContext || window.webkitAudioContext;
let ac = null;

function unlockAudio() {
  if (!ac) ac = new AC();
  if (ac.state === "suspended") ac.resume();
  try {
    const b = ac.createBuffer(1, 1, 22050);
    const s = ac.createBufferSource();
    s.buffer = b; s.connect(ac.destination); s.start(0);
  } catch (e) {}
}

function tone(freq, type, dur, vol, delay = 0) {
  if (!ac) return;
  const o = ac.createOscillator(), g = ac.createGain();
  o.connect(g); g.connect(ac.destination);
  o.type = type;
  const t0 = ac.currentTime + Math.max(0, delay);
  o.frequency.setValueAtTime(freq, t0);
  g.gain.setValueAtTime(0, t0);
  g.gain.linearRampToValueAtTime(vol, t0 + 0.01);
  g.gain.exponentialRampToValueAtTime(0.0001, t0 + Math.max(dur, 0.05));
  o.start(t0); o.stop(t0 + Math.max(dur, 0.05) + 0.06);
}

function playCatch(p = 1) {
  [523, 659, 784, 1047].forEach((f, i) => tone(f * p, "sine", 0.12, 0.10, i * 0.045));
}
function playStreak5() {
  [523, 659, 784, 1047, 1319, 1568].forEach((f, i) => tone(f, "sine", 0.18, 0.10, i * 0.06));
}
function playWow() {
  [[523, 659, 784], [659, 830, 988]].forEach(([a, b], i) => {
    tone(a, "sine", 0.4, 0.08, i * 0.08);
    tone(b, "sine", 0.4, 0.06, i * 0.08);
  });
  [1047, 1319, 1568, 2093].forEach((f, i) => tone(f, "sine", 0.22, 0.05, 0.22 + i * 0.05));
}
function playPerfect() {
  [523, 659, 784, 1047, 1319, 1568, 2093].forEach((f, i) => tone(f, "sine", 0.26, 0.09, i * 0.06));
  setTimeout(() => {
    [2093, 2637].forEach((f, i) => tone(f, "sine", 0.28, 0.04, i * 0.07));
  }, 450);
}
function playMultiUp(m) {
  const p = [1, 1.19, 1.335, 1.587, 2][Math.min(m - 2, 4)];
  [523, 659, 784, 1047, 1568].forEach((f, i) => tone(f * p, "sine", 0.18, 0.08, i * 0.05));
}
function playLevelUp() {
  [523, 587, 659, 698, 784, 880, 988, 1047].forEach((f, i) => tone(f, "sine", 0.16, 0.09, i * 0.048));
}
function playMiss() {
  tone(200, "sine", 0.28, 0.07);
  tone(160, "sine", 0.22, 0.05, 0.06);
}
function playBadCatch(losesLife) {
  if (losesLife) {
    [300, 250, 200, 150].forEach((f, i) => tone(f, "sine", 0.3, 0.10, i * 0.08));
  } else {
    tone(280, "sine", 0.2, 0.08);
    tone(220, "sine", 0.15, 0.06, 0.07);
  }
}
function playGameOver() {
  [400, 340, 280, 230, 180].forEach((f, i) => tone(f, "sine", 0.32, 0.09, i * 0.12));
}

// ── Background music ──────────────────────────────────────────────────────────
const TUNES = {
  birthday: { bpm: 120, notes: [[392,0.75],[392,0.25],[440,1],[392,1],[523,1],[494,2],[392,0.75],[392,0.25],[440,1],[392,1],[587,1],[523,2],[392,0.75],[392,0.25],[784,1],[659,1],[523,1],[494,1],[440,2],[698,0.75],[698,0.25],[659,1],[523,1],[587,1],[523,2]] },
  normal:   { bpm: 108, notes: [[659,0.5],[659,0.5],[659,1],[659,0.5],[659,0.5],[659,1],[659,0.5],[784,0.5],[523,0.5],[587,0.5],[659,2],[698,0.5],[698,0.75],[698,0.25],[698,0.5],[659,0.25],[659,0.5],[659,0.5],[587,0.5],[587,0.5],[659,0.5],[587,1],[784,1]] },
  challenge:{ bpm: 136, notes: [[784,0.5],[880,0.5],[988,0.5],[1047,0.5],[988,0.5],[880,0.5],[784,1],[698,0.5],[784,0.5],[880,0.5],[988,0.5],[1047,1],[988,0.5],[880,0.5],[784,0.5],[659,0.5],[698,0.5],[784,0.5],[880,1],[784,2]] },
  golden:   { bpm:  88, notes: [[523,1],[659,1],[784,1],[1047,2],[880,1],[784,1],[698,1],[659,2],[587,0.5],[659,0.5],[698,1],[784,1],[880,2],[1047,1],[784,3]] },
};

let musicTimeout = null;

function stopMusic() {
  clearTimeout(musicTimeout);
  musicTimeout = null;
}

function playTune(name, loop = true) {
  if (!ac) return;
  stopMusic();
  const tune = TUNES[name] || TUNES.normal;
  const beat = 60 / tune.bpm;
  let t = ac.currentTime + 0.1;
  tune.notes.forEach(([f, b]) => {
    tone(f, "sine", b * beat * 0.7, 0.040, t - ac.currentTime);
    t += b * beat;
  });
  if (loop) {
    const ms = tune.notes.reduce((s, [, b]) => s + b, 0) * (60000 / tune.bpm);
    musicTimeout = setTimeout(() => playTune(name, true), ms + 150);
  }
}

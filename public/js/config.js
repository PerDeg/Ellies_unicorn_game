"use strict";
// ══════════════════════════════════════
//  DIFFICULTY CONFIG
// ══════════════════════════════════════
const DIFF = {
  barn: {
    label:       "Barn",
    maxMisses:   5,
    baseSpeed:   1.2,
    speedInc:    0.015,  // slow build — takes much longer to reach max
    maxSpeed:    7.0,    // higher ceiling
    speedReset:  1.2,
    spawnBase:   1400,
    spawnMin:    800,
    spawnLevel:  12,
    multiThresh: [0, 8, 15, 25, 35, 50],
    roundSize:   8,      // base; grows dynamically with level
    perfBonus:   3,      // base; grows dynamically with level
    catchRadius: 95,
  },
  vuxen: {
    label:       "Vuxen",
    maxMisses:   3,      // lives only lost from catching 💔
    baseSpeed:   2.5,
    speedInc:    0.22,
    maxSpeed:    16.0,   // higher ceiling
    speedReset:  2.5,
    spawnBase:   1000,
    spawnMin:    360,
    spawnLevel:  14,
    multiThresh: [0, 5, 10, 15, 20, 25],
    roundSize:   10,     // base; grows dynamically with level
    perfBonus:   5,      // base; grows dynamically with level
    catchRadius: 58,
    missPenalty: 2,      // points lost per missed star (instead of life)
  },
};

// ══════════════════════════════════════
//  SPRITE TYPES
// ══════════════════════════════════════
const STAR_TYPES = [
  { emoji: "⭐", pts: 1, size: 38, bad: false },
  { emoji: "🌠", pts: 2, size: 36, bad: false },
  { emoji: "💫", pts: 3, size: 40, bad: false },
  { emoji: "🌟", pts: 5, size: 42, bad: false },
  { emoji: "✨", pts: 8, size: 38, bad: false },
];
const STAR_WEIGHTS = [40, 25, 18, 12, 5];

// Catching a bad sprite costs points + breaks streak. Avoiding is neutral.
const BAD_TYPES = [
  { emoji: "💀", pts: -3, size: 36, bad: true, label: "💀=-3p" },
  { emoji: "🌑", pts: -5, size: 38, bad: true, label: "🌑=-5p" },
];

// Life sprites: 💚/☯️ give life, 💔 costs a life
const LIFE_TYPES = [
  { emoji: "💔", size: 34, givesLife: false, life: true },
  { emoji: "💚", size: 34, givesLife: true,  life: true },
  { emoji: "☯️",  size: 34, givesLife: true,  life: true },
];

const POWERUP_TYPES = [
  { id: "magnet",  emoji: "🧲", label: "Magnet!",   dur: 6000, color: "#67e8f9" },
  { id: "slowmo",  emoji: "⏱️",  label: "Slow-mo!",  dur: 5000, color: "#c4b5fd" },
  { id: "rainbow", emoji: "🌈", label: "Regnbåge!", dur: 7000, color: "#ff9de2" },
];

// ══════════════════════════════════════
//  LEVEL THEMES
// ══════════════════════════════════════
const LEVEL_THEMES = [
  { bg: "linear-gradient(180deg,#0d1b6e 0%,#3a0e8f 30%,#7c1fa8 60%,#d45fbd 85%,#ffd6f0 100%)", name: "🌙 Natt"        },
  { bg: "linear-gradient(180deg,#1a0550 0%,#6d1b7b 25%,#c2185b 60%,#ff7043 85%,#ffe0b2 100%)", name: "🌅 Gryning"     },
  { bg: "linear-gradient(180deg,#0277bd 0%,#0288d1 30%,#29b6f6 60%,#81d4fa 85%,#e1f5fe 100%)", name: "☀️ Dag"         },
  { bg: "linear-gradient(180deg,#311b92 0%,#7b1fa2 20%,#e65100 55%,#ff8f00 80%,#ffcc02 100%)", name: "🌇 Solnedgång"  },
  { bg: "linear-gradient(180deg,#000000 0%,#0d0221 30%,#1a0550 60%,#2d1b69 85%,#3d1c8f 100%)", name: "🚀 Rymden"      },
];

// ══════════════════════════════════════
//  CHALLENGES
// ══════════════════════════════════════
const CHALLENGES = [
  { id: "rainbow", label: "🌈 Regnbågsläge!", music: "normal" },
  { id: "giant",   label: "⭐ Jättestjärnor!", sizeBoost: 20, music: "golden" },
  { id: "double",  label: "💫 Dubbla stjärnor!", doubleSpawn: true, music: "challenge" },
  { id: "golden",  label: "✨ Guldrusning!", forceType: 4, music: "golden" },
  { id: "party",   label: "🎉 Festläge!", partyMode: true, music: "challenge" },
];

// ══════════════════════════════════════
//  VISUAL CONSTANTS
// ══════════════════════════════════════
const CATCH_POPS  = ["🎉","🌈","💖","🦋","🍭","🎀","🎊","🌸","🍀","🌺"];
const COLORS      = ["#ffe066","#ff9de2","#a78bfa","#67e8f9","#86efac","#fda4af","#fcd34d","#f9a8d4","#c4b5fd","#6ee7b7"];
const MULTI_COLORS= ["","#c4b5fd","#ff9de2","#ffe066","#67e8f9","#ff80ff","#ffd700"];
const RING_CLASSES= ["","c1","c2","c3","c4","c5","c5"];
const STREAK_WORDS= ["Bra! 🌟","Häftigt! 💫","Flyger! 🦋","I eld! 🔥","Magiskt! ✨","GALET! 🤯"];

// ══════════════════════════════════════
//  SPAWN RATES
// ══════════════════════════════════════
const POWERUP_SPAWN_CHANCE = 0.05;
function lifeChance()      { return 0.04; }
function badChance(level)  { return Math.min(0.10 + level * 0.02, 0.28); }

// ══════════════════════════════════════
//  SEEDED RNG
// ══════════════════════════════════════
let rngSeed = 42;
function resetRng()  { rngSeed = 42; }
function seededRng() {
  rngSeed = (rngSeed * 1664525 + 1013904223) & 0xffffffff;
  return (rngSeed >>> 0) / 0xffffffff;
}
function pickStarType() {
  const r = seededRng() * 100;
  let cum = 0;
  for (let i = 0; i < STAR_WEIGHTS.length; i++) {
    cum += STAR_WEIGHTS[i];
    if (r < cum) return STAR_TYPES[i];
  }
  return STAR_TYPES[0];
}

// ══════════════════════════════════════
//  THEME HELPER
// ══════════════════════════════════════
function getTheme(lvl) {
  if (lvl <= 2) return LEVEL_THEMES[0];
  if (lvl <= 4) return LEVEL_THEMES[1];
  if (lvl <= 6) return LEVEL_THEMES[2];
  if (lvl <= 8) return LEVEL_THEMES[3];
  return LEVEL_THEMES[4];
}

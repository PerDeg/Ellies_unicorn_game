"use strict";
// ══════════════════════════════════════
//  DIFFICULTY CONFIG
// ══════════════════════════════════════
const DIFF = {
  barn: {
    label:       "Barn",
    maxMisses:   3,
    startMisses: 0,
    baseSpeed:   1.2,
    speedInc:    0.08,
    maxSpeed:    5.6,
    speedReset:  1.2,
    spawnBase:   1200,
    spawnMin:    550,
    spawnLevel:  20,
    multiThresh: [0, 8, 15, 25, 35, 50],
    roundSize:   8,
    perfBonus:   3,
    catchRadius: 95,
  },
  vuxen: {
    label:       "Vuxen",
    maxMisses:   3,
    startMisses: 2,      // starts with only 1 life out of max 3
    baseSpeed:   2.5,
    speedInc:    0.22,
    maxSpeed:    11.2,
    speedReset:  2.5,
    spawnBase:   1000,
    spawnMin:    360,
    spawnLevel:  14,
    multiThresh: [0, 5, 10, 15, 20, 25],
    roundSize:   10,
    perfBonus:   5,
    catchRadius: 58,
  },
};

// ══════════════════════════════════════
//  SPRITE TYPES
// ══════════════════════════════════════
const STAR_TYPES = [
  { emoji: "⭐", pts: 5, size: 38, bad: false },
  { emoji: "🌠", pts: 5, size: 36, bad: false },
  { emoji: "💫", pts: 5, size: 40, bad: false },
  { emoji: "🌟", pts: 5, size: 42, bad: false },
  { emoji: "✨", pts: 5, size: 38, bad: false },
];
const STAR_WEIGHTS = [40, 25, 18, 12, 5];

// Catching a bad sprite breaks streak. 💀 costs a life, 🌑 costs points only.
const BAD_TYPES = [
  { emoji: "💀", pts: 0,   size: 36, bad: true, takesLife: true, label: "💀 -Liv!"  },
  { emoji: "🌑", pts: -15, size: 38, bad: true, label: "🌑 -15p" },
];

// Life sprites — all give a life (no negative ones; skull handles that)
const LIFE_TYPES = [
  { emoji: "💚", size: 34, givesLife: true, life: true },
  { emoji: "☯️",  size: 34, givesLife: true, life: true },
  { emoji: "🌺", size: 34, givesLife: true, life: true },
];

const POWERUP_TYPES = [
  { id: "magnet",  emoji: "🧲", label: "Magnet!",   dur: 6000, color: "#67e8f9" },
  { id: "slowmo",  emoji: "⏱️",  label: "Slow-mo!",  dur: 5000, color: "#c4b5fd", onlyDiff: "vuxen" },
  { id: "rainbow", emoji: "🌈", label: "Regnbåge!", dur: 7000, color: "#ff9de2" },
];

// Party-mode sprite types (used during "party" challenge round)
const PARTY_TYPES = [
  { emoji: "🎂", pts: 5, size: 40 },
  { emoji: "🍰", pts: 5, size: 38 },
  { emoji: "🎉", pts: 5, size: 38 },
  { emoji: "🎊", pts: 5, size: 38 },
  { emoji: "🥳", pts: 5, size: 40 },
  { emoji: "🎈", pts: 5, size: 36 },
  { emoji: "🎁", pts: 5, size: 38 },
  { emoji: "🎀", pts: 5, size: 36 },
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
// Per-difficulty spawn rates. Barn uses gentler curves than Vuxen.
function lifeChance(difficulty, totalCaught){
  if(difficulty==="vuxen") return Math.max(0.012 - totalCaught*0.0001,  0.005);
  return                          Math.max(0.015 - totalCaught*0.00005, 0.008);
}
function skullChance(difficulty, level){
  if(difficulty==="vuxen") return Math.min(0.07 + level*0.015, 0.20);
  return                          Math.min(0.02 + level*0.007, 0.10);
}
function moonChance(difficulty, level){
  if(difficulty==="vuxen") return Math.min(0.14 + level*0.022, 0.36);
  return                          Math.min(0.04 + level*0.008, 0.15);
}

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

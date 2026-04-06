"use strict";
// ══════════════════════════════════════
//  VISUAL EFFECTS
//  Depends on: config.js (COLORS, CATCH_POPS)
//  Uses global: wrap (set by game.js), _cW / _cH
// ══════════════════════════════════════

// ── Helpers ───────────────────────────────────────────────────────────────────
function rndCol() { return COLORS[Math.floor(Math.random() * COLORS.length)]; }

function _tempEl(cls, css, parent, ttl) {
  const el = document.createElement("div");
  el.className = cls;
  el.style.cssText = css;
  parent.appendChild(el);
  setTimeout(() => { if (el.parentNode) el.parentNode.removeChild(el); }, ttl);
  return el;
}

// ── Screen flash ──────────────────────────────────────────────────────────────
function flashWrap(col) {
  wrap.style.boxShadow = "inset 0 0 0 4px " + col;
  setTimeout(() => wrap.style.boxShadow = "", 220);
}

// ── Particle burst ────────────────────────────────────────────────────────────
function burst(x, y, count = 6) {
  for (let i = 0; i < count; i++) {
    const ang = (i / count) * Math.PI * 2;
    const d   = 32 + Math.random() * 24;
    const sz  = 5  + Math.random() * 6;
    const dur = 0.35 + Math.random() * 0.2;
    _tempEl(
      "particle",
      `width:${sz}px;height:${sz}px;background:${rndCol()};left:${x}px;top:${y}px;` +
      `--tx:${Math.cos(ang) * d}px;--ty:${Math.sin(ang) * d}px;--dur:${dur}s`,
      wrap,
      dur * 1000 + 50
    );
  }
}

// ── Firework ──────────────────────────────────────────────────────────────────
function firework(x, y) {
  const fw  = document.createElement("div");
  fw.className = "firework";
  fw.style.cssText = `left:${x}px;top:${y}px`;
  const col = rndCol();
  for (let i = 0; i < 8; i++) {
    const ang = (i / 8) * Math.PI * 2;
    const d   = 40 + Math.random() * 45;
    const dur = 0.45 + Math.random() * 0.35;
    const sp  = document.createElement("div");
    sp.className = "fw-spark";
    sp.style.cssText =
      `background:${col};--x:${Math.cos(ang) * d}px;--y:${Math.sin(ang) * d}px;--d:${dur}s`;
    fw.appendChild(sp);
  }
  wrap.appendChild(fw);
  setTimeout(() => fw.remove(), 900);
}

function perfectFireworks() {
  for (let i = 0; i < 2; i++) {
    setTimeout(
      () => firework(_cW * 0.2 + Math.random() * _cW * 0.6, _cH * 0.15 + Math.random() * _cH * 0.35),
      i * 280
    );
  }
}

// ── Score pop ─────────────────────────────────────────────────────────────────
function scorePop(x, y, pts, m) {
  const col = m >= 4 ? "#ff80ff" : m >= 3 ? "#ffe066" : m >= 2 ? "#ff9de2" : "#fff";
  const sz  = m >= 3 ? 20 : m >= 2 ? 16 : 13;
  const el  = _tempEl(
    "score-pop",
    `left:${x}px;top:${y}px;font-size:${sz}px;color:${col};text-shadow:0 0 6px ${col};`,
    wrap, 880
  );
  el.textContent = m > 1 ? `+${pts} ×${m}` : `+${pts}`;
}

// ── Catch pop (emoji burst) ───────────────────────────────────────────────────
function catchPop(x, y) {
  const el = _tempEl("catch-pop", `left:${x}px;top:${y}px`, wrap, 680);
  el.textContent = CATCH_POPS[Math.floor(Math.random() * CATCH_POPS.length)];
}

// ── Miss pop ──────────────────────────────────────────────────────────────────
function missPop(x, y) {
  const el = _tempEl("miss-flash", `left:${x}px;top:${y}px`, wrap, 720);
  el.textContent = "💨";
}

// ── Streak word ───────────────────────────────────────────────────────────────
function streakWord(txt, x, y) {
  const el = _tempEl(
    "catch-pop",
    `left:${x}px;top:${y}px;font-size:14px;color:#ffe066;font-family:"Fredoka One",cursive;`,
    wrap, 700
  );
  el.textContent = txt;
}

// ── Unicorn trail ─────────────────────────────────────────────────────────────
function addTrail(x, y) {
  const sz = 12 + Math.random() * 10;
  _tempEl(
    "trail",
    `width:${sz}px;height:${sz}px;background:${rndCol()};left:${x - sz / 2}px;top:${y - sz / 2}px`,
    wrap, 400
  );
}

// ── Banners & Toasts ──────────────────────────────────────────────────────────
let _bannerTimer = null;
function showBanner(txt, cls = "normal", dur = 2000) {
  clearTimeout(_bannerTimer);
  eventBanner.textContent = txt;
  eventBanner.className = cls + " show";
  _bannerTimer = setTimeout(() => eventBanner.classList.remove("show"), dur);
}

let _flashTimer = null;
function flashCentre(txt, dur = 1400) {
  clearTimeout(_flashTimer);
  centreFlash.textContent = txt;
  centreFlash.style.opacity = "1";
  _flashTimer = setTimeout(() => centreFlash.style.opacity = "0", dur);
}

let _perfectToastTimer = null;
function showPerfectToast(txt, dur = 2800) {
  clearTimeout(_perfectToastTimer);
  perfectToast.textContent = txt;
  perfectToast.style.opacity = "1";
  _perfectToastTimer = setTimeout(() => perfectToast.style.opacity = "0", dur);
}

let _levelToastTimer = null;
function showLevelToast(txt, dur = 2200) {
  clearTimeout(_levelToastTimer);
  const el = document.getElementById("level-toast");
  el.textContent = txt;
  el.style.opacity = "1";
  _levelToastTimer = setTimeout(() => el.style.opacity = "0", dur);
}

// ── Combo flash ───────────────────────────────────────────────────────────────
let _comboFlashTimer = null;
function showComboFlash(m) {
  clearTimeout(_comboFlashTimer);
  const col = MULTI_COLORS[Math.min(m, MULTI_COLORS.length - 1)] || "#ffe066";
  comboFlash.style.cssText = `color:${col};text-shadow:0 0 20px ${col};opacity:1;`;
  comboFlash.textContent = "×" + m + "!";
  _comboFlashTimer = setTimeout(() => comboFlash.style.opacity = "0", 1000);
}

// ── Combo ring ────────────────────────────────────────────────────────────────
function updateComboRing(m) {
  comboRing.className = "";
  if (difficulty === "vuxen") comboRing.classList.add("vuxen");
  if (m > 1) comboRing.classList.add(RING_CLASSES[Math.min(m, RING_CLASSES.length - 1)]);
}

// ── Speed bar ─────────────────────────────────────────────────────────────────
const SPD_COL_LO  = "linear-gradient(90deg,#a78bfa,#c4b5fd)";
const SPD_COL_MID = "linear-gradient(90deg,#a78bfa,#ff80ff)";
const SPD_COL_HI  = "linear-gradient(90deg,#ff80ff,#ff4444)";
let _lastSpeedPct = -1, _lastSpeedCol = "";
function updateSpeedBar() {
  const pct = Math.min((currentSpeed - cfg.baseSpeed) / (cfg.maxSpeed - cfg.baseSpeed) * 100, 100);
  const col = pct > 75 ? SPD_COL_HI : pct > 45 ? SPD_COL_MID : SPD_COL_LO;
  const p   = Math.round(pct);
  if (p   !== _lastSpeedPct) { speedFill.style.width = p + "%"; _lastSpeedPct = p; }
  if (col !== _lastSpeedCol) { speedFill.style.background = col; _lastSpeedCol = col; }
}

// ── Round bar ─────────────────────────────────────────────────────────────────
let _lastRoundPct = -1, _lastRoundClass = "", _lastRoundLL = "", _lastRoundLR = "";
function updateRoundBar() {
  const rs    = typeof getRoundSize === "function" ? getRoundSize() : cfg.roundSize;
  const total = roundCaught + roundMissed;
  const pct   = Math.round(Math.min(total / rs * 100, 100));
  const cls   = roundMissed === 0 ? "clean" : "dirty";
  const ll    = "Omgång " + roundNum + (roundMissed === 0 ? " 💚" : " 💔");
  const lr    = total + "/" + rs + " 🎯";
  if (pct !== _lastRoundPct)   { roundFill.style.width = pct + "%"; _lastRoundPct = pct; }
  if (cls !== _lastRoundClass) { roundFill.className = cls;          _lastRoundClass = cls; }
  if (ll  !== _lastRoundLL)    { roundLabelL.textContent = ll;       _lastRoundLL = ll; }
  if (lr  !== _lastRoundLR)    { roundLabelR.textContent = lr;       _lastRoundLR = lr; }
}

// ── Hearts ────────────────────────────────────────────────────────────────────
function buildHearts() {
  heartsEl.innerHTML = "";
  for (let i = 0; i < cfg.maxMisses; i++) {
    const h = document.createElement("span");
    h.className = "heart"; h.textContent = "❤️";
    heartsEl.appendChild(h);
  }
}
function updateHearts() {
  [...heartsEl.querySelectorAll(".heart")].forEach((h, i) =>
    h.classList.toggle("lost", i >= cfg.maxMisses - misses)
  );
}

// ── BG decor ──────────────────────────────────────────────────────────────────
function makeBgStars() {
  for (let i = 0; i < 28; i++) {
    const s = document.createElement("div");
    s.className = "bg-star";
    const sz = 1 + Math.random() * 3;
    s.style.cssText =
      `width:${sz}px;height:${sz}px;left:${Math.random()*100}%;top:${Math.random()*65}%;` +
      `--d:${2+Math.random()*4}s;animation-delay:${Math.random()*4}s`;
    wrap.appendChild(s);
  }
}

let cloudEls = [];
function makeClouds() {
  if (cloudEls.length > 0) return;
  for (let i = 0; i < 4; i++) {
    const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    const w = 120 + Math.random() * 160, h = 50 + Math.random() * 30;
    svg.setAttribute("width", w); svg.setAttribute("height", h);
    svg.setAttribute("viewBox", `0 0 ${w} ${h}`);
    svg.style.cssText = "position:absolute;pointer-events:none;";
    [[w*.5,h*.65,w*.46,h*.36],[w*.28,h*.75,w*.28,h*.26],[w*.72,h*.75,w*.28,h*.23]].forEach(([cx,cy,rx,ry]) => {
      const e = document.createElementNS("http://www.w3.org/2000/svg", "ellipse");
      e.setAttribute("cx", cx); e.setAttribute("cy", cy);
      e.setAttribute("rx", rx); e.setAttribute("ry", ry);
      e.setAttribute("fill", `rgba(255,255,255,${0.10+Math.random()*0.13})`);
      svg.appendChild(e);
    });
    wrap.insertBefore(svg, unicornEl);
    const x = Math.random() * (_cW || wrap.clientWidth);
    const y = 30 + Math.random() * ((_cH || wrap.clientHeight) * 0.42);
    svg.style.left = x + "px"; svg.style.top = y + "px";
    cloudEls.push({ el: svg, x, y, speed: 0.12 + Math.random() * 0.18 });
  }
}

// ── Level theme ───────────────────────────────────────────────────────────────
let currentThemeIdx = -1;
function applyTheme(lvl) {
  const theme = getTheme(lvl);
  const idx   = LEVEL_THEMES.indexOf(theme);
  if (idx === currentThemeIdx) return;
  currentThemeIdx = idx;
  wrap.style.background = theme.bg;
  if (lvl > 1) showBanner(theme.name + " — Nivå " + lvl, "normal", 2000);
}
function resetTheme() { currentThemeIdx = -1; }

// ── Power-up bar ──────────────────────────────────────────────────────────────
function renderPowerupBar(activePowerups) {
  // Managed externally via DOM elements stored in activePowerups entries
}

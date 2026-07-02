"use strict";
// ══════════════════════════════════════
//  DOM REFS  (must match index.html IDs)
// ══════════════════════════════════════
const wrap        = document.getElementById("game-wrap");
const unicornEl   = document.getElementById("unicorn");
const comboRing   = document.getElementById("combo-ring");
const comboFlash  = document.getElementById("combo-flash");
const scoreEl     = document.getElementById("score-val");
const levelEl     = document.getElementById("level-val");
const streakEl    = document.getElementById("streak-val");
const multiEl     = document.getElementById("multi-val");
const heartsEl    = document.getElementById("hearts-display");
const speedFill   = document.getElementById("speed-fill");
const roundFill   = document.getElementById("round-fill");
const roundLabelL = document.getElementById("round-label-left");
const roundLabelR = document.getElementById("round-label-right");
const eventBanner = document.getElementById("event-banner");
const centreFlash = document.getElementById("centre-flash");
const perfectToast= document.getElementById("perfect-toast");
const overlay     = document.getElementById("overlay");
const playBtn     = document.getElementById("play-btn");
const nameInput   = document.getElementById("name-input");
const joyZone     = document.getElementById("joystick-zone");
const joyKnob     = document.getElementById("joy-knob");
const joyTrack    = document.getElementById("joy-track");
const quitBtn     = document.getElementById("quit-btn");
const soundBtn    = document.getElementById("sound-btn");

// ══════════════════════════════════════
//  GAME STATE
// ══════════════════════════════════════
let score=0, level=1, playing=false;
let ux=0, uy=0, streak=0, maxStreak=0;
let multiplier=1, maxMulti=1;
let misses=0, totalCaught=0, perfectRounds=0;
let roundCaught=0, roundMissed=0, roundNum=1;
let goodCatchesForLife=0;
let currentSpeed=DIFF.barn.baseSpeed;
let stars=[], spawnTimer=null, rafId=null, trailTimer=0;
let lastTime=0, playerName="Ellie";
let difficulty="barn", cfg=DIFF.barn;
let activeChallenge=null, nextChallengeAt=30, challengeEndTimer=null;
let activePowerups={};
let _cW=0, _cH=0;
let _lastUx=-1, _lastUy=-1;
let globalScoreCache={barn:null, vuxen:null};
let tlShowDiff="barn";
let bonusTimer=null;

// Rainbow trail
const RAINBOW_TRAIL_LEN = 22;
const RAINBOW_HUE = ["#ff4444","#ff8800","#ffee00","#44dd44","#44aaff","#aa44ff","#ff44cc"];
let rainbowPositions = []; // {x,y} ring buffer
let rainbowDots = [];      // pre-created DOM elements (reused each frame)

const keys={};
let joyTouchId=null, joyTargetX=null;

const UNICORN_SPEED=7, FIXED_Y_FRAC=0.70;

// ══════════════════════════════════════
//  TOPLIST (localStorage, per difficulty)
// ══════════════════════════════════════
function tlKey(d){ return "unicorn_top_"+d; }
function loadTop(d=difficulty){
  try{ return JSON.parse(localStorage.getItem(tlKey(d))||"[]"); }catch{ return []; }
}
function saveTop(l,d=difficulty){
  try{ localStorage.setItem(tlKey(d),JSON.stringify(l)); }catch{}
}
function addToTop(name,sc,ms,mx,pf,caught,d=difficulty){
  const l=loadTop(d);
  l.push({name,score:sc,maxStreak:ms,maxMulti:mx,perfect:pf,caught});
  l.sort((a,b)=>b.score-a.score);
  const t=l.slice(0,10); saveTop(t,d); return t;
}

const MEDALS=["🥇","🥈","🥉","4","5","6","7","8","9","10"];
// Names from the server may contain HTML — always escape before innerHTML
function escHtml(s){
  return String(s).replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;");
}
function renderTop(d, highlightName="", incomingList=undefined){
  tlShowDiff=d;
  document.querySelectorAll(".tl-tab").forEach(t=>t.classList.toggle("active",t.dataset.d===d));
  const status=document.getElementById("tl-global-status");
  const head=document.getElementById("tl-head");
  const body=document.getElementById("tl-body");
  const showDiff=(d==="alla");

  // Update header columns
  head.innerHTML=showDiff
    ? `<tr><th>#</th><th>Namn</th><th>Läge</th><th>Poäng</th><th>🔥</th></tr>`
    : `<tr><th>#</th><th>Namn</th><th>Poäng</th><th>🔥</th></tr>`;

  let list;
  if(d==="alla"){
    if(incomingList===undefined){
      if(globalScoreCache.alla){
        list=globalScoreCache.alla; status.textContent="";
      } else {
        body.innerHTML=`<tr><td colspan="5" class="tl-empty">Laddar…</td></tr>`;
        status.textContent="";
        fetchAllScores().then(r=>{
          if(r){ globalScoreCache.alla=r; if(tlShowDiff==="alla") renderTop("alla","",r); }
          else { body.innerHTML=`<tr><td colspan="5" class="tl-empty">Ej tillgänglig</td></tr>`; }
        });
        return;
      }
    } else {
      list=incomingList||[];
      status.textContent=list.length?"":"Ej tillgänglig";
    }
  } else {
    list=loadTop(d);
    status.textContent="";
  }

  if(!list.length){
    body.innerHTML=`<tr><td colspan="${showDiff?5:4}" class="tl-empty">Inga poäng än!</td></tr>`;
    return;
  }
  body.innerHTML=list.slice(0,10).map((e,i)=>{
    const isMe=e.name===highlightName&&(d==="alla"||d===difficulty);
    const diffBadge=showDiff
      ? `<td><span class="diff-badge ${e.difficulty||''}">${e.difficulty==="barn"?"🐣 Barn":"🔥 Vuxen"}</span></td>`
      : "";
    return `<tr class="${isMe?'me':''}">
      <td>${MEDALS[i]||i+1}</td>
      <td>${escHtml(e.name||"Anonym")}</td>
      ${diffBadge}
      <td>${e.score}</td>
      <td>${e.maxStreak||0}</td>
    </tr>`;
  }).join("");
}
// ══════════════════════════════════════
//  POWER-UPS
// ══════════════════════════════════════
function _setPowerupClass(){
  unicornEl.classList.toggle("has-magnet",  !!activePowerups.magnet);
  unicornEl.classList.toggle("has-slowmo",  !!activePowerups.slowmo);
  unicornEl.classList.toggle("has-rainbow", !!activePowerups.rainbow);
}
function activatePowerup(type){
  const EXTEND_MS=5000;
  let durMs=type.dur;
  const existing=activePowerups[type.id];
  if(existing){
    // Already active — add 5s to remaining time instead of resetting
    clearTimeout(existing.timer); clearInterval(existing.tick);
    const msLeft=existing.endsAt-Date.now();
    durMs=Math.max(msLeft,0)+EXTEND_MS;
  }
  const pu=document.getElementById("powerup-bar");
  const old=document.getElementById("pu-el-"+type.id);
  if(old) old.remove();
  const puEl=document.createElement("div");
  puEl.className="pu-active"; puEl.id="pu-el-"+type.id;
  puEl.innerHTML=`${type.emoji} ${type.label} <span class="pu-timer" id="pu-t-${type.id}"></span>`;
  pu.appendChild(puEl);
  let remaining=Math.ceil(durMs/1000);
  const timerEl=()=>document.getElementById("pu-t-"+type.id);
  if(timerEl()) timerEl().textContent=remaining+"s";
  const tick=setInterval(()=>{
    remaining--;
    const t=timerEl(); if(t) t.textContent=remaining+"s";
  },1000);
  const endTimer=setTimeout(()=>{
    clearInterval(tick); puEl.remove(); delete activePowerups[type.id];
    if(type.id==="rainbow"){ rainbowPositions=[]; hideRainbowDots(); }
    _setPowerupClass();
  },durMs);
  activePowerups[type.id]={timer:endTimer,tick,el:puEl,endsAt:Date.now()+durMs};
  _setPowerupClass();
  showBanner(existing?`${type.emoji} +5s!`:`${type.emoji} ${type.label}`,"powerup",1600);
  playMultiUp(2);
}
function clearAllPowerups(){
  Object.values(activePowerups).forEach(p=>{
    clearTimeout(p.timer); clearInterval(p.tick); if(p.el&&p.el.parentNode) p.el.remove();
  });
  activePowerups={};
  rainbowPositions=[]; hideRainbowDots();
  _setPowerupClass();
  const pb=document.getElementById("powerup-bar");
  if(pb) pb.innerHTML="";
}
function applyMagnet(s){
  if(!activePowerups.magnet||s.kind!=="good") return; // only attract good sprites
  const dx=ux-s.x, dy=uy-s.y, dist=Math.sqrt(dx*dx+dy*dy);
  if(dist<320&&dist>1){
    const force=8*(1-dist/320); // stronger when closer
    s.x+=dx/dist*force; s.y+=dy/dist*force; s.el.style.left=s.x+"px";
  }
}

// ── Rainbow trail ─────────────────────────────────────────────────────────────
function initRainbowDots(){
  for(let i=0;i<RAINBOW_TRAIL_LEN;i++){
    const d=document.createElement("div");
    d.className="rainbow-dot"; d.style.display="none";
    wrap.appendChild(d); rainbowDots.push(d);
  }
}
function hideRainbowDots(){ rainbowDots.forEach(d=>d.style.display="none"); }
function updateRainbowTrail(){
  if(!activePowerups.rainbow){ hideRainbowDots(); rainbowPositions=[]; return; }
  rainbowPositions.push({x:ux,y:uy});
  if(rainbowPositions.length>RAINBOW_TRAIL_LEN) rainbowPositions.shift();
  const len=rainbowPositions.length;
  rainbowDots.forEach((d,i)=>{
    const pos=rainbowPositions[len-1-i];
    if(!pos){ d.style.display="none"; return; }
    const frac=1-(i/RAINBOW_TRAIL_LEN);
    const sz=Math.round(8+frac*30);
    const col=RAINBOW_HUE[i%RAINBOW_HUE.length];
    d.style.cssText=`display:block;width:${sz}px;height:${sz}px;background:${col};`+
      `opacity:${(frac*0.75).toFixed(2)};left:${pos.x-sz/2}px;top:${pos.y-sz/2}px;`+
      `filter:blur(${Math.round((1-frac)*3)}px);`;
  });
}
function rainbowCatches(s){
  if(!activePowerups.rainbow||!rainbowPositions.length) return false;
  const r2=40*40;
  for(const p of rainbowPositions){
    const dx=s.x-p.x,dy=s.y-p.y;
    if(dx*dx+dy*dy<r2) return true;
  }
  return false;
}

// ── Star sparkle tail (pooled — avoids DOM churn at high spawn rates) ─────────
const TAIL_POOL_SIZE=48;
const TAIL_COLORS=["#ffe066","#ff9de2","#a78bfa","#67e8f9","#86efac","#fcd34d"];
let tailDots=[], tailIdx=0;
function initTailDots(){
  for(let i=0;i<TAIL_POOL_SIZE;i++){
    const d=document.createElement("div");
    d.className="star-tail-dot"; d.style.display="none";
    wrap.appendChild(d); tailDots.push(d);
  }
}
function hideTailDots(){ tailDots.forEach(d=>{ if(d._anim) d._anim.cancel(); d.style.display="none"; }); }
function emitTailDot(x,y){
  const d=tailDots[tailIdx]; tailIdx=(tailIdx+1)%TAIL_POOL_SIZE;
  if(d._anim) d._anim.cancel();
  d.style.display="block";
  d.style.background=TAIL_COLORS[Math.floor(Math.random()*TAIL_COLORS.length)];
  d.style.left=(x-4)+"px"; d.style.top=(y-4)+"px";
  d._anim=d.animate(
    [{opacity:0.72,transform:"scale(1)"},{opacity:0,transform:"scale(0.15)"}],
    {duration:420,easing:"ease-out"}
  );
  d._anim.onfinish=()=>{ d.style.display="none"; };
}

// ══════════════════════════════════════
//  DYNAMIC ROUND SIZE (grows with level)
// ══════════════════════════════════════
function getRoundSize(){ return Math.min(cfg.roundSize+Math.floor((level-1)*1.5),25); }
function getRoundBonus(){ return cfg.perfBonus+Math.floor((level-1)*0.8); }

// ══════════════════════════════════════
//  LIVE RANK INDICATOR
// ══════════════════════════════════════
// Toplist snapshot taken at game start — avoids JSON-parsing localStorage on every catch
let _rankTop=[];
const rankHudEl=document.getElementById("rank-hud");
function updateRankHud(){
  if(!rankHudEl) return;
  if(!_rankTop.length||score<=0){ rankHudEl.textContent=""; return; }
  if(score>=_rankTop[0].score){ rankHudEl.textContent="🥇 Nytt rekord!"; return; }
  const rank=_rankTop.findIndex(e=>score>=e.score);
  rankHudEl.textContent=(rank>=0&&rank<3)?[" 🥇"," 🥈"," 🥉"][rank]:"";
}

// ══════════════════════════════════════
//  MULTIPLIER
// ══════════════════════════════════════
function getMultiplier(str){
  const t=cfg.multiThresh;
  for(let i=t.length-1;i>=0;i--) if(str>=t[i]) return i+1;
  return 1;
}

// ══════════════════════════════════════
//  SPAWN
// ══════════════════════════════════════
function spawnOneStar(xOver){
  if(!playing) return;
  let type, kind="good";
  if(activeChallenge){
    // Bonus round — only good sprites, no bad/life
    kind="good";
    if(activeChallenge.partyMode){
      type=PARTY_TYPES[Math.floor(seededRng()*PARTY_TYPES.length)];
    } else {
      type=activeChallenge.forceType!==undefined?STAR_TYPES[activeChallenge.forceType]:pickStarType();
    }
  } else {
    const r=seededRng();
    const lifeP=lifeChance(difficulty, totalCaught);
    const skulP=skullChance(difficulty, level);
    const moonP=moonChance(difficulty, level);
    if(r < lifeP){
      kind="life"; type=LIFE_TYPES[Math.floor(seededRng()*LIFE_TYPES.length)];
    } else if(r < lifeP+skulP){
      kind="bad"; type=BAD_TYPES[0]; // 💀 life-taker
    } else if(r < lifeP+skulP+moonP){
      kind="bad"; type=BAD_TYPES[1]; // 🌑 point penalty
    } else {
      kind="good"; type=pickStarType();
    }
  }
  const el=document.createElement("div");
  el.className=kind==="bad"?"fbad":"fstar";
  if(activeChallenge?.partyMode) el.classList.add("party-sprite");
  else if(kind==="bad"){
    if(type.takesLife) el.classList.add("skull-sprite");
    else               el.classList.add("moon-sprite");
  } else {
    el.style.animationDelay=`-${(Math.random()*1.6).toFixed(2)}s`;
  }
  el.textContent=type.emoji;
  const sz=type.size+(kind==="good"?(activeChallenge?.sizeBoost||0):0);
  el.style.fontSize=sz+"px";
  const x=xOver!==undefined?xOver:28+Math.random()*((_cW||wrap.clientWidth)-60);
  el.style.left=x+"px"; el.style.top="-55px";
  wrap.insertBefore(el,centreFlash);
  stars.push({el,x:x+sz/2,y:-55,speed:currentSpeed+(Math.random()*0.3-0.15),type,kind,trailEmit:0});
}
function spawnPowerupSprite(){
  if(Math.random()>POWERUP_SPAWN_CHANCE) return;
  const available=POWERUP_TYPES.filter(t=>(!t.onlyDiff||t.onlyDiff===difficulty));
  if(!available.length) return;
  const type=available[Math.floor(Math.random()*available.length)];
  const el=document.createElement("div");
  el.className="fstar";
  el.textContent=type.emoji;
  el.style.fontSize="40px";
  el.style.filter=`drop-shadow(0 0 10px ${type.color}) drop-shadow(0 0 4px #fff)`;
  const x=28+Math.random()*((_cW||wrap.clientWidth)-60);
  el.style.left=x+"px"; el.style.top="-50px";
  wrap.insertBefore(el,centreFlash);
  stars.push({el,x:x+20,y:-50,speed:currentSpeed*0.7,type,kind:"powerup"});
}
function spawnStar(){
  if(!playing) return;
  spawnOneStar();
  const rw=()=>36+Math.random()*((_cW||wrap.clientWidth)-80);
  if(activeChallenge?.doubleSpawn){
    setTimeout(()=>spawnOneStar(rw()),200);
  } else if(activeChallenge?.partyMode){
    // Party mode: Barn gets double, Vuxen gets triple
    setTimeout(()=>spawnOneStar(rw()),200);
    if(difficulty==="vuxen" || level>=8) setTimeout(()=>spawnOneStar(rw()),380);
  } else if(difficulty==="vuxen"){
    setTimeout(()=>spawnOneStar(rw()),180);
    if(level>=4) setTimeout(()=>spawnOneStar(rw()),360);
  } else if(level>=12){
    // Barn gets multi-spawn from level 12
    setTimeout(()=>spawnOneStar(rw()),220);
    if(level>=18) setTimeout(()=>spawnOneStar(rw()),440);
  }
  spawnPowerupSprite();
}
function getSpawnInterval(){
  return Math.max(cfg.spawnMin, cfg.spawnBase - level * cfg.spawnLevel);
}
function scheduleSpawn(){
  clearTimeout(spawnTimer); spawnTimer=null;
  function tick(){
    if(!playing) return;
    spawnStar();
    spawnTimer=setTimeout(tick, getSpawnInterval());
  }
  spawnTimer=setTimeout(tick, getSpawnInterval());
}
// ══════════════════════════════════════
//  CATCH HANDLERS
// ══════════════════════════════════════
function onCatch(starType,x,y){
  const pts=starType.pts*multiplier;
  score+=pts; scoreEl.textContent=score;
  streak++; totalCaught++; roundCaught++;
  goodCatchesForLife++;
  if(streak>maxStreak) maxStreak=streak;
  streakEl.textContent=streak;
  currentSpeed=Math.min(currentSpeed+cfg.speedInc,cfg.maxSpeed);
  updateSpeedBar();
  const newM=getMultiplier(streak), oldM=multiplier; multiplier=newM;
  if(newM>maxMulti) maxMulti=newM;
  multiEl.textContent="×"+newM;
  updateComboRing(newM);
  if(newM>oldM){ showComboFlash(newM); playMultiUp(newM); }
  const nl=1+Math.floor(totalCaught/10);
  if(nl>level){ level=nl; levelEl.textContent=level; doLevelUp(); }
  burst(x,y,10);
  flashWrap("rgba(255,220,0,0.30)");
  scorePop(x,y-20,pts,oldM);
  catchPop(x,y);
  updateRankHud();
  const mt=cfg.multiThresh;
  if(streak===mt[1]){ playStreak5(); streakWord("×2! 🌟",x,y); }
  else if(streak===mt[2]){ playWow(); streakWord("×3! 💖",x,y); }
  else if(streak===mt[3]){ playWow(); streakWord("×4! 🔥",x,y); }
  else if(streak===mt[4]){ playWow(); streakWord("×5! 🦄",x,y); }
  else if(streak===mt[5]){ playWow(); streakWord("×6! 👑",x,y); }
  else if(streak>=3) streakWord(STREAK_WORDS[Math.min(Math.floor((streak-3)/4),STREAK_WORDS.length-1)],x,y);
  playCatch(1+streak*0.006);
  updateRoundBar();
  checkRoundEnd();
  if(difficulty==="barn"&&misses>0&&goodCatchesForLife>=12){
    misses--; goodCatchesForLife=0;
    updateHearts();
    showBanner("💖 Bonusliv för fin streak!","normal",1400);
    const d=document.createElement("div"); d.className="score-pop";
    d.textContent="💖 +1 Liv"; d.style.cssText=`left:${x}px;top:${y}px;font-size:20px;color:#f9a8d4;`;
    wrap.appendChild(d); setTimeout(()=>{if(d.parentNode)d.parentNode.removeChild(d);},900);
  }
  if(totalCaught>=nextChallengeAt&&!activeChallenge) activateChallenge();
}
function onMiss(sx,sy){
  // Missing a sprite has no penalty — only catching bad sprites costs anything.
  roundMissed++; totalCaught++;
  goodCatchesForLife=0;
  updateRoundBar(); checkRoundEnd();
  missPop(sx,sy); playMiss();
}
// Floating text at a sprite position (danger/reward feedback)
function floatPop(x,y,txt,color,cls="miss-flash",size=20){
  const d=document.createElement("div"); d.className=cls;
  d.textContent=txt;
  d.style.cssText=`left:${x}px;top:${y}px;color:${color};font-size:${size}px;`;
  wrap.appendChild(d); setTimeout(()=>d.remove(),900);
}
function onBadCatch(type,x,y){
  // Always break streak on catching any bad sprite — speed is NOT reset
  streak=0; streakEl.textContent=0;
  goodCatchesForLife=0;
  multiplier=1; multiEl.textContent="×1";
  updateComboRing(1);
  updateSpeedBar();

  if(type.takesLife){
    // 💀 — costs a life
    misses++; updateHearts();
    playBadCatch(true);
    wrap.style.outline="4px solid rgba(255,0,0,0.7)";
    setTimeout(()=>wrap.style.outline="",300);
    showBanner("💀 -1 Liv! Streak bruten!","danger",2000);
    flashCentre("💀 MINUS ETT LIV!",1200);
    floatPop(x,y,"💀 -Liv!","#ff4444");
    if(misses>=cfg.maxMisses) endGame();
  } else {
    // 🌑 — big point penalty
    playBadCatch(false);
    score=Math.max(0,score+type.pts); scoreEl.textContent=score;
    flashWrap("rgba(255,0,0,0.45)");
    showBanner(type.emoji+" "+type.pts+"p  Streak bruten!","danger",1400);
    floatPop(x,y,type.emoji+" "+type.pts+"p","#ff4444","miss-flash",18);
  }
}
function onLifeCatch(type,x,y){
  // All life sprites give a life (skull is the only life-taker)
  if(misses>0){ misses--; updateHearts(); }
  playMultiUp(2); showBanner("💚 +1 Liv!","normal",1500);
  floatPop(x,y,"💚 +❤️","#86efac","score-pop");
  burst(x,y,10);
}

// ══════════════════════════════════════
//  ROUND SYSTEM
// ══════════════════════════════════════
function checkRoundEnd(){
  const rs=getRoundSize();
  if(roundCaught+roundMissed<rs) return;
  const wasPerfect=roundMissed===0;
  if(wasPerfect){
    perfectRounds++;
    const bonus=getRoundBonus()*multiplier;
    score+=bonus; scoreEl.textContent=score;
    playPerfect();
    showBanner("🌟 PERFEKT omgång "+roundNum+"!  +"+bonus+"p 🎉","perfect",2800);
    perfectFireworks();
  } else {
    showBanner(`Omgång ${roundNum}: ${roundCaught}/${rs} ✅ ${roundMissed} ❌`,"round-end",1800);
  }
  roundNum++; roundCaught=0; roundMissed=0;
  updateRoundBar();
}

// ══════════════════════════════════════
//  CHALLENGE
// ══════════════════════════════════════
function activateChallenge(){
  clearTimeout(challengeEndTimer);
  const c=CHALLENGES[Math.floor(seededRng()*CHALLENGES.length)];
  activeChallenge=c;
  wrap.classList.add("in-bonus");
  const badge=document.getElementById("bonus-badge");
  if(badge){ badge.textContent="🎯 "+c.label; badge.classList.add("show"); }
  showBanner("🎯 BONUS: "+c.label,"challenge",3200);
  flashCentre("🎯 BONUSRUNDA!\n"+c.label,1800);
  playWow(); playTune(c.partyMode?"party":c.music==="golden"?"golden":"challenge");
  // Duration shrinks at higher levels (max 18s → min 8s)
  const bonusDur=Math.max(18000-level*200, 8000);
  challengeEndTimer=setTimeout(()=>{
    activeChallenge=null;
    wrap.classList.remove("in-bonus");
    if(badge) badge.classList.remove("show");
    playTune("birthday");
  },bonusDur);
  // Gap grows with level so high-level players face more danger
  nextChallengeAt=totalCaught+cfg.roundSize*4+level*4+Math.floor(seededRng()*15);
}
// ══════════════════════════════════════
//  GAME LOOP
// ══════════════════════════════════════
function loop(ts){
  if(!playing) return;
  const dt=Math.min(ts-lastTime,50); lastTime=ts;

  const spd=UNICORN_SPEED*(dt/16);
  if(joyTouchId!==null&&joyTargetX!==null) ux=joyTargetX;
  else {
    if(keys["ArrowLeft"]||keys["a"]) ux=Math.max(36,ux-spd);
    if(keys["ArrowRight"]||keys["d"]) ux=Math.min(_cW-36,ux+spd);
  }
  uy=_cH*FIXED_Y_FRAC;
  if(ux!==_lastUx||uy!==_lastUy){
    unicornEl.style.left=ux+"px"; unicornEl.style.top=uy+"px";
    comboRing.style.left=ux+"px"; comboRing.style.top=uy+"px";
    _lastUx=ux; _lastUy=uy;
  }

  trailTimer+=dt;
  const moving=joyTouchId!==null||keys["ArrowLeft"]||keys["ArrowRight"]||keys["a"]||keys["d"];
  if(trailTimer>80&&moving){ addTrail(ux,uy); trailTimer=0; }

  updateRainbowTrail();

  const speedMult=activePowerups.slowmo?0.4:1.0;
  const cr=cfg.catchRadius+((activeChallenge?.sizeBoost||0)>0?12:0);
  const cr2=cr*cr;

  for(let i=stars.length-1;i>=0;i--){
    const s=stars[i];
    s.y+=s.speed*speedMult*(dt/16);
    applyMagnet(s);
    s.el.style.top=s.y+"px";

    // Sparkle tail for regular good stars (not party/bad)
    if(s.kind==="good"&&!activeChallenge?.partyMode&&s.y>0){
      s.trailEmit=(s.trailEmit||0)+dt;
      if(s.trailEmit>30){ s.trailEmit=0; emitTailDot(s.x,s.y); }
    }

    const dx=s.x-ux, dy=s.y-uy;
    const caught=dx*dx+dy*dy<cr2||rainbowCatches(s);
    if(caught){
      s.el.remove(); stars.splice(i,1);
      if(s.kind==="bad")        onBadCatch(s.type,s.x,s.y);
      else if(s.kind==="life")  onLifeCatch(s.type,s.x,s.y);
      else if(s.kind==="powerup"){ activatePowerup(s.type); burst(s.x,s.y,8); }
      else                      onCatch(s.type,s.x,s.y);
    } else if(s.y>_cH+50){
      s.el.remove(); stars.splice(i,1);
      if(s.kind==="good") onMiss(s.x,_cH-16);
      // bad / life / powerup that fall off: neutral
    }
  }

  cloudEls.forEach(c=>{ c.x+=c.speed*(dt/16); if(c.x>_cW+200) c.x=-200; c.el.style.left=c.x+"px"; });
  rafId=requestAnimationFrame(loop);
}
function refreshBounds(){
  _cW=wrap.clientWidth; _cH=wrap.clientHeight;
}

// Bounds refresh (called on resize/orientation change)
function refreshBounds(){
  _cW=wrap.clientWidth; _cH=wrap.clientHeight;
  ux=Math.min(ux, _cW-36);
  _lastUx=-1; _lastUy=-1; // force unicorn position update on next frame
}

// Joystick
function updateJoy(clientX){
  const r=joyTrack.getBoundingClientRect(), knobR=27;
  const minX=r.left+knobR, maxX=r.right-knobR;
  const cx=Math.max(minX,Math.min(maxX,clientX));
  joyKnob.style.left=(cx-r.left)+"px";
  joyTargetX=36+(cx-minX)/(maxX-minX)*((_cW||wrap.clientWidth)-72);
}
joyZone.addEventListener("touchstart",e=>{e.stopPropagation();if(joyTouchId!==null)return;const t=e.changedTouches[0];joyTouchId=t.identifier;updateJoy(t.clientX);},{passive:true});
joyZone.addEventListener("touchmove", e=>{e.stopPropagation();for(const t of e.changedTouches){if(t.identifier===joyTouchId){updateJoy(t.clientX);break;}}},{passive:true});
joyZone.addEventListener("touchend",  e=>{e.stopPropagation();for(const t of e.changedTouches){if(t.identifier===joyTouchId){joyTouchId=null;joyTargetX=null;break;}}},{passive:true});

// ══════════════════════════════════════
//  LEVEL UP
// ══════════════════════════════════════
function doLevelUp(){
  playLevelUp();
  applyTheme(level);
  showLevelToast("🌈 Nivå "+level+"! ⚡");
  scheduleSpawn();
  setTimeout(()=>{
    if(activeChallenge?.partyMode) playTune("party");
    else playTune("birthday");
  },160);
}
// ══════════════════════════════════════
//  END GAME
// ══════════════════════════════════════
function endGame(){
  if(!playing) return;
  playing=false;
  cancelAnimationFrame(rafId); rafId=null;
  clearTimeout(spawnTimer); spawnTimer=null;
  clearTimeout(challengeEndTimer); clearTimeout(bonusTimer);
  clearAllPowerups(); stopMusic();
  wrap.classList.remove("in-bonus");
  const _bb=document.getElementById("bonus-badge"); if(_bb) _bb.classList.remove("show");
  quitBtn.classList.remove("visible");

  const list=addToTop(playerName,score,maxStreak,maxMulti,perfectRounds,totalCaught,difficulty);
  const rank=list.findIndex(e=>e.name===playerName&&e.score===score)+1;

  document.getElementById("overlay-title").textContent="Spelet slut! 🎊";
  document.getElementById("overlay-sub").textContent="Bra jobbat "+playerName+"! ("+cfg.label+")";
  const rl=document.getElementById("rank-line");
  rl.textContent=(rank===1?"🥇":rank===2?"🥈":rank===3?"🥉":"#"+rank)+" Plats "+rank+" ("+cfg.label+")!";
  rl.style.display="block";
  document.getElementById("stat-score").textContent=score;
  document.getElementById("stat-caught").textContent=totalCaught;
  document.getElementById("stat-streak").textContent=maxStreak;
  document.getElementById("stat-multi").textContent="×"+maxMulti;
  document.getElementById("stat-perfect").textContent=perfectRounds;
  document.getElementById("stats-card").style.display="block";
  document.getElementById("end-medals").style.display="none";
  nameInput.value=playerName;

  renderTop(difficulty, playerName);
  document.querySelectorAll(".tl-tab").forEach(t=>t.classList.toggle("active",t.dataset.d===difficulty));

  playGameOver();
  setTimeout(()=>overlay.classList.remove("hidden"),380);

  // Async: submit to backend → cache the per-difficulty list it returns,
  // then refresh the combined list (the new score may have entered it)
  globalScoreCache.alla=null;
  submitScore({name:playerName,score,difficulty,maxStreak,maxMulti,perfectRounds,caught:totalCaught})
    .then(res=>{
      if(res) globalScoreCache[difficulty]=res.list;
      return fetchAllScores();
    })
    .then(r=>{
      if(r){ globalScoreCache.alla=r; if(tlShowDiff==="alla") renderTop("alla",playerName,r); }
    });
}

// ══════════════════════════════════════
//  START GAME
// ══════════════════════════════════════
function startGame(){
  unlockAudio();
  if(playing) return;
  playerName=nameInput.value.trim()||"Ellie";
  cfg=DIFF[difficulty];

  cancelAnimationFrame(rafId); rafId=null;
  clearTimeout(spawnTimer); spawnTimer=null;
  clearTimeout(challengeEndTimer); clearTimeout(bonusTimer);
  clearAllPowerups(); stopMusic();
  stars.forEach(s=>{ try{ s.el.remove(); }catch{} }); stars=[];
  wrap.querySelectorAll(".trail,.particle,.catch-pop,.miss-flash,.firework,.score-pop").forEach(e=>e.remove());

  score=0; level=1; streak=0; maxStreak=0;
  multiplier=1; maxMulti=1; misses=cfg.startMisses||0; totalCaught=0; perfectRounds=0;
  roundCaught=0; roundMissed=0; roundNum=1;
  activeChallenge=null; nextChallengeAt=cfg.roundSize*8;
  currentSpeed=cfg.baseSpeed;
  resetTheme(); resetRng();

  scoreEl.textContent="0"; levelEl.textContent="1"; streakEl.textContent="0"; multiEl.textContent="×1";
  buildHearts(); updateHearts(); updateSpeedBar(); updateComboRing(1);
  comboFlash.style.opacity="0"; centreFlash.style.opacity="0"; perfectToast.style.opacity="0";
  eventBanner.classList.remove("show");
  document.getElementById("overlay-title").textContent="Grattis på\nFödelsedagen Ellie! 🎂";
  document.getElementById("overlay-sub").textContent="Fånga stjärnorna och samla poäng! ✨";
  document.getElementById("stats-card").style.display="none";
  document.getElementById("end-medals").style.display="none";
  document.getElementById("rank-line").style.display="none";

  _cW=wrap.clientWidth; _cH=wrap.clientHeight;
  ux=_cW/2; uy=_cH*FIXED_Y_FRAC; _lastUx=-1; _lastUy=-1;
  unicornEl.classList.toggle("vuxen", difficulty==="vuxen");
  comboRing.className=difficulty==="vuxen"?"vuxen":"";
  joyTouchId=null; joyTargetX=null; joyKnob.style.left="50%";
  updateRoundBar(); makeClouds(); applyTheme(1);

  // Init pooled effect dots on first play
  if(rainbowDots.length===0) initRainbowDots();
  if(tailDots.length===0) initTailDots();
  rainbowPositions=[]; hideRainbowDots(); hideTailDots();
  _rankTop=loadTop(difficulty);

  overlay.classList.add("hidden");
  quitBtn.classList.add("visible");
  playing=true;
  scheduleSpawn();
  lastTime=performance.now();
  rafId=requestAnimationFrame(loop);

  setTimeout(()=>playTune("birthday"),280);
}

// ══════════════════════════════════════
//  EVENT HANDLERS
// ══════════════════════════════════════
document.addEventListener("keydown", e=>{
  keys[e.key]=true;
  if(e.key==="Escape"&&playing) endGame();
});
document.addEventListener("keyup",   e=>{ keys[e.key]=false; });
window.addEventListener("resize", refreshBounds);
window.addEventListener("orientationchange", refreshBounds);
document.addEventListener("touchstart", ()=>unlockAudio(), {once:true,passive:true});
document.addEventListener("mousedown",  ()=>unlockAudio(), {once:true});

playBtn.addEventListener("click",    ()=>{ unlockAudio(); startGame(); });
playBtn.addEventListener("touchend", e=>{ e.preventDefault(); unlockAudio(); startGame(); });

quitBtn.addEventListener("click",    ()=>{ if(playing) endGame(); });
quitBtn.addEventListener("touchend", e=>{ e.preventDefault(); if(playing) endGame(); });

function updateSoundBtn(){
  const on=isSoundEnabled();
  soundBtn.textContent=on?"🔊":"🔇";
  soundBtn.classList.toggle("muted",!on);
}
function onSoundToggle(e){
  e.preventDefault();
  unlockAudio(); // ensures AudioContext exists on first mobile tap
  toggleSound();
  updateSoundBtn();
}
soundBtn.addEventListener("click",    onSoundToggle);
soundBtn.addEventListener("touchend", onSoundToggle);
updateSoundBtn(); // set initial icon from localStorage

document.querySelectorAll(".diff-btn").forEach(btn=>{
  const select=()=>{
    difficulty=btn.dataset.diff; cfg=DIFF[difficulty];
    document.querySelectorAll(".diff-btn").forEach(b=>b.classList.remove("selected"));
    btn.classList.add("selected");
    renderTop(difficulty);
  };
  btn.addEventListener("click", select);
  btn.addEventListener("touchend", e=>{ e.preventDefault(); select(); });
});

document.querySelectorAll(".tl-tab").forEach(tab=>{
  const show=()=>{
    const d=tab.dataset.d;
    renderTop(d);
  };
  tab.addEventListener("click", show);
  tab.addEventListener("touchend", e=>{ e.preventDefault(); show(); });
});

const howModal=document.getElementById("how-modal");
const howBtn  =document.getElementById("how-btn");
const howClose=document.getElementById("how-close");
const openHow =()=>howModal.classList.add("show");
const closeHow=()=>howModal.classList.remove("show");
howBtn.addEventListener("click", openHow);
howBtn.addEventListener("touchend", e=>{ e.preventDefault(); openHow(); });
howClose.addEventListener("click", closeHow);
howClose.addEventListener("touchend", e=>{ e.preventDefault(); closeHow(); });
howModal.addEventListener("click", e=>{ if(e.target===howModal) closeHow(); });

window.addEventListener("resize", refreshBounds);
window.addEventListener("orientationchange", ()=>setTimeout(refreshBounds, 120));

// ══════════════════════════════════════
//  INIT
// ══════════════════════════════════════
makeBgStars();
buildHearts();
renderTop("barn");

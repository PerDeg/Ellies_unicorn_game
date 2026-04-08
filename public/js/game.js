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

// ══════════════════════════════════════
//  GAME STATE
// ══════════════════════════════════════
let score=0, level=1, playing=false;
let ux=0, uy=0, streak=0, maxStreak=0;
let multiplier=1, maxMulti=1;
let misses=0, totalCaught=0, perfectRounds=0;
let roundCaught=0, roundMissed=0, roundNum=1;
let currentSpeed=DIFF.barn.baseSpeed;
let stars=[], spawnTimer=null, rafId=null, trailTimer=0;
let lastTime=0, playerName="Ellie";
let difficulty="barn", cfg=DIFF.barn;
let activeChallenge=null, nextChallengeAt=30, challengeEndTimer=null;
let activePowerups={};
let _cW=0, _cH=0;
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
function renderTop(d, highlightName="", globalList=undefined){
  tlShowDiff=d;
  document.querySelectorAll(".tl-tab").forEach(t=>t.classList.toggle("active",t.dataset.d===d));
  const status=document.getElementById("tl-global-status");
  const body=document.getElementById("tl-body");
  let list;
  if(d==="global"){
    if(globalList===undefined){
      if(globalScoreCache.barn){
        list=globalScoreCache[difficulty]||globalScoreCache.barn||[];
        status.textContent="";
      } else {
        body.innerHTML=`<tr><td colspan="4" class="tl-empty">Laddar…</td></tr>`;
        status.textContent="";
        fetchGlobalScores(difficulty).then(r=>{
          if(r){ globalScoreCache[difficulty]=r; if(tlShowDiff==="global") renderTop("global","",r); }
          else { body.innerHTML=`<tr><td colspan="4" class="tl-empty">Ej tillgänglig</td></tr>`; }
        });
        return;
      }
    } else {
      list=globalList||[];
      status.textContent=list.length?"":"Ej tillgänglig";
    }
  } else {
    list=loadTop(d);
    status.textContent="";
  }
  if(!list.length){
    body.innerHTML=`<tr><td colspan="4" class="tl-empty">Inga poäng än!</td></tr>`;
    return;
  }
  body.innerHTML=list.map((e,i)=>`
    <tr class="${e.name===highlightName&&(d==="global"||d===difficulty)?'me':''}">
      <td>${MEDALS[i]||i+1}</td>
      <td>${e.name||"Anonym"}</td>
      <td>${e.score}</td>
      <td>${e.maxStreak||0}</td>
    </tr>`).join("");
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
  if(activePowerups[type.id]) clearTimeout(activePowerups[type.id].timer);
  const pu=document.getElementById("powerup-bar");
  const old=document.getElementById("pu-el-"+type.id);
  if(old) old.remove();
  const puEl=document.createElement("div");
  puEl.className="pu-active"; puEl.id="pu-el-"+type.id;
  puEl.innerHTML=`${type.emoji} ${type.label} <span class="pu-timer" id="pu-t-${type.id}"></span>`;
  pu.appendChild(puEl);
  let remaining=Math.ceil(type.dur/1000);
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
  },type.dur);
  activePowerups[type.id]={timer:endTimer,tick,el:puEl};
  _setPowerupClass();
  showBanner(type.emoji+" "+type.label,"powerup",2000);
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
  if(dist<180&&dist>1){ s.x+=dx/dist*3; s.y+=dy/dist*3; s.el.style.left=s.x+"px"; }
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

// ══════════════════════════════════════
//  DYNAMIC ROUND SIZE (grows with level)
// ══════════════════════════════════════
function getRoundSize(){ return Math.min(cfg.roundSize+Math.floor((level-1)*1.5),25); }
function getRoundBonus(){ return cfg.perfBonus+Math.floor((level-1)*0.8); }

// ══════════════════════════════════════
//  LIVE RANK INDICATOR
// ══════════════════════════════════════
function updateRankHud(){
  const el=document.getElementById("rank-hud"); if(!el) return;
  const top=loadTop(difficulty);
  if(!top.length){ el.textContent=""; return; }
  const rank=top.findIndex(e=>score>=e.score);
  if(rank===0)     el.textContent="🥇";
  else if(rank===1)el.textContent="🥈";
  else if(rank===2)el.textContent="🥉";
  else if(rank<0&&score>0) el.textContent="";
  else             el.textContent="";
  // Show label if beating or at top score
  if(score>0&&score>=top[0].score) el.textContent="🥇 Nytt rekord!";
  else if(rank>=0&&rank<3)         el.textContent=[" 🥇"," 🥈"," 🥉"][rank];
  else                              el.textContent="";
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
  if(difficulty==="vuxen"){
    const r=seededRng();
    const badLifeP =Math.min(0.02+totalCaught*0.0008,0.10);
    const goodLifeP=Math.max(0.04-totalCaught*0.0003,0.01);
    const totalLP  =badLifeP+goodLifeP;
    if(r<totalLP){
      kind="life";
      type=seededRng()<badLifeP/totalLP
        ? LIFE_TYPES[0]
        : LIFE_TYPES[1+Math.floor(seededRng()*2)];
    } else if(r<totalLP+badChance(level)){
      kind="bad"; type=BAD_TYPES[Math.floor(seededRng()*BAD_TYPES.length)];
    } else {
      kind="good";
      type=activeChallenge?.forceType!==undefined?STAR_TYPES[activeChallenge.forceType]:pickStarType();
    }
  } else {
    kind="good";
    type=activeChallenge?.forceType!==undefined?STAR_TYPES[activeChallenge.forceType]:pickStarType();
  }
  const el=document.createElement("div");
  el.className=kind==="bad"?"fbad":"fstar";
  el.textContent=type.emoji;
  const sz=type.size+(kind==="good"?(activeChallenge?.sizeBoost||0):0);
  el.style.fontSize=sz+"px";
  const x=xOver!==undefined?xOver:28+Math.random()*((_cW||wrap.clientWidth)-60);
  el.style.left=x+"px"; el.style.top="-55px";
  wrap.insertBefore(el,centreFlash);
  stars.push({el,x:x+sz/2,y:-55,speed:currentSpeed+(Math.random()*0.3-0.15),type,kind});
}
function spawnPowerupSprite(){
  if(Math.random()>POWERUP_SPAWN_CHANCE) return;
  const type=POWERUP_TYPES[Math.floor(Math.random()*POWERUP_TYPES.length)];
  if(activePowerups[type.id]) return;
  const el=document.createElement("div");
  el.className="fstar";
  el.textContent=type.emoji;
  el.style.fontSize="52px";
  el.style.filter=`drop-shadow(0 0 14px ${type.color}) drop-shadow(0 0 6px #fff)`;
  const x=28+Math.random()*((_cW||wrap.clientWidth)-60);
  el.style.left=x+"px"; el.style.top="-60px";
  wrap.insertBefore(el,centreFlash);
  stars.push({el,x:x+26,y:-60,speed:currentSpeed*0.7,type,kind:"powerup"});
}
function spawnStar(){
  if(!playing) return;
  spawnOneStar();
  if(activeChallenge?.doubleSpawn){
    setTimeout(()=>spawnOneStar(36+Math.random()*((_cW||wrap.clientWidth)-80)),200);
  } else if(difficulty==="vuxen"){
    setTimeout(()=>spawnOneStar(36+Math.random()*((_cW||wrap.clientWidth)-80)),180);
    if(level>=4) setTimeout(()=>spawnOneStar(36+Math.random()*((_cW||wrap.clientWidth)-80)),360);
  }
  spawnPowerupSprite();
}
function scheduleSpawn(){
  clearInterval(spawnTimer);
  const interval=Math.max(cfg.spawnMin,cfg.spawnBase-level*cfg.spawnLevel);
  spawnTimer=setInterval(spawnStar,interval);
}
// ══════════════════════════════════════
//  CATCH HANDLERS
// ══════════════════════════════════════
function onCatch(starType,x,y){
  const pts=starType.pts*multiplier;
  score+=pts; scoreEl.textContent=score;
  streak++; totalCaught++; roundCaught++;
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
  scorePop(x,y-20,pts,newM);
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
  if(totalCaught>=nextChallengeAt&&!activeChallenge) activateChallenge();
}
function onMiss(sx,sy){
  if(streak>=5) flashCentre("Hoppsan! 😅\nStreak bruten…",900);
  streak=0; streakEl.textContent=0;
  multiplier=1; multiEl.textContent="×1";
  updateComboRing(1);
  currentSpeed=cfg.speedReset; updateSpeedBar();
  roundMissed++; totalCaught++;
  updateRoundBar(); checkRoundEnd();
  missPop(sx,sy); playMiss();

  if(difficulty==="vuxen"){
    // Vuxen: miss costs points, not a life
    const pen=cfg.missPenalty||2;
    score=Math.max(0,score-pen); scoreEl.textContent=score;
    const d=document.createElement("div"); d.className="miss-flash";
    d.textContent="-"+pen+"p";
    d.style.cssText=`left:${sx}px;top:${sy}px;color:#ff9de2;font-size:13px;`;
    wrap.appendChild(d); setTimeout(()=>{if(d.parentNode)d.parentNode.removeChild(d);},700);
  } else {
    misses++; updateHearts();
    if(misses>=cfg.maxMisses) endGame();
  }
}
function onBadCatch(type,x,y){
  streak=0; streakEl.textContent=0;
  multiplier=1; multiEl.textContent="×1";
  updateComboRing(1);
  currentSpeed=cfg.speedReset; updateSpeedBar();
  playBadCatch(false);
  score=Math.max(0,score+type.pts); scoreEl.textContent=score;
  flashWrap("rgba(255,0,0,0.5)");
  showBanner(type.emoji+" "+type.pts+"p  Streak bruten!","danger",1400);
  const d=document.createElement("div"); d.className="miss-flash";
  d.textContent=type.emoji+" "+type.pts+"p";
  d.style.cssText=`left:${x}px;top:${y}px;color:#ff4444;font-size:18px;`;
  wrap.appendChild(d); setTimeout(()=>{if(d.parentNode)d.parentNode.removeChild(d);},800);
}
function onLifeCatch(type,x,y){
  if(!type.givesLife){
    // 💔
    misses++; updateHearts(); playBadCatch(true);
    wrap.style.outline="4px solid rgba(255,0,0,0.6)";
    setTimeout(()=>wrap.style.outline="",250);
    showBanner("💔 -1 Liv!","danger",1600);
    flashCentre("💔 MINUS ETT LIV!",1100);
    const d=document.createElement("div"); d.className="miss-flash";
    d.textContent="💔 -Liv!"; d.style.cssText=`left:${x}px;top:${y}px;color:#ff4444;font-size:20px;`;
    wrap.appendChild(d); setTimeout(()=>{if(d.parentNode)d.parentNode.removeChild(d);},800);
    if(misses>=cfg.maxMisses) endGame();
  } else {
    if(misses>0){ misses--; updateHearts(); }
    playMultiUp(2); showBanner("💚 +1 Liv!","normal",1500);
    const d=document.createElement("div"); d.className="score-pop";
    d.textContent="💚 +❤️"; d.style.cssText=`left:${x}px;top:${y}px;font-size:20px;color:#86efac;`;
    wrap.appendChild(d); setTimeout(()=>{if(d.parentNode)d.parentNode.removeChild(d);},900);
    burst(x,y,10);
  }
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
  showBanner("🎯 UTMANING: "+c.label,"challenge",2800);
  playWow(); playTune(c.music||"challenge");
  challengeEndTimer=setTimeout(()=>{ activeChallenge=null; playTune("birthday"); },18000);
  nextChallengeAt=totalCaught+cfg.roundSize*3+Math.floor(seededRng()*10);
}
// ══════════════════════════════════════
//  GAME LOOP
// ══════════════════════════════════════
function loop(ts){
  if(!playing) return;
  const dt=Math.min(ts-lastTime,50); lastTime=ts;
  _cW=wrap.clientWidth; _cH=wrap.clientHeight;

  const spd=UNICORN_SPEED*(dt/16);
  if(joyTouchId!==null&&joyTargetX!==null) ux=joyTargetX;
  else {
    if(keys["ArrowLeft"]||keys["a"]) ux=Math.max(36,ux-spd);
    if(keys["ArrowRight"]||keys["d"]) ux=Math.min(_cW-36,ux+spd);
  }
  uy=_cH*FIXED_Y_FRAC;
  unicornEl.style.left=ux+"px"; unicornEl.style.top=uy+"px";
  comboRing.style.left=ux+"px"; comboRing.style.top=uy+"px";

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
  setTimeout(()=>playTune("birthday"),160);
}
// ══════════════════════════════════════
//  END GAME
// ══════════════════════════════════════
function endGame(){
  if(!playing) return;
  playing=false;
  cancelAnimationFrame(rafId); rafId=null;
  clearInterval(spawnTimer); spawnTimer=null;
  clearTimeout(challengeEndTimer); clearTimeout(bonusTimer);
  clearAllPowerups(); stopMusic();
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

  // Async: submit to backend → cache result
  submitScore({name:playerName,score,difficulty,maxStreak,maxMulti,perfectRounds,caught:totalCaught})
    .then(res=>{
      if(!res) return;
      globalScoreCache[difficulty]=res.list;
      if(tlShowDiff==="global") renderTop("global",playerName,res.list);
    });
  // Also pre-fetch global list for the other difficulty
  fetchGlobalScores(difficulty).then(r=>{ if(r) globalScoreCache[difficulty]=r; });
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
  clearInterval(spawnTimer); spawnTimer=null;
  clearTimeout(challengeEndTimer); clearTimeout(bonusTimer);
  clearAllPowerups(); stopMusic();
  stars.forEach(s=>{ try{ s.el.remove(); }catch{} }); stars=[];
  wrap.querySelectorAll(".trail,.particle,.catch-pop,.miss-flash,.firework,.score-pop").forEach(e=>e.remove());

  score=0; level=1; streak=0; maxStreak=0;
  multiplier=1; maxMulti=1; misses=0; totalCaught=0; perfectRounds=0;
  roundCaught=0; roundMissed=0; roundNum=1;
  activeChallenge=null; nextChallengeAt=cfg.roundSize*3;
  currentSpeed=cfg.baseSpeed;
  resetTheme(); resetRng();

  scoreEl.textContent="0"; levelEl.textContent="1"; streakEl.textContent="0"; multiEl.textContent="×1";
  buildHearts(); updateHearts(); updateSpeedBar(); updateComboRing(1);
  comboFlash.style.opacity="0"; centreFlash.style.opacity="0"; perfectToast.style.opacity="0";
  eventBanner.classList.remove("show");
  document.getElementById("overlay-title").textContent="Grattis på\nFödelsedagen Ellie! 🎂";
  document.getElementById("overlay-sub").textContent="Fånga stjärnorna och samla poäng! ✨";
  document.getElementById("how-vuxen").style.display=difficulty==="vuxen"?"":"none";
  document.getElementById("stats-card").style.display="none";
  document.getElementById("end-medals").style.display="none";
  document.getElementById("rank-line").style.display="none";

  _cW=wrap.clientWidth; _cH=wrap.clientHeight;
  ux=_cW/2; uy=_cH*FIXED_Y_FRAC;
  unicornEl.classList.toggle("vuxen", difficulty==="vuxen");
  comboRing.className=difficulty==="vuxen"?"vuxen":"";
  joyTouchId=null; joyTargetX=null; joyKnob.style.left="50%";
  updateRoundBar(); makeClouds(); applyTheme(1);

  // Init rainbow trail dots on first play
  if(rainbowDots.length===0) initRainbowDots();
  rainbowPositions=[]; hideRainbowDots();

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
document.addEventListener("keydown", e=>{ keys[e.key]=true; });
document.addEventListener("keyup",   e=>{ keys[e.key]=false; });
document.addEventListener("touchstart", ()=>unlockAudio(), {once:true,passive:true});
document.addEventListener("mousedown",  ()=>unlockAudio(), {once:true});
document.addEventListener("keydown", e=>{ if(e.key==="Escape"&&playing) endGame(); });

playBtn.addEventListener("click",    ()=>{ unlockAudio(); startGame(); });
playBtn.addEventListener("touchend", e=>{ e.preventDefault(); unlockAudio(); startGame(); });

quitBtn.addEventListener("click",    ()=>{ if(playing) endGame(); });
quitBtn.addEventListener("touchend", e=>{ e.preventDefault(); if(playing) endGame(); });

document.querySelectorAll(".diff-btn").forEach(btn=>{
  const select=()=>{
    difficulty=btn.dataset.diff; cfg=DIFF[difficulty];
    document.querySelectorAll(".diff-btn").forEach(b=>b.classList.remove("selected"));
    btn.classList.add("selected");
    renderTop(difficulty);
    document.getElementById("how-vuxen").style.display=difficulty==="vuxen"?"":"none";
  };
  btn.addEventListener("click", select);
  btn.addEventListener("touchend", e=>{ e.preventDefault(); select(); });
});

document.querySelectorAll(".tl-tab").forEach(tab=>{
  const show=()=>{
    const d=tab.dataset.d;
    if(d==="global"){
      const cached=globalScoreCache[difficulty]||globalScoreCache.barn||null;
      renderTop("global","",cached||undefined);
      if(!cached) fetchGlobalScores(difficulty).then(r=>{ if(r){ globalScoreCache[difficulty]=r; if(tlShowDiff==="global") renderTop("global","",r); }});
    } else {
      renderTop(d);
    }
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

// ══════════════════════════════════════
//  INIT
// ══════════════════════════════════════
makeBgStars();
buildHearts();
renderTop("barn");


/* ================= body: effects ================= */
function schedule(delay,type,v){S.events.push({at:S.t+delay,type:type,v:v})}
function isToxReason(r){return r==='toxin'||r==='neuro'||r==='fever'}
function damage(amt,reason){
  if(amt<=0)return;
  if(isToxReason(reason)&&S.toxProtect&&S.health-amt<10)amt=Math.max(0,S.health-10);
  S.health-=amt;S.loss[reason]=(S.loss[reason]||0)+amt;
}
function endSleep(){
  if(!S.sleeping)return;
  S.sleeping=false;
  if(S.sleepT>=60)consolidate();
  S.sleepT=0;
}
function consume(it,dose,taste){
  var v=comp(it),r=Math.random,g=S.gen,bad=spoiled(it);
  if(it.kind==='plant'&&!taste){
    var worry=v[2]+v[3]*.8+(v[1]>.45?v[1]*.4:0);
    if(g>=2&&worry>.28&&r()<Math.min(.9,.35+.12*g)){dose*=.45;say('Hořkost tě zastaví. Polkl jsi jen část.','warn');logSym('hořké')}
  }
  S.eaten.push({t:S.t,it:{kind:it.kind,sp:it.sp,part:it.part,ripe:it.ripe,season:it.season,soil:it.soil,cooked:it.cooked,dl:it.dl},taste:!!taste});
  if(S.eaten.length>24)S.eaten.shift();
  var nut=v[0]*(bad?.5:1);
  S.satiety=Math.min(100,S.satiety+nut*38*dose);
  if(nut*38*dose>=9)logSym('syté');
  var irr=v[1]*dose;
  if(irr>=.12){
    if(r()<clamp(irr*1.4,0,1))schedule(30+r()*210,'vomit',clamp(irr,.15,1));
    if(irr>.45&&r()<.6)schedule(60+r()*240,'diarrhea',clamp(irr,.3,1));
  }
  var gx=G();
  if(v[2]*dose>.02)schedule(60+r()*240,'toxin',v[2]*dose*35*gx.toxMul);
  if(v[3]*dose>.1)schedule(90+r()*270,'neuro',v[3]*dose*gx.toxMul);
  if(v[4]*dose>.1){S.stim=Math.max(S.stim,90*v[4]*dose*1.5);S.energy=Math.min(100,S.energy+22*v[4]*dose);logSym('posílilo');schedule(110+r()*60,'crash',v[4]*dose)}
  if(v[5]*dose>.05){
    var needs=S.fever>5||S.toxin>5||S.infection>5||S.limp>3;
    S.fever=Math.max(0,S.fever-35*v[5]*dose);S.toxin=Math.max(0,S.toxin-10*v[5]*dose);S.infection=Math.max(0,S.infection-20*v[5]*dose);S.limp=Math.max(0,S.limp-8*v[5]*dose);
    if(needs)logSym('léčí');
  }
  if(bad&&r()<(taste?.35:.85)*gx.badMul){if(gx.old)say('Železný žaludek to zvládl.','note');schedule(90+r()*300,'sick',(18+r()*22)*(taste?.5:1)*gx.badMul);if(r()<.5*gx.badMul)schedule(40+r()*120,'vomit',.5)}
  if(taste){tasteText(v,bad);sfx.eat()}
  else{say(it.kind==='meat'?'Snědl jsi maso.':'Snědl jsi to.');sfx.eat();
    if(nut>=.3&&v[2]<.05&&v[3]<.1&&v[1]<.25&&!bad)schedule(240,'food',0)}
}
function tasteText(v,bad){
  var out=[],r=Math.random,gl=skillLvl('gather'),g=S.gen;
  if(v[0]>.5)out.push('Sladká, sytá chuť.');else if(v[0]>.2)out.push('Mdlá, trochu sytá chuť.');else out.push('Skoro bez chuti.');
  var sharp=.5+.12*g+.08*gl;
  if(v[1]>.5){out.push('Pálí tě v ústech.');logSym('pálí')}else if(v[1]>.2&&r()<Math.min(.95,sharp+.2)){out.push('Trochu tě to dráždí v hrdle.');logSym('pálí')}
  if(v[2]>.4&&r()<Math.min(.98,.85+.03*g)){out.push('Zvláštně hořké a svíravé.');logSym('hořké')}else if(v[2]>.2&&r()<Math.min(.95,sharp)){out.push('Má hořkou pachuť.');logSym('hořké')}
  if(v[3]>.4&&r()<.3+.1*gl)out.push('Divně sladká, skoro mýdlová.');
  if(v[4]>.4)out.push('Štiplavě svěží, zaostří ti to smysly.');
  if(v[5]>.4)out.push('Svíravá, bylinná chuť.');
  if(bad)out.push('Páchne to a chutná odporně.');
  say('Ochutnal jsi. '+out.join(' '));
}
function runEvents(){
  var keep=[];
  for(var i=0;i<S.events.length;i++){
    var e=S.events[i];
    if(S.t<e.at){keep.push(e);continue}
    if(e.type==='vomit'){S.satiety=Math.max(0,S.satiety-18*e.v-4);S.hydration=Math.max(0,S.hydration-10);say('Žaludek se ti zvedá. Vyzvracíš se.','warn');logSym('zvracení');burst(S.px,S.py,8,'#8aa04a',1.6,.8);milestone('poison')}
    else if(e.type==='diarrhea'){S.diarr=Math.max(S.diarr,240*e.v);say('Břicho se ti svírá křečemi.','warn');logSym('průjem');milestone('poison')}
    else if(e.type==='toxin'){S.toxin+=e.v;if(e.v>20)say('Cítíš se divně, jako by tě něco zevnitř tížilo.','warn')}
    else if(e.type==='neuro'){
      if(S.health>=70)S.toxProtect=true;
      S.tremor=Math.max(S.tremor,e.v*240);logSym('třes');milestone('poison');
      say('Ruce se ti třesou a svět se začíná vlnit.','warn');
      if(e.v>.55){S.faint=Math.max(S.faint,140*e.v);endSleep();damage(10*e.v,'neuro');logSym('omdlení');say('Zrak se ti rozostřuje. Padáš na zem.','warn')}
      if(e.v>1.1)damage(35,'neuro');
    }
    else if(e.type==='crash'){S.energy=Math.max(0,S.energy-16*e.v-6);say('Ten příval sil je pryč. Cítíš prázdnotu.','warn')}
    else if(e.type==='infect'||e.type==='sick'){S.infection=Math.max(S.infection,e.v);say(e.type==='infect'?'Rána pálí a je horká. Něco se zanítilo.':'Je ti horko a špatně od žaludku. Něco v tobě se pokazilo.','warn')}
    else if(e.type==='food'){milestone('food')}
  }
  S.events=keep;
}
function allFires(){
  var f=S.fires.slice();
  world.tribes.forEach(function(t){f.push({x:t.camp.x+.5,y:t.camp.y+.5,burn:1})});
  return f;
}
function nearFire(rad){
  var fs=allFires();
  for(var i=0;i<fs.length;i++){var f=fs[i];if(f.burn>0&&dist(f.x,f.y,S.px,S.py)<=rad)return f}
  return null;
}
function sheltered(px,py){
  var sx=Math.floor(px),sy=Math.floor(py);
  var seen={},q=[[sx,sy]];seen[sy*W+sx]=1;var n=0;
  while(q.length){
    var c=q.pop();n++;if(n>36)return false;
    var d=[[1,0],[-1,0],[0,1],[0,-1]];
    for(var k=0;k<4;k++){
      var nx=c[0]+d[k][0],ny=c[1]+d[k][1];
      if(nx<0||ny<0||nx>=W||ny>=H)return false;
      var id=ny*W+nx;if(seen[id])continue;
      if(S.walls[id])continue;
      seen[id]=1;q.push([nx,ny]);
    }
  }
  return true;
}
function isSheltered(){return sheltered(S.px,S.py)}
function speedMul(){
  var m=1;
  m-=clamp((S.toxin-45)/200,0,.3);m-=clamp((S.fever-40)/150,0,.3);m-=clamp(S.limp/100*.45,0,.45);
  if(S.energy<15)m-=.2;if(S.stim>0)m+=.18;
  return clamp(m*G().speed,.35,1.3);
}
function physiology(gm){
  var amb=ambient(),nf=nearFire(2.6),sh=isSheltered(),gg=G();
  var tw=clamp(50+gg.cold+(amb-12)*2+(nf?28:0)+(sh?14:0)+(S.sleeping?3:0),0,100);
  S.warmth+=(tw-S.warmth)*Math.min(1,.004*gm);
  var thirst=100/1728;
  if(S.warmth>78)thirst*=1.4;if(S.fever>30)thirst*=1.3;
  if(S.diarr>0){thirst*=3;S.diarr-=gm}
  S.hydration=Math.max(0,S.hydration-thirst*gm*(S.sleeping?.5:1));
  S.satiety=Math.max(0,S.satiety-100/3600*gm*(S.sleeping?.6:1));
  if(S.sleeping){S.energy=Math.min(100,S.energy+.35*gm);S.sleepT+=gm}
  else S.energy=Math.max(0,S.energy-100/2200*gm*gg.drain*(S.stim>0?.5:1));
  if(S.sleeping&&(S.energy>=100||(S.energy>88&&daylight()>.7))){endSleep();say('Probudil ses odpočatý.','good')}
  S.toxin=Math.max(0,S.toxin-.03*gm);S.infection=Math.max(0,S.infection-.02*gm*gg.heal);S.limp=Math.max(0,S.limp-.015*gm*gg.heal);
  var tf=Math.max(0,(S.toxin-25)*1.3)+S.infection*.7;
  S.fever+=(tf-S.fever)*Math.min(1,.012*gm);
  if(S.fever>30&&!S.fevLog){S.fevLog=true;logSym('horečka');say('Hoří ti čelo.','warn')}else if(S.fever<10)S.fevLog=false;
  if(S.toxin>=25&&!S.toxEp){S.toxEp=true;S.toxProtect=S.health>=70}
  if(S.toxin<5&&S.fever<10){S.toxEp=false;if(S.tremor<=0)S.toxProtect=false}
  if(S.tremor>0)S.tremor=Math.max(0,S.tremor-gm);
  if(S.faint>0){S.faint-=gm;if(S.faint<=0){S.faint=0;say('Probíráš se. Hlava ti třeští.','warn')}}
  if(S.stim>0)S.stim=Math.max(0,S.stim-gm);
  if(S.hydration<=0)damage(.08*gm,'thirst');
  if(S.satiety<=0)damage(.03*gm,'hunger');
  if(S.warmth<18)damage(.03*gm*(1+(18-S.warmth)/9),'cold');
  if(S.fever>80)damage(.03*gm,S.infection>S.toxin*.5?'infection':'fever');
  if(S.toxin>70)damage((S.toxin-70)*.0012*gm,'toxin');
  if(S.bleed>0){damage(S.bleed*gm,'bleed');S.bleed=Math.max(0,S.bleed-.004*gm)}
  if(S.satiety>40&&S.hydration>40&&S.fever<10&&S.toxin<30&&S.bleed<=0&&S.warmth>25)S.health=Math.min(100,S.health+.012*gm*gg.heal*(S.sleeping?2:1));
  S.lossT+=gm;if(S.lossT>DAY_MIN){S.lossT=0;for(var k in S.loss)S.loss[k]*=.5}
  if(S.sleeping&&S.warmth<20){endSleep();say('Zimou nemůžeš spát.','warn')}
  runEvents();
  if(S.health<=0&&!S.dead)die();
}

/* ================= death, generations, respawn ================= */
var DEATH={
  bleed:'Vykrvácel jsi po zranění od zvířete.',wound:'Zemřel jsi na následky zranění od zvířete.',
  toxin:'Zemřel jsi na otravu, která se v těle hromadila.',neuro:'Zemřel jsi na otravu, která ti vzala vědomí.',
  fever:'Zemřel jsi na horečku.',infection:'Rána nebo nemoc tě vyčerpala horečkou.',
  cold:'Zemřel jsi podchlazením.',thirst:'Zemřel jsi žízní.',hunger:'Zemřel jsi hladem.'
};
function die(){
  var best='hunger',bv=-1;for(var k in S.loss){if(S.loss[k]>bv){bv=S.loss[k];best=k}}
  endSleep();
  S.dead=true;S.deathCause=DEATH[best]||'Zemřel jsi.';S.deaths++;S.lastDeath={x:S.px,y:S.py};
  S.corpses.push({x:S.px,y:S.py,inv:S.inv.map(function(s){return Object.assign({},s)}),at:S.t,gen:S.gen});
  S.inv=[];target=null;moveTo=null;closeMenu();$('veil').hidden=true;closeSpeech();
  sfx.hurt();openSheet('death');save();
}
function nextGen(){
  if(S.genMode==='year')return Math.min(GEN_CAP,Math.max(1,worldYear()));
  return Math.min(GEN_CAP,Math.max(1,S.gen+1));
}
function ownStructures(){
  var o=[];
  S.fires.forEach(function(f){o.push({x:f.x,y:f.y})});
  Object.keys(S.walls).forEach(function(k){o.push({x:(k%W)+.5,y:Math.floor(k/W)+.5})});
  S.signs.forEach(function(s){o.push({x:s.x,y:s.y})});
  return o;
}
function walkable(x,y){return !blockedTile(Math.floor(x),Math.floor(y))&&canStand(x,y,.3)}
function ringSpot(cx,cy,r0,r1,tries,okFn){
  for(var n=0;n<tries;n++){
    var a=Math.random()*TAU,rad=r0+Math.random()*(r1-r0),x=cx+Math.cos(a)*rad,y=cy+Math.sin(a)*rad;
    if(x<2||y<2||x>W-2||y>H-2)continue;
    if(walkable(x,y)&&(!okFn||okFn(x,y)))return {x:x,y:y};
  }
  return null;
}
function awayFromCamps(x,y){return world.tribes.every(function(t){return dist(x,y,t.camp.x,t.camp.y)>8})}
function pickWildSpawn(avoid,first){
  for(var n=0;n<900;n++){
    var x=Math.floor(Math.random()*W),y=Math.floor(Math.random()*H),t=world.tiles[y*W+x];
    var ok=(n<500?t===T.SAND:(t===T.SAND||t===T.GRASS))&&walkable(x+.5,y+.5);
    if(!ok)continue;
    if(first&&n<600&&!pondNear(world.tiles,x,y,12))continue;
    if(!awayFromCamps(x+.5,y+.5))continue;
    if(avoid&&dist(x,y,avoid.x,avoid.y)<22&&n<700)continue;
    return {x:x+.5,y:y+.5};
  }
  return {x:W/2,y:H/2};
}
function pickTribeSpawn(){
  var o=ownStructures();if(!o.length)return null;
  for(var n=0;n<30;n++){
    var s=o[Math.floor(Math.random()*o.length)];
    var p=ringSpot(s.x,s.y,4,7.5,20,function(x,y){return !sheltered(x,y)});
    if(p)return p;
  }
  return null;
}
function respawn(choice){
  var old=S,f=newState(S.seed);
  var keep=['seed','t','cal','calLen','dayLen','genMode','epoch','signs','walls','fires','traps','crops','pc','corpses','deaths','lastDeath','lastDay','lastSeason','intro','dreamDone','ms','pressure','rel','sound'];
  S=f;keep.forEach(function(k){S[k]=old[k]});
  S.gen=nextGen();S.born=old.t;
  var sp=null;
  if(choice==='tribe')sp=pickTribeSpawn();
  if(!sp)sp=pickWildSpawn(S.lastDeath,false);
  S.px=sp.x;S.py=sp.y;S.dead=false;
  S.t=(Math.floor(old.t/DAY_MIN)+1)*DAY_MIN+7*60;
  S.cal=old.cal+(S.t-old.t)/DAY_MIN*(S.dayLen/S.calLen);
  S.lastDay=dnDay();S.lastSeason=seasonIdx();
  S.corpses=S.corpses.filter(function(c){return S.t<c.at+4*DAY_MIN});
  animals=animals.filter(function(a){return dist(a.x,a.y,S.px,S.py)>16});
  target=null;moveTo=null;pBubble=null;
  closeSheet();save();updateHud();
  say(choice==='tribe'&&sp?'Narodil ses na okraji svého území. Lidé tě tu znají.':'Probouzíš se na neznámém místě. Nic nevíš.','note');
}

/* ================= offline (the character sleeps while you are away) ================= */
function applyOffline(){
  var elapsed=(Date.now()-S.savedAt)/1000;
  if(S.dead||elapsed<20)return;
  var gmPerSec=DAY_MIN/S.dayLen,gm=Math.min(elapsed*gmPerSec,2*DAY_MIN);
  var hadShelter=sheltered(S.px,S.py),oldDay=dnDay();
  S.t+=gm;S.cal+=Math.min(elapsed,2*S.dayLen)/S.calLen;
  S.fires.forEach(function(f){if(f.burn>0)f.burn=Math.max(0,f.burn-gm)});
  var frac=.15;
  S.satiety=Math.max(Math.min(S.satiety,12),S.satiety-100/3600*gm*frac);
  S.hydration=Math.max(Math.min(S.hydration,12),S.hydration-100/1728*gm*frac);
  S.energy=Math.min(100,S.energy+.35*gm);
  S.warmth=Math.max(Math.min(S.warmth,35),Math.min(S.warmth,55));
  S.toxin=Math.max(0,S.toxin-.03*gm);S.infection=Math.max(0,S.infection-.02*gm);S.fever=Math.max(0,S.fever-.1*gm);S.limp=Math.max(0,S.limp-.015*gm);S.diarr=0;S.tremor=0;S.stim=0;S.faint=0;S.stun=0;S.sleeping=false;
  S.events=S.events.filter(function(e){if(e.at<=S.t){if(e.type==='toxin')S.toxin+=e.v*.5;if(e.type==='sick'||e.type==='infect')S.infection=Math.max(S.infection,e.v*.6);return false}return true});
  S.health=Math.min(100,Math.max(S.health,8)+gm*.01);
  var msg='Byl jsi pryč '+Math.max(1,Math.round(elapsed/60))+' min. Svět šel dál o '+(Math.round(gm/DAY_MIN*10)/10)+' dne. ';
  if(hadShelter)msg+='Spal jsi v bezpečí přístřešku.';
  else{
    var p=ringSpot(S.px,S.py,3,8,40,function(x,y){return awayFromCamps(x,y)});
    if(p){S.px=p.x;S.py=p.y}
    msg+='Usnul jsi pod širým nebem. Svět tě ukryl o kus dál.';
  }
  S.pendingMsg=msg+' Jsi hladový a slabý, ale živý.';
  S.lastDay=oldDay;
}

/* ================= movement & collision ================= */
var keys={up:false,down:false,left:false,right:false},stuckT=0;
function blockedTile(tx,ty){
  if(tx<0||ty<0||tx>=W||ty>=H)return true;
  var k=ty*W+tx,t=world.tiles[k];
  if(t===T.SEA||t===T.POND)return true;
  if(world.trees.has(k))return true;
  if(S.walls[k])return true;
  return false;
}
function canStand(x,y,r){
  return !blockedTile(Math.floor(x-r),Math.floor(y-r))&&!blockedTile(Math.floor(x+r),Math.floor(y-r))&&!blockedTile(Math.floor(x-r),Math.floor(y+r))&&!blockedTile(Math.floor(x+r),Math.floor(y+r));
}
function terrainSpeed(x,y){var t=world.tiles[Math.floor(y)*W+Math.floor(x)];return t===T.BOG?.72:t===T.ROCK?.9:t===T.SAND?.95:1}
function reachFor(t){
  switch(t.kind){case 'plant':return .95;case 'animal':return 1.4;case 'fire':return 1.3;case 'sign':return 1.3;case 'tree':return 1.5;case 'rock':return 1.3;case 'water':return 1.7;case 'wall':return 1.6;case 'npc':return 2.2;case 'corpse':return 1.2;case 'trap':return 1.2}
  return 1.2;
}
function tpos(t){
  if(t.ref&&typeof t.ref.x==='number')return {x:t.ref.x,y:t.ref.y};
  return {x:t.x,y:t.y};
}
function targetValid(t){
  if(!t)return false;
  if(t.kind==='plant')return !plantGone(t.ref);
  if(t.kind==='animal')return animals.indexOf(t.ref)>=0;
  if(t.kind==='fire')return S.fires.indexOf(t.ref)>=0;
  if(t.kind==='sign')return S.signs.indexOf(t.ref)>=0||t.ref.ancient;
  if(t.kind==='wall')return !!S.walls[t.ref];
  if(t.kind==='trap')return S.traps.indexOf(t.ref)>=0;
  if(t.kind==='corpse')return S.corpses.indexOf(t.ref)>=0;
  if(t.kind==='npc')return npcs.indexOf(t.ref)>=0;
  return true;
}
function movePlayer(dt){
  if(S.stun>0){S.stun-=dt;return}
  if(S.faint>0||S.sleeping)return;
  var dx=0,dy=0;
  if(keys.up)dy-=1;if(keys.down)dy+=1;if(keys.left)dx-=1;if(keys.right)dx+=1;
  var byKey=dx||dy;
  if(byKey){moveTo=null;if(target){target=null;closeMenu()}}
  else if(moveTo){var vx=moveTo.x-S.px,vy=moveTo.y-S.py,d=Math.hypot(vx,vy);if(d<.15)moveTo=null;else{dx=vx/d;dy=vy/d}}
  else if(target){
    var p=tpos(target),vx2=p.x-S.px,vy2=p.y-S.py,d2=Math.hypot(vx2,vy2);
    if(d2>reachFor(target)-.15){dx=vx2/d2;dy=vy2/d2}
  }
  var len=Math.hypot(dx,dy);
  if(len>0){
    dx/=len;dy/=len;S.face=Math.atan2(dy,dx);
    var sp=4*speedMul()*terrainSpeed(S.px,S.py),step=sp*dt,ox=S.px,oy=S.py;
    if(canStand(S.px+dx*step,S.py,.28))S.px+=dx*step;
    if(canStand(S.px,S.py+dy*step,.28))S.py+=dy*step;
    var moved=Math.hypot(S.px-ox,S.py-oy);
    if(!byKey&&moved<step*.3){stuckT+=dt;if(stuckT>.45){moveTo=null;stuckT=0}}else stuckT=0;
  }
}

/* ================= animals ================= */
var ECO={boar:5,rabbit:12,deer:5,wolf:2};
function spawnAnimal(kind,avoidDist,near){
  for(var n=0;n<200;n++){
    var x,y;
    if(near){x=near.x+(Math.random()*6-3);y=near.y+(Math.random()*6-3)}
    else{x=Math.floor(Math.random()*W)+.5;y=Math.floor(Math.random()*H)+.5}
    var t=world.tiles[Math.floor(y)*W+Math.floor(x)];
    var okT=kind==='boar'||kind==='wolf'?(t===T.GRASS||t===T.FOREST):(t===T.GRASS||t===T.FOREST||t===T.SAND);
    if(!okT||!walkable(x,y))continue;
    if(dist(x,y,S.px,S.py)<avoidDist)continue;
    if(!awayFromCamps(x,y))continue;
    var hp={boar:7,rabbit:1,deer:4,wolf:5}[kind];
    var a={kind:kind,x:x,y:y,state:'wander',timer:Math.random()*3,hp:hp,tx:x,ty:y,aggro:0,dir:Math.random()*TAU,stam:3,dead:false,deadAt:0,graze:0,den:{x:x,y:y},ct:0,cdx:0,cdy:0};
    animals.push(a);return a;
  }
  return null;
}
function initAnimals(){
  animals=[];var i;
  for(i=0;i<ECO.boar;i++)spawnAnimal('boar',16);
  for(i=0;i<ECO.rabbit;i++)spawnAnimal('rabbit',6);
  var h=spawnAnimal('deer',14);if(h){for(i=1;i<ECO.deer;i++)spawnAnimal('deer',10,h)}
  for(i=0;i<ECO.wolf;i++)spawnAnimal('wolf',20);
}
function moveAnimal(a,dx,dy,r){
  if(canStand(a.x+dx,a.y,r))a.x+=dx;else return false;
  if(canStand(a.x,a.y+dy,r))a.y+=dy;else return false;
  return true;
}
function night(){return daylight()<.3}
function wound(sev,from){
  var red=1-.05*skillLvl('hunt');
  sev*=red*G().wound;
  damage(sev*.5,'wound');
  S.bleed=Math.min(.6,S.bleed+sev*.012);S.limp=Math.min(100,S.limp+sev*1.8);S.stun=.8;endSleep();
  if(sev>20&&Math.random()<.35)schedule(400+Math.random()*500,'infect',sev*.8);
  say(from==='wolf'?'Vlk tě pokousal. Krvácíš a bolí tě každý krok.':'Kly tě zasáhly. Krvácíš a bolí tě každý krok.','warn');
  sfx.hurt();burst(S.px,S.py,10,'#b0332a',2,.7);
}
function fireNear(x,y,rad){var fs=allFires();for(var i=0;i<fs.length;i++)if(fs[i].burn>0&&dist(fs[i].x,fs[i].y,x,y)<rad)return true;return false}
function herdCenter(a){
  var sx=0,sy=0,n=0;
  animals.forEach(function(b){if(b.kind==='deer'&&!b.dead&&dist(a.x,a.y,b.x,b.y)<14){sx+=b.x;sy+=b.y;n++}});
  return n?{x:sx/n,y:sy/n}:{x:a.x,y:a.y};
}
function trapCheck(a,dt){
  for(var i=0;i<S.traps.length;i++){
    var tp=S.traps[i];
    if(tp.caught)continue;
    if(dist(a.x,a.y,tp.x,tp.y)<.7&&Math.random()<(a.kind==='rabbit'?.9:.6)*dt*2){
      tp.caught={kind:a.kind,at:S.t};a.dead=true;a.caught=true;a.deadAt=S.t;S.pressure+=1;milestone('trap');
      say('V pasti se něco chytilo.','good');return true;
    }
  }
  return false;
}
function updateAnimals(dt){
  for(var i=0;i<animals.length;i++){
    var a=animals[i];
    if(a.dead)continue;
    var dx=S.px-a.x,dy=S.py-a.y,d=Math.hypot(dx,dy)||.001;
    if(a.kind==='rabbit'||a.kind==='deer'){
      var rb=a.kind==='rabbit',asleep=night()&&rb;
      if(a.state==='tired'){a.timer-=dt;if(a.timer<=0){a.state='wander';a.stam=3}continue}
      var flee=d<(asleep?2:(rb?5.5:7.5))&&!(S.sleeping&&false);
      if(flee){
        a.state='flee';a.stam-=dt;
        if(a.stam<=0){a.state='tired';a.timer=2.2;continue}
        var nx=-dx/d,ny=-dy/d,sp=(rb?5.6:6.2)*dt;
        if(!rb){var hc=herdCenter(a);var hx=hc.x-a.x,hy=hc.y-a.y,hd=Math.hypot(hx,hy)||1;nx=nx*.8+hx/hd*.2;ny=ny*.8+hy/hd*.2}
        if(!moveAnimal(a,nx*sp,ny*sp,.2)){moveAnimal(a,-ny*sp,nx*sp,.2)}
        trapCheck(a,dt);
        continue;
      }
      if(a.state==='flee'){a.state='wander';a.stam=Math.min(3,a.stam+dt)}
      if(asleep)continue;
      a.timer-=dt;
      if(a.state==='graze'){
        a.graze-=dt;
        if(Math.random()<dt*3&&dist(a.x,a.y,S.px,S.py)<12){parts.push({x:a.tx,y:a.ty,vx:(Math.random()-.5)*.8,vy:-.4-Math.random()*.4,l:.7,m:.7,c:'#9bbf62',s:2})}
        if(dist(a.x,a.y,S.px,S.py)<10)hint('rabbit','Zvíře okusuje rostlinu. Zvířata nevědí, co je bezpečné pro člověka, ale může to být stopa.');
        if(a.graze<=0)a.state='wander';continue;
      }
      if(a.timer<=0){
        a.timer=2+Math.random()*4;
        if(rb&&Math.random()<.5){
          var best=null,bd=7;
          for(var k=0;k<world.plants.length;k+=3){var pl=world.plants[k];if(!ARCH[world.species[pl.sp].arch].rabbit||plantGone(pl))continue;var pd=dist(pl.x,pl.y,a.x,a.y);if(pd<bd){bd=pd;best=pl}}
          if(best){a.tx=best.x;a.ty=best.y;a.state='toplant'}
        }else if(!rb){
          var hc2=herdCenter(a);a.tx=hc2.x+(Math.random()*5-2.5);a.ty=hc2.y+(Math.random()*5-2.5);a.state='wander';
        }else{a.tx=a.x+(Math.random()*6-3);a.ty=a.y+(Math.random()*6-3);a.state='wander'}
      }
      var vx=a.tx-a.x,vy=a.ty-a.y,vd=Math.hypot(vx,vy);
      if(vd>.3){var s2=(rb?1.2:1.0)*dt;moveAnimal(a,vx/vd*s2,vy/vd*s2,.2);a.dir=Math.atan2(vy,vx)}
      else if(a.state==='toplant'){a.state='graze';a.graze=5}
      trapCheck(a,dt);
      continue;
    }
    if(a.kind==='wolf'){
      a.aggro=Math.max(0,a.aggro-dt);
      var fireClose=fireNear(S.px,S.py,4.5);
      if(a.state==='wander'){
        a.timer-=dt;
        if(a.timer<=0){a.timer=3+Math.random()*4;var rad=night()?12:4;a.tx=a.den.x+(Math.random()*2-1)*rad;a.ty=a.den.y+(Math.random()*2-1)*rad}
        var wx=a.tx-a.x,wy=a.ty-a.y,wd=Math.hypot(wx,wy);
        if(wd>.3){a.dir=Math.atan2(wy,wx);moveAnimal(a,wx/wd*1.4*dt,wy/wd*1.4*dt,.3)}
        if(night()&&d<11&&!fireClose&&!sheltered(S.px,S.py)){a.state='stalk';a.timer=7+Math.random()*4;hint('wolf','Něco tě sleduje ze tmy. Oheň ho odradí.')}
      }else if(a.state==='stalk'){
        a.timer-=dt;a.dir=Math.atan2(dy,dx);
        if(fireClose||sheltered(S.px,S.py)||a.timer<=0||d>16){a.state='wander';a.timer=2;continue}
        var ang=Math.atan2(dy,dx)+Math.sin(S.t/23+i)*.9,sp3=(d>5?2.4:1.4)*dt;
        moveAnimal(a,Math.cos(ang)*sp3,Math.sin(ang)*sp3,.3);
        if(d<3.2){a.state='alert';a.timer=.8;sfx.alert()}
      }else if(a.state==='alert'){
        a.dir=Math.atan2(dy,dx);a.timer-=dt;
        if(a.timer<=0){a.state='charge';a.ct=.9;a.cdx=dx/d;a.cdy=dy/d;a.dir=Math.atan2(a.cdy,a.cdx)}
      }else if(a.state==='charge'){
        a.ct-=dt;var ok2=moveAnimal(a,a.cdx*8*dt,a.cdy*8*dt,.3);
        if(dist(S.px,S.py,a.x,a.y)<.75&&S.stun<=0&&!S.dead){wound(10+Math.random()*14,'wolf');a.state='exhaust';a.timer=2;continue}
        if(!ok2||a.ct<=0){a.state='exhaust';a.timer=1.6}
      }else if(a.state==='exhaust'){
        a.timer-=dt;if(a.timer<=0){a.state='wander';a.timer=1;a.tx=a.den.x;a.ty=a.den.y}
      }
      continue;
    }
    /* boar */
    a.aggro=Math.max(0,a.aggro-dt);
    if(d<3.4&&a.state!=='charge'&&!S.sleeping)a.aggro=12;
    if(S.sleeping&&d<7&&a.aggro>0){endSleep();say('Něco tě vzbudilo. Divočák je blízko.','warn')}
    if(a.state==='wander'){
      a.timer-=dt;
      if(a.timer<=0){a.timer=2+Math.random()*4;a.tx=a.x+(Math.random()*8-4);a.ty=a.y+(Math.random()*8-4)}
      var bx=a.tx-a.x,by=a.ty-a.y,bd2=Math.hypot(bx,by);
      if(bd2>.3){a.dir=Math.atan2(by,bx);moveAnimal(a,bx/bd2*.9*dt,by/bd2*.9*dt,.32)}
      if(a.aggro>0&&d<9){a.state='alert';a.timer=.9+.12*skillLvl('hunt');sfx.alert();hint('boar','Divočák se na tebe zaměřil. Až se rozběhne, uhni na stranu.')}
    }else if(a.state==='alert'){
      a.dir=Math.atan2(dy,dx);a.timer-=dt;
      if(a.timer<=0){a.state='charge';a.ct=1.1;a.cdx=dx/d;a.cdy=dy/d;a.dir=Math.atan2(a.cdy,a.cdx)}
    }else if(a.state==='charge'){
      a.ct-=dt;var cs=7*dt;
      var ok=moveAnimal(a,a.cdx*cs,a.cdy*cs,.32);
      if(dist(S.px,S.py,a.x,a.y)<.75&&S.stun<=0&&!S.dead){wound(18+Math.random()*22,'boar');a.state='exhaust';a.timer=2.5;continue}
      if(!ok||a.ct<=0){a.state='exhaust';a.timer=2.4}
    }else if(a.state==='exhaust'){
      a.timer-=dt;if(a.timer<=0){a.state=a.aggro>0&&d<9?'alert':'wander';a.timer=.8}
    }
  }
  animals=animals.filter(function(a){return !(a.dead&&!a.caught&&S.t>a.deadAt+DAY_MIN)&&!(a.caught&&S.t>a.deadAt+DAY_MIN*3)});
}


/* ================= ruins (old carvings with partly wrong notes) ================= */
function tagsFromComp(v){
  var t=[];
  if(v[0]>.3)t.push('syté');if(v[2]>.2)t.push('hořké');if(v[1]>.2)t.push('pálí');if(v[1]>.3)t.push('zvracení');if(v[1]>.5)t.push('průjem');
  if(v[2]>.12)t.push('horečka');if(v[3]>.3)t.push('třes');if(v[3]>.6)t.push('omdlení');if(v[4]>.3)t.push('posílilo');if(v[5]>.3)t.push('léčí');
  return t;
}
function makeRuins(){
  var r=mulberry32(world.seed^0xBEEF);
  world.ruins=world.ruinSpots.map(function(p){
    var sp=Math.floor(r()*world.species.length),A=ARCH[world.species[sp].arch];
    var parts=['fruit','root','leaf'].filter(function(x){return A.p[x]});
    var part=parts[Math.floor(r()*parts.length)];
    var v=comp({kind:'plant',sp:sp,part:part,ripe:1,season:1,soil:T.GRASS,cooked:0});
    var tags=tagsFromComp(v);
    if(r()<.3){
      if(tags.length&&r()<.6)tags.splice(Math.floor(r()*tags.length),1);
      var extra=TAGS[Math.floor(r()*TAGS.length)];if(tags.indexOf(extra)<0)tags.push(extra);
    }
    return {x:p.x,y:p.y,bits:bitsForShape(world.species[sp].shape),at:-1,perm:true,ancient:true,
      rec:{sp:sp,name:[Math.floor(r()*10),Math.floor(r()*10)],tags:tags,old:true}};
  });
}

/* ================= foreign tribes and language ================= */
var safeCache={key:'',map:{}};
function bestSafePart(sp){
  var key=seasonIdx()+'|'+S.epoch;if(safeCache.key!==key)safeCache={key:key,map:{}};
  if(safeCache.map[sp]!==undefined)return safeCache.map[sp];
  var A=ARCH[world.species[sp].arch],res=null;
  ['fruit','root','leaf'].some(function(p){
    if(!A.p[p])return false;
    var v=comp({kind:'plant',sp:sp,part:p,ripe:1,season:seasonIdx(),soil:T.GRASS,cooked:1});
    if(v[0]>.15&&v[1]<.25&&v[2]<.06&&v[3]<.1){res=p;return true}
    return false;
  });
  safeCache.map[sp]=res;return res;
}
function harmful(it){var v=comp(it);return v[2]>.12||v[3]>.2||v[1]>.45}
function initNPCs(){
  npcs=[];
  world.tribes.forEach(function(tr){
    for(var i=0;i<3;i++){
      var a=i/3*TAU+tr.id*1.3;
      npcs.push({tr:tr.id,x:tr.camp.x+.5+Math.cos(a)*2.4,y:tr.camp.y+.5+Math.sin(a)*2.4,hx:tr.camp.x+.5,hy:tr.camp.y+.5,
        face:a+Math.PI,state:'idle',timer:Math.random()*3,say:null,point:null,emote:null,emoteT:0,talkCd:Math.random()*6,askCd:0,
        tx:0,ty:0,stuck:0,eatT:0,act:null,plant:null,skin:SKINS[2+Math.floor(Math.random()*3)],hair:i===1?'#8a5a2a':'#2f2218'});
    }
  });
}
/* co hráč při slově vidí (situace), ne co znamená */
var CTXT={food:'u jídla',water:'u vody',danger:'zvíře poblíž',fire:'u ohně',yes:'kývá hlavou',no:'vrtí hlavou',poison:'odhazuje věc'};
var TONE={food:'calm',water:'calm',fire:'calm',yes:'calm',danger:'alarm',no:'alarm',poison:'alarm'};
function npcSay(n,concept){
  var tr=world.tribes[n.tr],w=tr.vocab[concept];
  n.say={w:w,t:3.2,c:concept,tone:TONE[concept]||'calm'};n.talkCd=6;
  var dd=dist(n.x,n.y,S.px,S.py);
  if(dd<14&&!S.dead){
    var k=wordKey(w),e=S.vocab[k]||(S.vocab[k]={w:w,heard:0,meaning:''});
    e.heard++;
    if(dd<12){
      var ct=e.ctx||(e.ctx={}),tc=ct[n.tr]||(ct[n.tr]={});tc[CTXT[concept]]=(tc[CTXT[concept]]||0)+1;
      S.lastHeard=w.slice();
      var sp=$('speech');if(sp&&!sp.hidden)renderSpeech();
      sfx.word(w);milestone('word');
    }
  }
}
/* Starý hlas: Pradávný se čtyři společná slova naučil ve snu */
function grantOldVoice(){
  if(!S||S.gen!==0||S.oldVoice||!world||!world.common)return;
  S.oldVoice=true;
  [['water','water'],['danger','danger'],['yes','yes'],['no','no']].forEach(function(p){
    var w=world.common[p[0]],k=wordKey(w);
    if(!S.vocab[k])S.vocab[k]={w:w,heard:0,meaning:p[1],old:true};
  });
}
/* gesto jako univerzální řeč (jíst, pít) */
function gestureTo(n,concept){
  if(!n)return;
  handleNPCWord(n,world.tribes[n.tr].vocab[concept]);
}
function nearestPond(x,y,maxR){
  var best=null,bd=maxR;
  for(var yy=-maxR;yy<=maxR;yy+=1)for(var xx=-maxR;xx<=maxR;xx+=1){
    var a=Math.floor(x)+xx,b=Math.floor(y)+yy;
    if(a<0||b<0||a>=W||b>=H)continue;
    if(world.tiles[b*W+a]!==T.POND)continue;
    var d=dist(a+.5,b+.5,x,y);if(d<bd){bd=d;best={x:a+.5,y:b+.5}}
  }
  return best;
}
function npcThink(n){
  var tr=world.tribes[n.tr],i,dang=false;
  for(i=0;i<animals.length;i++){
    var a=animals[i];if(a.dead)continue;
    if((a.kind==='boar'&&(a.state==='alert'||a.state==='charge'))||(a.kind==='wolf'&&(a.state==='stalk'||a.state==='alert'||a.state==='charge'))){
      if(dist(a.x,a.y,n.x,n.y)<9||dist(a.x,a.y,S.px,S.py)<7&&dist(n.x,n.y,S.px,S.py)<12){dang=true;break}
    }
  }
  if(dang){n.state='flee';n.timer=4;if(n.talkCd<=0)npcSay(n,'danger');return}
  var r=Math.random();
  if(r<.5){
    var best=null,bd=14;
    for(i=0;i<world.plants.length;i+=2){
      var pl=world.plants[i];if(plantGone(pl)||!cropReady(pl))continue;
      var dh=dist(pl.x,pl.y,n.hx,n.hy);if(dh>13)continue;
      if(bestSafePart(pl.sp)===null)continue;
      var dn=dist(pl.x,pl.y,n.x,n.y);if(dn<bd&&Math.random()<.5){bd=dn;best=pl}
    }
    if(best){n.state='goto';n.act='eat';n.plant=best;n.tx=best.x;n.ty=best.y;n.timer=12;return}
  }else if(r<.72){
    var p=nearestPond(n.hx,n.hy,16);
    if(p){n.state='goto';n.act='drink';n.tx=p.x;n.ty=p.y;n.timer=16;return}
  }
  n.state='idle';n.timer=3+Math.random()*4;
  var a2=Math.random()*TAU,rad=Math.random()*5;n.tx=n.hx+Math.cos(a2)*rad;n.ty=n.hy+Math.sin(a2)*rad;
}
function updateNPCs(dt){
  for(var i=0;i<npcs.length;i++){
    var n=npcs[i],tr=world.tribes[n.tr];
    n.talkCd-=dt;n.askCd-=dt;
    if(n.say){n.say.t-=dt;if(n.say.t<=0)n.say=null}
    if(n.point){n.point.t-=dt;if(n.point.t<=0)n.point=null}
    if(n.emoteT>0){n.emoteT-=dt;if(n.emoteT<=0)n.emote=null}
    if(n.state==='eat'){
      n.eatT-=dt;
      if(n.eatT<=0){n.state='idle';n.timer=2}
      continue;
    }
    n.timer-=dt;
    if(n.timer<=0&&n.state!=='goto'){npcThink(n)}
    var vx=n.tx-n.x,vy=n.ty-n.y,vd=Math.hypot(vx,vy);
    var sp=(n.state==='flee'?2.6:1.3)*dt;
    if(n.state==='flee'){vx=n.hx-n.x;vy=n.hy-n.y;vd=Math.hypot(vx,vy)}
    if(vd>.35){
      n.face=Math.atan2(vy,vx);
      var ok=moveAnimal(n,vx/vd*sp,vy/vd*sp,.25);
      if(!ok){n.stuck+=dt;if(n.stuck>1.2){n.stuck=0;n.state='idle';n.timer=1.5}}else n.stuck=0;
    }else if(n.state==='goto'){
      n.stuck=0;
      if(n.act==='eat'&&n.plant){
        n.state='eat';n.eatT=2.4;n.face=Math.atan2(n.plant.y-n.y,n.plant.x-n.x);
        if(dist(n.x,n.y,S.px,S.py)<14&&n.talkCd<=0){npcSay(n,'food');n.point={x:n.plant.x,y:n.plant.y,t:2.6}}
        if(dist(n.x,n.y,S.px,S.py)<10)hint('npceat','Cizinec něco jí a ukazuje na to. Sleduj, co dělá a co říká.');
      }else if(n.act==='drink'){
        n.state='eat';n.eatT=2;if(dist(n.x,n.y,S.px,S.py)<14&&n.talkCd<=0){npcSay(n,'water');n.point={x:n.tx,y:n.ty,t:2}}
      }
      n.act=null;
    }else if(n.state==='flee'){n.state='idle';n.timer=3}
  }
}
function conceptOf(tr,w){
  for(var i=0;i<CONCEPTS.length;i++){var c=CONCEPTS[i][0],v=tr.vocab[c];if(v.length===w.length&&v.every(function(x,k){return x===w[k]}))return c}
  return null;
}
function nearestNPC(maxD){
  var best=null,bd=maxD;
  npcs.forEach(function(n){var d=dist(n.x,n.y,S.px,S.py);if(d<bd){bd=d;best=n}});
  return best;
}
function giftBack(n){
  var tr=world.tribes[n.tr],list=[],i;
  for(i=0;i<world.species.length;i++)if(bestSafePart(i)!==null)list.push(i);
  if(!list.length)return;
  var sp=list[Math.floor(Math.random()*list.length)],part=bestSafePart(sp);
  if(Math.random()<.5){addItem({kind:'plant',sp:sp,part:part,ripe:1,season:seasonIdx(),soil:T.GRASS,cooked:1},2);say('Cizinec ti podal upečenou rostlinu.','good')}
  else{addItem({kind:'seed',sp:sp,dl:0},2);say('Cizinec ti dal semena.','good')}
  S.rel[n.tr].last=S.t;milestone('gift');
}
function handleNPCWord(n,w){
  var tr=world.tribes[n.tr],c=conceptOf(tr,w);
  if(!c){n.emote='?';n.emoteT=2;return}
  n.face=Math.atan2(S.py-n.y,S.px-n.x);
  if(c==='food'){
    if(S.t-S.rel[n.tr].last>DAY_MIN){giftBack(n);npcSay(n,'yes');n.emote='nod';n.emoteT=1.6}
    else{npcSay(n,'no');n.emote='shake';n.emoteT=1.6}
  }else if(c==='water'){
    var p=nearestPond(n.x,n.y,22);if(p)n.point={x:p.x,y:p.y,t:4};npcSay(n,'water');
  }else if(c==='danger'){npcSay(n,'danger');n.emote='!';n.emoteT=2}
  else if(c==='fire'){n.point={x:n.hx,y:n.hy,t:4};npcSay(n,'fire')}
  else if(c==='yes'){n.emote='nod';n.emoteT=1.6}
  else if(c==='no'){n.emote='shake';n.emoteT=1.6}
  else if(c==='poison'){
    var best=null,bd=20;
    world.plants.forEach(function(pl){if(plantGone(pl)||!cropReady(pl)||bestSafePart(pl.sp)!==null)return;var d=dist(pl.x,pl.y,n.x,n.y);if(d<bd){bd=d;best=pl}});
    if(best)n.point={x:best.x,y:best.y,t:4};
    npcSay(n,'poison');n.emote='shake';n.emoteT=1.6;
  }
}
function speakWord(w){
  pBubble={w:w.slice(),t:3.4};sfx.word(w);
  var n=nearestNPC(6);
  if(n)setTimeout(function(){if(S&&!S.dead)handleNPCWord(n,w)},520);
}
function askByPoint(pl){
  pointFx={x:pl.x,y:pl.y,t:2.4};
  var n=nearestNPC(7);if(!n||n.askCd>0)return;
  n.askCd=4;n.face=Math.atan2(S.py-n.y,S.px-n.x);
  setTimeout(function(){
    if(!S||S.dead)return;
    if(bestSafePart(pl.sp)!==null){npcSay(n,'food');n.emote='nod';n.emoteT=1.6}
    else{npcSay(n,'poison');n.emote='shake';n.emoteT=1.6}
  },500);
}
function giveTo(n,idx){
  var s=S.inv[idx];if(!s)return;
  var it=Object.assign({},s);
  if(s.kind==='plant'||s.kind==='meat'){
    s.qty-=1;S.inv=S.inv.filter(function(x){return x.qty>0});
    if(s.kind==='plant'&&harmful(it)){npcSay(n,'poison');n.emote='shake';n.emoteT=1.8;say('Cizinec zavrtěl hlavou a věc zahodil.')}
    else{
      npcSay(n,'yes');n.emote='nod';n.emoteT=1.6;
      if(S.t-S.rel[n.tr].last>DAY_MIN*.5)giftBack(n);else say('Cizinec se spokojeně usmívá.');
    }
  }else{n.emote='?';n.emoteT=2;say('Cizinec neví, co s tím.')}
}
function openSpeech(){$('speech').hidden=false;renderSpeech()}
function closeSpeech(){$('speech').hidden=true;spBuf=[]}
function renderSpeech(){
  var el=$('speech');
  el.innerHTML='<div class="syl">'+SYL.map(function(s,i){return '<button class="btn" data-syl="'+i+'">'+s+'<small>'+((i+1)%10)+'</small></button>'}).join('')
    +'</div><div class="word-row"><div class="cur" id="spCur">'+(spBuf.length?esc(wordStr(spBuf)):'Slabiky: Shift + 1 až 0')+'</div><button class="btn primary sm" data-sp="say">Říct</button>'+(S.lastHeard?'<button class="btn sm" data-sp="again" title="Naposledy slyšené slovo">Zopakovat: '+esc(wordStr(S.lastHeard))+'</button>':'')+'<button class="btn sm" data-sp="del">Smazat</button><button class="btn sm" data-sp="close">Zavřít</button></div>';
}
function addSyl(i){if(spBuf.length>=3)return;spBuf.push(i);sfx.syl(i);var c=$('spCur');if(c)c.textContent=wordStr(spBuf)}
function sayWord(){if(!spBuf.length)return;var w=spBuf.slice();spBuf=[];var c=$('spCur');if(c)c.textContent='Slabiky: Shift + 1 až 0';speakWord(w)}
function delSyl(){spBuf.pop();var c=$('spCur');if(c)c.textContent=spBuf.length?wordStr(spBuf):'Slabiky: Shift + 1 až 0'}

/* ================= actions ================= */
var atkCd=0,fishCd=0;
function growDays(){return [3,3,5,8][seasonIdx()]}
function addCrop(rec){
  var pl={id:world.plants.length,sp:rec.sp,x:rec.x,y:rec.y,tile:rec.tile,off:0,crop:true,dl:rec.dl||0,ready:rec.ready};
  world.plants.push(pl);return pl;
}
function seedItemFor(pl){
  return {kind:'seed',sp:pl.sp,dl:pl.crop?Math.min(4,(pl.dl||0)+1):0};
}
function pluck(pl,part){
  var c=pcOf(pl.id),season=seasonIdx(),L=skillLvl('gather'),extra=Math.random()<.12*L?1:0,dl=pl.dl||0;
  if(!cropReady(pl)&&part!=='root'){say('Je to ještě malá sazenice.');return}
  if(part==='leaf'){
    if(c.l&&c.l>S.t){say('Tady už žádné listy nezbyly.');return}
    if(addItem({kind:'plant',sp:pl.sp,part:'leaf',ripe:0,season:season,soil:pl.tile,cooked:0,dl:dl},1+extra)){c.l=S.t+700;say('Utrhl jsi list.');practice('gather',1);sfx.pick()}
  }else if(part==='fruit'){
    var st=fruitStage(pl);if(st<0){say('Žádné plody tu nejsou.');return}
    if(addItem({kind:'plant',sp:pl.sp,part:'fruit',ripe:st,season:season,soil:pl.tile,cooked:0,dl:dl},1+extra)){
      c.f=S.t+DAY_MIN;say('Utrhl jsi plod.');practice('gather',1);sfx.pick();
      if(pl.crop)milestone('farm');
    }
  }else if(part==='seed'){
    if(c.s&&c.s>S.t){say('Semena už jsou pryč.');return}
    if(addItem(seedItemFor(pl),1+(Math.random()<.4?1:0))){c.s=S.t+DAY_MIN;say('Sebral jsi semena.');practice('gather',1);sfx.pick()}
  }else{
    if(addItem({kind:'plant',sp:pl.sp,part:'root',ripe:0,season:season,soil:pl.tile,cooked:0,dl:dl},1+extra)){
      c.g=S.t+4*DAY_MIN;say('Vykopal jsi kořen. Rostlina je pryč.');practice('gather',1.2);sfx.pick();
      if(pl.crop){milestone('farm');S.crops=S.crops.filter(function(x){return !(Math.abs(x.x-pl.x)<.01&&Math.abs(x.y-pl.y)<.01)})}
      target=null;closeMenu();
    }
  }
}
function facingTile(){
  var a=S.face,fx=Math.cos(a),fy=Math.sin(a),x,y;
  if(Math.abs(fx)>Math.abs(fy)){x=Math.floor(S.px)+(fx>0?1:-1);y=Math.floor(S.py)}else{x=Math.floor(S.px);y=Math.floor(S.py)+(fy>0?1:-1)}
  return {x:x,y:y};
}
function freeFacing(){
  var f=facingTile();
  if(f.x<0||f.y<0||f.x>=W||f.y>=H||blockedTile(f.x,f.y))return null;
  if(Math.floor(S.px)===f.x&&Math.floor(S.py)===f.y)return null;
  return f;
}
function buildWall(){
  var cost=skillLvl('build')>=3?1:2;
  if(count('stick')<cost){say('Na zeď potřebuješ '+cost+' klacky.');return}
  var f=freeFacing();if(!f){say('Tady stavět nejde.');return}
  takeKind('stick',cost);S.walls[f.y*W+f.x]=1;say('Postavil jsi zeď z klacků.');practice('build',1);sfx.build();
}
function makeFire(){
  var cost=skillLvl('craft')>=3?2:3;
  if(count('stick')<cost||count('stone')<1){say('Na oheň potřebuješ '+cost+' klacky a kámen.');return}
  var f=freeFacing();if(!f){say('Tady oheň nerozdělíš.');return}
  takeKind('stick',cost);takeKind('stone',1);S.fires.push({x:f.x+.5,y:f.y+.5,burn:720});
  say('Oheň hoří. Je u něj teplo.','good');practice('craft',3);sfx.fire();milestone('fire');burst(f.x+.5,f.y+.5,12,'#f2b03f',2,.8);
}
function makeSpear(){
  if(count('spear')>0){say('Kopí už máš.');return}
  if(count('stick')<1||count('stone')<1){say('Na kopí potřebuješ klacek a kámen.');return}
  takeKind('stick',1);takeKind('stone',1);addItem({kind:'spear'});say('Ostrý kámen ke klacku. Máš kopí.','good');practice('craft',3);milestone('spear');sfx.build();
}
function makePot(){
  if(count('clay')<2){say('Na nádobu potřebuješ 2 jíl z bahna.');return}
  if(!nearFire(2.6)){say('Nádobu musíš vypálit u ohně.');return}
  takeKind('clay',2);if(addItem({kind:'pot',water:0})){say('Vypálil jsi nádobu.','good');practice('craft',3);milestone('pot');sfx.fire()}
}
function buildTrap(){
  if(count('stick')<3){say('Na past potřebuješ 3 klacky.');return}
  var f=freeFacing();if(!f){say('Tady past nepostavíš.');return}
  takeKind('stick',3);S.traps.push({x:f.x+.5,y:f.y+.5,caught:null});say('Postavil jsi past. Zvěř tudy možná půjde.');practice('craft',2);sfx.build();
}
function plantSeed(idx){
  var s=S.inv[idx];if(!s||s.kind!=='seed')return;
  var f=freeFacing();if(!f){say('Tady nic nezasadíš.');return}
  var t=world.tiles[f.y*W+f.x];
  if(t!==T.GRASS&&t!==T.FOREST&&t!==T.SAND&&t!==T.BOG){say('Do skály nic nevyroste.');return}
  var near=world.plants.some(function(p){return !plantGone(p)&&dist(p.x,p.y,f.x+.5,f.y+.5)<.7});
  if(near){say('Tady už něco roste.');return}
  var ready=S.t+growDays()*DAY_MIN,rec={sp:s.sp,x:f.x+.5,y:f.y+.5,tile:t,dl:s.dl||0,ready:ready};
  addCrop(rec);S.crops.push(rec);
  s.qty-=1;S.inv=S.inv.filter(function(x){return x.qty>0});
  say(seasonIdx()===3?'Zasadil jsi semeno do zimy. Poroste pomalu.':'Zasadil jsi semeno.');practice('gather',1);sfx.pick();
}
function waterAt(t){
  var tt=world.tiles[t.ty*W+t.tx];
  return tt===T.POND?1:tt===T.BOG?2:4;
}
function drinkKind(kind){
  if(kind===1){S.hydration=Math.min(100,S.hydration+40);say('Čistá, studená voda.','good');sfx.splash()}
  else if(kind===3){S.hydration=Math.min(100,S.hydration+40);say('Převařená voda. Chutná po kouři, ale je čistá.','good');sfx.splash()}
  else if(kind===2){
    var g=S.gen>=2;
    S.hydration=Math.min(100,S.hydration+(g?14:30));
    if(g)say('Zápach tě odpuzuje. Napil ses jen trochu.');else say('Voda je kalná a zapáchá, ale uhasila žízeň.');
    if(Math.random()<(g?.25:.55)){schedule(120+Math.random()*380,'sick',25+Math.random()*20);if(Math.random()<.4)schedule(90+Math.random()*250,'diarrhea',.4)}
    sfx.splash();
  }else{S.hydration=Math.max(0,S.hydration-8);say('Je slaná. Žízeň to jen zhoršilo.','warn')}
}
function fillPot(t){
  for(var i=0;i<S.inv.length;i++){var s=S.inv[i];if(s.kind==='pot'&&!s.water){s.water=waterAt(t);s.key=itemKey(s);say('Naplnil jsi nádobu.');sfx.splash();return}}
  say(count('pot')?'Nádoba je plná.':'Nemáš nádobu.');
}
function digClay(){if(addItem({kind:'clay'},1)){say('Nabral jsi jíl z bahna.');practice('gather',.5);sfx.pick()}}
function fish(t){
  if(fishCd>0)return;fishCd=1.2;
  if(count('spear')<1){say('Na rybařeni potřebuješ kopí.');return}
  var L=skillLvl('hunt');
  if(Math.random()<.32+.07*L){if(addItem({kind:'meat',cooked:0,fish:true},1)){say('Bodl jsi rybu.','good');practice('hunt',1.5);milestone('fish');sfx.splash()}}
  else{say('Ryba ti proklouzla.');practice('hunt',.4);sfx.splash()}
}
function attack(a){
  if(atkCd>0)return;
  var d=dist(S.px,S.py,a.x,a.y);
  if(d>1.6){say('Je moc daleko.');return}
  atkCd=.45;sfx.hit();
  if(a.kind==='rabbit'){a.dead=true;a.deadAt=S.t;S.pressure+=1;say('Chytil jsi malé zvíře.','good');practice('hunt',2);milestone('hunt');return}
  var hasSpear=count('spear')>0,dmg=hasSpear?3+(skillLvl('hunt')>=3?1:0):1;
  if(a.state==='exhaust')dmg*=2;
  a.hp-=dmg;a.aggro=25;burst(a.x,a.y,6,'#c8bfa9',1.6,.5);practice('hunt',1.2);
  if(a.hp<=0){a.dead=true;a.deadAt=S.t;a.state='dead';S.pressure+=1;
    say(a.kind==='boar'?'Divočák padl.':a.kind==='deer'?'Jelen padl.':'Vlk padl.','good');practice('hunt',4);milestone('hunt')}
  else{
    say(hasSpear?'Zasáhl jsi ho kopím.':'Praštil jsi ho pěstí. Spíš ho to rozzuřilo.');
    if(a.kind==='boar'&&a.state==='wander'){a.state='alert';a.timer=.5}
    if(a.kind==='deer'){a.state='flee'}
  }
}
function toggleSleep(){
  if(S.sleeping){endSleep();say('Probudil ses.');return}
  if(S.energy>85){say('Nejsi dost unavený.');return}
  if(S.warmth<22){say('Je ti moc zima na spaní.','warn');return}
  S.sleeping=true;S.sleepT=0;moveTo=null;target=null;closeMenu();say('Ležíš a spíš. Čas neběží rychleji, ale síly se vracejí.');
}
function cook(idx,mode){
  var s=S.inv[idx];if(!s||s.cooked||(s.kind!=='plant'&&s.kind!=='meat'))return;
  if(!nearFire(2.4)){say('Musíš být u ohně.');return}
  var it=Object.assign({},s);it.at=s.at;
  if(mode==='boil'){
    var pot=null;for(var i=0;i<S.inv.length;i++){var p=S.inv[i];if(p.kind==='pot'&&p.water&&p.water!==4){pot=p;break}}
    if(!pot){say('K vaření potřebuješ nádobu s vodou.');return}
    pot.water=0;pot.key=itemKey(pot);it.cooked=2;
  }else it.cooked=1;
  s.qty-=1;S.inv=S.inv.filter(function(x){return x.qty>0});
  addItem(it);say(mode==='boil'?'Uvařil jsi to v nádobě.':'Upekl jsi to na ohni.');practice('craft',.8);sfx.fire();
}
function boilWater(idx){
  var s=S.inv[idx];if(!s||s.kind!=='pot'||!s.water)return;
  if(!nearFire(2.4)){say('Musíš být u ohně.');return}
  if(s.water===4){say('Slanou vodu převařením neopravíš.');return}
  s.water=3;s.key=itemKey(s);say('Převařil jsi vodu.');sfx.fire();
}
function drinkPot(idx){
  var s=S.inv[idx];if(!s||s.kind!=='pot'||!s.water)return;
  var w=s.water;s.water=0;s.key=itemKey(s);drinkKind(w);
}

/* ================= targets & menu ================= */
function pickAt(wx,wy){
  var i,best=null,bd;
  for(i=0;i<npcs.length;i++){if(dist(npcs[i].x,npcs[i].y,wx,wy)<.8)return {kind:'npc',ref:npcs[i]}}
  for(i=0;i<animals.length;i++){var a=animals[i];if(dist(a.x,a.y,wx,wy)<.75)return {kind:'animal',ref:a}}
  for(i=0;i<S.corpses.length;i++){var cp=S.corpses[i];if(dist(cp.x,cp.y,wx,wy)<.8)return {kind:'corpse',ref:cp}}
  for(i=0;i<S.traps.length;i++){var tp=S.traps[i];if(dist(tp.x,tp.y,wx,wy)<.6)return {kind:'trap',ref:tp}}
  for(i=0;i<S.fires.length;i++){var f=S.fires[i];if(dist(f.x,f.y,wx,wy)<.6)return {kind:'fire',ref:f}}
  for(i=0;i<S.signs.length;i++){var sg=S.signs[i];if(dist(sg.x,sg.y,wx,wy)<.6)return {kind:'sign',ref:sg}}
  for(i=0;i<world.ruins.length;i++){var ru=world.ruins[i];if(dist(ru.x,ru.y,wx,wy)<.8)return {kind:'sign',ref:ru}}
  bd=.6;
  for(i=0;i<world.plants.length;i++){var pl=world.plants[i];if(plantGone(pl))continue;var pd=dist(pl.x,pl.y,wx,wy);if(pd<bd){bd=pd;best=pl}}
  if(best)return {kind:'plant',ref:best};
  var tx=Math.floor(wx),ty=Math.floor(wy);if(tx<0||ty<0||tx>=W||ty>=H)return null;
  var k=ty*W+tx;
  if(world.trees.has(k))return {kind:'tree',x:tx+.5,y:ty+.5,tx:tx,ty:ty};
  if(S.walls[k])return {kind:'wall',ref:k,x:tx+.5,y:ty+.5,tx:tx,ty:ty};
  var t=world.tiles[k];
  if(t===T.SEA||t===T.POND||t===T.BOG)return {kind:'water',x:tx+.5,y:ty+.5,tx:tx,ty:ty};
  if(t===T.ROCK)return {kind:'rock',x:tx+.5,y:ty+.5,tx:tx,ty:ty};
  return null;
}
function meatQty(kind){return kind==='boar'?3:kind==='deer'?4:kind==='wolf'?2:1}
function menuItems(t){
  var out=[],title='';
  if(t.kind==='plant'){
    var pl=t.ref,lab=S.labels[pl.sp];title=lab&&lab.name&&lab.name.length?wordStr(lab.name):'Rostlina';
    if(pl.crop&&!cropReady(pl))title+=' (sazenice)';
    out.push({l:'Utrhnout list',f:function(){pluck(pl,'leaf')}});
    if(fruitStage(pl)>=0)out.push({l:'Utrhnout plod',f:function(){pluck(pl,'fruit')}});
    out.push({l:'Sebrat semena',f:function(){pluck(pl,'seed')}});
    out.push({l:'Vykopat kořen',f:function(){pluck(pl,'root')}});
    out.push({l:'Poznámka',f:function(){openSheet('note',{sp:pl.sp})}});
    out.push({l:'Ukázat cizinci',f:function(){askByPoint(pl)}});
  }else if(t.kind==='tree'){
    title='Strom';
    out.push({l:'Nasbírat klacky',f:function(){
      var c=pcOf('t'+t.ty*W+t.tx);if(c.c===undefined||S.t>c.r){c.c=3;c.r=S.t+DAY_MIN}
      if(c.c<=0){say('Spadlé větve jsou pryč. Zkus to zítra.');return}
      c.c--;if(addItem({kind:'stick'},1+(Math.random()<.5?1:0))){say('Nasbíral jsi klacky.');sfx.pick()}else c.c++;
    }});
  }else if(t.kind==='rock'){
    title='Skála';out.push({l:'Vzít kámen',f:function(){if(addItem({kind:'stone'},1)){say('Vzal jsi kámen.');sfx.pick()}}});
  }else if(t.kind==='water'){
    var wt=world.tiles[t.ty*W+t.tx];title=wt===T.BOG?'Bahno':wt===T.POND?'Voda':'Moře';
    out.push({l:'Napít se',f:function(){drinkKind(waterAt(t))}});
    if(count('pot'))out.push({l:'Naplnit nádobu',f:function(){fillPot(t)}});
    if(wt===T.BOG)out.push({l:'Nabrat jíl',f:function(){digClay()}});
    if((wt===T.POND||wt===T.SEA)&&count('spear'))out.push({l:'Rybařit',f:function(){fish(t)}});
  }else if(t.kind==='animal'){
    var a=t.ref,nm=a.kind==='boar'?'divočák':a.kind==='deer'?'jelen':a.kind==='wolf'?'vlk':'malé zvíře';
    if(a.dead){title='Mrtvé zvíře ('+nm+')';out.push({l:'Stáhnout maso',f:function(){
      if(addItem({kind:'meat',cooked:0},meatQty(a.kind))){animals.splice(animals.indexOf(a),1);target=null;closeMenu();say('Získal jsi maso.')}}})}
    else{title=nm.charAt(0).toUpperCase()+nm.slice(1);out.push({l:count('spear')>0?'Bodnout kopím':'Udeřit',f:function(){attack(a)}})}
  }else if(t.kind==='fire'){
    var f=t.ref;title=f.burn>0?'Oheň':'Popel';
    if(f.burn>0)out.push({l:'Přiložit klacek',f:function(){if(count('stick')<1){say('Nemáš klacek.');return}takeKind('stick',1);f.burn+=300;say('Oheň se rozhořel.')}});
    else out.push({l:'Zapálit znovu (klacek)',f:function(){if(count('stick')<1){say('Nemáš klacek.');return}takeKind('stick',1);f.burn=300;say('Oheň znovu chytil.')}});
    out.push({l:'Batoh (péct a vařit)',f:function(){openSheet('pack')}});
  }else if(t.kind==='sign'){
    var sg=t.ref;title=sg.ancient?'Stará rytina':'Značka';
    out.push({l:'Přečíst',f:function(){openSheet('readsign',sg)}});
    if(!sg.ancient)out.push({l:'Smazat',f:function(){S.signs.splice(S.signs.indexOf(sg),1);target=null;closeMenu()}});
  }else if(t.kind==='wall'){
    title='Zeď';out.push({l:'Rozebrat',f:function(){delete S.walls[t.ref];addItem({kind:'stick'},1);target=null;closeMenu();say('Rozebral jsi zeď. Klacek zůstal.')}});
  }else if(t.kind==='trap'){
    var tp=t.ref;title='Past';
    if(tp.caught)out.push({l:'Vzít úlovek',f:function(){var q=tp.caught.kind==='deer'?3:1;if(addItem({kind:'meat',cooked:0},q)){tp.caught=null;say('Vzal jsi úlovek z pasti.','good')}}});
    out.push({l:'Zvednout past',f:function(){S.traps.splice(S.traps.indexOf(tp),1);addItem({kind:'stick'},2);target=null;closeMenu()}});
  }else if(t.kind==='corpse'){
    var cp=t.ref;title='Tělo předchozího života';
    out.push({l:'Prohledat tělo',f:function(){
      var left=[];cp.inv.forEach(function(s){var q=s.qty,it=Object.assign({},s);delete it.qty;if(!addItem(it,q))left.push(s)});
      cp.inv=left;
      if(!left.length){S.corpses.splice(S.corpses.indexOf(cp),1);target=null;closeMenu();say('Vzal sis své věci.','good')}
    }});
  }else if(t.kind==='npc'){
    title='Cizinec';
    out.push({l:'Promluvit',f:function(){openSpeech()}});
    out.push({l:'Dát dar',f:function(){giveMode=true;openSheet('pack')}});
  }
  return {title:title,items:out};
}
function openMenu(t){menuT=t;renderMenu()}
function closeMenu(){menuT=null;var m=$('menu');if(m)m.hidden=true}
function renderMenu(){
  var m=$('menu');if(!menuT){m.hidden=true;return}
  var mi=menuItems(menuT);
  m.innerHTML='';
  var h=document.createElement('div');h.className='mt';h.textContent=mi.title;m.appendChild(h);
  mi.items.forEach(function(it,i){
    var b=document.createElement('button');b.className='btn sm'+(i===0?' primary':'');b.textContent=it.l;
    b.addEventListener('click',function(){it.f();renderMenu();updateHud()});
    m.appendChild(b);
  });
  m.hidden=false;
}

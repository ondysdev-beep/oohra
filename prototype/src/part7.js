
/* ================= HUD ================= */
var hudT=0,saveT=0,winterSeen=false,lastNight=false,portraitGen=-1,barSig='',lastMeters='';
function pad(n){return n<10?'0'+n:''+n}
function updateHud(){
  if(!S)return;
  var h=Math.floor(hourNow()),m=Math.floor((hourNow()-h)*60);
  $('clock').textContent=SEASONS[seasonIdx()]+' · měsíc '+(yearDay()+1)+'/'+YEAR_DAYS+' · '+pad(h)+':'+pad(m)+(S.sleeping?' · spíš':'');
  var g=G();
  $('gentxt').textContent=g.old?'Pradávný':'Nový · generace '+(S.gen+1);
  if(portraitGen!==S.gen){
    portraitGen=S.gen;var pc=$('portrait').getContext('2d');pc.clearRect(0,0,68,68);
    drawHuman(pc,34,34,22,{gen:S.gen,face:-Math.PI/2+.0});
  }
  var ms=[['Sytost',S.satiety,'#c98a3a'],['Voda',S.hydration,'#4a90a8'],['Síla',S.energy,'#9aa84a'],['Teplo',S.warmth,'#d46a3a'],['Zdraví',S.health,'#b04a4a']];
  var sig=ms.map(function(x){return Math.round(x[1])}).join(',');
  if(sig!==lastMeters){
    lastMeters=sig;
    $('meters').innerHTML=ms.map(function(x){return '<div class="meter"><div class="lab">'+x[0]+'</div><div class="bar"><i style="--v:'+clamp(x[1],0,100)+'%;--c:'+x[2]+'"></i></div></div>'}).join('');
  }
  var sy=[];
  if(S.toxin>25)sy.push('ztěžklé tělo');if(S.fever>30)sy.push('horečka');if(S.tremor>0)sy.push('třes');if(S.stim>0)sy.push('rozjitřený');
  if(S.diarr>0)sy.push('průjem');if(S.bleed>0)sy.push('krvácíš');if(S.limp>1)sy.push('kulháš');if(S.infection>20)sy.push('zánět');
  $('symptoms').textContent=sy.join(' · ');
  var res=count('stick')+' klacků · '+count('stone')+' kamenů'+(count('clay')?' · '+count('clay')+' jílu':'');
  var sig2=res+'|'+S.sleeping+'|'+pointMode+'|'+(!$('speech').hidden);
  if(sig2!==barSig){
    barSig=sig2;
    var b1=[['wall','Zeď'],['fire','Oheň'],['spear','Kopí'],['trap','Past'],['pot','Nádoba'],['sign','Značka'],['sleep',S.sleeping?'Vstát':'Spát']];
    $('bar').innerHTML=b1.map(function(x,i){return '<button class="btn sm'+(x[0]==='sleep'&&S.sleeping?' on':'')+'" data-w="'+x[0]+'">'+x[1]+'<span class="kb">'+(i+1)+'</span></button>'}).join('');
    var b2=[['talk','Mluvit'],['point','Ukázat'],['nod','Kývnout'],['shake','Zavrtět'],['eat','Jíst'],['drink','Pít']];
    $('bar2').innerHTML=b2.map(function(x){return '<button class="btn sm'+((x[0]==='point'&&pointMode)||(x[0]==='talk'&&!$('speech').hidden)?' on':'')+'" data-w="'+x[0]+'">'+x[1]+(x[0]==='talk'?'<span class="kb">T</span>':'')+'</button>'}).join('')+'<span class="res">'+res+'</span>';
  }
  var v=$('veil');
  if(!away){
    if(S.faint>0&&!S.dead){v.hidden=false;v.innerHTML='<div class="vtxt">Tma…</div>'}
    else if(v.dataset.faint==='1'||v.innerHTML.indexOf('Tma')>=0){v.hidden=true;v.innerHTML=''}
  }
}
function onBar(w){
  if(S.dead||S.faint>0)return;
  wakeAudio();
  if(w==='wall')buildWall();else if(w==='fire')makeFire();else if(w==='spear')makeSpear();
  else if(w==='trap')buildTrap();else if(w==='pot')makePot();
  else if(w==='sign')openSheet('sign',{bits:'0'.repeat(144),rec:null});
  else if(w==='sleep')toggleSleep();
  else if(w==='talk'){if($('speech').hidden)openSpeech();else closeSpeech()}
  else if(w==='point'){pointMode=!pointMode;if(pointMode)say('Klepni na rostlinu, na kterou chceš ukázat.')}
  else if(w==='nod'){pBubble={e:'nod',t:1.6,w:null};nearGesture('yes')}
  else if(w==='shake'){pBubble={e:'shake',t:1.6,w:null};nearGesture('no')}
  else if(w==='eat'){pBubble={e:'eat',t:1.8,w:null};var ne=nearestNPC(6);if(ne)setTimeout(function(){if(S&&!S.dead)gestureTo(ne,'food')},520);else say('Nikdo poblíž.')}
  else if(w==='drink'){pBubble={e:'drink',t:1.8,w:null};var nd=nearestNPC(6);if(nd)setTimeout(function(){if(S&&!S.dead)gestureTo(nd,'water')},520);else say('Nikdo poblíž.')}
  barSig='';updateHud();
}
function nearGesture(c){
  var n=nearestNPC(5);if(!n)return;
  if(c==='yes'){n.emote='nod';n.emoteT=1.2}else{n.emote='shake';n.emoteT=1.2}
}

/* ================= daily & step ================= */
function daily(){
  var di=dnDay();
  while(S.lastDay<di){
    S.lastDay++;
    S.signs=S.signs.filter(function(s){return s.perm||S.t<s.at+5*DAY_MIN});
    S.corpses=S.corpses.filter(function(c){return S.t<c.at+4*DAY_MIN});
    S.pressure=Math.max(0,S.pressure-.15);
    ['boar','rabbit','deer','wolf'].forEach(function(k){
      var n=animals.filter(function(a){return a.kind===k&&!a.dead}).length,cap=ECO[k];
      if(n<cap&&Math.random()<clamp(.65-S.pressure*.35,.12,.8))spawnAnimal(k,20);
    });
    animals=animals.filter(function(a){return !a.dead||S.t<a.deadAt+DAY_MIN*.5});
  }
  var si=seasonIdx();
  if(si!==S.lastSeason){
    S.lastSeason=si;
    say(['Jaro. Země se probouzí.','Léto. Dny jsou dlouhé a horké.','Podzim. Plody dozrávají, jídlo bude brzy vzácné.','Zima. Mráz. Potřebuješ oheň a střechu.'][si],'note');
    if(si===3)winterSeen=true;
    if(si===0&&winterSeen){milestone('winter');winterSeen=false}
  }
}
function step(dt){
  if(!S||S.dead)return;
  atkCd=Math.max(0,atkCd-dt);fishCd=Math.max(0,fishCd-dt);
  var gm=dt*DAY_MIN/S.dayLen;
  S.t+=gm;S.cal+=dt/S.calLen;
  S.fires.forEach(function(f){if(f.burn>0)f.burn=Math.max(0,f.burn-gm)});
  runEvents();
  physiology(gm);
  if(S.dead)return;
  movePlayer(dt);
  updateAnimals(dt);
  updateNPCs(dt);
  daily();
  if(S.sleeping&&S.sleepT>=60&&!S.sleepGot){S.sleepGot=true}
  var nt=night();
  if(lastNight&&!nt&&S.intro)milestone('night');
  lastNight=nt;
  for(var i=parts.length-1;i>=0;i--){var p=parts[i];p.x+=p.vx*dt;p.y+=p.vy*dt;p.l-=dt;if(p.l<=0)parts.splice(i,1)}
  if(pBubble){pBubble.t-=dt;if(pBubble.t<=0)pBubble=null}
  if(pointFx){pointFx.t-=dt;if(pointFx.t<=0)pointFx=null}
  if(target){
    if(!targetValid(target)){target=null;closeMenu()}
    else{
      var tp=tpos(target);
      if(dist(S.px,S.py,tp.x,tp.y)<=reachFor(target)&&!menuT){moveTo=null;openMenu(target);sfx.pick()}
    }
  }
  if(menuT&&target&&dist(S.px,S.py,tpos(target).x,tpos(target).y)>reachFor(target)+1.2){closeMenu();target=null}
  if(S.intro&&isSheltered()&&!S.ms.shelter&&S.walls&&Object.keys(S.walls).length>=4)milestone('shelter');
  seenUpdate();
  hudT+=dt;saveT+=dt;
  if(hudT>.25){hudT=0;updateHud()}
  if(saveT>8){saveT=0;save()}
}

/* ================= epochs, leaving, world start ================= */
function runEpoch(){
  S.epoch++;
  mutateSpecies(world.species,S.seed,S.epoch,S.epoch);
  safeCache={key:"",map:{}};
  banner('Nová epocha','Rostliny se změnily. Co víš, nemusí platit.');
  say('Svět se posunul o epochu. Staré poznámky mohou lhát.','warn');save();
}
function leaveGame(){
  save();away=true;closeSheet();
  var v=$('veil');v.hidden=false;
  v.innerHTML='<div class="vbox"><p>Postava se uložila ke spánku. Svět běží dál bez tebe.</p><button class="btn primary" data-act="return">Vrátit se</button></div>';
}
function returnFromAway(){
  away=false;var v=$('veil');v.hidden=true;v.innerHTML='';
  applyOffline();if(S.pendingMsg){say(S.pendingMsg,'note');S.pendingMsg=null}
  daily();updateHud();
}
function startWorld(loaded){
  world=buildWorld(S.seed);
  makeRuins();
  if(S.epoch>0)mutateSpecies(world.species,S.seed,S.epoch,1);
  safeCache={key:"",map:{}};
  S.crops.forEach(function(rec){addCrop(rec)});
  initNPCs();
  parts=[];animals=[];target=null;moveTo=null;pBubble=null;pointFx=null;
  if(!loaded){
    var sp=pickWildSpawn(null,true);S.px=sp.x;S.py=sp.y;
    S.lastDay=dnDay();S.lastSeason=seasonIdx();
  }
  initAnimals();grantOldVoice();
  portraitGen=-1;barSig='';lastMeters='';
  updateHud();
}

/* ================= rendering ================= */
function resize(){
  dpr=Math.min(2,window.devicePixelRatio||1);
  var r=$('game').getBoundingClientRect();
  vw=Math.max(200,r.width);vh=Math.max(200,r.height);
  cv.width=Math.floor(vw*dpr);cv.height=Math.floor(vh*dpr);
  dark.width=Math.floor(vw*dpr);dark.height=Math.floor(vh*dpr);
  baseTs=clamp(Math.min(vw,vh)/13,30,52);ts=baseTs*zoom;
}
var camX=0,camY=0,baseTs=36,zoom=1,mouse=null,seenArr=null,seenFor=null,miniOn=true,miniT=0,mini=null;
function sx(x){return (x-camX)*ts+vw/2}
function sy(y){return (y-camY)*ts+vh/2}
function wx(px){return (px-vw/2)/ts+camX}
function wy(py){return (py-vh/2)/ts+camY}
function tileCol(t,x,y){
  var h=hash2(x,y,world.seed),si=seasonIdx(),v=h>.82?1:h>.64?2:0;
  switch(t){
    case T.SAND:return COL.sand;
    case T.ROCK:return h>.5?COL.rock:'#7d776c';
    case T.POND:return COL.pond;
    case T.SEA:return world.shore[y*W+x]?COL.shore:COL.sea;
    case T.BOG:return COL.bog[si===3?3:v>1?1:v];
    case T.FOREST:return COL.forest[si===3?3:si===2&&h>.5?2:v>1?1:v];
    default:return COL.grass[si===3?3:si===2&&h>.55?2:v>1?1:v];
  }
}
function drawAnimal(a){
  var x=sx(a.x),y=sy(a.y),r=ts*.34,k=a.kind;
  var c=ctx;
  if(k==='boar'&&SPR.on&&SPR.ready.boar&&!a.dead){
    var fl=Math.cos(a.dir||0)>0;
    ell(c,x,y+r*.45,r*1.5,r*.5,0,'rgba(0,0,0,.22)');
    sprDraw(c,'boar',x,y+r*.7,r*2.1,fl);
    if(a.state==='alert'||a.state==='charge'){ell(c,x+(fl?1:-1)*r*1.0,y-r*.5,r*.18,r*.18,0,'#d44')}
    return;
  }
  if(a.dead){ell(c,x,y,r*1.2,r*.6,0,k==='wolf'?'#4a4a4e':k==='deer'?'#8a6a45':k==='boar'?'#5a4636':'#a89a86');return}
  c.save();c.translate(x,y);c.rotate(a.dir||0);
  ell(c,0,r*.5,r*1.3,r*.55,0,'rgba(0,0,0,.2)');
  if(k==='boar'){
    ell(c,0,0,r*1.25,r*.8,0,'#5a4636');ell(c,r*1.05,0,r*.55,r*.45,0,'#4a3a2c');ell(c,r*1.4,-r*.3,r*.15,r*.1,0,'#e8d9bf');ell(c,r*1.4,r*.3,r*.15,r*.1,0,'#e8d9bf');
    if(a.state==='alert'||a.state==='charge'){ell(c,r*1.1,0,r*.2,r*.2,0,'#d44')}
  }else if(k==='rabbit'){
    ell(c,0,0,r*.65,r*.45,0,'#b8a690');ell(c,r*.6,0,r*.28,r*.25,0,'#b8a690');ell(c,r*.65,-r*.2,r*.25,r*.07,.3,'#9c8a76');ell(c,-r*.6,0,r*.18,r*.18,0,'#eee');
  }else if(k==='deer'){
    ell(c,0,0,r*1.1,r*.6,0,'#9a7650');ell(c,r*1.1,0,r*.35,r*.3,0,'#9a7650');
    c.strokeStyle='#6b5036';c.lineWidth=1.5;c.beginPath();c.moveTo(r*1.25,-r*.1);c.lineTo(r*1.6,-r*.5);c.moveTo(r*1.25,r*.1);c.lineTo(r*1.6,r*.5);c.stroke();
  }else{
    ell(c,0,0,r*1.1,r*.55,0,'#5c5c64');ell(c,r*1.0,0,r*.4,r*.32,0,'#4c4c54');
    var al=a.state==='stalk'||a.state==='alert'||a.state==='charge';
    ell(c,r*1.18,-r*.12,r*.07,r*.07,0,al?'#ffd03a':'#999');ell(c,r*1.18,r*.12,r*.07,r*.07,0,al?'#ffd03a':'#999');
  }
  c.restore();
}
function drawBubbleText(x,y,txt,tone){
  var c=ctx;c.font='600 13px system-ui,sans-serif';var w=c.measureText(txt).width+14;
  c.fillStyle='rgba(23,18,15,.88)';c.strokeStyle=tone==='alarm'?'#d4553a':tone==='calm'?'#9ab85a':'rgba(219,162,63,.6)';c.lineWidth=tone?2:1;
  var bx=x-w/2,by=y-22;c.beginPath();c.roundRect?c.roundRect(bx,by,w,22,8):c.rect(bx,by,w,22);c.fill();c.stroke();
  c.fillStyle='#f0e6d2';c.textAlign='center';c.textBaseline='middle';c.fillText(txt,x,by+11);
}
function emoteTxt(e){return e==='nod'?'↕ ano':e==='shake'?'↔ ne':e==='?'?'?':e==='!'?'!':e==='eat'?'jím':e==='drink'?'piju':''}
function drawArrow(px,py,t){
  var c=ctx,x=sx(px),y=sy(py)-ts*.9-Math.sin(t*8)*4;
  c.fillStyle='#dba23f';c.beginPath();c.moveTo(x,y+12);c.lineTo(x-8,y);c.lineTo(x+8,y);c.closePath();c.fill();
}
function draw(now){
  var c=ctx;c.setTransform(dpr,0,0,dpr,0,0);
  camX=S.px;camY=S.py;
  c.fillStyle=COL.sea;c.fillRect(0,0,vw,vh);
  var x0=Math.max(0,Math.floor(wx(0))-1),x1=Math.min(W-1,Math.ceil(wx(vw))+1),y0=Math.max(0,Math.floor(wy(0))-1),y1=Math.min(H-1,Math.ceil(wy(vh))+1);
  var x,y,i,tsz=Math.ceil(ts)+1;
  for(y=y0;y<=y1;y++)for(x=x0;x<=x1;x++){
    c.fillStyle=tileCol(world.tiles[y*W+x],x,y);c.fillRect(Math.floor(sx(x)),Math.floor(sy(y)),tsz,tsz);
  }
  /* pond shimmer */
  for(y=y0;y<=y1;y++)for(x=x0;x<=x1;x++){
    var tt=world.tiles[y*W+x];
    if(tt===T.POND||tt===T.SEA){
      var wv=Math.sin(now/700+x*1.3+y*.9);
      if(wv>.7){c.fillStyle='rgba(255,255,255,.08)';c.fillRect(sx(x)+ts*.2,sy(y)+ts*.4,ts*.5,2)}
    }
  }
  /* signs on ground */
  S.signs.forEach(function(s){if(Math.abs(s.x-camX)<12&&Math.abs(s.y-camY)<9){
    c.fillStyle='rgba(60,45,30,.85)';c.fillRect(sx(s.x)-ts*.3,sy(s.y)-ts*.3,ts*.6,ts*.6);
    drawBits(c,s.bits,sx(s.x)-ts*.26,sy(s.y)-ts*.26,ts*.52,'#e8d9bf');
  }});
  world.ruins.forEach(function(r){if(Math.abs(r.x-camX)<12&&Math.abs(r.y-camY)<9){
    ell(c,sx(r.x),sy(r.y)+ts*.15,ts*.5,ts*.3,0,'#6d6358');
    c.fillStyle='#8b8478';c.fillRect(sx(r.x)-ts*.3,sy(r.y)-ts*.45,ts*.6,ts*.6);
    drawBits(c,r.bits,sx(r.x)-ts*.24,sy(r.y)-ts*.4,ts*.48,'#3a342c');
  }});
  /* camp hearths */
  world.tribes.forEach(function(tr){
    var cx=tr.camp.x+.5,cy=tr.camp.y+.5;
    if(Math.abs(cx-camX)<12&&Math.abs(cy-camY)<9){
      var fl=1+Math.sin(now/90+tr.id)*.15;
      if(!(SPR.on&&sprDraw(c,'fire',sx(cx),sy(cy)+ts*.3,ts*.95*(.96+(fl-1)*.6),false))){
        ell(c,sx(cx),sy(cy),ts*.5,ts*.32,0,'#3a2e24');ell(c,sx(cx),sy(cy)-ts*.08,ts*.2*fl,ts*.28*fl,0,'#e8903a');ell(c,sx(cx),sy(cy)-ts*.05,ts*.1*fl,ts*.16*fl,0,'#f5d36a');}
    }
  });
  S.corpses.forEach(function(cp){if(Math.abs(cp.x-camX)<12&&Math.abs(cp.y-camY)<9)drawHuman(c,sx(cp.x),sy(cp.y),ts*.34,{gen:cp.gen,lying:true})});
  S.traps.forEach(function(t){
    if(Math.abs(t.x-camX)>12||Math.abs(t.y-camY)>9)return;
    c.strokeStyle='#6b5036';c.lineWidth=2;c.beginPath();c.arc(sx(t.x),sy(t.y),ts*.28,0,TAU);c.stroke();
    if(t.caught)ell(c,sx(t.x),sy(t.y),ts*.2,ts*.14,0,'#b8a690');
  });
  /* depth-sorted things */
  var list=[],season=seasonIdx();
  world.plants.forEach(function(pl){
    if(Math.abs(pl.x-camX)>12||Math.abs(pl.y-camY)>9||plantGone(pl))return;
    list.push({y:pl.y,f:function(){
      if(pl.crop&&!cropReady(pl)){var gx=sx(pl.x),gy=sy(pl.y);c.strokeStyle='#6e9a46';c.lineWidth=2;c.beginPath();c.moveTo(gx,gy+4);c.lineTo(gx,gy-6);c.stroke();ell(c,gx-4,gy-5,4,2,-.5,'#86a85a');ell(c,gx+4,gy-7,4,2,.5,'#86a85a');return}
      drawPlantAt(c,world.species[pl.sp],sx(pl.x),sy(pl.y),ts*.3,season,fruitStage(pl));
    }});
  });
  world.trees.forEach(function(t,k){
    if(Math.abs(t.x-camX)>12||Math.abs(t.y-camY)>10)return;
    list.push({y:t.y+.2,f:function(){
      var x=sx(t.x),y=sy(t.y),s=ts*(.55+t.v*.25),cr=season===3?COL.crown[3]:season===2&&t.v>.5?COL.crown[2]:COL.crown[t.v>.5?0:1];
      if(SPR.on&&SPR.ready.oak){
        var nm=t.v<.34?'pine':t.v<.68?'oak':'birch';
        ell(c,x,y+ts*.12,s*.62,s*.26,0,'rgba(0,0,0,.24)');
        sprDraw(c,nm,x,y+ts*.24,ts*(nm==='pine'?1.8:1.5)*(.82+t.v*.35),false,nm==='pine'&&season===2?1:season);
        return;
      }
      ell(c,x,y+ts*.1,s*.7,s*.35,0,'rgba(0,0,0,.22)');
      c.fillStyle='#5a4331';c.fillRect(x-ts*.07,y-ts*.15,ts*.14,ts*.4);
      ell(c,x,y-ts*.35,s*.78,s*.7,0,cr);ell(c,x-s*.3,y-ts*.2,s*.5,s*.45,0,cr);ell(c,x+s*.3,y-ts*.22,s*.45,s*.4,0,cr);
    }});
  });
  for(var wk in S.walls){(function(k){var tx=k%W,ty=Math.floor(k/W);
    if(Math.abs(tx-camX)>12||Math.abs(ty-camY)>10)return;
    list.push({y:ty+.9,f:function(){var x=sx(tx),y=sy(ty);
      if(SPR.on&&SPR.ready.logs){sprDraw(c,'logs',x+ts*.5,y+ts*.95,ts*.95,((tx+ty)&1)===1);return}
      c.fillStyle='#6b5036';c.fillRect(x+1,y+ts*.1,ts-2,ts*.8);
      c.strokeStyle='#4a3826';c.lineWidth=2;for(var q=0;q<4;q++){c.beginPath();c.moveTo(x+q*ts/4+ts/8,y+ts*.1);c.lineTo(x+q*ts/4+ts/8,y+ts*.9);c.stroke()}
    }})})(+wk)}
  S.fires.forEach(function(f){
    if(Math.abs(f.x-camX)>12||Math.abs(f.y-camY)>10)return;
    list.push({y:f.y,f:function(){
      var x=sx(f.x),y=sy(f.y);
      if(SPR.on&&SPR.ready.fire&&f.burn>0){var fl2=1+Math.sin(now/80+f.x*3)*.08;sprDraw(c,'fire',x,y+ts*.25,ts*.85*fl2,false);if(Math.random()<.2)burst(f.x,f.y-.2,1,'#e8903a',.5,.6);return}
      ell(c,x,y+ts*.05,ts*.3,ts*.18,0,'#3a2e24');
      if(f.burn>0){var fl=1+Math.sin(now/80+f.x*3)*.18;ell(c,x,y-ts*.1,ts*.17*fl,ts*.3*fl,0,'#e8903a');ell(c,x,y-ts*.05,ts*.09*fl,ts*.17*fl,0,'#f5d36a');if(Math.random()<.2)burst(f.x,f.y-.2,1,'#e8903a',.5,.6)}
      else ell(c,x,y,ts*.18,ts*.1,0,'#555');
    }});
  });
  animals.forEach(function(a){if(Math.abs(a.x-camX)<12&&Math.abs(a.y-camY)<9)list.push({y:a.y,f:function(){drawAnimal(a)}})});
  npcs.forEach(function(n){
    if(Math.abs(n.x-camX)>12||Math.abs(n.y-camY)>9)return;
    list.push({y:n.y,f:function(){
      var tr=world.tribes[n.tr];
      drawHuman(c,sx(n.x),sy(n.y),ts*.34,{gen:2+n.tr,face:n.face,skin:n.skin,hair:n.hair,tribe:tr.col,tunic:n.tr?'#4a5a6a':'#6a4a3a'});
    }});
  });
  if(!S.dead)list.push({y:S.py,f:function(){
    if(S.sleeping||S.faint>0)drawHuman(c,sx(S.px),sy(S.py),ts*.34,{gen:S.gen,lying:true});
    else drawHuman(c,sx(S.px),sy(S.py),ts*.34,{gen:S.gen,face:S.face});
  }});
  list.sort(function(a,b){return a.y-b.y});
  for(i=0;i<list.length;i++)list[i].f();
  /* bubbles */
  npcs.forEach(function(n){
    if(Math.abs(n.x-camX)>12||Math.abs(n.y-camY)>9)return;
    if(n.say)drawBubbleText(sx(n.x),sy(n.y)-ts*.55,wordStr(n.say.w),n.say.tone);
    else if(n.emote)drawBubbleText(sx(n.x),sy(n.y)-ts*.55,emoteTxt(n.emote));
    if(n.point)drawArrow(n.point.x,n.point.y,now/1000);
  });
  if(pBubble&&!S.dead){
    if(pBubble.w)drawBubbleText(sx(S.px),sy(S.py)-ts*.55,wordStr(pBubble.w));
    else if(pBubble.e)drawBubbleText(sx(S.px),sy(S.py)-ts*.55,emoteTxt(pBubble.e));
  }
  if(pointFx)drawArrow(pointFx.x,pointFx.y,now/1000);
  parts.forEach(function(p){c.globalAlpha=clamp(p.l/p.m,0,1);c.fillStyle=p.c;c.fillRect(sx(p.x),sy(p.y),p.s,p.s)});
  c.globalAlpha=1;
  if(target&&targetValid(target)){var tp=tpos(target);c.strokeStyle='rgba(219,162,63,.8)';c.lineWidth=2;c.beginPath();c.ellipse(sx(tp.x),sy(tp.y),ts*.45,ts*.28,0,0,TAU);c.stroke()}
  if(moveTo){c.strokeStyle='rgba(240,230,210,.5)';c.lineWidth=1.5;c.beginPath();c.arc(sx(moveTo.x),sy(moveTo.y),5,0,TAU);c.stroke()}
  /* night */
  var dk=(1-daylight())*.8+(S.sleeping?.25:0);
  if(dk>.02){
    var d=dctx;d.setTransform(dpr,0,0,dpr,0,0);d.globalCompositeOperation='source-over';d.clearRect(0,0,vw,vh);
    d.fillStyle='rgba(6,9,22,'+clamp(dk,0,.88)+')';d.fillRect(0,0,vw,vh);
    d.globalCompositeOperation='destination-out';
    var light=function(x,y,r,a){var g=d.createRadialGradient(x,y,r*.1,x,y,r);g.addColorStop(0,'rgba(0,0,0,'+a+')');g.addColorStop(1,'rgba(0,0,0,0)');d.fillStyle=g;d.fillRect(x-r,y-r,r*2,r*2)};
    light(sx(S.px),sy(S.py),ts*3.2,.55);
    allFires().forEach(function(f){if(f.burn>0||f.hearth)light(sx(f.x),sy(f.y),ts*(5+Math.sin(now/110+f.x)*.3),.95)});
    c.setTransform(1,0,0,1,0,0);c.drawImage(dark,0,0);c.setTransform(dpr,0,0,dpr,0,0);
  }
  /* altered perception */
  if(S.tremor>0||S.stim>40||S.fever>55){
    var pw=clamp((S.tremor>0?.5:0)+(S.fever>55?.3:0)+(S.stim>40?.2:0),0,.7);
    c.fillStyle='hsla('+((now/20)%360)+',60%,50%,'+(pw*.12)+')';c.fillRect(0,0,vw,vh);
  }
  if(S.health<30){c.fillStyle='rgba(120,0,0,'+(.25*(1-S.health/30))+')';c.fillRect(0,0,vw,vh)}
  /* popisek pod myší (jen na počítači) */
  var hv=null;
  if(mouse&&!paused()&&!pointMode){
    hv=pickAt(wx(mouse.x),wy(mouse.y));
    if(hv){var hp=tpos(hv);c.strokeStyle='rgba(240,230,210,.7)';c.lineWidth=1.5;c.beginPath();c.ellipse(sx(hp.x),sy(hp.y),ts*.42,ts*.26,0,0,TAU);c.stroke();
      var nm=pickName(hv);if(nm)drawBubbleText(mouse.x,mouse.y-8,nm)}
  }
  cv.style.cursor=pointMode?'crosshair':hv?'pointer':'default';
  if(mini&&miniOn){miniT+=1;if(miniT>=15){miniT=0;drawMini()}}
}


/* ================= minimap with fog of war (memory is lost on death) ================= */
function ensureSeen(){
  if(seenFor===S&&seenArr)return;
  seenFor=S;lastSeenTile=-1;seenArr=new Uint8Array(W*H);
  if(S.seenS&&S.seenS.length===W*H)for(var i=0;i<W*H;i++)seenArr[i]=S.seenS.charCodeAt(i)===49?1:0;
}
function packSeen(){
  if(!seenArr||seenFor!==S)return;
  var a=new Array(W*H);for(var i=0;i<W*H;i++)a[i]=seenArr[i]?'1':'0';
  S.seenS=a.join('');
}
var lastSeenTile=-1;
function seenUpdate(){
  ensureSeen();
  var tx=Math.floor(S.px),ty=Math.floor(S.py),k=ty*W+tx;
  if(k===lastSeenTile)return;lastSeenTile=k;
  for(var y=-9;y<=9;y++)for(var x=-9;x<=9;x++){
    if(x*x+y*y>81)continue;var a=tx+x,b=ty+y;
    if(a>=0&&b>=0&&a<W&&b<H)seenArr[b*W+a]=1;
  }
}
var MINI_COL={};
function drawMini(){
  if(!mini||!miniOn||!world||!S)return;
  ensureSeen();
  var c=mini.getContext('2d'),z=mini.width/W;
  c.fillStyle='#0e0b09';c.fillRect(0,0,mini.width,mini.height);
  var cols=[COL.sea,COL.sand,COL.grass[0],COL.forest[0],COL.rock,COL.bog[0],COL.pond];
  for(var y=0;y<H;y++)for(var x=0;x<W;x++){
    if(!seenArr[y*W+x])continue;
    c.fillStyle=cols[world.tiles[y*W+x]];c.fillRect(x*z,y*z,Math.ceil(z),Math.ceil(z));
  }
  c.fillStyle='#e8903a';S.fires.forEach(function(f){c.fillRect(f.x*z-1,f.y*z-1,3,3)});
  c.fillStyle='#e8d9bf';for(var k in S.walls){c.fillRect((k%W)*z,Math.floor(k/W)*z,Math.ceil(z),Math.ceil(z))}
  c.fillStyle='#fff';c.fillRect(S.px*z-2,S.py*z-2,4,4);
  c.strokeStyle='rgba(255,255,255,.6)';c.lineWidth=1;c.strokeRect(.5,.5,mini.width-1,mini.height-1);
}
var PNAME={boar:'Divočák',rabbit:'Králík',deer:'Jelen',wolf:'Vlk'};
function pickName(t){
  if(!t)return '';
  switch(t.kind){
    case 'plant':return 'Rostlina';case 'animal':return (t.ref.dead?'Mrtvý ':'')+(PNAME[t.ref.kind]||'Zvíře');
    case 'npc':return 'Cizinec ('+(t.ref.tr?'modrý':'rudý')+' kmen)';case 'fire':return 'Oheň';case 'sign':return t.ref.ancient?'Starý nápis':'Značka';
    case 'corpse':return 'Tělo';case 'trap':return 'Past';case 'tree':return 'Strom';case 'wall':return 'Zeď';case 'rock':return 'Skála';
    case 'water':{var tt=world.tiles[t.ty*W+t.tx];return tt===T.SEA?'Moře':tt===T.POND?'Jezírko':'Bažina'}
  }
  return '';
}

/* ================= input ================= */
var drag=null;
function canvasPoint(e){var r=cv.getBoundingClientRect();return {x:e.clientX-r.left,y:e.clientY-r.top}}
function onDown(e){
  if(paused()||S.dead)return;
  wakeAudio();
  var p=canvasPoint(e),x=wx(p.x),y=wy(p.y);
  if(pointMode){
    pointMode=false;barSig='';
    var t=pickAt(x,y);if(t&&t.kind==='plant')askByPoint(t.ref);else say('Ukaž na rostlinu.');
    updateHud();return;
  }
  if(S.sleeping){say('Spíš. Použij tlačítko Vstát.');return}
  closeMenu();
  var t2=pickAt(x,y);
  if(t2){target=t2;moveTo=null;if(t2.kind==='npc'||t2.kind==='plant'){}}
  else{target=null;moveTo={x:clamp(x,.5,W-.5),y:clamp(y,.5,H-.5)}}
}
function setupInput(){
  cv.addEventListener('pointerdown',onDown);
  cv.addEventListener('pointermove',function(e){var p=canvasPoint(e);mouse=p});
  cv.addEventListener('pointerleave',function(){mouse=null});
  cv.addEventListener('wheel',function(e){e.preventDefault();zoom=clamp(zoom*(e.deltaY<0?1.1:1/1.1),.7,1.8);ts=baseTs*zoom},{passive:false});
  var keyMap={KeyW:'up',ArrowUp:'up',KeyS:'down',ArrowDown:'down',KeyA:'left',ArrowLeft:'left',KeyD:'right',ArrowRight:'right'};
  window.addEventListener('keydown',function(e){
    if(!S)return;
    if(e.target&&(e.target.tagName==='INPUT'||e.target.tagName==='TEXTAREA'||e.target.tagName==='SELECT'))return;
    if(e.code==='Escape'){if(sheet&&!S.dead&&sheet!=='intro'){closeSheet()}else if(!$('speech').hidden)closeSpeech();else{closeMenu();target=null;pointMode=false}return}
    if(paused()||S.dead)return;
    if(keyMap[e.code]){keys[keyMap[e.code]]=true;e.preventDefault();return}
    if(e.shiftKey&&/^Digit\d$/.test(e.code)&&!$('speech').hidden){var n=+e.code.slice(5);addSyl((n+9)%10);e.preventDefault();return}
    if(!$('speech').hidden){
      if(e.code==='Enter'){sayWord();e.preventDefault()}else if(e.code==='Backspace'){delSyl();e.preventDefault()}
      return;
    }
    if(e.code==='KeyT'){openSpeech();updateHud()}
    else if(e.code==='KeyB')openSheet('pack');
    else if(e.code==='KeyN')openSheet('notebook');
    else if(e.code==='KeyM'){miniOn=!miniOn;if(mini)mini.hidden=!miniOn;if(miniOn)drawMini()}
    else if(/^Digit[1-7]$/.test(e.code)&&!e.shiftKey&&!menuT){onBar(['wall','fire','spear','trap','pot','sign','sleep'][+e.code.slice(5)-1])}
    else if(e.code==='KeyE'&&menuT){var bs=$('menu').querySelectorAll('button');if(bs[0])bs[0].click()}
    else if(/^Digit[1-4]$/.test(e.code)&&menuT){var b2=$('menu').querySelectorAll('button');var bb=b2[+e.code.slice(5)-1];if(bb)bb.click()}
  });
  window.addEventListener('keyup',function(e){if(keyMap[e.code])keys[keyMap[e.code]]=false});
  document.addEventListener('click',function(e){
    var el=e.target.closest?e.target.closest('[data-open],[data-w],[data-syl],[data-sp],[data-act="return"]'):null;
    if(!el)return;
    if(el.dataset.open){if(!S.dead&&!dreamOn){openSheet(el.dataset.open)}}
    else if(el.dataset.w){onBar(el.dataset.w)}
    else if(el.dataset.syl!==undefined){addSyl(+el.dataset.syl)}
    else if(el.dataset.sp==='say'){sayWord()}
    else if(el.dataset.sp==='del'){delSyl()}
    else if(el.dataset.sp==='again'){if(S.lastHeard){spBuf=S.lastHeard.slice(0,3);renderSpeech()}}
    else if(el.dataset.sp==='close'){closeSpeech();updateHud()}
    else if(el.dataset.act==='return'){returnFromAway()}
  });
  $('sheet').addEventListener('click',onSheetClick);
  $('dreamSkip').addEventListener('click',endDream);
  /* sign grid painting */
  document.addEventListener('pointerdown',function(e){
    var g=e.target.closest?e.target.closest('#grid [data-g]'):null;if(!g)return;
    gridDown=true;gridPaint=sheetData.bits.charAt(+g.dataset.g)!=='1';setGridCell(g,gridPaint?'1':'0');e.preventDefault();
  });
  document.addEventListener('pointerover',function(e){
    if(!gridDown)return;var g=e.target.closest?e.target.closest('#grid [data-g]'):null;if(g)setGridCell(g,gridPaint?'1':'0');
  });
  window.addEventListener('pointerup',function(){gridDown=false});
  document.addEventListener('change',function(e){
    var t=e.target;if(!t)return;
    if(t.id==='selDay'){S.dayLen=+t.value;save()}
    else if(t.id==='selCal'){S.calLen=+t.value;save()}
    else if(t.dataset&&t.dataset.vw){var ve=S.vocab[t.dataset.vw];if(ve){ve.meaning=t.value;save()}}
    else if(t.id==='selGen'){S.genMode=t.value;save()}
    else if(t.id==='chkSound'){S.sound=t.checked;save()}
    else if(t.id==='chkSpr'){SPR.on=t.checked;try{localStorage.setItem('oohra-spr',t.checked?'1':'0')}catch(e){}}
  });
  window.addEventListener('blur',function(){for(var k in keys)keys[k]=false;if(S)save()});
  document.addEventListener('visibilitychange',function(){if(document.hidden&&S)save()});
  window.addEventListener('beforeunload',function(){if(S&&!away)save()});
  window.addEventListener('resize',resize);
}

/* ================= main loop ================= */
var lastT=0;
function frame(now){
  var dt=Math.min(.1,(now-lastT)/1000||0);lastT=now;
  try{
    if(dreamOn)drawDream(now);
    if(S&&world){
      if(!paused())step(dt);
      draw(now);
    }
  }catch(err){if(window.console)console.error(err)}
  requestAnimationFrame(frame);
}
function init(){
  cv=$('view');ctx=cv.getContext('2d');dark=document.createElement('canvas');dctx=dark.getContext('2d');
  resize();setupInput();
  mini=document.createElement('canvas');mini.id='mini';mini.width=132;mini.height=132;$('game').appendChild(mini);
  var o=tryLoad();
  if(o){S=o;startWorld(true);
    S.lastDay=Math.min(S.lastDay,dnDay());
    applyOffline();
    if(S.pendingMsg){say(S.pendingMsg,'note');S.pendingMsg=null}
  }else{S=newState(Math.floor(Math.random()*1e9));startWorld(false)}
  updateHud();
  if(!S.intro)openSheet('intro');else if(S.dead)openSheet('death');
  requestAnimationFrame(frame);
}
init();
})();

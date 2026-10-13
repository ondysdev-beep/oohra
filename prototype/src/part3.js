
/* ================= colours & drawing helpers ================= */
var COL={
  sea:'#2b5463',shore:'#3a7282',sand:'#cdb88a',pond:'#3f7f90',rock:'#8b8478',
  grass:['#86a85a','#6e9a46','#a99a4a','#c7d0c6'],
  forest:['#6f9449','#5b8639','#9a8a3e','#b4bfb4'],
  bog:['#5d7048','#52693d','#667043','#8d9b8d'],
  crown:['#4e7d3a','#3d6b30','#b8651f','#6d6358']
};
var SKINS=['#a98c68','#b3966f','#bb9f76','#c5a97e','#cdb286','#d4ba91','#dac29a'];
function plantColors(sp,season){
  var hue=sp.hue,sat=sp.sat,lit=sp.lit;
  if(season===2){hue=hue+(40-hue)*.35;sat*=.9;lit-=2}else if(season===3){sat*=.35;lit+=10}
  return ['hsl('+hue+','+sat+'%,'+lit+'%)','hsl('+hue+','+sat+'%,'+(lit-9)+'%)'];
}
function fruitColor(sp,stage){return stage===0?'#a8b86e':stage===2?'#5a4332':'hsl('+sp.fh+',68%,52%)'}
function rootColor(sp){return 'hsl('+(20+(sp.hue%40))+',40%,'+(40+(sp.id*3)%14)+'%)'}
function ell(c,x,y,rx,ry,rot,col){c.fillStyle=col;c.beginPath();c.ellipse(x,y,Math.max(.1,rx),Math.max(.1,ry),rot||0,0,TAU);c.fill()}
function drawPlantAt(c,sp,x,y,r,season,fs){
  var cols=plantColors(sp,season),k;
  switch(sp.shape){
    case 0:for(k=0;k<6;k++){var a=k/6*TAU;ell(c,x+Math.cos(a)*r*.55,y+Math.sin(a)*r*.55,r*.55,r*.24,a,k%2?cols[1]:cols[0])}break;
    case 1:c.strokeStyle=cols[1];c.lineWidth=Math.max(1,r*.16);c.beginPath();c.moveTo(x,y+r);c.lineTo(x,y-r*1.05);c.stroke();
      for(k=0;k<3;k++){var yy=y+r*.6-k*r*.6;ell(c,x-r*.42,yy,r*.45,r*.18,-.5,cols[0]);ell(c,x+r*.42,yy-r*.15,r*.45,r*.18,.5,cols[1])}break;
    case 2:ell(c,x-r*.5,y+r*.2,r*.62,r*.62,0,cols[1]);ell(c,x+r*.5,y+r*.2,r*.62,r*.62,0,cols[1]);ell(c,x,y-r*.3,r*.7,r*.7,0,cols[0]);break;
    default:for(k=0;k<3;k++){var b=k/3*TAU-Math.PI/2;ell(c,x+Math.cos(b)*r*.55,y+Math.sin(b)*r*.55+r*.1,r*.5,r*.5,0,k%2?cols[1]:cols[0])}ell(c,x,y+r*.1,r*.2,r*.2,0,cols[1]);
  }
  if(fs>=0){var fc=fruitColor(sp,fs);ell(c,x+r*.45,y-r*.3,r*.2,r*.2,0,fc);ell(c,x-r*.4,y+r*.1,r*.2,r*.2,0,fc);ell(c,x+r*.05,y+r*.55,r*.2,r*.2,0,fc)}
}
/* low-resolution drawings (12x12) for signs and ruins */
function bitsForShape(shape){
  var b='',x,y;
  for(y=0;y<12;y++)for(x=0;x<12;x++){
    var dx=x-5.5,dy=y-5.5,rr=Math.hypot(dx,dy),on=false;
    if(shape===0){var a=Math.atan2(dy,dx);on=(rr<1.5)||(rr<5.3&&rr>1.9&&Math.cos(a*4)>.15)}
    else if(shape===1){on=(Math.abs(dx+.5)<.8)||(y>1&&y<10&&Math.abs(Math.abs(dx+.5)-(10-y)*.42)<.85&&rr<6.2)}
    else if(shape===2){on=(Math.hypot(dx+2.3,dy+1.4)<2.6)||(Math.hypot(dx-2.3,dy+1.4)<2.6)||(dy>0&&dy<5.2&&Math.abs(dx)<(5.4-dy)*.8)}
    else{on=(dy>-5&&dy<5&&Math.abs(dx)<(dy+5.4)*.45)}
    b+=on?'1':'0';
  }
  return b;
}
function drawBits(c,bits,x,y,size,col){
  var cell=size/12;c.fillStyle=col;
  for(var i=0;i<144;i++)if(bits.charAt(i)==='1')c.fillRect(x+(i%12)*cell,y+Math.floor(i/12)*cell,Math.ceil(cell),Math.ceil(cell));
}
function drawHuman(c,x,y,r,o){
  var g=Math.min(o.gen||0,GEN_CAP),face=o.face||0;
  var old=g===0;
  var sc=old?1.06:1+.045*g,w=old?r*1.2*sc:r*(1-.04*g)*sc,d=old?r*.92*sc:r*.82*sc;
  var skin=o.skin||SKINS[g],tunic=o.tunic||(g>=2?'#6f5a3e':'#5a4331'),hair=o.hair||'#2f2218';
  c.save();c.translate(x,y);
  ell(c,0,r*.5,d*.95,w*.55,0,'rgba(0,0,0,.22)');
  if(o.lying){
    ell(c,0,0,r*1.05*sc,r*.42*sc,0,tunic);
    ell(c,r*.95*sc,-r*.02,r*.34*sc,r*.34*sc,0,skin);
    c.restore();return;
  }
  c.rotate(face);
  ell(c,0,0,d*.62,w*.98,0,skin);
  if(old){
    /* Pradávný: mohutná postava v kožešinovém plášti, ochra, kostěné korálky, jizvy */
    ell(c,-d*.12,0,d*.62,w*1.08,0,'#4a3626');
    for(var f=0;f<7;f++){var fa=-1.2+f*.4;ell(c,-d*.4+Math.cos(fa)*d*.1,Math.sin(fa)*w*1.0,r*.09,r*.09,0,'#b89b72')}
    c.strokeStyle='#c9553a';c.lineWidth=Math.max(1.2,r*.1);c.beginPath();c.moveTo(d*.05,-w*.9);c.lineTo(d*.28,-w*.5);c.moveTo(d*.05,w*.9);c.lineTo(d*.28,w*.5);c.stroke();
  }
  else if(g>=2)ell(c,-d*.05,0,d*.5,w*.8,0,tunic);else ell(c,-d*.2,0,d*.38,w*.74,0,tunic);
  ell(c,d*.18,-w*.92,r*.17,r*.17,0,skin);ell(c,d*.18,w*.92,r*.17,r*.17,0,skin);
  ell(c,d*.46,0,r*.36,r*.36,0,hair);
  ell(c,d*.56,0,r*.31,r*.31,0,skin);
  if(old){
    c.strokeStyle='#3a2a1c';c.lineWidth=Math.max(1.4,r*.11);c.lineCap='round';c.beginPath();c.arc(d*.56,0,r*.3,-.95,.95);c.stroke();
    ell(c,d*.22,-w*.25,r*.07,r*.07,0,'#e8d9bf');ell(c,d*.27,0,r*.07,r*.07,0,'#e8d9bf');ell(c,d*.22,w*.25,r*.07,r*.07,0,'#e8d9bf');
    c.strokeStyle='#e8d9bf';c.lineWidth=1;c.beginPath();c.moveTo(d*.62,-r*.22);c.lineTo(d*.7,-r*.1);c.stroke();
  }
  else if(g<=1){c.strokeStyle='#3a2a1c';c.lineWidth=Math.max(1.5,r*.13);c.lineCap='round';c.beginPath();c.arc(d*.56,0,r*.3,-.95,.95);c.stroke()}
  if(g>=2){c.strokeStyle=o.adorn||'#c9553a';c.lineWidth=Math.max(1.2,r*.1);c.beginPath();c.moveTo(d*.5,-r*.3);c.lineTo(d*.5,r*.3);c.stroke()}
  if(g>=4){ell(c,d*.2,-w*.32,r*.07,r*.07,0,'#e8d9bf');ell(c,d*.24,0,r*.07,r*.07,0,'#e8d9bf');ell(c,d*.2,w*.32,r*.07,r*.07,0,'#e8d9bf')}
  if(g>=6){c.strokeStyle='#e8d9bf';c.lineWidth=Math.max(1,r*.06);c.beginPath();c.moveTo(d*.66,-r*.2);c.lineTo(d*.72,-r*.05);c.moveTo(d*.66,r*.2);c.lineTo(d*.72,r*.05);c.stroke()}
  if(o.tribe){ell(c,-d*.1,w*.5,r*.1,r*.1,0,o.tribe)}
  c.restore();
}
function drawItemIcon(c,it,s){
  var m=s/2;c.clearRect(0,0,s,s);
  if(it.kind==='plant'){
    var sp=world.species[it.sp],cols=plantColors(sp,it.season);
    if(it.part==='leaf'){ell(c,m,m,s*.38,s*.18,-.7,cols[0]);ell(c,m-s*.05,m+s*.08,s*.26,s*.12,-.7,cols[1]);
      c.strokeStyle=cols[1];c.lineWidth=1.5;c.beginPath();c.moveTo(m-s*.3,m+s*.3);c.lineTo(m+s*.3,m-s*.3);c.stroke()}
    else if(it.part==='root'){ell(c,m,m+s*.05,s*.32,s*.2,.4,rootColor(sp));c.strokeStyle=rootColor(sp);c.lineWidth=2;c.beginPath();c.moveTo(m+s*.2,m-s*.05);c.lineTo(m+s*.4,m-s*.28);c.stroke();ell(c,m+s*.12,m-s*.15,s*.1,s*.05,0,cols[1])}
    else{ell(c,m,m,s*.28,s*.28,0,fruitColor(sp,it.ripe));ell(c,m-s*.08,m-s*.1,s*.07,s*.07,0,'rgba(255,255,255,.35)');ell(c,m+s*.02,m-s*.3,s*.1,s*.05,0.5,cols[1])}
    if(it.cooked){c.fillStyle=(+it.cooked===2)?'rgba(200,220,240,.55)':'rgba(30,18,10,.55)';[[.3,.35],[.65,.55],[.45,.7]].forEach(function(p){c.beginPath();c.arc(s*p[0],s*p[1],s*.05,0,TAU);c.fill()})}
    if(spoiled(it)){c.fillStyle='rgba(90,120,50,.6)';c.beginPath();c.arc(m,m,s*.18,0,TAU);c.fill()}
  }else if(it.kind==='seed'){var sd=world.species[it.sp];[[.35,.4],[.6,.5],[.45,.68]].forEach(function(p){ell(c,s*p[0],s*p[1],s*.09,s*.06,.6,'hsl('+(sd.hue%360)+',30%,40%)')})}
  else if(it.kind==='stick'){c.strokeStyle='#8a6338';c.lineWidth=4;c.lineCap='round';c.beginPath();c.moveTo(s*.2,s*.8);c.lineTo(s*.8,s*.2);c.stroke();c.lineWidth=2;c.beginPath();c.moveTo(s*.5,s*.5);c.lineTo(s*.6,s*.68);c.stroke()}
  else if(it.kind==='stone'){ell(c,m,m+s*.06,s*.3,s*.22,.2,'#8e877b');ell(c,m-s*.08,m-s*.02,s*.12,s*.07,.2,'#b2ab9d')}
  else if(it.kind==='clay'){ell(c,m,m+s*.04,s*.3,s*.22,.1,'#9a6a4a');ell(c,m-s*.06,m-s*.04,s*.12,s*.07,.1,'#b98760')}
  else if(it.kind==='pot'){ell(c,m,m+s*.1,s*.28,s*.24,0,'#a0603a');ell(c,m,m-s*.1,s*.2,s*.07,0,'#7a4528');
    if(it.water)ell(c,m,m-s*.1,s*.16,s*.05,0,it.water===2?'#7b6b3f':it.water===4?'#9db4b8':'#58939f')}
  else if(it.kind==='spear'){c.strokeStyle='#8a6338';c.lineWidth=3;c.lineCap='round';c.beginPath();c.moveTo(s*.15,s*.85);c.lineTo(s*.78,s*.22);c.stroke();c.fillStyle='#9a9387';c.beginPath();c.moveTo(s*.9,s*.1);c.lineTo(s*.7,s*.2);c.lineTo(s*.8,s*.3);c.closePath();c.fill()}
  else if(it.kind==='meat'){
    if(it.fish){ell(c,m,m,s*.34,s*.15,.2,it.cooked?'#8b5e3a':'#9fb4c0');c.fillStyle=it.cooked?'#8b5e3a':'#9fb4c0';c.beginPath();c.moveTo(m+s*.28,m);c.lineTo(m+s*.44,m-s*.12);c.lineTo(m+s*.44,m+s*.12);c.closePath();c.fill()}
    else{ell(c,m,m,s*.32,s*.22,.3,it.cooked?'#7a4a2a':'#b84a4a');ell(c,m+s*.1,m-s*.05,s*.1,s*.08,0,it.cooked?'#a06a3f':'#e0a0a0')}
    if(spoiled(it)){c.fillStyle='rgba(90,120,50,.6)';c.beginPath();c.arc(m,m,s*.16,0,TAU);c.fill()}
  }
}
function iconEl(it,s){var c=document.createElement('canvas');c.width=s*2;c.height=s*2;c.style.width=s+'px';c.style.height=s+'px';c.className='sw';var g=c.getContext('2d');g.scale(2,2);drawItemIcon(g,it,s);return c}
function plantCanvas(sp,px){var c=document.createElement('canvas');c.width=px*2;c.height=px*2;c.style.width=px+'px';c.style.height=px+'px';c.className='sw';var g=c.getContext('2d');g.scale(2,2);drawPlantAt(g,world.species[sp],px/2,px/2,px*.3,seasonIdx(),-1);return c}

/* ================= audio ================= */
var AC=null,nbuf=null;
function ac(){if(AC)return AC;try{var C=window.AudioContext||window.webkitAudioContext;if(C)AC=new C()}catch(e){AC=null}return AC}
function wakeAudio(){var a=ac();if(a&&a.state==='suspended'){try{a.resume()}catch(e){}}}
function tone(f,d,type,vol,delay,slide){
  if(!S||!S.sound)return;var a=ac();if(!a)return;
  try{
    var t0=a.currentTime+(delay||0),o=a.createOscillator(),g=a.createGain();
    o.type=type||'sine';o.frequency.setValueAtTime(f,t0);
    if(slide)o.frequency.exponentialRampToValueAtTime(Math.max(30,slide),t0+d);
    g.gain.setValueAtTime(.0001,t0);g.gain.exponentialRampToValueAtTime(vol||.08,t0+.012);g.gain.exponentialRampToValueAtTime(.0001,t0+d);
    o.connect(g);g.connect(a.destination);o.start(t0);o.stop(t0+d+.04);
  }catch(e){}
}
function noiseBurst(d,vol,delay){
  if(!S||!S.sound)return;var a=ac();if(!a)return;
  try{
    if(!nbuf){nbuf=a.createBuffer(1,a.sampleRate*.4,a.sampleRate);var ch=nbuf.getChannelData(0);for(var i=0;i<ch.length;i++)ch[i]=Math.random()*2-1}
    var t0=a.currentTime+(delay||0),s=a.createBufferSource(),g=a.createGain(),f=a.createBiquadFilter();
    s.buffer=nbuf;f.type='bandpass';f.frequency.value=1400;
    g.gain.setValueAtTime(vol||.07,t0);g.gain.exponentialRampToValueAtTime(.0001,t0+d);
    s.connect(f);f.connect(g);g.connect(a.destination);s.start(t0);s.stop(t0+d+.03);
  }catch(e){}
}
var sfx={
  chime:function(){[523,659,784,1047].forEach(function(f,i){tone(f,.4,'triangle',.07,i*.09)})},
  syl:function(i,delay){var f=170+i*34;tone(f,.17,'triangle',.07,delay||0,f*.86);tone(f*2.01,.12,'sine',.022,delay||0)},
  word:function(w){w.forEach(function(i,k){sfx.syl(i,k*.22)})},
  pick:function(){tone(520,.08,'square',.025);noiseBurst(.05,.03)},
  eat:function(){noiseBurst(.07,.06);noiseBurst(.07,.05,.1)},
  hit:function(){tone(150,.12,'sawtooth',.07,0,70);noiseBurst(.08,.05)},
  hurt:function(){tone(210,.3,'sawtooth',.09,0,60);noiseBurst(.15,.08)},
  alert:function(){tone(740,.1,'square',.05);tone(740,.1,'square',.05,.14)},
  splash:function(){noiseBurst(.18,.05)},
  build:function(){tone(180,.06,'square',.05);tone(140,.07,'square',.05,.08)},
  fire:function(){noiseBurst(.2,.05);tone(330,.15,'sine',.03)}
};

/* ================= UI helpers ================= */
function say(text,cls){
  var log=$('log');if(!log)return;var d=document.createElement('div');if(cls)d.className=cls;d.textContent=text;log.appendChild(d);
  while(log.children.length>5)log.removeChild(log.firstChild);
  setTimeout(function(){if(d.parentNode)d.parentNode.removeChild(d)},9000);
}
function hint(key,text){if(S.hints[key])return;S.hints[key]=1;say(text,'note')}
var MS={
  fire:['První oheň','Teplo a světlo. Noc je o něco méně strašná.'],
  spear:['První kopí','Ostrý kámen na klacku mění lov.'],
  food:['První bezpečné jídlo','Snědl jsi něco, z čeho ti zatím není zle. Zapamatuj si to.'],
  poison:['První otrava','Takhle se učí hranice. Vzpomeň si, co jsi jedl.'],
  hunt:['První úlovek','Maso je síla. Nenech ho dlouho ležet.'],
  mark:['První značka','Kdo přijde po tobě, přečte si, co jsi věděl.'],
  shelter:['První přístřešek','Uzavřený prostor hřeje a dá se v něm bezpečně odejít ze hry.'],
  pot:['První nádoba','Hlína a oheň. Teď umíš vařit a nosit vodu.'],
  farm:['První úroda','Co zasadíš, to vyroste. A každou sklizní o něco lépe.'],
  word:['První slovo','Neznáš jeho význam. Sleduj, co dělají, když ho říkají.'],
  gift:['První dar od cizinců','Kdo se naučí jazyk, získá přátele.'],
  ruin:['Starý nápis','Někdo tu byl před tebou. Věřit mu ale nemusíš.'],
  night:['Přežil jsi noc','Je ráno.'],
  winter:['Přežil jsi zimu','Přichází jaro.'],
  trap:['První past','Trpělivost je také zbraň.'],
  fish:['První ryba','Voda dává i jídlo.']
};
var bannerTimer=null;
function banner(title,text){
  var b=$('banner');if(!b)return;
  b.innerHTML='<b>'+esc(title)+'</b><span>'+esc(text)+'</span>';b.hidden=false;
  if(bannerTimer)clearTimeout(bannerTimer);
  bannerTimer=setTimeout(function(){b.hidden=true},4600);
}
function burst(x,y,n,col,spd,life){
  for(var i=0;i<n;i++){var a=Math.random()*TAU,s=(.4+Math.random())*spd;parts.push({x:x,y:y,vx:Math.cos(a)*s,vy:Math.sin(a)*s,l:life*(.6+Math.random()*.6),m:life,c:col,s:2+Math.random()*2})}
}
function milestone(id){
  if(!S||S.ms[id]||!MS[id])return;
  S.ms[id]=S.t;banner(MS[id][0],MS[id][1]);sfx.chime();burst(S.px,S.py,18,'#dba23f',2.4,1.1);
}

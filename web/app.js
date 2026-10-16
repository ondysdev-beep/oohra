/* Logika stránky Oohra: značky a postavy z pixelů, vykreslení textů podle jazyka, mapa postupu v mlze
   a pixelová mlha s duchy slabik. Obsah (texty, postup, zápisy) je v content.js, který se načítá před tímto souborem. */
/* ===== POMOCNÉ ===== */
function mulberry32(a){return function(){a|=0;a=a+0x6D2B79F5|0;var t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296}}
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
function signSvg(shape,fill){
  var bits=bitsForShape(shape),r='';
  for(var i=0;i<144;i++)if(bits.charAt(i)==='1')r+='<rect x="'+(i%12)+'" y="'+Math.floor(i/12)+'" width="1.03" height="1.03"/>';
  return '<svg viewBox="0 0 12 12" fill="'+fill+'" shape-rendering="crispEdges" aria-hidden="true">'+r+'</svg>';
}
function esc(s){return String(s).replace(/[&<>"]/g,function(c){return {'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]})}
function setMeta(sel,value){var m=document.querySelector(sel);if(m)m.setAttribute('content',value)}
function cssVar(n){return getComputedStyle(document.documentElement).getPropertyValue(n).trim()}

var LANG='cs';
/* Mlha: zapnutá, pokud ji návštěvník tlačítkem nevypnul. STILL = systém chce omezený pohyb (nastaví mlha). */
var FOG=true,STILL=false;
try{FOG=localStorage.getItem('oohra-fog')!=='off'}catch(e){}
try{var sv=localStorage.getItem('oohra-lang');if(sv==='cs'||sv==='en')LANG=sv;else LANG=/^(cs|sk)/i.test(navigator.language||'')?'cs':'en'}catch(e){LANG=/^(cs|sk)/i.test(navigator.language||'')?'cs':'en'}

/* ===== STATICKÁ GRAFIKA ===== */
document.querySelectorAll('svg[data-sign]').forEach(function(s){
  var bits=bitsForShape(+s.getAttribute('data-sign')),r='';
  s.setAttribute('viewBox','0 0 12 12');s.setAttribute('fill','var(--lichen)');s.setAttribute('shape-rendering','crispEdges');
  for(var i=0;i<144;i++)if(bits.charAt(i)==='1')r+='<rect x="'+(i%12)+'" y="'+Math.floor(i/12)+'" width="1.03" height="1.03"/>';
  s.innerHTML=r;
});
/* Postavy: dvě siluety z pixelových obdélníků, Pradávný je širší a nižší, Nový štíhlejší a vyšší */
document.querySelectorAll('svg[data-body]').forEach(function(s){
  var old=s.getAttribute('data-body')==='old',col=old?'var(--blood)':'var(--lichen)',r='';
  function rect(x,y,w,h,o){r+='<rect x="'+x+'" y="'+y+'" width="'+w+'" height="'+h+'" opacity="'+(o||1)+'"/>'}
  if(old){rect(50,18,22,22);rect(36,44,50,46);rect(28,48,10,40,.8);rect(84,48,10,40,.8);rect(42,92,16,44);rect(64,92,16,44)}
  else{rect(54,10,14,16);rect(44,30,34,56);rect(36,34,8,50,.8);rect(78,34,8,50,.8);rect(48,88,12,54);rect(62,88,12,54)}
  s.setAttribute('fill',col);s.setAttribute('shape-rendering','crispEdges');s.innerHTML=r+'<rect x="10" y="144" width="100" height="1.5" opacity=".4"/>';
});

/* ===== VYKRESLENÍ TEXTŮ ===== */
function fmtDate(d){
  var p=d.split('-'),loc=LANG==='cs'?'cs-CZ':'en-GB';
  try{
    if(p.length===3)return new Date(+p[0],+p[1]-1,+p[2]).toLocaleDateString(loc,{day:'numeric',month:'long',year:'numeric'});
    return new Date(+p[0],+p[1]-1,1).toLocaleDateString(loc,{month:'long',year:'numeric'});
  }catch(e){return d}
}
var PCT=0;
function overall(){var tw=0,dn=0;PHASES.forEach(function(p){tw+=p.w;dn+=p.w*p.pct/100});return Math.round(dn/tw*1000)/10}
function render(){
  var T=I18N[LANG];
  document.documentElement.lang=LANG;
  document.querySelectorAll('[data-i]').forEach(function(e){var k=e.getAttribute('data-i');if(T[k]!==undefined&&typeof T[k]==='string')e.textContent=T[k]});
  document.getElementById('h1').innerHTML=T.h1;
  try{if(matchMedia('(hover: none)').matches)document.querySelector('.hint').textContent=T.hint_touch}catch(e){}
  document.querySelectorAll('.lang button').forEach(function(b){b.setAttribute('aria-pressed',b.getAttribute('data-lang')===LANG?'true':'false')});
  /* Titulek a popis stránky podle jazyka (i pro sdílení, když prohlížeč stránku sdílí sám). */
  document.title=CONFIG.name+' · '+T.meta_title;
  setMeta('meta[name="description"]',T.meta_desc);
  setMeta('meta[property="og:title"]',document.title);
  setMeta('meta[property="og:description"]',T.meta_desc);
  setMeta('meta[property="og:locale"]',LANG==='cs'?'cs_CZ':'en_GB');
  /* Mlha: tlačítko a nápověda (bez mlhy nebo při omezeném pohybu nemá „pohni myší“ smysl). */
  document.getElementById('fogBtn').setAttribute('aria-pressed',FOG?'true':'false');
  document.querySelector('.hint').hidden=!FOG||STILL;
  var h='';T.rules.forEach(function(r){h+='<div class="it"><h3>'+esc(r[0])+'</h3><p>'+esc(r[1])+'</p></div>'});
  document.getElementById('rules').innerHTML=h;
  document.getElementById('listA').innerHTML=T.A.map(function(x){return '<li>'+esc(x)+'</li>'}).join('');
  document.getElementById('listB').innerHTML=T.B.map(function(x){return '<li>'+esc(x)+'</li>'}).join('');
  /* značky */
  var g='';for(var i=0;i<4;i++)g+='<div class="gl">'+signSvg(i,'var(--fg)').replace('<svg ','<svg role="img" aria-label="'+T.sign+' '+(i+1)+'" ')+'<div class="mono">'+T.sign+' '+(i+1)+'</div><details><summary>'+T.where+'</summary><p>'+esc(NOTES[LANG][i])+'</p></details></div>';
  document.getElementById('glyphs').innerHTML=g;
  /* zápisy */
  var l='<article class="entry empty"><div class="mono">···</div><div class="tx"><h3>'+esc(T.next_entry)+'</h3></div></article>';
  POSTS.forEach(function(p){l+='<article class="entry"><div class="mono">'+esc(fmtDate(p.d))+'</div><div class="tx"><h3>'+esc(p.t[LANG])+'</h3><p>'+esc(p.b[LANG])+'</p>'+(p.img?'<img src="'+esc(p.img)+'" alt="'+esc(p.t[LANG])+'">':'')+'</div></article>'});
  document.getElementById('log').innerHTML=l;
  /* fragmenty */
  var pl='';
  for(var n=0;n<Math.max(SLOTS,IMAGES.length);n++){
    var im=IMAGES[n];
    pl+=im?'<figure class="plate"><div class="fr"><img src="'+esc(im.src)+'" alt="'+esc(im[LANG])+'"></div><figcaption>'+esc(im[LANG])+'</figcaption></figure>'
          :'<figure class="plate"><div class="fr">'+signSvg(n%4,'var(--fg)')+'</div><figcaption class="mono">'+esc(T.empty)+'</figcaption></figure>';
  }
  document.getElementById('plates').innerHTML=pl;
  /* postup */
  PCT=overall();
  document.getElementById('pct').textContent=Math.round(PCT)+' %';
  var sg='',sl='',rd='',tw=0;PHASES.forEach(function(p){tw+=p.w});
  PHASES.forEach(function(p){
    sg+='<div class="seg" style="flex:'+p.w+'"><i style="width:'+p.pct+'%"></i></div>';
    sl+='<span style="flex:'+p.w+'">'+p.k+'</span>';
    rd+='<li class="'+p.st+'"><span class="k">'+p.k+'</span><span class="t"><b style="font-weight:500">'+esc(p.n[LANG])+'</b><small>'+esc(p.d[LANG])+'</small></span><span class="s">'+T['st_'+p.st]+(p.pct>0&&p.pct<100?' · '+p.pct+' %':'')+'</span></li>';
  });
  var sgEl=document.getElementById('segs');sgEl.innerHTML=sg;sgEl.setAttribute('aria-label',T.aria_prog(Math.round(PCT)));
  document.getElementById('segl').innerHTML=sl;
  document.getElementById('road').innerHTML=rd;
  drawMap();
}
document.querySelectorAll('[data-name]').forEach(function(e){e.textContent=CONFIG.name});
(function(){
  var shown=0;
  [['demo','lnkDemo'],['discord','lnkDiscord']].forEach(function(p){var a=document.getElementById(p[1]);if(CONFIG[p[0]]){a.href=CONFIG[p[0]];a.hidden=false;shown++}});
  if(shown===2)document.getElementById('soon').hidden=true;
})();
document.querySelector('.lang').addEventListener('click',function(e){
  var b=e.target.closest('button');if(!b)return;
  LANG=b.getAttribute('data-lang');try{localStorage.setItem('oohra-lang',LANG)}catch(x){}
  render();
});

/* ===== MAPA VE MLZE ===== */
var MW=72,MH=36,MAP=null;
function buildMap(){
  var r=mulberry32(20261015),N=[];
  function noise(sx,sy,seed){
    var rr=mulberry32(seed),g=[],gx=Math.ceil(MW/sx)+2,gy=Math.ceil(MH/sy)+2,i;
    for(i=0;i<gx*gy;i++)g.push(rr());
    return function(x,y){
      var fx=x/sx,fy=y/sy,ix=Math.floor(fx),iy=Math.floor(fy),tx=fx-ix,ty=fy-iy;
      tx=tx*tx*(3-2*tx);ty=ty*ty*(3-2*ty);
      var a=g[iy*gx+ix],b=g[iy*gx+ix+1],c=g[(iy+1)*gx+ix],d=g[(iy+1)*gx+ix+1];
      return a+(b-a)*tx+(c-a)*ty+(a-b-c+d)*tx*ty;
    };
  }
  var n1=noise(11,11,3),n2=noise(5,5,9),n3=noise(3,3,17),cells=[],x,y,land=[];
  for(y=0;y<MH;y++)for(x=0;x<MW;x++){
    var dx=(x-MW/2)/(MW*.46),dy=(y-MH/2)/(MH*.46),rad=Math.pow(Math.hypot(dx,dy),1.7);
    var e=n1(x,y)*.55+n2(x,y)*.25+n3(x,y)*.1+.18-rad*.62;
    cells.push({e:e,v:r(),k:0});
  }
  var best=1e9,cx=MW/2,cy=MH/2,camp=null;
  for(y=0;y<MH;y++)for(x=0;x<MW;x++){var c=cells[y*MW+x];if(c.e>.5&&c.e<.62){var dd=Math.hypot(x-MW*.5,y-MH*.52);if(dd<best){best=dd;camp=[x,y]}}}
  if(!camp)camp=[MW>>1,MH>>1];
  for(y=0;y<MH;y++)for(x=0;x<MW;x++){var c2=cells[y*MW+x];c2.k=Math.hypot((x-camp[0])*.78,(y-camp[1])*1.5)+c2.v*3.2;if(c2.e>.42)land.push(c2.k)}
  land.sort(function(a,b){return a-b});
  return {cells:cells,camp:camp,land:land};
}
function drawMap(){
  if(!MAP)MAP=buildMap();
  var cv=document.getElementById('map'),c=cv.getContext('2d'),S=10;
  cv.width=MW*S;cv.height=MH*S;
  var share=Math.max(0,Math.min(1,PCT/100));
  var idx=Math.min(MAP.land.length-1,Math.round(share*MAP.land.length));
  var T=share<=0?-1:MAP.land[Math.max(0,idx-1)];
  var pal={deep:cssVar('--m-deep'),shallow:cssVar('--m-shallow'),sand:cssVar('--m-sand'),grass:cssVar('--m-grass'),forest:cssVar('--m-forest'),rock:cssVar('--m-rock'),fog:cssVar('--m-fog'),fog2:cssVar('--m-fog2')};
  for(var y=0;y<MH;y++)for(var x=0;x<MW;x++){
    var cell=MAP.cells[y*MW+x],col,e=cell.e,vis=cell.k<=T;
    if(!vis){col=cell.v>.55?pal.fog2:pal.fog;c.fillStyle=col;c.fillRect(x*S,y*S,S,S);
      if(cell.k<=T+2.2){c.globalAlpha=.35;c.fillStyle=e<.30?pal.deep:e<.42?pal.shallow:e<.48?pal.sand:e<.62?pal.grass:e<.74?pal.forest:pal.rock;c.fillRect(x*S,y*S,S,S);c.globalAlpha=1}
      continue}
    col=e<.30?pal.deep:e<.42?pal.shallow:e<.48?pal.sand:e<.62?pal.grass:e<.74?pal.forest:pal.rock;
    c.fillStyle=col;c.fillRect(x*S,y*S,S,S);
    if(cell.v>.82){c.globalAlpha=.18;c.fillStyle='#000';c.fillRect(x*S,y*S,S,S);c.globalAlpha=1}
  }
  /* tábor */
  var cx=MAP.camp[0]*S,cy=MAP.camp[1]*S;
  c.fillStyle=cssVar('--lichen');c.fillRect(cx+S*.25,cy+S*.25,S*.5,S*.5);
  c.globalAlpha=.35;c.fillRect(cx-S*.5,cy-S*.5,S*2,S*2);c.globalAlpha=1;
  document.getElementById('map').setAttribute('aria-label',I18N[LANG].aria_prog(Math.round(PCT)));
}
try{matchMedia('(prefers-color-scheme: light)').addEventListener('change',function(){setTimeout(drawMap,30)})}catch(e){}
new MutationObserver(function(){setTimeout(drawMap,30)}).observe(document.documentElement,{attributes:true,attributeFilter:['data-theme']});

/* ===== MLHA A POCHODEŇ ===== */
(function(){
  var cv=document.getElementById('fog'),c=cv.getContext('2d'),W=0,H=0,CS=14,cols=0,rows=0,dpr=1,t0=0;
  var still=window.matchMedia&&matchMedia('(prefers-reduced-motion: reduce)').matches;
  var mx=.7,my=.28,tx=.7,ty=.28,lastMove=-1e9,ghosts=[],colM='#2b3a33',colG='#b8c64c',lastCol=-1e9,running=false;
  function hash(x,y){var h=Math.imul(x|0,374761393)+Math.imul(y|0,668265263);h=Math.imul(h^h>>>13,1274126177);return((h^h>>>16)>>>0)/4294967296}
  function vn(x,y){var ix=Math.floor(x),iy=Math.floor(y),fx=x-ix,fy=y-iy;fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy);
    var a=hash(ix,iy),b=hash(ix+1,iy),c2=hash(ix,iy+1),d=hash(ix+1,iy+1);return a+(b-a)*fx+(c2-a)*fy+(a-b-c2+d)*fx*fy}
  function size(){
    dpr=1;W=Math.max(1,innerWidth);H=Math.max(1,innerHeight);cv.width=W;cv.height=H;
    CS=W<640?12:14;cols=Math.ceil(W/CS);rows=Math.ceil(H/CS);
    var r=mulberry32(77),n=W<640?10:20;ghosts=[];
    for(var i=0;i<n;i++)ghosts.push({s:SYL[Math.floor(r()*SYL.length)],x:r(),y:r(),z:46+r()*70,a:.2+r()*.5});
  }
  function colors(){colM=cssVar('--mist')||colM;colG=cssVar('--ghost')||colG}
  var lastDraw=0;
  function frame(ts){
    /* Mlha vypnutá tlačítkem (slabší zařízení): plátno je prázdné a smyčka stojí. */
    if(!FOG){c.clearRect(0,0,W,H);running=false;return}
    if(!still&&ts-lastDraw<30){requestAnimationFrame(frame);return}
    lastDraw=ts;
    var t=ts/1000;
    if(ts-lastCol>1500){colors();lastCol=ts}
    if(!still){
      if(ts-lastMove>3500){tx=.5+Math.cos(t*.23)*.34;ty=.45+Math.sin(t*.31)*.28}
      mx+=(tx-mx)*.06;my+=(ty-my)*.06;
    }
    var px=mx*W,py=my*H,R=Math.min(W,H)*.34+40;
    c.clearRect(0,0,W,H);
    c.fillStyle=colM;
    for(var y=0;y<rows;y++)for(var x=0;x<cols;x++){
      var n=vn(x*.11+t*.035,y*.13-t*.02)*.6+vn(x*.27-t*.05,y*.29+t*.03)*.4;
      var dx=(x*CS+CS/2-px),dy=(y*CS+CS/2-py),d=Math.sqrt(dx*dx+dy*dy)/R;
      var clear=d>=1?1:d*d*(3-2*d);
      var a=Math.max(0,n-.32)*.55*clear;
      a=Math.round(a*7)/7;
      if(a>0.02){c.globalAlpha=a;c.fillRect(x*CS,y*CS,CS-1,CS-1)}
    }
    c.globalAlpha=1;
    /* pochodeň */
    var g=c.createRadialGradient(px,py,0,px,py,R*.9);
    g.addColorStop(0,colG);g.addColorStop(1,'rgba(0,0,0,0)');
    c.globalAlpha=.07;c.fillStyle=g;c.fillRect(px-R,py-R,R*2,R*2);c.globalAlpha=1;
    /* skryté slabiky (při omezeném pohybu se neukazují) */
    c.fillStyle=colG;c.textBaseline='middle';
    for(var i=0;!still&&i<ghosts.length;i++){
      var gh=ghosts[i],gx=gh.x*W,gy=gh.y*H,gd=Math.hypot(gx-px,gy-py)/(R*.95),al=gd>=1?0:(1-gd)*(1-gd)*gh.a*.55;
      if(al>.01){c.globalAlpha=al;c.font='500 '+Math.round(gh.z)+'px "IBM Plex Mono",ui-monospace,monospace';c.fillText(gh.s,gx,gy)}
    }
    c.globalAlpha=1;
    if(!still)requestAnimationFrame(frame);
    else running=false;
  }
  function start(){
    if(running)return;
    running=true;
    if(still){colors();frame(0)}else requestAnimationFrame(frame);
  }
  size();
  window.addEventListener('resize',function(){size();if(still&&FOG)frame(0)});
  window.addEventListener('pointermove',function(e){tx=e.clientX/W;ty=e.clientY/H;lastMove=performance.now()},{passive:true});
  /* Tlačítko Mlha v hlavičce: zapne a vypne mlhu, volba se pamatuje. */
  document.getElementById('fogBtn').addEventListener('click',function(){
    FOG=!FOG;try{localStorage.setItem('oohra-fog',FOG?'on':'off')}catch(x){}
    render();
    if(FOG)start();else frame(0);
  });
  STILL=still;
  start();
})();

render();

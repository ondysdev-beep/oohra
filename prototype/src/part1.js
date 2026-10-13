(function(){
'use strict';

/* ================= basics ================= */
var TAU=Math.PI*2;
var W=84,H=84;
var T={SEA:0,SAND:1,GRASS:2,FOREST:3,ROCK:4,BOG:5,POND:6};
var YEAR_DAYS=12,SEASON_DAYS=3,DAY_MIN=1440,GEN_CAP=6;
var SEASONS=['Jaro','Léto','Podzim','Zima'];
var SAVE_KEY='oohra-0.5-save';
var SYL=['Gooh','Arh','Oo','Mah','Tek','Rin','Ush','Dak','Hee','Lom'];
var CONCEPTS=[['food','jídlo'],['water','voda'],['danger','nebezpečí'],['fire','oheň'],['yes','ano'],['no','ne'],['poison','jed']];
var TAGS=['syté','hořké','pálí','zvracení','průjem','horečka','třes','omdlení','posílilo','léčí'];
var SKN={gather:'Sbírání',hunt:'Lov',craft:'Řemeslo',build:'Stavění'};
var $=function(id){return document.getElementById(id)};
var clamp=function(v,a,b){return Math.max(a,Math.min(b,v))};
var dist=function(ax,ay,bx,by){return Math.hypot(ax-bx,ay-by)};
var esc=function(s){return String(s).replace(/[&<>"']/g,function(c){return {'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]})};
function mulberry32(a){return function(){a|=0;a=a+0x6D2B79F5|0;var t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296}}
function hash2(x,y,seed){var h=Math.imul(x|0,374761393)+Math.imul(y|0,668265263)+Math.imul(seed|0,2147483647);h=Math.imul(h^h>>>13,1274126177);return((h^h>>>16)>>>0)/4294967296}
function vnoise(x,y,seed){var xi=Math.floor(x),yi=Math.floor(y),xf=x-xi,yf=y-yi;var s=function(t){return t*t*(3-2*t)};var a=hash2(xi,yi,seed),b=hash2(xi+1,yi,seed),c=hash2(xi,yi+1,seed),d=hash2(xi+1,yi+1,seed);var u=s(xf),v=s(yf);return a+(b-a)*u+(c-a)*v+(a-b-c+d)*u*v}
function fbm(x,y,seed){var v=0,amp=.5,f=1;for(var i=0;i<4;i++){v+=amp*vnoise(x*f,y*f,seed+i*101);amp/=2;f*=2}return v/.9375}
function shuffle(a,r){a=a.slice();for(var i=a.length-1;i>0;i--){var j=Math.floor(r()*(i+1));var t=a[i];a[i]=a[j];a[j]=t}return a}
function pad2(n){return (n<10?'0':'')+n}
function wordStr(w){return w.map(function(i){return SYL[i]}).join('-')}
function wordKey(w){return w.join('-')}

/* shared runtime variables */
var S=null,world=null,animals=[],npcs=[],parts=[];
var cv,ctx,dark,dctx,dpr=1,vw=800,vh=600,ts=36;
var target=null,moveTo=null,menuT=null,sheet=null,sheetData=null;
var pBubble=null,pointMode=false,giveMode=false,nbTab='plants',spBuf=[],pointFx=null;

/* ================= plant archetypes (hidden composition) =================
   vector order: nutrition, irritant, toxin (cumulative), neurotoxin, stimulant, medicine */
var SOIL={};SOIL[T.BOG]=[1,1.1,1.35,1.2,1,1];SOIL[T.ROCK]=[.7,1,1,1,1,1];SOIL[T.FOREST]=[1.1,1,1,1,1,1];SOIL[T.SAND]=[.85,1,1,1,1,1];
var ARCH=[
 {home:[T.GRASS,T.FOREST],fruit:true,rabbit:true,
  p:{leaf:[.12,.1,0,0,0,0],root:[.08,0,0,0,0,0],fruit:[.75,0,0,0,0,0]},cook:[1.05,.3,.4,1,.6,.6]},
 {home:[T.GRASS,T.SAND],fruit:false,
  p:{leaf:[.05,0,.5,0,0,0],root:[.9,0,.8,0,0,0]},cook:[1.1,1,.04,1,1,1]},
 {home:[T.FOREST,T.BOG],fruit:true,
  p:{leaf:[.08,.9,0,0,0,0],root:[.1,.15,0,0,0,.8],fruit:[.18,.15,0,0,0,0]},cook:[1,.3,1,1,1,.7]},
 {home:[T.GRASS],fruit:false,rabbit:true,
  p:{leaf:[.6,0,.05,0,0,0],root:[.35,0,0,0,0,0]},cook:[1,1,.5,1,1,1],
  sm:function(part,s){return part==='leaf'?[[1.2,1,1,1,1,1],[1,1,4,1,1,1],[.7,1,10,1,1,1],[.3,1,20,1,1,1]][s]:null}},
 {home:[T.GRASS,T.SAND],fruit:true,rabbit:true,
  p:{leaf:[.05,0,.12,0,.8,0],root:[0,0,.35,0,.25,0],fruit:[.15,0,0,0,.1,0]},cook:[1,1,.6,1,.5,1]},
 {home:[T.SAND,T.GRASS],fruit:true,rabbit:true,
  p:{leaf:[.3,0,0,0,0,0],root:[.25,0,0,0,0,0],fruit:[.3,0,0,0,0,0]},cook:[1.2,1,1,1,1,1]},
 {home:[T.FOREST,T.BOG],fruit:true,unripeI:0,
  p:{leaf:[.05,0,0,.3,0,0],root:[.2,0,.2,.45,0,0],fruit:[.65,0,0,.85,0,0]},cook:[1,1,1,.9,1,1]},
 {home:[T.GRASS,T.FOREST],fruit:true,
  p:{leaf:[.12,.35,0,0,0,0],root:[.8,.55,0,0,0,0],fruit:[.4,.2,0,0,0,0]},cook:[1.15,.04,1,1,1,1],
  sm:function(part,s){return part==='root'?[[1,1,1,1,1,1],[1,1,1,1,1,1],[1.15,1,1,1,1,1],[1.2,1,1,1,1,1]][s]:null}}
];

function buildSpecies(seed){
  var r=mulberry32(seed^0x51ED);
  var order=shuffle([0,1,2,3,4,5,6,7],r);
  var leafHues=shuffle([95,120,145,170,75,300,18,55],r);
  var fruitHues=shuffle([0,25,48,280,330,200,160,350],r);
  var shapes=shuffle([0,1,2,3,0,1,2,3],r);
  return order.map(function(a,i){
    var jit={};['leaf','root','fruit'].forEach(function(p){var j=[];for(var k=0;k<6;k++)j.push(.85+r()*.35);jit[p]=j});
    return {id:i,arch:a,hue:leafHues[i]+(r()*14-7),sat:34+r()*20,lit:36+r()*14,fh:fruitHues[i],shape:shapes[i],start:Math.floor(r()*YEAR_DAYS),jit:jit};
  });
}
/* epoch: plants drift a little, old knowledge may be stale */
function mutateSpecies(species,seed,epoch,startE){
  for(var e=startE||1;e<=epoch;e++){
    var r=mulberry32(seed^(e*7919));
    species.forEach(function(sp){
      ['leaf','root','fruit'].forEach(function(p){sp.jit[p]=sp.jit[p].map(function(x){return clamp(x*(r()<.5?(.78+r()*.2):(1+r()*.3)),.45,1.9)})});
      sp.hue+=r()*24-12;sp.start=(sp.start+Math.floor(r()*3)-1+YEAR_DAYS)%YEAR_DAYS;
    });
  }
}

/* ================= tribes, language ================= */
function makeVocab(r,fixed){
  var used={},v={};
  Object.keys(fixed||{}).forEach(function(k){v[k]=fixed[k].slice();used[v[k].join()]=1});
  CONCEPTS.forEach(function(c){
    if(v[c[0]])return;
    var w;do{w=[Math.floor(r()*10),Math.floor(r()*10)]}while(used[w.join()]||w[0]===w[1]);
    used[w.join()]=1;v[c[0]]=w;
  });
  return v;
}

/* ================= world ================= */
function areaLand(tiles,x,y,rad){
  for(var yy=-rad;yy<=rad;yy++)for(var xx=-rad;xx<=rad;xx++){
    var a=x+xx,b=y+yy;if(a<2||b<2||a>=W-2||b>=H-2)return false;
    var t=tiles[b*W+a];if(t===T.SEA||t===T.POND||t===T.ROCK)return false;
  }
  return true;
}
function pondNear(tiles,x,y,rad){
  for(var yy=-rad;yy<=rad;yy++)for(var xx=-rad;xx<=rad;xx++){
    var a=x+xx,b=y+yy;if(a>=0&&b>=0&&a<W&&b<H&&tiles[b*W+a]===T.POND)return true;
  }
  return false;
}
function makeCamps(seed,tiles){
  var r=mulberry32(seed^0x7B1B),camps=[],n,x,y,pass;
  for(pass=0;pass<2&&!camps.length;pass++){
    for(n=0;n<5000&&!camps.length;n++){
      x=4+Math.floor(r()*(W-8));y=4+Math.floor(r()*(H-8));
      if(areaLand(tiles,x,y,2)&&(pass===1||pondNear(tiles,x,y,14)))camps.push({x:x,y:y});
    }
  }
  if(!camps.length)camps.push({x:W>>1,y:H>>1});
  for(pass=0;pass<2&&camps.length<2;pass++){
    for(n=0;n<8000&&camps.length<2;n++){
      x=4+Math.floor(r()*(W-8));y=4+Math.floor(r()*(H-8));
      if(areaLand(tiles,x,y,2)&&dist(x,y,camps[0].x,camps[0].y)>=(pass===0?34:18)&&(pass===1||pondNear(tiles,x,y,14)))camps.push({x:x,y:y});
    }
  }
  if(camps.length<2)camps.push({x:camps[0].x+9,y:camps[0].y});
  return camps;
}
function makeRuinSpots(seed,tiles,camps){
  var r=mulberry32(seed^0x2C7D),spots=[],n,x,y;
  for(n=0;n<8000&&spots.length<6;n++){
    x=5+Math.floor(r()*(W-10));y=5+Math.floor(r()*(H-10));
    var t=tiles[y*W+x];
    if(t===T.SEA||t===T.POND||t===T.BOG)continue;
    if(camps.some(function(c){return dist(x,y,c.x,c.y)<12}))continue;
    if(spots.some(function(s){return dist(x,y,s.x,s.y)<14}))continue;
    spots.push({x:x+.5,y:y+.5});
  }
  return spots;
}

function buildWorld(seed){
  var tiles=new Uint8Array(W*H),shore=new Uint8Array(W*H),x,y;
  for(y=0;y<H;y++)for(x=0;x<W;x++){
    var dx=(x+.5-W/2)/(W/2),dy=(y+.5-H/2)/(H/2),d2=dx*dx+dy*dy;
    var e=fbm(x/17,y/17,seed)-d2*.82+.34,t;
    if(e<.30)t=T.SEA;else if(e<.345)t=T.SAND;else{
      t=T.GRASS;
      var rock=fbm(x/8,y/8,seed+7),forest=fbm(x/9,y/9,seed+13),moist=fbm(x/11,y/11,seed+29);
      if(rock>.66&&e>.42)t=T.ROCK;else if(moist>.62&&e<.52)t=T.BOG;else if(forest>.54)t=T.FOREST;
    }
    tiles[y*W+x]=t;
  }
  var r=mulberry32(seed^0x70D);
  for(var k=0;k<4;k++){
    for(var n=0;n<300;n++){
      var cx=Math.floor(r()*W),cy=Math.floor(r()*H),tt=tiles[cy*W+cx];
      if(tt===T.GRASS||tt===T.FOREST){
        var rx=2+r()*2,ry=1.6+r()*2;
        for(var yy=cy-5;yy<=cy+5;yy++)for(var xx=cx-5;xx<=cx+5;xx++){
          if(xx<1||yy<1||xx>=W-1||yy>=H-1)continue;
          var q=((xx-cx)/rx)*((xx-cx)/rx)+((yy-cy)/ry)*((yy-cy)/ry);
          var cur=tiles[yy*W+xx];
          if(q<1&&cur!==T.SEA&&cur!==T.SAND&&cur!==T.ROCK)tiles[yy*W+xx]=T.POND;
        }
        break;
      }
    }
  }
  for(y=0;y<H;y++)for(x=0;x<W;x++){
    if(tiles[y*W+x]!==T.SEA)continue;
    for(var oy=-1;oy<=1;oy++)for(var ox=-1;ox<=1;ox++){
      var nx=x+ox,ny=y+oy;
      if(nx>=0&&ny>=0&&nx<W&&ny<H){var nt=tiles[ny*W+nx];if(nt!==T.SEA&&nt!==T.POND)shore[y*W+x]=1}
    }
  }
  var camps=makeCamps(seed,tiles),ruinSpots=makeRuinSpots(seed,tiles,camps);
  var species=buildSpecies(seed);
  var rr=mulberry32(seed^0xA5A5);
  var trees=new Map(),plants=[];
  var treeP={},plantP={};
  treeP[T.FOREST]=.42;treeP[T.GRASS]=.045;treeP[T.BOG]=.03;
  plantP[T.FOREST]=.09;plantP[T.GRASS]=.075;plantP[T.BOG]=.10;plantP[T.SAND]=.03;plantP[T.ROCK]=.015;
  var clearNear=function(px,py){
    for(var i=0;i<camps.length;i++)if(Math.abs(px-camps[i].x)<=2&&Math.abs(py-camps[i].y)<=2)return true;
    for(var j=0;j<ruinSpots.length;j++)if(Math.abs(px-Math.floor(ruinSpots[j].x))<=1&&Math.abs(py-Math.floor(ruinSpots[j].y))<=1)return true;
    return false;
  };
  for(y=0;y<H;y++)for(x=0;x<W;x++){
    var tl=tiles[y*W+x],idx=y*W+x;
    var skip=clearNear(x,y);
    if(treeP[tl]&&rr()<treeP[tl]){if(!skip)trees.set(idx,{x:x+.5,y:y+.5,v:rr()});continue}
    if(plantP[tl]&&rr()<plantP[tl]){
      var weights=species.map(function(sp){return ARCH[sp.arch].home.indexOf(tl)>=0?4:1});
      var sum=weights.reduce(function(a,b){return a+b},0),pick=rr()*sum,si=0;
      for(var i=0;i<weights.length;i++){pick-=weights[i];if(pick<=0){si=i;break}}
      var o=rr(),px2=x+.2+rr()*.6,py2=y+.2+rr()*.6;
      if(skip)continue;
      plants.push({id:plants.length,sp:si,x:px2,y:py2,tile:tl,off:o<.33?-1:(o<.66?0:1)});
    }
  }
  /* two foreign tribes with different words for the same ideas */
  var rv=mulberry32(seed^0x1A9);
  /* společný základ (Starý hlas): čtyři slova z jazyka Pradávných znají všechny kmeny */
  var cm=makeVocab(rv,{});
  var common={water:cm.water,danger:cm.danger,yes:cm.yes,no:cm.no};
  var vA=makeVocab(rv,common);
  var vB=makeVocab(rv,{food:vA.poison,poison:vA.food,water:common.water,danger:common.danger,yes:common.yes,no:common.no});
  var tribes=[{id:0,col:'#c9553a',camp:camps[0],vocab:vA},{id:1,col:'#4f86b3',camp:camps[1],vocab:vB}];
  return {seed:seed,tiles:tiles,shore:shore,trees:trees,plants:plants,species:species,tribes:tribes,common:common,ruinSpots:ruinSpots,ruins:[]};
}

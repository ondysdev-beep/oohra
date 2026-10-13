
/* ================= state ================= */
function newState(seed){
  return {v:2,seed:seed,savedAt:Date.now(),t:7*60,cal:0,calLen:180,dayLen:180,genMode:'death',epoch:0,
    px:0,py:0,face:0,gen:0,born:0,
    satiety:70,hydration:75,energy:90,warmth:55,health:100,
    toxin:0,fever:0,infection:0,tremor:0,faint:0,stim:0,diarr:0,bleed:0,limp:0,stun:0,
    toxEp:false,toxProtect:false,fevLog:false,sleeping:false,sleepT:0,
    inv:[],labels:{},eaten:[],events:[],loss:{},lossT:0,hints:{},log:[],vocab:{},
    skills:{gather:0,hunt:0,craft:0,build:0},pend:{gather:0,hunt:0,craft:0,build:0},
    signs:[],walls:{},fires:[],traps:[],crops:[],pc:{},corpses:[],
    deaths:0,dead:false,deathCause:'',lastDeath:null,lastDay:0,lastSeason:0,
    intro:false,dreamDone:false,ms:{},pressure:0,rel:[{last:-99999},{last:-99999}],sound:true};
}
function tryLoad(){
  try{var raw=localStorage.getItem(SAVE_KEY);if(!raw)return null;var o=JSON.parse(raw);if(o&&o.v===2&&typeof o.seed==='number'){if(o.cal===undefined)o.cal=o.t/DAY_MIN;if(!o.calLen)o.calLen=180;if(!o.dayLen)o.dayLen=180;return o}}catch(e){}
  return null;
}
function save(){if(!S)return;if(typeof packSeen==='function')packSeen();S.savedAt=Date.now();try{localStorage.setItem(SAVE_KEY,JSON.stringify(S))}catch(e){}}

/* ================= time ================= */
/* dvě hodiny: S.t = den a noc (herní minuty), S.cal = kalendář světa (měsíce) */
function dayF(){return S.cal}
function dayIdx(){return Math.floor(S.cal)}
function dnDay(){return Math.floor(S.t/DAY_MIN)}
function yearDay(){return dayIdx()%YEAR_DAYS}
function worldYear(){return Math.floor(dayIdx()/YEAR_DAYS)}
function seasonIdx(){return Math.floor(yearDay()/SEASON_DAYS)%4}
function hourNow(){return (S.t%DAY_MIN)/60}
function daylight(){var sh=Math.sin(((hourNow()-6)/24)*TAU);return clamp(sh*1.6+.35,0,1)}
function ambient(){var base=[10,22,8,-4][seasonIdx()];return base+8*(daylight()-.5)}

/* ================= generation (evolution, hidden bonuses) ================= */
function G(){
  var g=Math.min(S.gen,GEN_CAP),old=S.gen===0;
  /* Pradávní (gen 0): mohutní, železný žaludek, odolní zimě a zranění. Noví: rychlejší, učí se rychleji, ale křehčí. Stejný rozpočet síly, jiné rozložení. */
  return {g:g,old:old,slots:10+Math.min(3,Math.floor(g/2)),drain:1-.03*g,heal:old?1.05:1+.04*g,learn:old?1:1+.08*g,
    speed:old?.95:1+Math.min(.1,.02*g),toxMul:old?.6:1+.04*g,badMul:old?.45:1,wound:old?.78:1+.03*g,cold:old?7:-Math.min(4,g)*1.3};
}
function genFor(birthWorldYear){
  if(S.genMode==='year')return Math.min(GEN_CAP,birthWorldYear);
  return null;
}

/* ================= skills (practice is consolidated by sleep) ================= */
function mentorNear(){for(var i=0;i<npcs.length;i++)if(dist(npcs[i].x,npcs[i].y,S.px,S.py)<7)return true;return false}
function practice(sk,amt){S.pend[sk]+=amt*G().learn*(mentorNear()?1.8:1)}
function skillLvl(sk){return Math.min(5,Math.floor(Math.sqrt(S.skills[sk]/2)))}
function consolidate(){
  var out=[],any=false;
  Object.keys(S.skills).forEach(function(sk){
    var before=skillLvl(sk);
    if(S.pend[sk]>0)any=true;
    S.skills[sk]+=S.pend[sk];S.pend[sk]=0;
    var after=skillLvl(sk);if(after>before)out.push(SKN[sk]+' '+after);
  });
  if(out.length)say('Po spánku ti věci dávají větší smysl: '+out.join(', ')+'.','good');
  else if(any)say('Ve spánku se ti uložilo, co jsi dělal.','good');
}

/* ================= symptom log (for verified notes) ================= */
function logSym(tag){S.log.push({t:S.t,tag:tag});if(S.log.length>120)S.log.shift()}
function expTags(sp){
  var set={};
  S.eaten.forEach(function(e){
    if(e.it.sp!==sp)return;
    S.log.forEach(function(l){if(l.t>=e.t&&l.t<=e.t+720)set[l.tag]=1});
  });
  return set;
}

/* ================= items & composition ================= */
function pcOf(id){return S.pc[id]||(S.pc[id]={})}
function plantGone(pl){var c=S.pc[pl.id];return !!(c&&c.g&&c.g>S.t)}
function cropReady(pl){return !pl.crop||S.t>=pl.ready}
function fruitStage(pl){
  var sp=world.species[pl.sp];if(!ARCH[sp.arch].fruit)return -1;
  if(!cropReady(pl))return -1;
  var c=S.pc[pl.id];if(c&&c.f&&c.f>S.t)return -1;
  var d=(((dayF()+pl.off-sp.start)%YEAR_DAYS)+YEAR_DAYS)%YEAR_DAYS;
  if(d<2)return 0;if(d<4)return 1;if(d<6)return 2;return -1;
}
function itemKey(it){
  return [it.kind,it.sp,it.part,it.ripe,it.season,it.soil,(+it.cooked)||0,it.dl||0,it.water===undefined?'':it.water,it.fish?1:0,
    it.at===undefined?'':Math.floor(it.at/DAY_MIN),it.uid||''].join('|');
}
function count(kind){var n=0;S.inv.forEach(function(s){if(s.kind===kind)n+=s.qty});return n}
function takeKind(kind,q){
  for(var i=0;i<S.inv.length&&q>0;i++){
    var s=S.inv[i];if(s.kind!==kind)continue;
    var u=Math.min(q,s.qty);s.qty-=u;q-=u;
  }
  S.inv=S.inv.filter(function(s){return s.qty>0});
}
function addItem(it,q){
  q=q||1;
  if((it.kind==='meat'||(it.kind==='plant'&&it.part==='fruit'))&&it.at===undefined)it.at=S.t;
  if(it.kind==='pot'&&!it.uid)it.uid=Math.random().toString(36).slice(2,8);
  it.key=itemKey(it);
  for(var i=0;i<S.inv.length;i++){var s=S.inv[i];if(s.key===it.key&&s.qty+q<=9){s.qty+=q;return true}}
  if(S.inv.length>=G().slots){say('Nemáš kam to dát. Něco z batohu zahoď.','warn');return false}
  var c=Object.assign({},it);c.qty=q;S.inv.push(c);return true;
}
function ageDays(it){return it.at===undefined?0:(S.t-it.at)/DAY_MIN}
function spoiled(it){
  if(it.kind==='meat')return ageDays(it)>(it.cooked?4:1.5);
  if(it.kind==='plant'&&it.part==='fruit')return ageDays(it)>(it.cooked?5:3.5);
  return false;
}
function comp(it){
  if(it.kind==='meat')return it.cooked?[1,0,0,0,0,0]:[.85,.35,0,0,0,0];
  if(it.kind!=='plant')return [0,0,0,0,0,0];
  var sp=world.species[it.sp],A=ARCH[sp.arch];
  var v=(A.p[it.part]||[0,0,0,0,0,0]).slice();
  var sm=A.sm?A.sm(it.part,it.season):null;if(sm)v=v.map(function(x,k){return x*sm[k]});
  if(it.part==='fruit'){
    if(it.ripe===0){v[0]*=.45;v[1]+=(A.unripeI===undefined?.4:A.unripeI)}
    else if(it.ripe===2){v[0]*=.7;v[1]+=.2}
  }
  var soil=SOIL[it.soil];if(soil)v=v.map(function(x,k){return x*soil[k]});
  var j=sp.jit[it.part];v=v.map(function(x,k){return x*j[k]});
  if(it.cooked){
    v=v.map(function(x,k){return x*A.cook[k]});
    if(+it.cooked===2){v[1]*=.45;v[2]*=.75}
  }
  var dl=it.dl||0;if(dl){v[2]*=1-.12*dl;v[3]*=1-.1*dl;v[0]*=1+.06*dl}
  return v;
}
function describe(it){
  if(it.kind==='stick')return 'Klacek';if(it.kind==='stone')return 'Kámen';if(it.kind==='spear')return 'Kopí';
  if(it.kind==='clay')return 'Jíl';
  if(it.kind==='seed')return 'Semena'+(it.dl?' (vyšlechtěná '+it.dl+'×)':'');
  if(it.kind==='pot')return 'Nádoba'+(it.water===1?' · čistá voda':it.water===2?' · kalná voda':it.water===3?' · převařená voda':it.water===4?' · slaná voda':' · prázdná');
  if(it.kind==='meat'){
    var nm=it.fish?'Ryba':'Maso';
    var bad=spoiled(it)?' · zapáchá':'';
    return (it.cooked?nm+' upečená':nm+' syrová')+bad;
  }
  var base=it.part==='leaf'?'List':it.part==='root'?'Kořen':'Plod';
  var extra=[];
  if(it.part==='fruit')extra.push(it.ripe===0?'zelený':it.ripe===2?'scvrklý, tmavý':'zralý');
  if(+it.cooked===1)extra.push('upečený');
  if(+it.cooked===2)extra.push('uvařený');
  if(spoiled(it))extra.push('plesnivý');
  if(it.dl)extra.push('ze zahrady');
  return base+(extra.length?' · '+extra.join(', '):'');
}
function edible(it){return it.kind==='plant'||it.kind==='meat'}


/* ================= sheets (panels pause the game) ================= */
var dreamOn=false,dreamT0=0,away=false,gridDown=false,gridPaint=true;
function paused(){return sheet!==null||dreamOn||away}
function openSheet(name,data){
  sheet=name;sheetData=data;
  if(name==='note'){
    var lab=S.labels[data.sp]||{name:[],tags:[]},m={};
    (lab.tags||[]).forEach(function(t){m[t.t]=t.c});
    sheetData={sp:data.sp,name:(lab.name||[]).slice(),tags:m};
  }
  if(name==='sign')sheetData={bits:new Array(145).join('0'),rec:''};
  renderSheet();$('sheet').hidden=false;closeMenu();
  for(var k in keys)keys[k]=false;pointMode=false;
}
function closeSheet(){sheet=null;sheetData=null;giveMode=false;$('sheet').hidden=true}
var TRN=['Rudý kmen','Modrý kmen'];
var CTXMEAN={'u jídla':'jídlo','u vody':'voda','zvíře poblíž':'nebezpečí','u ohně':'oheň','kývá hlavou':'ano','vrtí hlavou':'ne','odhazuje věc':'jed'};
/* pomůcka: co jsi při slovu viděl a odhad významu (hráč ho musí potvrdit sám) */
function vocabInfo(e){
  var out=(e.old?'Starý hlas · ':'')+'slyšeno '+e.heard+'×',ct=e.ctx||{},tops=[];
  Object.keys(ct).forEach(function(t){
    var arr=Object.keys(ct[t]).sort(function(a,b){return ct[t][b]-ct[t][a]});
    out+='<br>'+esc(TRN[+t]||'Cizinci')+': '+arr.map(function(k){return esc(k)+' ×'+ct[t][k]}).join(', ');
    if(arr.length)tops.push(CTXMEAN[arr[0]]);
  });
  if(tops.length&&!e.meaning){
    var same=tops.every(function(x){return x===tops[0]});
    out+='<br><i>'+(same?'asi: '+esc(tops[0]):'kmeny ho používají jinak')+'</i>';
  }
  return out;
}
function lt(n){return n<1?'právě teď':(n<60?'před '+Math.round(n)+' min':(n<DAY_MIN?'před '+Math.round(n/60)+' h':'před '+(Math.round(n/DAY_MIN*10)/10)+' dny'))}
function tagsHtml(list){return list.map(function(t){var name=typeof t==='string'?t:t.t,read=typeof t!=='string'&&t.c==='read';return '<span class="tag'+(read?' read':'')+'">'+esc(name)+'</span>'}).join('')}
function renderSheet(){
  var c=$('card'),h='',i;
  c.innerHTML='';
  if(sheet==='intro'){
    h='<h2>Jsi první člověk na tomhle ostrově</h2>'
     +'<p>Nikdo ti neřekne, co je jedlé, co zabíjí a co léčí. Musíš si to zkusit, zapamatovat a ideálně někam zapsat.</p>'
     +'<ul><li><b>Pohyb:</b> <span class="kbd">WASD</span> nebo šipky. Na mobilu klepni na zem a drž prst.</li>'
     +'<li><b>Klepni na rostlinu, strom, vodu, kámen, zvíře nebo cizince</b> a vyber, co s tím.</li>'
     +'<li><b>Ochutnat</b> je malý kousek. Něco ti napoví, ale příznaky přijdou později a nikdo ti neřekne, co je způsobilo.</li>'
     +'<li><b>Řeč:</b> slabiky na <span class="kbd">Shift + 1 až 0</span> nebo tlačítko Mluvit. Slova nemají žádný význam, ten si domyslíš z toho, co lidé dělají.</li>'
     +'<li><b>Spánek</b> ti vrací síly a utvrzuje dovednosti. Čas při něm neběží rychleji.</li>'
     +'<li><b>Když zemřeš,</b> přijdeš o dovednosti, věci u sebe a zápisník. Zůstane osada a značky, které jsi nakreslil. Narodíš se jako další generace.</li></ul>'
     +'<p class="dim">Hra se při otevřeném panelu zastaví.</p>'
     +'<div class="acts"><button class="btn primary" data-act="start">Usnout a začít</button></div>';
  }else if(sheet==='pack'){
    h='<div class="head"><h2>Batoh</h2><button class="btn" data-act="close">Zavřít</button></div>'
     +'<p class="dim">'+S.inv.length+' z '+G().slots+' míst.'+(giveMode?' Vyber, co dát cizinci.':'')+'</p>';
    if(!S.inv.length)h+='<div class="empty">Batoh je prázdný. Sbírej klacky ze stromů, kameny ze skal, jíl z bahna a rostliny.</div>';
    var nf=!!nearFire(2.4),npcNear=!!nearestNPC(4),potW=S.inv.some(function(s){return s.kind==='pot'&&s.water&&s.water!==4});
    S.inv.forEach(function(s,idx){
      var lab=s.kind==='plant'&&S.labels[s.sp]&&S.labels[s.sp].name&&S.labels[s.sp].name.length?'<small>Tvoje jméno: '+esc(wordStr(S.labels[s.sp].name))+'</small>':'';
      var a='';
      if(s.kind==='plant'||s.kind==='meat'){
        a+='<button class="btn sm" data-act="taste" data-i="'+idx+'">Ochutnat</button><button class="btn sm" data-act="eat" data-i="'+idx+'">Sníst</button>';
        a+='<button class="btn sm" data-act="cook" data-i="'+idx+'"'+((!nf||s.cooked)?' disabled':'')+'>Upéct'+(nf?'':' <small>u ohně</small>')+'</button>';
        if(s.kind==='plant')a+='<button class="btn sm" data-act="boil" data-i="'+idx+'"'+((!nf||s.cooked||!potW)?' disabled':'')+'>Uvařit</button>';
        if(npcNear||giveMode)a+='<button class="btn sm'+(giveMode?' primary':'')+'" data-act="give" data-i="'+idx+'"'+(npcNear?'':' disabled')+'>Dát cizinci</button>';
      }else if(s.kind==='seed'){a+='<button class="btn sm" data-act="plantseed" data-i="'+idx+'">Zasadit</button>'}
      else if(s.kind==='pot'){
        if(s.water){a+='<button class="btn sm" data-act="potdrink" data-i="'+idx+'">Napít se</button>';
          a+='<button class="btn sm" data-act="potboil" data-i="'+idx+'"'+((!nf||s.water===3||s.water===4)?' disabled':'')+'>Převařit'+(nf?'':' <small>u ohně</small>')+'</button><button class="btn sm" data-act="potempty" data-i="'+idx+'">Vylít</button>'}
      }
      a+='<button class="btn sm" data-act="drop" data-i="'+idx+'">Zahodit</button>';
      h+='<div class="row"><span class="ic" data-ic="'+idx+'"></span><div class="desc"><b>'+esc(describe(s))+'</b> ×'+s.qty+lab+'</div><div class="acts">'+a+'</div></div>';
    });
    c.innerHTML=h;
    S.inv.forEach(function(s,idx){var el=c.querySelector('[data-ic="'+idx+'"]');if(el)el.appendChild(iconEl(s,38))});
    return;
  }else if(sheet==='notebook'){
    h='<div class="head"><h2>Zápisník</h2><button class="btn" data-act="close">Zavřít</button></div>'
     +'<div class="tabs">'+[['plants','Rostliny'],['vocab','Slovník'],['skills','Dovednosti'],['ms','Milníky']].map(function(t){return '<button class="btn sm'+(nbTab===t[0]?' on':'')+'" data-act="tab" data-t="'+t[0]+'">'+t[1]+'</button>'}).join('')+'</div>';
    if(nbTab==='plants'){
      h+='<p class="dim">Jména skládáš ze slabik. Příznaky můžeš zapsat jen ty, které jsi po jídle skutečně zažil, nebo které sis přečetl. Závěry si ale děláš sám a mohou být mylné. Po smrti zápisník mizí, značky ve světě zůstávají.</p>';
      var ids=Object.keys(S.labels).filter(function(k){var l=S.labels[k];return l&&((l.name&&l.name.length)||(l.tags&&l.tags.length))});
      if(!ids.length)h+='<div class="empty">Zatím nic. Klepni na rostlinu a vyber Poznámka.</div>';
      ids.forEach(function(k){var l=S.labels[k];h+='<div class="row"><span class="ic" data-nb="'+k+'"></span><div class="desc"><b>'+esc(l.name&&l.name.length?wordStr(l.name):'Bez jména')+'</b><small>'+tagsHtml(l.tags||[])+'</small></div><div class="acts"><button class="btn sm" data-act="editnote" data-i="'+k+'">Upravit</button></div></div>'});
      c.innerHTML=h;
      ids.forEach(function(k){var el=c.querySelector('[data-nb="'+k+'"]');if(el)el.appendChild(plantCanvas(+k,38))});
      return;
    }else if(nbTab==='vocab'){
      h+='<p class="dim">Slyšená slova. Hra ti nikdy neřekne, co znamenají. Přiřaď význam, jak ti dává smysl. Různé kmeny mohou používat stejné slovo pro něco jiného.</p>';
      var ks=Object.keys(S.vocab).sort(function(a,b){return S.vocab[b].heard-S.vocab[a].heard});
      if(!ks.length)h+='<div class="empty">Zatím jsi neslyšel žádné slovo. Najdi cizince.</div>';
      ks.forEach(function(k){
        var e=S.vocab[k];
        h+='<div class="row"><div class="desc"><b class="kbd">'+esc(wordStr(e.w))+'</b><small>'+vocabInfo(e)+'</small></div><div class="acts"><select data-vw="'+k+'" aria-label="Význam slova '+esc(wordStr(e.w))+'"><option value="">? neznámé</option>'
          +CONCEPTS.map(function(cc){return '<option value="'+cc[0]+'"'+(e.meaning===cc[0]?' selected':'')+'>'+cc[1]+'</option>'}).join('')+'<option value="other"'+(e.meaning==='other'?' selected':'')+'>jiné</option></select></div></div>';
      });
    }else if(nbTab==='skills'){
      h+='<p class="dim">Co procvičuješ, se utvrdí až ve spánku. Vedle zkušených lidí se učíš rychleji. Po smrti se dovednosti ztrácí.</p><div class="sk">';
      Object.keys(SKN).forEach(function(sk){
        var L=skillLvl(sk),pips='';for(i=0;i<5;i++)pips+='<i class="'+(i<L?'on':'')+'"></i>';
        h+='<div><b>'+SKN[sk]+'</b>'+(S.pend[sk]>.5?'<small class="dim"> · čeká na spánek</small>':'')+'</div><div class="pips">'+pips+'</div>';
      });
      h+='</div>';
    }else{
      var got=Object.keys(S.ms);
      if(!got.length)h+='<div class="empty">Zatím žádné.</div>';
      got.sort(function(a,b){return S.ms[a]-S.ms[b]}).forEach(function(k){h+='<div class="row"><div class="desc"><b>'+esc(MS[k][0])+'</b><small>'+esc(MS[k][1])+'</small></div></div>'});
    }
  }else if(sheet==='settings'){
    var dopts=[[180,'Den = 3 minuty (rychlý test)'],[900,'Den = 15 minut'],[3600,'Den = 1 hodina'],[5400,'Den = 90 minut (cíl hry)']];
    var copts=[[60,'Měsíc = 1 minuta (rok 12 minut)'],[180,'Měsíc = 3 minuty (rok 36 minut)'],[1800,'Měsíc = 30 minut (rok 6 hodin)'],[86400,'Měsíc = 1 den (rok 12 dní)'],[604800,'Měsíc = 1 týden (cíl hry, rok 12 týdnů)']];
    var mk=function(id,opts,cur){var has=opts.some(function(o){return o[0]===cur});return '<select id="'+id+'">'+opts.map(function(o){return '<option value="'+o[0]+'"'+(cur===o[0]?' selected':'')+'>'+o[1]+'</option>'}).join('')+(has?'':'<option value="'+cur+'" selected>vlastní ('+cur+' s)</option>')+'</select>'};
    h='<div class="head"><h2>Svět</h2><button class="btn" data-act="close">Zavřít</button></div>'
     +'<label class="f">Délka dne a noci<span class="dim"> řídí světlo, noc a tempo potřeb těla</span>'+mk('selDay',dopts,S.dayLen)+'</label>'
     +'<label class="f">Délka kalendáře<span class="dim"> řídí roční období a zrání plodů</span>'+mk('selCal',copts,S.calLen)+'</label>'
     +'<p class="dim">Rok má 12 měsíců, každé roční období 3. Cíl hry: den 60 až 90 minut, měsíc 1 týden. Pro zkoušení v prototypu zrychli oba.</p>'
     +'<label class="f">Generace postupuje<select id="selGen"><option value="death"'+(S.genMode==='death'?' selected':'')+'>po každé smrti (test)</option><option value="year"'+(S.genMode==='year'?' selected':'')+'>s každým rokem světa</option></select></label>'
     +'<label class="f"><span><input type="checkbox" id="chkSound"'+(S.sound?' checked':'')+'> Zvuk</span></label>'
     +'<label class="f"><span><input type="checkbox" id="chkSpr"'+(SPR.on?' checked':'')+'> Grafika z obrázků (pokus)</span><span class="dim"> stromy, divočák, oheň a hranice z klád. Rostliny zůstávají kreslené, aby šly rozlišit podle druhu.</span></label>'
     +'<p class="dim">Svět č. '+S.seed+'. Epocha '+S.epoch+'. Rok světa '+(worldYear()+1)+', měsíc '+(yearDay()+1)+'. Smrtí: '+S.deaths+'. Tvých značek ve světě: '+S.signs.length+'.</p>'
     +'<div class="acts"><button class="btn" data-act="epoch">Spustit novou epochu (test)</button><button class="btn" data-act="leave">Odejít ze hry (postava usne)</button><button class="btn danger" data-act="newworld">Nový svět</button></div>';
  }else if(sheet==='note'){
    var exp=expTags(sheetData.sp);
    h='<div class="head"><h2>Poznámka</h2></div><div id="notePrev"></div>'
     +'<label class="f">Jméno ze slabik<span class="kbd" id="noteName">'+esc(sheetData.name.length?wordStr(sheetData.name):'—')+'</span></label>'
     +'<div class="sylrow">'+SYL.map(function(s,k){return '<button class="btn" data-act="ns" data-i="'+k+'">'+s+'</button>'}).join('')+'</div>'
     +'<div class="acts"><button class="btn sm" data-act="nclear">Smazat jméno</button></div>'
     +'<label class="f">Co se stalo<span class="dim" style="text-transform:none;letter-spacing:0;font:500 14px var(--font-body)">Dostupné jsou jen příznaky, které jsi po jídle této rostliny zažil, nebo které sis přečetl.</span></label>'
     +'<div class="chips">'+TAGS.map(function(t){
        var ok=!!exp[t]||sheetData.tags[t]==='read',on=!!sheetData.tags[t];
        return '<button class="btn'+(on?' on':'')+'" data-act="nt" data-t="'+t+'"'+(ok?'':' disabled')+' aria-pressed="'+on+'">'+t+'</button>';
      }).join('')+'</div>'
     +'<div class="acts"><button class="btn primary" data-act="savenote">Uložit</button><button class="btn" data-act="close">Zrušit</button></div>';
    c.innerHTML=h;
    $('notePrev').appendChild(plantCanvas(sheetData.sp,52));
    return;
  }else if(sheet==='sign'){
    var rock=world.tiles[Math.floor(S.py)*W+Math.floor(S.px)]===T.ROCK;
    h='<div class="head"><h2>Značka</h2></div>'
     +'<p>'+(rock?'Stojíš na skále. Rytina vydrží.':'Kreslíš do hlíny. Za pár dní ji zvětrá.')+' Obrázek má jen 12 × 12 bodů a žádný text. Značku si může přečíst kdokoli, i ty po své smrti.</p>'
     +'<div class="grid12" id="grid">'+sheetData.bits.split('').map(function(b,k){return '<div data-g="'+k+'" class="'+(b==='1'?'on':'')+'"></div>'}).join('')+'</div>'
     +'<label class="f">Připojit záznam z jeho zápisníku<select id="selRec"><option value="">žádný</option>'
     +Object.keys(S.labels).filter(function(k){var l=S.labels[k];return l&&((l.name&&l.name.length)||(l.tags&&l.tags.length))}).map(function(k){var l=S.labels[k];return '<option value="'+k+'">'+esc((l.name&&l.name.length?wordStr(l.name):'rostlina bez jména')+' · '+(l.tags||[]).map(function(t){return t.t}).join(', '))+'</option>'}).join('')
     +'</select></label>'
     +'<div class="acts"><button class="btn primary" data-act="savesign">Nakreslit</button><button class="btn" data-act="clearsign">Vymazat</button><button class="btn" data-act="close">Zrušit</button></div>';
  }else if(sheet==='readsign'){
    var sg=sheetData;
    h='<div class="head"><h2>'+(sg.ancient?'Stará rytina':'Značka')+'</h2></div><div id="signPrev"></div>';
    if(sg.rec){
      h+='<div class="row" style="border-top:0"><span class="ic" id="recPlant"></span><div class="desc"><b>'+esc(sg.rec.name&&sg.rec.name.length?wordStr(sg.rec.name):'bez jména')+'</b><small>'+tagsHtml(sg.rec.tags||[])+'</small></div></div>'
        +'<p class="dim">'+(sg.rec.old?'Zvětralý nápis. Autor se mohl mýlit a svět se od té doby mohl změnit.':'Zapsal to autor značky. Jistotu nemáš.')+'</p>';
    }
    h+='<p class="dim">'+(sg.ancient?'Vyryto před dlouhou dobou.':'Zanecháno '+lt(S.t-sg.at)+'.')+'</p><div class="acts">'+(sg.rec?'<button class="btn primary" data-act="copyrec">Zapsat do zápisníku</button>':'')+'<button class="btn" data-act="close">Zavřít</button></div>';
    c.innerHTML=h;
    var cn=document.createElement('canvas');cn.width=192;cn.height=192;cn.style.width='96px';cn.style.height='96px';var g4=cn.getContext('2d');drawBits(g4,sg.bits,0,0,192,'#dba23f');$('signPrev').appendChild(cn);
    if(sg.rec&&$('recPlant'))$('recPlant').appendChild(plantCanvas(sg.rec.sp,48));
    if(sg.ancient)milestone('ruin');
    return;
  }else if(sheet==='death'){
    h='<h2>Zemřel jsi</h2><p>'+esc(S.deathCause)+'</p>';
    var list=S.eaten.slice(-4).reverse();
    if(list.length){h+='<p class="dim">Naposledy jsi jedl nebo ochutnal:</p>';list.forEach(function(e,idx){h+='<div class="row"><span class="ic" data-ed="'+idx+'"></span><div class="desc"><b>'+esc(describe(e.it))+'</b><small>'+(e.taste?'jen ochutnáno, ':'')+lt(S.t-e.t)+'</small></div></div>'})}
    var own=ownStructures().length,ng=nextGen();
    h+='<p>Přišel jsi o dovednosti, zápisník a věci u sebe. Věci zůstaly u těla na místě smrti několik dní. Ve světě po tobě zůstalo značek: <b>'+S.signs.length+'</b>.</p>'
      +'<p class="dim">Narodíš se jako Nový, generace '+(ng+1)+'. Status Pradávného je navždy pryč, svět si tě ale pamatuje.</p>'
      +'<div class="acts"><button class="btn primary" data-act="respawn" data-c="tribe"'+(own?'':' disabled')+'>Narodit se u svého kmene</button><button class="btn" data-act="respawn" data-c="wild">Narodit se v divočině</button></div>'
      +(own?'':'<p class="dim">Kmen nemáš. Postav oheň, zeď nebo nakresli značku, ať se máš kam vrátit.</p>');
    c.innerHTML=h;
    list.forEach(function(e,idx){var el=c.querySelector('[data-ed="'+idx+'"]');if(el)el.appendChild(iconEl(e.it,38))});
    return;
  }
  c.innerHTML=h;
}

function setGridCell(el,val){
  if(!el||!el.dataset||el.dataset.g===undefined)return;
  var k=+el.dataset.g,bits=sheetData.bits.split('');
  bits[k]=val?'1':'0';sheetData.bits=bits.join('');el.className=val?'on':'';
}
function onSheetClick(e){
  var b=e.target.closest('[data-act]');if(!b)return;
  var act=b.dataset.act,i=+b.dataset.i;
  wakeAudio();
  if(act==='close'){closeSheet()}
  else if(act==='start'){S.intro=true;closeSheet();if(!S.dreamDone)startDream();else hint('start','Nejdřív si najdi vodu. Světlejší modrá jsou jezírka, moře je slané.')}
  else if(act==='tab'){nbTab=b.dataset.t;renderSheet()}
  else if(act==='eat'){var s=S.inv[i];if(s){var it=Object.assign({},s);s.qty-=1;S.inv=S.inv.filter(function(x){return x.qty>0});consume(it,1,false);renderSheet()}}
  else if(act==='taste'){var s2=S.inv[i];if(s2){consume(s2,.1,true);renderSheet()}}
  else if(act==='cook'){cook(i,'bake');renderSheet()}
  else if(act==='boil'){cook(i,'boil');renderSheet()}
  else if(act==='give'){var n=nearestNPC(4);if(n){giveTo(n,i);closeSheet()}}
  else if(act==='plantseed'){closeSheet();plantSeed(i)}
  else if(act==='potdrink'){drinkPot(i);renderSheet()}
  else if(act==='potboil'){boilWater(i);renderSheet()}
  else if(act==='potempty'){var p=S.inv[i];if(p){p.water=0;p.key=itemKey(p);renderSheet()}}
  else if(act==='drop'){S.inv.splice(i,1);renderSheet()}
  else if(act==='editnote'){openSheet('note',{sp:i})}
  else if(act==='ns'){if(sheetData.name.length<3){sheetData.name.push(i);sfx.syl(i);$('noteName').textContent=wordStr(sheetData.name)}}
  else if(act==='nclear'){sheetData.name=[];$('noteName').textContent='—'}
  else if(act==='nt'){
    var t=b.dataset.t,exp=expTags(sheetData.sp);
    if(sheetData.tags[t])delete sheetData.tags[t];else sheetData.tags[t]=exp[t]?'own':'read';
    renderSheet();
  }
  else if(act==='savenote'){
    var tags=Object.keys(sheetData.tags).map(function(t){return {t:t,c:sheetData.tags[t]}});
    if(sheetData.name.length||tags.length)S.labels[sheetData.sp]={name:sheetData.name.slice(),tags:tags};else delete S.labels[sheetData.sp];
    closeSheet();
  }
  else if(act==='clearsign'){sheetData.bits=new Array(145).join('0');renderSheet()}
  else if(act==='savesign'){
    if(sheetData.bits.indexOf('1')<0){say('Nakresli aspoň pár bodů.');return}
    var rock=world.tiles[Math.floor(S.py)*W+Math.floor(S.px)]===T.ROCK,rec=null,sel=$('selRec');
    if(sel&&sel.value!==''){var l=S.labels[sel.value];if(l)rec={sp:+sel.value,name:(l.name||[]).slice(),tags:(l.tags||[]).map(function(t){return t.t}),old:false}}
    S.signs.push({x:S.px,y:S.py,bits:sheetData.bits,at:S.t,perm:rock,rec:rec});
    closeSheet();say('Nakreslil jsi značku.','good');milestone('mark');save();
  }
  else if(act==='copyrec'){
    var r=sheetData.rec,cur=S.labels[r.sp]||{name:[],tags:[]};
    if(!(cur.name&&cur.name.length))cur.name=(r.name||[]).slice();
    r.tags.forEach(function(t){if(!cur.tags.some(function(x){return x.t===t}))cur.tags.push({t:t,c:'read'})});
    S.labels[r.sp]=cur;say('Zapsal sis to do zápisníku jako přečtené.');closeSheet();
  }
  else if(act==='respawn'){respawn(b.dataset.c)}
  else if(act==='epoch'){runEpoch();renderSheet()}
  else if(act==='leave'){leaveGame()}
  else if(act==='newworld'){
    if(b.dataset.armed){
      try{localStorage.removeItem(SAVE_KEY)}catch(err){}
      S=newState(Math.floor(Math.random()*1e9));startWorld(false);closeSheet();openSheet('intro');
    }else{b.dataset.armed='1';b.textContent='Opravdu smazat? Klikni znovu'}
  }
}

/* ================= the dream (wordless tutorial) ================= */
function startDream(){dreamOn=true;dreamT0=performance.now();$('dream').hidden=false;wakeAudio();closeSpeech()}
function endDream(){
  dreamOn=false;$('dream').hidden=true;S.dreamDone=true;
  hint('start','Probudil ses. Nejdřív si najdi vodu. Světlejší modrá jsou jezírka, moře je slané.');
}
function drawDream(now){
  var cvd=$('dreamCv'),w=cvd.clientWidth,h=cvd.clientHeight;if(!w||!h)return;
  if(cvd.width!==w||cvd.height!==h){cvd.width=w;cvd.height=h}
  var c=cvd.getContext('2d'),t=(now-dreamT0)/1000,dur=6.5,sc=Math.floor(t/dur),lt2=t-sc*dur,u=Math.min(w,h)/10;
  if(sc>=4){endDream();return}
  var bg=['#10140f','#151311','#070913','#1f1812'][sc];
  c.setTransform(1,0,0,1,0,0);c.fillStyle=bg;c.fillRect(0,0,w,h);
  c.translate(w/2,h*.46);
  var P=function(x,y,face,tint,lying){c.save();drawHuman(c,x*u,y*u,u*.55,{gen:0,face:face,lying:lying,skin:tint||undefined});c.restore()};
  var k;
  if(sc===0){
    var bx=-2.6*u,by=0;
    ell(c,bx,by,u*1.1,u*.9,0,'#2f5a2a');
    for(k=0;k<7;k++){ell(c,bx+Math.cos(k*2.1)*u*.6,by+Math.sin(k*2.1)*u*.5,u*.16,u*.16,0,'#c43b3b')}
    var walk=clamp(lt2/1.8,0,1),fx=(2.2-(2.2+1.2)*walk)*u/u;
    var sick=clamp((lt2-3)/1.2,0,1);
    var fig=2.4-3.1*walk;
    P(fig,0,Math.PI,sick>0?'rgb('+Math.round(169-80*sick)+','+Math.round(140+20*sick)+','+Math.round(104-40*sick)+')':undefined);
    if(lt2>1.8&&lt2<3){ell(c,(fig-.4)*u,-.05*u+Math.sin(lt2*9)*u*.04,u*.14,u*.14,0,'#c43b3b')}
    if(sick>0){c.strokeStyle='#9bbf4a';c.lineWidth=u*.07;c.beginPath();for(k=0;k<24;k++){var aa=k*.5,rr=u*.05*k*.45;var sx=(fig)*u+Math.cos(aa+lt2*3)*rr,sy=-u*1.3+Math.sin(aa+lt2*3)*rr;if(k)c.lineTo(sx,sy);else c.moveTo(sx,sy)}c.stroke();
      for(k=0;k<3;k++)ell(c,(fig-.7)*u,u*(.2+((lt2*2+k*.4)%1)*.8),u*.07,u*.1,0,'#8aa04a')}
  }else if(sc===1){
    var bt=lt2;
    var boarX=-4*u+Math.min(bt/2.4,1)*8.5*u*(bt<2.4?1:1)+(bt>=2.4?(bt-2.4)*.6*u:0);
    var fy=(bt>1.6&&bt<2.9)?-1.6:0;
    var done=bt>4.2;
    P(2.4,fy,Math.PI);
    c.strokeStyle='#8a6338';c.lineWidth=u*.09;c.beginPath();c.moveTo(1.7*u,(fy-.2)*u);c.lineTo(.3*u,(fy-.2)*u);c.stroke();
    ell(c,boarX,0,u*.7,u*.45,0,done?'#5b4a3c':'#4a3326');ell(c,boarX+u*.55,0,u*.3,u*.28,0,done?'#5b4a3c':'#4a3326');
    if(!done&&bt>1.2){c.strokeStyle='#d65a31';c.lineWidth=u*.08;c.beginPath();c.moveTo(boarX-u*1.1,-u*.9);c.lineTo(boarX-u*.3,-u*.9);c.stroke()}
    if(bt>3.4){c.strokeStyle='#e9e1d0';c.lineWidth=u*.07;c.beginPath();c.moveTo(1.7*u,fy*u);c.lineTo(boarX+u*.8,0);c.stroke()}
  }else if(sc===2){
    var cold=lt2<3.2;
    var shake=cold?Math.sin(lt2*40)*u*.05:0;
    ell(c,u*3,-u*3,u*.55,u*.55,0,'#e8e4d0');ell(c,u*3.2,-u*3.1,u*.5,u*.5,0,'#070913');
    if(cold){for(k=0;k<14;k++){var fx2=((k*53)%100)/100*w-w/2,fy2=(((k*37)%100)/100*h*.9+lt2*u*1.2)%(h*.9)-h*.46;c.fillStyle='rgba(200,220,255,.5)';c.fillRect(fx2,fy2,u*.07,u*.07)}}
    P(-.4+shake/u,0,Math.PI/2,cold?'#9fb2c4':undefined);
    if(!cold){
      var gl=c.createRadialGradient(u*1.2,u*.2,0,u*1.2,u*.2,u*4);gl.addColorStop(0,'rgba(242,176,63,.55)');gl.addColorStop(1,'rgba(242,176,63,0)');c.fillStyle=gl;c.fillRect(-w,-h,2*w,2*h);
      var fl=1+Math.sin(lt2*14)*.12;ell(c,u*1.2,u*.2,u*.45*fl,u*.5*fl,0,'#d65a31');ell(c,u*1.2,u*.15,u*.26*fl,u*.32*fl,0,'#f2b03f');
      c.strokeStyle='#7c766a';c.lineWidth=u*.1;for(k=0;k<7;k++){var an=k/7*TAU;c.beginPath();c.arc(u*1.2+Math.cos(an)*u*.6,u*.35+Math.sin(an)*u*.4,u*.05,0,TAU);c.stroke()}
    }
  }else{
    var sun=clamp(lt2/3,0,1);
    ell(c,-u*3+sun*u*.8,u*(-2.8+(1-sun)*2.5),u*.7,u*.7,0,'#f2c35a');
    ell(c,u*2.4,u*1.2,u*2.1,u*1.1,0,'#3f7f90');
    var wk=clamp(lt2/3.2,0,1);
    P(-3+wk*3.6,.6,0);
    if(lt2>3.4){for(k=0;k<3;k++){var rp=((lt2-3.4)*.6+k*.33)%1;c.strokeStyle='rgba(232,244,248,'+(1-rp).toFixed(2)+')';c.lineWidth=u*.05;c.beginPath();c.ellipse(u*2.4,u*1.2,u*(.2+rp*1.4),u*(.1+rp*.7),0,0,TAU);c.stroke()}}
  }
  c.setTransform(1,0,0,1,0,0);
  for(k=0;k<4;k++){c.fillStyle=k===sc?'#dba23f':'#463829';c.beginPath();c.arc(w/2-27+k*18,h-72,4,0,TAU);c.fill()}
}

/* Obsah stránky Oohra: jediné místo, kde se mění texty, postup, zápisy a obrázky (návod v README.md).
   Logika stránky je v app.js, vzhled ve style.css. */
/* ===== NASTAVENÍ / SETTINGS ===== */
var CONFIG={name:'Oohra',demo:'',discord:''};

/* Postup: váha = plánovaný počet týdnů, pct = kolik je hotovo (0 až 100). Uprav pct, mapa i pruh se přepočítají. */
var PHASES=[
 {k:'0',w:7,pct:100,st:'done',n:{cs:'Základ',en:'Foundation'},d:{cs:'Návrh, prototyp v prohlížeči, nástroje',en:'Design, browser prototype, tools'}},
 {k:'A',w:5,pct:5,st:'now',n:{cs:'Jádro hry',en:'Game core'},d:{cs:'Svět, rostliny, potřeby a otravy v C#, zatím v konzoli',en:'World, plants, needs and poison in C#, console only for now'}},
 {k:'B',w:5,pct:0,st:'next',n:{cs:'Svět v Unity',en:'World in Unity'},d:{cs:'Postava, kamera shora, první obraz',en:'Character, top-down camera, first image'}},
 {k:'C',w:6,pct:0,st:'next',n:{cs:'Síť',en:'Network'},d:{cs:'Server a dva hráči, kteří se vidí',en:'A server and two players who can see each other'}},
 {k:'D',w:17,pct:0,st:'next',n:{cs:'Systémy',en:'Systems'},d:{cs:'Oheň, stavby, jazyk, zvířata, kmeny',en:'Fire, building, language, animals, tribes'}},
 {k:'E',w:5,pct:0,st:'next',n:{cs:'Provoz',en:'Operations'},d:{cs:'Ukládání světa, server, účty',en:'World saving, server, accounts'}},
 {k:'F',w:7,pct:0,st:'next',n:{cs:'Alfa',en:'Alpha'},d:{cs:'Prvních patnáct až dvacet testerů',en:'The first fifteen to twenty testers'}}
];

/* Zápisy: nový zápis přidej NA ZAČÁTEK seznamu. d je 'RRRR-MM-DD' nebo 'RRRR-MM'. img je volitelný obrázek (adresa souboru nebo data:). */
var POSTS=[
 {d:'2026-10-15',img:'',t:{cs:'Pradávní a Noví',en:'The Ancients and the Later-born'},b:{cs:'Do hry přibylo rozlišení dvou druhů lidí. První generace je robustní a odolná, ti, kdo se narodí později, jsou štíhlejší a učí se rychleji. Rozdíl se mění s každou generací.',en:'The game now tells two kinds of people apart. The first generation is sturdy and resilient; those born later are slimmer and learn faster. The gap shifts with each generation.'}},
 {d:'2026-10',img:'',t:{cs:'Hratelný prototyp',en:'A playable prototype'},b:{cs:'Ostrov se čtyřmi ročními obdobími, den a noc, oheň, zima, zvířata, jazyk ze slabik a značky na zemi. Běží v prohlížeči a slouží k ladění pravidel.',en:'An island with four seasons, day and night, fire, winter, animals, a language of syllables and marks on the ground. It runs in a browser and exists to tune the rules.'}},
 {d:'2026-10',img:'',t:{cs:'Nejdřív bez soubojů hráčů',en:'No player fights at first'},b:{cs:'V prvním testu pro patnáct až dvacet lidí se hráči proti sobě bít nebudou. Důležitější je zjistit, jestli si lidé předávají znalosti a vracejí se.',en:'In the first test for fifteen to twenty people, players will not fight each other. What matters more is whether people pass knowledge on and come back.'}}
];

/* Fragmenty: obrázky. Přidej {src:'img/nazev.jpg',cs:'popisek',en:'caption'}. Prázdná místa se doplní sama. */
var IMAGES=[
 {src:'img/night.jpg',cs:'Prototyp: noc u ohně',en:'Prototype: night by the fire'}
];
var SLOTS=6;

var SYL=['Gooh','Arh','Oo','Mah','Tek','Rin','Ush','Dak','Hee','Lom'];
var NOTES={cs:['Nalezeno na kameni u pobřeží. Pod značkou byl popel a tři rozbité skořápky. Někdo tu seděl dlouho.','Vyrytá do hlíny kousek od potoka. Kdo ji kreslil, spěchal: čáry jsou hluboké a na konci se zlomily.','Stála na zdi opuštěného úkrytu, zevnitř. Někdo ji tam nechal, aby ji viděl ten, kdo přijde po něm.','Čtvrtá značka je nejmenší. Byla u ohniště, které ještě hřálo, ale nikdo kolem nebyl.'],
 en:['Found on a rock by the shore. Ash lay beneath it, and three broken shells. Someone sat here a long time.','Cut into the clay near a stream. Whoever drew it was in a hurry: the lines are deep and snap off at the end.','It stood on the wall of an abandoned shelter, on the inside. Someone left it for whoever came after.','The fourth mark is the smallest. It was beside a hearth that was still warm, but nobody was around.']};

/* ===== TEXTY / COPY ===== */
var I18N={
cs:{nav_game:'Hra',nav_gen:'Generace',nav_mys:'Tajemství',nav_prog:'Postup',nav_log:'Deník',nav_frag:'Fragmenty',
 eyebrow:'Hra o přežití · jeden trvalý svět · ve vývoji',
 h1:'Nikdo neví, co je <em>jedovaté.</em>',
 lede:'Probudíš se na ostrově bez návodu. Co se naučíš, zemře s tebou, pokud to nepředáš dál.',
 cta1:'Sledovat postup',cta2:'Číst zápisy',demo:'Zahrát si ukázku',soon:'Hratelná ukázka a Discord přijdou brzy.',
 hint:'Pohni myší. Mlha se rozestoupí.',hint_touch:'Přejeď prstem po displeji. Mlha se rozestoupí.',
 game_h:'Co tu platí',game_p:'Žádný návod neřekne, kterou rostlinu sníst. Zbývá zkoušet, pozorovat ostatní a pamatovat si.',
 gen_h:'Pradávní a Noví',gen_p:'První lidé světa jsou jiní než ti, kdo se narodí později. Rozdíl se s každou generací mění.',
 gen_a_tag:'První generace',gen_a:'Pradávný',gen_b_tag:'Další generace',gen_b:'Nový',
 mys_h:'Značky ve starých ruinách',mys_p:'Hráči nechávají ve světě značky, malé kresby na mřížce dvanáct krát dvanáct. Tyto čtyři jsou z prvního prototypu. Co ti říkají?',
 where:'Kde se našla',sign:'Značka',syl_h:'Všech deset slabik',syl_p:'Z nich vznikají všechna slova. Které znamená vodu a které nebezpečí, ti svět neřekne.',
 prog_h:'Mapa vývoje',prog_p:'Čím víc je hotovo, tím víc ostrova mlha odkryje.',prog_rev:'odkryto',prog_leg:'Mlha = ještě nehotové. Plánovaná délka celé cesty: asi jeden rok.',
 log_h:'Deník vývoje',log_p:'Hru vyvíjí jediný člověk po večerech. Zápisy přibývají podle toho, jak věci postupují.',
 next_entry:'Další zápis se připravuje',
 frag_h:'Fragmenty',frag_p:'Obrazy z vývoje. Zatím jich je málo, mlha se zvedá pomalu.',empty:'Zatím nic',
 st_done:'Hotovo',st_now:'Teď',st_next:'Dál',
 foot1:'je ve vývoji. Pracovní název, vzhled a pravidla se mohou změnit.',foot2:'Čísla a obrázky na stránce pocházejí z prototypu.',
 rules:[['Skryté složení','Každá rostlina je směs látek, kterou hra nikdy neukáže. Složení se mění s epochami světa, takže zkušenost z jedné nemusí platit v další.'],
        ['Pozdní následky','Jed nezabíjí hned. Příznaky přijdou za chvíli a ty musíš odhadnout, co z toho, co jsi snědl, za to může. Poprvé tě hra ještě zachrání.'],
        ['Deset slabik','Všechna slova se skládají z deseti slabik. Slovo pro vodu nebo nebezpečí se v každém světě učíš znovu a cizí kmeny občas jmenují jídlo jinak, než čekáš.'],
        ['Paměť umírá','Co ses naučil, nepřejde na další postavu samo. Zbývají poznámky, značky na zemi, příběh u ohně a lidé, kteří si tě pamatují.']],
 A:['Širší a těžší, každá rána ho zraňuje méně','Železný žaludek: jed a zkažené jídlo snáší lépe','V zimě mrzne méně','Chodí pomaleji a učí se pomaleji než ti noví'],
 B:['Štíhlejší a vyšší, s každou generací víc','Učí se rychleji a rychleji běhá','Jed ho zasáhne tvrději a zima je pro něj krutější','Spoléhá na to, co mu předali ti před ním'],
 aria_prog:function(p){return 'Odkryto '+p+' % cesty k prvnímu testu'}},
en:{nav_game:'The game',nav_gen:'Generations',nav_mys:'Mysteries',nav_prog:'Progress',nav_log:'Journal',nav_frag:'Fragments',
 eyebrow:'Survival game · one persistent world · in development',
 h1:'Nobody knows what is <em>poisonous.</em>',
 lede:'You wake on an island with no instructions. What you learn dies with you, unless you pass it on.',
 cta1:'Follow progress',cta2:'Read the entries',demo:'Play the demo',soon:'A playable demo and Discord are coming.',
 hint:'Move the mouse. The fog parts.',hint_touch:'Drag a finger across the screen. The fog parts.',
 game_h:'The rules of the place',game_p:'No guide tells you which plant to eat. What is left is to try, to watch others, and to remember.',
 gen_h:'The Ancients and the Later-born',gen_p:'The first people of the world differ from those born later. The gap shifts with every generation.',
 gen_a_tag:'First generation',gen_a:'The Ancient',gen_b_tag:'Later generations',gen_b:'The Later-born',
 mys_h:'Marks in the old ruins',mys_p:'Players leave marks in the world: small drawings on a twelve-by-twelve grid. These four come from the first prototype. What do they say to you?',
 where:'Where it was found',sign:'Mark',syl_h:'All ten syllables',syl_p:'Every word is made of them. Which one means water and which means danger, the world will not tell you.',
 prog_h:'Map of progress',prog_p:'The more is finished, the more of the island the fog gives up.',prog_rev:'revealed',prog_leg:'Fog = not finished yet. The planned length of the whole road: about a year.',
 log_h:'Development journal',log_p:'The game is built by one person in the evenings. Entries appear as things move forward.',
 next_entry:'The next entry is being prepared',
 frag_h:'Fragments',frag_p:'Images from development. There are few so far; the fog lifts slowly.',empty:'Nothing yet',
 st_done:'Done',st_now:'Now',st_next:'Next',
 foot1:'is in development. The working title, look and rules may change.',foot2:'Numbers and images on this page come from the prototype.',
 rules:[['Hidden makeup','Every plant is a mix of substances the game never shows you. The mix shifts between epochs, so what you learned in one may not hold in the next.'],
        ['Late consequences','Poison does not kill at once. Symptoms arrive later and you must guess which thing you ate was to blame. The first time, the game spares you.'],
        ['Ten syllables','Every word is built from ten syllables. You relearn the word for water or danger in every world, and foreign tribes sometimes name food differently than you expect.'],
        ['Memory dies','What you learned does not pass to the next character on its own. What remains: notes, marks on the ground, a story by the fire, and people who remember you.']],
 A:['Broader and heavier; blows hurt him less','Iron stomach: handles poison and spoiled food better','Feels the cold less','Walks slower and learns slower than the later-born'],
 B:['Slimmer and taller, more so each generation','Learns faster and runs faster','Poison hits harder and the cold is crueler','Relies on what those before him passed on'],
 aria_prog:function(p){return p+' % of the road to the first test revealed'}}
};

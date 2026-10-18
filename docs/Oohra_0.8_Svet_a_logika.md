# Oohra 0.8: Svět a logika

> Navazuje na `Oohra_0.7.md`. Kde se obě verze liší, platí tato. Vychází z dotazníku 4 (18. 10. 2026) a z tvých poznámek. Grafika počká, modely řešíš ve VS Code. Čísla označená jako **návrh** jsem doplnil já a ladí se v JSON.

## 0. Souhrn rozhodnutí

- Ostrov bude **384 × 384 m** s pěti oblastmi: les, bažina, skalnatá vrchovina s jeskyněmi, louky, jezero nebo řeka. Oblasti vznikají ze seedu podle pravidel.
- **Generátor se přepíše** a golden testy z prototypu se nahradí novými. Starý uložený svět z robotů se smaže.
- **Stromy 3 až 8 m**, řidší, pět druhů. Strom se kácí jen nářadím, padá podle strany seknutí, zraní, co je pod ním, a rozdělí se na klady po 1 m.
- **Nováček nemá nic.** Nese dvě věci v rukou. Batoh a deník se vyrábí z lýka, kůže a kostí. Zápisník slov (J) zůstává od začátku.
- **Nebezpečné okolí:** komáři, pijavice, špatná voda, klíšťata, vosy a hadi. Nemoci jsou se zpožděním a léčí se rostlinami se skrytým složením.
- **Jeskyně** jsou samostatné malé mapy se vchodem ve skále. Přijdou po stromech, nářadí a nebezpečích, před patry.
- **Stavby do výšky:** přízemí s podlahou, nad ním druhé podlaží se zdí a podlahou a střecha. **Plná statika.**
- **Mapa** není od začátku. Hráč si ji kreslí, najde ji nebo dostane. Je to věc, která se dá ztratit, ukrást a zdědit.
- **Rozsah dalšího kola:** svět a biomy, stromy a kácení, nářadí, batoh a deník, nebezpečí. **Bez jeskyní a pater.** Začíná se světem a biomy.

---

## 1. Svět a oblasti

### Velikost
- 384 × 384 dlaždic, 1 dlaždice = 1 m. Velikost zůstává parametr v nastavení (ne konstanta v kódu).
- Ověřit v praxi: Unity Terrain má pevná rozlišení výškové mapy a velká plocha se může dělit na několik částí. Server a ukládání světa se musí otestovat s mnohem větším počtem stromů a rostlin.

### Oblasti (ze seedu pomocí pravidel)

| Oblast | Pravidlo (návrh) | Co v ní je |
|---|---|---|
| Les | střední výška terénu, pásma hustého a řídkého lesa se světlinami | dub, jehličnan, bříza nebo olše, rostliny |
| Bažina | nízké místo u vody | vrba, komáři, pijavice, špatná voda |
| Skalnatá vrchovina | vyšší místa | skály, jeskyně (později), pazourek |
| Louka | rovina bez lesa | rostliny, zvěř |
| Jezero nebo řeka | nejnižší místa | pitná voda, pobřeží |

Pobřeží a pláže už ve hře jsou a zůstávají.

### Generátor a testy
- Generátor se **přepíše**. Pravidlo z `CLAUDE.md` o přesném přepisu z prototypu a o golden testech (seedy 1, 12345, 987654321) **končí**. Prototyp zůstane zdrojem pravidel a čísel (rostliny, otravy, potřeby), ne mapy.
- Nové testy: stejný seed dá stejný svět, ostrov je celistvý, oblasti mají správný poměr plochy, na vodě nic nestojí, tábory a ruiny jsou na souši, stromy nestojí na vodě ani na skále.
- Starý `world.json` a `stats.json` z robotů se smažou. Server odmítá jiný seed, takže nový svět znamená nový soubor.

---

## 2. Stromy a kácení

### Druhy a výšky (návrh, ladí se v `trees.json`)

| Druh | Výška | Kde roste |
|---|---|---|
| Dub (listnatý) | 5 až 8 m | les |
| Jehličnan (borovice nebo smrk) | 6 až 8 m | les, vyšší místa |
| Bříza nebo olše | 4 až 7 m | les, okraj bažiny |
| Vrba | 3 až 5 m | bažina, břehy |
| Suchý mrtvý strom | 3 až 6 m | kdekoli, málo |

Výška každého stromu se počítá ze seedu (druh určuje rozsah), takže ji znají server i klient bez posílání.

### Hustota
Pásma hustého a řídkého lesa se světlinami. Je to **míň stromů než dnes**, ale vyšší. Světliny jsou dobré na tábory.

### Kácení krok za krokem
1. **Bez nářadí** strom nepokácíš. Rukama sbíráš jen klacky a větve.
2. Kamennou sekerou sekáš. Každá rána se počítá na serveru.
3. Strom **padá podle strany, ze které sekáš.** Před pádem zapraská (varování 1 až 2 s).
4. Co je v prostoru pádu (pruh podél směru pádu, délka podle výšky stromu), je **zraněno: hráči i zvířata.**
5. Strom ležící na zemi je **kmen**. Blokuje cestu, dá se obejít. Zůstane pařez.
6. Kmen se **sekerou dělí na klady po 1 m.** Příklad: 6 m = 6 kladů.
7. Každý klad dá **3 díly dřeva** (návrh, počet podle druhu v JSON, tvrdé dřevo víc a těžší práce).
8. Díl dřeva se dál štípe na **třísky.** Třísky jsou jako klacky, ale použijí se na víc věcí.

### K čemu třísky
Šití (jehla z třísky), podpal na oheň, pochodně.

### Přenášení
- **Jeden klad (1 m) unese jeden hráč**, pomalu.
- **Celý kmen (6 m) musí nést aspoň dva hráči.** Pravidlo pro mezilehlé délky (návrh): jeden nosič na každé 3 m délky.
- Kmeny a klady jsou předměty ve světě, ukládají se, lze je sdílet v kmeni.

### Obnova
- Mladé stromy dorůstají **pomalu (herní roky)**. Z pařezu nic neroste.
- **Kolem staveb hráčů strom nevyroste** (poloměr bezpečné zóny, návrh 5 m od stavby, v JSON).
- Ukládá se jen změna: pokácené stromy, pařezy, časovače mladých stromů. Zbytek se vždy vytvoří ze seedu.

---

## 3. Nářadí, batoh, deník a znalost výroby

### Nováček
- V rukou **dvě věci.** Bez batohu nic víc.
- Nářadí: **kamenná sekera, nůž z kamene nebo kosti, oštěp.** Opotřebuje se, po čase se rozbije, dá se opravit.
- **Pazourek** je ve vrstvené skále a dává nejlepší nářadí. Ostatní kameny jsou slabší.

### Batoh
- **Malý batoh:** z lýka a klacků (první, dostupný).
- **Velký batoh:** z kůže divočáka a kostí.
- Potřeba nového zdroje: **lýko** (z vnitřní kůry stromů nebo z rostlin). Otevřené rozhodnutí v bodě 10.

### Kůže
- Zdroje: **divočák (velká), jelen, králík (jen malé kousky).** Vlk kůži nedává.
- Jak ji nováček získá: **zabít zvíře oštěpem nebo kamenem, najít mršinu v lese, nebo past.**
- **Činění:** syrová kůže zahnije za dva dny. Vyčiní se popelem a vodou. Popel vzniká v ohništi. Protože nádoby ve hře ještě nejsou, činí se u zdroje vody (jezírko, potok).

### Deník
- **Zápisník slov (J) zůstává od začátku.** Jazyk se učí, hráči nic nechybí.
- **Deník je předmět** z kůže a kostí. Slouží na poznámky, kresby a mapy.
- **Po smrti zůstane u těla** (stejné pravidlo jako ostatní věci, 24 h jen pro mrtvého a jeho kmen, pak kdokoli). Deník je tak způsob, jak znalost přežije smrt.
- Server musí umět **předmět s obsahem** (poznámky, kresby). Ukládá se se světem.

### Kosti
Jehla na šití, šídlo, rybářský háček, hroty šípů, píšťalka, ozdoby a podpis.

### Znalost výroby jako dovednost
- Co hráč umí vyrobit, se **získá zkoušením nebo ukázáním od jiného hráče.** Smrtí se ztratí, stejně jako jazyk.
- Tohle je největší dopad na kód: pro každý recept bude znalost uložená u postavy. Návrh modelu: recept má stav „neznám / zkouším / umím“, ukázání jiným hráčem přeskočí fázi zkoušení.
- Protože to je nová vrstva, doporučuju první verzi udělat jako jednoduchý seznam odemčených receptů a ukázání přidat až po ní.

---

## 4. Skály a jeskyně (po kácení, nářadí a nebezpečích)

### Technika
- Vchod ve skále vede do **samostatné malé mapy (jeskyně).** Všichni, kdo do ní vstoupí, jsou ve stejné zóně a vidí se.
- Postava má `zone id`, server posílá snímek jen z její zóny. Uvnitř je jiný tick stav (teplota, světlo).

### Vlastnosti
| Vlastnost | Rozhodnutí |
|---|---|
| Velikosti | malá skrýš (1 až 3), střední (5 až 8), velká síť chodeb pro kmen |
| Vchody | náhodně ze seedu, některé nenápadné, najdou se až zblízka |
| Spojení | některé mají druhý východ jinde ve skále |
| Obyvatelé | **70 % prázdných, 30 % obsazených zvěří** |
| Vnitřek | tma (jen pochodeň), stálá teplota (v zimě tepleji), kouř z ohně dusí bez odvětrání, kapající voda a nerosty (pazourek) |
| Stavby | **ohniště, postel, zásoby a dekorace jako venku, ale bez stěn, oken a střech** |

### Návrh doplnění
- Obsazené jeskyně (30 %): podle velikosti vlčí doupě (malá), divočáci (střední), smečka (velká). Čísla v JSON.
- Rytiny Pradávných mohou ukazovat ke vchodům.

---

## 5. Nebezpečné okolí

### Zásada
Každé nebezpečí má **varování a obranu.** Hra nepíše jméno nemoci. Hráč pozná projev a hledá lék.

### Co bude (všech šest)

| Nebezpečí | Kde a kdy | Účinek | Obrana |
|---|---|---|---|
| Komáři | bažina, voda, v létě a za soumraku | zvuk, svědění, horší odpočinek, občas nemoc se zpožděním (horečka) | kouř z ohně, bahno nebo popel na kůži, oblečení z kůže |
| Pijavice | brodění bažinou | ubírají krev | sundají se ohněm nebo popelem |
| Špatná voda | bažina | bahenní voda způsobí zpožděnou nemoc | pít z jezírek a pramenů |
| Klíšťata | vysoká tráva, les | zpožděná nemoc | zkontrolovat a odstranit včas (návrh) |
| Vosí hnízda | stromy, zem | útok, když se přiblížíš nebo dýmáš | obejít, kouř (návrh) |
| Hadi | skály, louky | kousnutí s jedem se zpožděním | opatrnost, obuv z kůže (návrh) |

### Nemoci a léčba
- Zpožděné příznaky, stejný princip jako otravy. Léčí se **rostlinami se skrytým složením** a odpočinkem.
- Podle `CLAUDE.md` je složení rostlin ve skrytých datech (`Oohra.Core/Secret`). Bude potřeba přidat **léčivé složky**. Ověřit v kódu.
- **Přísnost:** oslabují, smrt hrozí jen v kombinaci a bez léčby.

### Sezónní nebezpečí (zařazeno)
Komáři v létě, hladoví vlci v zimě (dojdou blíž k táborům), rozvodněná bažina na jaře (zvětší se oblast).

### Další zařazené nápady
- **Stopy** zvířat a hráčů v bahně, déšť je smaže. Krátkodobé značky na zemi, posílají se jen blízkým klientům.
- **Sušení a uzení masa** nad ohněm. Maso se kazí, uzení prodlouží trvanlivost.

---

## 6. Stavění do výšky a střechy

### Podlaží
- **Přízemí** s podlahou, nad ním **druhé podlaží** s další zdí a podlahou, nahoře **střecha.** To je poslední část. Víc podlaží není.
- Nahoru: **žebřík, schody, rampa.**

### Statika (plná)
Zvolil jsi plnou statiku, tedy špatná stavba se zřítí. Je to nejdražší část. Doporučuju dvě etapy:
1. **Podpůrný graf:** každý díl stojí na něčem (zem, sloupek, zeď). Díl bez podpory spadne. Varování: zapraská (5 s).
2. **Nosnost:** díly mají nosnost a hmotnost, přetížení zřítí.
- Pád zraní to, co je pod nebo na dílu. Oheň, rozbití podpory a opotřebení dřeva snižují pevnost.
- Server počítá přepočet jen u změněné stavby, ne celého světa.

### Střechy
Větve a listí, kůra, kůže, došky z trávy, dřevěné šindele, hlína (později, potřebuje nový zdroj).
- Každý materiál chrání jinak dlouho, po čase protéká a opravuje se.

### Pád z výšky
Zraní, vyšší pád víc.

### Kamera
Když jsi pod střechou, střecha a vyšší podlaží se zprůhlední.

### Oheň ve stavbě
- Smí se. **Pochodeň dřevo nezapálí.**
- **Ohniště uvnitř dřevěné stavby ji zapálí** (hořlavá podlaha, stěny a střecha, deště pomáhají).
- Návrh: ohniště na kamenné nebo hliněné podlaze nebo s kamenným kruhem riziko výrazně sníží.

---

## 7. Mapa a orientace

- Hráč mapu **nemá od začátku.**
- Cesty k ní: **nakreslí si ji sám** (kreslení jako u značek), **najde ji ve zříceninách Pradávných** nebo **dostane od jiného hráče.**
- Mapa je **věc.** Dá se ztratit, ukrást a zdědit z těla.
- Řešit až po stromech, nářadí a nebezpečích. Konkrétní podoba se domýšlí časem.

---

## 8. Nápady navíc

| Nápad | Stav |
|---|---|
| Nářadí z pazourku | zařazeno (kap. 3) |
| Znalost výroby jako dovednost | zařazeno, nejdřív jednoduchá verze (kap. 3) |
| Sezónní nebezpečí | zařazeno (kap. 5) |
| Sušení a uzení masa | zařazeno (kap. 5) |
| Stopy v bahně | zařazeno (kap. 5) |
| Vor z kmenů | **až bude víc ostrovů.** Dnes je jen jeden ostrov |
| Lesní požár po bouřce | nezařazeno |

---

## 9. Pořadí prací

Hrubý odhad při 8 až 12 hodinách týdně, **neověřený v kódu**. Čísla se upřesní po první etapě.

| Etapa | Obsah | Odhad | Hotovo, když |
|---|---|---|---|
| 1 | Svět 384 × 384, oblasti, nový generátor, nové testy, nový svět | 2 až 3 týdny | server vytvoří nový svět, testy projdou, v Unity vidíš oblasti |
| 2 | Stromy: druhy, výšky, řidší les, kácení, pád, kmen, klady, dřevo, třísky, obnova | 2 až 3 týdny | pokácíš strom, spadne, rozděláš ho a dostaneš dřevo a třísky |
| 3 | Nářadí, ruce, batoh, kůže, činění, deník, kosti, jednoduché recepty | 2 až 3 týdny | vyrobíš sekeru, batoh a deník |
| 4 | Nebezpečí: komáři, pijavice, voda, nemoci a léčba, potom klíšťata, vosy, hadi | 2 až 3 týdny | v bažině tě pokouše komár, dostaneš horečku a vyléčíš se rostlinou |
| 5 | Sezónní nebezpečí, stopy, sušení a uzení | 1 až 2 týdny | |
| 6 | Jeskyně | později | |
| 7 | Patra, střechy, statika | později | |
| 8 | Mapa jako věc, vor | později | |

Do etapy 5 je to zhruba **9 až 14 týdnů.** Etapy 6 až 8 jsou mimo další kolo.

Při každé etapě platí pracovní smyčka z `CLAUDE.md`: větev `feature/...`, build, testy, slití do `main`, shrnutí „hotovo / příště“.

---

## 10. Otevřené otázky a rozpory

1. **Zdroj lýka** pro malý batoh. Návrh: vnitřní kůra živých stromů nebo vlákna z rostlin. Potřebuje nový předmět a pravidlo.
2. **Pravidlo nosnosti** mezilehlých délek kmene. Návrh: jeden nosič na každé 3 m.
3. **Statika:** jestli první verze stačí s podpůrným grafem, nebo hned i nosnost.
4. **Léčivé složky v rostlinách:** potřeba rozšířit skrytá data. Ověřit v `Oohra.Core/Secret`.
5. **Ohniště uvnitř stavby:** jestli jako ochrana bude kamenný kruh nebo jen kamenná podlaha.
6. **Zvěř v jeskyních:** jaké druhy v které velikosti (návrh v kap. 4).
7. **Vstup do jeskyně:** dveře nebo zátaras u vchodu (v dotazníku jsem se neptal).
8. **Vor:** řeší se, až bude víc ostrovů.
9. **Rozsah nebezpečí:** vybral jsi všech šest. Doporučuju dělat je ve dvou vlnách (komáři, pijavice, voda, potom klíšťata, vosy, hadi), aby se každé dalo ověřit zvlášť.

---

## 11. Co změnit v dokumentech

- `CLAUDE.md`: smazat pravidlo o přesném přepisu generátoru z prototypu a o golden testech, doplnit pravidla nového generátoru a oblastí, velikost 384, stromy a kácení, nářadí a batoh, nebezpečí, plánovaný postup etap.
- `docs/Oohra_Plan_Vyvoje.md`: přidat etapy 1 až 8 z kapitoly 9.
- `docs/Oohra_0.8_Svet_a_logika.md`: tento dokument.
- Golden testy v `server/Oohra.Core.Tests/golden/`: po přepsání generátoru odstranit, nahradit novými testy.

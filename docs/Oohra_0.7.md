# Oohra 0.7 – Herní koncept

> Pracovní název: **Oohra**
> Verze dokumentu: **0.7** (koncept)
> Žánr: multiplayerová survival hra a civilizační sandbox
> Engine: **Unity** (klient), vlastní autoritativní server
> Pohled: shora (izometrický, ve stylu Diabla), volné stavění na mřížce

### Změny oproti 0.6
Vychází z druhého dotazníku a z hraní prototypu 0.6. Rozhodnutí jsou v příloze B.
- **Změna postupu (sekce 17):** prohlížečový prototyp se dál nedolaďuje. Test se dělá rovnou v **C# a Unity** v podobě „šedé krabice“ (bez grafiky). Prototyp zůstává jako zdroj čísel a pravidel.
- **Sekce 4.1 a 12.2:** den 90 minut, rok v testu 4 až 5 dní, hráči se rodí rozptýleně a v teple.
- **Sekce 14.1:** kmen vzniká pozvánkou, sdílené zásoby, pravidla pro těla a chráněné místo narození.
- **Sekce 15:** kamera ze shora s otáčením po 90° a volným otáčením při stavění.
- **Sekce 17:** nový rozsah prvního testu a nová roadmapa.
- **Sekce 18:** pracovní tempo 2 až 3 hodiny denně od pondělí do čtvrtka.
- **Příloha B:** rozhodnutí z druhého dotazníku a návrhy názvu.

---

## 1. Vize

Oohra je online hra o přežití a vzestupu lidstva od nuly. Všichni hráči sdílejí **jeden velký svět**. Každý začíná jako první člověk bez jakéhokoliv vědění a vše se musí naučit: co je jedlé, jak lovit, jak rozdělat oheň, jak stavět, jak psát.

**Hlavní pilíř:** vědění je herní zdroj. Hra nenabízí menu s odemykáním technologií, vše se objevuje pokusem, pozorováním a předáváním mezi hráči.

**Druhý pilíř (nový):** je to **hra**, ne simulace. Hluboký systém je schovaný pod jednoduchým a zábavným povrchem. Realismus je prostředek k zajímavým rozhodnutím, ne cíl.

**Grafika:** záměrně jednoduchá. Důraz je na systémy, objevování a technologii.

### Co hru odlišuje
- Žádné jména, popisky ani ikony u rostlin a materiálů.
- Vědění se ztrácí, pokud se nepředá nebo nezapíše.
- Technologie jsou řetězce závislostí na materiálech, nástrojích a lidech, ne strom v menu.
- Hráči sami vytvářejí jazyk, písmo, mapy a knihy.
- Jeden persistentní svět pro tisíce hráčů, který se rozšiřuje po epochách.
- Opuštěné civilizace zanechávají ruiny a knihy, které mohou objevit další generace.
- Člověk se vyvíjí: první hráči jsou Pradávní (mohutní, odolní legendy světa), později narození Noví jsou rychlejší a mají skryté instinkty (sekce 8).

---

## 2. Designové zásady: hra, ne simulace

Rozdíl: **simulace chce být správná, hra chce být zajímavá.** Každá mechanika musí projít testem níže.

### 2.1 Test rozhodnutí
U každé mechaniky se ptáme: **vytváří zajímavé rozhodnutí, nebo jen práci?**
- Zajímavé rozhodnutí: „risknu neznámou rostlinu, nebo budu hladovět?“
- Jen práce: pravidelné doplňování pěti ukazatelů, čekání, mikromanagement.
- Pokud mechanika přináší jen práci, zjednodušíme ji, zpomalíme nebo odstraníme.

### 2.2 Činnosti musí být zábavné samy o sobě
- **Lov je pohybová hra:** čtení chování zvířete, uhýbání, načasování. Divočák se zaměří, rozběhne se a po výpadu je vyčerpaný.
- **Sbírání a výroba jsou rychlé:** žádné dlouhé čekání na dokončení akce.
- Pohled shora je vhodný pro akci a taktiku.

### 2.3 Potřeby jsou tlak, ne povinnost
- Hlad, žízeň, teplo a únava jsou **pomalé a odpouštějící**. Vytvářejí tlak na plánování, ne nutnost neustálého klikání.
- Hráč vidí **stav těla** (pruhy a popisné věty), ne tabulku čísel.
- Zranění: krvácení a kulhání ano. Podrobný model infekce jen v zjednodušené podobě (rána se může zanítit, léčivá rostlina pomůže).

### 2.4 Objevování je hádanka, ne loterie
Největší riziko hry: zpožděné účinky a více jídel najednou znamenají, že hráč nezjistí, co mu ublížilo. Pak je to jen náhoda a frustrace. Hráč musí mít **nástroje k detektivní práci**:

| Nástroj | Co dává hráči |
|---|---|
| **Ochutnat malý kousek** | Napoví (hořké, pálí, sladké, svíravé), ale neřekne vše |
| **Chování zvířat** | Zvířata jedí určité rostliny. Je to stopa, ne záruka pro člověka |
| **Zkoušet jednu věc najednou** | Čistý pokus, na jehož konci je jasná odpověď |
| **Malá dávka** | Menší riziko, slabší příznaky |
| **Zápisník a kresby** | Záznam pokusů pro vlastní paměť i pro ostatní |
| **Mentor** | Sdělení zkušeností od zkušenějšího hráče |

**Ochrana první otravy:** otrava z prvního pokusu zdravého hráče nezabije. Smrt přichází při opakovaném riskování, kombinaci s hladem, zimou nebo zraněním. Hráč tak chápe, co se stalo, a je to jeho chyba, ne smůla.

Výsledek: každé zjištění je malý příběh a „aha“ moment, ne výsledek hodu kostkou.

### 2.5 Krátké cíle dává svět, ne questy
Žádný seznam úkolů. Cíle vznikají z prostředí:
- soumrak: „najdi vodu a oheň do setmění“,
- zima: „nasbírej zásoby před zimou“,
- nemoc: „najdi něco, co pomůže“,
- divočák poblíž tábora: „zbav se ho, nebo se přesuň“.

Roční období jsou hodiny, které hráče vedou, aniž by mu cokoliv bylo vnuceno.

### 2.6 Časté malé výhry a odezva
Každý první úspěch má být zážitek: **první oheň, první bezpečné jídlo, první lov, první kresba, první naučený učedník.**
- Zvuk, animace, krátký záznam v zápisníku.
- Úspěchy hráči ukazují, že se učí, i když hra nic neříká přímo.

### 2.7 Spravedlivé selhání
- Smrt s vysvětlením, **proč** hráč zemřel.
- Nebezpečí s varováním (zvíře se zaměří, než zaútočí; počasí se mění pomalu).
- Smrt stojí **čas a pokrok**, ne hodiny frustrující práce (viz sekce 7).

### 2.8 Jednoduchý povrch, hloubka pod ním
- Hráč dělá několik základních sloves: **utrhnout, sníst, vyrobit, postavit, nakreslit, naučit.**
- Složení látek, kombinace a mutace hráč nevidí přímo, vidí jen důsledky.
- Hluboké systémy objeví ti, kdo je chtějí (léčitelé, kováři, písaři).

### 2.9 Co zjednodušit a co zachovat

| Zachovat | Zjednodušit nebo odstranit |
|---|---|
| Rostliny bez jmen, skryté složení | Příliš mnoho látek najednou (stačí 4 až 6 základních) |
| Zpožděné příznaky | Zpoždění delší než pár herních hodin |
| Realistická nebezpečnost zvířat | Složitý lékařský model |
| Vědění v zápisech a kresbách | Povinné neustálé doplňování ukazatelů |
| Roční období a epochy | Dlouhé čekání bez akce |
| Jazyk jako mechanika | Složitý slovník, který zdržuje komunikaci |

---

## 3. Základní herní smyčka

**Po minutách:** pohyb, sbírání, lov, uhýbání, výroba, malé rozhodnutí (sníst, nebo ne).
**Po hodině:** přežít noc, najít vodu, zajistit oheň a bezpečné jídlo, první nástroj.
**Po dnech:** přístřešek, zásoby, nové rostliny, zvířata, přežít zimu.
**Po týdnech:** osada, specializace, obchod, písmo, knihy.
**Dlouhodobě:** města, expedice, objevování ostrovů, předávání dědictví (viz sekce 13).

---

## 4. Start hráče a tutoriál

### 4.1 Spawn (návrh)
- **První život:** **rozptýleně po ostrově** (rozhodnuto v 0.7), s vodou na dosah, mimo území kmenů a ne přímo u nebezpečí.
- **Ochrana nováčka v zimě:** nový hráč se rodí v teple (první hodiny mu zima neubližuje) a má instinkt na zimu.
- **Místo narození je chráněné před cizí stavbou 24 hodin.** Pak se okolí uvolní (zabrání blokování cizího místa narození).
- **Další životy:** hráč si vybere mezi **narozením u svého kmene** (okraj jeho území, mimo domy) a **divočinou**. První volba dává kontinuitu rodu, druhá riziko a objevování.
- Pokud bude PvP, první hodiny nového života jsou chráněné před hráči (zvířata ne).

### 4.2 Tutoriál: sen bez slov (návrh)
- První život začíná krátkým **snem bez slov** (2 až 3 minuty): postava něco sní a je jí zle, vidí lov, oheň a noc. Žádná textová okna.
- Po probuzení vede hráče svět sám. Voda, bezpečné jídlo, kameny a klacky jsou nablízku. Soumrak a žízeň dávají první cíle.
- Další životy sen přeskočí.
- První hodina je navržená jako úspěšná.

### 4.3 Instinkty
- Postava začíná s **instinkty** podle své generace (první generace je nemá, viz sekce 8).

---

## 5. Přežití

### 5.1 Základní potřeby
- Sytost, voda, teplo, síla (únava), zdraví.
- Pomalé a odpouštějící (viz 2.3).

### 5.2 Zranění a nemoci
- **Zranění od zvířat** (kousnutí, nárazy, krvácení).
- Žádné číselné HP pro hráče, jen **příznaky** (bolest, horečka, slabost, kulhání).
- Hojení trvá dny, ne týdny (kvůli tempu hry).
- Čitelnost: hráč chápe, proč zemřel.

### 5.3 Lov
- Taktický, pohybový (viz 2.2).
- Zvířata mají chování (útěk, útok, stádo, teritorium). Nebezpečnost odpovídá realitě.
- Spolupráce více hráčů lov výrazně usnadňuje.

### 5.4 Spánek ve sdíleném světě (návrh)
- Čas světa se při spánku **nezrychluje** (ve sdíleném světě to nejde).
- Spánek rychle obnovuje sílu (řádově několik minut reálného času), ale postava je zranitelná. Spát se dá jen v teple, bezpečí a klidu.
- Dovednosti procvičené během dne se **utvrdí až ve spánku**. Spát v bezpečí se vyplatí a noc není ztracený čas.
- Noc je kratší než den, v zimě delší.
- Společné spaní ve skupině hřeje a hlídá.

### 5.5 Offline postava (návrh)
- Odchodem ze hry postava usne.
- Uvnitř uzavřeného přístřešku je v bezpečí. Venku zůstane pár minut zranitelná a pak ji svět skryje na nejbližším bezpečném místě.
- Potřeby offline klesají jen zlomkovým tempem a mají strop. Po návratu je postava hladová a slabá, ne mrtvá.
- Hráči ji nemohou napadnout (viz 14.2).
- Přístřešek tak má skutečnou hodnotu a nutí hráče stavět.

### 5.6 Nemoci (návrh)
- Nemoc vzniká z prostředí: špinavá voda, zkažené jídlo, nahuštěné osady bez hygieny. Infekce nezačínají od hráčů (ochrana proti zneužití).
- Hráč vidí jen příznaky. Šíří se vodou, jídlem a stykem.
- Lék hledá hráč mezi rostlinami (léčivá látka ve složení).
- Karanténa je sociální rozhodnutí.
- Nemoc mutuje mezi epochami. Velké osady jsou ohrožené a potřebují léčitele a studny.

---

## 6. Systém rostlin a otrav

### 6.1 Princip
Rostliny nemají „efekt“, ale **složení**. Žádná tabulka „rostlina X = jed“ ve hře neexistuje.

- Rostlina se dělí na části: **list, kořen, plod, semeno, kůra** a další.
- Každá část má skryté hodnoty látek (výživnost, dráždivost, toxin, nervový jed, stimulant, léčivo).
- Hodnoty se mění podle **stavu**: zralost, roční období, půda a klima, zpracování (sušení, vaření, kvašení).
- Příklad: toxin se vařením rozloží, výživnost zůstane. U jiné rostliny je to naopak.
- „Jedlá v zimě, jedovatá v létě“ vznikne přirozeně.

### 6.2 Otrava a účinky
- Okamžitá smrt se nevyskytuje (kromě opakovaného extrémního riskování).
- Příznaky: zvracení, průjem, třes, horečka, halucinace, slabost, dehydratace.
- **Zpožděné účinky:** hodiny, u kumulativních jedů dny. Při dobrých nástrojích (2.4) je hráč dokáže vystopovat.
- Kombinace látek vedou k bohatým možnostem objevování.

### 6.3 Žádná jména
- Rostliny, zvířata a materiály nemají popisky ani ikony s názvem.
- Jména si vymýšlejí hráči a kmeny sami, vzniká přirozená jazyková bariéra.

### 6.4 Ochrana proti wiki

| Mechanismus | Efekt |
|---|---|
| Kontextové vlastnosti (zralost, sezóna, půda, zpracování) | Wiki může popsat jen obecná pravidla |
| Zpožděné a kumulativní účinky | Nejistota, nutnost opakovaného testování |
| Pomalé mutace a změny prostředí | Znalosti zastarávají |
| Kombinace látek | Příliš mnoho možností pro úplnou dokumentaci |
| Epochy s novými ostrovy a druhy | Objevování začíná znovu |

**Technické pravidlo:** vlastnosti se generují ze **seedu světa** a drží se výhradně na serveru. Klient nesmí obsahovat data, ze kterých by šlo vlastnosti vyčíst.

> Poznámka k prototypu: prototyp je pouze v prohlížeči, takže skrytá data jsou v něm čitelná z kódu. Server-side generování přijde s multiplayerem.

---

## 7. Vědění, smrt a jeho předávání

### 7.1 Principy
- Vědění existuje v hlavách hráčů a v záznamech ve světě.
- Pokud se nepředá nebo nezapíše, **ztrácí se** (mezi hráči a po epochách).

### 7.2 Způsoby uchování
1. Paměť hráče.
2. Učení od mentora (rychlejší než samostatné objevování).
3. Kresby do hlíny, písku, skály, stěn, jeskyní.
4. Písmo a knihy (viz sekce 10).
5. Mapy kreslené ručně, mohou být chybné nebo zastaralé.

### 7.3 Smrt (rozhodnuto: ztráta všeho, co nezůstalo ve světě)
- Po smrti postava přijde o všechno, po čem nezůstala stopa ve světě (kniha, kresba, jeskyně).
- Nová postava začíná znovu na jiném místě ve světě a patří do **aktuální generace** (sekce 8.3).
- Stopy po sobě jsou motivace, ne bonus.

### 7.4 Cena smrti a dědictví (návrh)
Smrt stojí **čas a pokrok**, ne informace.

| Ztrácí se | Zůstává |
|---|---|
| Inventář u sebe (zůstane u těla několik dní, dá se pro něj dojít nebo ho vezme někdo jiný) | Osada, stavby, knihy a kresby |
| Dovednosti postavy | Vztahy s lidmi a pověst |
| Nezapsané osobní vědění postavy (zápisník v hlavě) | Učedníci a vědění, které předala |

Nová postava se narodí jako další generace (sekce 8) na místě podle 4.1.

### 7.5 Vědění v hlavě skutečného hráče
Smrt nesmaže to, co ví skutečný hráč. Hra může vzít jen to, co sama drží: dovednosti postavy, majetek, vztahy a pozici. Vědění se proto ztrácí **mezi hráči a v epochách** (mutace rostlin, nové ostrovy, nové druhy), takže zkušený hráč se musí učit pořád. Přeskládání světa po smrti navrhujeme nepoužít, byla by frustrující.

---

## 8. Evoluce člověka a generace (návrh)

### 8.1 Princip (přepsáno v 0.6)
Člověk ve hře se vyvíjí, ale **vývoj není žebříček, na kterém by novější byli lepší ve všem.** Jde o dva styly se stejným rozpočtem síly:

- **Pradávní** (první generace, generace 0) jsou **mohutní, odolní a drsní**. Vypadají jako starší válečníci v kožešinovém plášti, s ochrou, kostěnými ozdobami a jizvami. Nejsou „primitivní“, jsou **legendy světa**.
- **Noví** (generace 1 a dál) jsou **štíhlejší, rychlejší a učí se rychleji**, mají instinkty, ale jsou křehčí.

Evoluce je vidět na postavách, ale hra ji nikde nevysvětluje.

### 8.2 Čas evoluce: generace světa
- Evoluce **neběží podle kalendářního roku.** Při 1 herním roce za 1 reálný měsíc by „rok 1000“ nastal za 83 let.
- Běží po **generacích světa**. Generaci posouvá server (např. s každou epochou nebo po pevném reálném čase). Hodnoty se naladí v prototypu.
- Všichni, kdo se v dané generaci narodí, mají stejné vlastnosti.
- Počítadlo patří světu, ne hráči. Evoluci si nelze „nafarmit“ opakovanou smrtí.

### 8.3 Smrt jako další generace
- Po smrti se hráč narodí jako příslušník **aktuální generace** na novém místě (viz 7.3).
- Smrt tak není reset na „prvního člověka“, ale pokračování rodu.
- Stopy po předchozích životech (kresby, knihy, osada) může číst i potomek.

### 8.4 Co se mění

| Oblast | **Pradávní** | **Noví** (roste s generací, strop 6) |
|---|---|---|
| Vzhled | Mohutná postava, kožešinový plášť, ocher, kostěné korálky, jizvy | Vyšší, štíhlejší, prostší oděv, od 2. generace čelenka, později korálky a malba |
| Tělo | Víc odolnosti: zranění zasahují slaběji, zima škodí méně | Rychlejší chůze a vytrvalost, o něco víc míst v batohu |
| **Železný žaludek** | Snesou výrazně víc jedu a zkaženého jídla: otrava se u nich projeví slaběji a zkažené jídlo je skoro nezabíjí | Citlivější: jed působí o kousek silněji |
| Učení | Standardní tempo | Rychlejší nácvik dovedností |
| Instinkty | Nemají. Přišli do světa bez zkušeností a jejich odolnost je jejich pojistka | Mají (8.5) |
| Rychlost | Mírně pomalejší | Mírně rychlejší |

Vzhled je **parametrický** (několik hodnot, ne ručně kreslená postava pro každou generaci). V Unity je to levné a hodí se to pro jednoduchou grafiku.

**Výsledek ve hře:** Pradávný je v kmeni **ochutnávač a průzkumník**, který přežije, co by Nového zabilo. Noví jsou efektivní učenci a stavitelé. Žádný styl není vyšší úroveň druhého.

**Prototypové hodnoty (výchozí, ladí se hraním):**

| Vlastnost | Pradávný | Nový, generace g |
|---|---|---|
| Zranění od zvířat | ×0,78 | ×(1 + 0,03·g) |
| Jed a nervový jed | ×0,6 | ×(1 + 0,04·g) |
| Zkažené jídlo (nemoc) | ×0,45 | ×1 |
| Teplota těla | +7 | −1,3·g (do −5) |
| Rychlost | ×0,95 | ×(1 + 0,02·g), strop ×1,1 |
| Učení | ×1 | ×(1 + 0,08·g) |
| Hojení | ×1,05 | ×(1 + 0,04·g) |

### 8.5 Instinkty (skrytá genetická paměť)
- Instinkty jsou **reakce postavy, ne informace.** Hra nikdy neřekne „tato rostlina je jedlá“.
- Příklady: pozdější generace silněji cítí **hořkost a dráždivost**, zdráhá se spolknout věc s vysokým toxinem, kalná nebo zapáchající voda ji odpuzuje a dříve si všimne zvířete, které ji sleduje.
- Instinkty se vážou na **druhy látek a znaků** (např. hořké znamená toxin), ne na konkrétní rostliny. Vlastnosti rostlin se generují pro každý svět zvlášť, takže nic jiného nedává smysl.
- Instinkty jsou **pomoc, ne záruka.** Nové ostrovy a mutace v epochách je mohou částečně zmást.
- První generace instinkty nemá a objevuje vše sama.
- Hráči časem odhalí obecné pravidlo. To nevadí, nejde o seznam odpovědí.

### 8.6 Vyváženost a status Pradávného (rozhodnuto v 0.6)
- Bonusy jsou **malé a měkké.** Žádné zbraně ani hotové vědění. Mají pevné stropy.
- Odborné vědění, osady a knihy dávají větší výhodu než geny.
- **Kdo je Pradávný?** Všichni, kdo vstoupí do světa při jeho **startu** (a při startu každé nové epochy na novém ostrově, tam jsou zakladateli ostrova). **Počet Pradávných není omezený.**
- **Status se ztrácí navždy smrtí.** Po smrti se postava narodí jako Nový. Smrt se tak nevyplatí jako „vylepšení“, protože Noví nejsou silnější, jen jiní.
- **Pradávní mají jedinečné věci, které Noví nemají:**
  - **Starý hlas:** základní slova (9.6, společný základ) se odvozují z jejich jazyka. Pradávný je pochopen v kterémkoli kmeni a jeho hlas nese dál.
  - **Rytiny pro věky:** jejich značky na skalách nechátrají a ostatní je berou jako pradávné.
  - **Lepší mentoři:** učedník se u nich učí rychleji.
  - **Posvátná místa:** jen Pradávný může založit místo, které dává kmeni bonus (např. bezpečnější spánek).
- **Pamětníci:** hráči, kteří dlouho přežijí, získají viditelné znaky (tetování, jizvy). Znaky jsou čistě kosmetické a nedají se koupit.

### 8.6a Síň předků (nově v 0.6)
- Ve světě stojí **pomník s jmény ze slabik** (9.5), dobou života a tím, co daný Pradávný objevil. Zapíše se, když Pradávný zemře.
- Smrt Pradávného je tak **událost, ne ztráta bez stopy.** Svět si ho pamatuje.
- V pozdní hře se z Pradávných stávají legendy a pověsti, které čtou další generace.

### 8.7 Rizika

| Riziko | Řešení |
|---|---|
| Slábne fantazie „jsem první člověk“ | Noví se stále učí, instinkty jen zmírňují start |
| Pocit, že novější hráči jsou silnější (nebo že první jsou horší) | Dva styly se stejným rozpočtem, Pradávní mají jedinečné věci (8.6), vzhled starších válečníků |
| Hráči by umírali, aby byli „lepší“ | Noví nejsou silnější, smrt stojí vědění a status (8.6) |
| Železný žaludek je příliš silný | Jen zmírňuje jed, neruší ho. Ladit v prototypu (8.4) |
| Vizuální rozmanitost je drahá | Parametrický vzhled |
| Wiki odhalí pravidla instinktů | Jde o obecná pravidla, ne seznam rostlin |
| Hráči si nafarmí evoluci | Počítadlo patří světu |

### 8.8 Plán ověření v prototypu
Webový prototyp (verze po 0.5) už rozlišuje Pradávného a Nové: vzhled (kožešinový plášť a ochra proti štíhlé postavě), železný žaludek, odolnost proti zranění a zimě a rychlejší učení a pohyb Nových. Po smrti se z Pradávného stane Nový a status se nevrací. Cílem testu je zjistit, zda je rozdíl cítit a zda žádný styl nepůsobí jako ten slabší.

---

## 9. Jazyk a komunikace (rozhodnuto)

### 9.1 Princip
**Slova ve hře nemají žádný význam.** Hra pouze přenáší zvuky a slabiky. Význam existuje jen v hlavách hráčů. Nedorozumění je tak herní mechanika (stejné slovo může u jednoho kmene znamenat „jídlo“ a u jiného „jed“).

### 9.2 Ovládání
- Hráč má sadu slabik na klávesách (např. **SHIFT + 1 až 0**, 10 slabik).
- Slovo vznikne složením 1 až 3 slabik. Možných slov je přibližně 1000.
- Zvuk slabik je **stejný pro všechny**, takže se dají rozpoznat napříč kmeny, jen význam je jiný.

### 9.3 Jak se nováček učí
- **Univerzální gesta:** ukázat prstem, ukázat předmět, kývnutí, zakroutit hlavou.
- **Učení v kontextu:** zkušenější hráč řekne slovo a ukáže předmět nebo udělá akci.
- **Osobní zápisník:** hráč si u slyšeného slova může přiřadit kresbu nebo poznámku (hra ji nepotvrzuje, takže se může mýlit).

- Další pomůcky pro nováčky jsou v sekci 9.6.

### 9.4 Dosah komunikace
- Pouze **blízkostní komunikace**, žádný globální chat.

### 9.5 Jména z jazyka (návrh)
- Jména rostlin, míst a lidí si hráči skládají **ze slabik jazykového systému**, ne z volného textu.
- Ve hře tak není volný text, který by šel zneužít (vulgarity, reklama).

### 9.6 Pomůcky pro nové hráče (návrh, potvrdit hraním)
Autor se obává, že čistě slabiky a gesta budou pro nováčky příliš těžké. Zůstává to bez textu, jen se přidá pomoc:
1. **Tón hlasu.** Každé vyslovené slovo nese viditelnou náladu (klidné, poplach, radost), například barvu nebo tvar bubliny. Hráč podle toho pozná, jestli jde o varování, aniž zná slovo.
2. **Odhad významu v zápisníku.** Když slovo zaznělo vícekrát ve stejné situaci (při jídle, u vody, při útoku zvířete), zápisník ukáže „asi: jídlo (3×)“. Hra nic nepotvrzuje a odhad se může mýlit. Hráč si význam přiřazuje sám.
3. **Společný základ.** Slova pro 3 až 4 základní věci (voda, jídlo, nebezpečí a „pojď“) znají všichni hráči první generace, naučí se je ve snu (4.2). Další generace a epochy je nechají vyvíjet, takže se kmeny časem rozejdou do dialektů. Jazyk se tak vyvíjí stejně jako lidé.
4. **Kolo gest.** Pár univerzálních gest na jedno kliknutí (jíst, pít, ano, ne, pojď, pozor).
5. **Zopakovat, co zaznělo.** Tlačítko, které hráči znovu nabídne naposledy slyšené slovo, aby ho mohl hned napodobit.

Pořadí podle důležitosti pro test: 3, 2, 5, potom 1 a 4.

---

## 10. Technologie a pokrok

### 10.1 Princip
Technologie nejsou strom v menu. Jsou to **řetězce závislostí** na materiálech, nástrojích, vědění a spolupráci více lidí. Materiály mají stejné skryté vlastnosti jako rostliny (tvrdost, tavitelnost, hořlavost).

### 10.2 První čtyři úrovně (návrh)

| Úroveň | Co hráč zvládne | Na čem to závisí |
|---|---|---|
| 1. Dřevo a kámen | Úštěpy, kopí, kopací hůl, oheň, přístřešek | Tvrdost kamene, suché dřevo |
| 2. Oheň a hlína | Pečení, sušení, nádoby, cihly, pec | Hlína, žár. Nádoba umožní vaření (rozklad toxinů) a uchování vody |
| 3. Pevné stavby | Otesaný kámen, páka, vápno, studna | Tvrdší nástroj, spolupráce více lidí |
| 4. Kov | Měď, bronz, kovové nástroje | Ruda, dřevěné uhlí, pec, slitiny |

### 10.3 Specializace
- Hluboké dovednosti trvají dny až týdny (kovář, stavitel, léčitel, lovec, písař).
- Nikdo nezvládne vše, města vyžadují specializované lidi a obchod.
- Velké stavby, studny a lodě vyžadují **kritické množství lidí**.

### 10.4 Písmo a knihy
- Postup: kresba → piktogramy → písmo → knihy.
- Písmo si hráči vymýšlejí sami, vznikají různá písma.
- Knihy se dají kopírovat, ale také zničit (oheň, hniloba, ztráta).

### 10.5 Záznam v knize (datový model, návrh)
Záznam má strukturu:
- **kresba vzorku** (hra ji vygeneruje, bez jména),
- **podmínky** (část rostliny, zralost, sezóna, půda, zpracování),
- **výsledek** (příznaky a jejich zpoždění),
- **autor** a **jistota** („viděl jsem“ / „slyšel jsem“),
- **označení** z jazyka hráče (slabiky).

Hra ověří jen to, že autor to **skutečně zažil**. Závěry ale může mít špatné. Kniha tak obsahuje pravdivá pozorování, ale možná mylné výklady, a důvěra se opírá o pověst autora.

---

## 11. Stavění (rozhodnuto)

- Hráči **staví podle sebe**, žádné předdefinované budovy.
- Modulární systém na **mřížce**: zdi, podlahy, střechy, bloky, otvory.
- Materiál určuje vlastnosti stavby (dřevo hoří, kámen vydrží, hlína se rozpadá).
- Uzavřený prostor funguje jako přístřešek (teplo, ochrana) a umožňuje bezpečný odchod ze hry (5.5).
- Mřížková data jsou úsporná na ukládání a přenos po síti.
- Stavby chátrají, pokud je nikdo neudržuje.

---

## 12. Svět

### 12.1 Struktura: souostroví (návrh)
- Svět je **souostroví**. Jeden ostrov je jeden server pro zhruba 100 až 200 hráčů (orientačně, ladí se po prototypu).
- Mezi ostrovy se cestuje lodí a hráč se přenese na server cílového ostrova.
- Nový ostrov z epochy tedy znamená nový server, což sedí na plán rozšiřování.
- Procedurálně generovaný terén, klima, biomy, flóra a fauna.
- **Pro první test:** jeden ostrov velikosti dnešního prototypu (84×84 polí) a **15 až 20 hráčů**. Větší ostrov a stovky hráčů se řeší až po testu.

### 12.2 Čas a roční období (rozhodnuto, upraveno v 0.6)
Čas ve hře běží ve **dvou rytmech**, protože jeden dlouhý týden na měsíc by jinak dával herní den dlouhý přes pět hodin:

| Hodina | Co řídí | Tempo |
|---|---|---|
| **Kalendář světa** | roční období, zrání plodů, zima, nové epochy | **1 reálný týden = 1 herní měsíc.** Rok trvá 12 týdnů, jedno roční období 3 týdny. |
| **Den a noc** | světlo, noc, vlci, tempo potřeb těla (hlad, žízeň, únava), spánek | rychlé, výchozí cíl **1 herní den = 60 až 90 reálných minut** (ladí se hraním) |

- Důvod: hráč, který hraje dvě hodiny večer, musí zažít den, soumrak i noc. Kalendář naopak dává váhu přípravě na zimu a znalosti o rostlinách.
- Délka kalendáře i dne jsou **nastavení serveru**.
- **Pro první test s 15 až 20 lidmi (rozhodnuto v 0.7):** den a noc **90 reálných minut**, rok kalendáře **4 až 5 reálných dní**, takže tester zažije všechna roční období.
- Prototyp dnes používá jednu hodinu (rok 12 herních dní, den 3 minuty). Rozdělení na dvě hodiny je úkol Fáze 0 (sekce 17).
- **Generace.** Při roce dlouhém 12 týdnů je režim „generace podle roku“ (8.2) pro hru příliš pomalý. Generace se proto počítají **po smrti a narození** (8.3), rok je jen kalendář.
- Hlídat: nový hráč, který vstoupí v zimě, potřebuje ochranu (instinkty, zásoby v táboře, úkryt poblíž spawnu).

### 12.3 Epochy a expanze
- Každý update přidá nový ostrov nebo část mapy s **jinou sadou rostlin, zvířat a materiálů**.
- Nové oblasti jsou dostupné až po příslušné technologii (vor, loď).
- Mezi epochami probíhají mutace: stará rostlina se posune o pár hodnot, ne úplně přegeneruje.

### 12.4 Zdroje a populace
- Přelov a vyčerpání zdrojů, přirozený limit populace.
- Tlak na expanzi, obchod a technologie.

### 12.5 Zvířata a ekosystém (návrh)
Každé zvíře učí jinou dovednost a má čitelné chování:

| Typ | Chování | Co učí |
|---|---|---|
| Stádní býložravec | Stádo, útěk | Pasti, obklíčení, spolupráce |
| Teritoriální predátor | Hlídkuje, v noci loví | Ochranu tábora, noc |
| Osamělé nebezpečné zvíře (divočák) | Zaměří se, výpad, vyčerpání | Uhýbání, načasování |
| Ryby a ptáci | Jídlo u vody a ve vzduchu | Nástroje, trpělivost |

- Zvěř má kapacitu podle biomu. Přelov ji sníží a vrací se pomalu. Predátoři sledují kořist.
- **Migrace a vyvíjející se ekosystém** (nápad autora) se zařadí až po prvním testu, jako obsah další epochy.
- Zvířata jedí rostliny, což je stopa pro hráče (ne záruka, že je rostlina bezpečná i pro člověka).

### 12.6 Zemědělství a domestikace (návrh)
- Rostliny dávají **semena**, která se dají zasadit. Výsledek závisí na půdě a ročním období.
- Sázením semen z nejlepších rostlin se jejich vlastnosti po několika sklizních pomalu posouvají (např. nižší toxin). Je to „evoluce rostlin“ v rukou hráče.
- Zvířata se ochočují trpělivým krmením a mláďata dědí povahu. Autor chce ochočování a chov, ale **po prvním testu** (první test má jen dřevo, oheň a hlínu).
- Zemědělství vyžaduje plánování podle sezón a vede k usazení a k osadám.
- Mutace v epochách nutí znovu zkoumat, co platilo.

---

## 13. Dlouhodobá motivace (200 a více hodin)

Po stovkách hodin hráče neudrží další odemykání, ale **cíle, které si tvoří sám**, a svět, který se pořád mění.

### 13.1 Co hráče v jednotlivých fázích táhne

| Fáze | Co dělá hráč | Co ho motivuje |
|---|---|---|
| 0 až 1 h | Přežije první den | Voda, oheň, první bezpečné jídlo |
| 1 až 10 h | Objevuje jídlo, vyrábí nástroje, staví přístřešek | Hádanky s rostlinami, první lov, přežít zimu |
| 10 až 50 h | Zakládá osadu, specializuje se, obchoduje | Role v komunitě, učedníci, zásoby |
| 50 až 200 h | Město, písmo, knihy, technologie | Spolupráce, velké stavby, dědictví |
| 200 h a více | Expedice, archeologie, politika, údržba vědění | Svět se mění, příběhy vytvářejí hráči |

### 13.2 Pilíře pozdní hry
1. **Vědění je křehké a práce na něm nekončí.** Knihy hnijí a hoří, písmo se mění, mentor zemře. Z „objevit“ se stává „udržet a předat“. Knihovník, písař a učitel jsou plnohodnotná povolání.
2. **Epochy dělají z pozdní hry nový začátek.** Nový ostrov s jinými rostlinami znamená, že i veterán je zase zvědavý nováček. Velká výprava lodí s lidmi, zásobami a knihami je vrchol hry (může skončit i smrtí všech).
3. **Hrozby, které zvládne jen civilizace.** Epidemie, hladomor po špatné sklizni, sucho, sopka, přelovená zvěř. Jednotlivec je nevyřeší, ale kmen s léčitelem, zásobami a obchodem ano.
4. **Archeologie.** Opuštěná města chátrají, ale ruiny a staré knihy zůstanou. Nová generace je může objevit a luštit písmo, kterému nikdo nerozumí. To je unikátní hák hry.
5. **Identita a pověst.** Hráč není „level 80“, ale ten, kdo zná léčivé rostliny, nebo ten, koho se kmeny ptají na cestu. Mistrovství v řemesle dává smysl dál se rozvíjet.
6. **Příběhy, které vyprávějí hráči.** Rozcházející se jazyky, pověry, obchodní cesty, spory o území.

### 13.3 Rizika pozdní hry
- **Ztráta všeho po smrti při 200 hodinách** by hráče vyhnala. Řešení: smrt končí postavu, ne dědictví (7.4, 7.5).
- **Veteráni mohou ovládnout svět.** Řešení: nové ostrovy v epochách, limit území jednoho kmene.
- **Nedostatek hráčů** (pod desítky na ostrov) zabíjí archeologii i politiku.
- Pozdní hru nelze ověřit na papíře. Potřebuje test s reálnými hráči.

---

## 14. Sociální systémy

### 14.1 Kmeny a společnost (upraveno v 0.7)
- **Kmen vzniká pozvánkou:** člen kmene pozve jiného hráče gestem a slovem. Žádná správa, žádná listina.
- **Sdílené zásoby:** každý člen kmene může zásoby kmene brát i přidávat. Oheň kmene používají všichni členové.
- **Stavby:** cizí stavbu může rozebrat nebo zničit jen majitel a jeho kmen (14.2).
- **Věci z mrtvého hráče:** prvních 24 hodin je smí vzít jen jeho kmen, potom kdokoli. Tělo zůstává ve světě 4 dny.
- **Blokování:** okolí míst narození je 24 hodin chráněné před cizí stavbou (4.1).
- **Podpis hráče:** jméno ze slabik (1 až 3 slabiky) **a k tomu znak** (jednoduchý symbol), který se dá nakreslit na značku.
- **Učení mezi hráči:** zrychlení praxe funguje jen při **aktivním učení**: učitel udělá gesto a učedník je s ním několik minut u stejné činnosti.
- Kmeny a komunity vznikají spontánně. Tabu a pověry: komunity mohou věřit nepravdě a hráči ji mohou ověřit nebo vyvrátit.
- Obchod s předměty i s věděním. Spolupráce se vyplácí už od začátku (lov ve skupině, společné stavby).
- Konflikty: spory o zdroje, území, knihovny a města.

### 14.2 PvP (rozhodnuto v 0.6: do testu ne, ale server se připraví)
- **První test a Fáze 1 bez PvP.** Hráči nemohou zranit ani okrást jiné hráče.
- **Server se na PvP připraví hned:** zranění, útok a obrana jsou obecný systém platný pro zvířata. Pozdější zapnutí PvP je volba serveru, ne přepis.
- Po zapnutí musí mít **tření**: boj stojí zranění a infekci, spící hráč nejde zabít jedním útokem, offline postavu nejde napadnout.
- Vědění v hlavě ukrást nejde, jen knihy a věci. Knihovna hoří pomalu a knihy se dají zachránit.
- První hodiny nového života jsou chráněné před hráči.
- **Stavby:** cizí stavbu může rozebrat nebo zničit **jen majitel a jeho kmen** (rozhodnuto). Krádež a dobývání cizích staveb přijde s PvP.

### 14.3 Moderace a komunikace mimo hru (návrh)
- **Kresby** mají nízké rozlišení (např. mřížka 16×16) a sadu základních symbolů.
- **Jména** se skládají ze slabik jazyka, žádný volný text (9.5).
- **V testu:** tlačítko **Nahlásit** a správce rozhoduje (vyloučení řeší správce).
- Možnost kmene smazat značku na svém území.
- Komunikaci mimo hru (Discord) nebudeme aktivně omezovat.

---

## 15. Grafika a prezentace (upraveno v 0.6)

- **3D low-poly s kamerou shora** (styl Diablo), jednoduchý stylizovaný vzhled.
- **Kamera (rozhodnuto v 0.7):** pevný sklon shora, otáčení po krocích **90°** a při **stavění volné otáčení**. Hlídat, aby objekty (stromy, zdi) nezakrývaly hráče: průhlednost překážek mezi kamerou a postavou.
- **Ovládání:** klikání myší i klávesy WASD (jako v prototypu).
- Autor není grafik. Proto se **začne z hotových balíčků low-poly modelů** (stromy, terén, postavy, nástroje), upravených barvou. Zakázkový grafik přijde až po testu.
- Čitelnost je důležitější než detail.
- Rostliny a materiály rozlišitelné vzhledem, ne popisky. Rostliny v 3D vzniknou procedurálně z několika tvarů a barev, stejně jako v prototypu.
- Odezva na úspěchy a nebezpečí (zvuk, animace) je důležitá součást zábavnosti (2.6).
- Vzhled postavy je parametrický a mění se podle generace (sekce 8.4). V 3D to znamená několik tvarů, barev a doplňků na jedné základní postavě.
- **Zvuk:** efekty a ambientní hudba podle roční doby a denní doby. Zvuk slabik je stejný pro všechny (9.2).
- **Jen PC** (Windows, později Mac a Linux). Mobil se neřeší.

---

## 16. Technická část

### 16.1 Rozhodnutí
- **Klient: Unity.**
- **Server: dedikovaný, autoritativní, v C#**, sdílí datový model s klientem. Drží seed světa, skryté vlastnosti i veškerý stav.
- **Rozhodnuto v 0.6:** server v **C# (.NET)**, klient Unity na PC. Server počítá všechno včetně skrytých vlastností rostlin, klient vidí jen výsledek.
- První multiplayer (15 až 50 hráčů) využije hotovou síťovou knihovnu (konkrétní volba v sekci 16.4). Vlastní technologii pro stovky hráčů řešit až po ověření konceptu.

### 16.2 Klíčové výzvy
- **Škálování:** hráči na jednom ostrově (cíl stovky), víc ostrovů jako samostatné servery (12.1).
- **Trvalá data:** stavby, knihy, změny terénu.
- **Anti-cheat:** klient nesmí znát skrytá data. Prototyp v prohlížeči je má čitelná v kódu, to se opraví se serverem.
- **Opuštěná města:** postupné chátrání a vznik ruin (výkon i herní hodnota, viz archeologie).

### 16.3 Postup škálování
1. Prototyp: 1 hráč, malý ostrov.
2. Malý server: 20 až 50 hráčů.
3. Střední: 100 až 200 hráčů na ostrov.
4. Více ostrovů a přechody lodí.
5. Cílový stav: tisíce hráčů v souostroví.

Náklady na provoz závisí na počtu hráčů a odhadnou se po prototypu.

### 16.4 Architektura serveru pro první test (návrh, 0.6)

**Cíl:** jeden ostrov, 15 až 20 hráčů, vše počítá server, stabilní na jednom levném pronajatém serveru.

**Rozdělení do projektů (jedno řešení v C#):**
| Projekt | Obsah |
|---|---|
| **Oohra.Core** | Sdílená knihovna: datový model, generování světa ze seedu, pravidla (složení rostlin, účinky, potřeby, počasí). Běží na serveru. Klient dostane jen to, co smí vidět. |
| **Oohra.Server** | Hlavní smyčka, hráči, zvířata, NPC, ukládání, síť. |
| **Oohra.Protocol** | Zprávy mezi klientem a serverem (sdílí server i Unity). |
| **Oohra.TestClient** | Jednoduchý textový nebo bezgrafický klient, kterým se server zkouší bez Unity. |
| **Unity projekt** | Grafika, ovládání, zvuk, rozhraní. Žádná herní pravidla navíc. |

**Základní pravidla:**
- **Autoritativní server.** Klient posílá úmysly (jdi, vezmi, snězte, postav). Server rozhodne a pošle výsledek.
- **Tikání:** simulace 10 až 20× za sekundu, zprávy klientovi jen o věcech v dosahu (ostrov je malý, ale zásada platí od začátku).
- **Dva rytmy času** (12.2): kalendář a rychlý den a noc jsou nastavení serveru.
- **Ukládání:** stav světa a hráčů do databáze (začít jednoduše, například SQLite, později PostgreSQL). Pravidelné uložení a při vypnutí. Zálohy.
- **Skrytá data** (složení rostlin) existují jen na serveru. Klient vidí jen barvu, tvar, chuť a příznaky.
- **Offline postava** (5.5) a **spánek** (5.4) počítá server i po odpojení klienta.
- **Síť:** UDP knihovna pro .NET, kterou jde použít v Unity (kandidát **LiteNetLib**, k ověření spolu s alternativami). Serializace zpráv kompaktním formátem (kandidát MemoryPack nebo MessagePack, k ověření).
- **Účty (rozhodnuto v 0.7):** přihlášení **kódem pozvánky a přezdívkou**. Žádný e-mail, žádné platby.
- **Ukládaná data o testerech:** kód pozvánky, přezdívka a statistiky hraní pro ladění (testeři jsou informováni).
- **Hosting:** pronajatý virtuální server (VPS), orientačně stovky Kč měsíčně. Přesné ceny ověřit před nasazením.
- **Testy:** automatické testy pravidel (Oohra.Core) a testovací klient, který simuluje desítky hráčů.

**Co z webového prototypu přejde:** pravidla a čísla (composition, příznaky, potřeby, zvířata, jazyk) se přepíšou do Oohra.Core. Webový prototyp zůstává pro rychlé zkoušení změn dřív, než se přepíšou do C#.

---

## 17. Roadmapa a prototyp (přepsáno v 0.7)

**Změna postupu:** prohlížečový prototyp neumí otestovat server, síť, 3D kameru ani výkon, takže se dál nedolaďuje. Zůstává jako **zdroj čísel a pravidel**. Test se dělá rovnou v C# a Unity jako **„šedá krabice“** (kostky a kapsle místo modelů, žádná grafika).

**Tempo:** 2 až 3 hodiny denně od pondělí do čtvrtka, zhruba **8 až 12 hodin týdně**. Odhady jsou hrubé. Každá fáze končí **bránou**.

### Rozsah prvního testu (rozhodnuto v 0.7)
**Je v testu:** rostliny, jídlo a otravy, oheň a noc, zdi a úkryt, jazyk ze slabik (včetně pomůcek z 9.6), značky a nákresy, zvířata (divočák, vlk, králík, jelen), spánek a dovednosti, smrt a generace, Pradávní a Noví, NPC cizí kmeny (oba), kmeny pozvánkou a sdílené zásoby, tlačítko Nahlásit, přihlášení kódem pozvánky.

**Není v testu (odloženo):** zemědělství a setí, pasti a rybaření, hrnčířství a nádoby, posvátná místa, Síň předků, Pamětníci (tetování) a plná offline postava. Odhlášení je v testu jednoduché: postava se bezpečně uloží na místě.

Důsledek vynechání nádob: voda se pije přímo u zdroje a nedá se převařit. Výsledky z prototypu pro tyto systémy se zapíšou do pravidel, ale nepřepisují se.

### Fáze A – Pravidla v C# (týdny 1–6)
- Založit řešení (Core, Server, Protocol, TestClient, testy) a repozitář.
- Přepsat z prototypu do `Oohra.Core`: generování světa ze seedu, rostliny a jejich složení, dva rytmy času, potřeby, otravy, Pradávní a Noví, zvířata, jazyk a slovník.
- Textový testovací klient a automatické testy.
- **Brána:** `dotnet test` projde, v textovém klientu lze přežít den.

### Fáze B – Unity šedá krabice, jeden hráč (týdny 5–12)
- Unity projekt, ostrov z dat Core, kostka jako postava, kamera s otáčením po 90°, pohyb myší a WASD.
- Základní rozhraní: potřeby, batoh, zápisník, mluvení (slabiky).
- Učení Unity probíhá souběžně (Unity Essentials).
- **Brána:** v Unity se dá projít ostrov, sníst rostlinu a přežít noc.

### Fáze C – Síť (týdny 12–18)
- Výběr síťové knihovny malým pokusem, přihlášení kódem pozvánky.
- Server s hlavní smyčkou, pohyb dvou hráčů, jídlo a otravy počítá server.
- **Brána:** dva klienty se vidí a hra drží.

### Fáze D – Herní systémy (týdny 18–32)
Po jednom, vždy s krátkým testem: oheň a noc a zima, zdi a úkryt, spánek a dovednosti, jazyk a pomůcky, značky a znak, zvířata, smrt a generace, kmeny a zásoby, NPC cizinci, nahlášení.

### Fáze E – Ukládání a provoz (týdny 32–38)
- Ukládání světa a hráčů, přihlášení kódem, pronajatý server, zálohy.

### Fáze F – Alfa (týdny 38–44) a test s 15 až 20 lidmi (týdny 44 a dál)
- Několik známých hraje pár dní, ladění, potom širší uzavřený test.
- Hledání testerů se řeší až po alfě (zatím neřešit).

### Po testu
- Grafik a animátor, zemědělství, nádoby, pasti a rybaření, posvátná místa a Síň předků, Pamětníci, plná offline postava, ochočování, nemoci mezi lidmi, knihy, další epochy.

**Celkem do testu:** zhruba 10 až 11 měsíců při 8 až 12 hodinách týdně.

---

## 18. Rozsah, tým a monetizace

### 18.1 Současný stav
- Hru vyvíjí **jeden člověk jako koníček**: 2 až 3 hodiny denně od pondělí do čtvrtka, o víkendu nic (zhruba 8 až 12 hodin týdně). Pomoc při psaní serveru a prototypů poskytuje Claude.
- Rozpočet do prvního veřejného testu: **do 30 tisíc Kč**. To stačí na pronájem serveru a hotové balíčky modelů, ne na zakázkovou grafiku.

### 18.2 Zásady pro sólo vývoj
- **Vše procedurálně:** svět, rostliny, vzhled postavy. Ručně vyráběného obsahu co nejméně.
- **Primitivní grafika** (už teď záměrná), bez drahých modelů.
- **Hotové knihovny** místo vlastní technologie, zejména pro síť.
- **Vlastní serverovou technologii pro stovky hráčů nestavět**, dokud se neověří koncept.
- Před každou fází rozhodnout, co se **neudělá**.

### 18.3 Co odložit

| Odložit | Do fáze |
|---|---|
| PvP, krádeže, dobývání staveb | 4 nebo později (server na ně připravený od začátku) |
| Mobilní verze | neřeší se |
| Zemědělství, pasti, rybaření, hrnčířství | po testu |
| Posvátná místa, Síň předků, Pamětníci | po testu |
| Plná offline postava | po testu |
| Knihy se strukturovaným záznamem | 2 |
| Ochočování a chov zvířat | 2 |
| Nemoci mezi lidmi (nákazy) | 2 |
| Úroveň technologií nad dřevo, oheň, hlínu | 2 až 3 |
| Souostroví, lodě, migrace zvěře | 3 |
| Úroveň kovů | 3 |
| Větší ostrov (stovky hráčů) | po testu |
| Evoluce nad rámec velikosti postavy a malého bonusu | po testu |

### 18.4 Kdy a koho nabírat (rozhodnuto v 0.6)
- Autor chce **nejdřív zkusit všechno sám**. Nikdo se nenabírá před funkčním testem pro 15 až 20 lidí.
- Jako první přijde **grafik a animátor** (ne serverový programátor), protože server píše autor s pomocí Claudea.
- Další role až podle potřeby: designér a komunitní správce, později serverový specialista pro škálování.

### 18.5 Monetizace (návrh)
- **Jednorázový nákup za nízkou cenu.** Filtruje problémové hráče a nepotřebuje slibovat obsah.
- Kosmetické doplňky jsou v pořádku.
- **Nikdy neprodávat** výhody v evoluci, instinktech ani v informacích.
- **Rozhodnuto v 0.6:** jednorázová cena. Platforma jen PC, mobil se neřeší.

---

## 19. Rizika

| Riziko | Možné řešení |
|---|---|
| Hra působí jako simulace, ne jako zábava | Zásady v sekci 2, test rozhodnutí, hratelnost před realismem |
| Objevování je loterie a frustruje | Nástroje pro detektivní práci, ochrana první otravy |
| Frustrující první hodiny | Instinkty, dostupná voda a jídlo, prostředí vede |
| Wiki prozradí vše | Kontextové vlastnosti, mutace, epochy, server-side data |
| Technické škálování | Postupný růst, regiony, chátrání opuštěných oblastí |
| Moderace volného textu | Strukturované záznamy, omezené popisky |
| Smrt po stovkách hodin demotivuje | Dědictví přežije (osada, učedníci, knihy, pověst) |
| Veteráni ovládnou svět | Nové ostrovy v epochách, limit území |
| Evoluce rozdělí hráče na silné a slabé | Malé bonusy, jedinečné výhody Pradávných (sekce 8.6) |
| Sólo vývoj: příliš velký rozsah | MVP, odkládání funkcí, brány po každé fázi (sekce 17 a 18) |
| Zneužití offline ochrany | Ochrana jen v přístřešku, zlomkové tempo potřeb se stropem (5.5) |
| Urážlivé kresby | Nízké rozlišení, sada symbolů, jména ze slabik (14.3) |
| Jazyková bariéra odradí nováčky | Univerzální gesta, učení v kontextu |
| Prázdný nebo přeplněný svět | Dynamická velikost mapy, limity zdrojů |
| Hobby tempo: projekt se protáhne | Brány po fázích, malý první test, odkládání (17, 18.3) |
| 3D grafika bez grafika | Hotové low-poly balíčky, procedurální rostliny a postava |
| Pomalý kalendář: nováček v zimě | Společné zásoby a úkryt poblíž spawnu, instinkty (12.2) |
| Slabiky odradí nováčky | Pomůcky v 9.6 |
| Pradávní se cítí slabší než Noví | Dva styly se stejným rozpočtem, jedinečné výhody Pradávných (8.6) |

---

## 20. Otevřené otázky (po druhém dotazníku)

1. Výběr síťové knihovny (malý pokus ve Fázi C).
2. Ladění tempa potřeb, nemocí a první otravy v C# (nastavení Méně ochrany než v prototypu).
3. Jak se projeví „příliš mnoho tlačítek“ v rozhraní Unity (zjednodušit, kontextová nabídka).
4. Přesný vzhled znaku k podpisu hráče (sada symbolů).
5. Zda je rok 4 až 5 dní v testu pro 15 až 20 lidí vhodný.
6. Název hry (Příloha B).
7. Kdy a jak hledat testery (po alfě).
8. Jak vyvážit Pradávné a Nové v C# podle testu.

---

## 21. Další kroky

- [ ] Večer po koupi notebooku: instalace podle návodu, ověření `dotnet --version`, zkušební projekt v Unity.
- [ ] Založit repozitář `oohra` a napsat Claudeovi jeho jméno.
- [ ] Fáze A: Claude založí řešení v C# a přepíše pravidla.
- [ ] Souběžně: Unity Essentials.

---

## Příloha A: Rozhodnutí z dotazníku

| Téma | Rozhodnutí |
|---|---|
| Cíl | Dolaďování logiky v HTML, potom mini multiplayer. Test 15 až 20 lidí na jednom ostrově, hobby tempo |
| Čas autora | Do 10 hodin týdně |
| Evoluce | Dva styly: Pradávní (mohutní, železný žaludek, odolní) a Noví (rychlejší, učí se rychleji, křehčí). Počet Pradávných neomezený, status se ztrácí smrtí, Síň předků (8) |
| Smrt | Beze změny: ztráta dovedností, zápisníku, slov a batohu. Zůstanou stavby, značky a svět |
| Rok | 1 reálný týden = 1 herní měsíc. Kalendář oddělený od rychlého dne a noci (12.2) |
| PvP | Do testu ne. Server na něj připravený (14.2) |
| Cizí stavby | Rozebrat nebo zničit smí jen majitel a jeho kmen |
| Komunikace | Slabiky a gesta, nikdy text. Navíc pomůcky pro nováčky (9.6) |
| Knihy | Až později, nejdřív značky a nápisy |
| Technologie | Jen dřevo, oheň, hlína |
| Ostrov | 84×84 polí, do 20 hráčů v testu |
| Epochy | Jednou za 1 až 2 měsíce, důraz na nové ostrovy a objevování |
| Nemoci a zranění | Jako dnes, lépe vyvážit |
| Zvířata | Ochočování a chov po testu, migrace a ekosystém později |
| Klient | Unity na PC. Mobil ne |
| Server | C# (.NET), vše počítá server |
| Grafika | 3D low-poly, kamera shora, hotové balíčky |
| Zvuk | Efekty a ambientní hudba podle sezóny a denní doby |
| Financování | Jednorázová cena |
| Tým | Zatím sám. První přijde grafik a animátor po funkčním testu |
| Rozpočet do testu | Do 30 tisíc Kč |
| Co v prototypu bavilo | Zkoušení rostlin, přežití noci a zimy, úkryt a oheň, cizí kmeny, kresby |
| Co vadilo | Jen nevhodnost pro telefon (neřeší se) |

---

## Příloha B: Rozhodnutí z druhého dotazníku

| Téma | Rozhodnutí |
|---|---|
| Tempo hladu a žízně | Tak akorát |
| Pomůcky v jazyce | Pomohly: společná slova, situace a „asi“, zopakování slova, barva bubliny, gesta Jíst a Pít |
| Pradávný proti Novému | Vyvážený |
| Co štvalo | Příliš mnoho tlačítek |
| Minimapa | Nevšiml si jí (zlepšit viditelnost) |
| První otrava | Méně ochrany než dnes |
| Den | 90 minut |
| Rok v testu | 4 až 5 dní |
| Nový hráč v zimě | Rodí se v teple a má instinkt na zimu |
| Místo narození | Rozptýleně po ostrově, chráněné 24 hodin |
| Offline bez úkrytu | Jak v návrhu (ale v testu se odkládá, odhlášení = uložení na místě) |
| Kmen | Pozvánkou (gesto a slovo) |
| Zásoby kmene | Sdílené |
| Věci z mrtvého těla | 24 hodin jen kmen, potom kdokoli |
| Blokování místa narození | Zakázat stavbu v okolí, 24 hodin, potom povolit |
| NPC cizinci v testu | Zůstanou oba |
| Podpis | Jméno ze slabik a k tomu znak |
| Učení mezi hráči | Jen aktivní učení |
| V testu | Rostliny, oheň a noc, zdi, jazyk, značky, zvířata, spánek, smrt a generace, Pradávní a Noví |
| Mimo test | Zemědělství, pasti a rybaření, hrnčířství, posvátná místa, Síň předků, Pamětníci, plná offline postava |
| Posvátná místa (po testu) | Bonus pro bezpečnější spánek |
| Síň předků (po testu) | Na místě smrti Pradávného |
| Železný žaludek | Nechat |
| Pamětníci (po testu) | První znak po 7 dnech |
| Přihlášení | Kód pozvánky a přezdívka |
| Moderace | Tlačítko Nahlásit, správce rozhoduje |
| Ukládaná data | Navíc statistiky hraní pro ladění |
| Notebook | MSI Cyborg A15 |
| Kamera | Shora, otáčení po 90°, při stavění volné otáčení |
| Ovládání | Klikání myší i WASD |
| Tempo práce | 2 až 3 hodiny denně Po–Čt |
| Prototyp v prohlížeči | Dál se nedolaďuje, test rovnou v C# a Unity |
| Testeři | Zatím neřešit |
| Název | Zatím pracovní, hledat návrhy |
| Veřejná ukázka | Až po uzavřené alfě |

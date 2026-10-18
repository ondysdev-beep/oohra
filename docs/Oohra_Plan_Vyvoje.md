# Oohra – Plán vývoje (verze 2, podle dotazníku 2)

> Tempo: **pondělí až čtvrtek, 2–3 hodiny denně = 8–12 hodin týdně.** Pátek až neděle volno. Časy jsou hrubé odhady. Každá fáze má **„hotovo, když…“**.

## Stav (říjen 2026)

- **Hotovo:** fáze 0, A (jádro v C#), B (Unity), C (síť, 20 robotů) a D (všechny systémy prvního testu: oheň a noc, počasí (mlha, déšť, bouřka) a pochodeň, zdi a úkryt, jazyk ze slabik a gesta s pomůckami, značky, zvířata, kmeny a zásoby, těla a podpisy, Pradávní a Noví s generacemi podle skutečného času, cizí kmeny, tlačítko Nahlásit). Navíc pro nováčky sen bez slov, nápovědy, ochrana v zimě a zvuky.
- **Fáze E z větší části:** ukládání světa (soubor JSON se zálohami místo SQLite, pro 20 hráčů stačí), tajný klíč hráče, příkazy správce, statistiky hraní, záznam chyb u testerů. Návod: `docs/Provoz.md`.
- **Zbývá do alfy:** server na internetu (VPS nebo otevřený port), balíček hry pro testery, kódy pozvánek po lidech.

### Rozhodnutí z posledních sezení (17. 10. 2026)
- **Generace podle skutečného času:** každého půl roku nová generace (`daysPerGeneration` 182,5). Prvních 6 měsíců Pradávní, pak Noví, pak třetí generace s vlastním vzhledem (tkaný zelený oděv, páska přes hruď, čelenka s pírkem) a tak dál. Kdo přijde později, narodí se jako aktuální generace.
- **Po smrti v době Pradávných** se hráč narodí jako Nový (status Pradávného se smrtí ztrácí, návrh 8.6). Kdyby to mělo být jinak, změní se `GenerationTraits.AfterDeath`.
- **Přezdívky** můžou být jakékoli, jen ne sprosté (seznam `blocked_words.json`).
- **Divočáci jsou nebezpečnější** (víc kusů, rozzuří se dřív, silnější výpad a rány, víc životů).
- **Temnější vzhled hry i webu** (17. 10.): web je jen tmavý, s tmou kolem pochodně, mlhou, jiskrami a šeptanými větami. Hra má jít stejným směrem (temnější noci, víc tmy a mlhy, méně světlých ploch), až se bude ladit grafika.
- **Ponuré počasí** (18. 10.): mlha nejčastěji ráno (5 až 10 h), přes den občas, déšť méně často než mlha, bouřka vzácně (víc deště a mlhy na podzim, méně v létě). Počasí je pro všechny stejné, počítá ho server ze seedu a času (`weather.json`). V dešti bez úkrytu je zima a oheň hoří rychleji. Kouř z ohňů, pochodeň (klacek zapálený u ohně, hoří 4 herní hodiny, v dešti kratší, svítí, vlci se jí bojí jako ohně). Den i noc jsou tmavší.
- **Návrh 0.8 Svět a logika** (18. 10.): ostrov 384 × 384 s oblastmi, nový generátor (přesný přepis z prototypu a golden testy ostrova končí), stromy a kácení, nářadí, ruce a batoh, deník, nebezpečí. Jeskyně, patra a mapa později. Alfa až po etapě 3.
- **Ukládání světa do souboru JSON** místo SQLite (pro 20 hráčů stačí, dá se přečíst, přejít na databázi jde později).

### Další kroky (pořadí)
Rozhodnuto 18. 10. 2026: další kolo je **návrh 0.8 Svět a logika** (`docs/Oohra_0.8_Svet_a_logika.md`). Alfa se známými až **po etapě 3** (nováček bez ničeho hru úplně změní). Server na internetu se dá připravit kdykoli mezi etapami, je to krátká práce.
1. **Krok 0 (hotovo 18. 10.):** návrh 0.8 v `docs/`, pravidla v `CLAUDE.md`, tento plán, starý `stats.json` po robotech přesunutý do `stary-svet/`.
2. **Etapy 1 až 5** podle kapitoly „Kolo 0.8“ níže.
3. **Zbytek fáze E:** server na internetu, balíček hry pro testery, kódy pozvánek pro každého zvlášť. Web: nasazení (repozitář je soukromý) a zápis do deníku.
4. **Fáze F:** alfa se 3 až 5 známými (po etapě 3), pak test s 15 až 20 lidmi.

### Neověřeno (zkontrolovat při hraní)
- Vzhled třetí generace (ve hře nastane až za rok, zatím hlídají jen testy).
- Zvuky (slabiky, oheň, kroky, divočák, vlk, déšť, hrom): testy kontrolují jen to, že existují.
- Nový svět 384 × 384: jestli je ostrov hezký na procházení (vzdálenosti, kde se rodí nováček, jak daleko je voda), brod (postava stojí kousek nad vodou), hodně hromad kamenů ve vrchovině, plynulost na grafické kartě.
- Počasí a pochodeň: jestli není mlha moc hustá, déšť moc studený, bouřka moc častá a noc moc tmavá (čísla v `weather.json`, světlo v `DayNightLight`, dohled ve `WeatherView`). Snímky z testů: `unity/Logs/bourka.png`, `mlha.png`.

## Kolo 0.8: Svět a logika (etapy)

Zadání: `docs/Oohra_0.8_Svet_a_logika.md`. Odhad etap 1 až 5 je 9 až 14 týdnů. Každý krok má vlastní větev, testy a sloučení.

### Etapa 1: Svět 384 × 384 a oblasti (2 až 3 týdny)
- [x] Tvar a výška terénu (18. 10., `WorldGenerator`, `worldgen.json`): nový generátor vedle starého (výšková mapa a vlhkost ze seedu, jeden celistvý ostrov s pobřežím). Testy: stejný seed = stejný svět, ostrov v jednom kuse.
- [x] Oblasti podle výšky a vlhkosti: les, bažina, skalnatá vrchovina, louka, jezero **i řeka** (z vrchoviny do jezera a do moře, s brody). Test poměru ploch, oblasti v konzolovém klientovi (`-- 1 384`, příkaz `oblasti`).
- [x] Rozmístění: stromy (řidší, podle dlaždice), rostliny, skály (i vrstvená skála), pitná a bahenní voda, **2 tábory** cizinců (každý v jiné oblasti, u vody, aspoň 110 m od sebe) a **6 zřícenin** na souši. Testy: nic na vodě, tábory na souši. Místa narození zkontrolovat se serverem (další krok).
- [x] Server (18. 10.): výchozí velikost 384, nový svět, úvodní zpráva s mapou (protokol 26, LiteNetLib neumí pole nad 65 535 prvků), ukládání a načtení. 20 robotů 5 minut: tick 2 až 3 ms z 33 ms, vytížení 8 %, asi 15 až 20 kB/s na hráče. Zvířat je podle plochy asi 540, hráč dostane nejbližší, co se vejdou do paketu.
- [x] Unity (18. 10.): zem 384 m stačí jako jeden Terrain (načtení asi 3 s), nová vrstva kamenitá půda, brod jako mělká voda, tráva po čtvercích 16 × 16 m (kreslí se jen, co kamera vidí). Test `BigWorldSceneTests`, snímek `unity/Logs/svet.png`. Výkon na skutečné grafické kartě ověřit při hraní.
- [x] Úklid (18. 10.): starý generátor a golden testy ostrova pryč, testy pravidel v Core na novém světě 128 × 128 (testy náhody `mulberry32` a `hash2` a obsah starých rytin z prototypu zůstávají), roboti a cizinci fungují. **Etapa 1 hotová.**

Rozhodnuto 18. 10.: jezero i řeka, dva tábory cizinců, asi 6 zřícenin, podíly les 35 %, louka asi 25 až 33 %, vrchovina 15 %, bažina 10 %, jezero 3 %, řeka a pláž zbytek.

### Etapa 2: Stromy a kácení (2 až 3 týdny)
- [ ] Stromy jako objekty: pět druhů, výška ze seedu, `trees.json`, vzhled podle druhu v katalogu.
- [ ] Věci ležící ve světě (kmeny, klady, mršiny): poloha, ukládání, posílání hráčům v okolí.
- [ ] Kamenná sekera (jednoduchý recept, opotřebení).
- [ ] Kácení: rány na serveru, pád podle strany seknutí, zapraskání, zranění v pruhu pádu, ležící kmen blokuje, pařez.
- [ ] Klady po 1 m, dřevo, třísky (podpal, pochodeň).
- [ ] Nošení kladu (jeden hráč, pomalu). Kmen pro víc nosičů odložen, kmen se zatím rozseká na místě.
- [ ] Obnova: mladé stromy rostou herní roky, kolem staveb strom nevyroste.

### Etapa 3: Nářadí, ruce, batoh, deník (2 až 3 týdny)
- [ ] Ruce: dvě místa. Batoh je věc, která místa přidá. Nováček začíná bez ničeho (upravit nápovědy a roboty).
- [ ] Recepty jako data (`recipes.json`), u postavy seznam odemčených receptů, smrtí se ztratí.
- [ ] Nářadí: sekera, nůž, oštěp, opotřebení a oprava, pazourek jako lepší kámen.
- [ ] Lýko, kůže (divočák, jelen, králík), mršiny v lese, syrová kůže zahnije za 2 dny, činění popelem u vody (popel z ohniště).
- [ ] Malý a velký batoh.
- [ ] Deník: věc s obsahem (kresby 12 × 12 a poznámky), ukládá se, zůstane u těla.
- [ ] Kosti: jehla a háček (zbytek později).
- [ ] Ukázání receptu jiným hráčem.

### Etapa 4: Nebezpečí (2 až 3 týdny)
- [ ] První vlna: komáři, pijavice, bahenní voda v bažině. Nemoci se zpožděním, léky v rostlinách. Obrana: kouř, bahno nebo popel na kůži, kožené oblečení.
- [ ] Druhá vlna: klíšťata, vosí hnízda, hadi.

### Etapa 5: Sezónní nebezpečí, stopy, sušení a uzení (1 až 2 týdny)

### Později (mimo další kolo)
Jeskyně (etapa 6), patra, střechy a statika (7), mapa jako věc a vor (8).

### Rozhodnout před etapou
- **2:** souhlas s odložením nošení kmene ve více lidech.
- **3:** zdroj lýka (návrh 0.8, bod 10.1). Co znamená „zkoušení“ receptu (návrh: dokud recept neumíš, výroba se občas nepovede a materiál se ztratí).
- **4:** jestli má každá nemoc svůj lék, nebo stačí obecná léčivá složka, kterou rostliny už mají.

## Zásadní změna oproti verzi 1

Prohlížečový prototyp **už nedoladíváme**. Slouží jako zdroj pravidel a čísel (rostliny, otravy, potřeby, generace, jazyk). Zbytek se ověřuje rovnou v C# a v Unity, protože v prohlížeči nejde otestovat server, síť, výkon ani kameru.

## Pracovní smyčka (jedna session)

1. Řekneš, co chceš dělat.
2. Já napíšu kód a uložím ho do repozitáře.
3. V GitHub Desktop: **Fetch origin → Pull**.
4. Spustíš `dotnet build` a `dotnet test` (nebo Unity Play).
5. Pošleš výsledek nebo chybu, opravíme.

Omezení: v mém prostředí nemám .NET SDK ani Unity, takže ověření buildu je vždy u tebe.

---

## Fáze 0 – Příprava (týden 1, večer po koupi notebooku)

- [x] Instalace podle `Oohra_Navod_Nastaveni.md` (Git, GitHub Desktop, .NET SDK 10, Visual Studio, Unity Hub + Unity 6 LTS).
- [x] Ověření: `dotnet --version`, zkušební Unity projekt se spustí.
- [x] Soukromý repozitář `oohra`, klon do `C:\Oohra`, pošli mi jméno `tvojejmeno/oohra`.
- [x] Unity Essentials (learn.unity.com) průběžně.

**Hotovo, když:** `dotnet --version` vypíše číslo a repozitář je na disku.

## Fáze A – Jádro v C# (týdny 2–6)

- [x] Řešení `Oohra.Core`, `Oohra.Server`, `Oohra.Protocol`, `Oohra.TestClient` + testy.
- [x] Přepis z prototypu: generování ostrova ze seedu, rostliny se skrytým složením, otravy se zpožděním, potřeby, dva rytmy času (den 90 min, rok 4–5 dní), Pradávní a Noví, spánek a dovednosti, smrt a generace.
- [x] Automatické testy (stejný seed = stejný svět).
- [x] TestClient v konzoli: sníš rostlinu, po čase vidíš příznaky.

**Hotovo, když:** `dotnet test` projde a v konzoli prožiješ otravu.

## Fáze B – Unity „šedá krabice“ (týdny 6–11)

- [x] Unity projekt ve `unity/`, napojení Core.
- [x] Ostrov z kvádrů a válců, postava, pevná kamera shora, otáčení po 90°, klik + WASD.
- [x] Základní HUD: potřeby, batoh, zápisník.

**Hotovo, když:** chodíš po ostrově, sbíráš a jíš rostliny, vidíš příznaky. Zatím jeden hráč.

## Fáze C – Síť (týdny 11–17)

- [x] Malý pokus s LiteNetLib (ověřit aktuální stav knihovny).
- [x] Autoritativní server, tick, pohyb, přihlášení kódem pozvánky + přezdívkou.
- [x] Dva klienti se vidí. Skrytá data zůstávají na serveru.
- [x] Simulace 20 botů pro zátěž.

**Hotovo, když:** dvě okna hry se vidí a server počítá jídlo a otravy. **Největší technické riziko**, případně dáme víc času.

## Fáze D – Herní systémy (týdny 17–34)

Po jednom, vždy krátký test: oheň a noc, zdi a úkryt, jazyk ze slabik a gesta, značky a nákresy, zvířata (divočák, vlk, králík, jelen), kmen pozvánkou a sdílené zásoby, těla a podpisy (slabikové jméno + znak), Pradávní a Noví, tlačítko Nahlásit.

Mimo první test: zemědělství, pasti a rybaření, hrnčířství, posvátná místa, Síň předků, Pamětníci, offline postava.

## Fáze E – Provoz (týdny 34–39)

SQLite ukládání, účty přes pozvánky, statistiky hraní, VPS, zálohy, restart bez ztráty světa.

## Fáze F – Uzavřená alfa a test (týdny 39–46+)

3–5 lidí, pak 15–20. Sledujeme, kde umírají, kde se nudí, co nechápou. Veřejně (screenshoty, video) až po uzavřené alfě.

---

## Přehled

| Fáze | Trvání | Riziko |
|---|---|---|
| 0 Příprava | 1 týden | Instalace |
| A Jádro C# | 5 týdnů | Chyby v přepisu (hlídají testy) |
| B Unity | 5 týdnů | Učení Unity |
| C Síť | 6 týdnů | Největší riziko |
| D Systémy | 17 týdnů | Rozsah |
| E Provoz | 5 týdnů | Nasazení |
| F Alfa | 7+ týdnů | Zpětná vazba |
| **Celkem** | **zhruba 10–11 měsíců** | |

## Dnes večer

1. Instalace podle návodu (části 3.1–3.6).
2. Založ repozitář `oohra` (private) a napiš mi jeho jméno.
3. Pak začneme Fázi A.

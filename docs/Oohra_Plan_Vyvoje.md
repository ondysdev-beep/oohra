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
- **Ukládání světa do souboru JSON** místo SQLite (pro 20 hráčů stačí, dá se přečíst, přejít na databázi jde později).

### Další kroky (pořadí)
1. **Vyzkoušet hru sám:** nový hráč (sen bez slov), F1, zvuky, přežití první noci, cizinci, divočáci.
2. **Dlouhý test s roboty** (třeba 5 hodin) s chytřejšími roboty, potom projít `stats.json`. Před testem s lidmi `stats.json` a `world.json` po robotech přesunout nebo smazat.
3. **Webová stránka hry** (`web/`, koncept `docs/WEB_KONCEPT.md`): rozdělená na soubory, sdílení, favicon, přístupnost, tlačítko Mlha a skutečný postup (84 %) jsou hotové. Zbývá nasazení (GitHub Pages, repozitář je soukromý: buď veřejný repozitář jen pro web, nebo placený GitHub) a nový zápis do deníku.
4. **Zbytek fáze E:** server na internetu (VPS nebo otevřený port), balíček hry pro testery (zip s `Oohra.exe` a adresou serveru), kódy pozvánek pro každého testera zvlášť.
5. **Fáze F:** alfa se 3 až 5 známými, pak test s 15 až 20 lidmi.

### Neověřeno (zkontrolovat při hraní)
- Vzhled třetí generace (ve hře nastane až za rok, zatím hlídají jen testy).
- Zvuky (slabiky, oheň, kroky, divočák, vlk, déšť, hrom): testy kontrolují jen to, že existují.
- Počasí a pochodeň: jestli není mlha moc hustá, déšť moc studený, bouřka moc častá a noc moc tmavá (čísla v `weather.json`, světlo v `DayNightLight`, dohled ve `WeatherView`). Snímky z testů: `unity/Logs/bourka.png`, `mlha.png`.

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

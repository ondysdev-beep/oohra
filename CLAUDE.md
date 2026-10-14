# Oohra: pokyny pro práci na kódu

Hráč a vývojář mluví česky. Všechny odpovědi, komentáře v kódu a dokumenty piš česky, názvy v kódu anglicky.

## O projektu
Oohra je multiplayerová hra o přežití pro PC (Unity klient, vlastní autoritativní C# server, jeden trvalý svět ostrovů). Zásadní prvky: skryté složení rostlin (hráč ho zjišťuje pokusem), otravy se zpožděním, generace (Pradávní a Noví), jazyk ze slabik, znalosti se ztrácejí smrtí. Podrobný návrh je v `docs/Oohra_0.7.md` (to je zadání), plán v `docs/Oohra_Plan_Vyvoje.md`.

Vývojář je hobby programátor začátečník v C# a Unity. Vysvětluj stručně a srozumitelně, bez zbytečného žargonu. Neprováděj velké změny, které nebyly domluvené.

## Rozložení repozitáře
```
docs/        dokumenty
prototype/   prototyp v JS (jen čtení, zdroj pravidel). src/ jsou zdrojové části, oohra.html je sestavený prototyp
server/      C# řešení (Oohra.sln)
  Oohra.Shared/          viditelné typy pro server i klienta (dlaždice, části rostlin, kódy hlášek), jmenné prostory Oohra.Core.*
  Oohra.Core/            pravidla hry bez sítě a bez grafiky, i se skrytými daty (složka Secret)
  Oohra.Core.Tests/      xUnit testy, golden/ obsahuje data z prototypu
  Oohra.Protocol/        zprávy klient–server (LiteNetLib, ruční zápis přes NetDataWriter) a síťový klient GameConnection. NESMÍ odkazovat na Oohra.Core
  Oohra.Server/          autoritativní server (server.json: port, seed, kódy pozvánek; pro zkoušení timeSpeed, startHour, startSticks, startStones), spuštění dotnet run --project Oohra.Server
  Oohra.Server.Tests/    testy serveru a klientů přes skutečnou síť (localhost)
  Oohra.TestClient/      konzolový klient bez sítě (textová mapa a příkazy)
  Oohra.Bots/            zátěžový test: roboti jako hráči (jen Protocol), dotnet run --project Oohra.Bots -port 9050 -count 20 -seconds 60. Výkon vypisuje server (statsEverySeconds)
unity/       klient v Unity (Fáze B šedá krabice, Fáze C napojení na server)
```

## Rozhodnutí (z dotazníků)
- Cíl .NET 10 (LTS), C# s `<Nullable>enable</Nullable>`, testy xUnit.
- Názvy tříd, metod a proměnných anglicky, komentáře česky.
- Čísla hry (rostliny, potřeby, účinky, časy) v datových souborech JSON v `Oohra.Core/Data/`, ne natvrdo v kódu.
- Generátor ostrova se přepíše přesně z prototypu (`prototype/src/part1.js`), aby stejný seed dal stejný svět. Velikost světa je parametr. Prototyp má 84 × 84 a golden testy běží na 84 × 84. První test hry bude 128 × 128, výchozí hodnota v nastavení je 128.
- Server počítá 30 ticků za sekundu (hodnota v nastavení, ne konstanta v kódu).
- Den 90 minut, rok 4 až 5 reálných dní (v nastavení).
- Přihlášení kódem pozvánky a přezdívkou, tlačítko Nahlásit, statistiky hraní pro ladění (až ve fázi provozu).
- Skrytá data (složení rostlin) existují jen na serveru. Klient je nikdy nedostane. V `Oohra.Core` je držíme odděleně od typů, které půjdou na klienta.

## Pravidla kódu
- Determinismus: žádný `System.Random` ve světě. Použij vlastní generátor `mulberry32` a `hash2` přepsané z prototypu.
- Pozor na rozdíly JavaScriptu a C#: `Math.imul` je násobení `int` s přetečením (v C# `unchecked`), `>>>` je posun u `uint`, `|0` je přetypování na `int`, čísla v JS jsou `double`. Pořadí průchodu `Map` v JS je pořadí vkládání.
- Každé pravidlo hry má test. Test z prototypu: golden data v `server/Oohra.Core.Tests/golden/golden_world.json` (seedy 1, 12345 a 987654321).
- Malé, čitelné metody. Žádná předčasná obecnost.

## Unity a síť (Fáze B a C)
- Unity 6000.6.5f1, projekt `unity/` (URP, nový Input System). Scéna hry `Assets/Scenes/Ostrov.unity`, spouští ji objekt `Hra` se skriptem `IslandBootstrap` (přihlášení, pak stavba ostrova z mapy od serveru).
- Unity je jen síťový klient. Má `Oohra.Shared`, `Oohra.Protocol` a `LiteNetLib` (DLL v `unity/Assets/Plugins/Oohra/`, kopírují se samy po `dotnet build`, jsou v gitu). `Oohra.Core` do Unity NIKDY nedávej (skrytá data). Hlídá to test `ProtocolInUnityTests.ClientHasNoHiddenData`.
- Unity umí jen .NET Standard 2.1 a C# 9. Skripty v `unity/Assets` piš bez novějšího C# (žádné `namespace X;`, `required`, `[..]` kolekce, `record struct`). Typy, které Unity vytváří, nesmí mít `required` vlastnosti.
- Knihovny pro server i Unity (Shared, Protocol, Core) cílí i na `netstandard2.1`: nepoužívej `System.Text.Json` ani API novější než .NET Standard 2.1 (např. `MaxBy`, `Math.Tau`). JSON čte Newtonsoft.Json.
- Pravidla (pohyb, kolize, tělo) patří do Core a počítá je server. Klient posílá jen úmysly a zobrazuje stav ze serveru.
- Menu Unity „Oohra“: Spustit server, Sestavit hru pro Windows (`unity/Build/Oohra.exe`, mimo git, běží v okně a i na pozadí), Vytvořit scénu ostrova.
- Materiály a shadery musí být soubory v projektu (sdílené `Assets/Resources/OohraLit.mat` a `OohraGlow.mat` pro věci, které svítí samy; vytváří je `OohraSceneSetup.EnsureMaterials`). `Shader.Find` v kódu funguje jen v editoru, sestavená hra by byla růžová. Hlídá to test `BuildReadinessTests`.
- Katalog vzhledu (`VisualCatalog`, soubor `Assets/Resources/VisualCatalog.asset`, menu Oohra → Katalog vzhledu): vlastní prefaby pro stromy, rostliny (podle tvaru, obarví se barvou druhu přes `SpeciesTint`), stavby a zvířata (Animator, parametr `Stav`). Prázdné políčko = tvar z kódu. Zprůhledňování vlastních modelů dělá `FadeTarget` (zachová materiály). Návod pro vývojáře: `docs/Grafika.md`.
- Sestavená hra umí přihlášení z příkazové řádky: `Oohra.exe -nick Petr` (volitelně `-server`, `-port`, `-code`).
- Jazyk (návrh 9): pravidla v `Oohra.Core/Language` a `language.json`, zápisník slov je na serveru v `Character.Vocabulary` (smrtí se ztratí, Pradávný má Starý hlas). Hra přenáší jen slabiky a náladu, význam nikdy. V Unity `SpeechHud`: T řeč (Shift + 1 až 0, Enter, Tab nálada), V gesta, J zápisník.
- Značky (návrh 7.2, 14.3): kresba 12 × 12 bodů (`SignBits` v Shared), pravidla v `Oohra.Core/World/Signs.cs` a `signs.json`. Na skále rytina (vydrží), jinak zvětrá za 5 dní, smazat smí jen autor. Staré rytiny u ruin (`RuinCarvings`, shodné s prototypem makeRuins) přidá server při startu. V Unity `SignViews` (scéna) a `SignHud` (H kreslit, čtení po kliknutí na značku).
- Kmen (návrh 14.1): `Oohra.Core/Social/Tribes.cs` a `tribes.json`, patří hráči (číslo hráče), ne postavě, takže přežije smrt. Vzniká pozvánkou (klik na hráče → Pozvat do kmene, pozvaný přijme), kmen s jedním člověkem zanikne. Stavby mají majitele (`OwnerId`): rozebrat, vzít úlovek a brát ze zásob smí jen majitel a jeho kmen (`Structures.MayTouch`). Zásoby kmene = stavba `Stockpile` (koš). V Unity `TribeHud` (pozvánka, Opustit kmen, koš).
- Měřítko: 1 dlaždice = 1 metr (postava 1,8 m, poloměr 0,28 m, stromy 4 až 6 m).
- Zvířata (`Animals` v Core, data `animals.json`) počítá server a každému hráči posílá jen zvířata v okolí (`animalViewDistance`). Testy serveru a PlayMode je vypínají (`"animals": false`), pokud je test sám nechce.
- Stavby se staví v režimu stavby (`BuildMode`, klávesa G, R přepíná volně a modulárně, přichycení počítá `ModularSnap`): přesná poloha, úhel a délka zdi (0,25 až 1 m, dorovnání mezer), ne dlaždice, nejdál 3,5 m od postavy. Zeď s otvorem blokuje jen sloupky, dveře neblokují (otevírá je `DoorSwing`, jen vzhled), ale uzavřou úkryt. Nová stavba = hodnota v `BuildKind` (Shared), pravidla v Core (`Structures`, `Placement`), položka v `BuildMode.Catalog` a vzhled ve `StructureViews`.
- Ověření Unity bez okna (`U="/c/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe"`):
  `"$U" -batchmode -projectPath unity -runTests -testPlatform EditMode -testResults <soubor.xml> -logFile <log>` (stejně `PlayMode`). Test PlayMode spustí skutečný server (`server/Oohra.Server/bin/Debug/net10.0`, napřed `dotnet build`) a uloží snímky do `unity/Logs` (`sit.png`, `noc.png`). Pomůcky jsou v `PlayTestTools`. `WaitForEndOfFrame` a `ScreenCapture` v batch mode nefungují.

## Pracovní postup
- Každý úkol na vlastní větvi `feature/...`, na konci commit a push větve. Potom Claude větev sloučí do `main` (`git merge --no-ff`), na `main` ověří `dotnet build` a `dotnet test` a pushne `main`. Příkazy `git checkout main`, `git merge --no-ff ...` a `git push origin main` spouštěj každý zvlášť (tak jsou povolené v `.claude/settings.local.json`). Přímé commity do `main` se nedělají.
- Repozitář: `github.com/ondysdev-beep/oohra`. Nástroj `gh` není nainstalovaný, pull requesty se nedělají.
- Commit zprávy česky, stručně.
- Na začátku sezení vývojář řekne, co chce dělat, ty navrhneš plán na hodinu.
- Po každé změně spusť `dotnet build` a `dotnet test` a oprav chyby, než ohlásíš hotovo.
- Na konci sezení krátké shrnutí: hotovo a příště.
- Nic nemaž a nepřepisuj mimo zadání. Prototyp v `prototype/` neupravuj.

## Fáze A: úkoly (v tomto pořadí)
1. Kostra řešení: `server/Oohra.sln`, projekty Core, Core.Tests, TestClient, `.editorconfig`. `dotnet test` projde (zatím s jedním prázdným testem).
2. Generátor čísel: `mulberry32` a `hash2` + testy proti `prng` a `hash2` v golden datech.
3. Svět: přepis `buildWorld` z `prototype/src/part1.js` (dlaždice, pobřeží, tábory, ruiny, stromy, rostliny). Testy proti golden datům (dlaždice se musí shodovat přesně, desetinná čísla na 1e-9).
4. Druhy rostlin: přepis `buildSpecies` a archetypů do JSON dat + testy.
5. TestClient: textová mapa ostrova a seznam druhů rostlin.
6. Postava, potřeby, čas, jedení, zpožděná otrava a příznaky (pravidla z `prototype/src/part2.js` a `part4.js`, hodnoty z `docs/Oohra_0.7.md`).

Fáze A je hotová, když v konzoli snědíš rostlinu a po čase uvidíš příznaky otravy a `dotnet test` projde.

## Co teď nedělat
Síť, Unity, grafiku, jazyk, NPC kmeny, zvířata, stavby. To přijde v pozdějších fázích.

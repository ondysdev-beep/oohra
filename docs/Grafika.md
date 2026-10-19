# Vlastní grafika v Oohře

Hra umí místo jednoduchých tvarů (kostky, válce, koule) použít tvoje modely. Které model kde použít,
říká **katalog vzhledu**. Prázdné políčko v katalogu znamená, že hra použije tvar z kódu, takže
modely můžeš přidávat postupně, jeden po druhém.

Zatím jde vyměnit: **stromy, rostliny, zeď, zeď s otvorem, dveře, past, koš se zásobami, dekorace, zvířata,
textury zemi, kameny na skalách, vodu, postavu hráče a oheň.**

## Kde je katalog

V Unity v horním menu **Oohra → Katalog vzhledu**. Vpravo v Inspectoru se ukážou políčka
(Trees, Plants, Wall, Door…). Soubor leží v `unity/Assets/Resources/VisualCatalog.asset`.

## Postup: od modelu do hry

1. **Model.** Vymodeluj ho (např. Blender, zdarma) nebo stáhni hotový.
   Dobré zdroje zdarma: [Kenney](https://kenney.nl), [Quaternius](https://quaternius.com),
   [Poly Pizza](https://poly.pizza). U stažených modelů zkontroluj licenci (nejlepší je CC0).
2. **Export.** Ulož jako `.fbx` (v Blenderu File → Export → FBX).
   - Měřítko: 1 metr = 1 dlaždice. V Blenderu nech jednotky v metrech, při exportu
     „Apply Scalings: FBX All“.
   - Osa: v Blenderu je nahoru Z, Unity si to při importu otočí samo (nahoru Y).
3. **Do projektu.** Přetáhni `.fbx` (i s texturami) do složky `unity/Assets/Models/…`
   (např. `Models/Stromy/Dub.fbx`). Unity ho samo načte.
4. **Materiály.** Klikni na model v Unity, v Inspectoru záložka **Materials** a tlačítko
   **Extract Materials** (vytáhne materiály jako soubory). Každý materiál musí mít shader
   **Universal Render Pipeline/Lit**, jinak bude model ve hře **růžový**.
   Hra to kontroluje testem (`VisualCatalogTests`).
5. **Prefab** (hotový díl).
   - Přetáhni model ze složky Models rovnou do složky `Assets/Prefabs/`. Tím vznikne prefab.
     (Nebo model přetáhni do scény, uprav a pak ze scény do Prefabs. **Ze scény ho pak smaž**, jinak
     zůstane ve hře napevno na jednom místě.)
   - **Na středu a poloze modelu v prefabu nezáleží:** hra každý model posune tak, aby stál dole
     uprostřed. Stromy, rostliny, kameny a trávu navíc sama zmenší nebo zvětší (velikosti jsou
     v katalogu). U staveb, postavy a zvířat velikost dodrž (tabulka níže, kostka 1 × 1 × 1 m pomůže).
   - Natočení (kam míří „dopředu“) hra neopraví: podle tabulky níže.
6. **Do katalogu.** Otevři katalog (Oohra → Katalog vzhledu) a prefab přetáhni do políčka.
7. **Vyzkoušej.** Spusť server (Oohra → Spustit server) a hru (Play), nebo sestav hru
   (Oohra → Sestavit hru pro Windows).

Kolize (Collider) v prefabu nevadí, hra je smaže. Kudy se dá projít, počítá server.

## Jak mají být modely velké a natočené

„Střed“ je bod, kde má model v Unity počátek (pivot). „Dole“ znamená na úrovni země.

| Políčko | Velikost | Střed a natočení | Poznámka |
|---|---|---|---|
| **OakTrees**, **ConiferTrees**, **BirchTrees**, **WillowTrees**, **DeadTrees** (dub, jehličnan, bříza, vrba, suchý strom) | libovolná, hra strom srovná na **výšku ze serveru** (dub 5 až 8 m, jehličnan 6 až 8 m, bříza 4 až 7 m, vrba 3 až 5 m, suchý 3 až 6 m, čísla v `trees.json`) | na středu nezáleží | Políčko druhu má přednost. Víc modelů v políčku = víc variant, každý strom dostane jednu. Hra strom natočí podle místa. Na serveru je kmen kruh o poloměru 12 cm, tlustší kmen by hráč „prošel“. |
| **Trees** (společné stromy) | jako výše | | Použijí se pro druh, jehož políčko je prázdné (tvoje dosavadní modely tu zůstaly). Když je prázdné i tohle, hra nakreslí tvar z kódu podle druhu. |
| **Plants** (rostliny) | libovolná, hra ji srovná na výšku **PlantHeight** (0,45 m) ±15 % | na středu nezáleží | Jen **4 políčka** (0 až 3) = 4 tvary druhů, další se nepoužijí. Obarví se barvou druhu, aby šly druhy rozeznat (viz níže). |
| **Wall** (zeď) | 1 m dlouhá, 2 m vysoká, 10 cm tlustá | dole uprostřed, délka podél osy X | Kratší zeď (poloviční, dorovnání) hra zmáčkne podél X. |
| **DoorFrame** (zeď s otvorem) | 1 m × 2 m, otvor 0,8 m uprostřed, vysoký 1,9 m | dole uprostřed, délka podél X | Sloupky po krajích jsou po 10 cm. |
| **Door** (dveře) | 0,8 m široké, 1,9 m vysoké, asi 5 cm tlusté | dole uprostřed křídla, šířka podél X | Pant na levém kraji přidá hra sama (dveře se otáčejí kolem něj). |
| **Trap** (past) | asi 0,6 × 0,6 m | dole uprostřed | Úlovek v pasti zatím kreslí hra (hnědá koule). |
| **Stockpile** (koš) | průměr asi 0,6 m, výška 0,45 m | dole uprostřed | |
| **Boar, Rabbit, Deer, Wolf** (zvířata) | divočák 1 m dlouhý, vlk 0,95 m, jelen 1,1 m, králík 0,35 m | nohy dole, hlava ve směru osy Z (dopředu) | Mrtvé zvíře hra položí na bok. |
| **Snake** (had) | asi 1 m dlouhý, leží na zemi | břicho dole, hlava ve směru osy Z | Animator parametr `Stav`: 4 spí stočený (zima, noc), 6 varuje (vztyčený, syčí), 0 leze, 9 mrtvý. |
| **MeatStrip** (kus masa) | asi 0,08 × 0,25 m | úchyt nahoře uprostřed | Visí v kouři nad ohněm a na sušáku. Hra ho obarví podle stavu (syrové, uzené, sušené), model má být světlý nebo šedý. Model sušáku může mít maso namalované, hra pověsí skutečné kusy do výšky 1 m. |
| **CaveEntrance**, **HiddenCaveEntrance** (vchod do jeskyně) | otvor asi 1,5 × 2 m, nenápadný úzká puklina | otvor ve směru osy Z, zadní strana se opře o skálu, dole uprostřed | Stojí na hraně skály u chodného místa. Nenápadný vidí jen ten, kdo ho objevil. |
| **CaveExit** (východ uvnitř) | podle chuti, třeba suť pod otvorem | dole uprostřed | Hra k němu přidá světlo shora. Prázdné = průsvitný sloup světla. |
| **Post** (sloupek) | 2 m vysoký, asi 0,14 m silný | dole uprostřed | Podpírá střechu. |
| **Floor** (podlaha) | 2 × 2 m, nízká | dole uprostřed čtverce | Leží na zemi. |
| **Roof** (střecha) | 2 × 2 m, spodek ve výšce 2 m | počátek dole uprostřed čtverce na zemi, střecha v modelu ve výšce 2 m | Hra ji neposouvá nahoru, výšku má mít model. Praskající střecha se třese. |
| **Barricade** (zátaras) | asi 1,5 m široký, 1,6 m vysoký | podél osy X, dole uprostřed | Stojí na hraně skály před vchodem do jeskyně, hra ho natočí podle skály. |
| **CaveWallColor** (barva, ne model) | | | Barva skalních stěn jeskyně (bloky vysoké 1,6 m). |
| **FloodColor** (barva, ne model) | | | Barva kalné vody jarní záplavy nad zatopenou souší, průhlednost v alfě. |
| **WaspNest** (vosí hnízdo) | asi 0,3 × 0,4 m | úchyt nahoře uprostřed | Hra ho pověsí na strom asi 2,6 m nad zem. Rozzuřené vosy kolem kreslí hra sama (roj teček). |

## Rostliny a barva druhu

Hráč pozná druh rostliny podle tvaru a **barvy listů**. Proto hra vlastní model rostliny obarví barvou
druhu (barva se násobí s texturou, takže textura listů by měla být spíš světle šedá).

- Obarví se jen části, které mají komponentu **SpeciesTint** (v Inspectoru Add Component → Species Tint).
  Dej ji na listy. Stonek, květináč a podobně nech bez ní.
- Když SpeciesTint nemá žádná část, obarví se celý model.
- **Plod:** když má model přímo pod sebou prázdný objekt pojmenovaný `Plod`, plod se ukáže tam.
  Jinak se ukáže kousek vedle rostliny. Plod zatím kreslí hra (barevná kulička podle druhu a zralosti).

## Zvířata a animace

Model zvířete může mít **Animator** s animacemi (chůze, běh, spánek…). Hra do něj posílá stav zvířete
ze serveru jako celé číslo v parametru **`Stav`** (typ Int). V Animatoru udělej přechody podle hodnoty:

| Stav | Číslo | Co dělá |
|---|---|---|
| Wander | 0 | toulá se |
| Graze | 1 | pase se |
| Flee | 2 | utíká |
| Tired | 3 | je unavené |
| Asleep | 4 | spí |
| Stalk | 5 | plíží se (vlk) |
| Alert | 6 | je ve střehu (divočák) |
| Charge | 7 | útočí |
| Exhaust | 8 | vyčerpané po útoku |
| Dead | 9 | mrtvé |

Bez Animatoru se model jen posouvá a otáčí.

## Postava hráče

Políčko **Player**. Model: nohy dole v počátku, výška 1,8 m, obličej ve směru osy Z. Na serveru je
postava kruh o poloměru 28 cm, takže model by neměl být o moc širší.

- **Barva generace:** části s komponentou **SpeciesTint** dostanou barvu generace (Pradávní tmavší,
  Noví světlejší). Hodí se třeba na oblečení. Bez SpeciesTint zůstane model, jak je.
- **Generace:** každého půl roku skutečného času přijde nová generace a vypadá jinak.
  - Políčko **PlayerAncient** je model pro Pradávné (první půlrok: mohutní, kožešinový plášť, ochra,
    kostěné korálky).
  - Políčko **Player** je model pro Nové (druhý půlrok: štíhlejší, vyšší, prostý světlý oděv).
  - Políčko **PlayerThird** je model pro třetí generaci a další (tkaný zelený oděv, páska přes hruď,
    čelenka s pírkem).
  - Prázdné políčko = model z **Player**. Hra navíc každou postavu roztáhne podle generace (skript
    `GenerationLook`): Pradávný je o 15 % širší a o kousek menší, další generace jsou štíhlejší a vyšší.
  - Bez modelu hra kreslí postavu z tvarů i s doplňky (u čtvrté generace korálky, u šesté malba na obličeji).
    K vlastnímu modelu hra doplňky nepřidává (nevěděla by, kde má hlavu), takže je dej rovnou do modelu.
- **Animace:** když má model Animator, hra do něj posílá tyto parametry (stačí ty, které chceš použít):

| Parametr | Typ | Co znamená |
|---|---|---|
| `Rychlost` | Float | jak rychle postava jde (m/s): 0 stojí, asi 2 až 3 jde |
| `Spi` | Bool | postava spí |
| `Mrtvy` | Bool | postava je mrtvá |

- Když model nemá parametr `Spi` (nebo `Mrtvy`), hra ho při spánku (nebo smrti) prostě položí na zem.
- Tělo mrtvého hráče (zůstane na zemi 4 dny s jeho věcmi) použije stejný model, položený na záda.

## Kácení: pařez, kmen a klad

| Políčko | Velikost | Střed | Poznámka |
|---|---|---|---|
| **Stumps** (pařezy) | hra je srovná na výšku 0,6 m | na středu nezáleží | Zůstanou po pokáceném stromu. Víc variant = víc políček (třeba dub, borovice, bříza), každý pařez dostane jednu. |
| **Log** (ležící kmen) | jakkoli dlouhý model ležící **podél osy X nebo Z** (třeba 6 m z Meshy). Hra ho natáhne na délku kmene (3 až 7 m) a tloušťku srovná na 0,44 m | na středu nezáleží | Na serveru kmen blokuje jako válec o poloměru 22 cm. Konce se při natažení protáhnou. |
| **LogPiece** (klad) | stejně jako kmen, hra ho srovná na 1 m délky a 0,44 m tloušťky | na středu nezáleží | Kus kmene 1 m: leží na zemi a postava ho nese na rameni. |
| **Saplings** (mladé stromky) | hra je srovná na výšku podle růstu | na středu nezáleží | Dokud mladý strom roste. Dorostlý strom pak použije model svého druhu. Prázdné = zmenšený strom druhu. |
| **Torch** (pochodeň, nezapálená) | hra ji srovná na 0,6 m | na středu nezáleží | V ruce postavy, plamen, kouř a světlo přidá hra nahoře sama. |

Prázdné políčko = válec z kódu v barvě kůry podle druhu stromu (bříza světlá, suchý strom šedý).

## Batohy na zádech

| Políčko | Velikost | Poznámka |
|---|---|---|
| **SmallBag** (malý batoh z lýka) | hra ho srovná na výšku 0,4 m | Nosí se na zádech, vidí ho všichni. Strana, která přiléhá k zádům, ať míří k **-Z** modelu (záda postavy). |
| **LargeBag** (velký batoh z kůže) | hra ho srovná na výšku 0,6 m | Stejně jako malý. |

Prázdné políčko = kvádr z kódu v barvě batohu.

## Oheň

| Políčko | Co to je |
|---|---|
| **Campfire** | ohniště (kameny kolem, polena), průměr asi 0,7 m, střed dole uprostřed. Použije se u ohňů hráčů, u táborů kmenů i jako náhled v režimu stavby. |
| **Flame** | plamen. Ukáže se jen, když oheň hoří. Hodí se částicový systém (Particle System) s materiálem, který svítí sám. Oranžové mihotavé světlo přidá hra. |

Kouř nad ohněm přidá hra sama (částice `ParticleLook.CreateSmoke`, sklání se po větru). Pochodeň v ruce (`HandTorch`) je zatím tvar z kódu: klacek, plamínek, kouř a světlo.

## Počasí a světlo

Počasí posílá server (mlha, déšť, bouřka, síla a vítr). Ve scéně ho kreslí `WeatherView`: mlha je Unity fog (od kamery, dohled podle počasí, pozadí kamery má barvu mlhy), déšť jsou částice kolem postavy, v bouřce blýská a hřmí. Slunce a okolní světlo tlumí `DayNightLight` (svět je i ve dne ponurý, v mlze skoro bez stínů). Materiály částic jsou kopie `OohraFoam.mat` (URP/Particles/Simple Lit) s měkkou texturou z kódu, takže je osvětlí oheň a v noci nesvítí. Zkoušení: příkaz `pocasi` v okně serveru.

## Dekorace (lavice, totem, cokoli dalšího)

Dekorace jsou v seznamu **`server/Oohra.Core/Data/decorations.json`**. Novou dekoraci přidáš takto:

1. Do seznamu přidej jeden řádek, třeba:
   ```json
   { "id": "drying_rack", "name": "Sušák na maso", "sticks": 4, "stones": 0, "radius": 0.5, "blocks": true },
   ```
   - `id` je krátký název bez mezer a diakritiky. Spojuje řádek s modelem.
   - `name` uvidí hráč v nabídce stavby.
   - `sticks` a `stones` jsou cena. Rozebráním se vrátí polovina.
   - `radius` je poloměr v metrech, tedy kolik místa dekorace zabere.
   - `blocks: true` znamená, že se přes ni nedá projít. `false` je třeba pro kruh z kamenů na zemi.
   - Pozor na čárky: za posledním řádkem v seznamu čárka není.
2. V Unity v katalogu vzhledu v části **Decorations** přidej položku (tlačítko +), do **Id** napiš stejné
   id a do **Prefab** přetáhni model. Střed modelu dole uprostřed, velikost podle `radius`.
3. Restartuj server (Oohra → Spustit server). Dekorace se objeví v režimu stavby (G) za ostatními stavbami.

Bez modelu v katalogu se dekorace ukáže jako hnědý sloupek s kuličkou, takže jde vyzkoušet hned.
Dekoraci, kterou už někdo postavil, nepřejmenovávej (`id`), jinak ji hra nepozná.

## Zem: textury dlaždic

Zem je Unity **Terrain**. Každý druh dlaždice má svou texturu a na hranách se textury plynule prolínají.
Souš je rovně ve výšce 0, dno moře a jezírek je pod hladinou a ke břehu se zvedá.

V katalogu jsou políčka pro textury:

| Políčko | Kde se použije |
|---|---|
| **GrassTexture** | tráva |
| **SandTexture** | písek, pláž a dno moře |
| **ForestTexture** | les (zem pod stromy) |
| **RockTexture** | skála |
| **BogTexture** | bažina a bahnité dno jezírek |
| **HighlandTexture** | kamenitá půda skalnaté vrchoviny (mezi skalami). Vrstvená skála má zatím texturu skály, brod písečné dno |

- Textura musí jít **opakovat bez švů** (anglicky „seamless“ nebo „tileable“). Takové najdeš třeba na
  [ambientCG](https://ambientcg.com) nebo [Poly Haven](https://polyhaven.com) (zdarma, CC0).
  Stačí obrázek barvy („Color“ nebo „Albedo“), 512 nebo 1024 px.
- **GroundTextureSize** říká, po kolika metrech se textura opakuje (výchozí 4 m).
- Prázdné políčko = hra si texturu vygeneruje ze základní barvy dlaždice (jemně skvrnitá barva).

## Skály (hromady kamenů)

Na části skalních dlaždic leží hromada kamenů (které to jsou, určuje server podle seedu, `rocks.json`).
Každý kámen v hromadě je jeden kámen k vytěžení: hráč klikne na hromadu a dá „Vzít kámen“, nejvyšší kámen
zmizí. Prázdná hromada zmizí celá a za 3 herní dny se obnoví. Přes hromadu se nedá projít.

- Políčko **Rocks**: jeden nebo víc prefabů kamenů, hra je v hromadách střídá. Dole je kruh až čtyř
  velkých kamenů, nahoře menší.
- **RockMinSize** a **RockMaxSize**: největší rozměr jednoho kamene (0,4 až 0,6 m). Na velikosti a středu
  modelu nezáleží, hra ho srovná. Hromada pak má asi 1 m.
- Prázdné políčko = šedé zploštělé kuličky.

## Klikání na věci

Na strom, rostlinu, hromadu kamenů, stavbu, oheň, značku, zvíře i jiného hráče se kliká přímo na model
(i na korunu stromu vysoko nad zemí). Hra každému objektu dá neviditelnou krabici přes celý model.
Zprůhledněný strom (zakrývá postavu) klik propustí na to, co je za ním.

## Tráva (jen ozdoba)

Políčko **Grass**: trsy trávy, které hra rozhází po travnatých dlaždicích (v lese řidčeji, u kmene ne).
Nejsou to rostliny ze hry, nedá se na ně kliknout a server o nich neví.

- **GrassHeight**: výška trsu (hra model srovná, ±20 %).
- **GrassPerTile**: kolik trsů průměrně připadá na 1 m² trávy. Trsů jsou tisíce, kreslí se úsporně
  (stovky naráz), ale na slabém počítači můžeš číslo snížit.

## Voda

Moře i jezírka mají jednu společnou hladinu kousek pod souší. Je průhledná a lesklá (odráží oblohu)
a pomalu se po ní posouvají vlnky.

| Políčko | Co dělá |
|---|---|
| **WaterColor** | barva hladiny, alfa (A) je průhlednost (víc = méně je vidět dno) |
| **WaterNormalMap** | obrázek vlnek, normálová mapa. Po importu v Unity nastav **Texture Type: Normal map**. Prázdné = vlnky vygenerované hrou. |
| **WaveSize** | po kolika metrech se vlnky opakují |
| **WaveSpeed** | jak rychle se vlnky posouvají |
| **FoamColor** | barva pěny u břehu, alfa (A) = jak je pěna hustá (0 = žádná pěna) |
| **FoamWidth** | jak široký je pruh pěny od břehu do vody (m) |

**Vlastní voda** (třeba z Asset Storu, například IgniteCoders Simple Water Shader):

- **WaterMaterial**: přetáhni materiál vody (u IgniteCoders `Water_mat_01` až `03`). Pak se barva, vlnky
  a jejich posun z katalogu neberou, všechno je v materiálu.
- **WaterUvSize**: kolik metrů odpovídá jednomu opakování textury vody. U IgniteCoders 50 (jejich blok vody
  má 50 m). Když je textura moc velká nebo malá, uprav tohle číslo (nebo „Normal Tiling“ v materiálu).
- **CustomWaterDrop**: o kolik metrů níž se vlastní voda položí. Vlny IgniteCoders jdou nahoru až o 0,3 m
  (u `Water_mat_02` „Displacement“ 0,3), voda by jinak přetekla přes pláž. Výchozí 0,2 m.
- **WaterReflectionCamera**: jen když má materiál zapnuté odrazy (u IgniteCoders „Use Reflection“).
  Pak sem přetáhni prefab `WaterReflectionCamera` (ne `WaterBlock_50m`, to je kus vody). Hra jí sama dosadí
  hladinu. Druhá kamera kreslí scénu podruhé, takže hra poběží pomaleji. S vypnutými odrazy ji nech prázdnou.
- Do **WaterNormalMap** patří normálová mapa vlnek, ne obrázek (ikona). U vlastní vody se nepoužije.
- Prázdné WaterMaterial = voda hry.

Pobřeží není schodovité podle čtverců: rohy dlaždic se zakulatí (jen vzhled, kudy se dá chodit, určuje
server podle dlaždic). Podél břehu leží na hladině pruh pěny, který pomalu sílí a slábne.

## Zprůhledňování

Když strom nebo stavba zakryje postavu, hra je zprůhlední. U vlastních modelů hra použije průhledné
kopie jejich materiálů a pak vrátí původní, textury tedy zůstanou. Zprůhlední se celý model najednou.

## Modely z Meshy (Oohra_Meshy_Prompty.md): kam s nimi

| Soubor | Políčko katalogu |
|---|---|
| `tree_oak_*`, `tree_pine_*` (i smrk), `tree_birch_*`, `tree_willow_*`, `tree_dead_*` | **OakTrees**, **ConiferTrees**, **BirchTrees**, **WillowTrees**, **DeadTrees** (výšku dá hra podle druhu, nemusíš ji ladit) |
| `stump_*` | **Stumps** (víc variant) |
| `trunk_6m` | **Log** |
| `log_1m` | **LogPiece** |
| `sapling_01` | **Saplings** |
| `plant_shape0` až `plant_shape3` | **Plants**, políčka 0 až 3 |
| `grass_tuft_01`, `grass_tuft_02` | **Grass** |
| `rock_stone_01` až `03` | **Rocks** (kameny v hromadách na skalách) |
| `animal_boar`, `animal_wolf`, `animal_rabbit`, `animal_deer` | **Boar**, **Wolf**, **Rabbit**, **Deer** |
| `creature_snake` | **Snake** |
| `creature_wasp_nest` | **WaspNest** |
| `player_ancient`, `player_new`, `player_third` | **PlayerAncient**, **Player**, **PlayerThird** |
| `item_torch` | **Torch** |
| `cave_entrance_a`, `cave_entrance_b` | **CaveEntrance**, **HiddenCaveEntrance** |
| `item_bag_small`, `item_bag_large` | **SmallBag**, **LargeBag** (na zádech, strana k zádům na -Z) |
| `build_wall`, `build_doorframe`, `build_door`, `build_trap`, `build_stockpile`, `build_campfire` | **Wall**, **DoorFrame**, **Door**, **Trap**, **Stockpile**, **Campfire** |
| `deco_bench`, `deco_totem`, `deco_stone_circle`, `deco_drying_rack` | **Decorations** s id `bench`, `totem`, `stone_circle`, `drying_rack` (sušák na maso) |

**Zatím se nepoužijí** (můžeš je mít připravené): `wood_piece`, `splinters` a ostatní věci `item_*` (sekera, nůž, kopí, klacek, kámen, kůže, kost, jehla, lýko, deník, pazourek). Hra věci zatím nekreslí v ruce ani na zemi, jen v batohu jako barevný čtvereček. Dál houba a bylinka (rostliny mají jen 4 tvary), rákos, orobinec a kapradí na ozdobu, balvan, vrstvená skála a drobné kameny (skály jsou dlaždice terénu), vchody do jeskyní (etapa jeskyně), pijavice a klíště (hra je nekreslí, jsou jen v příznacích), netopýr (s jeskyněmi), patra, střechy, žebřík a schody (etapa staveb), chýše cizích kmenů a zříceniny Pradávných (zatím tvary z kódu, políčka přidám, až budou modely).

## Co hlídat

- **Růžový model** = špatný shader. Materiál musí mít „Universal Render Pipeline/Lit“.
- **Počet polygonů:** stromů jsou na ostrově tisíce a rostlin taky. Drž stromy pod asi 1 000
  trojúhelníky a rostliny pod asi 300. Low-poly styl hře sedí.
- **Textury:** stačí malé (256 nebo 512 px).
- Po každé změně je dobré spustit testy (nebo aspoň hru) a podívat se, že model sedí.

## Co přijde dál

Všechno ze seznamu je hotové. Další nápady (až budou potřeba): animované stromy ve větru, tráva
(drobné stébla), mraky a počasí, vlastní textury pro vodu a nebe.

# Vlastní grafika v Oohře

Hra umí místo jednoduchých tvarů (kostky, válce, koule) použít tvoje modely. Které model kde použít,
říká **katalog vzhledu**. Prázdné políčko v katalogu znamená, že hra použije tvar z kódu, takže
modely můžeš přidávat postupně, jeden po druhém.

Zatím jde vyměnit: **stromy, rostliny, zeď, zeď s otvorem, dveře, past, koš se zásobami a zvířata.**
Zem, voda, skály, oheň a postava hráče přijdou v dalších krocích.

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
   - Přetáhni model do scény.
   - Zkontroluj velikost: vedle polož kostku (GameObject → 3D Object → Cube), ta má přesně 1 m.
   - Zkontroluj natočení a střed (viz tabulka níže). Když je střed modelu jinde, vytvoř prázdný
     objekt (GameObject → Create Empty) na správném místě a model dej do něj jako dítě.
   - Přetáhni hotový objekt ze scény do složky `Assets/Prefabs/`. Tím vznikne prefab.
     Ze scény ho pak smaž.
6. **Do katalogu.** Otevři katalog (Oohra → Katalog vzhledu) a prefab přetáhni do políčka.
7. **Vyzkoušej.** Spusť server (Oohra → Spustit server) a hru (Play), nebo sestav hru
   (Oohra → Sestavit hru pro Windows).

Kolize (Collider) v prefabu nevadí, hra je smaže. Kudy se dá projít, počítá server.

## Jak mají být modely velké a natočené

„Střed“ je bod, kde má model v Unity počátek (pivot). „Dole“ znamená na úrovni země.

| Políčko | Velikost | Střed a natočení | Poznámka |
|---|---|---|---|
| **Trees** (stromy) | výška asi 5 m, kmen tlustý asi 25 cm | střed kmene dole | Víc variant = víc políček, každý strom dostane jednu. Hra strom náhodně natočí a trochu zvětší nebo zmenší. Na serveru je kmen kruh o poloměru 12 cm, tlustší kmen by hráč „prošel“. |
| **Plants** (rostliny) | 0,3 až 0,6 m | dole uprostřed | 4 políčka = 4 tvary druhů (0 až 3). Obarví se barvou druhu, aby šly druhy rozeznat (viz níže). |
| **Wall** (zeď) | 1 m dlouhá, 2 m vysoká, 10 cm tlustá | dole uprostřed, délka podél osy X | Kratší zeď (poloviční, dorovnání) hra zmáčkne podél X. |
| **DoorFrame** (zeď s otvorem) | 1 m × 2 m, otvor 0,8 m uprostřed, vysoký 1,9 m | dole uprostřed, délka podél X | Sloupky po krajích jsou po 10 cm. |
| **Door** (dveře) | 0,8 m široké, 1,9 m vysoké, asi 5 cm tlusté | dole uprostřed křídla, šířka podél X | Pant na levém kraji přidá hra sama (dveře se otáčejí kolem něj). |
| **Trap** (past) | asi 0,6 × 0,6 m | dole uprostřed | Úlovek v pasti zatím kreslí hra (hnědá koule). |
| **Stockpile** (koš) | průměr asi 0,6 m, výška 0,45 m | dole uprostřed | |
| **Boar, Rabbit, Deer, Wolf** (zvířata) | divočák 1 m dlouhý, vlk 0,95 m, jelen 1,1 m, králík 0,35 m | nohy dole, hlava ve směru osy Z (dopředu) | Mrtvé zvíře hra položí na bok. |

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

## Zprůhledňování

Když strom nebo stavba zakryje postavu, hra je zprůhlední. U vlastních modelů hra použije průhledné
kopie jejich materiálů a pak vrátí původní, textury tedy zůstanou. Zprůhlední se celý model najednou.

## Co hlídat

- **Růžový model** = špatný shader. Materiál musí mít „Universal Render Pipeline/Lit“.
- **Počet polygonů:** stromů jsou na ostrově tisíce a rostlin taky. Drž stromy pod asi 1 000
  trojúhelníky a rostliny pod asi 300. Low-poly styl hře sedí.
- **Textury:** stačí malé (256 nebo 512 px).
- Po každé změně je dobré spustit testy (nebo aspoň hru) a podívat se, že model sedí.

## Co přijde dál

1. Zem: opravdové textury trávy, písku a skály místo barevných čtverců, 3D kameny na skalách.
2. Voda: hladina s vlnkami.
3. Dekorace ze seznamu v JSON: novou dekoraci půjde přidat jedním řádkem a jedním prefabem.
4. Postava hráče a oheň v katalogu.

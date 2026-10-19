# Provoz serveru Oohra

Návod pro správce testu: jak server spustit, co ukládá, jak ho ovládat a co říct testerům.

## Spuštění
- V Unity: menu **Oohra → Spustit server** (otevře okno serveru).
- Nebo ve složce `server`: `dotnet run --project Oohra.Server`.
- Server čte nastavení ze `server.json` vedle sebe (při `dotnet run` je to `server/Oohra.Server/bin/Debug/net10.0/server.json`, zkopíruje se ze `server/Oohra.Server/server.json` při sestavení).
- Vypnout: napiš do okna `konec` (nebo Ctrl+C). Svět se při vypnutí uloží.

## Soubory vedle serveru

| Soubor | Co v něm je |
|---|---|
| `server.json` | nastavení (port, seed, kódy pozvánek, vyloučení, rychlost času…) |
| `world.json` | uložený svět: hráči, postavy, batohy, stavby, značky, těla, kmeny, čas. Ukládá se každé 2 minuty a při vypnutí |
| `zalohy/svet-RRRRMMDD-HHMM.json` | zálohy světa, jednou za hodinu, posledních 48 |
| `stats.json` | statistiky hraní pro ladění (kdo kolik hraje, na co umírá, co dělá) |
| `reports.jsonl` | nahlášení od hráčů (tlačítko Nahlásit), jeden řádek na nahlášení |
| `blocked_words.json` | sprostá slova, která nesmí být v přezdívce |

- **Nový svět:** přesuň `world.json` jinam (nemaž ho, je to záloha) a spusť server.
- **Návrat k záloze:** vypni server, zkopíruj vybranou zálohu ze složky `zalohy` jako `world.json` a spusť server.
- **Jiný seed:** server uložený svět z jiného seedu nenačte a odmítne start (aby se stavby nepřenesly na jiný ostrov). Vrať seed, nebo přesuň `world.json` jinam.
- Na zkoušení ročního období dej do `server.json` `"startMonth"` (měsíc 0 až 11: 1 = jarní záplava, 10 = zima s hladovými vlky). Platí jen pro nový svět (uložený svět má svůj kalendář). Ve hře 0.
- Pro test s roboty dej do `server.json` `"startBag": true` (nováček má jinak jen dvě ruce a roboti si batoh vyrobit neumí). Před hraním s lidmi vrať `false`.
- Po testu s roboty smaž (nebo přesuň) `stats.json` a `world.json`, ať v nich nejsou roboti.

## Příkazy v okně serveru
Napiš příkaz do okna serveru a stiskni Enter.

| Příkaz | Co udělá |
|---|---|
| `pomoc` | seznam příkazů |
| `hraci` | kdo je ve hře (generace, poloha) |
| `ulozit` | uloží svět hned |
| `oznamit Za 5 minut restartuji server.` | zpráva všem hráčům |
| `vyhodit Petr` | odpojí hráče (může se vrátit) |
| `vyloucit Petr` | vyhodí a už ho nepustí (uloží se se světem) |
| `povolit Petr` | zruší vyloučení |
| `odemknout Petr` | hráč přišel o klíč (jiný počítač): postavu dostane ten, kdo se s přezdívkou přihlásí první |
| `jeskyne Petr` | přenese hráče k nejbližšímu viditelnému vchodu do jeskyně (jen pro zkoušení) |
| `pocasi bourka` | vynutí počasí (`jasno`, `mlha`, `dest`, `bourka`), `pocasi auto` ho zase pustí samo, `pocasi` řekne, jaké je. Jen pro zkoušení, neukládá se. Od startu jde vynutit i v `server.json` (`forceWeather`) |
| `konec` | uloží svět a vypne server |

### Příkazy na zkoušení
Jen pro správce v okně serveru (hráči je napsat nemůžou). Názvy věcí a míst jsou bez háčků a čárek.

| Příkaz | Co udělá |
|---|---|
| `dat Ondra sekera` | dá věc (`dat Ondra kamen 9` víc kusů). Co se nevejde do rukou, položí na zem. Věci: klacek, kamen, pazourek, lyko, kost, kuze, vycinena, popel, drevo, trisky, maso, ryba, sekera, nuz, kopi, jehla, hacek, batoh, velkybatoh, denik |
| `cas 21` | posune čas dopředu na tu hodinu (dnes, nebo zítra, když už byla) |
| `mesic 10` | nastaví měsíc roku 1 až 12 (2 = jarní záplava, 10 až 12 = zima: vlci, hadi spí) |
| `telo Ondra sytost 20` | nastaví sytost, voda, energie, teplo nebo zdravi (0 až 100) |
| `uzdravit Ondra` | plné tělo, bez jedu a pijavic (skryté nemoci zůstanou) |
| `presun Ondra more` | přenese postavu na nejbližší místo: more, bazina, skala (u kamenů), vrchovina, les, louka, voda (u jezírka), tabor (cizinci), ruiny, nebo souřadnice `presun Ondra 120 80` |
| `recepty Ondra` | postava umí všechny recepty (smrtí je zapomene) |
| `ohen Ondra` | rozdělá oheň před postavou, zadarmo |

## Co říct testerům
- **Přihlášení:** kód pozvánky a přezdívka. Přezdívka může být jakákoli, jen ne sprostá.
- **Postava patří počítači:** hra si při prvním spuštění vytvoří tajný klíč a uloží ho na počítači. Na jiném počítači se ke své postavě nedostanou, dokud správce postavu neodemkne (`odemknout`).
- **První přihlášení:** začíná krátkým snem bez slov (mezerník ho přeskočí).
- **Ovládání:** F1 ukáže všechny klávesy.
- **Chyby:** když hra spadne nebo se chová divně, zapíše chybu do souboru `chyby.txt`. Cestu k němu ukáže F1. Ať ho pošlou správci. Problémy ve hře se dají hlásit tlačítkem Nahlásit.
- **Statistiky:** server sbírá statistiky hraní pro ladění (čas ve hře, akce, příčiny smrti). Zprávy ani polohy se neukládají. Testeři o tom mají vědět (návrh 16.4).

## Zátěžový test s roboty
- Server běží, pak ve složce `server`: `dotnet run --project Oohra.Bots -port 9050 -count 20 -seconds 600`.
- Roboti se chovají jako rozumní hráči (pijí z jezírek, rozdělávají oheň, loví, mluví, staví, vstupují do kmenů).
- Výkon vypisuje server, co roboti dělali, ukáže `stats.json`.
- Robota v okně serveru poznáš podle přezdívky Bot01 až Bot20.

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
| `pocasi bourka` | vynutí počasí (`jasno`, `mlha`, `dest`, `bourka`), `pocasi auto` ho zase pustí samo, `pocasi` řekne, jaké je. Jen pro zkoušení, neukládá se. Od startu jde vynutit i v `server.json` (`forceWeather`) |
| `konec` | uloží svět a vypne server |

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

# Oohra – webová stránka (koncept pro Claude ve VS Code)

> Soubor patří do `docs/WEB_KONCEPT.md`. Referenční verze stránky je jeden soubor `web/index.html`. Úkol: rozdělit ho na čistý projekt, který se snadno udržuje, a nasadit ho.

## Rychlý začátek (vlož do Claude Code)

```
Přečti si docs/WEB_KONCEPT.md a web/index.html. Navrhni mi na hodinu práce plán.
Cíl: rozdělit web/index.html na index.html, style.css, app.js a content.js bez změny vzhledu
a chování, potom nasadit na GitHub Pages. Nejdřív mi ukaž plán, potom pracuj po malých krocích.
```

## 1. Cíl stránky

Stránka má v zárodku hry vzbudit zájem. Neukazuje hru, ukazuje **atmosféru a tajemství**. Návštěvník má mít pocit, že vstupuje do něčeho, co se teprve skládá z mlhy. Má také vidět, že vývoj pokračuje (pruh postupu, deník, fragmenty).

Hra v jedné větě: perzistentní souostroví, sběr a otravy skrytým složením rostlin, generace Pradávných a Nových, jazyk ze slabik, znalosti mizí se smrtí.

Účel stránky:
1. Zaujmout (mlha, tajemství, ukázky).
2. Ukázat živý vývoj (postup, zápisy).
3. Sbírat zájemce (odkaz na Discord a ukázku, až budou).

## 2. Co už existuje

`web/index.html` je hotová, samostatná stránka (HTML, CSS a JS v jednom souboru, bez knihoven):

- **Dvojjazyčná** (čeština, angličtina), přepínač jazyka, texty v objektu `I18N`.
- **Mlha**: plátno `#fog` s pixelovou mlhou, která se rozestupuje kolem ukazatele myši (na dotykovém zařízení prstu) a odhaluje „duchy" slabik (`SYL`).
- **Sekce**: hra, generace (Pradávný a Nový), tajemství (znaky a slabiky), postup, deník, fragmenty (obrázky), patička.
- **Mapa postupu**: ostrov 72×36 ve „fog of war", čím víc je hotovo, tím víc je odhaleno (`canvas #map`).
- **Segmentovaný pruh postupu** a seznam fází `PHASES`. Celkový postup se počítá váženě (`overall()`).
- **Deník** z pole `POSTS`, **fragmenty** z `IMAGES` a `SLOTS = 6` (prázdná místa se doplní sama).
- **Konfigurace** `CONFIG = { name, demo, discord }`. Tlačítka Ukázka a Discord se zobrazí, jen když mají adresu.
- **Světlý a tmavý motiv** podle systému (`prefers-color-scheme`) a ruční `data-theme`.

## 3. Vizuální identita (neměnit bez dohody)

| Prvek | Hodnota |
|---|---|
| Nálada | temná, záhadná, mlha, lišejník, stará kronika |
| Písma | Forum (nadpisy), Spectral (text), IBM Plex Mono (štítky a data) |
| Tmavé barvy | pozadí `#080b09`, text `#d6d3c4`, lišejník `#b8c64c`, krev `#a2402c`, mlha `#2b3a33` |
| Světlé barvy | pozadí `#dde1d6`, text `#12170f`, lišejník `#4c5b0c`, krev `#8f2f1d` |
| Rozvržení | asymetrické: úzký sloupec se štítkem a pixelovým znakem vlevo, obsah vpravo, max. šířka 1120 px |
| Pixelové prvky | znaky 12×12, pixelová mlha, ostrov na mapě |

Barvy jsou už definované jako CSS proměnné v `:root`. Nepřidávej nové hodnoty natvrdo.

## 4. Cílová struktura

```
web/
  index.html      (jen kostra a odkazy na style.css, app.js, content.js)
  style.css       (celé CSS včetně proměnných a obou motivů)
  app.js          (logika: mlha, mapa, vykreslení sekcí, přepínač jazyka)
  content.js      (jediné místo, kde se upravuje obsah: CONFIG, PHASES, POSTS, IMAGES, I18N)
  img/            (fragmenty, obrázky do deníku)
  README.md       (jak přidat zápis, obrázek, změnit procenta)
```

Pravidla kódu (stejná jako v CLAUDE.md projektu):
- Názvy v kódu anglicky, komentáře česky.
- Žádný framework, žádný build krok. Jen čisté HTML, CSS a JS.
- Obsah odděl od logiky: úprava textu nebo postupu se dělá výhradně v `content.js`.
- Obrázek `__NIGHT__` (data URI) v referenční verzi přesuň do `img/night.jpg` a odkaž na něj.

## 5. Obsah, který se bude měnit

Všechno v `content.js`:

- **`PHASES`**: pole fází `{ k, w, pct, st, n:{cs,en}, d:{cs,en} }`. `w` je váha (plánované týdny), `pct` je hotovo 0–100, `st` je `done` / `now` / `next`.
  Současný stav: Základ 100 %, Jádro hry (A) 5 %, ostatní 0 %. Celkem 14 %.
- **`POSTS`**: nový zápis se přidává **na začátek** pole. `d` je `RRRR-MM-DD` nebo `RRRR-MM`, `t` a `b` jsou dvojjazyčné, `img` je volitelné.
- **`IMAGES`**: `{ src, cs, en }`. Prázdná místa se doplní podle `SLOTS`.
- **`CONFIG`**: `name`, `demo` (adresa ukázky v prohlížeči), `discord` (pozvánka).

## 6. Úkoly (po jednom, každý jako samostatný commit)

1. Rozděl `index.html` na čtyři soubory bez změny vzhledu a chování. Ověř porovnáním screenshotů před a po.
2. Přesuň obrázek z data URI do `img/` a přidej `README.md` s návodem pro úpravy.
3. Přidej `<meta>` pro sdílení (Open Graph: název, popis, obrázek `img/og.jpg` 1200×630), `lang` podle jazyka a faviconu (malý pixelový znak).
4. Přidej `prefers-reduced-motion`: u uživatelů s omezeným pohybem se mlha nehýbe a duchové se nezobrazují.
5. Zkontroluj přístupnost: kontrast v obou motivech, `alt` u obrázků, ovládání klávesnicí, `aria` u plátna.
6. Nasazení na **GitHub Pages** (složka `web/`, nebo větev `gh-pages`). Doménu zatím neřeš, jen ověř, že stránka běží na adrese `uzivatel.github.io/repo`.
7. Volitelně: formulář nebo odkaz pro přihlášení k testu, RSS nebo jednoduché sdílení zápisů.

## 7. Hotovo, když

- Stránka vypadá a chová se stejně jako referenční verze, na počítači i na telefonu (bez vodorovného posunu, 16 px okraje).
- Úprava postupu, zápisu nebo obrázku vyžaduje změnu jen v `content.js`.
- Přepínač jazyka mění všechny texty (i `title` a meta).
- Světlý i tmavý motiv jsou čitelné.
- Stránka běží na GitHub Pages.

## 8. Na co si dát pozor

- **Nevypadat jako šablona.** Žádné obecné hero sekce, kartičky s ikonami, gradientové tlačítka ani stock fotky. Zachovat asymetrii, mlhu a pixelové znaky.
- **Nic neprozrazovat.** Na webu nikdy neukazuj, co je jedlé nebo jedovaté, ani přesná pravidla složení rostlin. Tajemství je součást hry.
- **Čeština a angličtina musí mít stejný obsah.** Při přidání textu přidej obě verze.
- **Výkon.** Mlha běží s omezením snímků (cca 30 ms) a na slabých zařízeních se má dát vypnout.
- **Licence obrázků.** Používej jen vlastní nebo vygenerované obrázky, u kterých je ověřená licence.
- **Nic nepublikovat bez ověření.** Před nasazením zkontroluj `git status`, že v repozitáři nejsou osobní údaje ani klíče.

## 9. Postup práce (podle dohod s autorem)

- Na začátku sezení: shrň cíl a navrhni plán na hodinu.
- Pracuj po malých krocích, po každém krok ověř a ukaž výsledek.
- Chybu při sestavení vkládá autor celou do chatu.
- Na konci sezení krátké shrnutí ve tvaru „hotovo / příště".
- Commity: větve `feature/...` a pull request, ne přímo do `main` (první commit je výjimka).

# Web Oohra

Statická stránka bez knihoven a bez sestavování: stačí otevřít `index.html` v prohlížeči.

| Soubor | Co v něm je |
|---|---|
| `index.html` | kostra stránky, meta značky pro sdílení |
| `style.css` | vzhled, barvy obou motivů (proměnné v `:root`) |
| `content.js` | **obsah: jediné místo, kde se mění texty, postup, zápisy a obrázky** |
| `app.js` | logika: mlha, mapa postupu, značky, přepínač jazyka |
| `img/` | obrázky (fragmenty, `og.png` pro sdílení, `favicon.svg`) |
| `tools/og.html` | předloha obrázku `img/og.png` |

Koncept a pravidla vzhledu: `docs/WEB_KONCEPT.md`.

## Změnit postup
V `content.js` u fáze v `PHASES` uprav `pct` (hotovo 0 až 100) a `st` (`done` hotovo, `now` teď, `next` dál).
Celkové procento, pruh i odkrytá část mapy se přepočítají samy (`w` je váha fáze, plánované týdny).

## Přidat zápis do deníku
Do `POSTS` v `content.js` přidej **na začátek** seznamu:

```js
{d:'2026-11-02',img:'',t:{cs:'Nadpis',en:'Title'},b:{cs:'Text zápisu.',en:'Entry text.'}},
```

- `d` je datum `RRRR-MM-DD` nebo jen měsíc `RRRR-MM`.
- `img` je volitelný obrázek, třeba `'img/zapis-2026-11.jpg'`.
- Vždy vyplň češtinu i angličtinu.

## Přidat obrázek do fragmentů
Obrázek dej do `img/` a do `IMAGES` v `content.js` přidej `{src:'img/nazev.jpg',cs:'popisek',en:'caption'}`.
Prázdná místa (`SLOTS`) se doplní pixelovými značkami.
Používej jen vlastní obrázky (snímky ze hry). Nikdy neukazuj, co je jedlé nebo jedovaté.

## Ukázka a Discord
V `CONFIG` v `content.js` vyplň `demo` (adresa ukázky) nebo `discord` (pozvánka). Tlačítka se pak ukážou sama.

## Texty
Všechny texty jsou v `I18N` v `content.js` (`cs` a `en`). Nový text přidej do obou jazyků. Titulek a popis pro
vyhledávače a sdílení jsou `meta_title` a `meta_desc` (stejné texty jsou i přímo v `index.html` pro roboty sociálních
sítí, kteří JavaScript nespouštějí).

## Obrázek pro sdílení
`img/og.png` (1200 × 630) se vytvoří z `tools/og.html`. Po úpravě předlohy ve složce `web`:

```
"C:/Program Files/Google/Chrome/Application/chrome.exe" --headless=new --hide-scrollbars --window-size=1200,630 --virtual-time-budget=3000 --screenshot="img/og.png" tools/og.html
```

Po nasazení na internet dej do `index.html` k `og:image` celou adresu (např. `https://…/img/og.png`) a přidej
`<meta property="og:url" content="https://…/">`. Sociální sítě relativní adresu obrázku nenačtou.

## Mlha a přístupnost
- Tlačítko **Mlha** v hlavičce mlhu vypne (pro slabší zařízení), volba se pamatuje.
- Když systém chce omezený pohyb (`prefers-reduced-motion`), mlha se nehýbe a slabiky se neukazují.
- Odkaz „Přeskočit na obsah“ se ukáže po stisku Tab.

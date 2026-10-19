# Zvuky ve hře

Hra má dva druhy zvuků:

- **nahrávky** ze složky `unity/Assets/Sounds`, zařazené v katalogu zvuků (`Assets/Resources/SoundCatalog.asset`);
- **zvuky složené v kódu** (`Ambience`): hrají tam, kde nahrávka chybí (prázdné políčko katalogu).

## Jak přidat nový zvuk

1. Soubor (MP3, WAV nebo OGG) dej do `unity/Assets/Sounds`. Název musí **začínat** slovem z tabulky. Za něj můžeš přidat cokoli, třeba `chopping-wood_02.mp3`.
2. V Unity dej menu **Oohra → Zařadit zvuky ze složky Sounds**. Nástroj:
   - nastaví import (dlouhé smyčky se čtou průběžně z disku, krátké zvuky se načtou celé, zvuky z místa jsou mono);
   - zařadí soubory do katalogu podle názvu.
3. Hlasitost každého druhu jde ladit v katalogu (Inspector, oddíl Hlasitost).

Víc souborů ve stejném políčku: hra pokaždé vybere jeden a trochu změní výšku, ať se zvuk neopakuje jako stroj.

| Začátek názvu | Políčko | Kdy hraje |
|---|---|---|
| `fireplace` | FireLoop | u nejbližšího hořícího ohně (smyčka) |
| `rain-sound` | RainLoop | déšť (smyčka) |
| `thunderstorm` | StormLoop | bouřka místo deště (smyčka). Hrom k bleskům hraje dál kód. |
| `waves` | SeaLoop | moře, sílí do 30 m od pobřeží (smyčka) |
| `walk-on-grass` | StepsGrass | kroky po trávě, v lese, na louce a na kamenité půdě. Hraje i tam, kde jiná nahrávka chybí. |
| `walk-on-sand` | StepsSand | kroky po písku |
| `walk-on-stone` | StepsStone | kroky po skále a v jeskyni |
| `walk-in-mud` | StepsMud | kroky v bažině, brodu a zatopené zemi |
| `walk-on-wood` | StepsWood | kroky v patře stavby |
| `chopping-wood` | Chop | seknutí sekerou (strom, kmen, klad, štípání) |
| `tree-falls` | TreeFall | dopad padajícího stromu |
| `pickaxe-hit` | TakeStone | vzít kámen z hromady nebo pazourek ze skály |
| `rock-break` | StoneWork | výroba z kamene nebo pazourku (nůž, sekera, kopí) |
| `build_wall` | Build | postavená, rozebraná nebo opravená stavba |
| `stomach-growl` | Stomach | hlad (sytost pod 35, jednou za 40 až 100 s), bolení břicha |

Smyčky se na konci prolnou se začátkem (`LoopSound`), takže nahrávka nemusí být dělaná jako smyčka. Stačí, když má aspoň 10 sekund. Delší než 30 až 60 s ale být nemusí, jen zvětšuje hru: oheň má 5 minut, stačilo by ho zkrátit.

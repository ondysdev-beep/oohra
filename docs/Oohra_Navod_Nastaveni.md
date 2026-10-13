# Oohra – Návod: co stáhnout a jak začít

> Pro Windows. Zatím nic nemáš, takže je to od nuly. Všechno níže je zdarma. Ceny a podmínky programů si před stažením zkontroluj na jejich webu, mění se.

---

## 1. Plán v kostce

| Krok | Co | Kdo | Kdy |
|---|---|---|---|
| A | Pořídit notebook s 16 GB RAM (viz předchozí doporučení) | ty | před krokem C |
| B | Dolaďovat logiku v prototypu v prohlížeči | ty + Claude | hned, bez instalace |
| C | Nainstalovat nástroje (část 2 a 3) | ty | až bude počítač |
| D | Založit repozitář na GitHubu (část 4) | ty | hned po C |
| E | Server a pravidla v C# | Claude píše, ty spouštíš | po D |
| F | Unity klient | ty (podle návodu) + Claude | po E |
| G | Test pro 15 až 20 lidí | ty | až bude hotové F |

**Krok B nepotřebuje žádný program.** Prototyp otevřeš v prohlížeči a už teď na něm můžeme ladit hru. Zbytek počká na počítač.

---

## 2. Co stáhnout (kontrolní seznam)

| # | Program | K čemu | Odkud | Povinné? |
|---|---|---|---|---|
| 1 | **Git pro Windows** | Ukládání a sdílení kódu | git-scm.com | ano |
| 2 | **Účet na GitHubu** a **GitHub Desktop** | Úložiště kódu, přes které si předáváme soubory | github.com a desktop.github.com | ano |
| 3 | **.NET SDK 10 (LTS)** | Překlad a spuštění serveru v C# | dotnet.microsoft.com/download | ano |
| 4 | **Visual Studio Community** | Psaní a ladění C#, spolupracuje s Unity | visualstudio.microsoft.com | ano (nebo Rider, VS Code) |
| 5 | **Unity Hub** a **Unity 6 (LTS)** | Herní klient | unity.com/download | ano |
| 6 | **Unity ID** (účet) | Přihlášení do Unity Hubu a Asset Storu | při instalaci Hubu | ano |
| 7 | **Blender** | Úprava modelů, později | blender.org | zatím ne |

Poznámky:
- Visual Studio Community je zdarma pro jednotlivce a malé týmy. Unity Personal je zdarma, dokud příjmy hry nepřekročí limit uvedený na webu Unity.
- Verze se mění. Vždy si stáhni **aktuální LTS (dlouhodobě podporovanou) verzi** Unity 6 a .NET 10.

---

## 3. Instalace krok za krokem

Dodrž pořadí, ušetříš si problémy.

### 3.1 Git
1. Stáhni Git z git-scm.com a nainstaluj.
2. Ve všech obrazovkách nech výchozí hodnoty (stačí klikat Next).

### 3.2 GitHub
1. Zaregistruj se na github.com (zdarma).
2. Stáhni **GitHub Desktop** a přihlas se.

### 3.3 .NET SDK
1. Na dotnet.microsoft.com/download stáhni **.NET SDK** (ne jen „Runtime"), verzi označenou LTS.
2. Po instalaci otevři **Příkazový řádek** (Win + R, napiš `cmd`) a zadej:
   ```
   dotnet --version
   ```
   Mělo by to vypsat číslo verze (začínající 10). Pokud ne, restartuj počítač.

### 3.4 Visual Studio Community
1. Stáhni z visualstudio.microsoft.com a spusť instalátor.
2. V okně „Workloads" zaškrtni:
   - **Game development with Unity** (Vývoj her s Unity)
   - **.NET desktop development** (Vývoj desktopových aplikací v .NET)
3. Nainstaluj.

### 3.5 Unity
1. Stáhni **Unity Hub** z unity.com/download a nainstaluj.
2. Přihlas se nebo si vytvoř Unity ID. Zvol licenci **Personal** (zdarma).
3. V Hubu klikni na **Installs → Install Editor** a vyber **Unity 6 – LTS**.
4. V okně modulů zaškrtni **Windows Build Support (IL2CPP)** a **Documentation** (volitelné).
5. Počkej, instalace trvá, soubory jsou velké (v řádu několika GB).

### 3.6 Ověření
1. V Unity Hubu klikni na **New project**, vyber šablonu **3D (Universal 3D)** nebo **Universal 3D**, pojmenuj `OohraZkouska` a vytvoř.
2. Po otevření stiskni tlačítko **Play**. Pokud se otevře prázdná scéna a nic nespadne, vše funguje.
3. Tento projekt klidně smaž, byl jen na zkoušku.

---

## 4. GitHub: jak si předáváme kód

Kód nebudeme posílat přes chat jako soubory. Použijeme jeden soukromý repozitář.

1. Na github.com klikni na **New repository**.
2. Název: `oohra`. Nastav **Private** (soukromý).
3. Zaškrtni „Add a README".
4. Ve **GitHub Desktop** repozitář naklonuj (**File → Clone repository**) do složky například `C:\Oohra`.
5. Struktura složek, kterou založíme:

   ```
   oohra/
     docs/        dokumenty (Oohra_0.6.md atd.)
     prototype/   webový prototyp (HTML)
     server/      C# server a pravidla
       Oohra.Core/
       Oohra.Server/
       Oohra.Protocol/
       Oohra.TestClient/
     unity/       Unity projekt
   ```
6. Až budeš mít repozitář, napiš mi jeho jméno (např. `tvojejmeno/oohra`). Pokud bude účet propojený s Claudem, připojím ho a budu do něj ukládat kód. Ty ho pak jen stáhneš tlačítkem **Fetch origin / Pull** v GitHub Desktop.

---

## 5. Jak spolu budeme pracovat

1. Ty řekneš, co chceš (např. „přidej pohyb hráčů na server").
2. Já napíšu kód a uložím ho do repozitáře.
3. Ty v GitHub Desktop stáhneš změny (**Pull**).
4. V příkazovém řádku ve složce `server` spustíš:
   ```
   dotnet build
   dotnet test
   ```
   a pošleš mi výpis, pokud něco selže.
5. Pro Unity otevřeš složku `unity` v Unity Hubu (**Add → Add project from disk**).

**Důležité:** v mém pracovním prostředí zatím nemám .NET SDK. Kód v C# proto zatím nepřeložím sám a ověření buildu (`dotnet build`, `dotnet test`) bude na tobě. Zkusím .NET do prostředí doinstalovat, ale nemohu to slíbit. Chyby z buildu mi prostě pošli a opravím je.

---

## 6. Doporučený postup učení (Unity)

Unity se učí pomalu, ale na tvou hru stačí málo. Doporučuju:
1. **Unity Essentials** (learn.unity.com) zdarma, přibližně 10 až 15 hodin. Ukáže scénu, objekty, pohyb a skripty.
2. Jednoduchý projekt: postava, která chodí po terénu, kamera shora.
3. Teprve potom připojení na server.

Ve hře tvoříme jednoduché věci (postava, strom, voda, oheň), takže se nemusíš učit pokročilou grafiku. Modely stáhneš hotové z **Unity Asset Store** (kategorie low poly, zdarma i placené) a upravíš barvu.

---

## 7. Rozpočet (orientačně)

| Položka | Cena |
|---|---|
| Notebook (viz předchozí doporučení) | 12 až 25 tisíc Kč |
| Programy výše | 0 Kč |
| Modely z Asset Store (první balíčky) | 0 až 3 tisíce Kč |
| Pronájem serveru pro test | stovky Kč měsíčně |
| **Celkem do prvního testu** | zhruba do 30 tisíc Kč, včetně notebooku to bude těsné |

Pokud rozpočet 30 tisíc zahrnuje i notebook, vezmi levnější variantu s 16 GB RAM.

---

## 8. Co dělat hned

- [ ] Hrát prototyp v prohlížeči a psát si, co ti nesedí (vyvážení, nudné chvíle, nejasnosti).
- [ ] Rozhodnout, kdy koupíš notebook.
- [ ] Založit účet na GitHubu (zdarma, dá se i bez notebooku).
- [ ] Až bude počítač: části 3 a 4 tohoto návodu.

Do té doby můžeme pokračovat v ladění prototypu, takže se nic nezdrží.

---

## 9. Když něco nejde

- Napiš přesnou chybovou hlášku (nebo pošli snímek obrazovky). Neopisuj ji z paměti.
- Napiš, který krok návodu jsi dělal a jaký máš operační systém.
- Pokud Unity nebo Visual Studio chtějí „restart", udělej ho.

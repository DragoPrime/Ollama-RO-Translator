# Ollama RO Translator — plugin Jellyfin

Plugin pentru Jellyfin care se conectează la un server **Ollama** local (ex. `qwen3:8b`)
și traduce automat în limba română:

- descrierea (`Overview`)
- genurile (`Genres`)
- etichetele (`Tags`)

**Titlurile și restul metadatelor nu sunt niciodată atinse.** După traducere, câmpurile
traduse sunt **blocate** (`LockedFields`), astfel încât refresh-ul automat de metadate
(la tine setat la 30 de zile) să nu le suprascrie cu textul original în engleză.

> ⚠️ Codul din acest repo a fost scris respectând API-ul standard Jellyfin (10.9/10.10),
> dar **nu a fost compilat/testat** într-un mediu real (a fost generat fără acces la
> NuGet/Jellyfin server). E foarte probabil să funcționeze din prima, dar dacă `dotnet build`
> dă erori, trimite-mi mesajul de eroare și le reparăm — sunt de obicei diferențe minore de
> versiune de API între release-urile Jellyfin.

---

## 1. Structura proiectului

```
jellyfin-ollama-translator/
├── build.yaml                     # metadate folosite de jprm (nume, GUID, versiune...)
├── manifest.json                  # registrul de plugin-uri (se completează automat la release)
├── .github/workflows/build.yml    # build + release automat pe GitHub Actions
└── src/Jellyfin.Plugin.OllamaTranslator/
    ├── Jellyfin.Plugin.OllamaTranslator.csproj
    ├── Plugin.cs                  # clasa principală a plugin-ului
    ├── PluginConfiguration.cs     # setările din Dashboard
    ├── OllamaClient.cs            # comunicarea cu Ollama (/api/generate)
    ├── OllamaTranslationTask.cs   # task-ul programat care traduce + blochează câmpuri
    └── Configuration/configPage.html  # pagina de configurare din Dashboard
```

## 2. Pregătește Ollama

Pe serverul unraid, modelul `qwen3:8b` trebuie să fie deja tras:

```bash
ollama pull qwen3:8b
```

Notează adresa IP la care Ollama ascultă (portul implicit e `11434`). Dacă Jellyfin
rulează în alt container decât Ollama, **nu folosi `localhost`** — folosește IP-ul real
al containerului/serverului (sau numele containerului, dacă sunt în aceeași rețea Docker).

## 3. Compilează plugin-ul local (opțional, pentru testare rapidă)

Ai nevoie de [.NET SDK 8](https://dotnet.microsoft.com/download) instalat local:

```bash
cd src/Jellyfin.Plugin.OllamaTranslator
dotnet restore
dotnet build -c Release
```

Verifică mai întâi în `Jellyfin.Plugin.OllamaTranslator.csproj` că versiunea pachetului
`Jellyfin.Controller` corespunde cu versiunea serverului tău Jellyfin (Dashboard > Despre).

Copiază DLL-ul rezultat (`bin/Release/net8.0/Jellyfin.Plugin.OllamaTranslator.dll`) într-un
folder nou, de exemplu `plugins/Ollama RO Translator/`, în directorul de configurare al
serverului Jellyfin (pe unraid, de regulă în `appdata/jellyfin/plugins/`), apoi repornește
Jellyfin. Așa poți testa rapid, fără să publici nimic pe GitHub.

## 4. Publică pe GitHub ca registru de plugin-uri

Scopul tău final — să-l poți adăuga direct din registrul de servere Jellyfin — se obține
astfel:

1. **Creează un repo nou pe GitHub** (public) și încarcă tot conținutul acestui folder.
2. În `build.yaml`, completează `owner:` cu username-ul tău de GitHub.
3. **Creează un tag de versiune** și dă push, de exemplu:

   ```bash
   git tag v1.0.0.0
   git push origin v1.0.0.0
   ```

4. Workflow-ul `.github/workflows/build.yml` pornește automat, face:
   - build cu `dotnet` prin `jprm`
   - creează un **Release** pe GitHub cu arhiva `.zip` a plugin-ului
   - actualizează `manifest.json` din repo cu link-ul către noul release

   > Notă: `jprm` (Jellyfin Plugin Repository Manager) e unealta oficială folosită de
   > comunitatea Jellyfin pentru exact acest flux. Dacă sintaxa exactă a comenzilor
   > `jprm plugin build` / `jprm repo add` diferă puțin față de versiunea instalată în
   > Actions, verifică `jprm --help` sau [documentația jprm](https://github.com/oddstr13/jellyfin-plugin-repository-manager)
   > — workflow-ul e un punct de plecare solid, dar s-ar putea să ceară un mic ajustaj.

5. După ce workflow-ul rulează cu succes, `manifest.json` din `main` va conține automat
   intrarea plugin-ului, cu link către arhiva publicată.

## 5. Adaugă repository-ul în Jellyfin

Pe **fiecare** server Jellyfin de pe unraid:

1. Dashboard → Plugins → Repositories → **Add Repository**
2. Nume: `Ollama RO Translator` (sau ce vrei tu)
3. URL: link-ul RAW către `manifest.json`, de exemplu:
   ```
   https://raw.githubusercontent.com/<user>/<repo>/main/manifest.json
   ```
4. Salvează, apoi mergi la Dashboard → Plugins → Catalog — plugin-ul ar trebui să apară
   acolo, gata de instalat cu un click, la fel ca orice alt plugin din registrul oficial.

## 6. Configurează plugin-ul

După instalare și restart Jellyfin: Dashboard → Plugins → **Ollama RO Translator**:

- **URL server Ollama** — ex. `http://192.168.1.10:11434`
- **Model Ollama** — `qwen3:8b`
- Bifează ce vrei tradus: Overview / Genres / Tags
- Lasă bifat **„Blochează câmpurile după traducere"** (asta e mecanismul care împiedică
  refresh-ul de 30 de zile să suprascrie traducerea)

## 7. Rulează traducerea

Dashboard → Programare sarcini → categoria **Bibliotecă** → **Traducere automată RO (Ollama)**
→ Run acum (▶). Poți urmări progresul live acolo. Rulează implicit și zilnic (poți schimba
orarul din aceeași pagină) — elementele deja traduse sunt sărite rapid, deci rulările
ulterioare procesează doar conținutul nou adăugat în bibliotecă.

## Detecția conținutului deja tradus

Plugin-ul verifică, **per câmp** (Overview / Genres / Tags), înainte de a apela Ollama:

1. **Marcaj intern** — dacă itemul a fost deja procesat de plugin într-o rulare anterioară
   (`ProviderIds["OllamaRoTranslated"]`), este sărit complet, instant.
2. **Câmpuri deja blocate manual** — dacă tu ai blocat deja Overview/Genres/Tags din
   interfața Jellyfin (creion → lacăt) înainte să ruleze plugin-ul, acel câmp e respectat
   ca atare și nu e suprascris.
3. **Detecție de limbă (euristică, fără costuri suplimentare de API)**:
   - **Overview**: dacă textul conține diacritice românești (ă, â, î, ș, ț) sau un raport
     clar în favoarea cuvintelor de legătură românești ("și", "care", "pentru"...) față de
     cele englezești ("the", "and", "with"...), e considerat deja tradus.
   - **Genres**: dacă niciunul dintre genuri nu se potrivește cu lista standard de genuri
     în engleză folosită de TMDB/Jellyfin (Action, Comedy, Drama...), sunt considerate deja
     traduse/netraduse standard și nu sunt atinse.
   - **Tags**: aceeași detecție de diacritice/cuvinte ca la Overview, aplicată pe tot setul
     de etichete.

Dacă **toate** câmpurile activate în configurare sunt deja considerate „traduse" pentru un
item, plugin-ul **nu face niciun apel către Ollama** pentru acel item — doar îl blochează
(dacă opțiunea e activă) și îl marchează ca verificat.

> Detecția e euristică, nu perfectă: descrieri foarte scurte sau fără diacritice pot,
> teoretic, fi interpretate greșit. Dacă observi că un item deja tradus manual e retradus,
> cel mai sigur e să-i blochezi manual câmpurile din Jellyfin înainte de rularea task-ului —
> plugin-ul respectă întotdeauna blocarea manuală, necondiționat.

## Cum funcționează blocarea câmpurilor

Jellyfin ține pentru fiecare item o listă `LockedFields`. Orice câmp aflat în această listă
este **ignorat** de provider-ii de metadate la refresh — inclusiv la refresh-ul periodic
automat pe care l-ai setat la 30 de zile. Plugin-ul adaugă `Overview`, `Genres` și/sau `Tags`
în această listă imediat după ce le traduce, exact ca și cum ai bloca manual acele câmpuri
din interfața Jellyfin (creion → lacăt), doar că automat, pentru tot conținutul.

## Limitări cunoscute

- Traducerea genurilor înseamnă că, temporar (până rulează task-ul pe toată biblioteca),
  poți avea un amestec de genuri în engleză și română în bibliotecă.
- Modelele mici (8B) pot ocazional returna JSON ușor malformat; în acel caz elementul e
  sărit și reîncercat la următoarea rulare (nu e marcat ca „tradus" dacă a eșuat).
- Nu există în acest moment o opțiune de „retradu tot" din UI — pentru retraducere completă,
  șterge manual marcajul intern (`ProviderIds["OllamaRoTranslated"]`) sau simplu dezinstalează
  și reinstalează plugin-ul dacă vrei un reset complet (îți pierzi și restul progresului de
  configurare, deci ai grijă).

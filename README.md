# Local Lead Generation & Web Audit Pipeline (.NET 9 / C# 13)

Soluzione enterprise basata su **Clean / Onion Architecture**, progettata per l'individuazione autonoma, l'audit tecnico (responsive, HTTPS, contatti) e la preparazione di bozze di outreach per PMI locali dell'area **Jonico-Etnea** (Giarre, Riposto, Acireale, Mascali, Fiumefreddo).

L'intero ambiente è pronto all'uso e configurato per l'esecuzione in un **VS Code Docker DevContainer** con Chromium headless e dipendenze Linux preinstallate.

---

## 🏛 Architettura della Soluzione (Clean Architecture)

Il progetto è suddiviso in 4 layer indipendenti e conformi alla Dependency Inversion:

```
LocalLeadGen.sln
│
├── 1. LocalLeadGen.Domain               [Zero dipendenze esterne]
│   ├── Entities/Lead.cs                 (Incapsulamento, factory method, business logic)
│   ├── Enums/LeadStatus.cs              (Discovered, Audited, DraftCreated, SkippedNoContact, Failed)
│   ├── ValueObjects/EmailAddress.cs     (Validazione regex e normalizzazione email)
│   └── Repositories/ILeadRepository.cs  (Contratto di persistenza asincrono)
│
├── 2. LocalLeadGen.Application          [Dipende solo da Domain]
│   ├── DTOs/                            (ScrapedBusinessDto, WebsiteInspectionResult, AuditResultDto)
│   ├── Interfaces/                      (IScraperService, IAuditCopywriterAgent, IEmailDraftService)
│   └── UseCases/                        (ProcessDailyLeadsWorkflow - Orchestratore della pipeline)
│
├── 3. LocalLeadGen.Infrastructure       [Implementazioni concrete]
│   ├── Configuration/                   (OpenRouterOptions, GmailOptions, LeadGenSettings con DataAnnotations)
│   ├── Persistence/                     (AppDbContext EF Core 9 SQLite, LeadRepository con indice composito)
│   ├── Scraping/                        (PlaywrightScraperService headless per Maps e audit mobile 375x667)
│   ├── AI/                              (MicrosoftAgentService basato su Microsoft Agent Framework con AIAgent)
│   ├── Email/                           (GmailDraftService RFC 2822 base64url con blocco opt-out GDPR)
│   └── DependencyInjection.cs           (Extension method IoC con Options Pattern validato)
│
└── 4. LocalLeadGen.Worker               [Entrypoint & Host]
    ├── Worker.cs                        (BackgroundService con IServiceScopeFactory e auto-migrazione DB)
    ├── Program.cs                       (Generic Host .NET 9 + Serilog structured console e rolling file)
    └── appsettings.json                 (Configurazioni strongly-typed)
```

---

## 🤖 Microsoft Agent Framework (MAF)

Il cuore cognitivo del sistema adotta nativamente il nuovo **[Microsoft Agent Framework](https://learn.microsoft.com/it-it/agent-framework/overview/)** (`Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI` e `Microsoft.Agents.AI.Workflows`):

1. **Astrazione `AIAgent`**:
   L'agente viene istanziato tramite l'extension method `chatClient.AsAIAgent(...)` partendo dal client OpenAI connesso all'endpoint OpenAI-compatibile ufficiale di **Google Gemini API** (`https://generativelanguage.googleapis.com/v1beta/openai/`) o **OpenRouter** (`https://openrouter.ai/api/v1`).
2. **Esecuzione Asincrona Reattiva**:
   L'agente esegue il ragionamento e la formulazione del copy personalizzato tramite `agent.RunAsync(...)`, garantendo compatibilità con i pattern moderni di streaming, sessioni, middleware ed esecuzione ad eventi.
3. **Resilienza & Failover Cross-Provider**:
   - **Provider Primario:** Google Gemini API diretta (`gemini-2.0-flash`), ad altissima velocità e zero costi.
   - **Provider Secondario:** OpenRouter (`meta-llama/llama-3.3-70b-instruct:free` con fallback su `google/gemini-2.0-flash-exp:free`).

---

## 🛡 Vincoli Operativi & Sicurezza

1. **Costo Operativo 100% Zero:**
   - LLM: Google Gemini API Free Tier (`gemini-2.0-flash`) e OpenRouter Free Tier (`meta-llama/llama-3.3-70b-instruct:free`).
   - Browser: Microsoft Playwright Chromium headless open-source.
   - Storage: SQLite locale (`data/leads.db`).
   - Email: Gmail API Free Tier.
2. **Human-in-the-Loop (Zero Rischio Spam):**
   - L'agente interagisce con Google APIs **esclusivamente** per creare **Bozze (Drafts)**. Nessun messaggio viene inviato in automatico. La revisione e l'invio finale spettano all'utente dalla cartella *Bozze* di Gmail.
3. **GDPR & Anti-Spam Compliance:**
   - Inclusione automatica in calce a ciascuna bozza del disclaimer di trasparenza e clausola di disiscrizione rapida ("Cancella").
   - Esclusione dei lead privi di indirizzo email aziendale generico (`info@`, `amministrazione@`, `commerciale@`).
4. **Targeting Mirato - Solo Attività Senza Sito Web (`OnlyBusinessesWithoutWebsite`):**
   - La pipeline include un filtro configurabile (`OnlyBusinessesWithoutWebsite: true`) per concentrarsi **esclusivamente** sulle attività locali che non dispongono ancora di un proprio sito web (o hanno solo una pagina social / scheda Maps).
   - Per queste attività, l'agente effettua il discovery automatico dei contatti email pubblici (tramite pagine social o motore di ricerca) e prepara una proposta personalizzata ed empatica evidenziando il valore di una vetrina digitale proprietaria per farsi trovare dai clienti su smartphone.
5. **Rate Limiting Controllato:**
   - 5-10 lead per sessione per preservare la reputazione dell'account e consentire la reale verifica manuale.
   - Deduplicazione automatica: le aziende già contattate negli ultimi 90 giorni vengono scartate a monte.

---

## 🚀 Guida Rapida di Avvio con VS Code DevContainer

### Prerequisiti
- **Docker Desktop** (attivo e funzionante).
- **Visual Studio Code** con estensione **Dev Containers** (`ms-vscode-remote.remote-containers`).

---

### Step 1: Configurazione Chiavi e Credenziali

#### 1. OpenRouter (API Key Gratuita)
1. Registrati su [openrouter.ai](https://openrouter.ai/).
2. Genera una chiave API gratuita nella sezione *Keys*.
3. Inserisci la chiave in `src/LocalLeadGen.Worker/appsettings.json`:
   ```json
   "OpenRouter": {
     "ApiKey": "sk-or-v1-..."
   }
   ```
   *(In alternativa, puoi impostare la variabile d'ambiente `OpenRouter__ApiKey`)*.

#### 2. Google Cloud Console (Gmail Draft API)
1. Accedi alla [Google Cloud Console](https://console.cloud.google.com/) e crea un nuovo progetto (es. `LocalLeadGen`).
2. Vai su **APIs & Services > Enable APIs and Services** e abilita **Gmail API**.
3. Vai su **APIs & Services > OAuth consent screen**:
   - Scegli **External** (Esterno).
   - Inserisci nome app e la tua email.
   - In **Test users**, aggiungi il tuo indirizzo Gmail da cui gestirai le bozze.
4. Vai su **APIs & Services > Credentials > Create Credentials > OAuth client ID**:
   - Application type: **Desktop App**.
   - Nome: `LocalLeadGen Client`.
5. Scarica il file JSON generato, rinominalo in `credentials.json` e posizionalo nella cartella:
   ```
   credentials/credentials.json
   ```

---

### Step 2: Avvio del DevContainer in VS Code

1. Apri la cartella del progetto in VS Code:
   ```bash
   code .
   ```
2. VS Code mostrerà una notifica: *"Folder contains a Dev Container configuration file. Reopen in Container?"* -> Clicca su **Reopen in Container**.
   *(Oppure premi `F1` / `Cmd+Shift+P` e digita `Dev Containers: Reopen in Container`)*.
3. Docker costruirà l'immagine Linux `.NET 9` con tutte le librerie native Chromium e installerà i browser Playwright in automatico durante il `postCreateCommand`.

---

### Step 3: Primo Accesso OAuth 2.0 (Generazione Token)

Al primissimo avvio, la libreria Google richiederà l'autorizzazione OAuth. All'interno del terminale del DevContainer:

```bash
dotnet run --project src/LocalLeadGen.Worker
```

Nel terminale apparirà un messaggio con un URL di autorizzazione Google:
1. Copia l'URL e aprilo nel tuo browser host.
2. Effettua il login con l'account Google impostato come utente di test.
3. Conferma i permessi per la composizione delle bozze (`gmail.compose`).
4. Il token verrà salvato automaticamente nella cartella `credentials/token/` (persistita tramite volume Docker). Nelle esecuzioni successive l'autenticazione sarà istantanea e trasparente.

---

### Step 4: Esecuzione Ordinaria della Pipeline

Una volta completata l'autenticazione, ogni esecuzione della pipeline eseguirà:
1. Ricerca su Google Maps delle attività commerciali a rotazione nei comuni target (Giarre, Riposto, Acireale, ecc.).
2. Deduplicazione su SQLite (`data/leads.db`).
3. Ispezione Playwright su viewport mobile (375x667), verifica scroll orizzontale anomalo, controllo HTTPS ed estrazione email.
4. Generazione del copy empatico e costruttivo via OpenRouter AI.
5. Creazione della bozza in Gmail con footer GDPR.

Per avviare la pipeline:
```bash
dotnet run --project src/LocalLeadGen.Worker
```

Puoi ispezionare il database dei lead direttamente in VS Code aprendo `data/leads.db` con l'estensione **SQLite Viewer** (già inclusa nel DevContainer).

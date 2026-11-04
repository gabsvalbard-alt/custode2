# Custode delle chiavi 2.0 🔐

Applicazione Windows per l'inserimento rapido, sicuro e automatizzato di credenziali, utenze e password.
Sviluppata interamente in **C# su .NET Framework 4.8 nativo**:
- **Zero dipendenze esterne** (nessun runtime .NET Core o DLL esterne da installare).
- **Leggerissima** (eseguibile standalone di appena ~50 KB, consumo RAM < 20 MB).
- **Esecuzione silenziosa in background** nella System Tray (area di notifica di Windows).

---

## 🚀 Caratteristiche Principali

### 1. Crittografia Nativa Trasparente (Zero Reverse Engineering)
- Crittografia con **Windows DPAPI** (`ProtectedData`, ambito `CurrentUser`), combinata con hash e salt di sicurezza.
- La chiave di cifratura è generata e custodita direttamente dal kernel di Windows e legata al SID/account utente e al modulo TPM dell'hardware.
- Nessuna chiave annegata nel codice sorgente: anche decomponendo il file `.exe`, non è possibile estrarre alcuna password o chiave.
- File archivio `vault.dat` salvato automaticamente in `%LocalAppData%\Custode2\` oppure, se posizionato accanto all'eseguibile, in **modalità 100% portabile**.

### 2. Inserimento Rapido tramite Scorciatoie
- **Quick Picker Globale (Stile Spotlight / PowerToys Run)**:
  - Premi `Ctrl + Alt + Spazio` (modificabile) da qualsiasi applicazione.
  - Si apre una barra di ricerca minimale in sovrimpressione con **larghezza adattiva** (compatta e ridotta per titoli brevi, estendibile automaticamente se ci sono voci lunghe), **opacità/trasparenza regolabile**, **colore di sfondo personalizzabile (anche casuale)** e **posizione configurabile** (vicino al cursore del mouse come predefinita o al centro dello schermo).
  - **Pulsante Copia Password**: a fine riga è presente il pulsante `[📋 Copia]` (o scorciatoia `Ctrl+C`). Cliccandolo, la password viene copiata negli appunti e **rimossa automaticamente dopo un numero di secondi impostabile** (default 15 secondi) per garantire massima sicurezza.
  - **Selezione numerica rapida istantanea**: ogni riga ha un indice `[1]`, `[2]`, `[3]`... Premi direttamente il numero corrispondente (es. `2`) per compilare immediatamente la seconda riga senza premere Invio!
  - Digita testo nella casella di ricerca per filtrare istantaneamente per titolo, utente o note.
- **Scorciatoie Dirette per Riga**:
  - È possibile assegnare combinazioni specifiche alle utenze più frequenti (es. `Ctrl + Alt + 1`, `Ctrl + Shift + G`).

### 3. Sequenze Macro Personalizzabili
Ogni credenziale supporta una sequenza con token (valore predefinito: `$USER$<TAB>$PASS$<ENTER>`):
- `$USER$`: Inserisce il nome utente o l'email.
- `$PASS$`: Inserisce la password in modo protetto previa verifica di sicurezza.
- `$TAB$`: Pressione tasto Tabulatore.
- `$ENTER$` o `$INVIO$`: Invio.
- `$SPACE$` o `$SPAZIO$`: Spazio.
- `$ESC$`: Tasto Esc.
- `$DELAY(ms)$`: Pausa in millisecondi (es. `$DELAY(500)$` per form web a più passaggi o connessioni lente).
- Simulazione tramite API nativa Win32 `SendInput` con supporto caratteri Unicode (evita qualsiasi problema di layout o caratteri speciali).

### 4. Organizzazione Impostazioni & Funzioni di Sistema
- **Interfaccia a Schede Tematiche**: Le impostazioni sono suddivise in 4 schede chiare per evitare scrolling verticale:
  - **🔑 Archivio Credenziali**: Gestione rapida, ricerca e prova inserimento.
  - **🎨 Aspetto & Popup**: Scorciatoia globale, posizione (cursore/centro), trasparenza, colori (anche casuale) e timer svuotamento appunti.
  - **⚙️ Macro & Whitelist**: Template predefinito, ritardo tasti e gestione whitelist processi autorizzati.
  - **💻 Sistema & Archivio**:
    - **Avvio Automatico**: Esecuzione all'avvio di Windows (configurato su HKCU, zero privilegi amministrativi richiesti).
    - **Notifica di Avvio**: Possibilità di attivare/disattivare il fumetto di notifica iniziale.
    - **Backup Vault**: Accesso diretto alla cartella del vault e creazione backup al volo.
    - **Termina Processo**: Pulsante dedicato per arrestare e chiudere completamente l'applicazione e la relativa icona nella Tray.

---

## 🛠️ Come Avviare o Compilare

### Esecuzione Diretta
L'eseguibile compilato è subito disponibile in:
```
bin\Custode2.exe
```
Basta fare doppio clic per avviarlo. Comparirà l'icona con la chiave blu nella System Tray vicino all'orologio di Windows.

### Compilazione da Riga di Comando
Per ricompilare il progetto in qualsiasi momento, basta eseguire:
```cmd
build.bat
```
oppure:
```cmd
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe src\Custode2\Custode2.csproj /p:Configuration=Release
```


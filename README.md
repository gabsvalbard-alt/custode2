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
  - Si apre una barra di ricerca minimale in sovrimpressione con **opacità/trasparenza regolabile**, **colore di sfondo personalizzabile (anche casuale)** e **posizione configurabile** (vicino al cursore del mouse come predefinita o al centro dello schermo).
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

### 4. Controllo Sicurezza & Supporto SAP GUI / App Legacy
Prima dell'emissione del token `$PASS$`, Custode 2.0 effettua un controllo multilivello sul campo attivo della finestra target:
1. **Windows UI Automation**: Verifica `AutomationElement.IsPasswordProperty` (ottimale per browser come Chrome, Edge, Firefox e app moderne).
2. **Win32 Control Inspection**: Verifica stili di finestra `ES_PASSWORD` e messaggio `EM_GETPASSWORDCHAR` (per controlli nativi Edit Win32).
3. **Whitelist & Euristica Processi (es. SAP GUI)**:
   - Su applicazioni aziendali come SAP GUI (`saplogon.exe`, `sapgui.exe`) o Desktop Remoto (`mstsc.exe`), dove le API di Windows non espongono la proprietà `IsPassword`, l'applicazione può essere autorizzata.
4. **Popup di Conferma Rapido**: Se il campo attivo non è sicuramente mascherato, compare un avviso con la richiesta di conferma e la casella *"Fidati sempre di questa applicazione"* per non richiederlo più.

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


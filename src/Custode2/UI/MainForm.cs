using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Custode2.Hotkeys;
using Custode2.Models;
using Custode2.Security;

namespace Custode2.UI
{
    public class MainForm : Form
    {
        private readonly VaultData _vault;
        private readonly Action _onVaultChanged;
        private readonly Action _onHotkeysReloadRequested;
        private readonly Action _onExitRequested;

        private TabControl tabControl;

        // Credentials Tab
        private ListView lvCredentials;
        private TextBox txtSearch;
        private Button btnAdd;
        private Button btnEdit;
        private Button btnDelete;
        private Button btnDuplicate;
        private Button btnTestMacro;

        // Tab: Aspetto & Popup
        private TextBox txtPickerHotkey;
        private RadioButton rbPosCursor;
        private RadioButton rbPosCenter;
        private TrackBar tbOpacity;
        private Label lblOpacityVal;
        private Panel pnlColorPreview;
        private Button btnPickColor;
        private Button btnResetColor;
        private CheckBox chkRandomColor;
        private NumericUpDown numClipboardSeconds;
        private NumericUpDown numPickerTimeoutSeconds;

        // Tab: Macro & Whitelist / Blacklist
        private TextBox txtDefaultTemplate;
        private NumericUpDown numInterKeyDelay;
        private ListBox lbWhitelist;
        private TextBox txtNewWhitelistProcess;
        private Button btnAddProcess;
        private Button btnRemoveProcess;
        private ListBox lbBlacklist;
        private TextBox txtNewBlacklistProcess;
        private Button btnAddBlacklistProcess;
        private Button btnRemoveBlacklistProcess;

        // Tab: Sistema & Archivio
        private CheckBox chkStartWithWindows;
        private CheckBox chkStartupNotification;
        private TextBox txtVaultPath;
        private Button btnOpenFolder;
        private Button btnBackupNow;
        private Button btnTerminateProcess;

        public MainForm(VaultData vault, Action onVaultChanged, Action onHotkeysReloadRequested, Action onExitRequested = null)
        {
            _vault = vault ?? new VaultData();
            _onVaultChanged = onVaultChanged;
            _onHotkeysReloadRequested = onHotkeysReloadRequested;
            _onExitRequested = onExitRequested;

            InitializeComponent();
            RefreshCredentialsList();
            LoadSettingsToUI();
        }

        private void InitializeComponent()
        {
            this.Text = "Custode delle chiavi 2.0 - Gestione";
            this.Icon = TrayAppContext.GetAppIcon();
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.ClientSize = new Size(960, 610);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.BackColor = Color.FromArgb(245, 246, 248);

            tabControl = new TabControl { Dock = DockStyle.Fill };

            var tabCreds = new TabPage("🔑 Archivio Credenziali");
            var tabAppearance = new TabPage("🎨 Aspetto & Popup");
            var tabMacro = new TabPage("⚙️ Macro & Whitelist");
            var tabSystem = new TabPage("💻 Sistema & Archivio");

            BuildCredentialsTab(tabCreds);
            BuildAppearanceTab(tabAppearance);
            BuildMacroTab(tabMacro);
            BuildSystemTab(tabSystem);

            tabControl.TabPages.Add(tabCreds);
            tabControl.TabPages.Add(tabAppearance);
            tabControl.TabPages.Add(tabMacro);
            tabControl.TabPages.Add(tabSystem);

            this.Controls.Add(tabControl);

            // Intercept form close so it minimizes to system tray instead of exiting
            this.FormClosing += MainForm_FormClosing;
        }

        private void BuildCredentialsTab(TabPage tab)
        {
            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10) };
            var lblSearch = new Label { Text = "Cerca:", Location = new Point(10, 15), AutoSize = true };
            txtSearch = new TextBox { Location = new Point(60, 12), Width = 340 };
            txtSearch.TextChanged += (s, e) => RefreshCredentialsList();

            pnlTop.Controls.Add(lblSearch);
            pnlTop.Controls.Add(txtSearch);

            lvCredentials = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };
            lvCredentials.Columns.Add("Titolo / Servizio", 220);
            lvCredentials.Columns.Add("Nome Utente / Email", 210);
            lvCredentials.Columns.Add("Scorciatoia", 115);
            lvCredentials.Columns.Add("Filtro App / Sicurezza", 155);
            lvCredentials.Columns.Add("Template Macro", 230);
            lvCredentials.DoubleClick += (s, e) => EditSelected();

            var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 55, Padding = new Padding(10) };

            btnAdd = new Button { Text = "➕ Aggiungi", Location = new Point(10, 10), Size = new Size(105, 32), BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnAdd.FlatAppearance.BorderSize = 0;
            btnAdd.Click += (s, e) => AddCredential();

            btnEdit = new Button { Text = "✏️ Modifica", Location = new Point(120, 10), Size = new Size(95, 32) };
            btnEdit.Click += (s, e) => EditSelected();

            btnDuplicate = new Button { Text = "📄 Duplica", Location = new Point(220, 10), Size = new Size(95, 32) };
            btnDuplicate.Click += (s, e) => DuplicateSelected();

            btnDelete = new Button { Text = "🗑️ Elimina", Location = new Point(320, 10), Size = new Size(95, 32), ForeColor = Color.FromArgb(180, 40, 20) };
            btnDelete.Click += (s, e) => DeleteSelected();

            var btnMirino = new Button
            {
                Text = "🎯 Rileva con Mirino",
                Location = new Point(420, 10),
                Size = new Size(160, 32),
                BackColor = Color.FromArgb(240, 243, 246),
                FlatStyle = FlatStyle.Flat
            };
            btnMirino.Click += (s, e) => DetectProcessWithMirino();

            btnTestMacro = new Button
            {
                Text = "⏱️ Prova Inserimento (3s)",
                Size = new Size(205, 32),
                Location = new Point(735, 10)
            };
            btnTestMacro.Click += (s, e) => TestMacroCountdown();

            pnlBottom.Controls.Add(btnAdd);
            pnlBottom.Controls.Add(btnEdit);
            pnlBottom.Controls.Add(btnDuplicate);
            pnlBottom.Controls.Add(btnDelete);
            pnlBottom.Controls.Add(btnMirino);
            pnlBottom.Controls.Add(btnTestMacro);

            tab.Controls.Add(lvCredentials);
            tab.Controls.Add(pnlTop);
            tab.Controls.Add(pnlBottom);
        }

        private void BuildAppearanceTab(TabPage tab)
        {
            var pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20) };
            int y = 15;

            // Group 1: Scorciatoia Globale
            var gbHotkey = new GroupBox { Text = "Scorciatoia Globale di Richiamo", Location = new Point(15, y), Size = new Size(910, 80) };
            var lblPicker = new Label { Text = "Combinazione di tasti:", Location = new Point(15, 30), AutoSize = true };
            txtPickerHotkey = new TextBox { Location = new Point(165, 27), Width = 260 };
            var lblPickerHint = new Label { Text = "Es. Ctrl+Alt+Space, Win+Shift+C, Ctrl+Alt+C", Location = new Point(440, 30), AutoSize = true, ForeColor = Color.Gray };
            gbHotkey.Controls.Add(lblPicker);
            gbHotkey.Controls.Add(txtPickerHotkey);
            gbHotkey.Controls.Add(lblPickerHint);
            pnl.Controls.Add(gbHotkey);

            // Group 2: Posizione di Comparsa
            y += 95;
            var gbPos = new GroupBox { Text = "Posizione di Comparsa Popup", Location = new Point(15, y), Size = new Size(910, 75) };
            rbPosCursor = new RadioButton { Text = "Posizione cursore del mouse (predefinita)", Location = new Point(20, 30), AutoSize = true, Checked = true };
            rbPosCenter = new RadioButton { Text = "Centro dello schermo", Location = new Point(340, 30), AutoSize = true };
            gbPos.Controls.Add(rbPosCursor);
            gbPos.Controls.Add(rbPosCenter);
            pnl.Controls.Add(gbPos);

            // Group 3: Personalizzazione Visiva (Trasparenza e Colore)
            y += 90;
            var gbStyle = new GroupBox { Text = "Aspetto Grafico & Trasparenza", Location = new Point(15, y), Size = new Size(910, 130) };

            var lblOpacity = new Label { Text = "Livello trasparenza:", Location = new Point(15, 30), AutoSize = true };
            tbOpacity = new TrackBar { Location = new Point(165, 25), Width = 220, Minimum = 30, Maximum = 100, TickFrequency = 10, SmallChange = 5, LargeChange = 10 };
            lblOpacityVal = new Label { Location = new Point(395, 30), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            tbOpacity.ValueChanged += (s, e) => { lblOpacityVal.Text = tbOpacity.Value + "%"; };

            var lblBgColor = new Label { Text = "Colore di sfondo:", Location = new Point(15, 75), AutoSize = true };
            pnlColorPreview = new Panel { Location = new Point(165, 72), Size = new Size(32, 26), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(32, 33, 36) };
            btnPickColor = new Button { Text = "🎨 Scegli Colore...", Location = new Point(205, 72), Width = 130, Height = 28 };
            btnPickColor.Click += (s, e) =>
            {
                using (var cd = new ColorDialog())
                {
                    cd.Color = pnlColorPreview.BackColor;
                    if (cd.ShowDialog(this) == DialogResult.OK)
                    {
                        pnlColorPreview.BackColor = cd.Color;
                    }
                }
            };

            btnResetColor = new Button { Text = "Ripristina Scuro", Location = new Point(345, 72), Width = 115, Height = 28 };
            btnResetColor.Click += (s, e) => { pnlColorPreview.BackColor = Color.FromArgb(32, 33, 36); };

            chkRandomColor = new CheckBox { Text = "🎲 Usa colore casuale ad ogni apertura", Location = new Point(475, 76), AutoSize = true };
            chkRandomColor.CheckedChanged += (s, e) =>
            {
                pnlColorPreview.Enabled = !chkRandomColor.Checked;
                btnPickColor.Enabled = !chkRandomColor.Checked;
                btnResetColor.Enabled = !chkRandomColor.Checked;
            };

            gbStyle.Controls.Add(lblOpacity);
            gbStyle.Controls.Add(tbOpacity);
            gbStyle.Controls.Add(lblOpacityVal);
            gbStyle.Controls.Add(lblBgColor);
            gbStyle.Controls.Add(pnlColorPreview);
            gbStyle.Controls.Add(btnPickColor);
            gbStyle.Controls.Add(btnResetColor);
            gbStyle.Controls.Add(chkRandomColor);
            pnl.Controls.Add(gbStyle);

            // Group 4: Sicurezza Clipboard
            y += 145;
            var gbClipboard = new GroupBox { Text = "Sicurezza Appunti (Copia Password)", Location = new Point(15, y), Size = new Size(910, 75) };
            var lblClipboard = new Label { Text = "Svuota appunti dopo:", Location = new Point(15, 28), AutoSize = true };
            numClipboardSeconds = new NumericUpDown { Location = new Point(165, 26), Width = 70, Minimum = 3, Maximum = 120, Value = 15 };
            var lblSec = new Label { Text = "secondi", Location = new Point(245, 28), AutoSize = true };
            var lblClipboardHint = new Label { Text = "(cancella automaticamente la password dalla memoria per evitare accessi indesiderati)", Location = new Point(315, 28), AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8.5f) };

            gbClipboard.Controls.Add(lblClipboard);
            gbClipboard.Controls.Add(numClipboardSeconds);
            gbClipboard.Controls.Add(lblSec);
            gbClipboard.Controls.Add(lblClipboardHint);
            pnl.Controls.Add(gbClipboard);

            // Group 5: Timeout Chiusura Automatica Popup
            y += 90;
            var gbTimeout = new GroupBox { Text = "Timeout Chiusura Automatica Popup", Location = new Point(15, y), Size = new Size(910, 75) };
            var lblTimeout = new Label { Text = "Chiudi popup dopo:", Location = new Point(15, 28), AutoSize = true };
            numPickerTimeoutSeconds = new NumericUpDown { Location = new Point(165, 26), Width = 70, Minimum = 5, Maximum = 300, Value = 30 };
            var lblTimeoutSec = new Label { Text = "secondi", Location = new Point(245, 28), AutoSize = true };
            var lblTimeoutHint = new Label { Text = "(chiude automaticamente il popup in assenza di scelte dell'utente per sicurezza, default: 30s)", Location = new Point(315, 28), AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8.5f) };

            gbTimeout.Controls.Add(lblTimeout);
            gbTimeout.Controls.Add(numPickerTimeoutSeconds);
            gbTimeout.Controls.Add(lblTimeoutSec);
            gbTimeout.Controls.Add(lblTimeoutHint);
            pnl.Controls.Add(gbTimeout);

            // Pulsante Salva Impostazioni in fondo alla scheda
            y += 90;
            var btnSave = CreateSaveButton(y);
            pnl.Controls.Add(btnSave);

            tab.Controls.Add(pnl);
        }

        private void BuildMacroTab(TabPage tab)
        {
            var pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20) };
            int y = 15;

            // Group 1: Parametri Macro Predefiniti
            var gbMacro = new GroupBox { Text = "Parametri di Digitazione Macro", Location = new Point(15, y), Size = new Size(910, 125) };

            var lblDefTemplate = new Label { Text = "Template predefinito nuove voci:", Location = new Point(15, 30), AutoSize = true };
            txtDefaultTemplate = new TextBox { Location = new Point(240, 27), Width = 360 };
            var lblTmplHint = new Label { Text = "Token supportati: $USER$, <TAB>, $PASS$, <ENTER>, $DELAY(ms)$", Location = new Point(240, 52), AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8.5f) };

            var lblDelay = new Label { Text = "Ritardo tra caratteri (ms):", Location = new Point(15, 80), AutoSize = true };
            numInterKeyDelay = new NumericUpDown { Location = new Point(240, 78), Width = 80, Minimum = 0, Maximum = 200, Value = 15 };
            var lblDelayHint = new Label { Text = "(predefinito: 15 ms, garantisce ricezione corretta su form web e desktop)", Location = new Point(335, 80), AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8.5f) };

            gbMacro.Controls.Add(lblDefTemplate);
            gbMacro.Controls.Add(txtDefaultTemplate);
            gbMacro.Controls.Add(lblTmplHint);
            gbMacro.Controls.Add(lblDelay);
            gbMacro.Controls.Add(numInterKeyDelay);
            gbMacro.Controls.Add(lblDelayHint);
            pnl.Controls.Add(gbMacro);

            // Group 2: Processi Autorizzati (Whitelist) e Blacklist di Sicurezza
            y += 140;
            var gbWhitelist = new GroupBox { Text = "✅ Whitelist (Bypass Verifica Password)", Location = new Point(15, y), Size = new Size(445, 225) };

            var lblWhitelistSub = new Label { Text = "Bypass per app aziendali fidate (es. SAP GUI, RDP):", Location = new Point(15, 25), AutoSize = true, ForeColor = Color.DarkSlateGray, Font = new Font("Segoe UI", 8.5f) };
            lbWhitelist = new ListBox { Location = new Point(15, 52), Width = 245, Height = 150 };

            txtNewWhitelistProcess = new TextBox { Location = new Point(270, 52), Width = 115 };
            var btnMirinoWhitelist = new Button { Text = "🎯", Location = new Point(390, 50), Width = 40, Height = 28 };
            btnMirinoWhitelist.Click += (s, e) =>
            {
                this.WindowState = FormWindowState.Minimized;
                ProcessPickerHelper.StartPicking(this, result =>
                {
                    this.WindowState = FormWindowState.Normal;
                    this.BringToFront();
                    this.Activate();
                    if (result != null && !string.IsNullOrEmpty(result.ProcessName))
                    {
                        txtNewWhitelistProcess.Text = result.ProcessName;
                    }
                });
            };

            var lblProcessHint = new Label { Text = "Es. saplogon.exe, mstsc.exe", Location = new Point(270, 78), AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8f) };

            btnAddProcess = new Button { Text = "➕ Aggiungi", Location = new Point(270, 105), Width = 160, Height = 30 };
            btnAddProcess.Click += (s, e) => AddWhitelistProcess();

            btnRemoveProcess = new Button { Text = "🗑️ Rimuovi", Location = new Point(270, 145), Width = 160, Height = 30 };
            btnRemoveProcess.Click += (s, e) => RemoveWhitelistProcess();

            gbWhitelist.Controls.Add(lblWhitelistSub);
            gbWhitelist.Controls.Add(lbWhitelist);
            gbWhitelist.Controls.Add(txtNewWhitelistProcess);
            gbWhitelist.Controls.Add(btnMirinoWhitelist);
            gbWhitelist.Controls.Add(lblProcessHint);
            gbWhitelist.Controls.Add(btnAddProcess);
            gbWhitelist.Controls.Add(btnRemoveProcess);
            pnl.Controls.Add(gbWhitelist);

            // Group 3: Blacklist (Blocco Totale Inserimento)
            var gbBlacklist = new GroupBox { Text = "🚫 Blacklist (Blocco Totale Inserimento)", Location = new Point(480, y), Size = new Size(445, 225) };

            var lblBlacklistSub = new Label { Text = "Inserimento sempre bloccato (es. Teams, Slack, Discord):", Location = new Point(15, 25), AutoSize = true, ForeColor = Color.FromArgb(180, 40, 20), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            lbBlacklist = new ListBox { Location = new Point(15, 52), Width = 245, Height = 150 };

            txtNewBlacklistProcess = new TextBox { Location = new Point(270, 52), Width = 115 };
            var btnMirinoBlacklist = new Button { Text = "🎯", Location = new Point(390, 50), Width = 40, Height = 28 };
            btnMirinoBlacklist.Click += (s, e) =>
            {
                this.WindowState = FormWindowState.Minimized;
                ProcessPickerHelper.StartPicking(this, result =>
                {
                    this.WindowState = FormWindowState.Normal;
                    this.BringToFront();
                    this.Activate();
                    if (result != null && !string.IsNullOrEmpty(result.ProcessName))
                    {
                        txtNewBlacklistProcess.Text = result.ProcessName;
                    }
                });
            };

            var lblBlacklistHint = new Label { Text = "Es. teams.exe, slack.exe", Location = new Point(270, 78), AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8f) };

            btnAddBlacklistProcess = new Button { Text = "➕ Aggiungi", Location = new Point(270, 105), Width = 160, Height = 30 };
            btnAddBlacklistProcess.Click += (s, e) => AddBlacklistProcess();

            btnRemoveBlacklistProcess = new Button { Text = "🗑️ Rimuovi", Location = new Point(270, 145), Width = 160, Height = 30 };
            btnRemoveBlacklistProcess.Click += (s, e) => RemoveBlacklistProcess();

            gbBlacklist.Controls.Add(lblBlacklistSub);
            gbBlacklist.Controls.Add(lbBlacklist);
            gbBlacklist.Controls.Add(txtNewBlacklistProcess);
            gbBlacklist.Controls.Add(btnMirinoBlacklist);
            gbBlacklist.Controls.Add(lblBlacklistHint);
            gbBlacklist.Controls.Add(btnAddBlacklistProcess);
            gbBlacklist.Controls.Add(btnRemoveBlacklistProcess);
            pnl.Controls.Add(gbBlacklist);

            // Pulsante Salva Impostazioni
            y += 245;
            var btnSave = CreateSaveButton(y);
            pnl.Controls.Add(btnSave);

            tab.Controls.Add(pnl);
        }

        private void BuildSystemTab(TabPage tab)
        {
            var pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20) };
            int y = 15;

            // Group 1: Opzioni di Avvio
            var gbStartup = new GroupBox { Text = "Opzioni di Avvio e Notifiche", Location = new Point(15, y), Size = new Size(910, 95) };
            chkStartWithWindows = new CheckBox { Text = "Esegui automaticamente Custode delle chiavi all'avvio di Windows", Location = new Point(20, 26), AutoSize = true };
            chkStartupNotification = new CheckBox { Text = "Mostra notifica a fumetto all'avvio dell'applicazione", Location = new Point(20, 56), AutoSize = true };
            gbStartup.Controls.Add(chkStartWithWindows);
            gbStartup.Controls.Add(chkStartupNotification);
            pnl.Controls.Add(gbStartup);

            // Group 2: Archivio Crittografato DPAPI
            y += 110;
            var gbStorage = new GroupBox { Text = "Archivio Crittografato (Windows DPAPI)", Location = new Point(15, y), Size = new Size(910, 110) };
            var lblPath = new Label { Text = "Posizione file vault:", Location = new Point(15, 25), AutoSize = true };
            txtVaultPath = new TextBox { ReadOnly = true, Text = DpapiStorage.GetVaultFilePath(), Location = new Point(15, 45), Width = 690, BackColor = Color.White };

            btnOpenFolder = new Button { Text = "📁 Apri Cartella Vault", Location = new Point(715, 43), Width = 180, Height = 28 };
            btnOpenFolder.Click += (s, e) =>
            {
                string dir = Path.GetDirectoryName(DpapiStorage.GetVaultFilePath());
                if (Directory.Exists(dir)) Process.Start("explorer.exe", dir);
            };

            btnBackupNow = new Button { Text = "💾 Crea Copia di Backup Ora", Location = new Point(15, 75), Width = 210, Height = 28 };
            btnBackupNow.Click += (s, e) =>
            {
                try
                {
                    string path = DpapiStorage.GetVaultFilePath();
                    if (File.Exists(path))
                    {
                        string bkp = path + "." + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak";
                        File.Copy(path, bkp, true);
                        MessageBox.Show(string.Format("Backup salvato con successo:\n{0}", bkp), "Backup Vault", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Errore durante il backup: " + ex.Message, "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            gbStorage.Controls.Add(lblPath);
            gbStorage.Controls.Add(txtVaultPath);
            gbStorage.Controls.Add(btnOpenFolder);
            gbStorage.Controls.Add(btnBackupNow);
            pnl.Controls.Add(gbStorage);

            // Group 3: Chiusura & Arresto Processo
            y += 125;
            var gbExit = new GroupBox { Text = "Chiusura & Arresto Applicazione", Location = new Point(15, y), Size = new Size(880, 95) };
            var lblExitDesc = new Label
            {
                Text = "Nota: il pulsante 'X' della finestra riduce Custode 2.0 nella System Tray (vicino all'orologio).\nPer chiudere completamente l'applicazione e arrestare il processo:",
                Location = new Point(15, 22),
                AutoSize = true,
                ForeColor = Color.DarkSlateGray
            };

            btnTerminateProcess = new Button
            {
                Text = "🛑 Termina Processo (Chiudi del tutto)",
                Location = new Point(15, 55),
                Width = 260,
                Height = 32,
                BackColor = Color.FromArgb(195, 40, 30),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            btnTerminateProcess.FlatAppearance.BorderSize = 0;
            btnTerminateProcess.Click += BtnTerminateProcess_Click;

            gbExit.Controls.Add(lblExitDesc);
            gbExit.Controls.Add(btnTerminateProcess);
            pnl.Controls.Add(gbExit);

            // Pulsante Salva Impostazioni
            y += 115;
            var btnSave = CreateSaveButton(y);
            pnl.Controls.Add(btnSave);

            tab.Controls.Add(pnl);
        }

        private Button CreateSaveButton(int yPosition)
        {
            var btn = new Button
            {
                Text = "💾 Salva Tutte le Impostazioni",
                Location = new Point(15, yPosition),
                Width = 230,
                Height = 35,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) => SaveSettingsFromUI();
            return btn;
        }

        private void BtnTerminateProcess_Click(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show(
                "Sei sicuro di voler chiudere completamente Custode delle chiavi 2.0?\n\nL'applicazione non sarà più attiva in background e le scorciatoie non funzioneranno finché non verrà riavviata.",
                "Conferma Chiusura Custode 2.0",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm == DialogResult.Yes)
            {
                if (_onExitRequested != null)
                {
                    _onExitRequested();
                }
                else
                {
                    Application.Exit();
                }
            }
        }

        private void RefreshCredentialsList()
        {
            string query = txtSearch != null && txtSearch.Text != null ? txtSearch.Text.Trim() : string.Empty;
            lvCredentials.BeginUpdate();
            lvCredentials.Items.Clear();

            var list = string.IsNullOrEmpty(query)
                ? _vault.Entries
                : _vault.Entries.Where(e => (e.Title != null && e.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                            (e.Username != null && e.Username.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                            (e.Notes != null && e.Notes.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();

            foreach (var entry in list)
            {
                var lvi = new ListViewItem(entry.Title ?? string.Empty);
                lvi.SubItems.Add(entry.Username ?? string.Empty);
                lvi.SubItems.Add(entry.DirectHotkey ?? string.Empty);
                lvi.SubItems.Add(entry.AppFilter ?? (entry.BypassSecurityCheck ? "[Fidato]" : string.Empty));
                lvi.SubItems.Add(entry.Template ?? string.Empty);
                lvi.Tag = entry;
                lvCredentials.Items.Add(lvi);
            }

            lvCredentials.EndUpdate();
        }

        private void LoadSettingsToUI()
        {
            var s = _vault.Settings;

            // Hotkey & Template
            txtPickerHotkey.Text = s.GlobalPickerHotkey;
            txtDefaultTemplate.Text = s.DefaultTemplate;
            numInterKeyDelay.Value = Math.Max(0, Math.Min(200, s.InterKeyDelayMs));

            // Visual Style
            int op = s.PickerOpacity > 0 ? s.PickerOpacity : 95;
            tbOpacity.Value = Math.Max(30, Math.Min(100, op));
            lblOpacityVal.Text = tbOpacity.Value + "%";

            try
            {
                pnlColorPreview.BackColor = ColorTranslator.FromHtml(!string.IsNullOrEmpty(s.PickerBackgroundColor) ? s.PickerBackgroundColor : "#202124");
            }
            catch
            {
                pnlColorPreview.BackColor = Color.FromArgb(32, 33, 36);
            }

            chkRandomColor.Checked = s.PickerRandomColor;
            pnlColorPreview.Enabled = !s.PickerRandomColor;
            btnPickColor.Enabled = !s.PickerRandomColor;
            btnResetColor.Enabled = !s.PickerRandomColor;

            // Position mode
            if (string.Equals(s.PickerPositionMode, "CenterScreen", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.PickerPositionMode, "Center", StringComparison.OrdinalIgnoreCase))
            {
                rbPosCenter.Checked = true;
            }
            else
            {
                rbPosCursor.Checked = true;
            }

            // Clipboard
            int clipSec = s.ClipboardClearSeconds > 0 ? s.ClipboardClearSeconds : 15;
            numClipboardSeconds.Value = Math.Max(3, Math.Min(120, clipSec));

            // Auto-close Timeout
            int timeoutSec = s.PickerTimeoutSeconds > 0 ? s.PickerTimeoutSeconds : 30;
            numPickerTimeoutSeconds.Value = Math.Max(5, Math.Min(300, timeoutSec));

            // Startup & Notification
            chkStartWithWindows.Checked = StartupHelper.IsStartupEnabled();
            chkStartupNotification.Checked = s.ShowStartupNotification;

            // Whitelist
            lbWhitelist.Items.Clear();
            if (s.WhitelistedProcesses != null)
            {
                foreach (var p in s.WhitelistedProcesses)
                {
                    lbWhitelist.Items.Add(p);
                }
            }

            // Blacklist
            lbBlacklist.Items.Clear();
            if (s.BlacklistedProcesses != null)
            {
                foreach (var p in s.BlacklistedProcesses)
                {
                    lbBlacklist.Items.Add(p);
                }
            }
        }

        private void SaveSettingsFromUI()
        {
            var s = _vault.Settings;

            s.GlobalPickerHotkey = txtPickerHotkey.Text.Trim();
            s.DefaultTemplate = txtDefaultTemplate.Text.Trim();
            s.InterKeyDelayMs = (int)numInterKeyDelay.Value;

            s.PickerOpacity = tbOpacity.Value;
            s.PickerBackgroundColor = ColorTranslator.ToHtml(pnlColorPreview.BackColor);
            s.PickerRandomColor = chkRandomColor.Checked;
            s.PickerPositionMode = rbPosCenter.Checked ? "CenterScreen" : "Cursor";
            s.ClipboardClearSeconds = (int)numClipboardSeconds.Value;
            s.PickerTimeoutSeconds = (int)numPickerTimeoutSeconds.Value;

            s.StartWithWindows = chkStartWithWindows.Checked;
            StartupHelper.SetStartupEnabled(chkStartWithWindows.Checked);

            s.ShowStartupNotification = chkStartupNotification.Checked;

            if (_onVaultChanged != null) _onVaultChanged();
            if (_onHotkeysReloadRequested != null) _onHotkeysReloadRequested();

            MessageBox.Show("Impostazioni salvate con successo!", "Custode 2.0", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void AddWhitelistProcess()
        {
            string proc = txtNewWhitelistProcess.Text.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(proc)) return;
            if (!proc.EndsWith(".exe")) proc += ".exe";

            if (!_vault.Settings.WhitelistedProcesses.Contains(proc))
            {
                _vault.Settings.WhitelistedProcesses.Add(proc);
                lbWhitelist.Items.Add(proc);
                txtNewWhitelistProcess.Clear();
                if (_onVaultChanged != null) _onVaultChanged();
            }
        }

        private void RemoveWhitelistProcess()
        {
            if (lbWhitelist.SelectedItem != null)
            {
                string proc = lbWhitelist.SelectedItem.ToString();
                _vault.Settings.WhitelistedProcesses.Remove(proc);
                lbWhitelist.Items.Remove(proc);
                if (_onVaultChanged != null) _onVaultChanged();
            }
        }

        private void AddBlacklistProcess()
        {
            string proc = txtNewBlacklistProcess.Text.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(proc)) return;
            if (!proc.EndsWith(".exe")) proc += ".exe";

            if (_vault.Settings.BlacklistedProcesses == null)
            {
                _vault.Settings.BlacklistedProcesses = new System.Collections.Generic.List<string>();
            }

            if (!_vault.Settings.BlacklistedProcesses.Contains(proc))
            {
                _vault.Settings.BlacklistedProcesses.Add(proc);
                lbBlacklist.Items.Add(proc);
                txtNewBlacklistProcess.Clear();
                if (_onVaultChanged != null) _onVaultChanged();
            }
        }

        private void RemoveBlacklistProcess()
        {
            if (lbBlacklist.SelectedItem != null)
            {
                string proc = lbBlacklist.SelectedItem.ToString();
                if (_vault.Settings.BlacklistedProcesses != null)
                {
                    _vault.Settings.BlacklistedProcesses.Remove(proc);
                }
                lbBlacklist.Items.Remove(proc);
                if (_onVaultChanged != null) _onVaultChanged();
            }
        }

        private void AddCredential()
        {
            using (var dlg = new EditCredentialDialog())
            {
                dlg.Entry.Template = _vault.Settings.DefaultTemplate;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _vault.Entries.Add(dlg.Entry);
                    if (_onVaultChanged != null) _onVaultChanged();
                    if (_onHotkeysReloadRequested != null) _onHotkeysReloadRequested();
                    RefreshCredentialsList();
                }
            }
        }

        private void DetectProcessWithMirino()
        {
            this.WindowState = FormWindowState.Minimized;
            ProcessPickerHelper.StartPicking(this, result =>
            {
                this.WindowState = FormWindowState.Normal;
                this.BringToFront();
                this.Activate();

                if (result != null && !string.IsNullOrEmpty(result.ProcessName))
                {
                    string info = string.Format("🎯 Applicazione Rilevata:\n\n• Processo: {0}\n• Titolo Finestra: {1}\n\nVuoi creare una nuova credenziale preimpostata con questo processo?",
                        result.ProcessName, string.IsNullOrEmpty(result.WindowTitle) ? "-" : result.WindowTitle);

                    var answer = MessageBox.Show(info, "Custode 2.0 - Mirino Applicazione", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (answer == DialogResult.Yes)
                    {
                        var entry = new CredentialEntry
                        {
                            AppFilter = result.ProcessName,
                            Title = !string.IsNullOrEmpty(result.WindowTitle) ? result.WindowTitle : result.ProcessName,
                            Template = _vault.Settings.DefaultTemplate
                        };
                        using (var dlg = new EditCredentialDialog(entry))
                        {
                            if (dlg.ShowDialog(this) == DialogResult.OK)
                            {
                                _vault.Entries.Add(dlg.Entry);
                                if (_onVaultChanged != null) _onVaultChanged();
                                if (_onHotkeysReloadRequested != null) _onHotkeysReloadRequested();
                                RefreshCredentialsList();
                            }
                        }
                    }
                    else if (answer == DialogResult.No)
                    {
                        Clipboard.SetText(result.ProcessName);
                        MessageBox.Show(string.Format("Nome processo '{0}' copiato negli appunti!", result.ProcessName), "Copiato negli Appunti", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            });
        }

        private void EditSelected()
        {
            if (lvCredentials.SelectedItems.Count == 0) return;
            var entry = lvCredentials.SelectedItems[0].Tag as CredentialEntry;
            if (entry == null) return;

            using (var dlg = new EditCredentialDialog(entry))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (_onVaultChanged != null) _onVaultChanged();
                    if (_onHotkeysReloadRequested != null) _onHotkeysReloadRequested();
                    RefreshCredentialsList();
                }
            }
        }

        private void DuplicateSelected()
        {
            if (lvCredentials.SelectedItems.Count == 0) return;
            var entry = lvCredentials.SelectedItems[0].Tag as CredentialEntry;
            if (entry == null) return;

            var copy = new CredentialEntry
            {
                Title = entry.Title + " (Copia)",
                Username = entry.Username,
                Password = entry.Password,
                Template = entry.Template,
                DirectHotkey = string.Empty, // Avoid hotkey collision
                AppFilter = entry.AppFilter,
                BypassSecurityCheck = entry.BypassSecurityCheck,
                Notes = entry.Notes
            };

            _vault.Entries.Add(copy);
            if (_onVaultChanged != null) _onVaultChanged();
            RefreshCredentialsList();
        }

        private void DeleteSelected()
        {
            if (lvCredentials.SelectedItems.Count == 0) return;
            var entry = lvCredentials.SelectedItems[0].Tag as CredentialEntry;
            if (entry == null) return;

            var confirm = MessageBox.Show(string.Format("Sei sicuro di voler eliminare la credenziale '{0}'?", entry.Title), "Conferma eliminazione", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm == DialogResult.Yes)
            {
                _vault.Entries.Remove(entry);
                if (_onVaultChanged != null) _onVaultChanged();
                if (_onHotkeysReloadRequested != null) _onHotkeysReloadRequested();
                RefreshCredentialsList();
            }
        }

        private void TestMacroCountdown()
        {
            if (lvCredentials.SelectedItems.Count == 0)
            {
                MessageBox.Show("Seleziona una credenziale dall'elenco da testare.", "Attenzione", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var entry = lvCredentials.SelectedItems[0].Tag as CredentialEntry;
            if (entry == null) return;

            MessageBox.Show("Dopo aver premuto OK, avrai 3 secondi per cliccare e posizionare il cursore nel campo desiderato.", "Test Inserimento", MessageBoxButtons.OK, MessageBoxIcon.Information);

            var timer = new System.Windows.Forms.Timer { Interval = 3000 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                IntPtr target = Macro.NativeMethods.GetForegroundWindow();
                Macro.MacroEngine.Execute(entry, target, _vault.Settings, _onVaultChanged);
            };
            timer.Start();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // If user clicked the X button, minimize to System Tray instead of quitting
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
            }
        }
    }
}

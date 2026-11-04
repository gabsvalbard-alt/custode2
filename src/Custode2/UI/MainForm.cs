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

        private TabControl tabControl;
        private ListView lvCredentials;
        private TextBox txtSearch;
        private Button btnAdd;
        private Button btnEdit;
        private Button btnDelete;
        private Button btnDuplicate;
        private Button btnTestMacro;

        // Settings tab controls
        private TextBox txtPickerHotkey;
        private TextBox txtDefaultTemplate;
        private NumericUpDown numInterKeyDelay;
        private TrackBar tbOpacity;
        private Label lblOpacityVal;
        private Panel pnlColorPreview;
        private Button btnPickColor;
        private Button btnResetColor;
        private CheckBox chkRandomColor;
        private RadioButton rbPosCursor;
        private RadioButton rbPosCenter;
        private ListBox lbWhitelist;
        private TextBox txtNewWhitelistProcess;
        private Button btnAddProcess;
        private Button btnRemoveProcess;
        private TextBox txtVaultPath;
        private Button btnOpenFolder;
        private Button btnBackupNow;

        public MainForm(VaultData vault, Action onVaultChanged, Action onHotkeysReloadRequested)
        {
            _vault = vault ?? new VaultData();
            _onVaultChanged = onVaultChanged;
            _onHotkeysReloadRequested = onHotkeysReloadRequested;

            InitializeComponent();
            RefreshCredentialsList();
            LoadSettingsToUI();
        }

        private void InitializeComponent()
        {
            this.Text = "Custode delle chiavi 2.0 - Gestione";
            this.Size = new Size(950, 640);
            this.MinimumSize = new Size(880, 560);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.BackColor = Color.FromArgb(245, 246, 248);

            tabControl = new TabControl { Dock = DockStyle.Fill };
            var tabCreds = new TabPage("Archivio Credenziali");
            var tabSettings = new TabPage("Impostazioni & Sicurezza");

            BuildCredentialsTab(tabCreds);
            BuildSettingsTab(tabSettings);

            tabControl.TabPages.Add(tabCreds);
            tabControl.TabPages.Add(tabSettings);

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
            lvCredentials.Columns.Add("Nome Utente / Email", 200);
            lvCredentials.Columns.Add("Scorciatoia", 110);
            lvCredentials.Columns.Add("Filtro App / Sicurezza", 150);
            lvCredentials.Columns.Add("Template Macro", 220);
            lvCredentials.DoubleClick += (s, e) => EditSelected();

            var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 55, Padding = new Padding(10) };

            btnAdd = new Button { Text = "➕ Aggiungi", Location = new Point(10, 10), Size = new Size(110, 32), BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnAdd.FlatAppearance.BorderSize = 0;
            btnAdd.Click += (s, e) => AddCredential();

            btnEdit = new Button { Text = "✏️ Modifica", Location = new Point(130, 10), Size = new Size(100, 32) };
            btnEdit.Click += (s, e) => EditSelected();

            btnDuplicate = new Button { Text = "📄 Duplica", Location = new Point(240, 10), Size = new Size(100, 32) };
            btnDuplicate.Click += (s, e) => DuplicateSelected();

            btnDelete = new Button { Text = "🗑️ Elimina", Location = new Point(350, 10), Size = new Size(100, 32), ForeColor = Color.FromArgb(180, 40, 20) };
            btnDelete.Click += (s, e) => DeleteSelected();

            btnTestMacro = new Button
            {
                Text = "⏱️ Prova Inserimento (3s)",
                Size = new Size(200, 32),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnTestMacro.Location = new Point(pnlBottom.Width - 215, 10);
            btnTestMacro.Click += (s, e) => TestMacroCountdown();

            pnlBottom.Resize += (s, e) =>
            {
                btnTestMacro.Location = new Point(pnlBottom.Width - 215, 10);
            };

            pnlBottom.Controls.Add(btnAdd);
            pnlBottom.Controls.Add(btnEdit);
            pnlBottom.Controls.Add(btnDuplicate);
            pnlBottom.Controls.Add(btnDelete);
            pnlBottom.Controls.Add(btnTestMacro);

            tab.Controls.Add(lvCredentials);
            tab.Controls.Add(pnlTop);
            tab.Controls.Add(pnlBottom);
        }

        private void BuildSettingsTab(TabPage tab)
        {
            var pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20) };
            int y = 20;

            // Global Picker Hotkey
            var lblPicker = new Label { Text = "Scorciatoia Quick Picker (Barra Ricerca Rapida):", Location = new Point(20, y), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            txtPickerHotkey = new TextBox { Location = new Point(20, y + 25), Width = 280 };
            var lblPickerHint = new Label { Text = "Es. Ctrl+Alt+Space, Win+Shift+C, Ctrl+Alt+C", Location = new Point(310, y + 27), AutoSize = true, ForeColor = Color.Gray };
            pnl.Controls.Add(lblPicker);
            pnl.Controls.Add(txtPickerHotkey);
            pnl.Controls.Add(lblPickerHint);

            // Default Template
            y += 70;
            var lblDefTemplate = new Label { Text = "Template Macro Predefinito per nuove voci:", Location = new Point(20, y), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            txtDefaultTemplate = new TextBox { Location = new Point(20, y + 25), Width = 380 };
            pnl.Controls.Add(lblDefTemplate);
            pnl.Controls.Add(txtDefaultTemplate);

            // Inter-Key Delay
            y += 70;
            var lblDelay = new Label { Text = "Ritardo tra caratteri (millisecondi):", Location = new Point(20, y), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            numInterKeyDelay = new NumericUpDown { Location = new Point(20, y + 25), Width = 100, Minimum = 0, Maximum = 200, Value = 15 };
            pnl.Controls.Add(lblDelay);
            pnl.Controls.Add(numInterKeyDelay);

            // Quick Picker Popup Customization (Opacity & Color)
            y += 70;
            var lblPopupTitle = new Label { Text = "Personalizzazione Popup Quick Picker:", Location = new Point(20, y), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            pnl.Controls.Add(lblPopupTitle);

            y += 28;
            var lblOpacity = new Label { Text = "Livello trasparenza:", Location = new Point(20, y + 4), AutoSize = true };
            tbOpacity = new TrackBar { Location = new Point(160, y), Width = 220, Minimum = 30, Maximum = 100, TickFrequency = 10, SmallChange = 5, LargeChange = 10 };
            lblOpacityVal = new Label { Location = new Point(390, y + 4), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            tbOpacity.ValueChanged += (s, e) => { lblOpacityVal.Text = tbOpacity.Value + "%"; };
            pnl.Controls.Add(lblOpacity);
            pnl.Controls.Add(tbOpacity);
            pnl.Controls.Add(lblOpacityVal);

            y += 45;
            var lblBgColor = new Label { Text = "Colore di sfondo:", Location = new Point(20, y + 4), AutoSize = true };
            pnlColorPreview = new Panel { Location = new Point(160, y + 2), Size = new Size(32, 26), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(32, 33, 36) };
            btnPickColor = new Button { Text = "🎨 Scegli Colore...", Location = new Point(200, y + 2), Width = 135, Height = 28 };
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

            btnResetColor = new Button { Text = "Ripristina Scuro", Location = new Point(345, y + 2), Width = 115, Height = 28 };
            btnResetColor.Click += (s, e) => { pnlColorPreview.BackColor = Color.FromArgb(32, 33, 36); };

            chkRandomColor = new CheckBox { Text = "🎲 Usa colore casuale ad ogni apertura", Location = new Point(475, y + 6), AutoSize = true };
            chkRandomColor.CheckedChanged += (s, e) =>
            {
                pnlColorPreview.Enabled = !chkRandomColor.Checked;
                btnPickColor.Enabled = !chkRandomColor.Checked;
                btnResetColor.Enabled = !chkRandomColor.Checked;
            };

            pnl.Controls.Add(lblBgColor);
            pnl.Controls.Add(pnlColorPreview);
            pnl.Controls.Add(btnPickColor);
            pnl.Controls.Add(btnResetColor);
            pnl.Controls.Add(chkRandomColor);

            // Position mode: Cursor (default) or Center of screen
            y += 42;
            var lblPosTitle = new Label { Text = "Posizione di comparsa:", Location = new Point(20, y + 2), AutoSize = true };
            rbPosCursor = new RadioButton { Text = "Posizione cursore del mouse (predefinita)", Location = new Point(160, y), AutoSize = true, Checked = true };
            rbPosCenter = new RadioButton { Text = "Centro dello schermo", Location = new Point(440, y), AutoSize = true };
            pnl.Controls.Add(lblPosTitle);
            pnl.Controls.Add(rbPosCursor);
            pnl.Controls.Add(rbPosCenter);

            // Whitelisted Processes (SAP GUI, etc.)
            y += 65;
            var lblWhitelistTitle = new Label { Text = "Processi Autorizzati (Bypass automatico controllo password):", Location = new Point(20, y), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            var lblWhitelistSub = new Label { Text = "Ideale per software aziendali come SAP GUI (saplogon.exe, sapgui.exe) o RDP (mstsc.exe)", Location = new Point(20, y + 22), AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8.5f) };
            lbWhitelist = new ListBox { Location = new Point(20, y + 45), Width = 300, Height = 105 };

            txtNewWhitelistProcess = new TextBox { Location = new Point(335, y + 45), Width = 220 };
            btnAddProcess = new Button { Text = "Aggiungi Processo", Location = new Point(335, y + 78), Width = 150, Height = 28 };
            btnAddProcess.Click += (s, e) => AddWhitelistProcess();

            btnRemoveProcess = new Button { Text = "Rimuovi Selezionato", Location = new Point(335, y + 115), Width = 160, Height = 28 };
            btnRemoveProcess.Click += (s, e) => RemoveWhitelistProcess();

            pnl.Controls.Add(lblWhitelistTitle);
            pnl.Controls.Add(lblWhitelistSub);
            pnl.Controls.Add(lbWhitelist);
            pnl.Controls.Add(txtNewWhitelistProcess);
            pnl.Controls.Add(btnAddProcess);
            pnl.Controls.Add(btnRemoveProcess);

            // Vault file location & Backup
            y += 175;
            var lblVaultTitle = new Label { Text = "Archivio Crittografato (DPAPI):", Location = new Point(20, y), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            txtVaultPath = new TextBox { ReadOnly = true, Text = DpapiStorage.GetVaultFilePath(), Location = new Point(20, y + 25), Width = 680, BackColor = Color.White };
            btnOpenFolder = new Button { Text = "📁 Apri Cartella Vault", Location = new Point(20, y + 55), Width = 170, Height = 30 };
            btnOpenFolder.Click += (s, e) =>
            {
                string dir = Path.GetDirectoryName(DpapiStorage.GetVaultFilePath());
                if (Directory.Exists(dir)) Process.Start("explorer.exe", dir);
            };

            btnBackupNow = new Button { Text = "💾 Crea Copia di Backup", Location = new Point(205, y + 55), Width = 180, Height = 30 };
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

            pnl.Controls.Add(lblVaultTitle);
            pnl.Controls.Add(txtVaultPath);
            pnl.Controls.Add(btnOpenFolder);
            pnl.Controls.Add(btnBackupNow);

            // Save Settings Button
            y += 105;
            var btnSaveSettings = new Button { Text = "💾 Salva Impostazioni", Location = new Point(20, y), Width = 190, Height = 35, BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnSaveSettings.FlatAppearance.BorderSize = 0;
            btnSaveSettings.Click += (s, e) => SaveSettingsFromUI();
            pnl.Controls.Add(btnSaveSettings);

            tab.Controls.Add(pnl);
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
            txtPickerHotkey.Text = s.GlobalPickerHotkey;
            txtDefaultTemplate.Text = s.DefaultTemplate;
            numInterKeyDelay.Value = Math.Max(0, Math.Min(200, s.InterKeyDelayMs));

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

            if (string.Equals(s.PickerPositionMode, "CenterScreen", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.PickerPositionMode, "Center", StringComparison.OrdinalIgnoreCase))
            {
                rbPosCenter.Checked = true;
            }
            else
            {
                rbPosCursor.Checked = true;
            }

            lbWhitelist.Items.Clear();
            if (s.WhitelistedProcesses != null)
            {
                foreach (var p in s.WhitelistedProcesses)
                {
                    lbWhitelist.Items.Add(p);
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

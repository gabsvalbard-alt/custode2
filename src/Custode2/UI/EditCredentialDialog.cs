using System;
using System.Drawing;
using System.Windows.Forms;
using Custode2.Models;

namespace Custode2.UI
{
    public class EditCredentialDialog : Form
    {
        private TextBox txtTitle;
        private TextBox txtUsername;
        private TextBox txtPassword;
        private Button btnTogglePassword;
        private TextBox txtTemplate;
        private TextBox txtHotkey;
        private TextBox txtAppFilter;
        private CheckBox chkBypassSecurity;
        private TextBox txtNotes;
        private Button btnOk;
        private Button btnCancel;

        public CredentialEntry Entry { get; private set; }

        public EditCredentialDialog(CredentialEntry entry = null)
        {
            Entry = entry ?? new CredentialEntry();
            InitializeComponent();
            PopulateFields();
        }

        private void InitializeComponent()
        {
            this.Text = Entry.Id == null ? "Nuova Credenziale" : "Modifica Credenziale";
            this.Size = new Size(580, 560);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.BackColor = Color.FromArgb(250, 250, 252);

            int labelX = 25;
            int inputX = 175;
            int inputWidth = 355;
            int y = 20;
            int spacing = 42;

            // Titolo
            var lblTitle = new Label { Text = "Titolo / Servizio *:", Location = new Point(labelX, y + 3), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            txtTitle = new TextBox { Location = new Point(inputX, y), Width = inputWidth };
            this.Controls.Add(lblTitle);
            this.Controls.Add(txtTitle);

            // Username
            y += spacing;
            var lblUsername = new Label { Text = "Nome Utente / Email:", Location = new Point(labelX, y + 3), AutoSize = true };
            txtUsername = new TextBox { Location = new Point(inputX, y), Width = inputWidth };
            this.Controls.Add(lblUsername);
            this.Controls.Add(txtUsername);

            // Password
            y += spacing;
            var lblPassword = new Label { Text = "Password *:", Location = new Point(labelX, y + 3), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            txtPassword = new TextBox { Location = new Point(inputX, y), Width = inputWidth - 55, UseSystemPasswordChar = true };
            btnTogglePassword = new Button { Text = "Mostra", Location = new Point(inputX + inputWidth - 50, y - 1), Width = 50, Height = txtPassword.Height + 2 };
            btnTogglePassword.Click += (s, e) =>
            {
                txtPassword.UseSystemPasswordChar = !txtPassword.UseSystemPasswordChar;
                btnTogglePassword.Text = txtPassword.UseSystemPasswordChar ? "Mostra" : "Nascondi";
            };
            this.Controls.Add(lblPassword);
            this.Controls.Add(txtPassword);
            this.Controls.Add(btnTogglePassword);

            // Template Macro
            y += spacing;
            var lblTemplate = new Label { Text = "Sequenza Macro:", Location = new Point(labelX, y + 3), AutoSize = true };
            txtTemplate = new TextBox { Location = new Point(inputX, y), Width = inputWidth, Text = "$USER$<TAB>$PASS$<ENTER>" };
            this.Controls.Add(lblTemplate);
            this.Controls.Add(txtTemplate);

            // Preset Buttons for template
            y += 28;
            var pnlPresets = new FlowLayoutPanel { Location = new Point(inputX, y), Width = inputWidth, Height = 30 };
            AddTokenButton(pnlPresets, "$USER$");
            AddTokenButton(pnlPresets, "$TAB$");
            AddTokenButton(pnlPresets, "$PASS$");
            AddTokenButton(pnlPresets, "$ENTER$");
            AddTokenButton(pnlPresets, "$DELAY(500)$");
            this.Controls.Add(pnlPresets);

            // Direct Hotkey
            y += spacing;
            var lblHotkey = new Label { Text = "Scorciatoia Diretta:", Location = new Point(labelX, y + 3), AutoSize = true };
            txtHotkey = new TextBox { Location = new Point(inputX, y), Width = inputWidth };
            var lblHotkeyHint = new Label { Text = "Es. Ctrl+Alt+1, Ctrl+Shift+G (lascia vuoto se non necessaria)", Location = new Point(inputX, y + 26), AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8f) };
            this.Controls.Add(lblHotkey);
            this.Controls.Add(txtHotkey);
            this.Controls.Add(lblHotkeyHint);

            // Process / Window Filter (SAP / Whitelist)
            y += spacing + 12;
            var lblFilter = new Label { Text = "Filtro Processo / App:", Location = new Point(labelX, y + 3), AutoSize = true };
            txtAppFilter = new TextBox { Location = new Point(inputX, y), Width = inputWidth };
            var lblFilterHint = new Label { Text = "Es. saplogon.exe o parte del titolo finestra (opzionale)", Location = new Point(inputX, y + 26), AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8f) };
            this.Controls.Add(lblFilter);
            this.Controls.Add(txtAppFilter);
            this.Controls.Add(lblFilterHint);

            // Bypass Security Check Checkbox
            y += spacing + 12;
            chkBypassSecurity = new CheckBox
            {
                Text = "Fidati sempre (non chiedere conferma se il campo password non è mascherato)",
                Location = new Point(inputX, y),
                Width = inputWidth,
                AutoSize = true
            };
            this.Controls.Add(chkBypassSecurity);

            // Note
            y += 35;
            var lblNotes = new Label { Text = "Note:", Location = new Point(labelX, y + 3), AutoSize = true };
            txtNotes = new TextBox { Location = new Point(inputX, y), Width = inputWidth, Height = 55, Multiline = true, ScrollBars = ScrollBars.Vertical };
            this.Controls.Add(lblNotes);
            this.Controls.Add(txtNotes);

            // Bottom Buttons
            y += 75;
            btnOk = new Button { Text = "Salva", Location = new Point(inputX + inputWidth - 180, y), Width = 85, Height = 32, BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += BtnOk_Click;

            btnCancel = new Button { Text = "Annulla", DialogResult = DialogResult.Cancel, Location = new Point(inputX + inputWidth - 90, y), Width = 85, Height = 32, BackColor = Color.FromArgb(235, 235, 235), FlatStyle = FlatStyle.Flat };
            btnCancel.FlatAppearance.BorderSize = 0;

            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private void AddTokenButton(FlowLayoutPanel panel, string token)
        {
            var btn = new Button
            {
                Text = token,
                AutoSize = true,
                Height = 24,
                BackColor = Color.FromArgb(240, 240, 242),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8f)
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            btn.Click += (s, e) =>
            {
                txtTemplate.Paste(token);
                txtTemplate.Focus();
            };
            panel.Controls.Add(btn);
        }

        private void PopulateFields()
        {
            txtTitle.Text = Entry.Title;
            txtUsername.Text = Entry.Username;
            txtPassword.Text = Entry.Password;
            txtTemplate.Text = string.IsNullOrEmpty(Entry.Template) ? "$USER$<TAB>$PASS$<ENTER>" : Entry.Template;
            txtHotkey.Text = Entry.DirectHotkey;
            txtAppFilter.Text = Entry.AppFilter;
            chkBypassSecurity.Checked = Entry.BypassSecurityCheck;
            txtNotes.Text = Entry.Notes;
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text))
            {
                MessageBox.Show("Il titolo del servizio è obbligatorio.", "Attenzione", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtTitle.Focus();
                return;
            }

            Entry.Title = txtTitle.Text.Trim();
            Entry.Username = txtUsername.Text.Trim();
            Entry.Password = txtPassword.Text;
            Entry.Template = string.IsNullOrWhiteSpace(txtTemplate.Text) ? "$USER$<TAB>$PASS$<ENTER>" : txtTemplate.Text.Trim();
            Entry.DirectHotkey = txtHotkey.Text.Trim();
            Entry.AppFilter = txtAppFilter.Text.Trim();
            Entry.BypassSecurityCheck = chkBypassSecurity.Checked;
            Entry.Notes = txtNotes.Text.Trim();

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}


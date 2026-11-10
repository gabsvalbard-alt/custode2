using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Custode2.Models;

namespace Custode2.UI
{
    public class EditSharedPasswordDialog : Form
    {
        private TextBox txtName;
        private TextBox txtValue;
        private TextBox txtNotes;
        private Button btnToggle;
        private Button btnSave;
        private Button btnCancel;

        private readonly List<SharedPassword> _existingPasswords;

        public SharedPassword Item { get; private set; }

        public EditSharedPasswordDialog(SharedPassword item = null, List<SharedPassword> existingPasswords = null)
        {
            Item = item ?? new SharedPassword();
            _existingPasswords = existingPasswords ?? new List<SharedPassword>();
            InitializeComponent();
            PopulateFields();
        }

        private void InitializeComponent()
        {
            bool isNew = string.IsNullOrEmpty(Item.Name);
            this.Text = isNew ? "Nuova Password Jolly" : "Modifica Password Jolly";
            try { this.Icon = TrayAppContext.GetAppIcon(); } catch { }
            this.Size = new Size(520, 310);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(248, 249, 251);
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            int labelX = 25;
            int inputX = 140;
            int inputWidth = 330;
            int y = 22;
            int spacing = 42;

            // Nome Jolly
            var lblName = new Label { Text = "Nome Jolly *:", Location = new Point(labelX, y + 3), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            txtName = new TextBox { Location = new Point(inputX, y), Width = inputWidth };
            var lblNameHint = new Label { Text = "Es. Password 1, Password Dominio, PIN Aziendale, VPN", Location = new Point(inputX, y + 24), AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8.2f) };
            this.Controls.Add(lblName);
            this.Controls.Add(txtName);
            this.Controls.Add(lblNameHint);

            // Valore Password
            y += spacing + 10;
            var lblValue = new Label { Text = "Password *:", Location = new Point(labelX, y + 3), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            txtValue = new TextBox { Location = new Point(inputX, y), Width = inputWidth - 70, UseSystemPasswordChar = true };
            btnToggle = new Button { Text = "Mostra", Location = new Point(inputX + inputWidth - 65, y - 1), Width = 65, Height = txtValue.Height + 2 };
            btnToggle.Click += (s, e) =>
            {
                txtValue.UseSystemPasswordChar = !txtValue.UseSystemPasswordChar;
                btnToggle.Text = txtValue.UseSystemPasswordChar ? "Mostra" : "Nascondi";
            };
            this.Controls.Add(lblValue);
            this.Controls.Add(txtValue);
            this.Controls.Add(btnToggle);

            // Note
            y += spacing;
            var lblNotes = new Label { Text = "Note (opzionale):", Location = new Point(labelX, y + 3), AutoSize = true };
            txtNotes = new TextBox { Location = new Point(inputX, y), Width = inputWidth, Height = 48, Multiline = true };
            this.Controls.Add(lblNotes);
            this.Controls.Add(txtNotes);

            // Pulsanti Salva / Annulla
            y += 65;
            btnSave = new Button
            {
                Text = "Salva",
                DialogResult = DialogResult.OK,
                Location = new Point(inputX + inputWidth - 190, y),
                Width = 90,
                Height = 32,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button
            {
                Text = "Annulla",
                DialogResult = DialogResult.Cancel,
                Location = new Point(inputX + inputWidth - 90, y),
                Width = 90,
                Height = 32,
                BackColor = Color.FromArgb(235, 235, 235),
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;
        }

        private void PopulateFields()
        {
            txtName.Text = Item.Name ?? string.Empty;
            txtValue.Text = Item.Value ?? string.Empty;
            txtNotes.Text = Item.Notes ?? string.Empty;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            string name = txtName.Text.Trim();
            string val = txtValue.Text;

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Inserisci un nome per la Password Jolly (es. 'Password 1').", "Campo obbligatorio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                txtName.Focus();
                return;
            }

            // Check duplicate name
            if (_existingPasswords != null)
            {
                foreach (var other in _existingPasswords)
                {
                    if (other.Id != Item.Id && string.Equals(other.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show(string.Format("Esiste già una Password Jolly con il nome '{0}'. Scegli un nome diverso.", name), "Nome duplicato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        this.DialogResult = DialogResult.None;
                        txtName.Focus();
                        return;
                    }
                }
            }

            Item.Name = name;
            Item.Value = val;
            Item.Notes = txtNotes.Text.Trim();
            Item.LastModifiedAt = DateTime.Now;
        }
    }
}

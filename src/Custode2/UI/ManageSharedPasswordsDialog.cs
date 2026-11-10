using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Custode2.Models;
using Custode2.Security;

namespace Custode2.UI
{
    public class ManageSharedPasswordsDialog : Form
    {
        private ListView lvShared;
        private Button btnAdd;
        private Button btnEdit;
        private Button btnDelete;
        private Button btnCopy;
        private Button btnToggleShow;
        private Button btnClose;

        private readonly VaultData _vault;
        private readonly Action _onVaultChanged;
        private bool _showPlainPasswords = false;

        public ManageSharedPasswordsDialog(VaultData vault, Action onVaultChanged)
        {
            _vault = vault ?? new VaultData();
            _onVaultChanged = onVaultChanged;
            InitializeComponent();
            RefreshList();
        }

        private void InitializeComponent()
        {
            this.Text = "🔑 Gestione Password Jolly / Condivise";
            try { this.Icon = TrayAppContext.GetAppIcon(); } catch { }
            this.Size = new Size(740, 480);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(248, 249, 251);
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 68, Padding = new Padding(15, 12, 15, 8) };
            var lblHeader = new Label
            {
                Text = "Le Password Jolly consentono di riutilizzare la stessa password su più credenziali contemporaneamente.\nQuando aggiorni una Password Jolly, tutte le credenziali collegate si aggiorneranno automaticamente!",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(50, 55, 60),
                Font = new Font("Segoe UI", 9.2f)
            };
            pnlTop.Controls.Add(lblHeader);

            lvShared = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };
            lvShared.Columns.Add("Nome Jolly", 185);
            lvShared.Columns.Add("Password", 160);
            lvShared.Columns.Add("Credenziali Collegate", 175);
            lvShared.Columns.Add("Note", 180);
            lvShared.DoubleClick += (s, e) => EditSelected();

            var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 56, Padding = new Padding(15, 10, 15, 10) };

            btnAdd = new Button
            {
                Text = "➕ Nuova Jolly",
                Location = new Point(15, 10),
                Size = new Size(125, 34),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnAdd.FlatAppearance.BorderSize = 0;
            btnAdd.Click += (s, e) => AddNew();

            btnEdit = new Button
            {
                Text = "✏️ Modifica",
                Location = new Point(148, 10),
                Size = new Size(100, 34),
                FlatStyle = FlatStyle.Flat
            };
            btnEdit.Click += (s, e) => EditSelected();

            btnDelete = new Button
            {
                Text = "🗑️ Elimina",
                Location = new Point(256, 10),
                Size = new Size(95, 34),
                ForeColor = Color.FromArgb(180, 40, 20),
                FlatStyle = FlatStyle.Flat
            };
            btnDelete.Click += (s, e) => DeleteSelected();

            btnCopy = new Button
            {
                Text = "📋 Copia",
                Location = new Point(359, 10),
                Size = new Size(85, 34),
                FlatStyle = FlatStyle.Flat
            };
            btnCopy.Click += (s, e) => CopySelected();

            btnToggleShow = new Button
            {
                Text = "👁️ Mostra",
                Location = new Point(452, 10),
                Size = new Size(100, 34),
                FlatStyle = FlatStyle.Flat
            };
            btnToggleShow.Click += (s, e) =>
            {
                _showPlainPasswords = !_showPlainPasswords;
                btnToggleShow.Text = _showPlainPasswords ? "🙈 Nascondi" : "👁️ Mostra";
                RefreshList();
            };

            btnClose = new Button
            {
                Text = "Chiudi",
                DialogResult = DialogResult.OK,
                Location = new Point(620, 10),
                Size = new Size(90, 34),
                BackColor = Color.FromArgb(235, 238, 242),
                FlatStyle = FlatStyle.Flat
            };
            btnClose.FlatAppearance.BorderSize = 0;

            pnlBottom.Controls.Add(btnAdd);
            pnlBottom.Controls.Add(btnEdit);
            pnlBottom.Controls.Add(btnDelete);
            pnlBottom.Controls.Add(btnCopy);
            pnlBottom.Controls.Add(btnToggleShow);
            pnlBottom.Controls.Add(btnClose);

            this.Controls.Add(lvShared);
            this.Controls.Add(pnlTop);
            this.Controls.Add(pnlBottom);
            this.CancelButton = btnClose;
        }

        public void RefreshList()
        {
            lvShared.BeginUpdate();
            lvShared.Items.Clear();

            if (_vault.SharedPasswords == null)
            {
                _vault.SharedPasswords = new List<SharedPassword>();
            }

            foreach (var sp in _vault.SharedPasswords)
            {
                var lvi = new ListViewItem(sp.Name ?? string.Empty);

                string passDisplay;
                if (_showPlainPasswords)
                {
                    passDisplay = sp.Value ?? string.Empty;
                }
                else
                {
                    passDisplay = string.IsNullOrEmpty(sp.Value) ? "(Vuota)" : new string('•', Math.Min(12, Math.Max(6, sp.Value.Length)));
                }
                lvi.SubItems.Add(passDisplay);

                // Find linked credentials count
                var linked = _vault.Entries.Where(c => c.SharedPasswordId == sp.Id).ToList();
                string linkedText = linked.Count == 0
                    ? "Nessuna"
                    : string.Format("{0} credenziale/i", linked.Count);
                lvi.SubItems.Add(linkedText);

                lvi.SubItems.Add(sp.Notes ?? string.Empty);
                lvi.Tag = sp;

                if (linked.Count > 0)
                {
                    lvi.ToolTipText = "Utilizzata in:\n" + string.Join("\n", linked.Select(c => "• " + c.Title));
                }

                lvShared.Items.Add(lvi);
            }

            lvShared.EndUpdate();
        }

        private void AddNew()
        {
            using (var dlg = new EditSharedPasswordDialog(null, _vault.SharedPasswords))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _vault.SharedPasswords.Add(dlg.Item);
                    if (_onVaultChanged != null) _onVaultChanged();
                    RefreshList();
                }
            }
        }

        private void EditSelected()
        {
            if (lvShared.SelectedItems.Count == 0) return;
            var sp = lvShared.SelectedItems[0].Tag as SharedPassword;
            if (sp == null) return;

            using (var dlg = new EditSharedPasswordDialog(sp, _vault.SharedPasswords))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (_onVaultChanged != null) _onVaultChanged();
                    RefreshList();
                }
            }
        }

        private void DeleteSelected()
        {
            if (lvShared.SelectedItems.Count == 0) return;
            var sp = lvShared.SelectedItems[0].Tag as SharedPassword;
            if (sp == null) return;

            var linked = _vault.Entries.Where(c => c.SharedPasswordId == sp.Id).ToList();
            string confirmMsg;

            if (linked.Count > 0)
            {
                confirmMsg = string.Format(
                    "La Password Jolly '{0}' è attualmente utilizzata da {1} credenziale/i:\n\n{2}\n\n" +
                    "Se confermi, la Password Jolly verrà eliminata e il valore attuale della password verrà salvato come password dedicata sulle credenziali collegate (non andrà perso).\n\n" +
                    "Vuoi procedere con l'eliminazione?",
                    sp.Name,
                    linked.Count,
                    string.Join("\n", linked.Take(5).Select(c => "• " + c.Title)) + (linked.Count > 5 ? "\n• ..." : ""));
            }
            else
            {
                confirmMsg = string.Format("Sei sicuro di voler eliminare la Password Jolly '{0}'?", sp.Name);
            }

            var res = MessageBox.Show(confirmMsg, "Conferma eliminazione Password Jolly", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res == DialogResult.Yes)
            {
                // Convert linked credentials to local password so they don't break
                foreach (var c in linked)
                {
                    c.Password = sp.Value ?? string.Empty;
                    c.SharedPasswordId = string.Empty;
                }

                _vault.SharedPasswords.Remove(sp);
                if (_onVaultChanged != null) _onVaultChanged();
                RefreshList();
            }
        }

        private void CopySelected()
        {
            if (lvShared.SelectedItems.Count == 0) return;
            var sp = lvShared.SelectedItems[0].Tag as SharedPassword;
            if (sp == null) return;

            int clearSec = (_vault.Settings != null && _vault.Settings.ClipboardClearSeconds > 0) ? _vault.Settings.ClipboardClearSeconds : 15;
            ClipboardSecurity.CopyPassword(sp.Value ?? string.Empty, clearSec);
            MessageBox.Show(string.Format("Password per '{0}' copiata negli appunti!\n(Verrà rimossa automaticamente tra {1} secondi)", sp.Name, clearSec), "Copiato", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}

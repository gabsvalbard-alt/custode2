using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Custode2.Macro;
using Custode2.Models;

namespace Custode2.UI
{
    public class QuickPickerForm : Form
    {
        private TextBox txtSearch;
        private ListView lvResults;
        private Label lblHint;
        private readonly List<CredentialEntry> _allEntries;
        private readonly AppSettings _settings;
        private readonly Action _onSettingsUpdated;
        private IntPtr _previousForegroundWindow;
        private bool _isShowingPicker = false;
        private DateTime _openedAt = DateTime.MinValue;

        public QuickPickerForm(List<CredentialEntry> entries, AppSettings settings, Action onSettingsUpdated)
        {
            _allEntries = entries ?? new List<CredentialEntry>();
            _settings = settings ?? new AppSettings();
            _onSettingsUpdated = onSettingsUpdated;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(650, 400);
            this.BackColor = Color.FromArgb(32, 33, 36);
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            // Container panel for border effect
            var pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14),
                BackColor = Color.FromArgb(32, 33, 36)
            };

            // Search Box
            txtSearch = new TextBox
            {
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 13f, FontStyle.Regular),
                BackColor = Color.FromArgb(48, 49, 52),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            txtSearch.TextChanged += (s, e) => FilterEntries();
            txtSearch.KeyDown += TxtSearch_KeyDown;

            // Results List
            lvResults = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(32, 33, 36),
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 10.5f),
                Margin = new Padding(0, 10, 0, 0)
            };
            lvResults.Columns.Add("Servizio / Titolo", 320);
            lvResults.Columns.Add("Utente / Email", 280);
            lvResults.DoubleClick += (s, e) => ExecuteSelected();
            lvResults.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    ExecuteSelected();
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    CancelAndClose();
                    e.Handled = true;
                }
            };

            // Hint label
            lblHint = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                Text = "↑↓ Scegli   •   Invio: Compila ed esegui Macro   •   Esc: Annulla",
                ForeColor = Color.FromArgb(170, 170, 170),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9f)
            };

            var pnlSpacer = new Panel { Dock = DockStyle.Top, Height = 10 };

            pnlMain.Controls.Add(lvResults);
            pnlMain.Controls.Add(pnlSpacer);
            pnlMain.Controls.Add(txtSearch);
            pnlMain.Controls.Add(lblHint);
            this.Controls.Add(pnlMain);
        }

        public void ShowPicker(IntPtr previousWindow)
        {
            _previousForegroundWindow = previousWindow;
            _openedAt = DateTime.Now;
            _isShowingPicker = true;

            txtSearch.Clear();
            FilterEntries();

            // Center on active screen
            this.CenterToScreen();
            this.TopMost = true;
            this.Show();
            this.BringToFront();
            this.Activate();
            NativeMethods.SetForegroundWindow(this.Handle);
            txtSearch.Focus();
        }

        private void FilterEntries()
        {
            string query = txtSearch.Text.Trim();
            lvResults.BeginUpdate();
            lvResults.Items.Clear();

            var matches = string.IsNullOrEmpty(query)
                ? _allEntries
                : _allEntries.Where(e => (e.Title != null && e.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                         (e.Username != null && e.Username.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                         (e.Notes != null && e.Notes.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();

            foreach (var item in matches)
            {
                var lvi = new ListViewItem(item.Title ?? string.Empty);
                lvi.SubItems.Add(item.Username ?? string.Empty);
                lvi.Tag = item;
                lvResults.Items.Add(lvi);
            }

            if (lvResults.Items.Count > 0)
            {
                lvResults.Items[0].Selected = true;
                lvResults.Items[0].Focused = true;
            }

            lvResults.EndUpdate();
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down)
            {
                if (lvResults.Items.Count > 0)
                {
                    int next = lvResults.SelectedIndices.Count > 0 ? (lvResults.SelectedIndices[0] + 1) % lvResults.Items.Count : 0;
                    lvResults.Items[next].Selected = true;
                    lvResults.EnsureVisible(next);
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                if (lvResults.Items.Count > 0)
                {
                    int prev = lvResults.SelectedIndices.Count > 0 ? (lvResults.SelectedIndices[0] - 1 + lvResults.Items.Count) % lvResults.Items.Count : lvResults.Items.Count - 1;
                    lvResults.Items[prev].Selected = true;
                    lvResults.EnsureVisible(prev);
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                ExecuteSelected();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                CancelAndClose();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void ExecuteSelected()
        {
            if (lvResults.SelectedItems.Count == 0) return;

            var entry = lvResults.SelectedItems[0].Tag as CredentialEntry;
            if (entry == null) return;

            IntPtr target = _previousForegroundWindow;
            _isShowingPicker = false;
            this.Hide();

            MacroEngine.Execute(entry, target, _settings, _onSettingsUpdated);
        }

        private void CancelAndClose()
        {
            _isShowingPicker = false;
            this.Hide();
            if (_previousForegroundWindow != IntPtr.Zero)
            {
                NativeMethods.SetForegroundWindow(_previousForegroundWindow);
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (!_isShowingPicker) return;

            // Prevent premature closure within the first 400ms while window activates
            if ((DateTime.Now - _openedAt).TotalMilliseconds < 400)
            {
                return;
            }

            CancelAndClose();
        }
    }
}

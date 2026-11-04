using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Custode2.Macro;
using Custode2.Models;
using Custode2.Security;

namespace Custode2.UI
{
    public class QuickPickerForm : Form
    {
        private Panel pnlMain;
        private TextBox txtSearch;
        private ListView lvResults;
        private Label lblHint;
        private readonly List<CredentialEntry> _allEntries;
        private readonly AppSettings _settings;
        private readonly Action _onSettingsUpdated;
        private IntPtr _previousForegroundWindow;
        private bool _isShowingPicker = false;
        private DateTime _openedAt = DateTime.MinValue;

        private ColumnHeader colIndex;
        private ColumnHeader colTitle;
        private ColumnHeader colUser;
        private ColumnHeader colCopy;

        private static readonly Random _rnd = new Random();
        private static readonly Color[] CuratedRandomColors = new Color[]
        {
            Color.FromArgb(26, 35, 126),   // Deep Indigo
            Color.FromArgb(0, 77, 64),     // Deep Teal
            Color.FromArgb(74, 20, 140),   // Deep Purple
            Color.FromArgb(136, 14, 79),   // Deep Wine
            Color.FromArgb(183, 28, 28),   // Dark Crimson
            Color.FromArgb(27, 94, 32),    // Dark Forest Green
            Color.FromArgb(38, 50, 56),    // Blue Grey
            Color.FromArgb(33, 33, 33),    // Charcoal Black
            Color.FromArgb(49, 27, 146),   // Deep Violet
            Color.FromArgb(13, 71, 161),   // Royal Blue
            Color.FromArgb(62, 39, 35),    // Dark Espresso
            Color.FromArgb(0, 96, 100),    // Dark Cyan
            Color.FromArgb(88, 24, 69),    // Burgundy
            Color.FromArgb(21, 67, 96),    // Steel Blue
            Color.FromArgb(20, 90, 50)     // Emerald Dark
        };

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
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(460, 400);
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            // Container panel for border effect
            pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };

            // Search Box
            txtSearch = new TextBox
            {
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 12.5f, FontStyle.Regular),
                BorderStyle = BorderStyle.FixedSingle
            };
            txtSearch.TextChanged += (s, e) => FilterEntries();
            txtSearch.KeyDown += HandleKeyNavigation;

            // Results List
            lvResults = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10f),
                Margin = new Padding(0, 8, 0, 0)
            };

            colIndex = lvResults.Columns.Add("#", 42, HorizontalAlignment.Center);
            colTitle = lvResults.Columns.Add("Servizio / Titolo", 180);
            colUser = lvResults.Columns.Add("Utente / Email", 140);
            colCopy = lvResults.Columns.Add("Password", 72, HorizontalAlignment.Center);

            lvResults.DoubleClick += (s, e) => ExecuteSelected();
            lvResults.MouseClick += LvResults_MouseClick;
            lvResults.KeyDown += HandleKeyNavigation;

            // Hint label
            lblHint = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                Text = "1-9: Inserisci   •   Ctrl+C: Copia   •   Invio: Compila   •   Esc: Annulla",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8.5f)
            };

            var pnlSpacer = new Panel { Dock = DockStyle.Top, Height = 8 };

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

            // 1. Apply configured opacity (30% to 100%)
            int opacityVal = _settings.PickerOpacity > 0 ? _settings.PickerOpacity : 95;
            this.Opacity = Math.Max(0.3, Math.Min(1.0, opacityVal / 100.0));

            // 2. Apply configured or random background color
            Color bgColor = ResolveBackgroundColor();
            ApplyThemeColors(bgColor);

            txtSearch.Clear();
            FilterEntries();

            // 3. Position popup: Cursor position (default) or Center of screen
            PositionWindow();

            this.TopMost = true;
            this.Show();
            this.BringToFront();
            this.Activate();
            NativeMethods.SetForegroundWindow(this.Handle);
            txtSearch.Focus();
        }

        private Color ResolveBackgroundColor()
        {
            if (_settings.PickerRandomColor)
            {
                return CuratedRandomColors[_rnd.Next(CuratedRandomColors.Length)];
            }

            if (!string.IsNullOrEmpty(_settings.PickerBackgroundColor))
            {
                try
                {
                    return ColorTranslator.FromHtml(_settings.PickerBackgroundColor);
                }
                catch
                {
                    // Fallthrough to default
                }
            }

            return Color.FromArgb(32, 33, 36);
        }

        private void ApplyThemeColors(Color bgColor)
        {
            this.BackColor = bgColor;
            pnlMain.BackColor = bgColor;

            // Compute luminance to adjust text contrast
            double luminance = (0.299 * bgColor.R + 0.587 * bgColor.G + 0.114 * bgColor.B) / 255.0;
            bool isLight = luminance > 0.55;

            if (isLight)
            {
                Color searchBg = Color.FromArgb(Math.Max(0, bgColor.R - 25), Math.Max(0, bgColor.G - 25), Math.Max(0, bgColor.B - 25));
                txtSearch.BackColor = searchBg;
                txtSearch.ForeColor = Color.Black;

                lvResults.BackColor = bgColor;
                lvResults.ForeColor = Color.FromArgb(20, 20, 20);
                lblHint.ForeColor = Color.FromArgb(70, 70, 70);
            }
            else
            {
                Color searchBg = Color.FromArgb(Math.Min(255, bgColor.R + 25), Math.Min(255, bgColor.G + 25), Math.Min(255, bgColor.B + 25));
                txtSearch.BackColor = searchBg;
                txtSearch.ForeColor = Color.White;

                lvResults.BackColor = bgColor;
                lvResults.ForeColor = Color.WhiteSmoke;
                lblHint.ForeColor = Color.FromArgb(190, 190, 190);
            }
        }

        private void PositionWindow()
        {
            Point cursor = Cursor.Position;
            Screen screen = Screen.FromPoint(cursor);
            Rectangle area = screen.WorkingArea;

            string mode = string.IsNullOrEmpty(_settings.PickerPositionMode) ? "Cursor" : _settings.PickerPositionMode;

            int x, y;
            if (mode.Equals("CenterScreen", StringComparison.OrdinalIgnoreCase) || mode.Equals("Center", StringComparison.OrdinalIgnoreCase))
            {
                // Center on the active monitor
                x = area.Left + (area.Width - this.Width) / 2;
                y = area.Top + (area.Height - this.Height) / 2;
            }
            else
            {
                // Default: near cursor position with screen bounds clamping
                x = cursor.X - 45;
                y = cursor.Y - 20;

                if (x + this.Width > area.Right) x = area.Right - this.Width - 10;
                if (x < area.Left) x = area.Left + 10;
                if (y + this.Height > area.Bottom) y = area.Bottom - this.Height - 10;
                if (y < area.Top) y = area.Top + 10;
            }

            this.Location = new Point(x, y);
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

            // 1. Measure text to compute adaptive width
            int maxTitleW = 120;
            int maxUserW = 100;

            using (Graphics g = lvResults.CreateGraphics())
            {
                foreach (var match in matches)
                {
                    if (!string.IsNullOrEmpty(match.Title))
                    {
                        int w = (int)g.MeasureString(match.Title, lvResults.Font).Width;
                        if (w > maxTitleW) maxTitleW = w;
                    }
                    if (!string.IsNullOrEmpty(match.Username))
                    {
                        int w = (int)g.MeasureString(match.Username, lvResults.Font).Width;
                        if (w > maxUserW) maxUserW = w;
                    }
                }
            }

            // Cap measured widths to reasonable boundaries
            maxTitleW = Math.Min(310, maxTitleW + 15);
            maxUserW = Math.Min(230, maxUserW + 15);

            int colIndexW = 42;
            int colCopyW = 72;
            int paddingAndScrollbar = 50;

            int targetWidth = colIndexW + maxTitleW + maxUserW + colCopyW + paddingAndScrollbar;
            // Adaptive clamp: min 430px, max 680px
            targetWidth = Math.Max(430, Math.Min(680, targetWidth));

            if (this.Width != targetWidth)
            {
                this.Width = targetWidth;
                PositionWindow();
            }

            // Distribute column widths
            colIndex.Width = colIndexW;
            colCopy.Width = colCopyW;
            colTitle.Width = maxTitleW;
            colUser.Width = Math.Max(100, this.Width - (colIndexW + maxTitleW + colCopyW + 35));

            // 2. Populate rows
            for (int i = 0; i < matches.Count; i++)
            {
                var item = matches[i];
                string indexLabel = (i < 9) ? string.Format("[{0}]", i + 1) : (i == 9 ? "[0]" : string.Format("{0}", i + 1));

                var lvi = new ListViewItem(indexLabel);
                lvi.SubItems.Add(item.Title ?? string.Empty);
                lvi.SubItems.Add(item.Username ?? string.Empty);
                lvi.SubItems.Add("📋 Copia");
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

        private void LvResults_MouseClick(object sender, MouseEventArgs e)
        {
            var hit = lvResults.HitTest(e.Location);
            if (hit.Item != null && hit.SubItem != null)
            {
                int col = hit.Item.SubItems.IndexOf(hit.SubItem);
                if (col == 3) // Copy button clicked
                {
                    var entry = hit.Item.Tag as CredentialEntry;
                    if (entry != null)
                    {
                        CopyPasswordAndClose(entry, hit.SubItem);
                    }
                }
            }
        }

        private void CopyPasswordAndClose(CredentialEntry entry, ListViewItem.ListViewSubItem subItem)
        {
            if (entry == null) return;

            if (subItem != null)
            {
                subItem.Text = "✓ Copiata!";
            }

            int clearSec = _settings.ClipboardClearSeconds > 0 ? _settings.ClipboardClearSeconds : 15;
            ClipboardSecurity.CopyPassword(entry.Password ?? string.Empty, clearSec);

            // Small delay to let user see the checkmark, then hide and restore previous window
            var timer = new Timer { Interval = 230 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                _isShowingPicker = false;
                this.Hide();
                if (_previousForegroundWindow != IntPtr.Zero)
                {
                    NativeMethods.SetForegroundWindow(_previousForegroundWindow);
                }
            };
            timer.Start();
        }

        private void HandleKeyNavigation(object sender, KeyEventArgs e)
        {
            // 1. Ctrl+C: copy selected password to clipboard
            if (e.Control && e.KeyCode == Keys.C)
            {
                if (lvResults.SelectedItems.Count > 0)
                {
                    var entry = lvResults.SelectedItems[0].Tag as CredentialEntry;
                    if (entry != null)
                    {
                        ListViewItem.ListViewSubItem sub = lvResults.SelectedItems[0].SubItems.Count > 3
                            ? lvResults.SelectedItems[0].SubItems[3]
                            : null;
                        CopyPasswordAndClose(entry, sub);
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        return;
                    }
                }
            }

            // 2. Direct row selection via numeric key (1-9, or 0 for 10)
            int digit = -1;
            if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D9) digit = (e.KeyCode - Keys.D1) + 1;
            else if (e.KeyCode >= Keys.NumPad1 && e.KeyCode <= Keys.NumPad9) digit = (e.KeyCode - Keys.NumPad1) + 1;
            else if (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0) digit = 10;

            // If search box is empty or user is holding Alt, execute immediately by index!
            if (digit > 0 && (string.IsNullOrEmpty(txtSearch.Text) || e.Alt))
            {
                int targetIndex = digit - 1;
                if (targetIndex >= 0 && targetIndex < lvResults.Items.Count)
                {
                    lvResults.Items[targetIndex].Selected = true;
                    ExecuteSelected();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }
            }

            // 3. Up/Down Navigation
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

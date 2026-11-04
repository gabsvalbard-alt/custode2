using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private Panel pnlSearchSpacer;
        private DoubleBufferedPanel pnlHeader;
        private Panel pnlHeaderSpacer;
        private PickerListView lvResults;
        private Panel pnlHintSpacer;
        private Label lblHint;

        private Color _headerBgColor = Color.FromArgb(45, 46, 50);
        private Color _headerFgColor = Color.FromArgb(220, 220, 225);
        private Color _headerBorderColor = Color.FromArgb(65, 66, 70);

        private readonly List<CredentialEntry> _allEntries;
        private readonly AppSettings _settings;
        private readonly Action _onSettingsUpdated;
        private IntPtr _previousForegroundWindow;
        private bool _isShowingPicker = false;
        private DateTime _openedAt = DateTime.MinValue;
        private Point _openCursorPos = Point.Empty;
        private string _targetProcessName = string.Empty;
        private string _targetWindowTitle = string.Empty;

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
            this.Size = new Size(460, 280);
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

            // Spacer between search and header
            pnlSearchSpacer = new Panel { Dock = DockStyle.Top, Height = 8 };

            // Custom styled header panel
            pnlHeader = new DoubleBufferedPanel
            {
                Dock = DockStyle.Top,
                Height = 26
            };
            pnlHeader.Paint += PnlHeader_Paint;

            // Spacer between header and results list ("distanzia intestazioni dalle righe")
            pnlHeaderSpacer = new Panel { Dock = DockStyle.Top, Height = 8 };

            // Results List
            lvResults = new PickerListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                HeaderStyle = ColumnHeaderStyle.None,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10f),
                Margin = new Padding(0)
            };

            // Uniform comfortable row height
            var imgList = new ImageList { ImageSize = new Size(1, 26) };
            lvResults.SmallImageList = imgList;

            colIndex = lvResults.Columns.Add("#", 44, HorizontalAlignment.Center);
            colTitle = lvResults.Columns.Add("Servizio / Titolo", 180);
            colUser = lvResults.Columns.Add("Utente / Email", 140);
            colCopy = lvResults.Columns.Add("Password", 76, HorizontalAlignment.Center);

            lvResults.DoubleClick += (s, e) => ExecuteSelected();
            lvResults.MouseClick += LvResults_MouseClick;
            lvResults.KeyDown += HandleKeyNavigation;

            // Spacer above hint
            pnlHintSpacer = new Panel { Dock = DockStyle.Bottom, Height = 6 };

            // Hint label
            lblHint = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                Text = "1-9: Inserisci   •   Ctrl+C: Copia   •   Invio: Compila   •   Esc: Annulla",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8.5f)
            };

            // Controls added in order so DockStyle takes proper positions:
            // Top dock order: txtSearch (topmost) -> pnlSearchSpacer -> pnlHeader -> pnlHeaderSpacer
            // Bottom dock order: lblHint (bottommost) -> pnlHintSpacer
            // Fill: lvResults
            pnlMain.Controls.Add(lvResults);
            pnlMain.Controls.Add(pnlHintSpacer);
            pnlMain.Controls.Add(lblHint);
            pnlMain.Controls.Add(pnlHeaderSpacer);
            pnlMain.Controls.Add(pnlHeader);
            pnlMain.Controls.Add(pnlSearchSpacer);
            pnlMain.Controls.Add(txtSearch);
            this.Controls.Add(pnlMain);
        }

        private void PnlHeader_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var rect = pnlHeader.ClientRectangle;
            if (rect.Width <= 0 || rect.Height <= 0) return;

            // Header background
            using (var brush = new SolidBrush(_headerBgColor))
            {
                g.FillRectangle(brush, rect);
            }

            // Subtle bottom border line
            using (var pen = new Pen(_headerBorderColor, 1f))
            {
                g.DrawLine(pen, 0, rect.Height - 1, rect.Width, rect.Height - 1);
            }

            int x0 = 0;
            int w0 = colIndex != null ? colIndex.Width : 42;

            int x1 = x0 + w0;
            int w1 = colTitle != null ? colTitle.Width : 180;

            int x2 = x1 + w1;
            int w2 = colUser != null ? colUser.Width : 140;

            int x3 = x2 + w2;
            int w3 = colCopy != null ? colCopy.Width : 72;

            using (var textBrush = new SolidBrush(_headerFgColor))
            using (var font = new Font("Segoe UI", 9f, FontStyle.Bold))
            {
                using (var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (var sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("#", font, textBrush, new Rectangle(x0, 0, w0, rect.Height), sfCenter);
                    g.DrawString("Servizio / Titolo", font, textBrush, new Rectangle(x1 + 6, 0, Math.Max(10, w1 - 8), rect.Height), sfLeft);
                    g.DrawString("Utente / Email", font, textBrush, new Rectangle(x2 + 6, 0, Math.Max(10, w2 - 8), rect.Height), sfLeft);
                    g.DrawString("Password", font, textBrush, new Rectangle(x3, 0, w3, rect.Height), sfCenter);
                }
            }
        }

        public void ShowPicker(IntPtr previousWindow)
        {
            _previousForegroundWindow = previousWindow;
            _openedAt = DateTime.Now;
            _isShowingPicker = true;
            _openCursorPos = Cursor.Position;

            // Detect target process name and window title for smart row auto-selection
            _targetProcessName = string.Empty;
            _targetWindowTitle = string.Empty;
            if (previousWindow != IntPtr.Zero)
            {
                try
                {
                    uint pid;
                    NativeMethods.GetWindowThreadProcessId(previousWindow, out pid);
                    if (pid > 0)
                    {
                        var proc = Process.GetProcessById((int)pid);
                        _targetProcessName = proc.ProcessName.ToLowerInvariant();
                        if (!_targetProcessName.EndsWith(".exe")) _targetProcessName += ".exe";
                    }

                    var sb = new System.Text.StringBuilder(256);
                    NativeMethods.GetWindowText(previousWindow, sb, sb.Capacity);
                    _targetWindowTitle = sb.ToString();
                }
                catch { }
            }

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
            if (pnlSearchSpacer != null) pnlSearchSpacer.BackColor = bgColor;
            if (pnlHeaderSpacer != null) pnlHeaderSpacer.BackColor = bgColor;
            if (pnlHintSpacer != null) pnlHintSpacer.BackColor = bgColor;

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

                _headerBgColor = Color.FromArgb(Math.Max(0, bgColor.R - 20), Math.Max(0, bgColor.G - 20), Math.Max(0, bgColor.B - 20));
                _headerFgColor = Color.FromArgb(40, 40, 40);
                _headerBorderColor = Color.FromArgb(Math.Max(0, bgColor.R - 40), Math.Max(0, bgColor.G - 40), Math.Max(0, bgColor.B - 40));
            }
            else
            {
                Color searchBg = Color.FromArgb(Math.Min(255, bgColor.R + 25), Math.Min(255, bgColor.G + 25), Math.Min(255, bgColor.B + 25));
                txtSearch.BackColor = searchBg;
                txtSearch.ForeColor = Color.White;

                lvResults.BackColor = bgColor;
                lvResults.ForeColor = Color.WhiteSmoke;
                lblHint.ForeColor = Color.FromArgb(190, 190, 190);

                _headerBgColor = Color.FromArgb(Math.Min(255, bgColor.R + 25), Math.Min(255, bgColor.G + 25), Math.Min(255, bgColor.B + 25));
                _headerFgColor = Color.FromArgb(220, 220, 225);
                _headerBorderColor = Color.FromArgb(Math.Min(255, bgColor.R + 45), Math.Min(255, bgColor.G + 45), Math.Min(255, bgColor.B + 45));
            }

            if (pnlHeader != null) pnlHeader.Invalidate();
        }

        private void PositionWindow()
        {
            Point refPoint = _openCursorPos != Point.Empty ? _openCursorPos : Cursor.Position;
            Screen screen = Screen.FromPoint(refPoint);
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
                x = refPoint.X - 45;
                y = refPoint.Y - 20;

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

            int colIndexW = 44;
            int colCopyW = 76;
            int paddingAndSlack = 40;

            int targetWidth = colIndexW + maxTitleW + maxUserW + colCopyW + paddingAndSlack;
            // Adaptive clamp: min 440px, max 680px
            targetWidth = Math.Max(440, Math.Min(680, targetWidth));

            // 2. Populate rows first so we can accurately measure row count
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

            // Auto-select row matching target process / window title, otherwise first row
            int selectedIndex = 0;
            if (string.IsNullOrEmpty(query) && (!string.IsNullOrEmpty(_targetProcessName) || !string.IsNullOrEmpty(_targetWindowTitle)))
            {
                for (int i = 0; i < matches.Count; i++)
                {
                    if (MatchesTargetProcess(matches[i], _targetProcessName, _targetWindowTitle))
                    {
                        selectedIndex = i;
                        break; // Autoseleziona la prima che matcha
                    }
                }
            }

            if (lvResults.Items.Count > 0 && selectedIndex < lvResults.Items.Count)
            {
                lvResults.Items[selectedIndex].Selected = true;
                lvResults.Items[selectedIndex].Focused = true;
                lvResults.EnsureVisible(selectedIndex);
            }

            lvResults.EndUpdate();

            // 3. Compute adaptive height based on items (max 8 visible items before scrollbar)
            int maxVisibleRows = 8;
            int rowCount = matches.Count;
            int itemH = 26; // Fixed uniform height via SmallImageList

            int visibleRows = Math.Max(1, Math.Min(rowCount, maxVisibleRows));
            // +12px buffer ensures Windows SysListView32 NEVER triggers an unwanted vertical scrollbar when rowCount <= 8
            int listHeight = (rowCount == 0) ? 36 : (visibleRows * itemH) + 12;

            int nonListHeight = pnlMain.Padding.Vertical + txtSearch.Height + pnlSearchSpacer.Height + pnlHeader.Height + pnlHeaderSpacer.Height + pnlHintSpacer.Height + lblHint.Height;
            int targetHeight = nonListHeight + listHeight;
            // Clamp height: min 150px, max 380px
            targetHeight = Math.Max(150, Math.Min(380, targetHeight));

            // Update size if either width or height changed
            bool sizeChanged = (this.Width != targetWidth || this.Height != targetHeight);
            if (sizeChanged)
            {
                this.Size = new Size(targetWidth, targetHeight);
                PositionWindow();
            }

            // 4. Distribute column widths inside client area
            int scrollbarW = (rowCount > maxVisibleRows) ? SystemInformation.VerticalScrollBarWidth : 0;
            int usableW = this.ClientSize.Width - pnlMain.Padding.Horizontal - scrollbarW - 8;
            int remainingUserW = usableW - (colIndexW + maxTitleW + colCopyW);

            colIndex.Width = colIndexW;
            colCopy.Width = colCopyW;
            colTitle.Width = maxTitleW;
            colUser.Width = Math.Max(80, remainingUserW);

            pnlHeader.Invalidate();
        }

        private static bool MatchesTargetProcess(CredentialEntry entry, string procName, string windowTitle)
        {
            if (entry == null) return false;

            // 1. Check AppFilter (e.g. "chrome.exe", "saplogon.exe", "GitHub")
            if (!string.IsNullOrWhiteSpace(entry.AppFilter))
            {
                string filter = entry.AppFilter.Trim();

                if (!string.IsNullOrEmpty(procName))
                {
                    string cleanProc = procName.ToLowerInvariant();
                    string cleanProcNoExt = cleanProc.EndsWith(".exe") ? cleanProc.Substring(0, cleanProc.Length - 4) : cleanProc;

                    string cleanFilter = filter.ToLowerInvariant();
                    string cleanFilterNoExt = cleanFilter.EndsWith(".exe") ? cleanFilter.Substring(0, cleanFilter.Length - 4) : cleanFilter;

                    if (cleanProc.Equals(cleanFilter, StringComparison.OrdinalIgnoreCase) ||
                        cleanProcNoExt.Equals(cleanFilterNoExt, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (cleanProc.IndexOf(cleanFilterNoExt, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cleanFilter.IndexOf(cleanProcNoExt, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }

                if (!string.IsNullOrEmpty(windowTitle))
                {
                    if (windowTitle.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            // 2. Fallback: check if entry Title matches process name (e.g. Title "Chrome" with proc "chrome.exe")
            if (!string.IsNullOrWhiteSpace(entry.Title) && !string.IsNullOrEmpty(procName))
            {
                string cleanProc = procName.ToLowerInvariant();
                string cleanProcNoExt = cleanProc.EndsWith(".exe") ? cleanProc.Substring(0, cleanProc.Length - 4) : cleanProc;
                string cleanTitle = entry.Title.Trim().ToLowerInvariant();

                if (cleanProcNoExt.Equals(cleanTitle, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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

    internal class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();
        }
    }

    internal class PickerListView : ListView
    {
        private const int SB_HORZ = 0;
        private const int WM_NCCALCSIZE = 0x0083;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int ShowScrollBar(IntPtr hWnd, int wBar, int bShow);

        public PickerListView()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCCALCSIZE)
            {
                ShowScrollBar(this.Handle, SB_HORZ, 0);
            }
            base.WndProc(ref m);
        }
    }
}

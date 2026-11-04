using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Custode2.Macro;

namespace Custode2.UI
{
    public class ProcessPickerResult
    {
        public string ProcessName { get; set; }
        public string WindowTitle { get; set; }
        public string ExecutablePath { get; set; }
        public IntPtr WindowHandle { get; set; }
    }

    public static class ProcessPickerHelper
    {
        private static IntPtr _mouseHook = IntPtr.Zero;
        private static IntPtr _kbHook = IntPtr.Zero;
        private static NativeMethods.HookProc _mouseHookProc;
        private static NativeMethods.HookProc _kbHookProc;

        private static HighlightOverlay _highlightOverlay;
        private static BadgeOverlay _badgeOverlay;
        private static Action<ProcessPickerResult> _completionCallback;
        private static Form _ownerForm;
        private static IntPtr _lastHoverRoot = IntPtr.Zero;

        public static void StartPicking(Form ownerForm, Action<ProcessPickerResult> onComplete)
        {
            StopPicking(); // Clean up any active session

            _ownerForm = ownerForm;
            _completionCallback = onComplete;
            _lastHoverRoot = IntPtr.Zero;

            // 1. Create transparent highlight frame
            _highlightOverlay = new HighlightOverlay();
            _highlightOverlay.Show();

            // 2. Create floating info badge
            _badgeOverlay = new BadgeOverlay();
            _badgeOverlay.Show();

            // 3. Set crosshair cursor
            Cursor.Current = Cursors.Cross;

            // 4. Install low-level hooks
            _mouseHookProc = MouseHookCallback;
            _kbHookProc = KeyboardHookCallback;

            IntPtr hMod = NativeMethods.GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName);
            _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseHookProc, hMod, 0);
            _kbHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _kbHookProc, hMod, 0);
        }

        private static void StopPicking()
        {
            if (_mouseHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }
            if (_kbHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_kbHook);
                _kbHook = IntPtr.Zero;
            }

            if (_highlightOverlay != null)
            {
                try { _highlightOverlay.Close(); _highlightOverlay.Dispose(); } catch { }
                _highlightOverlay = null;
            }

            if (_badgeOverlay != null)
            {
                try { _badgeOverlay.Close(); _badgeOverlay.Dispose(); } catch { }
                _badgeOverlay = null;
            }

            Cursor.Current = Cursors.Default;
            _lastHoverRoot = IntPtr.Zero;
        }

        private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();

                // Mouse move: update crosshair cursor, highlight border, and badge
                if (msg == NativeMethods.WM_MOUSEMOVE)
                {
                    Cursor.Current = Cursors.Cross;
                    var hookStruct = (NativeMethods.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.MSLLHOOKSTRUCT));
                    UpdateHover(hookStruct.pt.X, hookStruct.pt.Y);
                }
                // Left click: select target window!
                else if (msg == NativeMethods.WM_LBUTTONDOWN)
                {
                    var hookStruct = (NativeMethods.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.MSLLHOOKSTRUCT));
                    var result = CaptureWindowAt(hookStruct.pt.X, hookStruct.pt.Y);

                    var cb = _completionCallback;
                    StopPicking();

                    if (cb != null)
                    {
                        cb(result);
                    }

                    return (IntPtr)1; // Consume click so it doesn't trigger buttons in target application
                }
                // Right click: cancel
                else if (msg == NativeMethods.WM_RBUTTONDOWN)
                {
                    var cb = _completionCallback;
                    StopPicking();
                    if (cb != null) cb(null);
                    return (IntPtr)1;
                }
            }

            return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        private static IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam.ToInt32() == NativeMethods.WM_KEYDOWN)
            {
                var kbdStruct = (NativeMethods.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.KBDLLHOOKSTRUCT));
                // Escape key: cancel picking
                if (kbdStruct.vkCode == NativeMethods.VK_ESCAPE)
                {
                    var cb = _completionCallback;
                    StopPicking();
                    if (cb != null) cb(null);
                    return (IntPtr)1;
                }
            }

            return NativeMethods.CallNextHookEx(_kbHook, nCode, wParam, lParam);
        }

        private static void UpdateHover(int x, int y)
        {
            IntPtr root = FindRootWindowAt(x, y);

            if (root == IntPtr.Zero || IsOwnWindow(root))
            {
                if (_highlightOverlay != null && _highlightOverlay.Visible) _highlightOverlay.Hide();
                if (_badgeOverlay != null && _badgeOverlay.Visible) _badgeOverlay.Hide();
                _lastHoverRoot = IntPtr.Zero;
                return;
            }

            if (root != _lastHoverRoot)
            {
                _lastHoverRoot = root;
                NativeMethods.RECT rc;
                if (NativeMethods.GetWindowRect(root, out rc))
                {
                    if (_highlightOverlay != null)
                    {
                        _highlightOverlay.SetTargetBounds(rc.Left, rc.Top, rc.Width, rc.Height);
                        if (!_highlightOverlay.Visible) _highlightOverlay.Show();
                    }

                    string procName = GetProcessName(root);
                    string title = GetWindowTitle(root);

                    if (_badgeOverlay != null)
                    {
                        _badgeOverlay.SetInfo(procName, title, x + 18, y + 18);
                        if (!_badgeOverlay.Visible) _badgeOverlay.Show();
                    }
                }
            }
            else
            {
                // Just move badge with mouse
                if (_badgeOverlay != null && _badgeOverlay.Visible)
                {
                    _badgeOverlay.UpdatePosition(x + 18, y + 18);
                }
            }
        }

        private static ProcessPickerResult CaptureWindowAt(int x, int y)
        {
            IntPtr root = FindRootWindowAt(x, y);
            if (root == IntPtr.Zero || IsOwnWindow(root))
            {
                return null;
            }

            string procName = GetProcessName(root);
            string title = GetWindowTitle(root);
            string exePath = GetProcessPath(root);

            return new ProcessPickerResult
            {
                ProcessName = procName,
                WindowTitle = title,
                ExecutablePath = exePath,
                WindowHandle = root
            };
        }

        private static IntPtr FindRootWindowAt(int x, int y)
        {
            var pt = new NativeMethods.POINT { X = x, Y = y };
            IntPtr hwnd = NativeMethods.WindowFromPoint(pt);
            if (hwnd == IntPtr.Zero) return IntPtr.Zero;

            IntPtr root = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
            return root != IntPtr.Zero ? root : hwnd;
        }

        private static bool IsOwnWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;
            if (_ownerForm != null && _ownerForm.Handle == hWnd) return true;
            if (_highlightOverlay != null && _highlightOverlay.Handle == hWnd) return true;
            if (_badgeOverlay != null && _badgeOverlay.Handle == hWnd) return true;

            uint pid;
            NativeMethods.GetWindowThreadProcessId(hWnd, out pid);
            return pid == (uint)Process.GetCurrentProcess().Id;
        }

        private static string GetProcessName(IntPtr hWnd)
        {
            try
            {
                uint pid;
                NativeMethods.GetWindowThreadProcessId(hWnd, out pid);
                if (pid > 0)
                {
                    var proc = Process.GetProcessById((int)pid);
                    string name = proc.ProcessName.ToLowerInvariant();
                    return name.EndsWith(".exe") ? name : name + ".exe";
                }
            }
            catch { }
            return string.Empty;
        }

        private static string GetProcessPath(IntPtr hWnd)
        {
            try
            {
                uint pid;
                NativeMethods.GetWindowThreadProcessId(hWnd, out pid);
                if (pid > 0)
                {
                    var proc = Process.GetProcessById((int)pid);
                    return proc.MainModule != null ? proc.MainModule.FileName : string.Empty;
                }
            }
            catch { }
            return string.Empty;
        }

        private static string GetWindowTitle(IntPtr hWnd)
        {
            try
            {
                var sb = new StringBuilder(256);
                NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
                return sb.ToString();
            }
            catch { }
            return string.Empty;
        }

        // --- Visual Overlay 1: Glowing Highlight Border Frame ---
        private class HighlightOverlay : Form
        {
            public HighlightOverlay()
            {
                this.FormBorderStyle = FormBorderStyle.None;
                this.ShowInTaskbar = false;
                this.TopMost = true;
                this.StartPosition = FormStartPosition.Manual;
                this.BackColor = Color.Magenta;
                this.TransparencyKey = Color.Magenta;
                this.DoubleBuffered = true;
            }

            protected override CreateParams CreateParams
            {
                get
                {
                    var cp = base.CreateParams;
                    // WS_EX_TRANSPARENT (0x20) + WS_EX_LAYERED (0x80000) + WS_EX_TOOLWINDOW (0x80) + WS_EX_NOACTIVATE (0x08000000)
                    cp.ExStyle |= 0x00000020 | 0x00080000 | 0x00000080 | 0x08000000;
                    return cp;
                }
            }

            public void SetTargetBounds(int x, int y, int width, int height)
            {
                // Expand border 2px around window
                this.Location = new Point(x - 2, y - 2);
                this.Size = new Size(Math.Max(10, width + 4), Math.Max(10, height + 4));
                this.Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                using (var pen = new Pen(Color.FromArgb(0, 160, 255), 3.5f))
                {
                    e.Graphics.DrawRectangle(pen, 2, 2, this.Width - 4, this.Height - 4);
                }
            }
        }

        // --- Visual Overlay 2: Floating Tooltip Badge near cursor ---
        private class BadgeOverlay : Form
        {
            private string _procName = string.Empty;
            private string _title = string.Empty;

            public BadgeOverlay()
            {
                this.FormBorderStyle = FormBorderStyle.None;
                this.ShowInTaskbar = false;
                this.TopMost = true;
                this.StartPosition = FormStartPosition.Manual;
                this.BackColor = Color.FromArgb(28, 30, 34);
                this.ForeColor = Color.White;
                this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
                this.Size = new Size(240, 52);
                this.DoubleBuffered = true;
            }

            protected override CreateParams CreateParams
            {
                get
                {
                    var cp = base.CreateParams;
                    cp.ExStyle |= 0x00000020 | 0x00080000 | 0x00000080 | 0x08000000;
                    return cp;
                }
            }

            public void SetInfo(string procName, string title, int x, int y)
            {
                _procName = string.IsNullOrEmpty(procName) ? "Sconosciuto" : procName;
                _title = string.IsNullOrEmpty(title) ? "" : (title.Length > 35 ? title.Substring(0, 32) + "..." : title);

                UpdatePosition(x, y);
                this.Invalidate();
            }

            public void UpdatePosition(int x, int y)
            {
                Screen scr = Screen.FromPoint(new Point(x, y));
                Rectangle area = scr.WorkingArea;

                int posX = x;
                int posY = y;
                if (posX + this.Width > area.Right) posX = x - this.Width - 10;
                if (posY + this.Height > area.Bottom) posY = y - this.Height - 10;

                this.Location = new Point(posX, posY);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                // Border
                using (var pen = new Pen(Color.FromArgb(0, 160, 255), 1.5f))
                {
                    g.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
                }

                // Header icon and process name
                using (var fontBold = new Font("Segoe UI", 10f, FontStyle.Bold))
                using (var brushCyan = new SolidBrush(Color.FromArgb(80, 200, 255)))
                {
                    g.DrawString("🎯 " + _procName, fontBold, brushCyan, 8, 6);
                }

                // Title or prompt
                using (var fontSmall = new Font("Segoe UI", 8.5f))
                using (var brushGray = new SolidBrush(Color.FromArgb(200, 205, 210)))
                {
                    string sub = string.IsNullOrEmpty(_title) ? "Clic per selezionare • Esc per annullare" : _title;
                    g.DrawString(sub, fontSmall, brushGray, 8, 28);
                }
            }
        }
    }
}

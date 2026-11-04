using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Custode2.Macro;

namespace Custode2.Hotkeys
{
    public class GlobalHotkeyManager : IDisposable
    {
        private class HotkeyForm : Form
        {
            private readonly Action<int> _onHotkeyPressed;

            public HotkeyForm(Action<int> onHotkeyPressed)
            {
                _onHotkeyPressed = onHotkeyPressed;
                this.FormBorderStyle = FormBorderStyle.None;
                this.ShowInTaskbar = false;
                this.WindowState = FormWindowState.Minimized;
                this.Size = new System.Drawing.Size(0, 0);
                this.Opacity = 0;
                // Force Win32 window handle creation
                IntPtr h = this.Handle;
            }

            protected override void SetVisibleCore(bool value)
            {
                base.SetVisibleCore(false);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == NativeMethods.WM_HOTKEY)
                {
                    int id = m.WParam.ToInt32();
                    if (_onHotkeyPressed != null)
                    {
                        _onHotkeyPressed(id);
                    }
                }
                base.WndProc(ref m);
            }
        }

        private readonly HotkeyForm _form;
        private readonly Dictionary<int, string> _idToTag = new Dictionary<int, string>();
        private int _currentId = 1000;

        public event Action<string> HotkeyPressed;

        public GlobalHotkeyManager()
        {
            _form = new HotkeyForm(HandleHotkeyPressed);
        }

        private void HandleHotkeyPressed(int id)
        {
            string tag;
            if (_idToTag.TryGetValue(id, out tag))
            {
                if (HotkeyPressed != null)
                {
                    HotkeyPressed(tag);
                }
            }
        }

        public bool Register(string hotkeyString, string tag, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(hotkeyString)) return false;

            uint modifiers;
            uint vk;
            if (!TryParseHotkey(hotkeyString, out modifiers, out vk))
            {
                errorMessage = string.Format("Scorciatoia non valida: '{0}'", hotkeyString);
                return false;
            }

            int id = ++_currentId;

            // Try first with MOD_NOREPEAT
            bool success = NativeMethods.RegisterHotKey(_form.Handle, id, modifiers | NativeMethods.MOD_NOREPEAT, vk);
            if (!success)
            {
                // Fallback without MOD_NOREPEAT for compatibility
                success = NativeMethods.RegisterHotKey(_form.Handle, id, modifiers, vk);
            }

            if (!success)
            {
                errorMessage = string.Format("Impossibile registrare '{0}' (il tasto è già in uso da un'altra applicazione).", hotkeyString);
                return false;
            }

            _idToTag[id] = tag;
            return true;
        }

        public void UnregisterAll()
        {
            foreach (var kvp in _idToTag)
            {
                NativeMethods.UnregisterHotKey(_form.Handle, kvp.Key);
            }
            _idToTag.Clear();
        }

        public static bool TryParseHotkey(string hotkeyStr, out uint modifiers, out uint vk)
        {
            modifiers = 0;
            vk = 0;

            if (string.IsNullOrWhiteSpace(hotkeyStr)) return false;

            string[] tokens = hotkeyStr.Split(new char[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            Keys key = Keys.None;

            foreach (var rawToken in tokens)
            {
                string token = rawToken.Trim();

                if (token.Equals("CTRL", StringComparison.OrdinalIgnoreCase) || token.Equals("CONTROL", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= NativeMethods.MOD_CONTROL;
                }
                else if (token.Equals("ALT", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= NativeMethods.MOD_ALT;
                }
                else if (token.Equals("SHIFT", StringComparison.OrdinalIgnoreCase) || token.Equals("MAIUSC", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= NativeMethods.MOD_SHIFT;
                }
                else if (token.Equals("WIN", StringComparison.OrdinalIgnoreCase) || token.Equals("WINDOWS", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= NativeMethods.MOD_WIN;
                }
                else
                {
                    // 1. Single character check (Digits and Letters)
                    if (token.Length == 1)
                    {
                        char c = char.ToUpperInvariant(token[0]);
                        if (c >= '0' && c <= '9')
                        {
                            key = Keys.D0 + (c - '0');
                        }
                        else if (c >= 'A' && c <= 'Z')
                        {
                            key = (Keys)c;
                        }
                    }
                    // 2. Named special keys
                    else if (token.Equals("SPACE", StringComparison.OrdinalIgnoreCase) || token.Equals("SPAZIO", StringComparison.OrdinalIgnoreCase))
                    {
                        key = Keys.Space;
                    }
                    else if (token.Equals("ENTER", StringComparison.OrdinalIgnoreCase) || token.Equals("INVIO", StringComparison.OrdinalIgnoreCase) || token.Equals("RETURN", StringComparison.OrdinalIgnoreCase))
                    {
                        key = Keys.Enter;
                    }
                    else if (token.Equals("TAB", StringComparison.OrdinalIgnoreCase))
                    {
                        key = Keys.Tab;
                    }
                    else if (token.Equals("ESC", StringComparison.OrdinalIgnoreCase) || token.Equals("ESCAPE", StringComparison.OrdinalIgnoreCase))
                    {
                        key = Keys.Escape;
                    }
                    // 3. Fallback to Enum.Parse (for F1..F12, Home, End, etc.)
                    else
                    {
                        try
                        {
                            key = (Keys)Enum.Parse(typeof(Keys), token, true);
                        }
                        catch
                        {
                            // Ignored
                        }
                    }
                }
            }

            if (key == Keys.None)
            {
                return false;
            }

            vk = (uint)key;
            return true;
        }

        public void Dispose()
        {
            UnregisterAll();
            if (_form != null && !_form.IsDisposed)
            {
                _form.Dispose();
            }
        }
    }
}

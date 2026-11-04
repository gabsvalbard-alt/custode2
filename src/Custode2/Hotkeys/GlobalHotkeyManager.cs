using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Custode2.Macro;

namespace Custode2.Hotkeys
{
    public class GlobalHotkeyManager : IDisposable
    {
        private class HotkeyWindow : NativeWindow
        {
            private readonly Action<int> _onHotkeyPressed;

            public HotkeyWindow(Action<int> onHotkeyPressed)
            {
                _onHotkeyPressed = onHotkeyPressed;
                var cp = new CreateParams
                {
                    Caption = "Custode2_HotkeySink",
                    Parent = IntPtr.Zero
                };
                CreateHandle(cp);
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

        private readonly HotkeyWindow _window;
        private readonly Dictionary<int, string> _idToTag = new Dictionary<int, string>();
        private int _currentId = 1000;

        public event Action<string> HotkeyPressed;

        public GlobalHotkeyManager()
        {
            _window = new HotkeyWindow(HandleHotkeyPressed);
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
            bool success = NativeMethods.RegisterHotKey(_window.Handle, id, modifiers | NativeMethods.MOD_NOREPEAT, vk);
            if (!success)
            {
                errorMessage = string.Format("Impossibile registrare '{0}' (già in uso da un'altra applicazione).", hotkeyString);
                return false;
            }

            _idToTag[id] = tag;
            return true;
        }

        public void UnregisterAll()
        {
            foreach (var kvp in _idToTag)
            {
                NativeMethods.UnregisterHotKey(_window.Handle, kvp.Key);
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
                    // Attempt key parsing
                    if (token.Equals("SPACE", StringComparison.OrdinalIgnoreCase) || token.Equals("SPAZIO", StringComparison.OrdinalIgnoreCase))
                    {
                        key = Keys.Space;
                    }
                    else if (token.Equals("ENTER", StringComparison.OrdinalIgnoreCase) || token.Equals("INVIO", StringComparison.OrdinalIgnoreCase))
                    {
                        key = Keys.Enter;
                    }
                    else
                    {
                        try
                        {
                            key = (Keys)Enum.Parse(typeof(Keys), token, true);
                        }
                        catch
                        {
                            if (token.Length == 1)
                            {
                                char c = char.ToUpperInvariant(token[0]);
                                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                                {
                                    key = (Keys)c;
                                }
                            }
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
            if (_window.Handle != IntPtr.Zero)
            {
                _window.DestroyHandle();
            }
        }
    }
}
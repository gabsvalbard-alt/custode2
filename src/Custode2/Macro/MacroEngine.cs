using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Custode2.Models;
using Custode2.Security;
using Custode2.UI;

namespace Custode2.Macro
{
    public static class MacroEngine
    {
        private enum ActionType
        {
            TypeUnicodeText,
            TypePassword,
            PressVirtualKey,
            Delay
        }

        private class MacroAction
        {
            public ActionType Type { get; set; }
            public string Text { get; set; }
            public ushort VirtualKey { get; set; }
            public int DelayMs { get; set; }
        }

        public static void Execute(CredentialEntry entry, IntPtr targetHwnd, AppSettings settings, Action onSettingsUpdated)
        {
            if (entry == null) return;
            if (settings == null) settings = new AppSettings();

            // Run macro execution on a background thread so UI doesn't hang
            var thread = new Thread(() =>
            {
                try
                {
                    // 1. Restore target window focus
                    if (targetHwnd != IntPtr.Zero)
                    {
                        NativeMethods.SetForegroundWindow(targetHwnd);
                        NativeMethods.BringWindowToTop(targetHwnd);
                        Thread.Sleep(120);
                    }

                    // 2. Parse template tokens
                    string template = string.IsNullOrEmpty(entry.Template) ? settings.DefaultTemplate : entry.Template;
                    List<MacroAction> actions = ParseTemplate(template, entry);

                    // 3. Execute actions in sequence
                    foreach (var action in actions)
                    {
                        switch (action.Type)
                        {
                            case ActionType.TypeUnicodeText:
                                SendUnicodeText(action.Text, settings.InterKeyDelayMs);
                                break;

                            case ActionType.PressVirtualKey:
                                SendVirtualKey(action.VirtualKey, settings.InterKeyDelayMs);
                                break;

                            case ActionType.Delay:
                                Thread.Sleep(action.DelayMs);
                                break;

                            case ActionType.TypePassword:
                                // Pre-flight security inspection before sending the password!
                                if (!VerifyPasswordSecurity(entry, targetHwnd, settings, onSettingsUpdated))
                                {
                                    // User aborted! Stop macro execution immediately
                                    return;
                                }

                                // If confirmed or whitelisted, ensure target window is still in foreground
                                if (targetHwnd != IntPtr.Zero)
                                {
                                    NativeMethods.SetForegroundWindow(targetHwnd);
                                    Thread.Sleep(80);
                                }

                                SendUnicodeText(entry.Password ?? string.Empty, settings.InterKeyDelayMs);
                                break;
                        }
                    }

                    // Update last used timestamp
                    entry.LastUsedAt = DateTime.Now;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Errore esecuzione Macro: " + ex.Message);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        private static bool VerifyPasswordSecurity(CredentialEntry entry, IntPtr targetHwnd, AppSettings settings, Action onSettingsUpdated)
        {
            string details;
            var detection = PasswordFieldDetector.Inspect(targetHwnd, entry, settings, out details);

            if (detection == PasswordDetectionResult.ConfirmedPassword || detection == PasswordDetectionResult.Whitelisted)
            {
                return true;
            }

            // Uncertain or plain text: show confirmation dialog to the user
            bool sendAnyway = false;
            bool remember = false;
            string processName = GetProcessName(targetHwnd);

            // Since we are in background thread, invoke dialog synchronously
            using (var form = new SecurityConfirmDialog(entry.Title, processName, details))
            {
                var result = form.ShowDialog();
                if (result == DialogResult.OK)
                {
                    sendAnyway = true;
                    remember = form.RememberChoice;
                }
            }

            if (sendAnyway && remember && !string.IsNullOrEmpty(processName))
            {
                if (settings.WhitelistedProcesses == null)
                    settings.WhitelistedProcesses = new List<string>();

                if (!settings.WhitelistedProcesses.Contains(processName.ToLowerInvariant()))
                {
                    settings.WhitelistedProcesses.Add(processName.ToLowerInvariant());
                    if (onSettingsUpdated != null)
                    {
                        onSettingsUpdated();
                    }
                }
            }

            return sendAnyway;
        }

        private static string GetProcessName(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return string.Empty;
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

        private static List<MacroAction> ParseTemplate(string template, CredentialEntry entry)
        {
            var actions = new List<MacroAction>();
            if (string.IsNullOrEmpty(template)) return actions;

            // Regex matches tokens like $USER$, $PASS$, $TAB$, $ENTER$, $INVIO$, $SPACE$, $ESC$, $DELAY(100)$
            var regex = new Regex(@"(\$[A-Za-z0-9_]+(?:\(\d+\))?\$)", RegexOptions.Compiled);
            string[] parts = regex.Split(template);

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                if (part.StartsWith("$") && part.EndsWith("$"))
                {
                    string tokenUpper = part.ToUpperInvariant();

                    if (tokenUpper == "$USER$")
                    {
                        actions.Add(new MacroAction { Type = ActionType.TypeUnicodeText, Text = entry.Username ?? string.Empty });
                    }
                    else if (tokenUpper == "$PASS$")
                    {
                        actions.Add(new MacroAction { Type = ActionType.TypePassword });
                    }
                    else if (tokenUpper == "$TAB$")
                    {
                        actions.Add(new MacroAction { Type = ActionType.PressVirtualKey, VirtualKey = NativeMethods.VK_TAB });
                    }
                    else if (tokenUpper == "$ENTER$" || tokenUpper == "$INVIO$")
                    {
                        actions.Add(new MacroAction { Type = ActionType.PressVirtualKey, VirtualKey = NativeMethods.VK_RETURN });
                    }
                    else if (tokenUpper == "$SPACE$" || tokenUpper == "$SPAZIO$")
                    {
                        actions.Add(new MacroAction { Type = ActionType.PressVirtualKey, VirtualKey = NativeMethods.VK_SPACE });
                    }
                    else if (tokenUpper == "$ESC$" || tokenUpper == "$ESCAPE$")
                    {
                        actions.Add(new MacroAction { Type = ActionType.PressVirtualKey, VirtualKey = NativeMethods.VK_ESCAPE });
                    }
                    else if (tokenUpper.StartsWith("$DELAY(") && tokenUpper.EndsWith(")$"))
                    {
                        string numStr = part.Substring(7, part.Length - 9);
                        int ms;
                        if (int.TryParse(numStr, out ms) && ms > 0)
                        {
                            actions.Add(new MacroAction { Type = ActionType.Delay, DelayMs = ms });
                        }
                    }
                    else
                    {
                        // Unknown token, type literally
                        actions.Add(new MacroAction { Type = ActionType.TypeUnicodeText, Text = part });
                    }
                }
                else
                {
                    // Plain text between tokens
                    actions.Add(new MacroAction { Type = ActionType.TypeUnicodeText, Text = part });
                }
            }

            return actions;
        }

        private static void SendUnicodeText(string text, int interKeyDelayMs)
        {
            if (string.IsNullOrEmpty(text)) return;

            foreach (char c in text)
            {
                var inputs = new NativeMethods.INPUT[2];

                // Key down
                inputs[0].type = NativeMethods.INPUT_KEYBOARD;
                inputs[0].u.ki.wVk = 0;
                inputs[0].u.ki.wScan = c;
                inputs[0].u.ki.dwFlags = NativeMethods.KEYEVENTF_UNICODE;
                inputs[0].u.ki.time = 0;
                inputs[0].u.ki.dwExtraInfo = IntPtr.Zero;

                // Key up
                inputs[1].type = NativeMethods.INPUT_KEYBOARD;
                inputs[1].u.ki.wVk = 0;
                inputs[1].u.ki.wScan = c;
                inputs[1].u.ki.dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP;
                inputs[1].u.ki.time = 0;
                inputs[1].u.ki.dwExtraInfo = IntPtr.Zero;

                NativeMethods.SendInput(2, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));

                if (interKeyDelayMs > 0)
                {
                    Thread.Sleep(interKeyDelayMs);
                }
            }
        }

        private static void SendVirtualKey(ushort vk, int interKeyDelayMs)
        {
            var inputs = new NativeMethods.INPUT[2];

            // Key down
            inputs[0].type = NativeMethods.INPUT_KEYBOARD;
            inputs[0].u.ki.wVk = vk;
            inputs[0].u.ki.wScan = 0;
            inputs[0].u.ki.dwFlags = 0;
            inputs[0].u.ki.time = 0;
            inputs[0].u.ki.dwExtraInfo = IntPtr.Zero;

            // Key up
            inputs[1].type = NativeMethods.INPUT_KEYBOARD;
            inputs[1].u.ki.wVk = vk;
            inputs[1].u.ki.wScan = 0;
            inputs[1].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;
            inputs[1].u.ki.time = 0;
            inputs[1].u.ki.dwExtraInfo = IntPtr.Zero;

            NativeMethods.SendInput(2, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));

            if (interKeyDelayMs > 0)
            {
                Thread.Sleep(interKeyDelayMs);
            }
        }
    }
}
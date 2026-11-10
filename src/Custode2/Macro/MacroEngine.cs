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
            Execute(entry, targetHwnd, settings, null, onSettingsUpdated);
        }

        public static void Execute(CredentialEntry entry, IntPtr targetHwnd, AppSettings settings, VaultData vault, Action onSettingsUpdated)
        {
            if (entry == null) return;
            if (settings == null) settings = new AppSettings();

            // Run macro execution on a background thread so UI doesn't hang
            var thread = new Thread(() =>
            {
                try
                {
                    // 0. Pre-flight check: Application Blacklist (Blocks insertion regardless of credential settings)
                    string processName = GetProcessName(targetHwnd);
                    string windowTitle = GetWindowTitle(targetHwnd);
                    if (IsProcessBlacklisted(processName, settings))
                    {
                        MessageBox.Show(
                            string.Format("L'inserimento delle credenziali per '{0}' è stato BLOCCATO!\n\nL'applicazione attiva '{1}' è inclusa nella Blacklist di sicurezza (es. chat/messaggistica).\nL'inserimento è stato interrotto per prevenire la diffusione accidentale delle credenziali.",
                                entry.Title, string.IsNullOrEmpty(processName) ? "Sconosciuta" : processName),
                            "Custode 2.0 - Applicazione in Blacklist",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Stop);
                        return;
                    }

                    // 0.5. Pre-flight check: Process Mismatch Warning
                    // If credential has an AppFilter configured, but the active process is different:
                    if (!string.IsNullOrEmpty(entry.AppFilter) && !DoesProcessMatchFilter(processName, windowTitle, entry.AppFilter))
                    {
                        bool proceedAnyway = false;
                        using (var form = SecurityConfirmDialog.CreateProcessMismatch(entry.Title, entry.AppFilter, processName, windowTitle))
                        {
                            if (form.ShowDialog() == DialogResult.OK)
                            {
                                proceedAnyway = true;
                            }
                        }

                        if (!proceedAnyway)
                        {
                            // User aborted due to process mismatch
                            return;
                        }

                        // Refocus target window after dialog closes
                        if (targetHwnd != IntPtr.Zero)
                        {
                            NativeMethods.SetForegroundWindow(targetHwnd);
                            NativeMethods.BringWindowToTop(targetHwnd);
                            Thread.Sleep(80);
                        }
                    }

                    // 1. Restore target window focus
                    if (targetHwnd != IntPtr.Zero)
                    {
                        NativeMethods.SetForegroundWindow(targetHwnd);
                        NativeMethods.BringWindowToTop(targetHwnd);
                        Thread.Sleep(120);
                    }

                    // 2. Pre-flight check: Is an editable field focused?
                    string nonEditableReason;
                    if (!PasswordFieldDetector.IsTargetEditable(targetHwnd, entry, settings, out nonEditableReason))
                    {
                        bool forceAnyway = false;
                        bool remember = false;
                        using (var form = new SecurityConfirmDialog(entry.Title, processName, nonEditableReason, isEditableWarning: true))
                        {
                            if (form.ShowDialog() == DialogResult.OK)
                            {
                                forceAnyway = true;
                                remember = form.RememberChoice;
                            }
                        }

                        if (!forceAnyway)
                        {
                            // User aborted because no editable field was found
                            return;
                        }

                        if (remember)
                        {
                            entry.BypassSecurityCheck = true;

                            if (!string.IsNullOrEmpty(processName))
                            {
                                if (settings.WhitelistedProcesses == null) settings.WhitelistedProcesses = new List<string>();
                                if (!settings.WhitelistedProcesses.Contains(processName.ToLowerInvariant()))
                                {
                                    settings.WhitelistedProcesses.Add(processName.ToLowerInvariant());
                                }
                            }

                            if (onSettingsUpdated != null) onSettingsUpdated();
                        }

                        // Refocus target window after dialog closes
                        if (targetHwnd != IntPtr.Zero)
                        {
                            NativeMethods.SetForegroundWindow(targetHwnd);
                            Thread.Sleep(80);
                        }
                    }

                    // Target PID for continuous active focus guard
                    uint targetPid = 0;
                    if (targetHwnd != IntPtr.Zero)
                    {
                        NativeMethods.GetWindowThreadProcessId(targetHwnd, out targetPid);
                    }

                    // 3. Parse template tokens
                    string template = string.IsNullOrEmpty(entry.Template) ? settings.DefaultTemplate : entry.Template;
                    List<MacroAction> actions = ParseTemplate(template, entry, vault);

                    // 4. Execute actions in sequence with Active Focus Guard
                    foreach (var action in actions)
                    {
                        // Check if the user switched windows before this action
                        if (!IsTargetActive(targetHwnd, targetPid))
                        {
                            TrayAppContext.ShowNotification(
                                "Custode 2.0 - Inserimento Interrotto",
                                "Hai cambiato finestra durante la digitazione. La macro è stata arrestata per sicurezza.",
                                ToolTipIcon.Warning);
                            return;
                        }

                        switch (action.Type)
                        {
                            case ActionType.TypeUnicodeText:
                                if (!SendUnicodeText(action.Text, settings.InterKeyDelayMs, targetHwnd, targetPid))
                                    return;
                                break;

                            case ActionType.PressVirtualKey:
                                if (!SendVirtualKey(action.VirtualKey, settings.InterKeyDelayMs, targetHwnd, targetPid))
                                    return;
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

                                // Verify focus guard right before typing password!
                                if (!IsTargetActive(targetHwnd, targetPid))
                                {
                                    TrayAppContext.ShowNotification(
                                        "Custode 2.0 - Password Non Inviata",
                                        "Finestra cambiata prima dell'invio della password. Inserimento arrestato per sicurezza.",
                                        ToolTipIcon.Warning);
                                    return;
                                }

                                string passwordToType = entry.ResolvePassword(vault);
                                if (!SendUnicodeText(passwordToType, settings.InterKeyDelayMs, targetHwnd, targetPid))
                                    return;
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

            if (sendAnyway && remember)
            {
                entry.BypassSecurityCheck = true;

                if (!string.IsNullOrEmpty(processName))
                {
                    if (settings.WhitelistedProcesses == null)
                        settings.WhitelistedProcesses = new List<string>();

                    if (!settings.WhitelistedProcesses.Contains(processName.ToLowerInvariant()))
                    {
                        settings.WhitelistedProcesses.Add(processName.ToLowerInvariant());
                    }
                }

                if (onSettingsUpdated != null)
                {
                    onSettingsUpdated();
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

        private static string GetWindowTitle(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return string.Empty;
            try
            {
                var sb = new System.Text.StringBuilder(256);
                NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
                return sb.ToString();
            }
            catch { }
            return string.Empty;
        }

        public static bool DoesProcessMatchFilter(string processName, string windowTitle, string appFilter)
        {
            if (string.IsNullOrEmpty(appFilter)) return true;
            return CredentialEntry.IsFilterMatch(appFilter, processName, windowTitle);
        }

        private static bool IsProcessBlacklisted(string processName, AppSettings settings)
        {
            if (string.IsNullOrEmpty(processName) || settings == null || settings.BlacklistedProcesses == null)
            {
                return false;
            }

            string cleanProc = processName.Trim().ToLowerInvariant();
            if (!cleanProc.EndsWith(".exe")) cleanProc += ".exe";

            foreach (var bp in settings.BlacklistedProcesses)
            {
                if (string.IsNullOrEmpty(bp)) continue;
                string cleanBp = bp.Trim().ToLowerInvariant();
                if (!cleanBp.EndsWith(".exe")) cleanBp += ".exe";

                if (cleanProc.Equals(cleanBp, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<MacroAction> ParseTemplate(string template, CredentialEntry entry, VaultData vault = null)
        {
            var actions = new List<MacroAction>();
            if (string.IsNullOrEmpty(template)) return actions;

            // Regex matches tokens like $USER$, <USER>, [USER], {USER}, $TAB$, <TAB>, [TAB], $ENTER$, <INVIO>, $DELAY(100)$, <JOLLY(Nome)>, etc.
            var regex = new Regex(@"(\$[A-Za-z0-9_]+(?:\([^$]+\))?\$|<[A-Za-z0-9_]+(?:\([^>]+\))?>|\[[A-Za-z0-9_]+(?:\([^\]]+\))?\]|\{[A-Za-z0-9_]+(?:\([^}]+\))?\})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            string[] parts = regex.Split(template);

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                char start = part[0];
                char end = part[part.Length - 1];
                bool isToken = (start == '$' && end == '$') ||
                               (start == '<' && end == '>') ||
                               (start == '[' && end == ']') ||
                               (start == '{' && end == '}');

                if (isToken && part.Length >= 3)
                {
                    string inner = part.Substring(1, part.Length - 2).Trim().ToUpperInvariant();

                    if (inner == "USER" || inner == "USERNAME" || inner == "UTENTE")
                    {
                        actions.Add(new MacroAction { Type = ActionType.TypeUnicodeText, Text = entry.Username ?? string.Empty });
                    }
                    else if (inner == "PASS" || inner == "PASSWORD")
                    {
                        actions.Add(new MacroAction { Type = ActionType.TypePassword });
                    }
                    else if (inner.StartsWith("JOLLY(") && inner.EndsWith(")"))
                    {
                        string jollyName = inner.Substring(6, inner.Length - 7).Trim();
                        string jollyVal = string.Empty;
                        if (vault != null && vault.SharedPasswords != null)
                        {
                            var sp = vault.SharedPasswords.Find(p => p.Name != null && p.Name.Equals(jollyName, StringComparison.OrdinalIgnoreCase));
                            if (sp != null) jollyVal = sp.Value ?? string.Empty;
                        }
                        actions.Add(new MacroAction { Type = ActionType.TypeUnicodeText, Text = jollyVal });
                    }
                    else if (inner == "TAB" || inner == "TABULATORE")
                    {
                        actions.Add(new MacroAction { Type = ActionType.PressVirtualKey, VirtualKey = NativeMethods.VK_TAB });
                    }
                    else if (inner == "ENTER" || inner == "INVIO" || inner == "RETURN")
                    {
                        actions.Add(new MacroAction { Type = ActionType.PressVirtualKey, VirtualKey = NativeMethods.VK_RETURN });
                    }
                    else if (inner == "SPACE" || inner == "SPAZIO" || inner == "BLANK" || inner == "BARRA")
                    {
                        actions.Add(new MacroAction { Type = ActionType.PressVirtualKey, VirtualKey = NativeMethods.VK_SPACE });
                    }
                    else if (inner == "ESC" || inner == "ESCAPE")
                    {
                        actions.Add(new MacroAction { Type = ActionType.PressVirtualKey, VirtualKey = NativeMethods.VK_ESCAPE });
                    }
                    else if (inner == "UP" || inner == "SU" || inner == "FRECCIA_SU" || inner == "ARROWUP")
                    {
                        actions.Add(new MacroAction { Type = ActionType.PressVirtualKey, VirtualKey = NativeMethods.VK_UP });
                    }
                    else if (inner == "DOWN" || inner == "GIU" || inner == "GIÙ" || inner == "FRECCIA_GIU" || inner == "ARROWDOWN")
                    {
                        actions.Add(new MacroAction { Type = ActionType.PressVirtualKey, VirtualKey = NativeMethods.VK_DOWN });
                    }
                    else if (inner == "LEFT" || inner == "SINISTRA" || inner == "FRECCIA_SINISTRA" || inner == "ARROWLEFT")
                    {
                        actions.Add(new MacroAction { Type = ActionType.PressVirtualKey, VirtualKey = NativeMethods.VK_LEFT });
                    }
                    else if (inner == "RIGHT" || inner == "DESTRA" || inner == "FRECCIA_DESTRA" || inner == "ARROWRIGHT")
                    {
                        actions.Add(new MacroAction { Type = ActionType.PressVirtualKey, VirtualKey = NativeMethods.VK_RIGHT });
                    }
                    else if (inner.StartsWith("DELAY(") && inner.EndsWith(")"))
                    {
                        string numStr = inner.Substring(6, inner.Length - 7);
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

        private static bool SendUnicodeText(string text, int interKeyDelayMs, IntPtr targetHwnd, uint targetPid)
        {
            if (string.IsNullOrEmpty(text)) return true;

            foreach (char c in text)
            {
                // Continuous Active Focus Guard: check if the user switched windows in the middle of typing!
                if (!IsTargetActive(targetHwnd, targetPid))
                {
                    TrayAppContext.ShowNotification(
                        "Custode 2.0 - Inserimento Interrotto",
                        "Rilevato cambio finestra durante la digitazione. La macro è stata arrestata per sicurezza.",
                        ToolTipIcon.Warning);
                    return false;
                }

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

                uint sent = NativeMethods.SendInput(2, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
                if (sent == 0)
                {
                    int err = Marshal.GetLastWin32Error();
                    NotifySendInputFailure(targetHwnd, err);
                    return false;
                }

                if (interKeyDelayMs > 0)
                {
                    Thread.Sleep(interKeyDelayMs);
                }
            }

            return true;
        }

        private static bool SendVirtualKey(ushort vk, int interKeyDelayMs, IntPtr targetHwnd, uint targetPid)
        {
            // Continuous Active Focus Guard
            if (!IsTargetActive(targetHwnd, targetPid))
            {
                TrayAppContext.ShowNotification(
                    "Custode 2.0 - Inserimento Interrotto",
                    "Rilevato cambio finestra durante la digitazione. La macro è stata arrestata per sicurezza.",
                    ToolTipIcon.Warning);
                return false;
            }

            var inputs = new NativeMethods.INPUT[2];

            uint flagsDown = 0;
            uint flagsUp = NativeMethods.KEYEVENTF_KEYUP;

            if (vk == NativeMethods.VK_UP || vk == NativeMethods.VK_DOWN ||
                vk == NativeMethods.VK_LEFT || vk == NativeMethods.VK_RIGHT)
            {
                flagsDown |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
                flagsUp |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
            }

            // Key down
            inputs[0].type = NativeMethods.INPUT_KEYBOARD;
            inputs[0].u.ki.wVk = vk;
            inputs[0].u.ki.wScan = 0;
            inputs[0].u.ki.dwFlags = flagsDown;
            inputs[0].u.ki.time = 0;
            inputs[0].u.ki.dwExtraInfo = IntPtr.Zero;

            // Key up
            inputs[1].type = NativeMethods.INPUT_KEYBOARD;
            inputs[1].u.ki.wVk = vk;
            inputs[1].u.ki.wScan = 0;
            inputs[1].u.ki.dwFlags = flagsUp;
            inputs[1].u.ki.time = 0;
            inputs[1].u.ki.dwExtraInfo = IntPtr.Zero;

            uint sent = NativeMethods.SendInput(2, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
            if (sent == 0)
            {
                int err = Marshal.GetLastWin32Error();
                NotifySendInputFailure(targetHwnd, err);
                return false;
            }

            if (interKeyDelayMs > 0)
            {
                Thread.Sleep(interKeyDelayMs);
            }

            return true;
        }

        private static bool IsTargetActive(IntPtr targetHwnd, uint targetPid)
        {
            if (targetHwnd == IntPtr.Zero && targetPid == 0) return true;

            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;

            // Direct match with target window
            if (fg == targetHwnd) return true;

            // Match with any child window or dialog belonging to the same process
            if (targetPid > 0)
            {
                uint fgPid;
                NativeMethods.GetWindowThreadProcessId(fg, out fgPid);
                if (fgPid == targetPid) return true;
            }

            return false;
        }

        private static void NotifySendInputFailure(IntPtr targetHwnd, int err)
        {
            string procName = GetProcessName(targetHwnd);
            if (err == 5) // ERROR_ACCESS_DENIED (UIPI block)
            {
                TrayAppContext.ShowNotification(
                    "Custode 2.0 - Blocco Permessi (UIPI)",
                    string.Format("La finestra '{0}' richiede privilegi di Amministratore.\nAvvia Custode come Amministratore per poter digitare in questa finestra.",
                        string.IsNullOrEmpty(procName) ? "destinazione" : procName),
                    ToolTipIcon.Error);
            }
            else
            {
                TrayAppContext.ShowNotification(
                    "Custode 2.0 - Errore di Scrittura",
                    string.Format("Windows non ha accettato l'invio dei tasti (errore Win32: {0}).", err),
                    ToolTipIcon.Warning);
            }
        }
    }
}
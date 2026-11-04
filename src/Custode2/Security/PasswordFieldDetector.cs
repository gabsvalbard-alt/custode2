using System;
using System.Diagnostics;
using System.Text;
using System.Windows.Automation;
using Custode2.Macro;
using Custode2.Models;

namespace Custode2.Security
{
    public enum PasswordDetectionResult
    {
        ConfirmedPassword,
        Whitelisted,
        Uncertain
    }

    public static class PasswordFieldDetector
    {
        public static PasswordDetectionResult Inspect(IntPtr targetHwnd, CredentialEntry entry, AppSettings settings, out string details)
        {
            details = string.Empty;

            // 1. Check if Entry explicitly has security check bypassed
            if (entry != null && entry.BypassSecurityCheck)
            {
                details = "Verifica saltata: abilitata opzione 'Fidati' sulla credenziale.";
                return PasswordDetectionResult.Whitelisted;
            }

            // 2. Identify target Process name & Window title
            string processName = string.Empty;
            string windowTitle = string.Empty;

            try
            {
                if (targetHwnd != IntPtr.Zero)
                {
                    uint pid;
                    NativeMethods.GetWindowThreadProcessId(targetHwnd, out pid);
                    if (pid > 0)
                    {
                        var proc = Process.GetProcessById((int)pid);
                        processName = proc.ProcessName.ToLowerInvariant();
                        if (!processName.EndsWith(".exe"))
                        {
                            processName += ".exe";
                        }
                    }

                    var sbTitle = new StringBuilder(256);
                    NativeMethods.GetWindowText(targetHwnd, sbTitle, sbTitle.Capacity);
                    windowTitle = sbTitle.ToString();
                }
            }
            catch
            {
                // Process inspection might fail for system processes, ignore
            }

            // 3. Process Whitelist Check (e.g. saplogon.exe, sapgui.exe, or entry's specific AppFilter)
            if (settings != null && settings.WhitelistedProcesses != null && !string.IsNullOrEmpty(processName))
            {
                foreach (var wp in settings.WhitelistedProcesses)
                {
                    if (string.Equals(wp.Trim(), processName, StringComparison.OrdinalIgnoreCase))
                    {
                        details = string.Format("Applicazione in whitelist di sistema: '{0}'", processName);
                        return PasswordDetectionResult.Whitelisted;
                    }
                }
            }

            if (entry != null && !string.IsNullOrEmpty(entry.AppFilter))
            {
                string filter = entry.AppFilter.Trim();
                if ((!string.IsNullOrEmpty(processName) && processName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(windowTitle) && windowTitle.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    details = string.Format("Corrispondenza con filtro credenziale: '{0}'", filter);
                    return PasswordDetectionResult.Whitelisted;
                }
            }

            // 4. Level 1: Windows UI Automation inspection (Browser, WPF, WinUI, modern apps)
            try
            {
                var focused = AutomationElement.FocusedElement;
                if (focused != null)
                {
                    object isPassObj = focused.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty, true);
                    if (isPassObj is bool && (bool)isPassObj)
                    {
                        details = string.Format("Campo password confermato via UI Automation ({0})", focused.Current.ControlType.ProgrammaticName);
                        return PasswordDetectionResult.ConfirmedPassword;
                    }
                }
            }
            catch
            {
                // UI Automation might throw for elevated/unresponsive windows, proceed to Win32 checks
            }

            // 5. Level 2: Win32 API inspection for native Edit controls
            if (targetHwnd != IntPtr.Zero)
            {
                try
                {
                    uint dummyPid;
                    uint targetThreadId = NativeMethods.GetWindowThreadProcessId(targetHwnd, out dummyPid);
                    uint currentThreadId = NativeMethods.GetCurrentThreadId();

                    IntPtr hFocus = IntPtr.Zero;
                    bool attached = false;

                    if (targetThreadId != currentThreadId)
                    {
                        attached = NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, true);
                    }

                    try
                    {
                        hFocus = NativeMethods.GetFocus();
                    }
                    finally
                    {
                        if (attached)
                        {
                            NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, false);
                        }
                    }

                    if (hFocus != IntPtr.Zero)
                    {
                        // Check ES_PASSWORD style
                        long style = NativeMethods.GetWindowLong(hFocus, NativeMethods.GWL_STYLE).ToInt64();
                        if ((style & NativeMethods.ES_PASSWORD) != 0)
                        {
                            details = "Campo password confermato via stile Win32 ES_PASSWORD";
                            return PasswordDetectionResult.ConfirmedPassword;
                        }

                        // Check EM_GETPASSWORDCHAR message
                        IntPtr passChar = NativeMethods.SendMessage(hFocus, NativeMethods.EM_GETPASSWORDCHAR, IntPtr.Zero, IntPtr.Zero);
                        if (passChar != IntPtr.Zero)
                        {
                            details = "Campo password confermato via messaggio Win32 EM_GETPASSWORDCHAR";
                            return PasswordDetectionResult.ConfirmedPassword;
                        }

                        // Check class name for known password-like patterns
                        var sbClass = new StringBuilder(128);
                        NativeMethods.GetClassName(hFocus, sbClass, sbClass.Capacity);
                        string className = sbClass.ToString();
                        if (className.IndexOf("Password", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            details = string.Format("Classe controllo password rilevata: '{0}'", className);
                            return PasswordDetectionResult.ConfirmedPassword;
                        }
                    }
                }
                catch
                {
                    // Fallthrough to uncertain
                }
            }

            // 6. Level 3: SAP GUI Heuristics
            // If class name or process is SAP but not caught above
            if (!string.IsNullOrEmpty(processName) && (processName.Contains("sap") || windowTitle.Contains("SAP")))
            {
                details = string.Format("Rilevato ambiente SAP ('{0}').", processName);
                // Return uncertain to trigger confirmation or prompt user to whitelist
                return PasswordDetectionResult.Uncertain;
            }

            details = string.IsNullOrEmpty(processName) 
                ? "Impossibile identificare con certezza se il controllo attivo è mascherato." 
                : string.Format("Finestra '{0}': il controllo con focus non espone la proprietà password.", processName);

            return PasswordDetectionResult.Uncertain;
        }
    }
}
using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Custode2.Security
{
    public static class StartupHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "Custode2";

        public static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key != null)
                    {
                        object val = key.GetValue(AppName);
                        return val != null;
                    }
                }
            }
            catch
            {
                // Ignore registry read errors
            }
            return false;
        }

        public static void SetStartupEnabled(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            string exePath = "\"" + Application.ExecutablePath + "\"";
                            key.SetValue(AppName, exePath);
                        }
                        else
                        {
                            key.DeleteValue(AppName, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Errore durante la configurazione dell'avvio automatico: " + ex.Message, "Custode 2.0", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}


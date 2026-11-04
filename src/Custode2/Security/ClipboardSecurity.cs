using System;
using System.Windows.Forms;

namespace Custode2.Security
{
    public static class ClipboardSecurity
    {
        private static Timer _clearTimer;
        private static string _lastCopiedPassword;
        public static event Action ClipboardCleared;

        public static void CopyPassword(string password, int clearSeconds)
        {
            if (string.IsNullOrEmpty(password)) return;

            try
            {
                Clipboard.SetText(password);
                _lastCopiedPassword = password;

                if (_clearTimer != null)
                {
                    _clearTimer.Stop();
                    _clearTimer.Dispose();
                    _clearTimer = null;
                }

                int intervalMs = Math.Max(3, clearSeconds) * 1000;
                _clearTimer = new Timer();
                _clearTimer.Interval = intervalMs;
                _clearTimer.Tick += (s, e) =>
                {
                    _clearTimer.Stop();
                    _clearTimer.Dispose();
                    _clearTimer = null;

                    try
                    {
                        if (Clipboard.ContainsText() && Clipboard.GetText() == _lastCopiedPassword)
                        {
                            Clipboard.Clear();
                            if (ClipboardCleared != null)
                            {
                                ClipboardCleared();
                            }
                        }
                    }
                    catch
                    {
                        // Ignore clipboard access errors
                    }
                    finally
                    {
                        _lastCopiedPassword = null;
                    }
                };

                _clearTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Impossibile copiare negli appunti: " + ex.Message, "Custode 2.0", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}

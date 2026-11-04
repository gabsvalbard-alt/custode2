using System;
using System.Threading;
using System.Windows.Forms;
using Custode2.UI;

namespace Custode2
{
    static class Program
    {
        private static Mutex _singleInstanceMutex;

        [STAThread]
        static void Main()
        {
            // Enforce single instance per Windows user session
            const string mutexName = "CustodeDelleChiavi2_SingleInstanceMutex";
            bool createdNew;

            _singleInstanceMutex = new Mutex(true, mutexName, out createdNew);

            if (!createdNew)
            {
                MessageBox.Show(
                    "Custode delle chiavi 2.0 è già in esecuzione in background!\nControlla le icone nell'area di notifica (System Tray) vicino all'orologio di Windows.",
                    "Custode delle chiavi 2.0",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayAppContext());
            }
            finally
            {
                if (_singleInstanceMutex != null)
                {
                    _singleInstanceMutex.ReleaseMutex();
                    _singleInstanceMutex.Dispose();
                }
            }
        }
    }
}


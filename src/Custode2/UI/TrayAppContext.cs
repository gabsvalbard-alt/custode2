using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Custode2.Hotkeys;
using Custode2.Macro;
using Custode2.Models;
using Custode2.Security;

namespace Custode2.UI
{
    public class TrayAppContext : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private readonly GlobalHotkeyManager _hotkeyManager;
        private VaultData _vault;
        private MainForm _mainForm;
        private QuickPickerForm _pickerForm;
        private bool _hotkeysSuspended = false;
        private ToolStripMenuItem _menuSuspend;
        private static TrayAppContext _instance;

        public TrayAppContext()
        {
            _instance = this;

            // 1. Load Vault using DPAPI
            try
            {
                _vault = DpapiStorage.LoadVault();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Errore durante l'apertura del vault: " + ex.Message, "Custode 2.0", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _vault = new VaultData();
            }

            // 2. Initialize Tray Icon
            _trayIcon = new NotifyIcon
            {
                Icon = GetAppIcon(),
                Text = "Custode delle chiavi 2.0 (In esecuzione)",
                Visible = true
            };

            // 3. Build Tray Menu
            var contextMenu = new ContextMenuStrip();
            var menuPicker = new ToolStripMenuItem("🔍 Ricerca Rapida", null, (s, e) => OpenQuickPicker(IntPtr.Zero));
            menuPicker.Font = new Font(contextMenu.Font, FontStyle.Bold);

            var menuManager = new ToolStripMenuItem("🔑 Gestione Credenziali...", null, (s, e) => ShowMainForm());
            _menuSuspend = new ToolStripMenuItem("⏸️ Sospendi Scorciatoie", null, (s, e) => ToggleSuspendHotkeys());
            var menuExit = new ToolStripMenuItem("❌ Esci", null, (s, e) => ExitApplication());

            contextMenu.Items.Add(menuPicker);
            contextMenu.Items.Add(menuManager);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(_menuSuspend);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(menuExit);

            _trayIcon.ContextMenuStrip = contextMenu;
            _trayIcon.DoubleClick += (s, e) => ShowMainForm();

            // 4. Initialize Hotkey Manager
            _hotkeyManager = new GlobalHotkeyManager();
            _hotkeyManager.HotkeyPressed += OnHotkeyPressed;

            // 5. Clipboard security notification
            ClipboardSecurity.ClipboardCleared += () =>
            {
                _trayIcon.ShowBalloonTip(1500, "Custode 2.0", "Password rimossa automaticamente dagli appunti.", ToolTipIcon.Info);
            };

            RegisterAllHotkeys();

            // Notify user of background startup if enabled
            if (_vault.Settings.ShowStartupNotification)
            {
                string tipMsg = string.Format("In esecuzione in background.\nPremi {0} per la ricerca rapida.", _vault.Settings.GlobalPickerHotkey);
                _trayIcon.ShowBalloonTip(2000, "Custode 2.0", tipMsg, ToolTipIcon.Info);
            }
        }

        private void RegisterAllHotkeys()
        {
            if (_hotkeysSuspended) return;

            _hotkeyManager.UnregisterAll();

            string pickerErr;
            // 1. Global Picker Hotkey
            if (!string.IsNullOrWhiteSpace(_vault.Settings.GlobalPickerHotkey))
            {
                if (!_hotkeyManager.Register(_vault.Settings.GlobalPickerHotkey, "__PICKER__", out pickerErr))
                {
                    _trayIcon.ShowBalloonTip(4000, "Custode 2.0 - Scorciatoia non attiva",
                        string.Format("Impossibile registrare '{0}':\n{1}\nPuoi cambiarla dalle Impostazioni.", _vault.Settings.GlobalPickerHotkey, pickerErr),
                        ToolTipIcon.Warning);
                }
            }

            // 2. Direct entry hotkeys
            foreach (var entry in _vault.Entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.DirectHotkey))
                {
                    string entryErr;
                    _hotkeyManager.Register(entry.DirectHotkey, entry.Id, out entryErr);
                }
            }
        }

        private void OnHotkeyPressed(string tag)
        {
            if (_hotkeysSuspended) return;

            // Capture the target window immediately before opening any picker or dialog
            IntPtr currentTargetHwnd = NativeMethods.GetForegroundWindow();

            if (tag == "__PICKER__")
            {
                OpenQuickPicker(currentTargetHwnd);
            }
            else
            {
                // Direct Entry Hotkey
                var entry = _vault.Entries.FirstOrDefault(e => e.Id == tag);
                if (entry != null)
                {
                    MacroEngine.Execute(entry, currentTargetHwnd, _vault.Settings, _vault, SaveVault);
                }
            }
        }

        private void OpenQuickPicker(IntPtr targetHwnd)
        {
            if (targetHwnd == IntPtr.Zero)
            {
                targetHwnd = NativeMethods.GetForegroundWindow();
            }

            if (_pickerForm == null || _pickerForm.IsDisposed)
            {
                _pickerForm = new QuickPickerForm(_vault, SaveVault);
            }

            _pickerForm.ShowPicker(targetHwnd);
        }

        private void ShowMainForm()
        {
            if (_mainForm == null || _mainForm.IsDisposed)
            {
                _mainForm = new MainForm(_vault, SaveVault, RegisterAllHotkeys, ExitApplication);
            }
            _mainForm.Show();
            _mainForm.WindowState = FormWindowState.Normal;
            _mainForm.Activate();
        }

        private void ToggleSuspendHotkeys()
        {
            _hotkeysSuspended = !_hotkeysSuspended;
            if (_hotkeysSuspended)
            {
                _hotkeyManager.UnregisterAll();
                _menuSuspend.Text = "▶️ Riattiva Scorciatoie";
                _trayIcon.Text = "Custode 2.0 (Scorciatoie Sospese)";
            }
            else
            {
                RegisterAllHotkeys();
                _menuSuspend.Text = "⏸️ Sospendi Scorciatoie";
                _trayIcon.Text = "Custode delle chiavi 2.0 (In esecuzione)";
            }
        }

        private void SaveVault()
        {
            try
            {
                DpapiStorage.SaveVault(_vault);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Errore salvataggio vault: " + ex.Message, "Custode 2.0", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExitApplication()
        {
            SaveVault();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _hotkeyManager.Dispose();
            Application.Exit();
        }

        public static Icon GetAppIcon()
        {
            try
            {
                Icon exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (exeIcon != null) return exeIcon;
            }
            catch { }

            return CreateTrayIcon();
        }

        private static Icon CreateTrayIcon()
        {
            // Generate a crisp, elegant key icon programmatically
            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // Background circular badge
                using (var brush = new SolidBrush(Color.FromArgb(0, 120, 215)))
                {
                    g.FillEllipse(brush, 1, 1, 30, 30);
                }

                // Key graphic in white
                using (var pen = new Pen(Color.White, 2.5f))
                {
                    // Key head (circle)
                    g.DrawEllipse(pen, 7, 7, 9, 9);
                    // Key stem
                    g.DrawLine(pen, 15, 14, 24, 23);
                    // Key teeth
                    g.DrawLine(pen, 20, 19, 23, 16);
                    g.DrawLine(pen, 23, 22, 26, 19);
                }

                IntPtr hIcon = bmp.GetHicon();
                return Icon.FromHandle(hIcon);
            }
        }

        public static void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            try
            {
                if (_instance != null && _instance._trayIcon != null)
                {
                    _instance._trayIcon.ShowBalloonTip(3500, title, message, icon);
                }
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_trayIcon != null) _trayIcon.Dispose();
                if (_hotkeyManager != null) _hotkeyManager.Dispose();
                if (_mainForm != null) _mainForm.Dispose();
                if (_pickerForm != null) _pickerForm.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Custode2.UI
{
    public enum SecurityDialogType
    {
        PasswordSecurity,
        NonEditableField,
        ProcessMismatch
    }

    public class SecurityConfirmDialog : Form
    {
        private Label lblWarning;
        private Label lblDetails;
        private CheckBox chkRememberProcess;
        private Button btnAction;
        private Button btnCancel;

        public bool RememberChoice
        {
            get { return chkRememberProcess != null && chkRememberProcess.Checked; }
        }

        public SecurityConfirmDialog(string entryTitle, string processName, string details, bool isEditableWarning = false)
            : this(entryTitle, processName, details, isEditableWarning ? SecurityDialogType.NonEditableField : SecurityDialogType.PasswordSecurity)
        {
        }

        public SecurityConfirmDialog(string entryTitle, string processName, string details, SecurityDialogType dialogType, string expectedApp = null, string windowTitle = null)
        {
            InitializeComponent(entryTitle, processName, details, dialogType, expectedApp, windowTitle);
        }

        public static SecurityConfirmDialog CreateProcessMismatch(string entryTitle, string expectedApp, string actualProcess, string windowTitle)
        {
            return new SecurityConfirmDialog(entryTitle, actualProcess, null, SecurityDialogType.ProcessMismatch, expectedApp, windowTitle);
        }

        private void InitializeComponent(string entryTitle, string processName, string details, SecurityDialogType dialogType, string expectedApp, string windowTitle)
        {
            string formTitle;
            string warningTitle;
            Color warningColor;
            string detailBody;
            string actionText;
            Color actionColor;

            string detailMsg = string.IsNullOrEmpty(details) ? "" : string.Format("Dettagli: {0}\n", details);

            switch (dialogType)
            {
                case SecurityDialogType.NonEditableField:
                    formTitle = "Custode 2.0 - Nessun Campo Editabile Rilevato";
                    warningTitle = string.Format("Attenzione: nessun campo editabile per '{0}'", entryTitle);
                    warningColor = Color.FromArgb(190, 80, 0);
                    detailBody = "Il cursore non sembra trovarsi in un campo in cui è possibile scrivere (es. sfondo pagina, pulsante o controllo non testuale).\n\n" +
                                 detailMsg +
                                 "Se prosegui, i tasti digitati potrebbero andare persi o attivare scorciatoie involontarie.\n\nVuoi forzare comunque l'inserimento?";
                    actionText = "Forza Inserimento";
                    actionColor = Color.FromArgb(220, 100, 20);
                    break;

                case SecurityDialogType.ProcessMismatch:
                    formTitle = "Custode 2.0 - Processo Non Corrispondente";
                    warningTitle = string.Format("Attenzione: applicazione diversa per '{0}'", entryTitle);
                    warningColor = Color.FromArgb(190, 80, 0);
                    detailBody = string.Format(
                        "Questa credenziale è configurata specificamente per l'applicazione:\n🎯  {0}\n\n" +
                        "La finestra attualmente attiva appartiene invece a:\n⚠️  {1}{2}\n\n" +
                        "Vuoi procedere comunque con l'inserimento in questa finestra?",
                        expectedApp,
                        string.IsNullOrEmpty(processName) ? "Sconosciuta" : processName,
                        string.IsNullOrEmpty(windowTitle) ? "" : " (\"" + windowTitle + "\")");
                    actionText = "Procedi Comunque";
                    actionColor = Color.FromArgb(220, 100, 20);
                    break;

                case SecurityDialogType.PasswordSecurity:
                default:
                    formTitle = "Custode 2.0 - Controllo di Sicurezza Password";
                    warningTitle = string.Format("Attenzione: invio password per '{0}'", entryTitle);
                    warningColor = Color.FromArgb(180, 40, 20);
                    detailBody = "Il campo attivo nella finestra target non è stato identificato con certezza come campo password mascherato (es. potrebbe essere una chat o un campo testo in chiaro).\n\n" +
                                 detailMsg +
                                 "\nVuoi inviare comunque la password?";
                    actionText = "Invia Comunque";
                    actionColor = Color.FromArgb(220, 53, 69);
                    break;
            }

            this.Text = formTitle;
            try { this.Icon = TrayAppContext.GetAppIcon(); } catch { }
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.TopMost = true;
            this.ShowInTaskbar = true;
            this.BackColor = Color.FromArgb(248, 249, 250);
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            var iconBox = new PictureBox
            {
                Image = SystemIcons.Warning.ToBitmap(),
                SizeMode = PictureBoxSizeMode.CenterImage,
                Location = new Point(20, 20),
                Size = new Size(48, 48)
            };

            int left = 80;
            int maxContentWidth = 500;

            var fontWarning = new Font("Segoe UI", 11f, FontStyle.Bold);
            var fontDetails = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            Size warningSize = TextRenderer.MeasureText(
                warningTitle,
                fontWarning,
                new Size(maxContentWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);

            lblWarning = new Label
            {
                Text = warningTitle,
                Font = fontWarning,
                ForeColor = warningColor,
                Location = new Point(left, 20),
                Size = new Size(maxContentWidth, Math.Max(26, warningSize.Height + 4)),
                AutoSize = false
            };

            int detailsY = lblWarning.Bottom + 12;

            Size detailsSize = TextRenderer.MeasureText(
                detailBody,
                fontDetails,
                new Size(maxContentWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);

            lblDetails = new Label
            {
                Text = detailBody,
                Font = fontDetails,
                ForeColor = Color.FromArgb(40, 45, 50),
                Location = new Point(left, detailsY),
                Size = new Size(maxContentWidth, detailsSize.Height + 12),
                AutoSize = false
            };

            int nextY = lblDetails.Bottom + 16;

            if (dialogType != SecurityDialogType.ProcessMismatch)
            {
                chkRememberProcess = new CheckBox
                {
                    Text = string.IsNullOrEmpty(processName)
                        ? "Fidati sempre di questa applicazione per le prossime volte"
                        : string.Format("Fidati sempre dell'applicazione '{0}'", processName),
                    Location = new Point(left, nextY),
                    Size = new Size(maxContentWidth, 26),
                    Checked = false
                };
                this.Controls.Add(chkRememberProcess);
                nextY = chkRememberProcess.Bottom + 20;
            }
            else
            {
                nextY += 8;
            }

            int btnWidth = dialogType == SecurityDialogType.NonEditableField ? 150 : 145;
            btnAction = new Button
            {
                Text = actionText,
                DialogResult = DialogResult.OK,
                Location = new Point(left + maxContentWidth - btnWidth - 115, nextY),
                Size = new Size(btnWidth, 34),
                BackColor = actionColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnAction.FlatAppearance.BorderSize = 0;

            btnCancel = new Button
            {
                Text = "Annulla",
                DialogResult = DialogResult.Cancel,
                Location = new Point(left + maxContentWidth - 105, nextY),
                Size = new Size(105, 34),
                BackColor = Color.FromArgb(230, 233, 236),
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            this.Controls.Add(iconBox);
            this.Controls.Add(lblWarning);
            this.Controls.Add(lblDetails);
            this.Controls.Add(btnAction);
            this.Controls.Add(btnCancel);

            // Focus on Cancel button by default for safety on warnings
            this.AcceptButton = btnAction;
            this.CancelButton = btnCancel;

            if (dialogType == SecurityDialogType.ProcessMismatch)
            {
                this.Shown += (s, e) => btnCancel.Focus();
            }

            // Dynamically calculate form client dimensions so nothing is ever clipped
            this.ClientSize = new Size(left + maxContentWidth + 25, btnCancel.Bottom + 20);
        }
    }
}
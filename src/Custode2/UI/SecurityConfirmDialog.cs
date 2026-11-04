using System;
using System.Drawing;
using System.Windows.Forms;

namespace Custode2.UI
{
    public class SecurityConfirmDialog : Form
    {
        private Label lblWarning;
        private Label lblDetails;
        private CheckBox chkRememberProcess;
        private Button btnSendAnyway;
        private Button btnCancel;

        public bool RememberChoice
        {
            get { return chkRememberProcess != null && chkRememberProcess.Checked; }
        }

        public SecurityConfirmDialog(string entryTitle, string processName, string details, bool isEditableWarning = false)
        {
            InitializeComponent(entryTitle, processName, details, isEditableWarning);
        }

        private void InitializeComponent(string entryTitle, string processName, string details, bool isEditableWarning)
        {
            this.Text = isEditableWarning 
                ? "Custode 2.0 - Nessun Campo Editabile Rilevato" 
                : "Custode 2.0 - Controllo di Sicurezza Password";

            try { this.Icon = TrayAppContext.GetAppIcon(); } catch { }
            this.Size = new Size(580, 290);
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

            lblWarning = new Label
            {
                Text = isEditableWarning
                    ? string.Format("Attenzione: nessun campo editabile per '{0}'", entryTitle)
                    : string.Format("Attenzione: invio password per '{0}'", entryTitle),
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = isEditableWarning ? Color.FromArgb(190, 80, 0) : Color.FromArgb(180, 40, 20),
                Location = new Point(80, 20),
                Size = new Size(460, 25)
            };

            string detailMsg = string.IsNullOrEmpty(details) ? "" : string.Format("Dettagli: {0}\n", details);
            lblDetails = new Label
            {
                Text = isEditableWarning
                    ? "Il cursore non sembra trovarsi in un campo in cui è possibile scrivere (es. sfondo pagina, pulsante o controllo non testuale).\n\n" +
                      detailMsg +
                      "Se prosegui, i tasti digitati potrebbero andare persi o attivare scorciatoie involontarie.\nVuoi forzare comunque l'inserimento?"
                    : "Il campo attivo nella finestra target non è stato identificato con certezza come campo password mascherato (es. potrebbe essere una chat o un campo testo in chiaro).\n\n" +
                      detailMsg +
                      "Vuoi inviare comunque la password?",
                Location = new Point(80, 50),
                Size = new Size(460, 95)
            };

            chkRememberProcess = new CheckBox
            {
                Text = string.IsNullOrEmpty(processName) 
                    ? "Fidati sempre di questa applicazione per le prossime volte" 
                    : string.Format("Fidati sempre dell'applicazione '{0}'", processName),
                Location = new Point(80, 155),
                Size = new Size(460, 24),
                Checked = false
            };

            btnSendAnyway = new Button
            {
                Text = isEditableWarning ? "Forza Inserimento" : "Invia Comunque",
                DialogResult = DialogResult.OK,
                Location = new Point(275, 195),
                Size = new Size(145, 32),
                BackColor = isEditableWarning ? Color.FromArgb(220, 100, 20) : Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSendAnyway.FlatAppearance.BorderSize = 0;

            btnCancel = new Button
            {
                Text = "Annulla",
                DialogResult = DialogResult.Cancel,
                Location = new Point(430, 195),
                Size = new Size(110, 32),
                BackColor = Color.FromArgb(230, 230, 230),
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            this.Controls.Add(iconBox);
            this.Controls.Add(lblWarning);
            this.Controls.Add(lblDetails);
            this.Controls.Add(chkRememberProcess);
            this.Controls.Add(btnSendAnyway);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnSendAnyway;
            this.CancelButton = btnCancel;
        }
    }
}
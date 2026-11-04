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

        public SecurityConfirmDialog(string entryTitle, string processName, string details)
        {
            InitializeComponent(entryTitle, processName, details);
        }

        private void InitializeComponent(string entryTitle, string processName, string details)
        {
            this.Text = "Custode 2.0 - Controllo di Sicurezza Password";
            this.Size = new Size(520, 260);
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
                Text = string.Format("Attenzione: invio password per '{0}'", entryTitle),
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 40, 20),
                Location = new Point(80, 20),
                Size = new Size(400, 25)
            };

            string detailMsg = string.IsNullOrEmpty(details) ? "" : string.Format("Dettagli: {0}\n", details);
            lblDetails = new Label
            {
                Text = "Il campo attivo nella finestra target non è stato identificato con certezza come campo password mascherato (es. potrebbe essere una chat o un campo testo in chiaro).\n\n" +
                       detailMsg +
                       "Vuoi inviare comunque la password?",
                Location = new Point(80, 50),
                Size = new Size(410, 85)
            };

            chkRememberProcess = new CheckBox
            {
                Text = string.IsNullOrEmpty(processName) 
                    ? "Fidati sempre di questa applicazione per le prossime volte" 
                    : string.Format("Fidati sempre dell'applicazione '{0}'", processName),
                Location = new Point(80, 145),
                Size = new Size(410, 24),
                Checked = false
            };

            btnSendAnyway = new Button
            {
                Text = "Invia Comunque",
                DialogResult = DialogResult.OK,
                Location = new Point(240, 180),
                Size = new Size(130, 32),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSendAnyway.FlatAppearance.BorderSize = 0;

            btnCancel = new Button
            {
                Text = "Annulla",
                DialogResult = DialogResult.Cancel,
                Location = new Point(380, 180),
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
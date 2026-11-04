using System;
using System.Collections.Generic;

namespace Custode2.Models
{
    [Serializable]
    public class AppSettings
    {
        public string GlobalPickerHotkey { get; set; }
        public string DefaultTemplate { get; set; }
        public int InterKeyDelayMs { get; set; }
        public bool StartWithWindows { get; set; }
        public bool AutoRestorePreviousWindow { get; set; }
        public List<string> WhitelistedProcesses { get; set; }
        public List<string> BlacklistedProcesses { get; set; }

        // Popup customization
        public int PickerOpacity { get; set; }
        public string PickerBackgroundColor { get; set; }
        public bool PickerRandomColor { get; set; }
        public string PickerPositionMode { get; set; }
        public int ClipboardClearSeconds { get; set; }
        public int PickerTimeoutSeconds { get; set; }
        public bool ShowStartupNotification { get; set; }

        public AppSettings()
        {
            GlobalPickerHotkey = "Ctrl+Alt+Space";
            DefaultTemplate = "$USER$<TAB>$PASS$<ENTER>";
            InterKeyDelayMs = 15;
            StartWithWindows = false;
            AutoRestorePreviousWindow = true;
            ShowStartupNotification = true;
            WhitelistedProcesses = new List<string>
            {
                "saplogon.exe",
                "sapgui.exe",
                "mstsc.exe"
            };

            BlacklistedProcesses = new List<string>
            {
                "teams.exe",
                "ms-teams.exe",
                "slack.exe",
                "discord.exe",
                "whatsapp.exe",
                "telegram.exe"
            };

            PickerOpacity = 95;
            PickerBackgroundColor = "#202124";
            PickerRandomColor = false;
            PickerPositionMode = "Cursor";
            ClipboardClearSeconds = 15;
            PickerTimeoutSeconds = 30;
        }
    }
}

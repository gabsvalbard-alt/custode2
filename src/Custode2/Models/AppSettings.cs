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

        public AppSettings()
        {
            GlobalPickerHotkey = "Ctrl+Alt+Space";
            DefaultTemplate = "$USER$<TAB>$PASS$<ENTER>";
            InterKeyDelayMs = 15;
            StartWithWindows = false;
            AutoRestorePreviousWindow = true;
            WhitelistedProcesses = new List<string>
            {
                "saplogon.exe",
                "sapgui.exe",
                "mstsc.exe"
            };
        }
    }
}
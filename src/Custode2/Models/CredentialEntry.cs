using System;

namespace Custode2.Models
{
    [Serializable]
    public class CredentialEntry
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string SharedPasswordId { get; set; }
        public string Template { get; set; }
        public string DirectHotkey { get; set; }
        public string AppFilter { get; set; }
        public bool BypassSecurityCheck { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }

        public bool UsesSharedPassword
        {
            get { return !string.IsNullOrEmpty(SharedPasswordId); }
        }

        public string ResolvePassword(VaultData vault)
        {
            if (!string.IsNullOrEmpty(SharedPasswordId) && vault != null && vault.SharedPasswords != null)
            {
                var sp = vault.SharedPasswords.Find(p => p.Id == SharedPasswordId);
                if (sp != null)
                {
                    return sp.Value ?? string.Empty;
                }
            }
            return Password ?? string.Empty;
        }

        public string GetSharedPasswordName(VaultData vault)
        {
            if (!string.IsNullOrEmpty(SharedPasswordId) && vault != null && vault.SharedPasswords != null)
            {
                var sp = vault.SharedPasswords.Find(p => p.Id == SharedPasswordId);
                if (sp != null)
                {
                    return sp.Name ?? string.Empty;
                }
            }
            return string.Empty;
        }

        public CredentialEntry()
        {
            Id = Guid.NewGuid().ToString("N");
            Title = string.Empty;
            Username = string.Empty;
            Password = string.Empty;
            SharedPasswordId = string.Empty;
            Template = "$USER$<TAB>$PASS$<ENTER>";
            DirectHotkey = string.Empty;
            AppFilter = string.Empty;
            BypassSecurityCheck = false;
            Notes = string.Empty;
            CreatedAt = DateTime.Now;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Username) ? Title : string.Format("{0} ({1})", Title, Username);
        }

        /// <summary>
        /// Checks whether the entry's AppFilter matches the specified target process or window title.
        /// Rules:
        /// 1. If AppFilter ends with ".exe", it requires an EXACT, case-insensitive match on the process name.
        /// 2. If AppFilter does NOT end with ".exe", it acts in LIKE mode (case-insensitive substring on process or window title).
        /// </summary>
        public bool MatchesProcess(string processName, string windowTitle)
        {
            return IsFilterMatch(this.AppFilter, processName, windowTitle);
        }

        public static bool IsFilterMatch(string appFilter, string processName, string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(appFilter)) return false;

            string filter = appFilter.Trim();
            if (string.IsNullOrEmpty(filter)) return false;

            string cleanProc = string.IsNullOrEmpty(processName) ? string.Empty : processName.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(cleanProc) && !cleanProc.EndsWith(".exe"))
            {
                cleanProc += ".exe";
            }

            // RULE 1: If filter ends with ".exe", it MUST match the process name exactly
            if (filter.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(cleanProc, filter, StringComparison.OrdinalIgnoreCase);
            }

            // RULE 2: If filter does NOT end with ".exe", LIKE mode (substring of process name or window title)
            if (!string.IsNullOrEmpty(cleanProc))
            {
                string procNoExe = cleanProc.EndsWith(".exe") ? cleanProc.Substring(0, cleanProc.Length - 4) : cleanProc;
                if (cleanProc.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    procNoExe.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(windowTitle))
            {
                if (windowTitle.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
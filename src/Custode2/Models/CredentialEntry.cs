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
        public string Template { get; set; }
        public string DirectHotkey { get; set; }
        public string AppFilter { get; set; }
        public bool BypassSecurityCheck { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }

        public CredentialEntry()
        {
            Id = Guid.NewGuid().ToString("N");
            Title = string.Empty;
            Username = string.Empty;
            Password = string.Empty;
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
    }
}
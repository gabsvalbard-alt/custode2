using System;
using System.Collections.Generic;

namespace Custode2.Models
{
    [Serializable]
    public class VaultData
    {
        public int Version { get; set; }
        public AppSettings Settings { get; set; }
        public List<CredentialEntry> Entries { get; set; }

        public VaultData()
        {
            Version = 2;
            Settings = new AppSettings();
            Entries = new List<CredentialEntry>();
        }
    }
}
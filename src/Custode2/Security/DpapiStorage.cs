using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using Custode2.Models;

namespace Custode2.Security
{
    public static class DpapiStorage
    {
        private static readonly object FileLock = new object();
        private static readonly byte[] OptionalEntropy = Encoding.UTF8.GetBytes("Custode2.0_Entropy_SecureSalt_2026");

        public static string GetVaultFilePath()
        {
            // 1. Portable mode: if vault.dat exists in the application directory, use that
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string localFile = Path.Combine(appDir, "vault.dat");
            if (File.Exists(localFile))
            {
                return localFile;
            }

            // 2. Default mode: %LocalAppData%\Custode2\vault.dat
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string custodeDir = Path.Combine(appData, "Custode2");
            if (!Directory.Exists(custodeDir))
            {
                Directory.CreateDirectory(custodeDir);
            }
            return Path.Combine(custodeDir, "vault.dat");
        }

        public static VaultData LoadVault()
        {
            lock (FileLock)
            {
                string path = GetVaultFilePath();
                if (!File.Exists(path))
                {
                    return new VaultData();
                }

                try
                {
                    byte[] encryptedBytes = File.ReadAllBytes(path);
                    if (encryptedBytes.Length == 0)
                    {
                        return new VaultData();
                    }

                    byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, OptionalEntropy, DataProtectionScope.CurrentUser);
                    string json = Encoding.UTF8.GetString(plainBytes);

                    var serializer = new JavaScriptSerializer();
                    var data = serializer.Deserialize<VaultData>(json);
                    if (data == null)
                    {
                        data = new VaultData();
                    }
                    if (data.Settings == null)
                    {
                        data.Settings = new AppSettings();
                    }
                    if (data.Entries == null)
                    {
                        data.Entries = new System.Collections.Generic.List<CredentialEntry>();
                    }
                    if (data.SharedPasswords == null)
                    {
                        data.SharedPasswords = new System.Collections.Generic.List<SharedPassword>();
                    }
                    return data;
                }
                catch (CryptographicException)
                {
                    // If file belongs to a different Windows user or is corrupted
                    throw new InvalidOperationException("Impossibile decifrare il file vault.dat con l'account Windows corrente.");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Errore durante il caricamento del file del vault: " + ex.Message, ex);
                }
            }
        }

        public static void SaveVault(VaultData vault)
        {
            if (vault == null) throw new ArgumentNullException("vault");

            lock (FileLock)
            {
                string path = GetVaultFilePath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(vault);
                byte[] plainBytes = Encoding.UTF8.GetBytes(json);
                byte[] encryptedBytes = ProtectedData.Protect(plainBytes, OptionalEntropy, DataProtectionScope.CurrentUser);

                // Create backup if file exists
                if (File.Exists(path))
                {
                    string backupPath = path + ".bak";
                    try
                    {
                        File.Copy(path, backupPath, true);
                    }
                    catch
                    {
                        // Ignore backup errors
                    }
                }

                File.WriteAllBytes(path, encryptedBytes);
            }
        }
    }
}
using System.Security.Cryptography;
using System.Text;

namespace jihadkhawaja.chat.server.Security
{
    public class EncryptionService
    {
        private readonly byte[] Key;
        private readonly byte[] IV;

        public EncryptionService(string? keyString, string? ivString)
        {
            if (string.IsNullOrEmpty(keyString)) throw new ArgumentNullException("Encryption Key not found in configuration");
            if (string.IsNullOrEmpty(ivString)) throw new ArgumentNullException("Encryption IV not found in configuration");

            if (keyString.Length != 32) throw new ArgumentException("Encryption Key must be exactly 32 characters long.");
            if (ivString.Length != 16) throw new ArgumentException("Encryption IV must be exactly 16 characters long.");

            Key = Encoding.UTF8.GetBytes(keyString);
            IV = Encoding.UTF8.GetBytes(ivString);
        }

        public string Encrypt(string plainText)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plainText);
                    }
                    return Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
        }

        public string Decrypt(string cipherText)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;
                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
                using (MemoryStream msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText)))
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                {
                    return srDecrypt.ReadToEnd();
                }
            }
        }
    }
}

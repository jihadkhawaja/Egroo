using System.Security.Cryptography;

namespace jihadkhawaja.chat.server.Helpers
{
    internal static class CryptographyHelper
    {
        public static string SecurePassword(string value)
        {
            byte[] salt;
            RandomNumberGenerator.Create().GetBytes(salt = new byte[16]);

#pragma warning disable SYSLIB0041 // Type or member is obsolete
            Rfc2898DeriveBytes pbkdf2 = new(value, salt, 100000);
#pragma warning restore SYSLIB0041 // Type or member is obsolete
            byte[] hash = pbkdf2.GetBytes(20);

            byte[] hashBytes = new byte[36];
            Array.Copy(salt, 0, hashBytes, 0, 16);
            Array.Copy(hash, 0, hashBytes, 16, 20);

            string savedPasswordHash = Convert.ToBase64String(hashBytes);

            return savedPasswordHash;
        }
        public static bool ComparePassword(string password, string storedpassword)
        {
            /* Fetch the stored value */
            string savedPasswordHash = storedpassword;
            /* Extract the bytes */
            byte[] hashBytes = Convert.FromBase64String(savedPasswordHash);
            /* Get the salt */
            byte[] salt = new byte[16];
            Array.Copy(hashBytes, 0, salt, 0, 16);
            /* Compute the hash on the password the user entered */
#pragma warning disable SYSLIB0041 // Type or member is obsolete
            Rfc2898DeriveBytes pbkdf2 = new(password, salt, 100000);
#pragma warning restore SYSLIB0041 // Type or member is obsolete
            byte[] hash = pbkdf2.GetBytes(20);
            /* Compare the results */
            for (int i = 0; i < 20; i++)
            {
                if (hashBytes[i + 16] != hash[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}

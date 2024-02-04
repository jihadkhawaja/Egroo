using System.Text.RegularExpressions;

namespace jihadkhawaja.mobilechat.server.Helpers
{
    internal static class PatternMatchHelper
    {
        public static bool IsValidEmail(string content)
        {
            return Regex.IsMatch(content, @"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// only contain letters, numbers, and underscores, and be between 3 and 16 characters in length
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public static bool IsValidUsername(string username)
        {
            return Regex.IsMatch(username, @"^[a-zA-Z0-9_]{3,16}$");
        }

        /// <summary>
        /// <para>(?=.*[a-z]) - at least one lowercase letter</para> 
        /// <para>(?=.*[A-Z]) - at least one uppercase letter</para> 
        /// <para>(?=.*\d) - at least one digit</para> 
        /// <para>(?=.*[^\da-zA-Z]) - at least one special character</para> 
        /// <para>{8,15} - at least 8 characters and at most 15 characters</para> 
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public static bool IsValidPassword(string password)
        {
            return Regex.IsMatch(password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,}$");
        }

        public static bool IsValidDisplayName(string displayname)
        {
            return Regex.IsMatch(displayname, @"^[a-zA-Z0-9_]{3,16}$");
        }
    }
}

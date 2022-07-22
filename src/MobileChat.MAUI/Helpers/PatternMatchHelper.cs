using System.Text.RegularExpressions;

namespace MobileChat.MAUI.Helpers
{
    public static class PatternMatchHelper
    {
        public static bool IsEmail(string content)
        {
            return Regex.IsMatch(content, @"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z", RegexOptions.IgnoreCase);
        }
    }
}

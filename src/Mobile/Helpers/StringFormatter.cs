namespace MobileChat.Helpers
{
    public static class StringFormatter
    {
        public static string GetHexString(this Color color)
        {
            int red = (int)(color.Red * 255);
            int green = (int)(color.Green * 255);
            int blue = (int)(color.Blue * 255);
            int alpha = (int)(color.Alpha * 255);
            string hex = $"#{alpha:X2}{red:X2}{green:X2}{blue:X2}";

            return hex;
        }
    }
}
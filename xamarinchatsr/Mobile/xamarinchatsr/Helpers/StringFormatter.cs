namespace xamarinchatsr.Helpers
{
    public static class StringFormatter
    {
        public static string GetHexString(this Xamarin.Forms.Color color)
        {
            int red = (int)(color.R * 255);
            int green = (int)(color.G * 255);
            int blue = (int)(color.B * 255);
            int alpha = (int)(color.A * 255);
            string hex = $"#{alpha:X2}{red:X2}{green:X2}{blue:X2}";

            return hex;
        }
    }
}
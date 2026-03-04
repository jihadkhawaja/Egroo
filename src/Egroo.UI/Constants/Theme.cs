using MudBlazor;

namespace Egroo.UI.Constants
{
    public static class Theme
    {
        public static readonly MudTheme DefaultTheme = new MudTheme()
        {
            PaletteDark = new PaletteDark()
            {
                Primary = "#F25922",
                PrimaryContrastText = "#FFFFFF",
                Secondary = "#F2B591",
                SecondaryContrastText = "#1a1a1a",
                Black = "#27272f",
                Background = "#32333d",
                BackgroundGray = "#27272f",
                Surface = "#1e1e1e",
                DrawerBackground = "#1e1e1e",
                DrawerText = "rgba(255,255,255, 0.50)",
                DrawerIcon = "rgba(255,255,255, 0.50)",
                AppbarBackground = "#1e1e1e",
                AppbarText = "rgba(255,255,255, 0.70)",
                TextPrimary = "rgba(255,255,255, 0.90)",
                TextSecondary = "rgba(255,255,255, 0.70)",
                ActionDefault = "#adadb1",
                ActionDisabled = "rgba(255,255,255, 0.26)",
                ActionDisabledBackground = "rgba(255,255,255, 0.12)",
                Divider = "rgba(255,255,255, 0.12)",
                DividerLight = "rgba(255,255,255, 0.06)",
                TableLines = "rgba(255,255,255, 0.12)",
                LinesDefault = "rgba(255,255,255, 0.12)",
                LinesInputs = "rgba(255,255,255, 0.3)",
                TextDisabled = "rgba(255,255,255, 0.2)",
            },
            Typography = new Typography()
            {
                Default = new DefaultTypography()
                {
                    FontFamily = ["Inter", "Roboto", "Helvetica", "Arial", "sans-serif"],
                    FontSize = "0.875rem",
                    FontWeight = "400",
                    LineHeight = "1.5",
                    LetterSpacing = "0.00938em",
                },
                H5 = new H5Typography() { FontWeight = "600" },
                H6 = new H6Typography() { FontWeight = "600" },
                Button = new ButtonTypography()
                {
                    FontWeight = "600",
                    LetterSpacing = "0.02em",
                    TextTransform = "none",
                },
            },
        };
    }
}

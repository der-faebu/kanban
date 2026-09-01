using MudBlazor;

namespace Kanban.Theme;

// Colors here must stay in sync with the --knb-* tokens in wwwroot/theme.css —
// this is the same palette expressed for MudBlazor's C#-based theming, since
// MudTheme can't read CSS custom properties.
public static class KanbanTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteDark = new PaletteDark
        {
            Black = "#05060a",
            Background = "#0a0c10",
            BackgroundGray = "#12151b",
            Surface = "#12151b",
            DrawerBackground = "#12151b",
            DrawerText = "#9aa2b8",
            DrawerIcon = "#9aa2b8",
            AppbarBackground = "#0a0c10",
            AppbarText = "#e7e9f0",

            Primary = "#6d5efc",
            PrimaryDarken = "#5a4cf0",
            PrimaryLighten = "#8172ff",

            Secondary = "#22d3ee",
            Info = "#3b82f6",
            Success = "#35d399",
            Warning = "#f2b84b",
            Error = "#f65a6a",

            TextPrimary = "#e7e9f0",
            TextSecondary = "#9aa2b8",
            TextDisabled = "#656d82",

            LinesDefault = "#2e3446",
            LinesInputs = "#3d4459",
            TableLines = "#2e3446",

            ActionDefault = "#9aa2b8",
            ActionDisabled = "#454e63",

            Divider = "#1f2430",
        },
        PaletteLight = new PaletteLight
        {
            Background = "#f6f7fb",
            Surface = "#ffffff",
            DrawerBackground = "#ffffff",
            AppbarBackground = "#ffffff",

            Primary = "#5b4cf0",
            Secondary = "#0891a8",
            Info = "#3b82f6",
            Success = "#1a9c6c",
            Warning = "#b9770e",
            Error = "#d9364a",

            TextPrimary = "#151824",
            TextSecondary = "#565d75",

            LinesDefault = "#d8dbe6",
            LinesInputs = "#b7bbd0",
            Divider = "#ebedf3",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["-apple-system", "BlinkMacSystemFont", "Segoe UI", "Inter", "Roboto", "Helvetica", "Arial", "sans-serif"],
            },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "6px",
            DrawerWidthLeft = "260px",
            AppbarHeight = "56px",
        },
    };
}

using MudBlazor;

namespace Kanban.Theme;

// Colors here must stay in sync with the --knb-* tokens in wwwroot/theme.css —
// this is the same palette expressed for MudBlazor's C#-based theming, since
// MudTheme can't read CSS custom properties. PaletteLight is the shipped
// default (Kanso-inspired); PaletteDark is a same-accent-family companion,
// unused until a toggle exists — Kanso itself has no dark mode.
public static class KanbanTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Black = "#1f2933",
            Background = "#eef4fa",
            BackgroundGray = "#f5f6f8",
            Surface = "#ffffff",
            DrawerBackground = "#ffffff",
            DrawerText = "#5b6570",
            DrawerIcon = "#5b6570",
            AppbarBackground = "#ffffff",
            AppbarText = "#1f2933",

            Primary = "#0972ad",
            PrimaryDarken = "#064a72",
            PrimaryLighten = "#4a9dc9",

            Secondary = "#2f6f4f",
            Info = "#0972ad",
            Success = "#2f6f4f",
            Warning = "#b5730a",
            Error = "#c0392b",

            TextPrimary = "#1f2933",
            TextSecondary = "#5b6570",
            TextDisabled = "#8a9199",

            LinesDefault = "#e0e4e9",
            LinesInputs = "#c7ccd3",
            TableLines = "#e0e4e9",

            ActionDefault = "#5b6570",
            ActionDisabled = "#c7ccd3",

            Divider = "#eef1f4",
        },
        PaletteDark = new PaletteDark
        {
            Black = "#05070c",
            Background = "#0b1220",
            BackgroundGray = "#121a2b",
            Surface = "#121a2b",
            DrawerBackground = "#121a2b",
            DrawerText = "#a3aec2",
            DrawerIcon = "#a3aec2",
            AppbarBackground = "#0b1220",
            AppbarText = "#e8edf5",

            Primary = "#4fa8d8",
            PrimaryDarken = "#3f8fb9",
            PrimaryLighten = "#6bb9e2",

            Secondary = "#4f9c78",
            Info = "#4fa8d8",
            Success = "#4f9c78",
            Warning = "#d99a3f",
            Error = "#e2685c",

            TextPrimary = "#e8edf5",
            TextSecondary = "#a3aec2",
            TextDisabled = "#6b7791",

            LinesDefault = "#2a3650",
            LinesInputs = "#3a4864",
            TableLines = "#2a3650",

            ActionDefault = "#a3aec2",
            ActionDisabled = "#3a4864",

            Divider = "#1b2536",
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
            DefaultBorderRadius = "8px",
            DrawerWidthLeft = "260px",
            AppbarHeight = "56px",
        },
    };
}

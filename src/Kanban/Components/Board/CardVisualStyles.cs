namespace Kanban.Components.Board;

// Presentation-only helpers for the Kanso-inspired card tile: a display-only
// card-ID code (no new schema — derived from the existing Board.Name +
// Card.Id, not a per-board sequence counter like Kanso's own "PRODU-1"),
// a deterministic color for initials avatars, and a contrast-safe text
// color for label pills painted with a user-chosen hex background.
public static class CardVisualStyles
{
    public static string IdCode(string boardName, int cardId)
    {
        var letters = new string(boardName.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var prefix = letters.Length >= 3 ? letters[..Math.Min(letters.Length, 5)] : "CARD";
        return $"{prefix}-{cardId}";
    }

    private static readonly (string Bg, string Fg)[] AvatarPalette =
    [
        ("rgba(9, 114, 173, 0.15)", "#0972ad"),
        ("rgba(47, 111, 79, 0.15)", "#2f6f4f"),
        ("rgba(181, 115, 10, 0.15)", "#b5730a"),
        ("rgba(192, 57, 43, 0.15)", "#c0392b"),
        ("#f3e8fd", "#7c3aed"),
        ("#e0f7f5", "#0f766e"),
        ("#fde8f3", "#be185d"),
    ];

    public static (string Bg, string Fg) AvatarColor(string seed)
    {
        var index = Math.Abs(seed.GetHashCode()) % AvatarPalette.Length;
        return AvatarPalette[index];
    }

    public static string ContrastText(string hexColor)
    {
        if (!TryParseHex(hexColor, out var r, out var g, out var b))
            return "#ffffff";

        // Relative luminance (WCAG approximation) decides black vs white text.
        var luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
        return luminance > 0.6 ? "#1f2933" : "#ffffff";
    }

    private static bool TryParseHex(string hex, out int r, out int g, out int b)
    {
        r = g = b = 0;
        var value = hex.TrimStart('#');
        if (value.Length != 6)
            return false;

        return int.TryParse(value[..2], System.Globalization.NumberStyles.HexNumber, null, out r)
            && int.TryParse(value[2..4], System.Globalization.NumberStyles.HexNumber, null, out g)
            && int.TryParse(value[4..6], System.Globalization.NumberStyles.HexNumber, null, out b);
    }
}

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using AmharcAgent.Core.Domain;
using OpenMacroBoard.SDK;

namespace AmharcAgent.Infrastructure.StreamDeck;

public sealed class AmharcStreamDeckButtonRenderer
{
    private const string AmharcBlack = "#000000";
    private const string AmharcGreen = "#1C8551";
    private const string AmharcLime = "#B6DC46";
    private const string AmharcWhite = "#FFFFFF";

    public KeyBitmap Render(
        StreamDeckButton button,
        bool active,
        int width = 72,
        int height = 72)
    {
        using var bitmap = new Bitmap(
            width,
            height,
            PixelFormat.Format32bppArgb);

        using var graphics = Graphics.FromImage(bitmap);

        graphics.SmoothingMode =
            SmoothingMode.AntiAlias;

        graphics.TextRenderingHint =
            System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        var backgroundColour =
            ParseColour(
                button.Colour,
                button.Team);

        using var backgroundBrush =
            new SolidBrush(backgroundColour);

        graphics.FillRectangle(
            backgroundBrush,
            0,
            0,
            width,
            height);

        if (active)
        {
            using var activePen =
                new Pen(
                    FromHex(AmharcWhite),
                    4);

            graphics.DrawRectangle(
                activePen,
                2,
                2,
                width - 5,
                height - 5);
        }

        var textColour =
            IsLightColour(backgroundColour)
                ? FromHex(AmharcBlack)
                : FromHex(AmharcWhite);

        var lines =
            SplitLabel(button.Label);

        using var font =
            new Font(
                "Arial",
                lines.Length > 1 ? 11f : 12f,
                FontStyle.Bold,
                GraphicsUnit.Pixel);

        using var textBrush =
            new SolidBrush(textColour);

        using var format =
            new StringFormat
            {
                Alignment =
                    StringAlignment.Center,
                LineAlignment =
                    StringAlignment.Center
            };

        var textRectangle =
            new RectangleF(
                4,
                4,
                width - 8,
                height - 8);

        graphics.DrawString(
            string.Join(
                Environment.NewLine,
                lines),
            font,
            textBrush,
            textRectangle,
            format);

        using var stream =
            new MemoryStream();

        bitmap.Save(
            stream,
            ImageFormat.Png);

        stream.Position = 0;

        return KeyBitmap.Create.FromStream(
            stream);
    }

    private static string[] SplitLabel(
        string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return [""];

        var words =
            label.Trim()
                .Split(
                    ' ',
                    StringSplitOptions
                        .RemoveEmptyEntries);

        if (words.Length <= 1)
            return words;

        if (words.Length == 2)
            return words;

        var midpoint =
            (int)Math.Ceiling(
                words.Length / 2.0);

        return
        [
            string.Join(
                " ",
                words.Take(midpoint)),
            string.Join(
                " ",
                words.Skip(midpoint))
        ];
    }

    private static Color ParseColour(
        string? hex,
        ButtonTeam? team)
    {
        if (!string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                return FromHex(hex);
            }
            catch
            {
                // Fall back to AMHARC defaults.
            }
        }

        return team switch
        {
            ButtonTeam.Home =>
                FromHex(AmharcGreen),

            ButtonTeam.Away =>
                FromHex(AmharcLime),

            _ =>
                FromHex(AmharcBlack)
        };
    }

    private static bool IsLightColour(
        Color colour)
    {
        var luminance =
            (0.299 * colour.R) +
            (0.587 * colour.G) +
            (0.114 * colour.B);

        return luminance > 160;
    }

    private static Color FromHex(string hex)
{
    if (string.IsNullOrWhiteSpace(hex))
        return Color.Black;

    var value = hex.Trim().TrimStart('#');

    if (value.Length != 6)
        return Color.Black;

    return Color.FromArgb(
        Convert.ToInt32(value[..2], 16),
        Convert.ToInt32(value.Substring(2, 2), 16),
        Convert.ToInt32(value.Substring(4, 2), 16));
}
}
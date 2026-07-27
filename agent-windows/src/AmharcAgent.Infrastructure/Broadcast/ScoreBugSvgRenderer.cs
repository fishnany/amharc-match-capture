using System.Security;
using System.Text;
using AmharcAgent.Core.Broadcast;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;

namespace AmharcAgent.Infrastructure.Broadcast;

/// <summary>
/// Resolution-independent Phase 1.1 renderer. The AMHARC mark is never redrawn:
/// the SVG references the approved immutable master artwork supplied with the UI.
/// </summary>
public sealed class ScoreBugSvgRenderer : IScoreBugRenderer
{
    public string RenderSvg(ScoreBugViewModel m)
    {
        var homeScore = m.HomeScore.Format(m.ScoringModel);
        var awayScore = m.AwayScore.Format(m.ScoringModel);
        var clock = FormatClock(m);
        var logo = Escape(m.LogoAssetPath);
        var home = Escape(m.HomeAbbreviation);
        var away = Escape(m.AwayAbbreviation);

        const int width = 1200;
        const int height = 110;
        const int logoEnd = 175;
        const int homeTeamEnd = 320;
        const int homeScoreEnd = 505;
        const int clockEnd = 695;
        const int awayTeamEnd = 840;
        const int awayScoreEnd = 1190;

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\" role=\"img\" aria-label=\"AMHARC score bug\">");
        sb.Append("<defs>");
        sb.Append("<linearGradient id=\"panel\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" stop-color=\"#151515\"/><stop offset=\"1\" stop-color=\"#050505\"/></linearGradient>");
        sb.Append("<linearGradient id=\"team\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"0\"><stop offset=\"0\" stop-color=\"#0b3b26\"/><stop offset=\"1\" stop-color=\"#07140e\"/></linearGradient>");
        sb.Append("<filter id=\"shadow\" x=\"-10%\" y=\"-50%\" width=\"120%\" height=\"200%\"><feDropShadow dx=\"0\" dy=\"4\" stdDeviation=\"5\" flood-color=\"#000\" flood-opacity=\"0.65\"/></filter>");
        sb.Append("</defs>");

        // Transparent canvas; only the score bug is painted.
        sb.Append("<g filter=\"url(#shadow)\">");
        sb.Append("<rect x=\"5\" y=\"8\" width=\"1190\" height=\"94\" rx=\"12\" fill=\"url(#panel)\" stroke=\"#5e5e5e\" stroke-width=\"2\"/>");
        sb.Append($"<rect x=\"7\" y=\"10\" width=\"{homeTeamEnd-7}\" height=\"90\" rx=\"10\" fill=\"url(#team)\"/>");
        sb.Append($"<rect x=\"{clockEnd}\" y=\"10\" width=\"{awayTeamEnd-clockEnd}\" height=\"90\" fill=\"url(#team)\"/>");

        foreach (var x in new[] { logoEnd, homeTeamEnd, homeScoreEnd, clockEnd, awayTeamEnd })
            sb.Append($"<line x1=\"{x}\" y1=\"14\" x2=\"{x}\" y2=\"96\" stroke=\"#ffffff\" stroke-opacity=\"0.13\"/>");

        // Approved master asset only — never reconstructed.
        sb.Append($"<image href=\"{logo}\" x=\"20\" y=\"19\" width=\"135\" height=\"72\" preserveAspectRatio=\"xMidYMid meet\"/>");

        Text(sb, home, 247, 69, 36, 800, "#FFFFFF");
        ScoreText(sb, homeScore, m.HomeScore.Total, 405, 69);
        Text(sb, clock, 600, 70, 41, 800, "#FFFFFF");
        sb.Append("<rect x=\"548\" y=\"94\" width=\"104\" height=\"6\" rx=\"3\" fill=\"#B6DC46\"/>");
        Text(sb, away, 768, 69, 36, 800, "#FFFFFF");
        ScoreText(sb, awayScore, m.AwayScore.Total, 1006, 69);

        DisciplineBar(sb, isHome: true, m.HomeDiscipline);
        DisciplineBar(sb, isHome: false, m.AwayDiscipline);

        if (m.ReplayMode)
        {
            sb.Append("<rect x=\"535\" y=\"3\" width=\"130\" height=\"25\" rx=\"5\" fill=\"#1C8551\"/>");
            Text(sb, "REPLAY", 600, 21, 16, 800, "#FFFFFF");
        }

        sb.Append("</g></svg>");
        return sb.ToString();
    }

    private static void ScoreText(StringBuilder sb, string score, int total, int cx, int y)
    {
        Text(sb, score, cx - 16, y, 39, 800, "#FFFFFF", "end");
        Text(sb, $"({total})", cx - 4, y, 25, 800, "#B6DC46", "start");
    }

    private static void DisciplineBar(StringBuilder sb, bool isHome, TeamDisciplineState d)
    {
        var x = isHome ? 9 : 1181;
        var colour = d.HasRedCard ? "#E63946" : d.HasActiveBlackCard ? "#000000" : isHome ? "#1C8551" : "#B6DC46";
        var stroke = d.HasActiveBlackCard && !d.HasRedCard ? " stroke=\"#8b8b8b\" stroke-width=\"2\"" : string.Empty;
        sb.Append($"<rect x=\"{x}\" y=\"17\" width=\"10\" height=\"76\" rx=\"4\" fill=\"{colour}\"{stroke}/>");
    }

    private static string FormatClock(ScoreBugViewModel m)
    {
        if (m.MatchStatus == AmharcAgent.Core.Domain.MatchStatus.HalfTime) return "HT";
        if (m.MatchStatus == AmharcAgent.Core.Domain.MatchStatus.Complete) return "FT";
        var minutes = Math.Max(0, m.MatchClockSeconds) / 60;
        var seconds = Math.Max(0, m.MatchClockSeconds) % 60;
        var time = $"{minutes:00}:{seconds:00}";
        return m.CurrentPeriod > 2 ? $"ET {time}" : time;
    }

    private static void Text(StringBuilder sb, string text, int x, int y, int size, int weight, string fill, string anchor = "middle") =>
        sb.Append($"<text x=\"{x}\" y=\"{y}\" text-anchor=\"{anchor}\" font-family=\"Arial, Helvetica, sans-serif\" font-size=\"{size}\" font-weight=\"{weight}\" fill=\"{fill}\">{Escape(text)}</text>");

    private static string Escape(string value) => SecurityElement.Escape(value) ?? string.Empty;
}

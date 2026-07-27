using System.Security;
using System.Text;
using AmharcAgent.Core.Broadcast;
using AmharcAgent.Core.Interfaces;

namespace AmharcAgent.Infrastructure.Broadcast;

/// <summary>Phase 1.2 canonical AMHARC event-banner SVG renderer.</summary>
public sealed class EventBannerSvgRenderer : IEventBannerRenderer
{
    public string RenderSvg(EventBannerViewModel m)
    {
        var type = Normalize(m.EventType);
        var (title, accent, badge) = type switch
        {
            "goal" or "penalty_goal" => ("GOAL", "#1C8551", (string?)null),
            "point" => ("POINT", "#1C8551", null),
            "two_point" or "two_point_score" or "two_point_score_" => ("TWO-POINT SCORE", "#B6DC46", "+2"),
            "wide" => ("WIDE", "#1C8551", null),
            "short" => ("SHORT", "#1C8551", null),
            "off_the_post" => ("OFF THE POST", "#1C8551", null),
            "yellow_card" => ("YELLOW CARD", "#F2C94C", null),
            "black_card" => ("BLACK CARD", "#050505", null),
            "red_card" => ("RED CARD", "#E63946", null),
            "substitution" => ("SUBSTITUTION", "#7A4EA3", null),
            "half_time" => ("HALF TIME", "#1C8551", null),
            "full_time" => ("FULL TIME", "#1C8551", null),
            "free" or "free_awarded" => ("FREE", "#B66A00", null),
            "penalty" => ("PENALTY", "#B66A00", null),
            "mark" => ("MARK", "#B66A00", null),
            "45" or "45m_free" => ("45", "#B66A00", null),
            "65" or "65m_free" => ("65", "#B66A00", null),
            "replay" => ("REPLAY", "#1C8551", null),
            _ => (m.EventType.Replace('_',' ').ToUpperInvariant(), "#1C8551", null)
        };

        var player = m.PlayerNumber is null ? string.Empty : m.PlayerName is { Length: > 0 }
            ? $"{m.PlayerNumber}  •  {m.PlayerName}" : $"{m.PlayerNumber}";
        var logo=E(m.LogoAssetPath); var team=E(m.TeamName.ToUpperInvariant());
        var sb=new StringBuilder();
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"900\" height=\"240\" viewBox=\"0 0 900 240\" role=\"img\" aria-label=\"AMHARC event banner\">");
        sb.Append("<defs><linearGradient id=\"p\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"0\"><stop stop-color=\"#07140e\"/><stop offset=\".42\" stop-color=\"#080808\"/><stop offset=\"1\" stop-color=\"#111\"/></linearGradient><filter id=\"s\"><feDropShadow dx=\"0\" dy=\"8\" stdDeviation=\"8\" flood-opacity=\".7\"/></filter></defs>");
        sb.Append("<g filter=\"url(#s)\"><rect x=\"8\" y=\"18\" width=\"884\" height=\"204\" rx=\"18\" fill=\"url(#p)\" stroke=\"#6c6c6c\" stroke-width=\"2\"/>");
        sb.Append($"<rect x=\"8\" y=\"18\" width=\"14\" height=\"204\" rx=\"7\" fill=\"{accent}\"/>");
        sb.Append($"<image href=\"{logo}\" x=\"42\" y=\"55\" width=\"190\" height=\"110\" preserveAspectRatio=\"xMidYMid meet\"/>");
        T(sb,E(title),545,88,44,800,"#fff"); T(sb,team,545,133,27,800,"#1C8551");
        if(player.Length>0){ sb.Append("<line x1=\"310\" y1=\"156\" x2=\"780\" y2=\"156\" stroke=\"#fff\" stroke-opacity=\".35\"/>"); T(sb,E(player),545,194,23,500,"#fff"); }
        if(badge is not null){ sb.Append("<rect x=\"760\" y=\"63\" width=\"86\" height=\"60\" rx=\"8\" fill=\"#B6DC46\"/>"); T(sb,badge,803,106,31,900,"#000"); }
        sb.Append("</g></svg>"); return sb.ToString();
    }
    static string Normalize(string s)=>s.Trim().ToLowerInvariant().Replace('-','_').Replace(' ','_');
    static string E(string s)=>SecurityElement.Escape(s)??string.Empty;
    static void T(StringBuilder b,string t,int x,int y,int size,int weight,string fill)=>b.Append($"<text x=\"{x}\" y=\"{y}\" text-anchor=\"middle\" font-family=\"Arial,Helvetica,sans-serif\" font-size=\"{size}\" font-weight=\"{weight}\" fill=\"{fill}\">{t}</text>");
}

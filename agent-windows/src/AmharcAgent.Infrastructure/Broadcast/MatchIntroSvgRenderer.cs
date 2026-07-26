using System.Security;
using System.Text;
using AmharcAgent.Core.Broadcast;
using AmharcAgent.Core.Interfaces;

namespace AmharcAgent.Infrastructure.Broadcast;

/// <summary>Phase 1.3 ten-second AMHARC match-introduction SVG animation.</summary>
public sealed class MatchIntroSvgRenderer : IMatchIntroRenderer
{
    public string RenderSvg(MatchIntroViewModel m)
    {
        var logo=E(m.LogoAssetPath); var comp=E(m.Competition.ToUpperInvariant()); var round=E(m.Round??"");
        var home=E(m.HomeTeam.ToUpperInvariant()); var away=E(m.AwayTeam.ToUpperInvariant()); var venue=E(m.Venue??"VENUE TBC");
        var date=E(m.Date.ToString("ddd d MMM yyyy").ToUpperInvariant()); var throwIn=E(m.ThrowInLabel);
        var sb=new StringBuilder();
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1920\" height=\"1080\" viewBox=\"0 0 1920 1080\" role=\"img\" aria-label=\"AMHARC ten second match introduction\">");
        sb.Append("<defs><radialGradient id=\"bg\"><stop stop-color=\"#0b2d20\"/><stop offset=\".55\" stop-color=\"#06101a\"/><stop offset=\"1\" stop-color=\"#000\"/></radialGradient><linearGradient id=\"sweep\"><stop stop-color=\"#1C8551\" stop-opacity=\"0\"/><stop offset=\".5\" stop-color=\"#B6DC46\"/><stop offset=\"1\" stop-color=\"#1C8551\" stop-opacity=\"0\"/></linearGradient></defs>");
        sb.Append("<rect width=\"1920\" height=\"1080\" fill=\"url(#bg)\"/>");
        sb.Append("<g opacity=\"0\"><rect x=\"-900\" y=\"460\" width=\"900\" height=\"16\" fill=\"url(#sweep)\"><animate attributeName=\"x\" from=\"-900\" to=\"2100\" dur=\"1s\" begin=\"0s\" fill=\"freeze\"/></rect><animate attributeName=\"opacity\" values=\"0;1;0\" dur=\"1s\" begin=\"0s\" fill=\"freeze\"/></g>");
        Group(sb,"logo",1,2.4,$"<image href=\"{logo}\" x=\"610\" y=\"330\" width=\"700\" height=\"420\" preserveAspectRatio=\"xMidYMid meet\"/>");
        Group(sb,"competition",2.2,4.1,$"<text x=\"960\" y=\"500\" text-anchor=\"middle\" font-family=\"Arial\" font-size=\"66\" font-weight=\"800\" fill=\"#fff\">{comp}</text><text x=\"960\" y=\"570\" text-anchor=\"middle\" font-family=\"Arial\" font-size=\"34\" fill=\"#B6DC46\">{round}</text>");
        Group(sb,"teams",3.8,6.5,$"<text x=\"510\" y=\"520\" text-anchor=\"middle\" font-family=\"Arial\" font-size=\"72\" font-weight=\"900\" fill=\"#fff\">{home}</text><text x=\"960\" y=\"520\" text-anchor=\"middle\" font-family=\"Arial\" font-size=\"55\" font-weight=\"700\" fill=\"#B6DC46\">v</text><text x=\"1410\" y=\"520\" text-anchor=\"middle\" font-family=\"Arial\" font-size=\"72\" font-weight=\"900\" fill=\"#fff\">{away}</text>");
        Group(sb,"details",6.1,8.3,$"<text x=\"960\" y=\"465\" text-anchor=\"middle\" font-family=\"Arial\" font-size=\"52\" font-weight=\"700\" fill=\"#fff\">{venue}</text><text x=\"960\" y=\"545\" text-anchor=\"middle\" font-family=\"Arial\" font-size=\"34\" fill=\"#B6DC46\">{date}   •   {throwIn}</text>");
        Group(sb,"outro",8.0,9.8,$"<image href=\"{logo}\" x=\"710\" y=\"320\" width=\"500\" height=\"300\" preserveAspectRatio=\"xMidYMid meet\"/><text x=\"960\" y=\"720\" text-anchor=\"middle\" font-family=\"Arial\" font-size=\"30\" letter-spacing=\"5\" fill=\"#fff\">CAPTURE  •  ANALYSE  •  ELEVATE</text>");
        sb.Append("<rect width=\"1920\" height=\"1080\" fill=\"#000\" opacity=\"0\"><animate attributeName=\"opacity\" values=\"0;0;1\" keyTimes=\"0;.9;1\" begin=\"9.4s\" dur=\".6s\" fill=\"freeze\"/></rect>");
        sb.Append("</svg>"); return sb.ToString();
    }
    static void Group(StringBuilder sb,string id,double begin,double end,string content){ var dur=end-begin; sb.Append($"<g id=\"{id}\" opacity=\"0\">{content}<animate attributeName=\"opacity\" values=\"0;1;1;0\" keyTimes=\"0;.12;.86;1\" begin=\"{begin:0.0}s\" dur=\"{dur:0.0}s\" fill=\"freeze\"/></g>"); }
    static string E(string s)=>SecurityElement.Escape(s)??string.Empty;
}

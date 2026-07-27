using AmharcAgent.Core.Broadcast;
using AmharcAgent.Core.Domain;
using AmharcAgent.Infrastructure.Broadcast;
using Xunit;
namespace AmharcAgent.Tests;
public class MatchIntroSvgRendererTests
{
 [Fact] public void Intro_ContainsTenSecondTimelineAndCanonicalLogo(){
  var r=new MatchIntroSvgRenderer(); var m=new MatchIntroViewModel("m",Sport.Hurling,"Senior Championship","Final","Clane","Naas","Clane GAA Park",new DateOnly(2026,7,26),"19:30","/branding/amharc-logo-transparent.png",BrandAssets.DefaultTheme);
  var svg=r.RenderSvg(m); Assert.Contains("9.4s",svg); Assert.Contains("CLANE",svg); Assert.Contains("NAAS",svg); Assert.Contains("/branding/amharc-logo-transparent.png",svg);
 }
}

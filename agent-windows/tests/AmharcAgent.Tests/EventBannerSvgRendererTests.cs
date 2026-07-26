using AmharcAgent.Core.Broadcast;
using AmharcAgent.Core.Domain;
using AmharcAgent.Infrastructure.Broadcast;
using Xunit;
namespace AmharcAgent.Tests;
public class EventBannerSvgRendererTests
{
 [Fact] public void TwoPointBanner_IsDistinctAndUsesMasterLogo(){
  var r=new EventBannerSvgRenderer(); var m=new EventBannerViewModel("m",Sport.GaelicFootball,"two_point","Clane","CLA",11,null,"/branding/amharc-logo-transparent.png",BrandAssets.DefaultTheme);
  var svg=r.RenderSvg(m); Assert.Contains("TWO-POINT SCORE",svg); Assert.Contains("+2",svg); Assert.Contains("/branding/amharc-logo-transparent.png",svg);
 }
 [Fact] public void RedCardBanner_UsesRedAccent(){
  var r=new EventBannerSvgRenderer(); var m=new EventBannerViewModel("m",Sport.Hurling,"red_card","Naas","NAA",5,null,"/branding/amharc-logo-transparent.png",BrandAssets.DefaultTheme);
  var svg=r.RenderSvg(m); Assert.Contains("RED CARD",svg); Assert.Contains("#E63946",svg);
 }
}

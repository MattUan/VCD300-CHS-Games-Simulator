using System;
using System.Drawing;

namespace VCD300 {
static class WindowGeometry {
 internal static Rectangle Fit(Size available){
  int unit=Math.Min(available.Width/4,available.Height/3);if(unit<=0)return Rectangle.Empty;
  int width=unit*4,height=unit*3;return new Rectangle((available.Width-width)/2,(available.Height-height)/2,width,height);
 }
 // footer is outside the 4:3 picture. Edges use the Windows WMSZ_* values.
 internal static Rectangle Constrain(Rectangle requested,Rectangle previous,int edge,Size frame,int footer,Size minimum){
  int minUnit=Math.Max(1,Math.Max(Ceil(minimum.Width-frame.Width,4),Ceil(minimum.Height-frame.Height-footer,3)));
  bool vertical=edge==3||edge==6;
  if(edge==4||edge==5||edge==7||edge==8)vertical=Math.Abs(requested.Height-previous.Height)*4>Math.Abs(requested.Width-previous.Width)*3;
  int unit=vertical?(requested.Height-frame.Height-footer)/3:(requested.Width-frame.Width)/4;unit=Math.Max(minUnit,unit);
  int width=unit*4+frame.Width,height=unit*3+frame.Height+footer;
  bool left=edge==1||edge==4||edge==7,top=edge==3||edge==4||edge==5;
  return new Rectangle(left?requested.Right-width:requested.Left,top?requested.Bottom-height:requested.Top,width,height);
 }
 static int Ceil(int value,int divisor){return value<=0?0:(value+divisor-1)/divisor;}
}
}

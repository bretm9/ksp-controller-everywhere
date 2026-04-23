using UnityEngine;

namespace ControllerEverywhere
{
    // IMGUI helpers for drawing outlines and boxes shared across UI overlays.
    internal static class UiDraw
    {
        public static void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        public static void Outline(Rect r, Color c, float thickness)
        {
            Fill(new Rect(r.xMin, r.yMin, r.width, thickness),              c); // top
            Fill(new Rect(r.xMin, r.yMax - thickness, r.width, thickness),  c); // bottom
            Fill(new Rect(r.xMin, r.yMin, thickness, r.height),             c); // left
            Fill(new Rect(r.xMax - thickness, r.yMin, thickness, r.height), c); // right
        }
    }
}

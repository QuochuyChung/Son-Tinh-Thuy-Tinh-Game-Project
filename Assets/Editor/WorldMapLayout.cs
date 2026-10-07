using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // How the three gameplay maps sit on the world map picture (docs/progress.md 9.9). The picture is a top-down view of the real scenes:
    // the Son Tinh map on the left, the palace plateau in the middle, the Thuy Tinh map on the right, both routes ending at the level
    // of the plateau's centre line, where their gates face the plateau's west and east gates. Everything is in metres, then scaled
    // by PixelsPerMeter. World +x is right on the picture, world +z is up.
    public static class WorldMapLayout
    {
        public const float PixelsPerMeter = 3.5f;

        // Visible part of each route map (the valley and its bays), and of the plateau (island plus a margin of sea).
        public const float RouteHalfWidth = 76f, RouteZMin = -12f, RouteZMax = 306f, RouteEndZ = 295f;
        public const float PalaceHalf = 118f;

        // Picture layout in metres, from the top-left corner: [Son Tinh column][palace][Thuy Tinh column], all the same row line.
        const float EndLine = 124f;                                   // y of the line where both routes end and the plateau's centre line lies
        const float PalaceLeft = 2f * RouteHalfWidth;
        public static readonly Vector2 SizeMeters = new(PalaceLeft + 2f * PalaceHalf + 2f * RouteHalfWidth, EndLine + (RouteEndZ - RouteZMin) + 8f);
        public static Vector2Int SizePixels => new(Mathf.RoundToInt(SizeMeters.x * PixelsPerMeter), Mathf.RoundToInt(SizeMeters.y * PixelsPerMeter));

        // A scene's ground placed on the picture: the world point (x, z) that lands on `referenceMeters` (picture metres, y down).
        public readonly struct Frame
        {
            public readonly Vector2 WorldReference;
            public readonly Vector2 ReferenceMeters;

            public Frame(Vector2 worldReference, Vector2 referenceMeters)
            {
                WorldReference = worldReference;
                ReferenceMeters = referenceMeters;
            }

            public Vector2 ToPixels(Vector2 world) =>
                (ReferenceMeters + new Vector2(world.x - WorldReference.x, -(world.y - WorldReference.y))) * PixelsPerMeter;

            // Fractions of the picture from the top-left corner (what MapRoute and the labels use).
            public Vector2 ToFraction(Vector2 world)
            {
                Vector2 p = ToPixels(world);
                Vector2Int size = SizePixels;
                return new Vector2(p.x / size.x, p.y / size.y);
            }

            public Vector2 MapPerMeter
            {
                get
                {
                    Vector2Int size = SizePixels;
                    return new Vector2(PixelsPerMeter / size.x, PixelsPerMeter / size.y);
                }
            }
        }

        public static readonly Frame SonTinh = new(new Vector2(0f, RouteEndZ), new Vector2(RouteHalfWidth, EndLine));
        public static readonly Frame Palace = new(Vector2.zero, new Vector2(PalaceLeft + PalaceHalf, EndLine));
        public static readonly Frame ThuyTinh = new(new Vector2(0f, RouteEndZ), new Vector2(PalaceLeft + 2f * PalaceHalf + RouteHalfWidth, EndLine));
    }
}

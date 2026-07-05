using UnityEngine;

namespace AO.Rhythm
{
    public static class LaneLayout
    {
        public const float DefaultDiagonalRadius = 0.4f;
        public const float DefaultDiagonalComponent = 0.2828427f;
        public const float DefaultCenterYOffset = 0f;

        public static readonly Vector3 UpperLeftOffset = new Vector3(-DefaultDiagonalComponent, DefaultDiagonalComponent, 0f);
        public static readonly Vector3 LowerLeftOffset = new Vector3(-DefaultDiagonalComponent, -DefaultDiagonalComponent, 0f);
        public static readonly Vector3 UpperRightOffset = new Vector3(DefaultDiagonalComponent, DefaultDiagonalComponent, 0f);
        public static readonly Vector3 LowerRightOffset = new Vector3(DefaultDiagonalComponent, -DefaultDiagonalComponent, 0f);
        public static readonly Vector3 CenterOffset = new Vector3(0f, DefaultCenterYOffset, 0f);
        public static readonly Vector3 TopMidSpawnOffset = new Vector3(0f, DefaultDiagonalComponent, 0f);

        public static Vector3 GetDefaultOffset(Lane lane)
        {
            return lane switch
            {
                Lane.Up => UpperLeftOffset,
                Lane.Left => LowerLeftOffset,
                Lane.Right => UpperRightOffset,
                Lane.Down => LowerRightOffset,
                Lane.Center => CenterOffset,
                _ => CenterOffset,
            };
        }

        public static string GetPhysicalName(Lane lane)
        {
            return lane switch
            {
                Lane.Up => "Upper Left",
                Lane.Left => "Lower Left",
                Lane.Right => "Upper Right",
                Lane.Down => "Lower Right",
                Lane.Center => "Center",
                _ => "None",
            };
        }
    }
}

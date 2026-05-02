using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace cs2external
{
    public static class Calculator
    {
        public static Vector2 CalculateAngles(Vector3 from, Vector3 to)
        {
            float yaw;
            float pitch;

            float deltaX = to.X - from.X;
            float deltaY = to.Y - from.Y;
            yaw = (float)(Math.Atan2(deltaY, deltaX) * 180 / Math.PI);

            float deltaZ = to.Z - from.Z;
            double distance = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));
            pitch = -(float)(Math.Atan2(deltaZ, distance) * 180 / Math.PI);

            return new Vector2(yaw, pitch);
        }

        public static float CalculateFOV(Vector3 viewAngles, Vector2 targetAngles)
        {
            float yawDiff = Math.Abs(viewAngles.Y - targetAngles.X);
            float pitchDiff = Math.Abs(viewAngles.X - targetAngles.Y);

            // Handle yaw wrap-around
            if (yawDiff > 180) yawDiff = 360 - yawDiff;

            return (float)Math.Sqrt(Math.Pow(yawDiff, 2) + Math.Pow(pitchDiff, 2));
        }

        public static string GetWeaponName(int id)
        {
            switch (id)
            {
                case 1: return "Deagle";
                case 2: return "Berettas";
                case 3: return "Five-Seven";
                case 4: return "Glock";
                case 7: return "AK-47";
                case 8: return "AUG";
                case 9: return "AWP";
                case 10: return "FAMAS";
                case 11: return "G3SG1";
                case 13: return "Galil";
                case 14: return "M249";
                case 16: return "M4A4";
                case 17: return "MAC-10";
                case 19: return "P90";
                case 23: return "MP5-SD";
                case 24: return "UMP-45";
                case 25: return "XM1014";
                case 26: return "PP-Bizon";
                case 27: return "MAG-7";
                case 28: return "Negev";
                case 29: return "Sawed-Off";
                case 30: return "Tec-9";
                case 31: return "Zeus";
                case 32: return "P2000";
                case 33: return "MP7";
                case 34: return "MP9";
                case 35: return "Nova";
                case 36: return "P250";
                case 38: return "SCAR-20";
                case 39: return "SG 553";
                case 40: return "SSG 08";
                case 42: return "Knife";
                case 60: return "M4A1-S";
                case 61: return "USP-S";
                case 63: return "CZ75-Auto";
                case 64: return "R8 Revolver";
                default: return "Weapon";
            }
        }

        public static Vector3 Smooth(Vector3 currentAngles, Vector2 targetAngles, float smoothFactor)
        {
            if (smoothFactor <= 1.0f) return new Vector3(targetAngles.Y, targetAngles.X, 0);

            float yawDiff = targetAngles.X - currentAngles.Y;
            float pitchDiff = targetAngles.Y - currentAngles.X;

            if (yawDiff > 180) yawDiff -= 360;
            if (yawDiff < -180) yawDiff += 360;

            float smoothYaw = currentAngles.Y + yawDiff / smoothFactor;
            float smoothPitch = currentAngles.X + pitchDiff / smoothFactor;

            return new Vector3(smoothPitch, smoothYaw, 0);
        }

        public static bool WorldToScreen(float[] m, Vector3 pos, out Vector2 screenPos, Vector2 screenSize)
        {
            float w = m[12] * pos.X + m[13] * pos.Y + m[14] * pos.Z + m[15];

            if (w < 0.001f)
            {
                w = m[3] * pos.X + m[7] * pos.Y + m[11] * pos.Z + m[15];
            }

            if (w < 0.001f)
            {
                screenPos = Vector2.Zero;
                return false;
            }

            float invw = 1.0f / w;

            float x = (m[0] * pos.X + m[1] * pos.Y + m[2] * pos.Z + m[3]) * invw;
            float y = (m[4] * pos.X + m[5] * pos.Y + m[6] * pos.Z + m[7]) * invw;

            if (Math.Abs(x) > 2.0f || Math.Abs(y) > 2.0f)
            {
                x = (m[0] * pos.X + m[4] * pos.Y + m[8] * pos.Z + m[12]) * invw;
                y = (m[1] * pos.X + m[5] * pos.Y + m[9] * pos.Z + m[13]) * invw;
            }

            screenPos.X = (screenSize.X / 2.0f) + (screenSize.X / 2.0f) * x;
            screenPos.Y = (screenSize.Y / 2.0f) - (screenSize.Y / 2.0f) * y;

            return true;
        }

        public static Vector3 SmoothConstant(Vector3 current, Vector2 target, float speed)
        {
            Vector2 diff = new Vector2(target.X - current.Y, target.Y - current.X);

            if (diff.X > 180) diff.X -= 360;
            if (diff.X < -180) diff.X += 360;

            if (diff.Length() < speed) return new Vector3(target.Y, target.X, 0);

            Vector2 move = Vector2.Normalize(diff) * speed;
            return new Vector3(current.X + move.Y, current.Y + move.X, 0);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cs2external
{
    public class Offsets
    {
        // Buttons
        public static int dwForceAttack = 0x2066760;

        // Base entries
        public static int dwViewAngles = 0x231E9B8;
        public static int dwLocalPlayerPawn = 0x206D9E0;
        public static int dwEntityList = 0x24B3268;
        public static int dwViewMatrix = 0x2313F10;
        public static int m_entitySpottedState = 0x26E0;

        // Member offsets
        public static int m_bSpottedByMask = 0xC;
        public static int m_vOldOrigin = 0x1588;
        public static int m_hPlayerPawn = 0x90C;
        public static int m_vecViewOffset = 0xD58;
        public static int m_iHealth = 0x354;
        public static int m_iTeamNum = 0x3F3;
        public static int m_lifeState = 0x35C;
        public static int m_iIDEntIndex = 0x3EAC;
        public static int m_vecVelocity = 0x438;
        public static int m_pClippingWeapon = 0x3DC0;
        public static int m_iItemDefinitionIndex = 0x1BA;
        public static int m_aimPunchAngle = 0x1568; // RECOIL RE-CALIBRATED
        public static int m_iShotsFired = 0x2280; // RECOIL RE-CALIBRATED
        public static int m_fFlags = 0x3CC; // BHOP RE-CALIBRATED

        // Bone offsets
        public static int m_pGameSceneNode = 0x338;
        public static int m_modelState = 0x160;

        // Visibility
        public static int m_bSpotted = 0x8;

        // Bone Indices
        public static int bone_head = 6;
        public static int bone_neck = 5;
        public static int bone_spine = 4;
        public static int bone_l_arm = 13;
        public static int bone_r_arm = 26;
        public static int bone_l_leg = 22;
        public static int bone_r_leg = 25;
    }
}

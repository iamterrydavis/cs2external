using cs2external;
using Swed64;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;
using ClickableTransparentOverlay;

// Set Process Priority to High for maximum responsiveness
Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

[DllImport("kernel32.dll")]
static extern IntPtr GetConsoleWindow();
[DllImport("user32.dll")]
static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
[DllImport("user32.dll")]
static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")]
static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

// Hide Console on Startup
ShowWindow(GetConsoleWindow(), 0); // 0 = SW_HIDE

Swed swed = new Swed("cs2");
IntPtr client = swed.GetModuleBase("client.dll");
Random random = new Random();

Renderer renderer = new Renderer();
renderer.ReplaceFont(@"C:\Windows\Fonts\segoeui.ttf", 18, FontGlyphRangeType.English);
renderer.Start().Wait();

List<Entity> entities = new List<Entity>();
Entity localPlayer = new Entity();
int localPlayerIndex = -1;
Vector3 oldPunch = Vector3.Zero;
DateTime lastShotTime = DateTime.MinValue;

const int ENTITY_STRIDE = 0x70;
int[] skeletonBones = { 0, 2, 4, 5, 6, 8, 9, 10, 13, 14, 16, 22, 23, 24, 25, 26, 27, 28 };
int espUpdateCounter = 0;

while (true)
{
    Thread.Sleep(5);

    // Only render/process if CS2 is active
    IntPtr handle = GetForegroundWindow();
    StringBuilder title = new StringBuilder(256);
    GetWindowText(handle, title, 256);
    string currentTitle = title.ToString();
    bool isGameActive = currentTitle.Contains("Counter-Strike 2") || currentTitle.Contains("Overlay");

    renderer.showMenuEnabled = isGameActive;
    if (!isGameActive) { Thread.Sleep(100); continue; }

    IntPtr entityList = swed.ReadPointer(client, Offsets.dwEntityList);
    var config = renderer.config;
    Vector2 screenSize = renderer.GetScreenSize();

    if ((GetAsyncKeyState(0x2D) & 0x8000) != 0)
    {
        renderer.showMenu = !renderer.showMenu;
        Thread.Sleep(200);
    }

    // Local Player Sync
    localPlayer.pawnAdress = swed.ReadPointer(client, Offsets.dwLocalPlayerPawn);
    if (localPlayer.pawnAdress != IntPtr.Zero)
    {
        localPlayer.team = swed.ReadInt(localPlayer.pawnAdress, Offsets.m_iTeamNum);
        localPlayer.origin = swed.ReadVec(localPlayer.pawnAdress, Offsets.m_vOldOrigin);
        localPlayer.view = swed.ReadVec(localPlayer.pawnAdress, Offsets.m_vecViewOffset);
    }

    Vector3 currentViewAngles = swed.ReadVec(client, Offsets.dwViewAngles);
    float[] viewMatrix = swed.ReadMatrix(client + Offsets.dwViewMatrix);

    // 1. RCS ENGINE
    if (config.rcs && localPlayer.pawnAdress != IntPtr.Zero)
    {
        int shotsFired = swed.ReadInt(localPlayer.pawnAdress, Offsets.m_iShotsFired);
        Vector3 aimPunch = swed.ReadVec(localPlayer.pawnAdress, Offsets.m_aimPunchAngle);

        if (shotsFired > 1)
        {
            Vector3 delta = aimPunch - oldPunch;
            Vector3 newAngles = currentViewAngles;

            newAngles.X -= delta.X * config.rcsAmount;
            newAngles.Y -= delta.Y * config.rcsAmount;

            if (newAngles.X > 89.0f) newAngles.X = 89.0f;
            if (newAngles.X < -89.0f) newAngles.X = -89.0f;
            while (newAngles.Y > 180.0f) newAngles.Y -= 360.0f;
            while (newAngles.Y < -180.0f) newAngles.Y += 360.0f;

            swed.WriteVec(client, Offsets.dwViewAngles, newAngles);
        }
        oldPunch = aimPunch;
    }
    else { oldPunch = Vector3.Zero; }

    // 3. TRIGGERBOT
    if (config.tbot && (GetAsyncKeyState(config.tbotKey) & 0x8000) != 0 && localPlayer.pawnAdress != IntPtr.Zero)
    {
        int entIndex = swed.ReadInt(localPlayer.pawnAdress, Offsets.m_iIDEntIndex);
        if (entIndex > 0)
        {
            nint entityEntry = (nint)swed.ReadLong(entityList + (8 * (entIndex >> 9) + 0x10));
            nint entity = (nint)swed.ReadLong(entityEntry + 120 * (entIndex & 0x1FF));

            int entityTeam = swed.ReadInt((IntPtr)entity, Offsets.m_iTeamNum);
            if (entityTeam != localPlayer.team && (DateTime.Now - lastShotTime).TotalMilliseconds >= config.tbotDelay)
            {
                swed.WriteInt(client, Offsets.dwForceAttack, 65537);
                Thread.Sleep(1);
                swed.WriteInt(client, Offsets.dwForceAttack, 256);
                lastShotTime = DateTime.Now;
            }
        }
    }

    // 4. ENTITY & VISUAL ENGINE
    List<Entity> frameEntities = new List<Entity>();
    bool doHeavyEsp = (espUpdateCounter % 8 == 0);

    for (int i = 0; i < 128; i++)
    {
        IntPtr listEntry = swed.ReadPointer(entityList, 0x8 * (i >> 9) + 0x10);
        if (listEntry == IntPtr.Zero) continue;

        IntPtr currentController = swed.ReadPointer(listEntry, ENTITY_STRIDE * (i & 0x1FF));
        if (currentController == IntPtr.Zero) continue;

        int pawnHandle = swed.ReadInt(currentController, Offsets.m_hPlayerPawn);
        if (pawnHandle == 0) continue;

        IntPtr listEntry2 = swed.ReadPointer(entityList, 0x8 * ((pawnHandle & 0x7FFF) >> 9) + 0x10);
        if (listEntry2 == IntPtr.Zero) continue;

        IntPtr currentPawn = swed.ReadPointer(listEntry2, ENTITY_STRIDE * (pawnHandle & 0x1FF));
        if (currentPawn == IntPtr.Zero) continue;

        if (currentPawn == localPlayer.pawnAdress) { localPlayerIndex = i; continue; }

        int team = swed.ReadInt(currentPawn, Offsets.m_iTeamNum);
        if (team == localPlayer.team && !config.espTeam && !config.aimOnTeam) continue;

        int health = swed.ReadInt(currentPawn, Offsets.m_iHealth);
        uint lifeState = swed.ReadUInt(currentPawn, Offsets.m_lifeState);
        if (health <= 0 || health > 100 || lifeState != 256) continue;

        Entity entity = new Entity { pawnAdress = currentPawn, health = health, team = team };
        entity.origin = swed.ReadVec(currentPawn, Offsets.m_vOldOrigin);
        entity.view = swed.ReadVec(currentPawn, Offsets.m_vecViewOffset);

        // Visibility
        IntPtr spottedState = currentPawn + Offsets.m_entitySpottedState;
        ulong spottedMask = (ulong)swed.ReadLong(spottedState, Offsets.m_bSpottedByMask);
        entity.isVisible = (spottedMask & (1UL << (localPlayerIndex - 1))) != 0 || swed.ReadBool(spottedState, Offsets.m_bSpotted);

        // Bone data (needed for head and skeleton)
        Vector3 headPos = Vector3.Zero;
        Dictionary<int, Vector3> bonePositions = null;
        IntPtr sceneNode = swed.ReadPointer(currentPawn, Offsets.m_pGameSceneNode);
        IntPtr boneArray = swed.ReadPointer(sceneNode, Offsets.m_modelState + 0x80);
        if (boneArray != IntPtr.Zero)
        {
            byte[] boneBytes = swed.ReadBytes(boneArray, 1024);
            Vector3 GetBonePos(int index)
            {
                int baseIdx = index * 32;
                if (baseIdx + 12 > boneBytes.Length) return Vector3.Zero;
                return new Vector3(
                    BitConverter.ToSingle(boneBytes, baseIdx),
                    BitConverter.ToSingle(boneBytes, baseIdx + 4),
                    BitConverter.ToSingle(boneBytes, baseIdx + 8));
            }
            headPos = GetBonePos(Offsets.bone_head);
            entity.head = headPos;
            entity.fov = Calculator.CalculateFOV(currentViewAngles, Calculator.CalculateAngles(Vector3.Add(localPlayer.origin, localPlayer.view), headPos));

            if (doHeavyEsp && config.espSkeleton)
            {
                bonePositions = new Dictionary<int, Vector3>();
                foreach (int boneId in skeletonBones)
                    bonePositions[boneId] = GetBonePos(boneId);
            }
        }

        // Heavy ESP (only every 8th loop)
        if (doHeavyEsp)
        {
            if (config.espName)
                entity.name = swed.ReadString(currentController, 0x6F8, 32);

            // World-to-screen for origin
            if (Calculator.WorldToScreen(viewMatrix, entity.origin, out Vector2 o2D, screenSize))
                entity.origin2D = o2D;
            else
                entity.origin2D = new Vector2(-1000, -1000);

            // Head 2D
            if (headPos != Vector3.Zero && Calculator.WorldToScreen(viewMatrix, headPos, out Vector2 h2D, screenSize))
                entity.head2D = h2D;
            else
                entity.head2D = new Vector2(-1000, -1000);

            // Skeleton
            if (config.espSkeleton && bonePositions != null)
            {
                foreach (var kv in bonePositions)
                {
                    if (Calculator.WorldToScreen(viewMatrix, kv.Value, out Vector2 b2D, screenSize))
                        entity.bone2D[kv.Key] = b2D;
                    else
                        entity.bone2D[kv.Key] = new Vector2(-1000, -1000);
                }
            }

            // Weapon name
            if (config.espWeapon)
            {
                IntPtr clippingWeapon = swed.ReadPointer(currentPawn, Offsets.m_pClippingWeapon);
                if (clippingWeapon != IntPtr.Zero)
                    entity.weaponName = Calculator.GetWeaponName(swed.ReadInt(clippingWeapon, Offsets.m_iItemDefinitionIndex));
            }
        }

        frameEntities.Add(entity);
    }

    // Update overlay with fresh ESP data only when heavy ESP ran
    if (doHeavyEsp)
    {
        lock (renderer.entities)
        {
            renderer.entities.Clear();
            renderer.entities.AddRange(frameEntities);
        }
    }
    espUpdateCounter++;

    // 5. AIM ENGINE
    if (config.aimbot && (GetAsyncKeyState(config.aimKey) & 0x8000) != 0)
    {
        var target = frameEntities.Where(e => e.fov <= config.aimFov && (!config.visibilityCheck || e.isVisible)).OrderBy(o => o.fov).FirstOrDefault();
        if (target != null)
        {
            Vector2 targetAngles = Calculator.CalculateAngles(Vector3.Add(localPlayer.origin, localPlayer.view), target.head);
            swed.WriteVec(client, Offsets.dwViewAngles, Calculator.Smooth(currentViewAngles, targetAngles, config.aimSmoothing));
        }
    }
}

[DllImport("user32.dll")]
static extern short GetAsyncKeyState(int vKey);
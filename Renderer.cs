using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Linq;
using ClickableTransparentOverlay;
using ImGuiNET;

namespace cs2external
{
    public class Config
    {
        public bool aimbot = true;
        public float aimFov = 35.0f;
        public float aimSmoothing = 1.0f;
        public bool aimOnTeam = false;
        public bool visibilityCheck = true;

        // ESP Settings
        public bool espEnabled = true;
        public bool espTeam = false;
        public bool espBox = true;
        public bool espCorner = true;
        public bool espHealth = true;
        public bool espName = true;
        public bool espWeapon = true;
        public bool espSkeleton = true;

        // Outline Toggles
        public bool boxOutline = true;
        public bool skeletonOutline = true;
        public bool healthOutline = true;
        public bool nameOutline = true;
        public bool weaponOutline = true;

        // Colors
        public Vector4 boxColor = new Vector4(1, 0, 0, 1);
        public Vector4 skeletonColor = new Vector4(1, 1, 1, 1);
        public Vector4 nameColor = new Vector4(1, 1, 1, 1);
        public Vector4 weaponColor = new Vector4(1, 1, 0, 1);
        public Vector4 healthColorTop = new Vector4(0, 1, 0, 1);
        public Vector4 healthColorBottom = new Vector4(1, 0, 0, 1);
        public uint outlineColor = 0xFF000000;

        // Thickness / Width
        public float boxThickness = 1.2f;
        public float skeletonThickness = 1.0f;
        public float healthWidth = 3.0f;

        // Misc
        public bool rcs = true;
        public float rcsAmount = 2.0f;
        public bool tbot = true;
        public int tbotDelay = 500;
        public bool vSync = false;

        // Binds
        public int aimKey = 0x01;
        public int tbotKey = 0x06;
    }

    public class Renderer : Overlay
    {
        public Renderer() : base(false)
        {
            RefreshConfigs();
        }

        public Config config = new Config();
        public List<Entity> entities = new List<Entity>();
        private int selectedTab = 0;
        public bool showMenu = true;
        public bool showMenuEnabled = true; // Synced with focus
        private bool isBinding = false;
        private int bindingTarget = -1;
        private bool isWaitingForRelease = false;
        
        private string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "external636");
        private string currentConfigName = "default";
        private List<string> configList = new List<string>();
        private int selectedConfigIndex = 0;
        private string configStatus = "";
        private DateTime statusTime = DateTime.MinValue;



        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")]
        static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);
        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private bool taskbarHidden = false;

        private void RefreshConfigs()
        {
            try
            {
                if (!Directory.Exists(configPath)) Directory.CreateDirectory(configPath);
                configList = Directory.GetFiles(configPath, "*.json").Select(Path.GetFileNameWithoutExtension).ToList()!;
            }
            catch { }
        }

        public void SaveConfig(string name)
        {
            try
            {
                if (!Directory.Exists(configPath)) Directory.CreateDirectory(configPath);
                string fullPath = Path.Combine(configPath, name + ".json");
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(fullPath, json);
                configStatus = $"Saved: {name}";
                statusTime = DateTime.Now;
                RefreshConfigs();
            }
            catch { configStatus = "Save Error!"; }
        }

        public void LoadConfig(string name)
        {
            try
            {
                string fullPath = Path.Combine(configPath, name + ".json");
                if (File.Exists(fullPath))
                {
                    string json = File.ReadAllText(fullPath);
                    config = JsonSerializer.Deserialize<Config>(json) ?? new Config();
                    configStatus = $"Loaded: {name}";
                    statusTime = DateTime.Now;
                }
                else { configStatus = "File Missing!"; }
            }
            catch { configStatus = "Load Error!"; }
        }

        public string GetKeyName(int vKey)
        {
            switch (vKey)
            {
                case 0x01: return "MB1"; case 0x02: return "MB2"; case 0x05: return "MB4"; case 0x06: return "MB5";
                case 0x12: return "LAlt"; case 0x10: return "LShift"; case 0x20: return "Space"; case 0x2D: return "Insert";
            }
            if (vKey >= 0x41 && vKey <= 0x5A) return ((char)vKey).ToString();
            return vKey.ToString();
        }

        public Vector2 GetScreenSize()
        {
            IntPtr hwnd = FindWindow(null, "Counter-Strike 2");
            if (hwnd == IntPtr.Zero) hwnd = FindWindow("SDL_app", "Counter-Strike 2");
            if (hwnd != IntPtr.Zero && GetClientRect(hwnd, out RECT rect)) return new Vector2(rect.Right - rect.Left, rect.Bottom - rect.Top);
            return new Vector2(GetSystemMetrics(0), GetSystemMetrics(1));
        }

        private void HideFromTaskbar()
        {
            IntPtr hwnd = FindWindow(null, "Overlay"); // Default name for ClickableTransparentOverlay
            if (hwnd != IntPtr.Zero)
            {
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
                taskbarHidden = true;
            }
        }

        private void SetupStyle()
        {
            var style = ImGui.GetStyle();
            
            // Rounding & Spacing (DeepBlue standard)
            style.WindowRounding = 16.0f;
            style.ChildRounding = 8.0f;
            style.FrameRounding = 4.0f;
            style.PopupRounding = 4.0f;
            style.ScrollbarRounding = 9.0f;
            style.GrabRounding = 4.0f;
            style.TabRounding = 4.0f;
            
            style.WindowBorderSize = 0.0f;
            style.ChildBorderSize = 1.0f; // Subtle card outlines
            style.PopupBorderSize = 1.0f;
            style.FrameBorderSize = 0.0f;
            
            style.ItemSpacing = new Vector2(12, 12);
            style.WindowPadding = new Vector2(15, 15);
            style.WindowTitleAlign = new Vector2(0.5f, 0.5f);

            var colors = style.Colors;
            // Colors from DeepBlue settings.h
            colors[(int)ImGuiCol.WindowBg] = new Vector4(0.03f, 0.04f, 0.05f, 0.94f); // #080a0e
            colors[(int)ImGuiCol.ChildBg] = new Vector4(0.05f, 0.06f, 0.09f, 1.00f);  // #0e1016
            colors[(int)ImGuiCol.Border] = new Vector4(0.12f, 0.15f, 0.18f, 0.50f);
            
            colors[(int)ImGuiCol.Header] = new Vector4(0.23f, 0.39f, 0.61f, 1.00f);      // #3a649c
            colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.28f, 0.45f, 0.70f, 1.00f);
            colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.36f, 0.64f, 1.00f, 1.00f); // Accent: #5ca2ff
            
            colors[(int)ImGuiCol.Button] = new Vector4(0.10f, 0.12f, 0.18f, 1.00f);
            colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.14f, 0.16f, 0.24f, 1.00f);
            colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.36f, 0.64f, 1.00f, 1.00f);
            
            colors[(int)ImGuiCol.FrameBg] = new Vector4(0.06f, 0.07f, 0.10f, 1.00f);
            colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.10f, 0.12f, 0.18f, 1.00f);
            colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.14f, 0.16f, 0.24f, 1.00f);
            
            colors[(int)ImGuiCol.CheckMark] = new Vector4(0.36f, 0.64f, 1.00f, 1.00f);
            colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.36f, 0.64f, 1.00f, 1.00f);
            colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.50f, 0.75f, 1.00f, 1.00f);
            
            colors[(int)ImGuiCol.Text] = new Vector4(0.49f, 0.53f, 0.61f, 1.00f);       // #7e889c
            colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.30f, 0.33f, 0.40f, 1.00f);
            
            colors[(int)ImGuiCol.Separator] = new Vector4(0.09f, 0.10f, 0.13f, 1.00f); // #161a22
        }

        protected override void Render()
        {
            if (!taskbarHidden) HideFromTaskbar();
            SetupStyle();
            this.VSync = config.vSync;
            if (showMenu && showMenuEnabled) DrawMenu();
            if (showMenuEnabled)
            {
                DrawESP();
                DrawFOVCircle();
            }
            if (isBinding) HandleBinding();
        }

        private void DrawFOVCircle()
        {
            if (!config.aimbot) return;
            var drawList = ImGui.GetBackgroundDrawList();
            Vector2 screen = GetScreenSize();
            float radius = (config.aimFov / 106.0f) * screen.X;
            drawList.AddCircle(new Vector2(screen.X / 2.0f, screen.Y / 2.0f), radius, ImGui.ColorConvertFloat4ToU32(new Vector4(0.0f, 0.60f, 1.00f, 0.15f)), 100, 1.5f);
        }

        private void DrawMenu()
        {
            ImGui.SetNextWindowSize(new Vector2(850, 600), ImGuiCond.Always);
            
            // Remove ImGui window padding so we can handle it manually perfectly like DeepBlue
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
            // Start Menu Window
            ImGui.Begin("DEEPBLUE_OVERLAY", ref showMenu, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoBackground);
            ImGui.PopStyleVar();
            
            var drawList = ImGui.GetWindowDrawList();
            Vector2 pos = ImGui.GetWindowPos();
            Vector2 size = ImGui.GetWindowSize();
            
            // Base Background
            drawList.AddRectFilled(pos, pos + size, ImGui.ColorConvertFloat4ToU32(new Vector4(0.03f, 0.04f, 0.05f, 0.94f)), 16.0f); // WindowBg

            // Layout Variables (Straight from gui.cc)
            Vector2 frameMargin = new Vector2(15, 15);
            Vector2 innerMin = pos + frameMargin;
            Vector2 innerMax = pos + size - frameMargin;
            Vector2 innerSize = innerMax - innerMin;

            float leftWidth = 130.0f;
            float gutter = 12.0f;

            // 1. SIDEBAR AREA
            Vector2 tabFrameMin = innerMin;
            Vector2 tabFrameMax = new Vector2(innerMin.X + leftWidth, innerMax.Y);

            // Centering math for tabs
            string[] tabNames = { "AIMBOT", "VISUALS", "MISC", "CONFIGS", "BINDS" };
            float tabItemHeight = 40.0f;
            float tabSpacing = 8.0f;
            float totalTabs = tabItemHeight * tabNames.Length + tabSpacing * (tabNames.Length - 1);
            float yOffset = Math.Max(0.0f, (innerSize.Y - totalTabs) * 0.5f);
            float xOffset = 10.0f;

            ImGui.SetCursorPos(frameMargin + new Vector2(xOffset, yOffset));
            ImGui.BeginChild("tab_column", new Vector2(leftWidth - 10, innerSize.Y), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground);
            {
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, tabSpacing));
                for (int i = 0; i < tabNames.Length; i++)
                {
                    bool isSelected = (selectedTab == i);
                    Vector2 pStart = ImGui.GetCursorScreenPos();
                    
                    if (isSelected)
                    {
                        // Accent indicator
                        Vector2 p1 = pStart + new Vector2(-10, 5);
                        Vector2 p2 = pStart + new Vector2(-7, 35);
                        drawList.AddRectFilled(p1, p2, ImGui.ColorConvertFloat4ToU32(new Vector4(0.36f, 0.64f, 1.00f, 1.00f)), 2.0f);
                    }

                    ImGui.PushStyleColor(ImGuiCol.Text, isSelected ? new Vector4(0.86f, 0.90f, 0.96f, 1.00f) : new Vector4(0.49f, 0.53f, 0.61f, 1.00f));
                    if (ImGui.Selectable($"  {tabNames[i]}", isSelected, ImGuiSelectableFlags.None, new Vector2(leftWidth - 10, tabItemHeight))) selectedTab = i;
                    ImGui.PopStyleColor();
                }
                ImGui.PopStyleVar();
            }
            ImGui.EndChild();

            // 2. MAIN CONTENT AREA
            Vector2 contentMin = new Vector2(innerMin.X + leftWidth + gutter, innerMin.Y);
            Vector2 contentMax = innerMax;
            
            // Content Layout Background
            drawList.AddRectFilled(contentMin, contentMax, ImGui.ColorConvertFloat4ToU32(new Vector4(0.05f, 0.06f, 0.09f, 1.00f)), 8.0f);
            drawList.AddRect(contentMin, contentMax, ImGui.ColorConvertFloat4ToU32(new Vector4(0.12f, 0.15f, 0.18f, 0.50f)), 8.0f, 0, 1.0f);

            ImGui.SetCursorPos((contentMin - pos) + new Vector2(15, 15));
            ImGui.BeginChild("content", contentMax - contentMin - new Vector2(30, 30), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground);
            {
                float colWidth = (contentMax.X - contentMin.X - 30 - 20) / 2.0f; // Calculate proper 2-column width

                if (selectedTab == 0) // AIMBOT
                {
                    ImGui.BeginGroup(); // Left Column
                    {
                        DrawCard("Ragebot", new Vector2(colWidth, 240), () => {
                            ImGui.Checkbox("Aimbot Master", ref config.aimbot);
                            ImGui.Checkbox("Visibility Check", ref config.visibilityCheck);
                            ImGui.Separator();
                            ImGui.SliderFloat("Field of View", ref config.aimFov, 1.0f, 180.0f);
                        });
                        
                        DrawCard("Other", new Vector2(colWidth, 200), () => {
                            ImGui.SliderFloat("Smoothing Level", ref config.aimSmoothing, 1.0f, 100.0f);
                            ImGui.Checkbox("Aim on Team", ref config.aimOnTeam);
                        });
                    }
                    ImGui.EndGroup();

                    ImGui.SameLine(0, 15);

                    ImGui.BeginGroup(); // Right Column
                    {
                        DrawCard("Recoil", new Vector2(colWidth, 240), () => {
                            ImGui.Checkbox("Auto Compensation", ref config.rcs);
                            ImGui.SliderFloat("RCS Strength", ref config.rcsAmount, 0.0f, 2.0f);
                        });
                        
                        DrawCard("Trigger", new Vector2(colWidth, 200), () => {
                            ImGui.Checkbox("Triggerbot", ref config.tbot);
                            ImGui.SliderInt("Trigger Delay", ref config.tbotDelay, 0, 1000);
                        });
                    }
                    ImGui.EndGroup();
                }
                else if (selectedTab == 1) // VISUALS
                {
                    ImGui.BeginGroup(); // Left Column
                    {
                        DrawCard("Player ESP", new Vector2(colWidth, 310), () => {
                            ImGui.Checkbox("Enable ESP", ref config.espEnabled);
                            ImGui.Checkbox("Teammates", ref config.espTeam);
                            ImGui.Separator();
                            ImGui.Checkbox("Draw Box", ref config.espBox);
                            ImGui.Checkbox("Draw Corner Box", ref config.espCorner);
                            ImGui.Checkbox("Health Bar", ref config.espHealth);
                            ImGui.Checkbox("Player Names", ref config.espName);
                            ImGui.Checkbox("Weapon Text", ref config.espWeapon);
                            ImGui.Checkbox("Skeleton rendering", ref config.espSkeleton);
                        });
                    }
                    ImGui.EndGroup();

                    ImGui.SameLine(0, 15);

                    ImGui.BeginGroup(); // Right Column
                    {
                        DrawCard("Colors", new Vector2(colWidth, 310), () => {
                            ImGui.Checkbox("Outlines", ref config.boxOutline);
                            ImGui.ColorEdit4("Box Color", ref config.boxColor, ImGuiColorEditFlags.NoInputs);
                            ImGui.ColorEdit4("Skeleton Color", ref config.skeletonColor, ImGuiColorEditFlags.NoInputs);
                            ImGui.ColorEdit4("Name Color", ref config.nameColor, ImGuiColorEditFlags.NoInputs);
                            ImGui.ColorEdit4("Weapon Color", ref config.weaponColor, ImGuiColorEditFlags.NoInputs);
                            ImGui.ColorEdit4("Health Top", ref config.healthColorTop, ImGuiColorEditFlags.NoInputs);
                            ImGui.ColorEdit4("Health Bottom", ref config.healthColorBottom, ImGuiColorEditFlags.NoInputs);
                        });
                    }
                    ImGui.EndGroup();
                }
                else if (selectedTab == 2) // MISC
                {
                    ImGui.BeginGroup();
                    {
                        DrawCard("Settings", new Vector2(colWidth, 200), () => {
                            ImGui.Checkbox("VSync (Lock FPS)", ref config.vSync);
                        });
                    }
                    ImGui.EndGroup();
                }
                else if (selectedTab == 3) // CONFIGS
                {
                    DrawCard("Profile Manager", new Vector2(colWidth * 2 + 15, 450), () => {
                        ImGui.InputText("File Name", ref currentConfigName, 32);
                        if (ImGui.Button("SAVE TO DISK", new Vector2(colWidth, 35))) SaveConfig(currentConfigName);
                        ImGui.Separator();
                        if (ImGui.BeginListBox("##profiles", new Vector2(-1, 200)))
                        {
                            for (int n = 0; n < configList.Count; n++)
                            {
                                bool isSelected = (selectedConfigIndex == n);
                                if (ImGui.Selectable(configList[n], isSelected)) selectedConfigIndex = n;
                            }
                            ImGui.EndListBox();
                        }
                        if (ImGui.Button("LOAD", new Vector2(120, 35)) && configList.Count > 0) LoadConfig(configList[selectedConfigIndex]); ImGui.SameLine();
                        if (ImGui.Button("REFRESH", new Vector2(120, 35))) RefreshConfigs();
                        if ((DateTime.Now - statusTime).TotalSeconds < 3) ImGui.TextColored(new Vector4(0, 1, 0, 1), configStatus);
                    });
                }
                else if (selectedTab == 4) // BINDS
                {
                    DrawCard("Input Bindings", new Vector2(colWidth * 2 + 15, 200), () => {
                        if (ImGui.Button($"AIM KEY: {GetKeyName(config.aimKey).ToUpper()}", new Vector2(-1, 45))) { isBinding = true; isWaitingForRelease = true; bindingTarget = 0; }
                        ImGui.Spacing();
                        if (ImGui.Button($"TRIGGER KEY: {GetKeyName(config.tbotKey).ToUpper()}", new Vector2(-1, 45))) { isBinding = true; isWaitingForRelease = true; bindingTarget = 2; }
                    });
                }
            }
            ImGui.EndChild();

            // Footer / Close Button
            ImGui.SetCursorPos(new Vector2(size.X - 45, 10));
            if (ImGui.Button("X", new Vector2(35, 25))) showMenu = false;

            ImGui.End();
        }

        private void DrawCard(string title, Vector2 size, Action content)
        {
            ImGui.BeginChild(title, size, ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar);
            {
                // DeepBlue replica card header
                ImGui.TextColored(new Vector4(0.36f, 0.64f, 1.00f, 1.00f), $" {title}");
                ImGui.Separator();
                ImGui.Spacing();
                
                ImGui.BeginGroup();
                content();
                ImGui.EndGroup();
            }
            ImGui.EndChild();
        }

        private void HandleBinding()
        {
            if (isWaitingForRelease) { if ((GetAsyncKeyState(0x01) & 0x8000) == 0) isWaitingForRelease = false; return; }
            for (int i = 0x01; i < 255; i++)
            {
                if ((GetAsyncKeyState(i) & 0x8000) != 0)
                {
                    if (bindingTarget == 0) config.aimKey = i;
                    else if (bindingTarget == 2) config.tbotKey = i;
                    isBinding = false; bindingTarget = -1; break;
                }
            }
        }

        private void DrawESP()
        {
            if (!config.espEnabled) return;
            var drawList = ImGui.GetBackgroundDrawList();
            List<Entity> tempEntities;
            lock (entities) { tempEntities = new List<Entity>(entities); }

            foreach (var entity in tempEntities)
            {
                if (entity.origin2D.X < 1 || entity.head2D.X < 1) continue;

                float height = Math.Abs(entity.origin2D.Y - entity.head2D.Y);
                float width = height / 1.8f;
                Vector2 topLeft = new Vector2(entity.head2D.X - width / 2.0f, entity.head2D.Y);
                Vector2 bottomRight = new Vector2(entity.origin2D.X + width / 2.0f, entity.origin2D.Y);

                if (config.espBox)
                {
                    if (config.espCorner) DrawCornerBox(drawList, topLeft, bottomRight, ImGui.ColorConvertFloat4ToU32(config.boxColor), config.boxThickness, config.boxOutline);
                    else
                    {
                        if (config.boxOutline) drawList.AddRect(topLeft, bottomRight, config.outlineColor, 0, 0, config.boxThickness + 1.2f);
                        drawList.AddRect(topLeft, bottomRight, ImGui.ColorConvertFloat4ToU32(config.boxColor), 0, 0, config.boxThickness);
                    }
                }

                if (config.espHealth)
                {
                    float h = height * (entity.health / 100.0f);
                    Vector2 hTop = new Vector2(topLeft.X - 8, bottomRight.Y - h);
                    Vector2 hBot = new Vector2(topLeft.X - 8 + config.healthWidth, bottomRight.Y);
                    if (config.healthOutline) drawList.AddRect(new Vector2(hTop.X - 1, bottomRight.Y - height - 1), new Vector2(hBot.X + 1, hBot.Y + 1), config.outlineColor, 0, 0, 1.0f);
                    drawList.AddRectFilledMultiColor(hTop, hBot, ImGui.ColorConvertFloat4ToU32(config.healthColorTop), ImGui.ColorConvertFloat4ToU32(config.healthColorTop), ImGui.ColorConvertFloat4ToU32(config.healthColorBottom), ImGui.ColorConvertFloat4ToU32(config.healthColorBottom));
                }

                if (config.espName)
                {
                    Vector2 tS = ImGui.CalcTextSize(entity.name);
                    Vector2 tP = new Vector2(entity.head2D.X - tS.X / 2.0f, topLeft.Y - tS.Y - 2);
                    if (config.nameOutline) drawList.AddText(new Vector2(tP.X + 1, tP.Y + 1), config.outlineColor, entity.name);
                    drawList.AddText(tP, ImGui.ColorConvertFloat4ToU32(config.nameColor), entity.name);
                }

                if (config.espWeapon)
                {
                    Vector2 tS = ImGui.CalcTextSize(entity.weaponName);
                    Vector2 tP = new Vector2(entity.origin2D.X - tS.X / 2.0f, bottomRight.Y + 2);
                    if (config.weaponOutline) drawList.AddText(new Vector2(tP.X + 1, tP.Y + 1), config.outlineColor, entity.weaponName);
                    drawList.AddText(tP, ImGui.ColorConvertFloat4ToU32(config.weaponColor), entity.weaponName);
                }

                if (config.espSkeleton) DrawSkeleton(drawList, entity);
            }
        }

        private void DrawCornerBox(ImDrawListPtr drawList, Vector2 topLeft, Vector2 bottomRight, uint color, float thickness, bool outline)
        {
            float lineW = (bottomRight.X - topLeft.X) / 4.0f;
            float lineH = (bottomRight.Y - topLeft.Y) / 4.0f;
            if (outline) DrawCornerSegments(drawList, topLeft, bottomRight, config.outlineColor, thickness + 1.2f, lineW, lineH);
            DrawCornerSegments(drawList, topLeft, bottomRight, color, thickness, lineW, lineH);
        }

        private void DrawCornerSegments(ImDrawListPtr drawList, Vector2 topLeft, Vector2 bottomRight, uint color, float thickness, float lineW, float lineH)
        {
            drawList.AddLine(topLeft, new Vector2(topLeft.X + lineW, topLeft.Y), color, thickness);
            drawList.AddLine(topLeft, new Vector2(topLeft.X, topLeft.Y + lineH), color, thickness);
            drawList.AddLine(new Vector2(bottomRight.X, topLeft.Y), new Vector2(bottomRight.X - lineW, topLeft.Y), color, thickness);
            drawList.AddLine(new Vector2(bottomRight.X, topLeft.Y), new Vector2(bottomRight.X, topLeft.Y + lineH), color, thickness);
            drawList.AddLine(new Vector2(topLeft.X, bottomRight.Y), new Vector2(topLeft.X + lineW, bottomRight.Y), color, thickness);
            drawList.AddLine(new Vector2(topLeft.X, bottomRight.Y), new Vector2(topLeft.X, bottomRight.Y - lineH), color, thickness);
            drawList.AddLine(bottomRight, new Vector2(bottomRight.X - lineW, bottomRight.Y), color, thickness);
            drawList.AddLine(bottomRight, new Vector2(bottomRight.X, bottomRight.Y - lineH), color, thickness);
        }

        private void DrawSkeleton(ImDrawListPtr drawList, Entity entity)
        {
            uint col = ImGui.ColorConvertFloat4ToU32(config.skeletonColor);
            float thick = config.skeletonThickness;
            Vector2 GetB(int id) => entity.bone2D.ContainsKey(id) ? entity.bone2D[id] : Vector2.Zero;
            void L(Vector2 a, Vector2 b)
            {
                if (a.X < 1 || b.X < 1) return;
                if (config.skeletonOutline) drawList.AddLine(a, b, config.outlineColor, thick + 1.2f);
                drawList.AddLine(a, b, col, thick);
            }
            Vector2 head = GetB(6), neck = GetB(5), pelvis = GetB(0);
            Vector2 l_sh = GetB(8), l_eb = GetB(10), l_hn = GetB(11);
            Vector2 r_sh = GetB(13), r_eb = GetB(15), r_hn = GetB(16);
            Vector2 l_hp = GetB(22), l_kn = GetB(23), l_ft = GetB(24);
            Vector2 r_hp = GetB(25), r_kn = GetB(26), r_ft = GetB(27);
            L(head, neck); L(neck, pelvis);
            L(neck, l_sh); L(l_sh, l_eb); L(l_eb, l_hn);
            L(neck, r_sh); L(r_sh, r_eb); L(r_eb, r_hn);
            L(pelvis, l_hp); L(l_hp, l_kn); L(l_kn, l_ft);
            L(pelvis, r_hp); L(r_hp, r_kn); L(r_kn, r_ft);
        }
    }
}

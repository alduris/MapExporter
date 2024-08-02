using System;
using System.Collections.Generic;
using MapExporter.Generation;
using Menu.Remix.MixedUI;
using UnityEngine;
using static MapExporter.RegionInfo;

namespace MapExporter.Tabs.UI
{
    internal class OpMapBox : OpScrollBox
    {
        public RegionInfo activeRegion = null;
        public string activeRoom = null;

        private OpImage mapOpImage = null;
        private Texture2D texture = null;
        private int textureWidth;
        private int textureHeight;

        private bool mapDirty = false;
        public Vector2 viewOffset = Vector2.zero;
        private readonly LabelBorrower labelBorrower;
        private Vector2? lastMousePos;
        private bool hasPicked = false;

        private static readonly Color FOCUS_COLOR = new(0.5f, 1f, 1f);
        private static readonly Color CONNECTION_COLOR = new(0.75f, 0.75f, 0.75f);
        private static readonly Color CAMERA_COLOR = new(1f, 1f, 0.5f);
        private static readonly Color OVERLAP_COLOR = new(1f, 0.3f, 0.3f);
        private const float CROSSHAIR_SIZE = 6f;
        private bool checkForOverlap = false;

        private Player.InputPackage ctlr;
        private Player.InputPackage lastCtlr;

        public OpMapBox(OpTab tab, float contentSize, bool horizontal = false, bool hasSlideBar = true) : base(tab, 0, horizontal, hasSlideBar)
        {
            throw new NotImplementedException(); // nope, not for you :3
        }

        public OpMapBox(Vector2 pos, Vector2 size) : base(pos, size, size.y, false, true, false)
        {
            labelBorrower = new(this);
            description = "Left click + drag to move, right click to pick room (or use list), right click picked room to toggle hidden";
        }

        public void Initialize()
        {
            if (texture != null) return;
            texture = new Texture2D(Mathf.CeilToInt(size.x), Mathf.CeilToInt(size.y), TextureFormat.ARGB32, false);
            textureWidth = texture.width; // store these in variable to reduce method call (width and height are properties)
            textureHeight = texture.height;
            mapOpImage = new OpImage(new(0, 0), texture)
            {
                color = new Color(0f, 0f, 0f, 0f)
            };
            AddItems(mapOpImage);
            ClearCanvas();
        }

        public override void Update()
        {
            base.Update();
            checkForOverlap = Preferences.EditorCheckOverlap.GetValue();

            if (activeRegion != null)
            {
                // Control
                if (MenuMouseMode)
                {
                    if (MouseOver)
                    {
                        if (Input.GetMouseButton(0))
                        {
                            // Try to move map
                            if (lastMousePos != null)
                            {
                                var scroll = lastMousePos.Value - (Vector2)Futile.mousePosition;
                                Move(scroll);
                                UpdateMap();
                            }
                            lastMousePos = (Vector2)Futile.mousePosition;
                        }
                        else if (Input.GetMouseButton(1))
                        {
                            if (!hasPicked)
                            {
                                hasPicked = true;

                                // See if we are focused on a room
                                var clickPos = viewOffset - (size / 2) + MousePos;
                                RoomEntry roomEntry = null;
                                foreach (var room in activeRegion.rooms.Values)
                                {
                                    if (new Rect(room.devPos, room.size.ToVector2()).Contains(clickPos))
                                    {
                                        roomEntry = room;
                                        break;
                                    }
                                }

                                if (roomEntry != null && activeRoom == roomEntry.roomName)
                                {
                                    roomEntry.hidden = !roomEntry.hidden;
                                    UpdateMap();
                                }
                                FocusRoom(roomEntry?.roomName);
                                (tab as EditTab)._SwitchActiveButton(roomEntry?.roomName);
                            }
                        }
                        else
                        {
                            lastMousePos = null;
                            hasPicked = false;
                        }
                    }
                    else
                    {
                        lastMousePos = null;
                        hasPicked = false;
                    }
                }
                else if (held)
                {
                    // Get player input manually because CtlrInput won't record other button presses
                    lastCtlr = ctlr;
                    ctlr = RWInput.PlayerInput(0);

                    var dir = new Vector2(ctlr.x, ctlr.y) * (ctlr.pckp ? 4f : 1f);
                    Move(dir);
                    bool redraw = dir.magnitude > 0.1f;

                    if (ctlr.jmp && !lastCtlr.jmp)
                    {
                        if (activeRoom == null)
                        {
                            RoomEntry roomEntry = null;
                            foreach (var room in activeRegion.rooms.Values)
                            {
                                if (new Rect(room.devPos, room.size.ToVector2()).Contains(viewOffset))
                                {
                                    roomEntry = room;
                                    break;
                                }
                            }

                            if (roomEntry != null)
                            {
                                FocusRoom(roomEntry.roomName);
                                (tab as EditTab)._SwitchActiveButton(roomEntry.roomName);
                                redraw = true;
                            }
                            else
                            {
                                PlaySound(SoundID.MENU_Error_Ping);
                            }
                        }
                        else
                        {
                            FocusRoom(null);
                            (tab as EditTab)._SwitchActiveButton(null);
                            redraw = true;
                        }
                    }

                    if (ctlr.mp && !lastCtlr.mp && activeRoom != null)
                    {
                        var room = activeRegion.rooms[activeRoom];
                        room.hidden = !room.hidden;
                        redraw = true;
                    }

                    if (redraw)
                        UpdateMap();
                }
            }

            // Redraw
            if (texture != null && mapDirty)
            {
                mapDirty = false;
                Draw();
            }
        }

        private static readonly Vector2 camSize = new(70f, 40f); // Cameras are 1400x800, tiles are 20x20. Using this, we can determine cameras here should be 70x40 pixels.
        private void Draw()
        {
            ClearCanvas();
            Color[] pixels = texture.GetPixels();
            labelBorrower.Update();

            // Draw nothing if there is no active region
            if (activeRegion == null)
            {
                return;
            }

            // Figure out our draw area
            Vector2 drawPosition = viewOffset;
            if (activeRoom != null)
            {
                var room = activeRegion.rooms[activeRoom];
                drawPosition = room.devPos + room.size.ToVector2() / 2;
                viewOffset = drawPosition;
            }

            Rect drawArea = new(drawPosition - size / 2, size);
            Vector2 drawBL = drawPosition - size / 2;

            // Figure out what rooms and connections lie within our draw area
            List<RoomEntry> showRooms = [];
            List<ConnectionEntry> showConns = [];

            foreach (var room in activeRegion.rooms.Values)
            {
                if (drawArea.CheckIntersect(new Rect(room.devPos, new Vector2(room.size.x, room.size.y))))
                {
                    showRooms.Add(room);
                }
            }
            foreach (var conn in activeRegion.connections)
            {
                if (!activeRegion.rooms.ContainsKey(conn.roomA) || !activeRegion.rooms.ContainsKey(conn.roomB))
                    continue;

                Vector2 A = activeRegion.rooms[conn.roomA].devPos + conn.posA.ToVector2();
                Vector2 B = activeRegion.rooms[conn.roomB].devPos + conn.posB.ToVector2();
                if (LineCloseEnough(drawArea, A, B))
                {
                    showConns.Add(conn);
                }
            }

            // Draw rooms
            foreach (var room in showRooms)
            {
                int startX = Mathf.RoundToInt(room.devPos.x);
                int startY = Mathf.RoundToInt(room.devPos.y);

                // Draw the pixels of the room geometry
                int sizeX = room.tiles != null ? room.size.x : GenUtil.offscreenSizeInt.x / 20;
                int sizeY = room.tiles != null ? room.size.y : GenUtil.offscreenSizeInt.y / 20;
                for (int i = 0; i < sizeX; i++)
                {
                    if (startX + i < drawArea.xMin || startX + i > drawArea.xMax)
                        continue;

                    for (int j = 0; j < sizeY; j++)
                    {
                        if (startY + j < drawArea.yMin || startY + j > drawArea.yMax)
                            continue;

                        var p = new Vector2(startX + i - drawBL.x, startY + j - drawBL.y) + Vector2.one * 0.0001f;
                        var color = GetTileColor(room.tiles?[i, j]);
                        if (room.hidden) color = Color.Lerp(color, Color.black, 0.5f);
                        SetPixel(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), color, pixels);
                    }
                }

                // Draw camera outlines
                if (room.cameras != null && !room.hidden)
                {
                    if (Preferences.EditorShowCameras.GetValue())
                    {
                        // Draw individual cameras
                        foreach (var cam in room.cameras)
                        {
                            // Check for overlap if needed
                            bool overlap = false;
                            if (checkForOverlap)
                            {
                                Rect thisRoom = new(room.devPos + cam / 20f, camSize);
                                foreach (var other in showRooms)
                                {
                                    if (other == room || other.cameras == null) continue;
                                    foreach (var ocam in other.cameras)
                                    {
                                        if (thisRoom.CheckIntersect(new Rect(other.devPos + ocam / 20f, camSize)))
                                        {
                                            overlap = true;
                                            break;
                                        }
                                    }
                                    if (overlap) break;
                                }
                            }
                        
                            // Draw
                            DrawRectOutline(room.devPos + cam / 20f - drawBL, camSize, overlap ? OVERLAP_COLOR : CAMERA_COLOR, pixels, 1);
                        }
                    }
                    else
                    {
                        // Draw a whole rectangle for all room camera boundaries

                        bool overlap = false;
                        Vector2 start = room.cameras[0] / 20f;
                        Vector2 end = room.cameras[0] / 20f + camSize;
                        foreach (var cam in room.cameras)
                        {
                            // Get bounds
                            start = Vector2.Min(start, cam / 20f);
                            end = Vector2.Max(end, cam / 20f + camSize);

                            // Check for overlap if needed
                            if (checkForOverlap)
                            {
                                Rect thisRoom = new(room.devPos + cam / 20f, camSize);
                                foreach (var other in showRooms)
                                {
                                    if (other.hidden) continue;
                                    if (other == room || other.cameras == null) continue;
                                    foreach (var ocam in other.cameras)
                                    {
                                        if (thisRoom.CheckIntersect(new Rect(other.devPos + ocam / 20f, camSize)))
                                        {
                                            overlap = true;
                                            break;
                                        }
                                    }
                                    if (overlap) break;
                                }
                            }
                        }

                        // Draw
                        DrawRectOutline(room.devPos + start - drawBL, end - start, overlap ? OVERLAP_COLOR : CAMERA_COLOR, pixels, 1);
                    }
                }

                // Give it a border if it is the focused room
                if (activeRoom != null && room.roomName == activeRoom)
                {
                    DrawRectOutline(new Vector2(startX - 1, startY - 1) - drawBL, new Vector2(room.size.x + 2, room.size.y + 2), FOCUS_COLOR, pixels, 2);
                }

                // Give it an OpLabel name
                labelBorrower.AddLabel(room.roomName == activeRoom ? "> " + room.roomName : room.roomName, new Vector2(startX, startY + room.size.y) - drawBL);
            }

            // Draw connections
            foreach (var conn in showConns)
            {
                Vector2 A = activeRegion.rooms[conn.roomA].devPos + conn.posA.ToVector2() - drawBL;
                Vector2 B = activeRegion.rooms[conn.roomB].devPos + conn.posB.ToVector2() - drawBL;

                // DrawLine(A, B, CONNECTION_COLOR, pixels, 1);
                float dist = (B - A).magnitude / 4f;
                Vector2 basicDir = (B - A).normalized;
                Vector2 A1 = A + (conn.dirA == -1 ? basicDir : GenUtil.fourDirections[conn.dirA]) * dist;
                Vector2 B1 = B + (conn.dirB == -1 ? -basicDir : GenUtil.fourDirections[conn.dirB]) * dist;
                DrawCubic(A, A1, B1, B, CONNECTION_COLOR, pixels, 1);
            }

            // Draw cursor if needed
            if (!MenuMouseMode && held)
            {
                DrawLine(size / 2 + Vector2.down * CROSSHAIR_SIZE, size / 2 + Vector2.up    * CROSSHAIR_SIZE, colorEdge, pixels, 1);
                DrawLine(size / 2 + Vector2.left * CROSSHAIR_SIZE, size / 2 + Vector2.right * CROSSHAIR_SIZE, colorEdge, pixels, 1);
            }

            // Apply texture so it actually shows lol
            texture.SetPixels(pixels);
            UpdateTexture();
        }

        public void LoadRegion(RegionInfo region)
        {
            activeRegion = region;
            activeRoom = null;
            mapDirty = true;
            viewOffset = Vector2.zero;
        }

        public void UnloadRegion()
        {
            if (activeRegion != null)
            {
                activeRegion = null;
                activeRoom = null;
                mapDirty = true;
                viewOffset = Vector2.zero;
            }
        }

        public void FocusRoom(string roomName)
        {
            if (roomName == activeRoom || (roomName != null && !activeRegion.rooms.ContainsKey(roomName)))
            {
                PlaySound(SoundID.MENU_Error_Ping);
                return;
            }

            activeRoom = roomName;
            mapDirty = true;
        }

        public void UpdateMap()
        {
            mapDirty = true;
            // Draw();
        }

        public void Move(Vector2 dir)
        {
            if (activeRoom != null)
            {
                activeRegion.rooms[activeRoom].devPos += dir;
            }
            else if (activeRegion != null)
            {
                viewOffset += dir;
            }
        }

        private void ClearCanvas()
        {
            var fillColor = new Color(0f, 0f, 0f, 0f);
            var fillColorArray = texture.GetPixels();

            for (var i = 0; i < fillColorArray.Length; ++i)
            {
                fillColorArray[i] = fillColor;
            }
            texture.SetPixels(fillColorArray);

            UpdateTexture();
        }

        private void UpdateTexture()
        {
            texture.filterMode = FilterMode.Point;
            texture.Apply();
            (mapOpImage.sprite as FTexture).SetTexture(texture);
        }

        private void SetPixel(int x, int y, Color color, Color[] pixels)
        {
            if (x < 0 || x >= textureWidth || y < 0 || y >= textureHeight) return;
            int i = x + y * textureWidth;
            if (i < 0 || i >= pixels.Length) return;
            pixels[i] = color;
        }

        private void DrawRectOutline(Vector2 pos, Vector2 size, Color color, Color[] pixels, int width = 1)
        {
            pos = new Vector2(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y));
            size = new Vector2(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y));
            width--;
            // Top
            if (pos.y >= 0)
            {
                for (int i = -width; i < size.x + width; i++)
                {
                    int x = Mathf.RoundToInt(pos.x + i), y = Mathf.RoundToInt(pos.y);
                    if (x < 0 || x >= textureWidth) continue;
                    SetPixel(x, y, color, pixels);

                    for (int j = 1; j <= width; j++)
                    {
                        SetPixel(x, y - j, color, pixels);
                    }
                }
            }
            // Bottom
            if (pos.y + size.y - 1 < textureHeight)
            {
                for (int i = -width; i < size.x + width; i++)
                {
                    int x = Mathf.RoundToInt(pos.x + i), y = Mathf.RoundToInt(pos.y + size.y - 1);
                    if (x < 0 || x >= textureWidth) continue;
                    SetPixel(x, y, color, pixels);

                    for (int j = 1; j <= width; j++)
                    {
                        SetPixel(x, y + j, color, pixels);
                    }
                }
            }
            // Left
            if (pos.x >= 0)
            {
                for (int j = -width; j < size.y + width; j++)
                {
                    int x = Mathf.RoundToInt(pos.x), y = Mathf.RoundToInt(pos.y + j);
                    if (y < 0 || y >= textureHeight) continue;
                    SetPixel(x, y, color, pixels);

                    for (int i = 1; i <= width; i++)
                    {
                        SetPixel(x - i, y, color, pixels);
                    }
                }
            }
            // Right
            if (pos.x + size.x - 1 < textureWidth)
            {
                for (int j = -width; j < size.y + width; j++)
                {
                    int x = Mathf.RoundToInt(pos.x + size.x - 1), y = Mathf.RoundToInt(pos.y + j);
                    if (y < 0 || y >= textureHeight) continue;
                    SetPixel(x, y, color, pixels);

                    for (int i = 1; i <= width; i++)
                    {
                        SetPixel(x + i, y, color, pixels);
                    }
                }
            }
        }

        private void DrawLine(Vector2 A, Vector2 B, Color color, Color[] pixels, int width = 1)
        {
            float dy = B.y - A.y;
            float dx = B.x - A.x;

            if (Mathf.Abs(dx) >= Mathf.Abs(dy))
            {
                if (A.x > B.x)
                {
                    (B, A) = (A, B);
                    dy = B.y - A.y;
                    dx = B.x - A.x;
                }
                float m = dy / dx;

                for (int x = Mathf.RoundToInt(A.x); x < Mathf.RoundToInt(B.x); x++)
                {
                    if (x < 0 || x >= textureWidth) continue;
                    int y = Mathf.RoundToInt(A.y + m * (x - A.x));
                    if (y < 0 || y >= textureHeight) continue;
                    SetPixel(x, y, color, pixels);
                    if (width > 1)
                    {
                        // Yes there are two for-loops. Note that one is ceil and one is floor, they cannot be combined.
                        for (int i = 1; i <= Mathf.CeilToInt(width / 2f); i++)
                        {
                            SetPixel(x, y + i, color, pixels);
                        }
                        for (int i = 1; i <= Mathf.FloorToInt(width / 2f); i++)
                        {
                            SetPixel(x, y - i, color, pixels);
                        }
                    }
                }
            }
            else
            {
                if (A.y > B.y)
                {
                    (B, A) = (A, B);
                    dy = B.y - A.y;
                    dx = B.x - A.x;
                }
                float m = dx / dy;

                for (int y = Mathf.RoundToInt(A.y); y < Mathf.RoundToInt(B.y); y++)
                {
                    if (y < 0 || y >= textureHeight) continue;
                    int x = Mathf.RoundToInt(A.x + m * (y - A.y));
                    if (x < 0 || x >= textureWidth) continue;
                    SetPixel(x, y, color, pixels);
                    if (width > 1)
                    {
                        for (int i = 1; i <= Mathf.CeilToInt(width / 2f); i++)
                        {
                            SetPixel(x + i, y, color, pixels);
                        }
                        for (int i = 1; i <= Mathf.FloorToInt(width / 2f); i++)
                        {
                            SetPixel(x - i, y, color, pixels);
                        }
                    }
                }
            }
        }

        private void DrawCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color color, Color[] pixels, int width = 1, int samples = -1)
        {
            // Figure out some number of samples thing
            if (samples <= 1)
            {
                samples = (int)(Vector2.Distance(p0, p1) + Vector2.Distance(p1, p2) + Vector2.Distance(p2, p3)) / (1 + 2 * width);
            }

            // Take samples and draw lines
            Vector2 lastPoint = p0;
            for (int i = 0; i < samples; i++)
            {
                float t = (i + 1f) / samples;

                // Calculate the bezier point
                float u = 1 - t;
                float tt = t * t;
                float uu = u * u;
                float uuu = uu * u;
                float ttt = tt * t;

                Vector2 point = uuu * p0; // (1-t)^3 * P0
                point += 3 * uu * t * p1; // 3(1-t)^2 * t * P1
                point += 3 * u * tt * p2; // 3(1-t) * t^2 * P2
                point += ttt * p3; // t^3 * P3

                // Draw the line
                DrawLine(lastPoint, point, color, pixels, width);
                lastPoint = point;
            }
        }

        private bool LineCloseEnough(Rect rect, Vector2 A, Vector2 B)
        {
            if (rect.Contains(A) || rect.Contains(B)) return true;

            return
                !(A.x < rect.xMin && B.x < rect.xMin) &&
                !(A.x > rect.xMax && B.x > rect.xMax) &&
                !(A.y < rect.yMin && B.y < rect.yMin) &&
                !(A.y > rect.yMax && B.y > rect.yMax);
        }

        private static Color GetTileColor(int[] tile)
        {
            if (tile == null) return new Color(0.3f, 0.3f, 0.3f);

            var terrain = (Room.Tile.TerrainType)tile[0];
            if (terrain == Room.Tile.TerrainType.ShortcutEntrance)
            {
                return tile[2] switch
                {
                    1 => new Color(1f, 1f, 1f),      // Normal
                    2 => new Color(0f, 1f, 0.2f),    // Room exit
                    3 => new Color(1f, 0f, 1f),      // Creature hole
                    4 => new Color(0.7f, 0f, 0f),    // Whack-a-mole (ShortcutData.Type.NPCTransportation)
                    5 => new Color(0f, 0f, 0f),      // Region transportation
                    _ => new Color(0.3f, 0.3f, 0.3f) // Dead end or unknown (same as solid color)
                };
            }
            if (terrain == Room.Tile.TerrainType.Floor || terrain == Room.Tile.TerrainType.Slope || tile[1] > 0)
            {
                return new Color(0.5f, 0.3f, 0.3f);
            }
            return terrain == Room.Tile.TerrainType.Solid ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.6f, 0.6f, 0.6f);
            // Wall behind uses new Color(0.5f, 0.5f, 0.5f) but we don't track that
        }
    }
}

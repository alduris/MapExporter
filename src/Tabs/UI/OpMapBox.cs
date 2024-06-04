using System;
using System.Collections.Generic;
using Menu.Remix.MixedUI;
using RWCustom;
using UnityEngine;

namespace MapExporter.Tabs.UI
{
    internal class OpMapBox : OpScrollBox
    {
        public RegionInfo activeRegion = null;
        public string activeRoom = null;

        private OpImage mapOpImage = null;
        private Texture2D texture = null;

        private bool mapDirty = false;
        public Vector2 viewOffset = Vector2.zero;
        private readonly LabelBorrower labelBorrower;
        private Vector2? lastMousePos;
        private Vector2 accumulatedScrollAmount = Vector2.zero;

        private static readonly Color FOCUS_COLOR = new(0.5f, 1f, 1f);
        private static readonly Color CONNECTION_COLOR = new(0.75f, 0.75f, 0.75f);
        private static readonly Color CAMERA_COLOR = new(1f, 1f, 0.5f);
        private const float CROSSHAIR_SIZE = 6f;

        public OpMapBox(OpTab tab, float contentSize, bool horizontal = false, bool hasSlideBar = true) : base(tab, 0, horizontal, hasSlideBar)
        {
            throw new NotImplementedException(); // nope, not for you :3
        }

        public OpMapBox(Vector2 pos, Vector2 size) : base(pos, size, size.y, false, true, false)
        {
            labelBorrower = new(this);
        }

        public void Initialize()
        {
            if (texture != null) return;
            texture = new Texture2D(Mathf.CeilToInt(size.x), Mathf.CeilToInt(size.y), TextureFormat.ARGB32, false);
            mapOpImage = new OpImage(new(0, 0), texture)
            {
                color = new Color(0f, 0f, 0f, 0f),
                //scale = new Vector2(2f, 2f)
            };
            AddItems(mapOpImage);
            ClearCanvas();
        }

        public override void Update()
        {
            base.Update();

            if (activeRegion != null)
            {
                // Control
                if (MenuMouseMode)
                {
                    if (MouseOver)
                    {
                        if (Input.GetMouseButton(0) && lastMousePos != null)
                        {
                            var scroll = lastMousePos.Value - MousePos;
                            Move(scroll);
                            UpdateMap();
                            accumulatedScrollAmount += scroll;
                        }
                        else if (Input.GetMouseButtonUp(0) && accumulatedScrollAmount.magnitude < 10f)
                        {
                            // See if we are focused on a room
                            var clickPos = viewOffset - (size / 2) + MousePos;
                            RegionInfo.RoomEntry roomEntry = null;
                            foreach (var room in activeRegion.rooms.Values)
                            {
                                if (new Rect(room.devPos, room.size.ToVector2()).Contains(clickPos))
                                {
                                    roomEntry = room;
                                    break;
                                }
                            }
                            FocusRoom(roomEntry?.roomName);
                        }
                        else
                        {
                            accumulatedScrollAmount = Vector2.zero;
                        }
                        lastMousePos = MousePos;
                    }
                    else
                    {
                        lastMousePos = null;
                        accumulatedScrollAmount = Vector2.zero;
                    }
                }
                else if (held)
                {
                    //
                }
            }

            // Redraw
            if (texture != null && mapDirty)
            {
                mapDirty = false;
                Draw();
            }
        }

        private void Draw()
        {
            ClearCanvas();
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
            List<RegionInfo.RoomEntry> showRooms = [];
            List<RegionInfo.ConnectionEntry> showConns = [];

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
                if (drawArea.Contains(A) || drawArea.Contains(B) || LineIntersectsRect(drawArea, A, B))
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
                for (int i = 0; i < room.size.x; i++)
                {
                    if (startX + i < drawArea.xMin || startX + i > drawArea.xMax)
                        continue;

                    for (int j = 0; j < room.size.y; j++)
                    {
                        if (startY + j < drawArea.yMin || startY + j > drawArea.yMax)
                            continue;

                        var p = new Vector2(startX + i - drawBL.x, startY + j - drawBL.y) + Vector2.one * 0.0001f;
                        texture.SetPixel(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), GetTileColor(room.tiles?[i, j]));
                    }
                }

                // Draw camera outlines
                if (room.cameras != null)
                {
                    foreach (var cam in room.cameras)
                    {
                        // Cameras are 1400x800, tiles are 20x20. Using this, we can determine cameras here should be 70x40 pixels.
                        DrawRectOutline(new Vector2(startX, startY) + cam / 20f - drawBL, new Vector2(70f, 40f), CAMERA_COLOR, 1);
                    }
                }

                // Give it a border if it is the focused room
                if (activeRoom != null && room.roomName == activeRoom)
                {
                    DrawRectOutline(new Vector2(startX - 1, startY - 1) - drawBL, new Vector2(room.size.x + 2, room.size.y + 2), FOCUS_COLOR, 2);
                }

                // Give it an OpLabel name
                labelBorrower.AddLabel(room.roomName == activeRoom ? "> " + room.roomName : room.roomName, new Vector2(startX, startY + room.size.y) - drawBL);
            }

            // Draw connections
            foreach (var conn in showConns)
            {
                Vector2 A = activeRegion.rooms[conn.roomA].devPos + conn.posA.ToVector2() - drawBL;
                Vector2 B = activeRegion.rooms[conn.roomB].devPos + conn.posB.ToVector2() - drawBL;

                DrawLine(A, B, CONNECTION_COLOR, 1);
            }

            // Draw cursor if needed
            if (!MenuMouseMode && held)
            {
                DrawLine(size / 2 + Vector2.down * CROSSHAIR_SIZE, size / 2 + Vector2.up    * CROSSHAIR_SIZE, colorEdge, 2);
                DrawLine(size / 2 + Vector2.left * CROSSHAIR_SIZE, size / 2 + Vector2.right * CROSSHAIR_SIZE, colorEdge, 2);
            }

            // Apply texture so it actually shows lol
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

        private void DrawRectOutline(Vector2 pos, Vector2 size, Color color, int width = 1)
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
                    if (x < 0 || x >= texture.width) continue;
                    texture.SetPixel(x, y, color);

                    for (int j = 1; j <= width; j++)
                    {
                        texture.SetPixel(x, y - j, color);
                    }
                }
            }
            // Bottom
            if (pos.y + size.y - 1 < texture.height)
            {
                for (int i = -width; i < size.x + width; i++)
                {
                    int x = Mathf.RoundToInt(pos.x + i), y = Mathf.RoundToInt(pos.y + size.y - 1);
                    if (x < 0 || x >= texture.width) continue;
                    texture.SetPixel(x, y, color);

                    for (int j = 1; j <= width; j++)
                    {
                        texture.SetPixel(x, y + j, color);
                    }
                }
            }
            // Left
            if (pos.x >= 0)
            {
                for (int j = -width; j < size.y + width; j++)
                {
                    int x = Mathf.RoundToInt(pos.x), y = Mathf.RoundToInt(pos.y + j);
                    if (y < 0 || y >= texture.height) continue;
                    texture.SetPixel(x, y, color);

                    for (int i = 1; i <= width; i++)
                    {
                        texture.SetPixel(x - i, y, color);
                    }
                }
            }
            // Right
            if (pos.x + size.x - 1 < texture.width)
            {
                for (int j = -width; j < size.y + width; j++)
                {
                    int x = Mathf.RoundToInt(pos.x + size.x - 1), y = Mathf.RoundToInt(pos.y + j);
                    if (y < 0 || y >= texture.height) continue;
                    texture.SetPixel(x, y, color);

                    for (int i = 1; i <= width; i++)
                    {
                        texture.SetPixel(x + i, y, color);
                    }
                }
            }
        }

        private void DrawLine(Vector2 A, Vector2 B, Color color, int width = 1)
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
                    if (x < 0 || x >= texture.width) continue;
                    int y = Mathf.RoundToInt(A.y + m * (x - A.x));
                    if (y < 0 || y >= texture.height) continue;
                    texture.SetPixel(x, y, color);
                    if (width > 1)
                    {
                        for (int i = 1; i <= Mathf.CeilToInt(width / 2f); i++)
                        {
                            texture.SetPixel(x, y + i, color);
                        }
                        for (int i = 1; i <= Mathf.FloorToInt(width / 2f); i++)
                        {
                            texture.SetPixel(x, y - i, color);
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
                    if (y < 0 || y >= texture.height) continue;
                    int x = Mathf.RoundToInt(A.x + m * (y - A.y));
                    if (x < 0 || x >= texture.width) continue;
                    texture.SetPixel(x, y, color);
                    if (width > 1)
                    {
                        for (int i = 1; i <= Mathf.CeilToInt(width / 2f); i++)
                        {
                            texture.SetPixel(x + i, y, color);
                        }
                        for (int i = 1; i <= Mathf.FloorToInt(width / 2f); i++)
                        {
                            texture.SetPixel(x - i, y, color);
                        }
                    }
                }
            }
        }

        private static bool LineIntersectsRect(Rect self, Vector2 A, Vector2 B)
        {
            // This is perhaps the worst possible way to do it but I hate linear algebra lol so homebrew it is
            if (A.x == B.x) return A.x > self.xMin && A.x < self.xMax;
            if (A.y == B.y) return A.y > self.yMin && A.y < self.yMax;

            float m = (A.y + B.y) / (A.x + B.x);
            float b = A.y - m * A.x;

            float yl = m * self.xMin + b;
            float yu = m * self.xMax + b;
            float xl = (self.yMin - b) / m;
            float xu = (self.yMax - b) / m;

            return
                (xl > self.xMin && xl < self.xMax && In01(InvLerpUnclamped(A.x, B.x, xl))) ||
                (xu > self.xMin && xu < self.xMax && In01(InvLerpUnclamped(A.x, B.x, xu))) ||
                (yl > self.yMin && yl < self.yMax && In01(InvLerpUnclamped(A.y, B.y, yl))) ||
                (yu > self.yMin && yu < self.yMax && In01(InvLerpUnclamped(A.y, B.y, yu)));
        }

        private static float InvLerpUnclamped(float a, float b, float x) => (x - a) / (b - a);
        private static bool In01(float val) => val >= 0 && val <= 1;

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

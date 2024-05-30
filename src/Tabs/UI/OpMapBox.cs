using System;
using System.Collections.Generic;
using Menu.Remix.MixedUI;
using RWCustom;
using UnityEngine;

namespace MapExporter.Tabs.UI
{
    internal class OpMapBox : OpScrollBox
    {
        private RegionInfo activeRegion = null;
        private OpImage opImage = null;
        private Texture2D texture = null;
        private bool mapDirty = false;
        private string focusRoom = null;

        public OpMapBox(OpTab tab, float contentSize, bool horizontal = false, bool hasSlideBar = true) : base(tab, contentSize, horizontal, hasSlideBar)
        {
            throw new NotImplementedException(); // nope, not for you :3
        }

        public OpMapBox(Vector2 pos, Vector2 size) : base(pos, size, size.y, false, true, false)
        {
        }

        public void Initialize()
        {
            texture = new Texture2D(Mathf.CeilToInt(size.x), Mathf.CeilToInt(size.y));
            opImage = new OpImage(new(0, 0), texture)
            {
                color = new Color(0f, 0f, 0f, 0f)
            };
            AddItems(opImage);
            ClearCanvas();
        }

        public override void Update()
        {
            base.Update();
            if (texture != null && mapDirty)
            {
                mapDirty = false;
                Plugin.Logger.LogDebug("Drawing!");
                ClearCanvas();

                // Draw nothing if there is no active region
                if (activeRegion == null)
                {
                    return;
                }

                // Figure out our draw area
                Vector2 drawPosition = new(0, 0);
                if (focusRoom != null)
                {
                    var room = activeRegion.rooms[focusRoom];
                    drawPosition = room.devPos + new Vector2(room.size[0], room.size[1]) / 2;
                }

                Rect drawArea = new(drawPosition - size / 2, size);
                Vector2 drawBL = drawPosition - size / 2;

                // Figure out what rooms and connections lie within our draw area
                List<RegionInfo.RoomEntry> showRooms = [];
                List<RegionInfo.ConnectionEntry> showConns = [];

                foreach (var room in activeRegion.rooms.Values)
                {
                    if (room.size == null) continue;
                    if (drawArea.CheckIntersect(new Rect(room.devPos, new Vector2(room.size[0], room.size[1]))))
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
                    if (LineIntersectsRect(drawArea, A, B))
                    {
                        showConns.Add(conn);
                    }
                }

                // Draw rooms
                foreach (var room in showRooms)
                {
                    int startX = Mathf.RoundToInt(room.devPos.x);
                    int startY = Mathf.RoundToInt(room.devPos.y);

                    for (int i = 0; i < room.size[0]; i++)
                    {
                        if (startX + i < drawArea.xMin || startX + i > drawArea.xMax)
                            continue;

                        for (int j = 0; j < room.size[1]; j++)
                        {
                            if (startY + j < drawArea.yMin || startY + j > drawArea.yMax)
                                continue;

                            var p = new Vector2(startX + i - drawBL.x, startY + j - drawBL.y);
                            texture.SetPixel(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), GetTileColor(room.tiles[i, j]));
                        }
                    }
                }

                // Draw connections (TODO:)

                // Apply texture so it actually shows lol
                UpdateTexture();
            }
        }

        public void LoadRegion(RegionInfo region)
        {
            activeRegion = region;
            focusRoom = null;
            mapDirty = true;
        }

        public void UnloadRegion()
        {
            if (activeRegion != null)
            {
                activeRegion = null;
                focusRoom = null;
                mapDirty = true;
            }
        }

        public void FocusRoom(string roomName)
        {
            if (roomName == focusRoom || !activeRegion.rooms.ContainsKey(roomName))
            {
                PlaySound(SoundID.MENU_Error_Ping);
                return;
            }

            focusRoom = roomName;
            mapDirty = true;
        }

        private void ClearCanvas()
        {
            texture.Resize(1, 1);
            texture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0f));
            texture.Resize(Mathf.CeilToInt(size.x), Mathf.CeilToInt(size.y));
            UpdateTexture();
        }

        private void UpdateTexture()
        {
            texture.Apply();
            (opImage.sprite as FTexture).SetTexture(texture);
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
                (xl > self.xMin && xl < self.xMax) ||
                (xu > self.xMin && xu < self.xMax) ||
                (yl > self.yMin && yl < self.yMax) ||
                (yu > self.yMin && yu < self.yMax);
        }

        private static Color GetTileColor(int[] tile)
        {
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

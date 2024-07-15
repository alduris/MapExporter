using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MoreSlugcats;
using UnityEngine;

namespace MapExporter
{
    internal static class Resources
    {
        /// <summary>
        /// Gets the front-end location path thingy of something
        /// </summary>
        public static string FEPathTo(params string[] path) => path.Aggregate(Path.Combine(Data.ModDirectory, "map-frontend"), Path.Combine);
        public static string TilePathTo(params string[] path) => path.Aggregate(Data.FinalDir, Path.Combine);
        private static string CreatureIconPath(string item) => item == null ? FEPathTo("resources", "creatures") : FEPathTo("resources", "creatures", item + ".png");
        private static string SlugcatIconPath(string scug) => scug == null ? FEPathTo("resources", "slugcats") : FEPathTo("resources", "slugcats", scug + ".png");

        public static bool TryGetActualPath(string req, out string path)
        {
            if (req.Length > 0)
                req = req.Substring(1);

            if (req.Length == 0) req = "index.html";

            path = FEPathTo(req.Split('/'));
            if (!File.Exists(path))
            {
                path = null;
                return false;
            }
            return true;
        }

        public static bool TryGetTile(string req, out byte[] bytes)
        {
            bytes = null;
            if (!req.StartsWith("/slugcats/")) return false;

            string path = TilePathTo(req.Substring(10).Split('/')); // 10 = len("/slugcats/")
            if (!File.Exists(path)) return false;

            bytes = File.ReadAllBytes(path);
            return true;
        }

        public static bool TryGetJsonResource(string req, out byte[] res)
        {
            res = null;

            if (req == "/regions.json")
            {
                Dictionary<string, Dictionary<string, object>> finished = [];
                foreach (var kv in Data.FinishedRegions)
                {
                    finished.Add(kv.Key, new() {
                        {"slugcats", kv.Value.Select(x => x.value.ToLowerInvariant()).ToArray()},
                        {"name", Region.GetRegionFullName(kv.Key, null)},
                        {"specificNames", kv.Value.Select(x => Region.GetRegionFullName(kv.Key, x)).ToArray()}
                    });
                }
                res = Encoding.UTF8.GetBytes(Json.Serialize(finished));
                return true;
            }
            else if (req == "/slugcats.json")
            {
                HashSet<string> scugs = [];

                foreach (var list in Data.FinishedRegions.Values)
                {
                    foreach (var scug in list)
                    {
                        scugs.Add(scug.value.ToLowerInvariant());
                    }
                }

                res = Encoding.UTF8.GetBytes(Json.Serialize(scugs.ToList()));
                return true;
            }

            return false;
        }

        private static bool IsDefaultSlugcat(SlugcatStats.Name scug) =>
            scug != SlugcatStats.Name.White &&
            scug != SlugcatStats.Name.Yellow &&
            scug != SlugcatStats.Name.Red &&
            scug != SlugcatStats.Name.Night &&
            (
                !ModManager.MSC || (
                    scug != MoreSlugcatsEnums.SlugcatStatsName.Gourmand &&
                    scug != MoreSlugcatsEnums.SlugcatStatsName.Artificer &&
                    scug != MoreSlugcatsEnums.SlugcatStatsName.Rivulet &&
                    scug != MoreSlugcatsEnums.SlugcatStatsName.Spear &&
                    scug != MoreSlugcatsEnums.SlugcatStatsName.Saint &&
                    scug != MoreSlugcatsEnums.SlugcatStatsName.Sofanthiel &&
                    scug != MoreSlugcatsEnums.SlugcatStatsName.Slugpup
                )
            );
        public static void CopyFrontendFiles(bool replaceAll = false)
        {
            // Get slugcat icons
            foreach (var item in SlugcatStats.Name.values.entries)
            {
                var scug = new SlugcatStats.Name(item, false);
                if (SlugcatStats.HiddenOrUnplayableSlugcat(scug) && (!ModManager.MSC || item != MoreSlugcatsEnums.SlugcatStatsName.Sofanthiel.value)) continue;

                if (!File.Exists(SlugcatIconPath(item.ToLower())) || (replaceAll && !IsDefaultSlugcat(scug)))
                {
                    var color = PlayerGraphics.DefaultSlugcatColor(scug);
                    var tex = new Texture2D(1, 1);
                    tex.LoadImage(File.ReadAllBytes(Path.Combine(SlugcatIconPath("default"))));
                    var pixels = tex.GetPixels();
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        pixels[i] *= color;
                    }
                    tex.SetPixels(pixels);
                    tex.Apply();
                    File.WriteAllBytes(SlugcatIconPath(item.ToLower()), tex.EncodeToPNG());
                    UnityEngine.Object.Destroy(tex);
                }
            }

            // Get creature icons
            foreach (var item in CreatureTemplate.Type.values.entries)
            {
                if (!File.Exists(CreatureIconPath(item.ToLower())) || replaceAll)
                {
                    if (item == CreatureTemplate.Type.Centipede.value)
                    {
                        for (int i = 1; i <= 3; i++)
                        {
                            var iconData = new IconSymbol.IconSymbolData
                            {
                                critType = new CreatureTemplate.Type(item, false),
                                intData = i
                            };
                            var sprite = new FSprite(CreatureSymbol.SpriteNameOfCreature(iconData), true);
                            var color = CreatureSymbol.ColorOfCreature(iconData);
                            var tex = SpriteColor(sprite, color);
                            Iconify(tex);
                            tex.Apply();
                            string name = i switch { 1 => "smallcentipede", 2 => "centipede", 3 => "bigcentipede", _ => throw new NotImplementedException() };
                            File.WriteAllBytes(CreatureIconPath(name), tex.EncodeToPNG());
                            UnityEngine.Object.Destroy(tex);
                        }
                    }
                    else
                    {
                        var iconData = new IconSymbol.IconSymbolData {
                            critType = new CreatureTemplate.Type(item, false)
                        };
                        var spriteName = CreatureSymbol.SpriteNameOfCreature(iconData);
                        if (spriteName == "Futile_White") continue;
                        var sprite = new FSprite(spriteName, true);
                        var color = CreatureSymbol.ColorOfCreature(iconData);
                        var tex = SpriteColor(sprite, color);
                        Iconify(tex);
                        tex.Apply();
                        File.WriteAllBytes(CreatureIconPath(item.ToLower()), tex.EncodeToPNG());
                        UnityEngine.Object.Destroy(tex);
                    }
                }
            }
        }

        public static void Reset(ResetSeverity severity)
        {
            string path = severity switch
            {
                ResetSeverity.InputOnly => Data.RenderDir,
                ResetSeverity.OutputOnly => Data.FinalDir,
                ResetSeverity.Everything => Data.DataDirectory,
                _ => throw new NotImplementedException()
            };
            Directory.Delete(path, true);
            Data.GetData();
        }

        public enum ResetSeverity
        {
            InputOnly,
            OutputOnly,
            Everything
        }

        public static Texture2D GetSpriteFromAtlas(FSprite sprite)
        {
            Texture2D atlasTex = sprite.element.atlas.texture as Texture2D;
            if (sprite.element.atlas.texture.name != "")
            {
                var oldRT = RenderTexture.active;

                var rt = new RenderTexture(atlasTex.width, atlasTex.height, 32, RenderTextureFormat.ARGB32);
                Graphics.Blit(atlasTex, rt);
                RenderTexture.active = rt;
                atlasTex = new Texture2D(atlasTex.width, atlasTex.height, TextureFormat.ARGB32, false);
                atlasTex.ReadPixels(new Rect(0, 0, atlasTex.width, atlasTex.height), 0, 0);

                RenderTexture.active = oldRT;
            }

            // Get sprite pos and size
            var pos = sprite.element.uvRect.position * sprite.element.atlas.textureSize; // sprite.element.sourceRect says the sprite is at (0, 0), it is not
            var size = sprite.element.sourceRect.size;

            // Fix size issues
            if (pos.x + size.x > atlasTex.width) size = new Vector2(atlasTex.width - pos.x, size.y);
            if (pos.y + size.y > atlasTex.height) size = new Vector2(size.x, atlasTex.height - pos.y);

            // Get the texture
            var tex = new Texture2D((int)size.x, (int)size.y, atlasTex.format, 1, false);
            Graphics.CopyTexture(atlasTex, 0, 0, (int)pos.x, (int)pos.y, (int)size.x, (int)size.y, tex, 0, 0, 0, 0);
            return tex;
        }

        public static Texture2D SpriteColor(FSprite sprite, Color color)
        {
            Texture2D texture = GetSpriteFromAtlas(sprite);
            SpriteColor(texture, color);
            return texture;
        }
        public static void SpriteColor(Texture2D texture, Color color)
        {
            Color[] pixels = texture.GetPixels();

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] *= color;
            }

            texture.SetPixels(pixels);
        }

        public static void CenterTextureInRect(Texture2D texture, int width, int height)
        {
            // Get old pixels
            var pixels = texture.GetPixels();
            var (oldW, oldH) = (texture.width, texture.height);

            // Resize and clear from invalid color
            texture.Resize(width, height);
            Color[] clear = new Color[width * height];
            for (int i = 0; i < clear.Length; i++) clear[i] = new Color(0f, 0f, 0f, 0f);
            texture.SetPixels(clear);

            // Put old image back
            texture.SetPixels(width / 2 - oldW / 2, height / 2 - oldH / 2, oldW, oldH, pixels);
        }

        public static void Iconify(Texture2D texture)
        {
            CenterTextureInRect(texture, 50, 50);

            // Add outline
            Color[] pixels = texture.GetPixels();
            bool[] colored = pixels.Select(x => x.a == 1f).ToArray();

            for (int i = 0; i < 50; i++)
            {
                for (int j = 0; j < 50; j++)
                {
                    if (colored[i + j * 50]) continue; // don't overwrite

                    // Check all 8 neighboring tiles if can
                    if (
                        // left
                        (i > 0 && j > 0  && colored[(i - 1) + (j - 1) * 50]) ||
                        (i > 0 &&           colored[(i - 1) +  j      * 50]) ||
                        (i > 0 && j < 49 && colored[(i - 1) + (j + 1) * 50]) ||
                        // middle
                        (j > 0  && colored[i + (j - 1) * 50]) ||
                        (j < 49 && colored[i + (j + 1) * 50]) ||
                        // right
                        (i < 49 && j > 0  && colored[(i + 1) + (j - 1) * 50]) ||
                        (i < 49 &&           colored[(i + 1) +  j      * 50]) ||
                        (i < 49 && j < 49 && colored[(i + 1) + (j + 1) * 50])
                    )
                    {
                        pixels[i + j * 50] = new Color(0f, 0f, 0f, 1f);
                    }
                }
            }

            texture.SetPixels(pixels);
        }
    }
}

using System;
using System.IO;
using System.Linq;
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
        private static string CreatureIconPath(string item) => item == null ? FEPathTo("resources", "creatures") : FEPathTo("resources", "creatures", item + ".png");
        private static string SlugcatIconPath(string scug) => scug == null ? FEPathTo("resources", "slugcats") : FEPathTo("resources", "slugcats", scug + ".png");

        public static string SafePath(string path)
        {
            path = path.Replace("/..", "").Substring(1);
            return FEPathTo(path.Split('/'));
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
            // Copy over all frontend files
            if (true)
            {
                string path = Path.Combine(Data.ModDirectory, "map-frontend");
                Plugin.Logger.LogDebug(path);
            }

            // Get slugcat icons
            foreach (var item in SlugcatStats.Name.values.entries)
            {
                var scug = new SlugcatStats.Name(item, false);
                if (!File.Exists(SlugcatIconPath(item.ToLower())) || (replaceAll && !IsDefaultSlugcat(scug)))
                {
                    var sprite = new FSprite("Kill_Slugcat", true);
                    var color = PlayerGraphics.DefaultSlugcatColor(scug);
                    var tex = SpriteColor(sprite, color);
                    Iconify(tex);
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
                        var sprite = new FSprite(CreatureSymbol.SpriteNameOfCreature(iconData), true);
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
            var atlasTex = sprite.element.atlas.texture;
            var atlas = new Texture2D(atlasTex.width, atlasTex.height, (atlasTex as Texture2D).format, atlasTex.mipmapCount, false);
            Plugin.Logger.LogDebug((atlasTex as Texture2D).format);
            Graphics.CopyTexture(atlasTex, atlas);
            var rect = sprite.element.sourceRect;
            var pixels = atlas.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
            var tex = new Texture2D((int)rect.width, (int)rect.height);
            tex.SetPixels(pixels);
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
            bool[] colored = pixels.Select(x => x.a > 0f).ToArray();

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

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MoreSlugcats;
using RWCustom;
using UnityEngine;
using UnityEngine.Networking;

namespace MapExporterNew
{
    internal static class Resources
    {
        /// <summary>
        /// Gets the front-end location path thingy of something
        /// </summary>
        public static string FEPathTo(params string[] path) => path.Aggregate(Path.Combine(Data.ModDirectory, "map-frontend"), Path.Combine);
        public static string TilePathTo(params string[] path) => path.Aggregate(Data.FinalDir, Path.Combine);
        public static string CreatureIconPath(string item = null) => item == null ? FEPathTo("resources", "creatures") : FEPathTo("resources", "creatures", item + ".png");
        public static string ObjectIconPath(string item = null) => item == null ? FEPathTo("resources", "objects") : FEPathTo("resources", "objects", item + ".png");
        public static string SlugcatIconPath(string scug = null) => scug == null ? FEPathTo("resources", "slugcats") : FEPathTo("resources", "slugcats", scug + ".png");

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
                    bool hasRegionName = Data.RegionNames.TryGetValue(kv.Key, out var regionName);
                    finished.Add(kv.Key, new() {
                        {
                            "slugcats",
                            kv.Value.Select(x => x.value.ToLowerInvariant()).ToArray()
                        },
                        {
                            "name",
                            hasRegionName ? regionName.name : Region.GetRegionFullName(kv.Key, null)
                        },
                        {
                            "specificNames",
                            kv.Value.Select(
                                x => (hasRegionName && regionName.personalNames.TryGetValue(x, out var name)) ? name : Region.GetRegionFullName(kv.Key, x))
                            .ToArray()
                        }
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

        private static void GenerateSlugcatIcon(SlugcatStats.Name scug, string outputPath)
        {
            GenerateDefaultIcon(); // Generate an icon so that it will be there by default
            Custom.rainWorld.StartCoroutine(ProcessLoop());

            ///////////////////////////////////////////////////////////////////////////////////////////////////////////

            void GenerateDefaultIcon()
            {
                var tex = new Texture2D(1, 1);
                tex.LoadImage(File.ReadAllBytes(Path.Combine(SlugcatIconPath("default"))));

                var pixels = tex.GetPixels();
                var color = PlayerGraphics.DefaultSlugcatColor(scug);
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] *= color;
                }

                tex.SetPixels(pixels);

                File.WriteAllBytes(outputPath, tex.EncodeToPNG());
                UnityEngine.Object.Destroy(tex);
            }

            IEnumerator ProcessLoop()
            {
                /**
                 * There are two ways we could go about doing this
                 *   Option 1: https://rainworldmods.miraheze.org/wiki/Special:FilePath/{slugcat}_icon.png
                 *     Pros: on success, this redirects to the image
                 *     Cons: on not success, it takes us to a normal HTML page
                 *   Option 2: https://rainworldmods.miraheze.org/w/api.php?action=query&titles=File:{slugcat}_icon.png&prop=imageinfo&iiprop=url&format=json
                 *     Pros: proper API endpoint, gives us computer readable data always
                 *     Cons: we need to make a second request to get the image on success
                 * 
                 * Given these, Option 2 is perhaps the best option as we just have to parse the JSON.
                 */

                // Make the request to get the URL
                var actualName = SlugcatStats.getSlugcatName(scug).Replace(' ', '_');
                var uriReqUri = $"https://rainworldmods.miraheze.org/w/api.php?action=query&titles=File:{actualName}_icon.png&prop=imageinfo&iiprop=url&format=json";

                using (var uriReq = UnityWebRequest.Get(uriReqUri))
                {
                    yield return uriReq.SendWebRequest();

                    // Was it successful?
                    if (uriReq.result == UnityWebRequest.Result.Success)
                    {
                        // Get the image's URL
                        var json = (Dictionary<string, object>)((Dictionary<string, object>)Json.Deserialize(uriReq.downloadHandler.text))["query"];
                        if (json.ContainsKey("pages"))
                        {
                            var pages = (Dictionary<string, object>)json["pages"];
                            var page = pages.First();
                            if (page.Key != "-1")
                            {
                                // File *does* exist, we can proceed to request the texture
                                // Big ugly casting hell to get the actual URL because I went the hard route instead of using Newtonsoft :3
                                var imgUrl = (string)((Dictionary<string, object>)((List<object>)((Dictionary<string, object>)page.Value)["imageinfo"])[0])["url"];

                                // Request to get the image
                                using (var texReq = UnityWebRequestTexture.GetTexture(imgUrl))
                                {
                                    yield return texReq.SendWebRequest();

                                    if (texReq.result == UnityWebRequest.Result.Success)
                                    {
                                        // Yayayyayyayayayayay we finally have the texture :D
                                        var tex = DownloadHandlerTexture.GetContent(texReq);
                                        CenterTextureInRect(tex, 50, 50);

                                        File.WriteAllBytes(outputPath, tex.EncodeToPNG());
                                        UnityEngine.Object.Destroy(tex);
                                    }
                                    else
                                    {
                                        Plugin.Logger.LogWarning($"ERRORED RETRIEVING TEXTURE FOR {scug}\nReason: {texReq.result}\nError details: {texReq.error}");
                                    }
                                }
                            }
                        }

                    }
                    else
                    {
                        Plugin.Logger.LogWarning($"ERRORED RETRIEVING URI FOR {scug}\nReason: {uriReq.result}\nError details: {uriReq.error}");
                    }
                }

                yield break;
            }
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
                    Plugin.Logger.LogDebug("Making icon for " + item);
                    // await Task.Run(() => { GenerateSlugcatIcon(scug, SlugcatIconPath(item.ToLower())); });
                    GenerateSlugcatIcon(scug, SlugcatIconPath(item.ToLower()));
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
                        File.WriteAllBytes(CreatureIconPath(item.ToLower()), tex.EncodeToPNG());
                        UnityEngine.Object.Destroy(tex);
                    }
                }
            }

            // Get object icons
            if (!Directory.Exists(ObjectIconPath()))
            {
                Directory.CreateDirectory(ObjectIconPath());
            }
            var creatureNames = CreatureTemplate.Type.values.entries.ToHashSet();
            var addedNames = new HashSet<string>();
            foreach (var item in PlacedObject.Type.values.entries.ToArray()) // the ToArray is just to create a copy of it because it fails for some reason
            {
                string name = item;
                bool flag = false;
                if (item.StartsWith("Dead") && creatureNames.Contains(name.Substring(4)))
                {
                    name = item.Substring(4);
                    flag = true;
                }
                if ((flag || creatureNames.Contains(name)) && (!File.Exists(ObjectIconPath(name)) || replaceAll))
                {
                    var iconData = new IconSymbol.IconSymbolData
                    {
                        critType = new CreatureTemplate.Type(name, false)
                    };
                    var spriteName = CreatureSymbol.SpriteNameOfCreature(iconData);
                    if (spriteName == "Futile_White") continue;
                    var sprite = new FSprite(spriteName, true);
                    var color = CreatureSymbol.ColorOfCreature(iconData);
                    var tex = SpriteColor(sprite, color);
                    Iconify(tex);
                    File.WriteAllBytes(ObjectIconPath(item.ToLower()), tex.EncodeToPNG());
                    UnityEngine.Object.Destroy(tex);
                    addedNames.Add(name.ToLower());
                }
            }
            foreach (var item in AbstractPhysicalObject.AbstractObjectType.values.entries.ToArray())
            {
                if (!File.Exists(ObjectIconPath(item)) || replaceAll)
                {

                    var type = new AbstractPhysicalObject.AbstractObjectType(item, false);
                    var spriteName = ItemSymbol.SpriteNameForItem(type, 0);
                    if (spriteName == "Futile_White") continue;
                    var sprite = new FSprite(spriteName, true);
                    var color = ItemSymbol.ColorForItem(type, 0);
                    var tex = SpriteColor(sprite, color);
                    Iconify(tex);
                    File.WriteAllBytes(ObjectIconPath(item.ToLower()), tex.EncodeToPNG());
                    UnityEngine.Object.Destroy(tex);
                    addedNames.Add(item.ToLower());
                }
            }

            foreach (var item in PlacedObject.Type.values.entries.ToArray())
            {
                var po = new PlacedObject(new PlacedObject.Type(item, false), null);
                bool token = po.data is CollectToken.CollectTokenData;
                if (AcceptablePlacedObject(po) && !token && !Data.PlacedObjectIcons.ContainsKey(item.ToLower()))
                {
                    Data.PlacedObjectIcons.Add(item.ToLower(), (addedNames.Contains(item.ToLower()) ? item.ToLower() : "unknown", true));
                }
            }

            // Pearls
            if (!Directory.Exists(Path.Combine(ObjectIconPath(), "pearl")))
            {
                Directory.CreateDirectory(Path.Combine(ObjectIconPath(), "pearl"));
            }
            foreach (var pearl in DataPearl.AbstractDataPearl.DataPearlType.values.entries)
            {
                var path = ObjectIconPath(Path.Combine("pearl", pearl.ToLower()));
                if (!File.Exists(path) || replaceAll)
                {
                    var spriteName = ItemSymbol.SpriteNameForItem(AbstractPhysicalObject.AbstractObjectType.DataPearl, 0);
                    if (spriteName == "Futile_White") continue;
                    var sprite = new FSprite(spriteName, true);
                    var color = DataPearl.UniquePearlMainColor(new DataPearl.AbstractDataPearl.DataPearlType(pearl, false));
                    var tex = SpriteColor(sprite, color);
                    Iconify(tex);
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    UnityEngine.Object.Destroy(tex);
                }
            }
        }

        public static bool AcceptablePlacedObject(PlacedObject obj)
        {
            return (obj.data is PlacedObject.ConsumableObjectData && obj.data is not PlacedObject.VoidSpawnEggData)
                || obj.data is CollectToken.CollectTokenData;
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
            const int w = 50, h = 50;

            CenterTextureInRect(texture, w, h);

            // Add outline
            Color[] pixels = texture.GetPixels();
            bool[] colored = pixels.Select(x => x.a > 0f).ToArray();

            for (int i = 0; i < w; i++)
            {
                for (int j = 0; j < h; j++)
                {
                    if (colored[i + j * w])
                    {
                        var col = pixels[i + j * w];
                        pixels[i + j * w] = Color.Lerp(Color.black, new Color(col.r, col.g, col.b), col.a);
                    }
                    else
                    {
                        // Check all 8 neighboring tiles if can do outline
                        if (
                            // left
                            (i > 0 && j > 0 && colored[(i - 1) + (j - 1) * w]) ||
                            (i > 0 && colored[(i - 1) + j * w]) ||
                            (i > 0 && j < h - 1 && colored[(i - 1) + (j + 1) * w]) ||
                            // middle
                            (j > 0 && colored[i + (j - 1) * w]) ||
                            (j < h - 1 && colored[i + (j + 1) * w]) ||
                            // right
                            (i < w - 1 && j > 0 && colored[(i + 1) + (j - 1) * w]) ||
                            (i < w - 1 && colored[(i + 1) + j * w]) ||
                            (i < w - 1 && j < h - 1 && colored[(i + 1) + (j + 1) * w])
                        )
                        {
                            pixels[i + j * w] = new Color(0f, 0f, 0f, 1f);
                        }
                    }

                }
            }

            texture.SetPixels(pixels);
        }
    }
}

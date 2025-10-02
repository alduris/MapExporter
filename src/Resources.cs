using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using DevInterface;
using MoreSlugcats;
using RWCustom;
using UnityEngine;
using UnityEngine.Networking;
using Watcher;

namespace MapExporterNew
{
    internal static class Resources
    {
        /// <summary>
        /// Gets the front-end location path thingy of something
        /// </summary>
        public static string FEPathTo(params string[] path) => path.Aggregate(Path.Combine(Data.ModDirectory, "map-frontend"), Path.Combine);
        public static string TilePathTo(params string[] path) => path.Aggregate(Data.FinalDir, Path.Combine);
        public static string CreatureIconPath(string item = null) => item == null ? FEPathTo("resources", "icons") : FEPathTo("resources", "icons", item + ".png");
        public static string ObjectIconPath(string item = null) => item == null ? FEPathTo("resources", "icons") : FEPathTo("resources", "icons", item + ".png");
        public static string PearlIconPath(string item = null) => item == null ? FEPathTo("resources", "icons", "pearl") : FEPathTo("resources", "icons", "pearl", item + ".png");
        public static string WarpIconPath(string region = null) => region == null ? FEPathTo("resources", "warp") : FEPathTo("resources", "warp", region.ToLowerInvariant() + ".png");
        public static string SlugcatIconPath(string scug = null) => scug == null ? FEPathTo("resources", "slugcats") : FEPathTo("resources", "slugcats", scug + ".png");

        public static bool TryGetActualPath(string req, out string path)
        {
            if (req.Length > 0)
                req = req[1..];

            if (req.Length == 0) req = "index.html";

            var split = req.Split('/');
            path = FEPathTo(split);
            if (!File.Exists(path))
            {
                if (path.EndsWith(".png"))
                {
                    split[^1] = "unknown.png";
                    path = FEPathTo(split);
                    if (File.Exists(path))
                    {
                        return true;
                    }
                }
                path = null;
                return false;
            }
            return true;
        }

        public static bool TryGetTile(string req, out byte[] bytes)
        {
            bytes = null;
            if (!req.StartsWith("/slugcats/")) return false;

            string path = TilePathTo(req[10..].Split('/')); // 10 = len("/slugcats/")
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
            scug != SlugcatStats.Name.White
            && scug != SlugcatStats.Name.Yellow 
            && scug != SlugcatStats.Name.Red 
            && scug != SlugcatStats.Name.Night 
            && (
                !ModManager.MSC || (
                    scug != MoreSlugcatsEnums.SlugcatStatsName.Gourmand &&
                    scug != MoreSlugcatsEnums.SlugcatStatsName.Artificer &&
                    scug != MoreSlugcatsEnums.SlugcatStatsName.Rivulet &&
                    scug != MoreSlugcatsEnums.SlugcatStatsName.Spear &&
                    scug != MoreSlugcatsEnums.SlugcatStatsName.Saint &&
                    scug != MoreSlugcatsEnums.SlugcatStatsName.Sofanthiel &&
                    scug != MoreSlugcatsEnums.SlugcatStatsName.Slugpup
                )
            ) 
            && (!ModManager.Watcher || scug != WatcherEnums.SlugcatStatsName.Watcher);

        public static void CopyFrontendFiles(bool replaceAll = false)
        {
            // Get slugcat icons
            foreach (var item in SlugcatStats.Name.values.entries)
            {
                var scug = new SlugcatStats.Name(item, false);
                if (SlugcatStats.HiddenOrUnplayableSlugcat(scug) && (!ModManager.MSC || item != MoreSlugcatsEnums.SlugcatStatsName.Sofanthiel.value)) continue;

                if (!File.Exists(SlugcatIconPath(item.ToLowerInvariant())) || (replaceAll && !IsDefaultSlugcat(scug)))
                {
                    Plugin.Logger.LogDebug("Making icon for " + item);
                    // await Task.Run(() => { GenerateSlugcatIcon(scug, SlugcatIconPath(item.ToLowerInvariant())); });
                    GenerateSlugcatIcon(scug, SlugcatIconPath(item.ToLowerInvariant()));
                }
            }

            // Get creature icons
            var creatureNames = CreatureTemplate.Type.values.entries.ToHashSet();
            foreach (var item in creatureNames)
            {
                try
                {
                    bool isLizard = StaticWorld.GetCreatureTemplate(new CreatureTemplate.Type(item, false)).IsLizard;
                    if (!File.Exists(CreatureIconPath(item.ToLowerInvariant())) || isLizard || replaceAll)
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
                        else if (ModManager.Watcher && item == WatcherEnums.CreatureTemplateType.SkyWhale.value)
                        {
                            for (int i = 0; i <= 2; i++)
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
                                string name = i switch { 0 => "skywhale", 1 => "altskywhale", _ => throw new NotImplementedException() };
                                File.WriteAllBytes(CreatureIconPath(name), tex.EncodeToPNG());
                                UnityEngine.Object.Destroy(tex);
                            }
                        }
                        else if (isLizard)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                var iconData = new IconSymbol.IconSymbolData {
                                    critType = new CreatureTemplate.Type(item, false),
                                    intData = i
                                };
                                var spriteName = CreatureSymbol.SpriteNameOfCreature(iconData);
                                if (spriteName == "Futile_White") continue;
                                var sprite = new FSprite(spriteName, true);
                                var color = CreatureSymbol.ColorOfCreature(iconData);
                                var tex = SpriteColor(sprite, color);
                                Iconify(tex);
                                File.WriteAllBytes(CreatureIconPath(item.ToLowerInvariant() + (i == 0 ? "" : ("_" + i))), tex.EncodeToPNG());
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
                            File.WriteAllBytes(CreatureIconPath(item.ToLowerInvariant()), tex.EncodeToPNG());
                            UnityEngine.Object.Destroy(tex);
                        }
                    }
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogError($"Could not create icon for {item}!");
                    Plugin.Logger.LogError(e);
                }
            }

            // Get object icons
            if (!Directory.Exists(ObjectIconPath()))
            {
                Directory.CreateDirectory(ObjectIconPath());
            }
            var addedNames = new HashSet<string>();
            var objectNames = AbstractPhysicalObject.AbstractObjectType.values.entries.ToHashSet();
            foreach (var item in PlacedObject.Type.values.entries.ToArray()) // the ToArray is just to create a copy of it because it fails for some reason
            {
                try
                {
                    string name = item;
                    bool deadCritter = false;
                    bool placedCritter = false;
                    bool rottenObject = false;
                    if (item.StartsWith("Dead") && creatureNames.Contains(item[4..]))
                    {
                        name = item[4..];
                        deadCritter = true;
                    }
                    else if (item.StartsWith("Rotten") && objectNames.Contains(item[6..]))
                    {
                        name = item[6..];
                        rottenObject = true;
                    }
                    else if (item.StartsWith("Placed") && objectNames.Contains(item[6..]))
                    {
                        name = item[6..];
                        placedCritter = true;
                    }
                    else if (item.StartsWith("Placed") && objectNames.Contains(item[6..^1]))
                    {
                        name = item[6..^1];
                        placedCritter = true;
                    }
                    if ((deadCritter || placedCritter || creatureNames.Contains(name)) && (!File.Exists(ObjectIconPath(name)) || replaceAll))
                    {
                        var iconData = new IconSymbol.IconSymbolData
                        {
                            critType = new CreatureTemplate.Type(name, false)
                        };
                        var spriteName = CreatureSymbol.SpriteNameOfCreature(iconData);
                        if (spriteName == "Futile_White") continue;
                        var sprite = new FSprite(spriteName, true);
                        var color = CreatureSymbol.ColorOfCreature(iconData);
                        var tex = SpriteColor(sprite, deadCritter ? Color.Lerp(color, Color.black, 0.25f) : color);
                        Iconify(tex);
                        File.WriteAllBytes(ObjectIconPath(item.ToLowerInvariant()), tex.EncodeToPNG());
                        UnityEngine.Object.Destroy(tex);
                        addedNames.Add(name.ToLowerInvariant());
                    }
                    else if (rottenObject && (!File.Exists(ObjectIconPath(name)) || replaceAll))
                    {
                        var type = new AbstractPhysicalObject.AbstractObjectType(name, false);
                        var spriteName = ItemSymbol.SpriteNameForItem(type, 1);
                        if (spriteName == "Futile_White") continue;
                        var sprite = new FSprite(spriteName, true);
                        var color = ItemSymbol.ColorForItem(type, 1);
                        var tex = SpriteColor(sprite, color);
                        Iconify(tex);
                        File.WriteAllBytes(ObjectIconPath(item.ToLowerInvariant()), tex.EncodeToPNG());
                        UnityEngine.Object.Destroy(tex);
                        addedNames.Add(item.ToLowerInvariant());
                    }
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogError($"Could not create icon for {item}!");
                    Plugin.Logger.LogError(e);
                }
            }
            foreach (var item in objectNames)
            {
                try
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
                        File.WriteAllBytes(ObjectIconPath(item.ToLowerInvariant()), tex.EncodeToPNG());
                        UnityEngine.Object.Destroy(tex);
                        addedNames.Add(item.ToLowerInvariant());
                    }
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogError($"Could not create icon for {item}!");
                    Plugin.Logger.LogError(e);
                }
            }

            /*foreach (var item in PlacedObject.Type.values.entries.ToArray())
            {
                var po = new PlacedObject(new PlacedObject.Type(item, false), null);
                bool token = po.data is CollectToken.CollectTokenData;
                if (AcceptablePlacedObject(po) && !token && !Data.PlacedObjectIcons.ContainsKey(item.ToLowerInvariant()))
                {
                    string defaultKey = "unknown";
                    var type = po.type;
                    var typeStr = type.ToString();
                    if (po.type == PlacedObject.Type.UniqueDataPearl) defaultKey = "datapearl";
                    else if (po.type == PlacedObject.Type.KarmaFlower) defaultKey = "karmaflower";
                    else if (typeStr.StartsWith("Dead") && creatureNames.Contains(typeStr.Substring(4))) defaultKey = typeStr.Substring(4).ToLowerInvariant();
                    else if (typeStr.StartsWith("Rotten") && objectNames.Contains(typeStr.Substring(6))) defaultKey = typeStr.Substring(6).ToLowerInvariant();
                    else if (typeStr.StartsWith("Placed") && creatureNames.Contains(typeStr.Substring(6))) defaultKey = typeStr.Substring(6).ToLowerInvariant();
                    else if (typeStr.StartsWith("Placed") && creatureNames.Contains(typeStr.Substring(6, typeStr.Length - 7))) defaultKey = typeStr.Substring(6, typeStr.Length - 7).ToLowerInvariant();

                    Data.PlacedObjectIcons.Add(item.ToLowerInvariant(), (addedNames.Contains(item.ToLowerInvariant()) ? item.ToLowerInvariant() : defaultKey, true));
                }
            }*/

            // Pearls
            if (!Directory.Exists(Path.Combine(ObjectIconPath(), "pearl")))
            {
                Directory.CreateDirectory(Path.Combine(ObjectIconPath(), "pearl"));
            }
            foreach (var pearl in DataPearl.AbstractDataPearl.DataPearlType.values.entries)
            {
                var path = ObjectIconPath(Path.Combine("pearl", pearl.ToLowerInvariant()));
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

            // Get warp icons
            foreach (var region in Region.GetFullRegionOrder().Append("unknown"))
            {
                var filename = $"illustrations/warp-{region.ToLowerInvariant()}.png";
                var iconPath = AssetManager.ResolveFilePath(filename);
                if (File.Exists(iconPath))
                {
                    var copyPath = WarpIconPath(region);
                    bool exists = File.Exists(copyPath);
                    if (!exists || replaceAll)
                    {
                        if (exists) File.Delete(copyPath);
                        File.Copy(iconPath, copyPath);
                    }
                }
            }
        }

        public static bool AcceptablePlacedObject(PlacedObject obj)
        {
            if (obj.deactivattable && !obj.active) return false;
            return
                (obj.data is PlacedObject.ConsumableObjectData
                    && obj.data is not PlacedObject.VoidSpawnEggData
                    && (!ModManager.MSC || obj.type != MoreSlugcatsEnums.PlacedObjectType.Germinator)
                    && (obj.type != PlacedObject.Type.SandGrubNetwork))
                || obj.data is CollectToken.CollectTokenData
                || obj.data is WarpPoint.WarpPointData
                || obj.data is DynamicWarpTargetData
                || obj.data is PlacedObject.RippleSpawnEggData // not VoidSpawnEggData surprisingly
                || obj.data is SpinningTopData
                || obj.type == WatcherEnums.PlacedObjectType.WeaverSpot
                || obj.type == PlacedObject.Type.SandGrubHole
                || (obj.type.ToString().StartsWith("Placed") && GetObjectCategory(obj.type) == ObjectsPage.DevObjectCategories.Creatures);

            static ObjectsPage.DevObjectCategories GetObjectCategory(PlacedObject.Type type)
            {
                // Have to do this because for some reason the method is not static and it's too much of a hassle to initialize the object to run one method otherwise
                ObjectsPage objPage = (ObjectsPage)FormatterServices.GetUninitializedObject(typeof(ObjectsPage));
                return objPage.DevObjectGetCategoryFromPlacedType(type);
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
            const int w = 50, h = 50;

            CenterTextureInRect(texture, w, h);

            Color[] pixels = texture.GetPixels();

            // Figure out where holes lie
            bool[,] outside = new bool[w, h];
            bool[,] ffChecked = new bool[w, h];
            Stack<(int x, int y)> toCheck = [];
            toCheck.Push((0, 0));
            while (toCheck.Count > 0)
            {
                var (x, y) = toCheck.Pop();
                if (x < 0 || x >= w || y < 0 || y >= h) continue;
                if (ffChecked[x, y]) continue;

                ffChecked[x, y] = true;
                if (pixels[x + y * w].a == 0f)
                {
                    outside[x, y] = true;
                    toCheck.Push((x + 1, y));
                    toCheck.Push((x - 1, y));
                    toCheck.Push((x, y + 1));
                    toCheck.Push((x, y - 1));
                }
            }

            // Add outline and fill holes
            for (int i = 0; i < w; i++)
            {
                for (int j = 0; j < h; j++)
                {
                    if (outside[i, j])
                    {
                        // Check all 8 neighboring tiles if can do outline
                        if (
                            // left
                            (i > 0 && j > 0 && !outside[i - 1, j - 1]) ||
                            (i > 0 && !outside[i - 1, j]) ||
                            (i > 0 && j < h - 1 && !outside[i - 1, j + 1]) ||
                            // middle
                            (j > 0 && !outside[i, j - 1]) ||
                            (j < h - 1 && !outside[i, j + 1]) ||
                            // right
                            (i < w - 1 && j > 0 && !outside[i + 1, j - 1]) ||
                            (i < w - 1 && !outside[i + 1, j]) ||
                            (i < w - 1 && j < h - 1 && !outside[i + 1, j + 1])
                        )
                        {
                            pixels[i + j * w] = new Color(0f, 0f, 0f, 1f);
                        }
                    }
                    else
                    {
                        var col = pixels[i + j * w];
                        pixels[i + j * w] = Color.Lerp(Color.black, new Color(col.r, col.g, col.b), col.a);
                    }
                }
            }

            texture.SetPixels(pixels);
        }
    }
}

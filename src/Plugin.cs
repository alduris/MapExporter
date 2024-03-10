using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using UnityEngine;
using MoreSlugcats;
using RWCustom;
using Random = UnityEngine.Random;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using System.Globalization;

#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete

namespace MapExporter;

[BepInPlugin(MOD_ID, "Map Exporter", "1.0.0")]
sealed class Plugin : BaseUnityPlugin
{
    // Config
    const string MOD_ID = "henpemaz-dual-noblecat-alduris.mapexporter";
    const string FLAG_TRIGGER = "--mapexport";
    static readonly bool screenshots = true;
    public static string regionRendering = "SU"; // in case something drastic goes wrong, this is the default
    public static readonly Queue<string> slugsRendering = [];

    public static bool FlagTriggered => Environment.GetCommandLineArgs().Contains(FLAG_TRIGGER);

    static readonly Dictionary<string, int[]> blacklistedCams = new()
    {
        { "SU_B13", new int[]{2} }, // one indexed
        { "GW_S08", new int[]{2} }, // in vanilla only
        { "SL_C01", new int[]{4,5} }, // crescent order or will break
    };

    public static new ManualLogSource Logger;

    public static bool NotHiddenRoom(AbstractRoom room) => !HiddenRoom(room);
    public static bool HiddenRoom(AbstractRoom room)
    {
        if (room == null)
        {
            return true;
        }
        if (room.world.DisabledMapRooms.Contains(room.name, StringComparer.InvariantCultureIgnoreCase))
        {
            Logger.LogDebug($"Room {room.world.game.StoryCharacter}/{room.name} is disabled");
            return true;
        }
        if (!room.offScreenDen)
        {
            if (room.connections.Length == 0)
            {
                Logger.LogDebug($"Room {room.world.game.StoryCharacter}/{room.name} with no outward connections is ignored");
                return true;
            }
            if (room.connections.All(r => room.world.GetAbstractRoom(r) is not AbstractRoom other || !other.connections.Contains(room.index)))
            {
                Logger.LogDebug($"Room {room.world.game.StoryCharacter}/{room.name} with no inward connections is ignored");
                return true;
            }
        }
        return false;
    }

    public void OnEnable()
    {
        try
        {
            Logger = base.Logger;
            IL.RainWorld.Awake += RainWorld_Awake;
            On.RainWorld.Start += RainWorld_Start; // "FUCK compatibility just run my hooks" - love you too henpemaz
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private void RainWorld_Start(On.RainWorld.orig_Start orig, RainWorld self)
    {
        Logger.LogDebug("Started start thingy");
        try
        {
            if (FlagTriggered)
            {
                On.Json.Serializer.SerializeValue += Serializer_SerializeValue;
                On.RainWorld.LoadSetupValues += RainWorld_LoadSetupValues;
                On.RainWorld.Update += RainWorld_Update;
                On.World.SpawnGhost += World_SpawnGhost;
                On.GhostWorldPresence.SpawnGhost += GhostWorldPresence_SpawnGhost;
                On.GhostWorldPresence.GhostMode_AbstractRoom_Vector2 += GhostWorldPresence_GhostMode_AbstractRoom_Vector2;
                On.Ghost.Update += Ghost_Update;
                On.RainWorldGame.ctor += RainWorldGame_ctor;
                On.RainWorldGame.Update += RainWorldGame_Update;
                On.RainWorldGame.RawUpdate += RainWorldGame_RawUpdate;
                new Hook(typeof(RainWorldGame).GetProperty("TimeSpeedFac").GetGetMethod(), typeof(Plugin).GetMethod("RainWorldGame_ZeroProperty"), this);
                new Hook(typeof(RainWorldGame).GetProperty("InitialBlackSeconds").GetGetMethod(), typeof(Plugin).GetMethod("RainWorldGame_ZeroProperty"), this);
                new Hook(typeof(RainWorldGame).GetProperty("FadeInTime").GetGetMethod(), typeof(Plugin).GetMethod("RainWorldGame_ZeroProperty"), this);
                On.OverWorld.WorldLoaded += OverWorld_WorldLoaded;
                // On.Room.ReadyForAI += Room_ReadyForAI;
                On.Room.Loaded += Room_Loaded;
                On.Room.ScreenMovement += Room_ScreenMovement;
                On.RoomCamera.DrawUpdate += RoomCamera_DrawUpdate;
                On.VoidSpawnGraphics.DrawSprites += VoidSpawnGraphics_DrawSprites;
                On.AntiGravity.BrokenAntiGravity.ctor += BrokenAntiGravity_ctor;
                On.GateKarmaGlyph.DrawSprites += GateKarmaGlyph_DrawSprites;
                On.WorldLoader.ctor_RainWorldGame_Name_bool_string_Region_SetupValues += WorldLoader_ctor_RainWorldGame_Name_bool_string_Region_SetupValues;
                On.ScavengersWorldAI.WorldFloodFiller.Update += WorldFloodFiller_Update;
                On.CustomDecal.LoadFile += CustomDecal_LoadFile;
                On.CustomDecal.InitiateSprites += CustomDecal_InitiateSprites;
                On.Menu.DialogBoxNotify.Update += DialogBoxNotify_Update;
                IL.BubbleGrass.Update += BubbleGrass_Update;
                On.MoreSlugcats.BlinkingFlower.DrawSprites += BlinkingFlower_DrawSprites;
                On.RoomCamera.ApplyEffectColorsToPaletteTexture += RoomCamera_ApplyEffectColorsToPaletteTexture;

                Logger.LogDebug("Finished start thingy");
            }
            else
            {
                // Register options thing
                Logger.LogDebug("Normal game instance, don't run hooks");

                // Register UI
                On.RainWorld.OnModsInit += (orig, self) =>
                {
                    orig(self);
                    MachineConnector.SetRegisteredOI(MOD_ID, new UI());
                    Logger.LogDebug("UI registered");
                };
            }
        }
        catch (Exception e)
        {
            Logger.LogDebug("Caught start thingy");
            Debug.LogException(e);
        }

        orig(self);
    }

    private void RainWorld_Awake(ILContext il)
    {
        var c = new ILCursor(il);

        for (int i = 0; i < 2; i++)
        {
            ILLabel brto = null;
            c.GotoNext(x => x.MatchCall(typeof(File), "Exists"), x => x.MatchBrfalse(out brto));
            if (brto != null )
            {
                c.Index--;
                c.MoveAfterLabels();
                c.EmitDelegate(() => FlagTriggered);
                c.Emit(OpCodes.Brtrue, brto);
            }
        }
    }

    // Fixes some crashes in ZZ (Aerial Arrays)
    private void RoomCamera_ApplyEffectColorsToPaletteTexture(On.RoomCamera.orig_ApplyEffectColorsToPaletteTexture orig, RoomCamera self, ref Texture2D texture, int color1, int color2)
    {
        color1 = Math.Min(color1, 21);
        color2 = Math.Min(color2, 21);
        orig(self, ref texture, color1, color2);
    }
    private void BlinkingFlower_DrawSprites(On.MoreSlugcats.BlinkingFlower.orig_DrawSprites orig, BlinkingFlower self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
        if (self.room == null)
        {
            foreach (var sprite in sLeaser.sprites)
            {
                sprite.isVisible = false;
            }
        }
        else
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);
        }
    }

    // Fix a crash in inv SS
    private void BubbleGrass_Update(ILContext il)
    {
        // Original code: (base.Submersion >= 0.2f && this.room.waterObject.WaterIsLethal)
        // Code after hook: (base.Submersion >= 0.2f && this.room.waterObject != null && this.room.waterObject.WaterIsLethal)
        // You'd think that because it was partially submerged that there would be water but apparently not...

        try
        {
            var c = new ILCursor(il);
            ILLabel brto = null;

            c.GotoNext(
                MoveType.After,
                x => x.MatchLdarg(0),
                x => x.MatchCall<PhysicalObject>("get_Submersion"),
                x => x.Match(OpCodes.Ldc_R4),
                x => x.MatchBltUn(out brto)
            );
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<BubbleGrass, bool>>(self => self.room.waterObject != null);
            c.Emit(OpCodes.Brfalse, brto);
        }
        catch (Exception e)
        {
            Logger.LogError("Bubble grass IL hook failed!");
            Logger.LogError(e);
        }
    }

    // Reapply on mod update screen
    private void DialogBoxNotify_Update(On.Menu.DialogBoxNotify.orig_Update orig, Menu.DialogBoxNotify self)
    {
        orig(self);
        if (self.continueButton.signalText == "REAPPLY")
        {
            self.continueButton.Clicked();
        }
    }


    // prevents a crash when a decal isn't loaded by pretending it doesn't exist and hiding it
    private void CustomDecal_LoadFile(On.CustomDecal.orig_LoadFile orig, CustomDecal self, string fileName)
    {
        try
        {
            orig(self, fileName);
        }
        catch (FileLoadException e)
        {
            Logger.LogError(e);
            (self.placedObject.data as PlacedObject.CustomDecalData).imageName = "PH";
            orig(self, "PH");
            // PH is the default image that you see when you place a custom decal into the world. It's just a white box with a thick red X and border in it.
        }
    }
    private void CustomDecal_InitiateSprites(On.CustomDecal.orig_InitiateSprites orig, CustomDecal self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
    {
        orig(self, sLeaser, rCam);
        if ((self.placedObject.data as PlacedObject.CustomDecalData).imageName == "PH")
        {
            sLeaser.sprites[0].isVisible = false;
        }
    }

    // prevents a crash with broken connections (scavengers don't need their ai in this)
    private void WorldFloodFiller_Update(On.ScavengersWorldAI.WorldFloodFiller.orig_Update orig, ScavengersWorldAI.WorldFloodFiller self)
    {
        self.finished = true;
    }

    private void Serializer_SerializeValue(On.Json.Serializer.orig_SerializeValue orig, Json.Serializer self, object value)
    {
        if (value is IJsonObject obj)
        {
            orig(self, obj.ToJson());
        }
        else
        {
            orig(self, value);
        }
    }

    // Consistent RNG ?
    private void RainWorld_Update(On.RainWorld.orig_Update orig, RainWorld self)
    {
        Random.InitState(0);
        orig(self);
    }

    #region fixes
    // shortcut consistency
    private void RoomCamera_DrawUpdate(On.RoomCamera.orig_DrawUpdate orig, RoomCamera self, float timeStacker, float timeSpeed)
    {
        if (self.room != null && self.room.shortcutsBlinking != null)
        {
            self.room.shortcutsBlinking = new float[self.room.shortcuts.Length, 4];
            for (int i = 0; i < self.room.shortcutsBlinking.GetLength(0); i++)
            {
                self.room.shortcutsBlinking[i, 3] = -1;
            }
        }
        orig(self, timeStacker, timeSpeed);
    }
    // no shake
    private void Room_ScreenMovement(On.Room.orig_ScreenMovement orig, Room self, Vector2? pos, Vector2 bump, float shake)
    {
        return;
    }
    // update faster
    private void RainWorldGame_RawUpdate(On.RainWorldGame.orig_RawUpdate orig, RainWorldGame self, float dt)
    {
        self.myTimeStacker += 2f;
        orig(self, dt);
    }
    //  no grav swithcing
    private void BrokenAntiGravity_ctor(On.AntiGravity.BrokenAntiGravity.orig_ctor orig, AntiGravity.BrokenAntiGravity self, int cycleMin, int cycleMax, RainWorldGame game)
    {
        orig(self, cycleMin, cycleMax, game);
        self.on = false;
        self.from = self.on ? 1f : 0f;
        self.to = self.on ? 1f : 0f;
        self.lights = self.to;
        self.counter = 40000;
    }
    // Make gate glyphs more visible
    private void GateKarmaGlyph_DrawSprites(On.GateKarmaGlyph.orig_DrawSprites orig, GateKarmaGlyph self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
        orig(self, sLeaser, rCam, timeStacker, camPos);

        sLeaser.sprites[1].shader = FShader.defaultShader;
        sLeaser.sprites[1].color = Color.white;

        if (self.requirement == MoreSlugcats.MoreSlugcatsEnums.GateRequirement.RoboLock)
        {
            for (int i = 2; i < 11; i++)
            {
                sLeaser.sprites[i].shader = FShader.defaultShader;
                sLeaser.sprites[i].color = Color.white;
            }
        }
    }
    // zeroes some annoying fades
    public delegate float orig_PropertyToZero(RainWorldGame self);
    public float RainWorldGame_ZeroProperty(orig_PropertyToZero _, RainWorldGame _1)
    {
        return 0f;
    }
    // spawn ghost always
    private void World_SpawnGhost(On.World.orig_SpawnGhost orig, World self)
    {
        self.game.rainWorld.safariMode = false;
        orig(self);
        self.game.rainWorld.safariMode = true;
    }
    // spawn ghosts always, to show them on the map
    private bool GhostWorldPresence_SpawnGhost(On.GhostWorldPresence.orig_SpawnGhost orig, GhostWorldPresence.GhostID ghostID, int karma, int karmaCap, int ghostPreviouslyEncountered, bool playingAsRed)
    {
        return true;
    }
    // don't let them affect nearby rooms
    private float GhostWorldPresence_GhostMode_AbstractRoom_Vector2(On.GhostWorldPresence.orig_GhostMode_AbstractRoom_Vector2 orig, GhostWorldPresence self, AbstractRoom testRoom, Vector2 worldPos)
    {
        if (self.ghostRoom.name != testRoom.name)
        {
            return 0f;
        }
        return orig(self, testRoom, worldPos);
    }
    // don't let them hurl us back to the karma screen
    private void Ghost_Update(On.Ghost.orig_Update orig, Ghost self, bool eu)
    {
        orig(self, eu);
        self.fadeOut = self.lastFadeOut = 0f;
    }
    // setup == useful
    private RainWorldGame.SetupValues RainWorld_LoadSetupValues(On.RainWorld.orig_LoadSetupValues orig, bool distributionBuild)
    {
        var setup = orig(false);

        setup.loadAllAmbientSounds = false;
        setup.playMusic = false;

        setup.cycleTimeMax = 10000;
        setup.cycleTimeMin = 10000;

        setup.gravityFlickerCycleMin = 10000;
        setup.gravityFlickerCycleMax = 10000;

        setup.startScreen = false;
        setup.cycleStartUp = false;

        setup.player1 = false;
        setup.worldCreaturesSpawn = false;
        setup.singlePlayerChar = 0;

        return setup;
    }

    // fuck you in particular
    private void VoidSpawnGraphics_DrawSprites(On.VoidSpawnGraphics.orig_DrawSprites orig, VoidSpawnGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
        //youre code bad
        for (int i = 0; i < sLeaser.sprites.Length; i++)
        {
            sLeaser.sprites[i].isVisible = false;
        }
    }

    // effects blacklist
    private void Room_Loaded(On.Room.orig_Loaded orig, Room self)
    {
        for (int i = self.roomSettings.effects.Count - 1; i >= 0; i--)
        {
            if (self.roomSettings.effects[i].type == RoomSettings.RoomEffect.Type.VoidSea) self.roomSettings.effects.RemoveAt(i); // breaks with no player
            else if (self.roomSettings.effects[i].type.ToString() == "CGCameraZoom") self.roomSettings.effects.RemoveAt(i); // bad for screenies
            // else if (((int)self.roomSettings.effects[i].type) >= 27 && ((int)self.roomSettings.effects[i].type) <= 36) self.roomSettings.effects.RemoveAt(i); // insects bad for screenies
        }
        foreach (var item in self.roomSettings.placedObjects)
        {
            // if (item.type == PlacedObject.Type.InsectGroup) item.active = false;
            if (item.type == PlacedObject.Type.FlyLure
                || item.type == PlacedObject.Type.JellyFish
                || item.type == PlacedObject.Type.BubbleGrass
                || item.type == PlacedObject.Type.TempleGuard
                || item.type == PlacedObject.Type.StuckDaddy
                || item.type == PlacedObject.Type.Hazer
                || item.type == PlacedObject.Type.Vine
                || item.type == PlacedObject.Type.ScavengerOutpost
                || item.type == PlacedObject.Type.DeadTokenStalk
                || item.type == PlacedObject.Type.SeedCob
                || item.type == PlacedObject.Type.DeadSeedCob
                || item.type == PlacedObject.Type.DeadHazer
                || item.type == PlacedObject.Type.HangingPearls
                || item.type == PlacedObject.Type.VultureGrub
                || item.type == PlacedObject.Type.HangingPearls
                || item.type == PlacedObject.Type.DeadVultureGrub
                || item.type == MoreSlugcatsEnums.PlacedObjectType.BigJellyFish
                || item.type == MoreSlugcatsEnums.PlacedObjectType.GlowWeed
                || item.type == MoreSlugcatsEnums.PlacedObjectType.GooieDuck
                || item.type == MoreSlugcatsEnums.PlacedObjectType.RotFlyPaper
                || item.type == MoreSlugcatsEnums.PlacedObjectType.DevToken
                || item.type == MoreSlugcatsEnums.PlacedObjectType.LillyPuck
                || item.type == MoreSlugcatsEnums.PlacedObjectType.Stowaway
                || item.type == MoreSlugcatsEnums.PlacedObjectType.MoonCloak
                || item.type == MoreSlugcatsEnums.PlacedObjectType.HRGuard
            )
                self.waitToEnterAfterFullyLoaded = Mathf.Max(self.waitToEnterAfterFullyLoaded, 20);

        }
        orig(self);
    }

    // no orcacles
    /*private void Room_ReadyForAI(On.Room.orig_ReadyForAI orig, Room self)
    {
        string oldname = self.abstractRoom.name;
        if (self.abstractRoom.name.EndsWith("_AI")) self.abstractRoom.name = "XXX"; // oracle breaks w no player
        orig(self);
        self.abstractRoom.name = oldname;
    }*/

    // no gate switching
    private void OverWorld_WorldLoaded(On.OverWorld.orig_WorldLoaded orig, OverWorld self)
    {
        return; // orig assumes a gate
    }

    private void WorldLoader_ctor_RainWorldGame_Name_bool_string_Region_SetupValues(On.WorldLoader.orig_ctor_RainWorldGame_Name_bool_string_Region_SetupValues orig, WorldLoader self, RainWorldGame game, SlugcatStats.Name playerCharacter, bool singleRoomWorld, string worldName, Region region, RainWorldGame.SetupValues setupValues)
    {
        orig(self, game, playerCharacter, singleRoomWorld, worldName, region, setupValues);

        for (int i = self.lines.Count - 1; i > 0; i--)
        {
            string[] split1 = Regex.Split(self.lines[i], " : ");
            if (split1.Length != 3 || split1[1] != "EXCLUSIVEROOM")
            {
                continue;
            }
            string[] split2 = Regex.Split(self.lines[i - 1], " : ");
            if (split2.Length != 3 || split2[1] != "EXCLUSIVEROOM")
            {
                continue;
            }
            // If rooms match on both EXCLUSIVEROOM entries, but not characters, merge the characters.
            if (split1[0] != split2[0] && split1[2] == split2[2])
            {
                string newLine = $"{split1[0]},{split2[0]} : EXCLUSIVEROOM : {split1[2]}";

                self.lines[i - 1] = newLine;
                self.lines.RemoveAt(i);
            }
        }
    }

    #endregion fixes

    // start
    private void RainWorldGame_ctor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
    {
        // Use safari mode, it's very sanitary
        manager.rainWorld.safariMode = true;
        manager.rainWorld.safariRainDisable = true;
        manager.rainWorld.safariSlugcat = SlugcatStats.Name.White;
        manager.rainWorld.safariRegion = "SU";

        orig(self, manager);

        // No safari overseers
        if (self.cameras[0].followAbstractCreature != null)
        {
            self.cameras[0].followAbstractCreature.Room.RemoveEntity(self.cameras[0].followAbstractCreature);
            self.cameras[0].followAbstractCreature.realizedObject?.Destroy();
            self.cameras[0].followAbstractCreature = null;
        }
        self.roomRealizer.followCreature = null;
        self.roomRealizer = null;

        // misc wtf fixes
        self.GetStorySession.saveState.theGlow = false;
        self.rainWorld.setup.playerGlowing = false;

        // Begone (according to noblecat)
        self.GetStorySession.saveState.deathPersistentSaveData.theMark = false;
        self.GetStorySession.saveState.deathPersistentSaveData.redsDeath = false;
        self.GetStorySession.saveState.deathPersistentSaveData.reinforcedKarma = false;
        // self.GetStorySession.saveState.deathPersistentSaveData.altEnding = false;
        self.GetStorySession.saveState.hasRobo = false;
        self.GetStorySession.saveState.redExtraCycles = false;
        self.GetStorySession.saveState.deathPersistentSaveData.ascended = false;

        // plus more
        self.GetStorySession.saveState.deathPersistentSaveData.PoleMimicEverSeen = true;
        self.GetStorySession.saveState.deathPersistentSaveData.SMEatTutorial = true;
        self.GetStorySession.saveState.deathPersistentSaveData.ArtificerMaulTutorial = true;
        self.GetStorySession.saveState.deathPersistentSaveData.GateStandTutorial = true;

        // no tutorials
        self.GetStorySession.saveState.deathPersistentSaveData.KarmaFlowerMessage = true;
        self.GetStorySession.saveState.deathPersistentSaveData.ScavMerchantMessage = true;
        self.GetStorySession.saveState.deathPersistentSaveData.ScavTollMessage = true;
        self.GetStorySession.saveState.deathPersistentSaveData.ArtificerTutorialMessage = true;
        self.GetStorySession.saveState.deathPersistentSaveData.DangleFruitInWaterMessage = true;
        self.GetStorySession.saveState.deathPersistentSaveData.GoExploreMessage = true;
        self.GetStorySession.saveState.deathPersistentSaveData.KarmicBurstMessage = true;
        self.GetStorySession.saveState.deathPersistentSaveData.SaintEnlightMessage = true;
        self.GetStorySession.saveState.deathPersistentSaveData.SMTutorialMessage = true;
        self.GetStorySession.saveState.deathPersistentSaveData.TongueTutorialMessage = true;

        // allow Saint ghosts
        self.GetStorySession.saveState.cycleNumber = 1;

        Logger.LogDebug("Starting capture task");

        captureTask = CaptureTask(self);
    }

    private void RainWorldGame_Update(On.RainWorldGame.orig_Update orig, RainWorldGame self)
    {
        orig(self);
        captureTask.MoveNext();
    }

    static string PathOfRegion(string slugcat, string region)
    {
        return Directory.CreateDirectory(Data.RenderOutputDir(slugcat.ToLower(), region.ToLower())).FullName;
    }

    static string PathOfMetadata(string slugcat, string region)
    {
        return Path.Combine(PathOfRegion(slugcat, region), "metadata.json");
    }

    static string PathOfScreenshot(string slugcat, string region, string room, int num)
    {
        return $"{Path.Combine(PathOfRegion(slugcat, region), room.ToLower())}_{num}.png";
    }

    private static int ScugPriority(string slugcat)
    {
        return slugcat switch
        {
            "white" => 10,      // do White first, they have the most generic regions
            "artificer" => 9,   // do Artificer next, they have Metropolis, Waterfront Facility, and past-GW
            "saint" => 8,       // do Saint next for Undergrowth and Silent Construct
            "rivulet" => 7,     // do Rivulet for The Rot
            "inv" => 6,         // do Inv because inv
            _ => 0              // everyone else has a mix of duplicate rooms
        };
    }

    // Runs half-synchronously to the game loop, bless iters
    System.Collections.IEnumerator captureTask;
    private System.Collections.IEnumerator CaptureTask(RainWorldGame game)
    {
        // Task start
        Random.InitState(0);

        var args = Environment.GetCommandLineArgs();
        var split = args[args.IndexOf(FLAG_TRIGGER) + 1].Split(';');
        regionRendering = split[0];
        foreach (var str in split[1].Split(','))
        {
            slugsRendering.Enqueue(str);
        }

        // 1st camera transition is a bit whack ? give it a sec to load
        while (game.cameras[0].room == null || !game.cameras[0].room.ReadyForPlayer) yield return null;
        for (int i = 0; i < 40; i++) yield return null;
        // ok game loaded I suppose
        game.cameras[0].room.abstractRoom.Abstractize();

        // Recreate scuglat list from last time if needed
        while (slugsRendering.Count > 0)
        {
            SlugcatStats.Name slugcat = new(slugsRendering.Dequeue());

            game.GetStorySession.saveStateNumber = slugcat;
            game.GetStorySession.saveState.saveStateNumber = slugcat;

            foreach (var step in CaptureRegion(game, regionRendering))
                yield return step;
        }

        Data.ScreenshotterStatus = Data.SSStatus.Finished;
        Data.SaveData();
        Application.Quit();
    }

    private System.Collections.IEnumerable CaptureRegion(RainWorldGame game, string region)
    {
        SlugcatStats.Name slugcat = game.StoryCharacter;

        // load region
        Random.InitState(0);
        game.overWorld.LoadWorld(region, slugcat, false);
        Logger.LogDebug($"Loaded {slugcat}/{region}");

        Directory.CreateDirectory(PathOfRegion(slugcat.value, region));

        RegionInfo mapContent = new(game.world);

        List<AbstractRoom> rooms = [.. game.world.abstractRooms];

        // Don't image rooms not available for this slugcat
        rooms.RemoveAll(HiddenRoom);

        // Don't image offscreen dens
        rooms.RemoveAll(r => r.offScreenDen);

        if (ReusedRooms.SlugcatRoomsToUse(slugcat.value, game.world, rooms) is string copyRooms)
        {
            mapContent.copyRooms = copyRooms;
        }
        else
        {
            foreach (var room in rooms)
            {
                foreach (var step in CaptureRoom(room, mapContent))
                    yield return step;
            }
        }

        File.WriteAllText(PathOfMetadata(slugcat.value, region), Json.Serialize(mapContent));

        Logger.LogDebug("capture task done with " + region);
    }

    private System.Collections.IEnumerable CaptureRoom(AbstractRoom room, RegionInfo regionContent)
    {
        RainWorldGame game = room.world.game;

        // load room
        game.overWorld.activeWorld.loadingRooms.Clear();
        Random.InitState(0);
        game.overWorld.activeWorld.ActivateRoom(room);
        // load room until it is loaded
        if (game.overWorld.activeWorld.loadingRooms.Count > 0 && game.overWorld.activeWorld.loadingRooms[0].room == room.realizedRoom)
        {
            RoomPreparer loading = game.overWorld.activeWorld.loadingRooms[0];
            while (!loading.done)
            {
                loading.Update();
            }
        }
        while (!(room.realizedRoom.loadingProgress >= 3 && room.realizedRoom.waitToEnterAfterFullyLoaded < 1))
        {
            room.realizedRoom.Update();
        }

        if (blacklistedCams.TryGetValue(room.name, out int[] cams))
        {
            var newpos = room.realizedRoom.cameraPositions.ToList();
            for (int i = cams.Length - 1; i >= 0; i--)
            {
                newpos.RemoveAt(cams[i] - 1);
            }
            room.realizedRoom.cameraPositions = [.. newpos];
        }

        yield return null;
        Random.InitState(0);
        // go to room
        game.cameras[0].MoveCamera(room.realizedRoom, 0);
        game.cameras[0].virtualMicrophone.AllQuiet();
        // get to room
        while (game.cameras[0].loadingRoom != null) yield return null;
        Random.InitState(0);

        regionContent.UpdateRoom(room.realizedRoom);

        for (int i = 0; i < room.realizedRoom.cameraPositions.Length; i++)
        {
            // load screen
            Random.InitState(room.name.GetHashCode()); // allow for deterministic random numbers, to make rain look less garbage
            game.cameras[0].MoveCamera(i);
            game.cameras[0].virtualMicrophone.AllQuiet();
            while (game.cameras[0].www != null) yield return null;
            yield return null;
            yield return null; // one extra frame maybe
                               // fire!

            if (screenshots)
            {
                string filename = PathOfScreenshot(game.StoryCharacter.value, room.world.name, room.name, i);

                if (!File.Exists(filename))
                {
                    ScreenCapture.CaptureScreenshot(filename);
                }
            }

            // palette and colors
            regionContent.LogPalette(game.cameras[0].currentPalette);

            yield return null; // one extra frame after ??
        }
        Random.InitState(0);
        room.Abstractize();
        yield return null;
    }
}
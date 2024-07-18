using System;
using System.IO;
using System.Linq;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Logging;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Mono.Cecil.Cil;
using UnityEngine;
using MoreSlugcats;
using RWCustom;
using MapExporter.Screenshotter;
using Random = UnityEngine.Random;
using Menu.Remix.MixedUI;

#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete

namespace MapExporter;

[BepInPlugin(MOD_ID, "Map Exporter", "1.0.0")]
sealed class Plugin : BaseUnityPlugin
{
    // Config
    public const string MOD_ID = "mapexporter";
    public const string FLAG_TRIGGER = "--mapexport";

    public static bool FlagTriggered => Environment.GetCommandLineArgs().Contains(FLAG_TRIGGER);

    public static new ManualLogSource Logger;

    public void OnEnable()
    {
        try
        {
            Logger = base.Logger;
            IL.RainWorld.Awake += RainWorld_Awake;
            On.RainWorld.Start += RainWorld_Start; // "FUCK compatibility just run my hooks" - love you too henpemaz

            Data.Initialize();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }

    private void RainWorld_Start(On.RainWorld.orig_Start orig, RainWorld self)
    {
        Logger.LogDebug("Started start thingy");
        try
        {
            On.Json.Serializer.SerializeValue += Serializer_SerializeValue;
            if (FlagTriggered)
            {
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
                On.SeedCob.FreezingPaletteUpdate += SeedCob_FreezingPaletteUpdate;
                On.InsectCoordinator.CreateInsect += InsectCoordinator_CreateInsect;
                IL.WorldLoader.MappingRooms += WorldLoader_MappingRooms;
                On.AbstractRoom.AddTag += AbstractRoom_AddTag;
                IL.WorldLoader.NextActivity += WorldLoader_NextActivity;
                On.WorldLoader.FindingCreaturesThread += WorldLoader_FindingCreaturesThread;
                On.Lightning.LightningSource.Update += LightningSource_Update;
                On.Oracle.Update += Oracle_Update;
                On.TempleGuard.Update += TempleGuard_Update;

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

                    static float OpScrollBox_MaxScroll_get(Func<OpScrollBox, float> orig, OpScrollBox self) => self.horizontal ? -Mathf.Max(self.contentSize - self.size.x, 0f) : orig(self);
                    _ = new Hook(typeof(OpScrollBox).GetProperty(nameof(OpScrollBox.MaxScroll)).GetGetMethod(), OpScrollBox_MaxScroll_get);

                    Logger.LogDebug("UI registered");
                };
            }
        }
        catch (Exception e)
        {
            Logger.LogError("Caught start thingy");
            Logger.LogError(e);
        }

        orig(self);
    }

    // Save and remove spawns
    private void WorldLoader_FindingCreaturesThread(On.WorldLoader.orig_FindingCreaturesThread orig, WorldLoader self)
    {
        orig(self);
        RegionInfo.spawnerCWT.Add(self.world, self.spawners);
        self.spawners = [];
    }

    // Make spawns be read, even though we remove them later
    private void WorldLoader_NextActivity(ILContext il)
    {
        var c = new ILCursor(il);

        c.GotoNext(x => x.MatchLdftn<WorldLoader>(nameof(WorldLoader.FindingCreaturesThread)));
        c.GotoPrev(x => x.MatchBrtrue(out _));
        c.EmitDelegate((bool _) => false);
        c.GotoPrev(x => x.MatchBrfalse(out _));
        c.EmitDelegate((bool _) => true);
    }

    // Make sure to grab all tags
    private void AbstractRoom_AddTag(On.AbstractRoom.orig_AddTag orig, AbstractRoom self, string tg)
    {
        // Always put room tags in the AbstractRoom roomTags array
        self.roomTags ??= [];
        self.roomTags.Add(tg);
        orig(self, tg);
        if (self.roomTags.Count > 1 && self.roomTags[self.roomTags.Count - 1] == self.roomTags[self.roomTags.Count - 2])
        {
            self.roomTags.Pop();
        }
    }

    private void WorldLoader_MappingRooms(ILContext il)
    {
        // Always put room tags in the WorldLoader roomTags array so they get added to the room
        try
        {
            var c = new ILCursor(il);
            int array = 0;
            int index = 3;

            c.GotoNext(x => x.MatchLdstr("SWARMROOM"));
            c.GotoNext(x => x.MatchLdloc(out array), x => x.MatchLdloc(out index), x => x.MatchLdelemRef());
            c.GotoNext(MoveType.AfterLabel, x => x.MatchLdloc(index), x => x.MatchLdcI4(1), x => x.MatchAdd());

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloc, array);
            c.Emit(OpCodes.Ldloc, index);
            c.Emit(OpCodes.Ldelem_Ref);
            c.EmitDelegate((WorldLoader self, string tag) =>
            {
                Logger.LogDebug(tag);
                if (self.roomTags[self.roomTags.Count - 1] == null)
                {
                    self.roomTags[self.roomTags.Count - 1] = [tag];
                }
                else
                {
                    var tags = self.roomTags[self.roomTags.Count - 1];
                    if (tags[tags.Count - 1] != tag)
                    {
                        tags.Add(tag);
                    }
                }
            });
        }
        catch (Exception e)
        {
            Logger.LogError("WorldLoader MappingRooms hook failed!");
            Logger.LogError(e);
        }
    }


    // disable resetting logs
    private void RainWorld_Awake(ILContext il)
    {
        var c = new ILCursor(il);

        for (int i = 0; i < 2; i++)
        {
            ILLabel brto = null;
            if (c.TryGotoNext(x => x.MatchCall(typeof(File), nameof(File.Exists)), x => x.MatchBrfalse(out brto)))
            {
                c.Index--;
                c.MoveAfterLabels();
                c.EmitDelegate(() => FlagTriggered);
                c.Emit(OpCodes.Brtrue, brto);
            }
        }
    }

    #region fixes

    // Only show guardians if user wants to
    private void TempleGuard_Update(On.TempleGuard.orig_Update orig, TempleGuard self, bool eu)
    {
        // Only call orig if user wants to spawn iteratosr
        orig(self, eu);
        bool value = Data.GetPreference(Preferences.ShowGuardians);
        if (!value)
        {
            self.Destroy();
            self.RemoveGraphicsModule();
            self.RemoveFromRoom();
        }
    }

    // no you don't
    private void LightningSource_Update(On.Lightning.LightningSource.orig_Update orig, Lightning.LightningSource self)
    {
        // you're code also bad
        self.wait = int.MaxValue;
        self.intensity = 0;
        self.lastIntensity = 0;
        orig(self);
    }

    // Only show iterators if the user wants to
    private void Oracle_Update(On.Oracle.orig_Update orig, Oracle self, bool eu)
    {
        // Only call orig if user wants to spawn iteratosr
        orig(self, eu);
        bool value = Data.GetPreference(Preferences.ShowOracles);
        if (!value)
        {
            self.Destroy();
            self.RemoveFromRoom();
        }
    }

    // Disable insects from spawning (including custom ones; was in original henpe code but didn't account for custom ones)
    private void InsectCoordinator_CreateInsect(On.InsectCoordinator.orig_CreateInsect orig, InsectCoordinator self, CosmeticInsect.Type type, Vector2 pos, InsectCoordinator.Swarm swarm)
    {
        // Only call orig if user wants to spawn insects
        bool value = Data.GetPreference(Preferences.ShowInsects);

        if (value)
        {
            orig(self, type, pos, swarm);
        }
    }


    // Fix seed cob crash for saint ig (sorry bensone I replaced your code copied straight from RW code :leditoroverload:)
    private void SeedCob_FreezingPaletteUpdate(On.SeedCob.orig_FreezingPaletteUpdate orig, SeedCob self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
    {
        if (self.room != null)
        {
            orig(self, sLeaser, rCam);
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
                x => x.MatchCall(typeof(PhysicalObject).GetProperty(nameof(PhysicalObject.Submersion)).GetGetMethod()),
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

    // spawn ghost always (actually use player preference)
    private void World_SpawnGhost(On.World.orig_SpawnGhost orig, World self)
    {
        // true by default
        if (Data.GetPreference(Preferences.ShowGhosts))
        {
            self.game.rainWorld.safariMode = false;
            orig(self);
            self.game.rainWorld.safariMode = true;
        }
    }

    // spawn ghosts always, to show them on the map (actually again, use player preference)
    private bool GhostWorldPresence_SpawnGhost(On.GhostWorldPresence.orig_SpawnGhost orig, GhostWorldPresence.GhostID ghostID, int karma, int karmaCap, int ghostPreviouslyEncountered, bool playingAsRed)
    {
        return Data.GetPreference(Preferences.ShowGhosts);
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
        setup.worldCreaturesSpawn = Data.GetPreference(Preferences.ShowCreatures);
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

    // no gate switching
    private void OverWorld_WorldLoaded(On.OverWorld.orig_WorldLoaded orig, OverWorld self)
    {
        return; // orig assumes a gate
    }

    #endregion fixes

    // effects blacklist
    private void Room_Loaded(On.Room.orig_Loaded orig, Room self)
    {
        for (int i = self.roomSettings.effects.Count - 1; i >= 0; i--)
        {
            if (self.roomSettings.effects[i].type == RoomSettings.RoomEffect.Type.VoidSea) self.roomSettings.effects.RemoveAt(i); // breaks with no player
            else if (self.roomSettings.effects[i].type.ToString() == "CGCameraZoom") self.roomSettings.effects.RemoveAt(i); // bad for screenies
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
                || (ModManager.MSC && (
                    item.type == MoreSlugcatsEnums.PlacedObjectType.BigJellyFish
                    || item.type == MoreSlugcatsEnums.PlacedObjectType.GlowWeed
                    || item.type == MoreSlugcatsEnums.PlacedObjectType.GooieDuck
                    || item.type == MoreSlugcatsEnums.PlacedObjectType.RotFlyPaper
                    || item.type == MoreSlugcatsEnums.PlacedObjectType.DevToken
                    || item.type == MoreSlugcatsEnums.PlacedObjectType.LillyPuck
                    || item.type == MoreSlugcatsEnums.PlacedObjectType.Stowaway
                    || item.type == MoreSlugcatsEnums.PlacedObjectType.MoonCloak
                    || item.type == MoreSlugcatsEnums.PlacedObjectType.HRGuard
                ))
            )
                self.waitToEnterAfterFullyLoaded = Mathf.Max(self.waitToEnterAfterFullyLoaded, 20);

        }
        orig(self);
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

    // Runs half-synchronously to the game loop, bless iters
    System.Collections.IEnumerator captureTask;

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

        captureTask = new Capturer().CaptureTask(self);
    }

    private void RainWorldGame_Update(On.RainWorldGame.orig_Update orig, RainWorldGame self)
    {
        orig(self);
        captureTask.MoveNext();
    }
}
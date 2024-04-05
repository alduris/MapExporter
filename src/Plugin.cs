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
using MapExporter.Screenshotter;
using Random = UnityEngine.Random;
using System.Drawing;
using RWCustom;

#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete

namespace MapExporter;

[BepInPlugin(MOD_ID, "Map Exporter", "1.0.0")]
sealed class Plugin : BaseUnityPlugin
{
    // Config
    public const string MOD_ID = "henpemaz-dual-noblecat-alduris.mapexporter";
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

            Data.TryCreateDirectories();
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
                On.SeedCob.DrawSprites += SeedCob_DrawSprites;

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
            Logger.LogError("Caught start thingy");
            Logger.LogError(e);
        }

        orig(self);
    }

    private void SeedCob_DrawSprites(On.SeedCob.orig_DrawSprites orig, SeedCob self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
        Vector2 vector = Vector2.Lerp(self.firstChunk.lastPos, self.firstChunk.pos, timeStacker);
        Vector2 vector2 = Vector2.Lerp(self.bodyChunks[1].lastPos, self.bodyChunks[1].pos, timeStacker);
        float num = 0.5f;
        Vector2 vector3 = self.rootPos;
        for (int i = 0; i < self.stalkSegments; i++)
        {
            float f = (float)i / (float)(self.stalkSegments - 1);
            Vector2 vector4 = Custom.Bezier(self.rootPos, self.rootPos + self.rootDir * Vector2.Distance(self.rootPos, self.placedPos) * 0.2f, vector2, vector2 + Custom.DirVec(vector, vector2) * Vector2.Distance(rootPos, placedPos) * 0.2f, f);
            Vector2 normalized = (vector3 - vector4).normalized;
            Vector2 vector5 = Custom.PerpendicularVector(normalized);
            float num2 = Vector2.Distance(vector3, vector4) / 5f;
            float num3 = Mathf.Lerp(self.bodyChunkConnections[0].distance / 14f, 1.5f, Mathf.Pow(Mathf.Sin(Mathf.Pow(f, 2f) * (float)Math.PI), 0.5f));
            float num4 = 1f;
            Vector2 vector6 = default(Vector2);
            for (int j = 0; j < 2; j++)
            {
                (sLeaser.sprites[self.StalkSprite(j)] as TriangleMesh).MoveVertice(i * 4, vector3 - normalized * num2 - vector5 * (num3 + num) * 0.5f * num4 - camPos + vector6);
                (sLeaser.sprites[self.StalkSprite(j)] as TriangleMesh).MoveVertice(i * 4 + 1, vector3 - normalized * num2 + vector5 * (num3 + num) * 0.5f * num4 - camPos + vector6);
                (sLeaser.sprites[self.StalkSprite(j)] as TriangleMesh).MoveVertice(i * 4 + 2, vector4 + normalized * num2 - vector5 * num3 * num4 - camPos + vector6);
                (sLeaser.sprites[self.StalkSprite(j)] as TriangleMesh).MoveVertice(i * 4 + 3, vector4 + normalized * num2 + vector5 * num3 * num4 - camPos + vector6);
                num4 = 0.35f;
                vector6 += -rCam.room.lightAngle.normalized * num3 * 0.5f;
            }

            vector3 = vector4;
            num = num3;
        }

        vector3 = vector2 + Custom.DirVec(vector, vector2);
        num = 2f;
        for (int k = 0; k < self.cobSegments; k++)
        {
            float t = (float)k / (float)(self.cobSegments - 1);
            Vector2 vector7 = Vector2.Lerp(vector2, vector, t);
            Vector2 normalized2 = (vector3 - vector7).normalized;
            Vector2 vector8 = Custom.PerpendicularVector(normalized2);
            float num5 = Vector2.Distance(vector3, vector7) / 5f;
            float num6 = 2f;
            (sLeaser.sprites[self.CobSprite] as TriangleMesh).MoveVertice(k * 4, vector3 - normalized2 * num5 - vector8 * (num6 + num) * 0.5f - camPos);
            (sLeaser.sprites[self.CobSprite] as TriangleMesh).MoveVertice(k * 4 + 1, vector3 - normalized2 * num5 + vector8 * (num6 + num) * 0.5f - camPos);
            (sLeaser.sprites[self.CobSprite] as TriangleMesh).MoveVertice(k * 4 + 2, vector7 + normalized2 * num5 - vector8 * num6 - camPos);
            (sLeaser.sprites[self.CobSprite] as TriangleMesh).MoveVertice(k * 4 + 3, vector7 + normalized2 * num5 + vector8 * num6 - camPos);
            vector3 = vector7;
            num = num6;
        }

        float num7 = Mathf.Lerp(self.lastOpen, self.open, timeStacker);
        for (int l = 0; l < 2; l++)
        {
            float num8 = -1f + (float)l * 2f;
            num = 2f;
            vector3 = vector + Custom.DirVec(vector2, vector) * 7f;
            float num9 = Custom.AimFromOneVectorToAnother(vector, vector2);
            Vector2 vector9 = vector;
            for (int m = 0; m < self.cobSegments; m++)
            {
                float num10 = (float)m / (float)(self.cobSegments - 1);
                vector9 += Custom.DegToVec(num9 + num8 * Mathf.Pow(num7, Mathf.Lerp(1f, 0.1f, num10)) * 50f * Mathf.Pow(num10, 0.5f)) * (Vector2.Distance(vector, vector2) * 1.1f + 8f) / self.cobSegments;
                Vector2 normalized3 = (vector3 - vector9).normalized;
                Vector2 vector10 = Custom.PerpendicularVector(normalized3);
                float num11 = Vector2.Distance(vector3, vector9) / 5f;
                float num12 = Mathf.Lerp(2f, 6f, Mathf.Pow(Mathf.Sin(Mathf.Pow(num10, 0.5f) * (float)Math.PI), 0.5f));
                (sLeaser.sprites[self.ShellSprite(l)] as TriangleMesh).MoveVertice(m * 4, vector3 - normalized3 * num11 - vector10 * (num12 + num) * 0.5f * (1 - l) - camPos);
                (sLeaser.sprites[self.ShellSprite(l)] as TriangleMesh).MoveVertice(m * 4 + 1, vector3 - normalized3 * num11 + vector10 * (num12 + num) * 0.5f * l - camPos);
                (sLeaser.sprites[self.ShellSprite(l)] as TriangleMesh).MoveVertice(m * 4 + 2, vector9 + normalized3 * num11 - vector10 * num12 * (1 - l) - camPos);
                (sLeaser.sprites[self.ShellSprite(l)] as TriangleMesh).MoveVertice(m * 4 + 3, vector9 + normalized3 * num11 + vector10 * num12 * l - camPos);
                vector3 = new Vector2(vector9.x, vector9.y);
                num = num12;
                num9 = Custom.VecToDeg(-normalized3);
            }
        }

        if (num7 > 0f)
        {
            Vector2 vector11 = Custom.DirVec(vector2, vector);
            Vector2 vector12 = Custom.PerpendicularVector(vector11);
            for (int n = 0; n < self.seedPositions.Length; n++)
            {
                Vector2 vector13 = vector2 + vector11 * self.seedPositions[n].y * (Vector2.Distance(vector2, vector) - 10f) + vector12 * self.seedPositions[n].x * 3f;
                float num13 = 1f + Mathf.Sin((float)n / (float)(self.seedPositions.Length - 1) * (float)Math.PI);
                if (self.AbstractCob.dead)
                {
                    num13 *= 0.5f;
                }

                sLeaser.sprites[self.SeedSprite(n, 0)].isVisible = true;
                sLeaser.sprites[self.SeedSprite(n, 1)].isVisible = self.seedsPopped[n];
                sLeaser.sprites[self.SeedSprite(n, 2)].isVisible = true;
                sLeaser.sprites[self.SeedSprite(n, 0)].scale = (self.seedsPopped[n] ? num13 : 0.35f);
                sLeaser.sprites[self.SeedSprite(n, 0)].x = vector13.x - camPos.x;
                sLeaser.sprites[self.SeedSprite(n, 0)].y = vector13.y - camPos.y;
                Vector2 vector14 = default(Vector2);
                if (self.seedsPopped[n])
                {
                    vector14 = vector12 * Mathf.Pow(Mathf.Abs(self.seedPositions[n].x), Custom.LerpMap(num13, 1f, 2f, 1f, 0.5f)) * Mathf.Sign(self.seedPositions[n].x) * 3.5f * num13;
                    if (!self.AbstractCob.dead)
                    {
                        sLeaser.sprites[self.SeedSprite(n, 2)].element = Futile.atlasManager.GetElementWithName("tinyStar");
                    }

                    sLeaser.sprites[self.SeedSprite(n, 2)].rotation = Custom.VecToDeg(vector11);
                    sLeaser.sprites[self.SeedSprite(n, 2)].scaleX = Mathf.Pow(1f - Mathf.Abs(self.seedPositions[n].x), 0.2f);
                }

                sLeaser.sprites[self.SeedSprite(n, 1)].x = vector13.x + vector14.x * 0.35f - camPos.x;
                sLeaser.sprites[self.SeedSprite(n, 1)].y = vector13.y + vector14.y * 0.35f - camPos.y;
                sLeaser.sprites[self.SeedSprite(n, 1)].scale = (self.seedsPopped[n] ? num13 : 0.4f) * 0.5f;
                sLeaser.sprites[self.SeedSprite(n, 2)].x = vector13.x + vector14.x - camPos.x;
                sLeaser.sprites[self.SeedSprite(n, 2)].y = vector13.y + vector14.y - camPos.y;
            }
        }

        for (int num14 = 0; num14 < self.leaves.GetLength(0); num14++)
        {
            Vector2 vector15 = Vector2.Lerp(self.leaves[num14, 1], self.leaves[num14, 0], timeStacker);
            sLeaser.sprites[self.LeafSprite(num14)].x = vector2.x - camPos.x;
            sLeaser.sprites[self.LeafSprite(num14)].y = vector2.y - camPos.y;
            sLeaser.sprites[self.LeafSprite(num14)].rotation = Custom.AimFromOneVectorToAnother(vector2, vector15);
            sLeaser.sprites[self.LeafSprite(num14)].scaleY = Vector2.Distance(vector2, vector15) / 26f;
        }

        if (self.slatedForDeletetion || self.room != rCam.room)
        {
            sLeaser.CleanSpritesAndRemove();
        }
    }

    // disable resetting logs
    private void RainWorld_Awake(ILContext il)
    {
        var c = new ILCursor(il);

        for (int i = 0; i < 2; i++)
        {
            ILLabel brto = null;
            if (c.TryGotoNext(x => x.MatchCall(typeof(File), "Exists"), x => x.MatchBrfalse(out brto)))
            {
                c.Index--;
                c.MoveAfterLabels();
                c.EmitDelegate(() => FlagTriggered);
                c.Emit(OpCodes.Brtrue, brto);
            }
        }
    }

    #region fixes

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
        sLeaser.sprites[1].color = UnityEngine.Color.white;

        if (self.requirement == MoreSlugcats.MoreSlugcatsEnums.GateRequirement.RoboLock)
        {
            for (int i = 2; i < 11; i++)
            {
                sLeaser.sprites[i].shader = FShader.defaultShader;
                sLeaser.sprites[i].color = UnityEngine.Color.white;
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

    #endregion fixes

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
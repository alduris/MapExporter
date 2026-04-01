using System;
using System.Collections.Generic;
using MonoMod.RuntimeDetour;
using MoreSlugcats;
using UnityEngine;
using Watcher;

namespace MapExporterNew.Hooks
{
    /// <summary>
    /// Fixes the appearance of some things
    /// </summary>
    internal static class DisplayHooks
    {
        public static void Apply()
        {
            On.Lightning.LightningSource.Update += NoLightningFlash;
            On.RoomCamera.DrawUpdate += NoShortcutBlink;
            On.Room.ScreenMovement += NoScreenShake;
            On.AntiGravity.BrokenAntiGravity.ctor += AntiBrokenGravity;
            On.GateKarmaGlyph.DrawSprites += MoreVisibleGateKarma;
            On.VoidSpawnGraphics.DrawSprites += AntiVoidSpawn;
            On.Room.Loaded += EffectsBlacklistAndSeeding;
            On.Room.LoadFromDataString += RockAndSpearSeeding;
            On.Watcher.Prince.Update += HidePrince;
            On.Watcher.RippleDepths.SpawnRippleVisions += ForceSpawnRippleVisions;
            On.Watcher.PearlContent.Update += PearlContentSetToMiddle;
            _ = new Hook(typeof(RegionState.RippleSpawnEggState).GetProperty(nameof(RegionState.RippleSpawnEggState.percentEggsCollected)).GetGetMethod(), RippleSpawnEggState_percentEggsCollected_get);
        }

        private static void RockAndSpearSeeding(On.Room.orig_LoadFromDataString orig, Room self, string[] lines)
        {
            int roomIndex = self.abstractRoom.index - self.abstractRoom.world.firstRoomIndex;
            UnityEngine.Random.State state = UnityEngine.Random.state;
            orig(self, lines);
            UnityEngine.Random.state = state;
        }

        private static float RippleSpawnEggState_percentEggsCollected_get(Func<RegionState.RippleSpawnEggState, float> orig, RegionState.RippleSpawnEggState self)
        {
            return self.totalEggs > 0 ? 1f : 0f;
        }

        private static void NoLightningFlash(On.Lightning.LightningSource.orig_Update orig, Lightning.LightningSource self)
        {
            // you're code also bad
            self.wait = int.MaxValue;
            self.intensity = 0;
            self.lastIntensity = 0;
            orig(self);
        }

        private static void NoShortcutBlink(On.RoomCamera.orig_DrawUpdate orig, RoomCamera self, float timeStacker, float timeSpeed)
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

        private static void NoScreenShake(On.Room.orig_ScreenMovement orig, Room self, Vector2? pos, Vector2 bump, float shake)
        {
            return;
        }

        private static void AntiBrokenGravity(On.AntiGravity.BrokenAntiGravity.orig_ctor orig, AntiGravity.BrokenAntiGravity self, int cycleMin, int cycleMax, RainWorldGame game)
        {
            // No gravity switching please!
            orig(self, cycleMin, cycleMax, game);
            self.on = false;
            self.from = self.on ? 1f : 0f;
            self.to = self.on ? 1f : 0f;
            self.lights = self.to;
            self.counter = 40000;
        }

        private static void MoreVisibleGateKarma(On.GateKarmaGlyph.orig_DrawSprites orig, GateKarmaGlyph self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);

            sLeaser.sprites[1].shader = FShader.defaultShader;
            //sLeaser.sprites[1].color = Color.white;

            if (self.requirement == MoreSlugcatsEnums.GateRequirement.RoboLock)
            {
                for (int i = 2; i < 11; i++)
                {
                    sLeaser.sprites[i].shader = FShader.defaultShader;
                    //sLeaser.sprites[i].color = Color.white;
                }
            }
        }

        private static void AntiVoidSpawn(On.VoidSpawnGraphics.orig_DrawSprites orig, VoidSpawnGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            //youre code bad
            for (int i = 0; i < sLeaser.sprites.Length; i++)
            {
                sLeaser.sprites[i].isVisible = false;
            }
        }

        private static void EffectsBlacklistAndSeeding(On.Room.orig_Loaded orig, Room self)
        {
            for (int i = self.roomSettings.effects.Count - 1; i >= 0; i--)
            {
                if (self.roomSettings.effects[i].type == RoomSettings.RoomEffect.Type.VoidSea) self.roomSettings.effects.RemoveAt(i); // breaks with no player
                else if (self.roomSettings.effects[i].type == RoomSettings.RoomEffect.Type.Lightning) self.roomSettings.effects.RemoveAt(i); // bad for screenies
                else if (self.roomSettings.effects[i].type.ToString() == "CGCameraZoom") self.roomSettings.effects.RemoveAt(i); // bad for screenies
            }
            List<PlacedObject> reactivateLater = [];
            foreach (var item in self.roomSettings.placedObjects)
            {
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
                    || (ModManager.DLCShared && (
                        item.type == DLCSharedEnums.PlacedObjectType.BigJellyFish
                        || item.type == DLCSharedEnums.PlacedObjectType.GlowWeed
                        || item.type == DLCSharedEnums.PlacedObjectType.GooieDuck
                        || item.type == DLCSharedEnums.PlacedObjectType.RotFlyPaper
                        || item.type == DLCSharedEnums.PlacedObjectType.LillyPuck
                        || item.type == DLCSharedEnums.PlacedObjectType.Stowaway
                    )
                    || (ModManager.MSC &&
                        item.type == MoreSlugcatsEnums.PlacedObjectType.DevToken
                        || item.type == MoreSlugcatsEnums.PlacedObjectType.MoonCloak
                        || item.type == MoreSlugcatsEnums.PlacedObjectType.HRGuard
                    ))
                )
                    self.waitToEnterAfterFullyLoaded = Mathf.Max(self.waitToEnterAfterFullyLoaded, 20);

                if (item.type == PlacedObject.Type.Pomegranate 
                    || item.type == PlacedObject.Type.PomegranateVine 
                    || item.type == PlacedObject.Type.RippleStalk 
                    || item.type == PlacedObject.Type.RippleTree)
                    self.waitToEnterAfterFullyLoaded = Mathf.Max(self.waitToEnterAfterFullyLoaded, 40);

                // Fuck you
                if (item.active)
                {
                    if (ModManager.DLCShared && item.type == DLCSharedEnums.PlacedObjectType.Stowaway)
                    {
                        item.active = false;
                        reactivateLater.Add(item);
                    }
                }

            }

            // Seed for rocks and spears so we don't get random changes in git diffs
            int roomIndex = self.abstractRoom.index - self.abstractRoom.world.firstRoomIndex;
            UnityEngine.Random.State state = UnityEngine.Random.state;
            UnityEngine.Random.InitState(roomIndex);
            orig(self);
            UnityEngine.Random.state = state;

            foreach (var item in reactivateLater)
            {
                item.active = true;
            }
        }

        private static void HidePrince(On.Watcher.Prince.orig_Update orig, Prince self, bool eu)
        {
            if (!self.slatedForDeletetion && !Preferences.ShowPrince.GetValue())
            {
                self.Destroy();
                return;
            }
            orig(self, eu);
        }

        private static void ForceSpawnRippleVisions(On.Watcher.RippleDepths.orig_SpawnRippleVisions orig, RippleDepths self)
        {
            self.rippleVisionsCooldown.Finish();
            orig(self);
        }

        private static void PearlContentSetToMiddle(On.Watcher.PearlContent.orig_Update orig, PearlContent self, bool eu)
        {
            self.life.Set(self.life.max / 2);
            orig(self, eu);
        }
    }
}

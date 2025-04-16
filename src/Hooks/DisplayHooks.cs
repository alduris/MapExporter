using System;
using System.Collections.Generic;
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
            On.Room.Loaded += EffectsBlacklist;
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

        private static void EffectsBlacklist(On.Room.orig_Loaded orig, Room self)
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

                if (ModManager.Watcher && (item.type == PlacedObject.Type.Pomegranate || item.type == PlacedObject.Type.PomegranateVine))
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

            orig(self);

            foreach (var item in reactivateLater)
            {
                item.active = true;
            }
        }
    }
}

using System;
using MoreSlugcats;
using UnityEngine;

namespace MapExporterNew.Hooks
{
    /// <summary>
    /// Fixes a lot of crashes
    /// </summary>
    internal static class CrashFixHooks
    {
        public static void Apply()
        {
            On.ScavengersWorldAI.WorldFloodFiller.Update += IgnoreScavAIComplaints;
            On.SeedCob.FreezingPaletteUpdate += SeedCob_FreezingPaletteUpdate;
            On.MoreSlugcats.BlinkingFlower.DrawSprites += BlinkingFlower_DrawSprites;
            On.RoomCamera.ApplyEffectColorsToPaletteTexture += EffectColorOOBFix;
            On.Watcher.WatcherRoomSpecificScript.WRSA_J01.UpdateObjects += WRSA_J01_UpdateObjects;
        }

        private static void IgnoreScavAIComplaints(On.ScavengersWorldAI.WorldFloodFiller.orig_Update orig, ScavengersWorldAI.WorldFloodFiller self)
        {
            // ignores broken world connections that cause crashes sometimes
            self.finished = true;
        }

        private static void SeedCob_FreezingPaletteUpdate(On.SeedCob.orig_FreezingPaletteUpdate orig, SeedCob self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            if (self.room != null)
            {
                orig(self, sLeaser, rCam);
            }
        }

        private static void BlinkingFlower_DrawSprites(On.MoreSlugcats.BlinkingFlower.orig_DrawSprites orig, BlinkingFlower self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
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

        private static void EffectColorOOBFix(On.RoomCamera.orig_ApplyEffectColorsToPaletteTexture orig, RoomCamera self, ref Texture2D texture, int color1, int color2)
        {
            color1 = Math.Min(color1, 21);
            color2 = Math.Min(color2, 21);
            orig(self, ref texture, color1, color2);
        }

        private static void WRSA_J01_UpdateObjects(On.Watcher.WatcherRoomSpecificScript.WRSA_J01.orig_UpdateObjects orig, Watcher.WatcherRoomSpecificScript.WRSA_J01 self)
        {
            // No follow camera object check, just turn on the eyes for hint purposes
            self.leftEye.active = true;
            self.rightEye.active = true;
            self.centerEye1.active = true;
        }
    }
}

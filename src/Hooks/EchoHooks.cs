using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using UnityEngine;

namespace MapExporterNew.Hooks
{
    /// <summary>
    /// Deals with showing echoes and preventing them from killing map exporter in other ways
    /// </summary>
    internal static class EchoHooks
    {
        public static void Apply()
        {
            On.World.SpawnGhost += SpawnGhost;
            On.GhostWorldPresence.SpawnGhost += SpawnGhost2;
            IL.World.SpawnGhost += ModdedGhostFix;

            On.GhostWorldPresence.GhostMode_AbstractRoom_Vector2 += PreventGhostEffect;
            On.Ghost.Update += PreventGhostFade;
        }

        // whether or not to show ghosts
        private static void SpawnGhost(On.World.orig_SpawnGhost orig, World self)
        {
            // true by default
            if (Preferences.ShowGhosts.GetValue())
            {
                self.game.rainWorld.safariMode = false;
                orig(self);
                self.game.rainWorld.safariMode = true;
            }
        }

        // ALSO whether or not to show ghosts
        private static bool SpawnGhost2(On.GhostWorldPresence.orig_SpawnGhost orig, GhostWorldPresence.GhostID ghostID, int karma, int karmaCap, int ghostPreviouslyEncountered, bool playingAsRed)
        {
            throw new NotImplementedException();
        }

        // fix for modded ghosts
        private static void ModdedGhostFix(ILContext il)
        {
            try
            {
                var c = new ILCursor(il);

                c.GotoNext(MoveType.AfterLabel, x => x.MatchStloc(2));
                c.Emit(OpCodes.Pop);
                c.EmitDelegate(Preferences.ShowGhosts.GetValue); // force to value
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError("Cannot IL Hook World.SpawnGhost!");
                Plugin.Logger.LogError(e);
            }
        }

        // Prevents ghost effect from appearing in nearby rooms
        private static float PreventGhostEffect(On.GhostWorldPresence.orig_GhostMode_AbstractRoom_Vector2 orig, GhostWorldPresence self, AbstractRoom testRoom, Vector2 worldPos)
        {
            if (self.ghostRoom.name != testRoom.name)
            {
                return 0f;
            }
            return orig(self, testRoom, worldPos);
        }

        // Prevents ghosts from flinging us to karma screen
        private static void PreventGhostFade(On.Ghost.orig_Update orig, Ghost self, bool eu)
        {
            orig(self, eu);
            self.fadeOut = self.lastFadeOut = 0f;
        }
    }
}

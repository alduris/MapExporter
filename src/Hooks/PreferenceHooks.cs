using System;

namespace MapExporterNew.Hooks
{
    /// <summary>
    /// Optional things to display
    /// </summary>
    internal static class PreferenceHooks
    {
        public static void Apply()
        {
            On.InsectCoordinator.CreateInsect += InsectCoordinator_CreateInsect;
            On.TempleGuard.Update += TempleGuard_Update;
            On.Oracle.Update += Oracle_Update;

            // There's also some preference hooks in EchoHooks
        }

        private static void InsectCoordinator_CreateInsect(On.InsectCoordinator.orig_CreateInsect orig, InsectCoordinator self, CosmeticInsect.Type type, UnityEngine.Vector2 pos, InsectCoordinator.Swarm swarm)
        {
            // Only call orig if user wants to spawn insects
            bool value = Preferences.ShowInsects.GetValue();

            if (value)
            {
                orig(self, type, pos, swarm);
            }
        }

        private static void TempleGuard_Update(On.TempleGuard.orig_Update orig, TempleGuard self, bool eu)
        {
            // Only call orig if user wants to spawn iteratosr
            orig(self, eu);
            bool value = Preferences.ShowGuardians.GetValue();
            if (!value)
            {
                self.Destroy();
                self.RemoveGraphicsModule();
                self.RemoveFromRoom();
            }
        }

        private static void Oracle_Update(On.Oracle.orig_Update orig, Oracle self, bool eu)
        {
            // Only call orig if user wants to spawn iterators
            orig(self, eu);
            bool value = Preferences.ShowOracles.GetValue();
            if (!value)
            {
                self.Destroy();
                self.RemoveFromRoom();
            }
        }
    }
}

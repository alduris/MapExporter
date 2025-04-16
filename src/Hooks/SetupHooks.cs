using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapExporterNew.Hooks
{
    internal static class SetupHooks
    {
        public static void Apply()
        {
            On.ProcessManager.CreateValidationLabel += NoValidationLabel;
            On.Menu.DialogBoxNotify.Update += SkipModUpdate;
            On.RainWorld.LoadSetupValues += LoadSetupValues;

            // Assume safari
            On.RoomRealizer.RemoveNotVisitedRooms += SafariRoomRemoval;
            On.SaveState.setDenPosition += SafariDenPosition;
            On.PlayerProgression.GetOrInitiateSaveState += SafariSaveState;
        }

        private static void NoValidationLabel(On.ProcessManager.orig_CreateValidationLabel orig, ProcessManager self)
        {
            // No validation label while screenshotting. Womp womp.
            self.validationLabel = null;
        }

        private static void SkipModUpdate(On.Menu.DialogBoxNotify.orig_Update orig, Menu.DialogBoxNotify self)
        {
            orig(self);
            if (self.continueButton.signalText == "REAPPLY")
            {
                self.continueButton.Clicked();
            }
        }

        private static RainWorldGame.SetupValues LoadSetupValues(On.RainWorld.orig_LoadSetupValues orig, bool distributionBuild)
        {
            var setup = orig(false);

            setup.loadAllAmbientSounds = false;
            setup.playMusic = false;

            setup.cycleTimeMax = 10000;
            setup.cycleTimeMin = 10000;
            setup.disableRain = true;

            setup.gravityFlickerCycleMin = 10000;
            setup.gravityFlickerCycleMax = 10000;

            setup.startScreen = false;
            setup.cycleStartUp = false;

            setup.player1 = false;
            setup.worldCreaturesSpawn = Preferences.ShowCreatures.GetValue();
            setup.singlePlayerChar = 0;

            return setup;
        }

        private static void SafariRoomRemoval(On.RoomRealizer.orig_RemoveNotVisitedRooms orig, RoomRealizer self)
        {
            // no call orig to act like safari
        }

        private static void SafariDenPosition(On.SaveState.orig_setDenPosition orig, SaveState self)
        {
            // Assume safari
            self.SetDenPositionForSafari();
        }

        private static SaveState SafariSaveState(On.PlayerProgression.orig_GetOrInitiateSaveState orig, PlayerProgression self, SlugcatStats.Name saveStateNumber, RainWorldGame game, ProcessManager.MenuSetup setup, bool saveAsDeathOrQuit)
        {
            // Assume safari
            return orig(self, saveStateNumber, game, setup, false);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Logging;
using MapExporterNew.Hooks;
using MapExporterNew.Screenshotter;
using MapExporterNew.Server;
using MapExporterNew.Tabs.UI;
using Menu.Remix.MixedUI;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MoreSlugcats;
using UnityEngine;
using Random = UnityEngine.Random;

#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete

namespace MapExporterNew;

[BepInPlugin(MOD_ID, "Map Exporter", "1.0.0")]
sealed class Plugin : BaseUnityPlugin
{
    // Config
    public const string MOD_ID = "mapexporter";
    public const string FLAG_TRIGGER = "--mapexport";

    public static bool FlagTriggered => Environment.GetCommandLineArgs().Contains(FLAG_TRIGGER);

    public static Plugin Instance;
    public static new ManualLogSource Logger;

    public void OnEnable()
    {
        try
        {
            Instance = this;
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
                SetupHooks.Apply();
                EchoHooks.Apply();
                ZeroingHooks.Apply();
                DisplayHooks.Apply();
                CrashFixHooks.Apply();
                PreferenceHooks.Apply();
                LoadingHooks.Apply();

                // The hooks that make the world go 'round (main functionality)
                On.RainWorldGame.ctor += RainWorldGame_ctor;
                On.RainWorldGame.Update += RainWorldGame_Update;

                Logger.LogDebug("Finished start thingy");
            }
            else
            {
                // Register options thing
                Logger.LogDebug("Normal game instance, don't run hooks");

                // Register UI
                bool isInit = false;
                On.RainWorld.OnModsInit += (orig, self) =>
                {
                    orig(self);
                    if (isInit) return;

                    isInit = true;
                    Data.CheckData();
                    Exporter.ResetFileCounter();

                    MachineConnector.SetRegisteredOI(MOD_ID, new UI());

                    static float OpScrollBox_MaxScroll_get(Func<OpScrollBox, float> orig, OpScrollBox self) => self.horizontal ? -Mathf.Max(self.contentSize - self.size.x, 0f) : orig(self);
                    _ = new Hook(typeof(OpScrollBox).GetProperty(nameof(OpScrollBox.MaxScroll)).GetGetMethod(), OpScrollBox_MaxScroll_get);
                    On.Menu.Remix.MixedUI.OpTab._RemoveItem += (orig, self, element) =>
                    {
                        orig(self, element);
                        if (element is OpProgressBar progressBar && progressBar.inner != null) orig(self, progressBar.inner);
                    };

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


    // disable resetting logs
    private void RainWorld_Awake(ILContext il)
    {
        var c = new ILCursor(il);

        for (int i = 0; i < 2; i++)
        {
            if (c.TryGotoNext(MoveType.After, x => x.MatchCall(typeof(File), nameof(File.Exists))))
            {
                c.EmitDelegate(() => !FlagTriggered);
                c.Emit(OpCodes.And);
            }
        }
    }

    // JSON saving
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


    // Runs half-synchronously to the game loop, bless iters
    private System.Collections.IEnumerator captureTask;
    public ErrorPopup errorPopup;
    public static Queue<ErrorInfo> errorQueue = [];

    // start
    private void RainWorldGame_ctor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
    {
        // Use safari mode, it's very sanitary
        manager.rainWorld.safariMode = true;
        manager.rainWorld.safariRainDisable = true;
        manager.rainWorld.safariSlugcat = SlugcatStats.Name.White;
        manager.rainWorld.safariRegion = "SU";

        // Unfortunately safari mode also assumes we have MSC enabled so... just play pretend :3 (it'll throw exceptions otherwise)
        bool oldmsc = ModManager.MSC;
        ModManager.MSC = true;

        orig(self, manager);

        ModManager.MSC = oldmsc;
        RainWorld.lockGameTimer = true;

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
        self.GetStorySession.saveState.deathPersistentSaveData.altEnding = false;
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

        // watcher stuff yay
        self.GetStorySession.saveState.miscWorldSaveData.numberOfPrinceEncounters = 5; // display the prince
        self.GetStorySession.saveState.miscWorldSaveData.highestPrinceConversationSeen = 226;

        // allow Saint ghosts
        self.GetStorySession.saveState.cycleNumber = 1;

        Logger.LogDebug("Starting capture task");

        captureTask = new Capturer().CaptureTask(self);
    }

    private void RainWorldGame_Update(On.RainWorldGame.orig_Update orig, RainWorldGame self)
    {
        if (errorPopup != null)
        {
            errorPopup.Update();
            if (!errorPopup.active)
            {
                errorPopup = null;
            }
        }
        else if (errorQueue.Count > 0)
        {
            var info = errorQueue.Dequeue();
            errorPopup = new ErrorPopup(info.canContinue, info.title, info.message);
            if (info.canContinue && info.onContinue != null)
            {
                errorPopup.OnContinue += info.onContinue;
            }
        }
        else
        {
            try
            {
                orig(self);
                captureTask.MoveNext();
            }
            catch (Exception e)
            {
                Logger.LogError(e);
                throw;
            }
        }
    }
}

using System;
using System.Text.RegularExpressions;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace MapExporterNew.Hooks
{
    internal static class LoadingHooks
    {
        public static void Apply()
        {
            On.WorldLoader.FindingCreaturesThread += SaveCreatures;
            IL.WorldLoader.NextActivity += ForceReadSpawns;

            On.AbstractRoom.AddTag += AbstractRoomTagFix;
            IL.WorldLoader.CreatingAbstractRooms += CreatingAbstractRoomTagFix;
            IL.WorldLoader.MappingRooms += MappingRoomTagFix;

            On.WorldLoader.ctor_RainWorldGame_Name_bool_string_Region_SetupValues += ExclusiveRoomFix;
            On.OverWorld.WorldLoaded += SkipGateLoading;
        }

        private static void SaveCreatures(On.WorldLoader.orig_FindingCreaturesThread orig, WorldLoader self)
        {
            orig(self);
            RegionInfo.spawnerCWT.Add(self.world, self.spawners);
            if (!Preferences.ShowCreatures.GetValue())
            {
                self.spawners = [];
            }
        }

        private static void ForceReadSpawns(ILContext il)
        {
            // Make spawns be read, even though we remove them later
            try
            {
                var c = new ILCursor(il);

                c.GotoNext(x => x.MatchLdftn<WorldLoader>(nameof(WorldLoader.FindingCreaturesThread)));
                c.GotoPrev(x => x.MatchBrtrue(out _));
                c.EmitDelegate((bool _) => false);
                c.GotoPrev(x => x.MatchBrfalse(out _));
                c.EmitDelegate((bool _) => true);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError("WorldLoader NextActivity IL hook failed!");
                Plugin.Logger.LogError(e);
            }
        }

        private static void AbstractRoomTagFix(On.AbstractRoom.orig_AddTag orig, AbstractRoom self, string tg)
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

        private static void CreatingAbstractRoomTagFix(ILContext il)
        {
            // Add the tags, skipping the exceptions (SWARMROOM, GATE, SHELTER)
            try
            {
                var c = new ILCursor(il);

                // Find location where we want to go and grab it
                c.GotoNext(x => x.MatchLdstr("SWARMROOM"));
                c.GotoNext(MoveType.Before, x => x.MatchLdfld<WorldLoader>(nameof(WorldLoader.abstractRooms)));
                var match = c.Prev;

                // Go back to where we want the break to be
                c.GotoPrev(x => x.MatchLdstr("SWARMROOM"));
                c.GotoPrev(MoveType.After, x => x.MatchBrfalse(out _));

                // Create the break
                c.Emit(OpCodes.Br, match);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError("WorldLoader CreatingAbstractRooms IL hook failed!");
                Plugin.Logger.LogError(e);
            }
        }

        private static void MappingRoomTagFix(ILContext il)
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
                    Plugin.Logger.LogDebug(tag);
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
                Plugin.Logger.LogError("WorldLoader MappingRooms IL hook failed!");
                Plugin.Logger.LogError(e);
            }
        }

        private static void ExclusiveRoomFix(On.WorldLoader.orig_ctor_RainWorldGame_Name_bool_string_Region_SetupValues orig, WorldLoader self, RainWorldGame game, SlugcatStats.Name playerCharacter, bool singleRoomWorld, string worldName, Region region, RainWorldGame.SetupValues setupValues)
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

        private static void SkipGateLoading(On.OverWorld.orig_WorldLoaded orig, OverWorld self, bool warpUsed)
        {
            GC.Collect();
            return; // orig assumes a gate
        }
    }
}

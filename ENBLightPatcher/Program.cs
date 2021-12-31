using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using System.Threading.Tasks;
using System.IO;

namespace ENBLightPatcher
{
    public static class MyExtensions
    {
        public static bool ContainsInsensitive(this string str, string rhs)
        {
            return str.Contains(rhs, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class Program
    {
        public static Task<int> Main(string[] args)
        {
            return SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetTypicalOpen(GameRelease.SkyrimSE, "ENBLightPatcher.esp")
                .Run(args);
        }


        private static readonly string enbLightPluginNameWithExtension = "ENB Light.esp";

        private static readonly string[] lightNamesToAdjust = { "Candle", "Torch", "Camp" };

        private static readonly float fadeMultiplier = 0.5f;



        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            string textFile = Path.Combine(state.ExtraSettingsDataPath, "blacklist.txt");
            var text_count = 0;
            var blacklist_found = false;
            if (File.Exists(textFile))
            {
                blacklist_found = true;
                string[] lines_there = File.ReadAllLines(textFile);
                foreach (string line in lines_there)
                {
                    text_count++;
                }
            }

            ModKey[] blacklisted_mods = new ModKey[text_count];
            Console.WriteLine("*** DETECTED BLACKLIST ***");
            if (blacklist_found)
            {
                string[] lines = File.ReadAllLines(textFile);
                var idx = 0;
                foreach (string line in lines)
                {
                    Console.WriteLine("mod tomes allowed: text " + line);
                    ModKey entry = ModKey.FromNameAndExtension(line);
                    blacklisted_mods[idx] = entry;
                    idx++;
                }
                Console.WriteLine("*************************");

            }
            // Part 1 - Patch every placed light in worldspaces/cells
            foreach (var placedObjectGetter in state.LoadOrder.PriorityOrder.PlacedObject().WinningContextOverrides(state.LinkCache))
            {
                ModKey current_mod = placedObjectGetter.ModKey;
                if (current_mod == enbLightPluginNameWithExtension) continue;
                if (blacklisted_mods.Contains(current_mod)) continue;
                var placedObject = placedObjectGetter.Record;
                if (placedObject.LightData == null) continue;
                placedObject.Base.TryResolve<ILightGetter>(state.LinkCache, out var placedObjectBase);
                if (placedObjectBase == null || placedObjectBase.EditorID == null) continue;
                if (lightNamesToAdjust.Any(placedObjectBase.EditorID.ContainsInsensitive))
                {
                    if (placedObject != null && placedObject.LightData != null && placedObject.LightData.FadeOffset > 0)
                    {
                        IPlacedObject modifiedObject = placedObjectGetter.GetOrAddAsOverride(state.PatchMod);
                        modifiedObject.LightData!.FadeOffset *= fadeMultiplier;
                    }
                }
                else continue;
            }

            // Part 2 - Patch every LIGH record
            foreach (var lightGetter in state.LoadOrder.PriorityOrder.Light().WinningContextOverrides())
            {
                ModKey current_mod = lightGetter.ModKey;
                if (current_mod == enbLightPluginNameWithExtension) continue;
                if (blacklisted_mods.Contains(current_mod)) continue;
                var light = lightGetter.Record;
                if (light.EditorID == null) continue;
                if (lightNamesToAdjust.Any(light.EditorID.ContainsInsensitive))
                {
                    Light modifiedLight = state.PatchMod.Lights.GetOrAddAsOverride(light);
                    modifiedLight.FadeValue *= fadeMultiplier;
                }
            }
        }
    }
}

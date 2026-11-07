using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace LMUStoryLockFix
{
    // LethalMoonUnlocks (LMU) treats a moon as story-locked when it is hidden AND locked at the
    // moment LMU builds its moon list (LMUnlockable.RefreshStoryUnlockFromStartupState).
    // During a run, though, LMU hides and locks every moon you haven't discovered, and
    // LethalLevelLoader saves those flags. When LMU rebuilds its list later (after getting fired,
    // when you start a new save in the same session, or when you load a save that carries those
    // flags), it mistakes its own discovery hiding for story locks. Those moons then become
    // permanently undiscoverable, and a constellation/charter can end up offering only its
    // default moon.
    //
    // This plugin keeps a story lock only for moons on an allowlist: the config list plus any moon
    // LMU itself force-locks as a built-in story moon (Artifice, Embrion, optionally Galetry).
    // Any other inferred story lock is dropped, and the moon's "original" hidden/locked state is
    // reset so LMU doesn't write those flags back when it restores original state.
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(LmuGuid)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "Gezang.LMUStoryLockFix";
        public const string Name = "LMUStoryLockFix";
        public const string Version = "1.0.0";
        private const string LmuGuid = "com.xmods.lethalmoonunlocks";

        private static ManualLogSource Log;
        private static readonly HashSet<string> Allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> Reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static PropertyInfo nameProp;
        private static PropertyInfo storyUnlockProp;
        private static PropertyInfo originallyHiddenProp;
        private static PropertyInfo originallyLockedProp;

        private void Awake()
        {
            Log = Logger;

            string list = Config.Bind(
                "General",
                "StoryLockedMoons",
                "Embrion, Artifice, Galetry",
                "Comma-separated moon names (as LethalMoonUnlocks shows them, without the number) that may keep a story lock. " +
                "LethalMoonUnlocks' own built-in story moons are always allowed. Every other inferred story lock is removed. " +
                "Add a moon here if another mod is meant to hide and lock it until a story event unlocks it.").Value;
            foreach (string moon in list.Split(','))
            {
                if (moon.Trim().Length > 0) Allowed.Add(moon.Trim());
            }

            Type unlockable = AccessTools.TypeByName("LethalMoonUnlocks.LMUnlockable");
            if (unlockable == null)
            {
                Log.LogError("LethalMoonUnlocks.LMUnlockable not found; nothing patched.");
                return;
            }

            nameProp = AccessTools.Property(unlockable, "Name");
            storyUnlockProp = AccessTools.Property(unlockable, "StoryUnlock");
            originallyHiddenProp = AccessTools.Property(unlockable, "OriginallyHidden");
            originallyLockedProp = AccessTools.Property(unlockable, "OriginallyLocked");
            MethodInfo refresh = AccessTools.Method(unlockable, "RefreshStoryUnlockFromStartupState");
            MethodInfo forceLock = AccessTools.Method(unlockable, "ForceStoryLockAtStartup", Type.EmptyTypes);

            if (new object[] { nameProp, storyUnlockProp, originallyHiddenProp, originallyLockedProp, refresh, forceLock }.Any(m => m == null))
            {
                Log.LogError("LethalMoonUnlocks members not found (unsupported version?); nothing patched.");
                return;
            }

            var harmony = new Harmony(Guid);
            harmony.Patch(forceLock, prefix: new HarmonyMethod(typeof(Plugin), nameof(RememberBuiltInStoryMoon)));
            harmony.Patch(refresh, postfix: new HarmonyMethod(typeof(Plugin), nameof(DropInferredStoryLock)));
            Log.LogInfo($"Loaded. Story locks allowed for: {string.Join(", ", Allowed.ToArray())} (plus LMU built-ins).");
        }

        // LMU forcing one of its built-in story moons: always allow that moon.
        private static void RememberBuiltInStoryMoon(object __instance)
        {
            string name = nameProp.GetValue(__instance, null) as string;
            if (!string.IsNullOrEmpty(name) && Allowed.Add(name.Trim()))
            {
                Log.LogInfo($"{name}: allowed as a LethalMoonUnlocks built-in story moon.");
            }
        }

        private static void DropInferredStoryLock(object __instance)
        {
            if (!(bool)storyUnlockProp.GetValue(__instance, null)) return;

            string name = nameProp.GetValue(__instance, null) as string;
            if (string.IsNullOrEmpty(name) || Allowed.Contains(name.Trim())) return;

            originallyHiddenProp.SetValue(__instance, false, null);
            originallyLockedProp.SetValue(__instance, false, null);
            storyUnlockProp.SetValue(__instance, false, null);

            if (Reported.Add(name))
            {
                Log.LogInfo($"{name}: removed inferred story lock (it was hidden by moon discovery, not by a story).");
            }
        }
    }
}

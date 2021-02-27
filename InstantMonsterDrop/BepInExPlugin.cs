﻿using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace InstantMonsterDrop
{
    [BepInPlugin("aedenthorn.InstantMonsterDrop", "Instant Monster Drop", "0.1.1")]
    public class BepInExPlugin : BaseUnityPlugin
    {
        private static readonly bool isDebug = true;
        private static ConfigEntry<bool> modEnabled;
        private static ConfigEntry<int> nexusID;

        public static void Dbgl(string str = "", bool pref = true)
        {
            if (isDebug)
                Debug.Log((pref ? typeof(BepInExPlugin).Namespace + " " : "") + str);
        }
        private void Awake()
        {
            modEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod");
            nexusID = Config.Bind<int>("General", "NexusID", 164, "Mod ID on the Nexus for update checks");
            if (!modEnabled.Value)
                return;

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        [HarmonyPatch(typeof(Ragdoll), "Awake")]
        static class Ragdoll_Awake_Patch
        {
            static void Prefix(Ragdoll __instance)
            {
                __instance.m_ttl = 0f;
            }
        }
    }
}

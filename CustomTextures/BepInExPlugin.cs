﻿using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CustomTextures
{
    [BepInPlugin("aedenthorn.CustomTextures", "Custom Textures", "0.6.0")]
    public class BepInExPlugin: BaseUnityPlugin
    {
        private static readonly bool isDebug = true;

        public static ConfigEntry<float> m_range;
        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> dumpSceneTextures;
        public static ConfigEntry<string> hotKey;
        public static ConfigEntry<int> nexusID;
        public static Dictionary<string, Texture2D> customTextures = new Dictionary<string, Texture2D>();
        public static List<string> outputDump = new List<string>();

        public static void Dbgl(string str = "", bool pref = true)
        {
            if (isDebug)
                Debug.Log((pref ? typeof(BepInExPlugin).Namespace + " " : "") + str);
        }
        private void Awake()
        {
            modEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod");
            hotKey = Config.Bind<string>("General", "HotKey", "page down", "Key to reload textures");
            dumpSceneTextures = Config.Bind<bool>("General", "DumpSceneTextures", false, "Dump scene textures to BepInEx/plugins/CustomTextures/scene_dump.txt");
            nexusID = Config.Bind<int>("General", "NexusID", 48, "Nexus mod ID for updates");

            if (!modEnabled.Value)
                return;

            LoadCustomTextures();

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        private void Update()
        {
            if (ZNetScene.instance != null && Input.GetKey(hotKey.Value))
            {
                LoadCustomTextures();
                Dbgl($"Pressed reload key.");

                GameObject root = (GameObject)typeof(ZNetScene).GetField("m_netSceneRoot", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ZNetScene.instance);

                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

                List<GameObject> gos = new List<GameObject>();
                foreach (Transform t in transforms)
                {
                    if(t.parent == root.transform)
                        gos.Add(t.gameObject);
                }

                LoadSceneTextures(gos.ToArray());
                LoadSceneTextures(((Dictionary<int, GameObject>)typeof(ZNetScene).GetField("m_namedPrefabs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ZNetScene.instance )).Values.ToArray());

                foreach(Player player in Player.GetAllPlayers())
                {
                    VisEquipment ve = (VisEquipment)typeof(Humanoid).GetField("m_visEquipment", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player);
                    if(ve != null)
                    {
                        SetEquipmentTexture((string)typeof(VisEquipment).GetField("m_leftItem", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ve), (GameObject)typeof(VisEquipment).GetField("m_leftItemInstance", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ve));
                        SetEquipmentTexture((string)typeof(VisEquipment).GetField("m_rightItem", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ve), (GameObject)typeof(VisEquipment).GetField("m_rightItemInstance", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ve));
                        SetEquipmentTexture((string)typeof(VisEquipment).GetField("m_helmetItem", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ve), (GameObject)typeof(VisEquipment).GetField("m_helmetItemInstance", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ve));
                        SetEquipmentTexture((string)typeof(VisEquipment).GetField("m_leftBackItem", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ve), (GameObject)typeof(VisEquipment).GetField("m_leftBackItemInstance", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ve));
                        SetEquipmentTexture((string)typeof(VisEquipment).GetField("m_rightBackItem", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ve), (GameObject)typeof(VisEquipment).GetField("m_rightBackItemInstance", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ve));
                        SetEquipmentListTexture((string)typeof(VisEquipment).GetField("m_shoulderItem", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ve), (List<GameObject>)typeof(VisEquipment).GetField("m_shoulderItemInstances", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ve));
                        SetEquipmentListTexture((string)typeof(VisEquipment).GetField("m_utilityItem", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ve), (List<GameObject>)typeof(VisEquipment).GetField("m_utilityItemInstances", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ve));
                        SetBodyEquipmentTexture((string)typeof(VisEquipment).GetField("m_legItem", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ve), ve.m_bodyModel, (List<GameObject>)typeof(VisEquipment).GetField("m_legItemInstances", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ve), "_Legs");
                        SetBodyEquipmentTexture((string)typeof(VisEquipment).GetField("m_chestItem", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ve), ve.m_bodyModel, (List<GameObject>)typeof(VisEquipment).GetField("m_chestItemInstances", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ve), "_Chest");
                    }
                }

            }

        }


        private static void LoadCustomTextures()
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"CustomTextures");

            if (!Directory.Exists(path))
            {
                Dbgl($"Directory {path} does not exist! Creating.");
                Directory.CreateDirectory(path);
                return;
            }

            customTextures.Clear();

            foreach (string file in Directory.GetFiles(path, "*.png", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileName(file);
                Dbgl($"adding {fileName} custom texture.");

                string id = fileName.Substring(0, fileName.Length - 4);
                Texture2D tex = new Texture2D(2, 2);
                byte[] imageData = File.ReadAllBytes(file);
                tex.LoadImage(imageData);
                customTextures[id] = tex;
            }
        }

        private static void LoadSceneTextures(GameObject[] gos)
        {

            Dbgl($"loading {gos.Length} scene textures");
            outputDump.Clear();

            foreach (GameObject gameObject in gos)
            {
                
                if (gameObject.name == "_NetSceneRoot")
                    continue;

                LoadOneTexture(gameObject, gameObject.name, "object");

            }
            GameObject root = (GameObject)typeof(ZNetScene).GetField("m_netSceneRoot", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ZNetScene.instance);
            foreach (Terrain ter in root.GetComponentsInChildren<Terrain>(true))
            {
                Dbgl($"terrain name: {ter.name}, layers  {ter.terrainData.terrainLayers.Length}");
                foreach (TerrainLayer tl in ter.terrainData.terrainLayers)
                {
                    Dbgl($"layer name: {tl.name}, d {tl.diffuseTexture},  {tl.maskMapTexture}, n {tl.normalMapTexture}");
                }

            }
            if (dumpSceneTextures.Value)
            {
                string path = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\\CustomTextures\\scene_dump.txt";
                File.WriteAllLines(path, outputDump);
            }
        }

        private static void LoadOneTexture(GameObject gameObject, string thingName, string prefix)
        {
            List<string> logDump = new List<string>();
            try
            {
                //Dbgl($"loading textures for { gameObject.name}");
                MeshRenderer[] mrs = gameObject.GetComponentsInChildren<MeshRenderer>(true);
                SkinnedMeshRenderer[] smrs = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);

                if (mrs?.Any() == true)
                {
                    outputDump.Add($"{prefix} {thingName} has {mrs.Length} MeshRenderers:");
                    foreach (MeshRenderer mr in mrs)
                    {
                        if (mr == null)
                        {
                            outputDump.Add($"\tnull");
                            continue;
                        }
                        foreach (Material m in mr.materials)
                        {
                            if (!m.HasProperty("_MainTex"))
                                continue;

                            outputDump.Add($"\t{mr.name}: {mr.material.mainTexture.name}");
                            string name = m.GetTexture("_MainTex").name;

                            if (customTextures.ContainsKey($"{prefix}_{thingName}_texture"))
                            {
                                logDump.Add($"{prefix} {thingName}, MeshRenderer {mr.name}, material {m.name}, texture {name}, using {prefix}_{thingName}_texture custom texture.");
                                m.mainTexture = customTextures[$"{prefix}_{thingName}_texture"];
                                m.mainTexture.name = name;
                                m.color = Color.white;
                            }
                            else if (customTextures.ContainsKey($"texture_{name}_texture"))
                            {
                                logDump.Add($"object {thingName}, MeshRenderer {mr.name}, material {m.name}, texture {name}, using texture_{name}_texture custom texture.");
                                m.mainTexture = customTextures[$"texture_{name}_texture"];
                                m.mainTexture.name = name;
                                m.color = Color.white;
                            }
                        }
                    }
                }

                if (smrs?.Any() == true)
                {
                    outputDump.Add($"{prefix} {thingName} has {smrs.Length} SkinnedMeshRenderers:");
                    foreach (SkinnedMeshRenderer smr in smrs)
                    {
                        if (smr == null)
                        {
                            outputDump.Add($"\tnull");
                            continue;
                        }
                        foreach (Material m in smr.materials)
                        {

                            outputDump.Add($"\t{prefix}: {thingName}, smr: {smr.name}, mat: {smr.material.name},  texture: {smr.material.mainTexture?.name}");
                            string name = smr.material.mainTexture?.name;
                            if (name == null)
                                name = thingName;

                            if (customTextures.ContainsKey($"{prefix}_{thingName}_texture"))
                            {
                                logDump.Add($"{prefix} {thingName}, SkinnedMeshRenderer {smr.name}, material {m.name}, texture {name}, using {prefix}_{thingName}_texture custom texture.");
                                m.mainTexture = customTextures[$"{prefix}_{thingName}_texture"];
                                m.mainTexture.name = name;
                                m.color = Color.white;
                            }
                            else if (customTextures.ContainsKey($"texture_{name}_texture"))
                            {
                                logDump.Add($"{prefix} {thingName}, SkinnedMeshRenderer {smr.name}, material {m.name}, texture {name}, using texture_{name}_texture custom texture.");
                                m.mainTexture = customTextures[$"texture_{name}_texture"];
                                m.mainTexture.name = name;
                                m.color = Color.white;

                            }
                        }
                    }
                }
            }
            catch
            {
                //Dbgl($"Error checking texture in {gameObject.name}:\n\n{ex}");
            }
            if (logDump.Any())
                Dbgl(string.Join("\n", logDump));
        }

        private static void SetEquipmentTexture(string itemName, GameObject item)
        {
            if (item != null && itemName != null && itemName.Length > 0)
            {
                LoadOneTexture(item.gameObject, itemName, "item");
            }
        }

        private static void SetEquipmentListTexture(string itemName, List<GameObject> items)
        {
            if (items != null && items.Any() && itemName != null && itemName.Length > 0)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] == null)
                        continue;
                    SetEquipmentTexture(itemName, items[i]);

                }
            }
        }

        private static void SetBodyEquipmentTexture(string itemName, SkinnedMeshRenderer smr, List<GameObject> itemInstances, string which)
        {

            if (itemName != null && itemName.Length > 0)
            {
                foreach(GameObject go in itemInstances)
                {
                    /*
                    Dbgl($"body equipment {which} gameObject: {go.name}");
                    foreach(SkinnedMeshRenderer s in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        Dbgl($"body equipment {which} smr: {s.name}");
                    }
                    foreach(MeshRenderer s in go.GetComponentsInChildren<MeshRenderer>())
                    {
                        Dbgl($"body equipment {which} mr: {s.name}");
                    }
                    foreach(Material m in go.GetComponentsInChildren<Material>())
                    {
                        Dbgl($"body equipment {which} material: {m.name}");
                    }
                    foreach(Texture2D t in go.GetComponentsInChildren<Texture2D>())
                    {
                        Dbgl($"body equipment {which} texture: {t.name}");
                    }
                    outputDump.Clear();*/
                    LoadOneTexture(go, itemName, "item");
                    //Dbgl(string.Join("\n", outputDump));
                }

                Dbgl($"{which} item: {itemName}");
                if (customTextures.ContainsKey($"item_{itemName}_texture"))
                {
                    Dbgl($"setting custom texture for item {itemName}: item_{itemName}_texture.png");
                    smr.material.SetTexture($"{which}Tex", customTextures[$"item_{itemName}_texture"]);
                    smr.material.color = Color.white;

                }
                else
                    Dbgl($"item {itemName}, texture name {smr.material.mainTexture.name}; use item_{itemName}_texture.png");

                if (customTextures.ContainsKey($"item_{itemName}_bump"))
                {
                    Dbgl($"setting custom texture for item {itemName}: item_{itemName}_bump.png");
                    smr.material.SetTexture($"{which}BumpMap", customTextures[$"item_{itemName}_bump"]);
                }
                if (customTextures.ContainsKey($"item_{itemName}_metal"))
                {
                    Dbgl($"setting custom texture for item {itemName}: item_{itemName}_metal.png");
                    smr.material.SetTexture($"{which}Metal", customTextures[$"item_{itemName}_metal"]);
                }
            }

        }


        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        static class ZNetScene_Awake_Patch
        {
            static void Postfix(Dictionary<int, GameObject> ___m_namedPrefabs)
            {
                LoadSceneTextures(___m_namedPrefabs.Values.ToArray());
            }

        }
        
        [HarmonyPatch(typeof(VisEquipment), "Awake")]
        static class VisEquipment_Awake_Patch
        {
            static void Postfix(VisEquipment __instance)
            {
                for(int i = 0; i < __instance.m_models.Length; i++)
                {
                    if (customTextures.ContainsKey($"player_model_{i}_texture"))
                    {
                        __instance.m_models[i].m_baseMaterial.SetTexture("_MainTex", customTextures[$"player_model_{i}_texture"]);
                        Dbgl($"set player_model_{i}_texture custom texture.");
                    }
                    if (customTextures.ContainsKey($"player_model_{i}_bump"))
                    {
                        __instance.m_models[i].m_baseMaterial.SetTexture("_SkinBumpMap", customTextures[$"player_model_{i}_bump"]);
                        Dbgl($"set player_model_{i}_bump custom skin bump map.");
                    }
                }
            }
        }


        [HarmonyPatch(typeof(VisEquipment), "SetLeftHandEquiped")]
        static class VisEquipment_SetLeftHandEquiped_Patch
        {
            static void Postfix(bool __result, string ___m_leftItem, GameObject ___m_leftItemInstance)
            {
                if (!__result)
                    return;

                SetEquipmentTexture(___m_leftItem, ___m_leftItemInstance);
            }
        }

        [HarmonyPatch(typeof(VisEquipment), "SetRightHandEquiped")]
        static class VisEquipment_SetRightHandEquiped_Patch
        {
            static void Postfix(bool __result, string ___m_rightItem, GameObject ___m_rightItemInstance)
            {
                if (!__result)
                    return;

                SetEquipmentTexture(___m_rightItem, ___m_rightItemInstance);
            }
        }

        [HarmonyPatch(typeof(VisEquipment), "SetHelmetEquiped")]
        static class VisEquipment_SetHelmetEquiped_Patch
        {
            static void Postfix(bool __result, string ___m_helmetItem, GameObject ___m_helmetItemInstance)
            {
                if (!__result)
                    return;

                SetEquipmentTexture(___m_helmetItem, ___m_helmetItemInstance);
            }
        }
        
        [HarmonyPatch(typeof(VisEquipment), "SetBackEquiped")]
        static class VisEquipment_SetBackEquiped_Patch
        {
            static void Postfix(bool __result, string ___m_leftBackItem, GameObject ___m_leftBackItemInstance, string ___m_rightBackItem, GameObject ___m_rightBackItemInstance)
            {
                if (!__result)
                    return;

                SetEquipmentTexture(___m_leftBackItem, ___m_leftBackItemInstance);
                SetEquipmentTexture(___m_rightBackItem, ___m_rightBackItemInstance);


            }
        }

        [HarmonyPatch(typeof(VisEquipment), "SetShoulderEquiped")]
        static class VisEquipment_SetShoulderEquiped_Patch
        {
            static void Postfix(bool __result, string ___m_shoulderItem, List<GameObject> ___m_shoulderItemInstances)
            {
                if (!__result)
                    return;

                SetEquipmentListTexture(___m_shoulderItem, ___m_shoulderItemInstances);

            }
        }

        [HarmonyPatch(typeof(VisEquipment), "SetUtilityEquiped")]
        static class VisEquipment_SetUtilityEquiped_Patch
        {
            static void Postfix(bool __result, string ___m_utilityItem, List<GameObject> ___m_utilityItemInstances)
            {
                if (!__result)
                    return;

                SetEquipmentListTexture(___m_utilityItem, ___m_utilityItemInstances);
            }
        }

        [HarmonyPatch(typeof(VisEquipment), "SetLegEquiped")]
        static class VisEquipment_SetLegEquiped_Patch
        {
            static void Postfix(bool __result, string ___m_legItem, SkinnedMeshRenderer ___m_bodyModel, List<GameObject> ___m_legItemInstances)
            {
                if (!__result)
                    return;

                SetBodyEquipmentTexture(___m_legItem, ___m_bodyModel, ___m_legItemInstances, "_Legs");
            }
        }

        [HarmonyPatch(typeof(VisEquipment), "SetChestEquiped")]
        static class VisEquipment_SetChestEquiped_Patch
        {
            static void Postfix(bool __result, string ___m_chestItem, SkinnedMeshRenderer ___m_bodyModel, List<GameObject> ___m_chestItemInstances)
            {
                if (!__result)
                    return;

                SetBodyEquipmentTexture(___m_chestItem, ___m_bodyModel, ___m_chestItemInstances, "_Chest");

            }
        }

    }
}

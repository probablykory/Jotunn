using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

namespace Jotunn.Utils
{
    /// <summary>
    ///     Utility class to query metadata about added content of any loaded mod, including non-Jötunn ones.
    ///     It is disabled by default, as it unnecessary increases the loading time when not used.<br/>
    ///     <see cref="ModQuery.Enable()"/> has to be called anytime before FejdStartup.Awake, meaning in your plugin's Awake or Start.
    /// </summary>
    public class ModQuery
    {
        private static readonly Dictionary<string, Dictionary<int, ModPrefab>> Prefabs = new Dictionary<string, Dictionary<int, ModPrefab>>();
        private static readonly Dictionary<string, List<Recipe>> Recipes = new Dictionary<string, List<Recipe>>();

        private static readonly HashSet<MethodInfo> PatchedMethods = new HashSet<MethodInfo>();
        private static readonly HarmonyMethod PrePatch = new HarmonyMethod(AccessTools.Method(typeof(ModQuery), nameof(BeforePatch)));
        private static readonly HarmonyMethod PostPatch = new HarmonyMethod(AccessTools.Method(typeof(ModQuery), nameof(AfterPatch)));

        private static bool enabled = false;

        internal static void Init()
        {
            Main.LogInit("ModQuery");
            Main.Harmony.PatchAll(typeof(ModQuery));
        }

        private class ModPrefab : IModPrefab
        {
            public GameObject Prefab { get; }
            public BepInPlugin SourceMod { get; }

            public ModPrefab(GameObject prefab, BepInPlugin mod)
            {
                Prefab = prefab;
                SourceMod = mod;
            }
        }

        /// <summary>
        ///     Enables the collection of mod metadata.
        ///     It is disabled by default, as it unnecessary increases the loading time when not used.<br/>
        ///     This method has to be called anytime before FejdStartup.Awake, meaning in your plugin's Awake or Start.
        /// </summary>
        public static void Enable()
        {
            if (!enabled)
            {
                Init();
            }

            enabled = true;
        }

        /// <summary>
        ///     Get all prefabs that were added by mods. Does not include Vanilla prefabs.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<IModPrefab> GetPrefabs()
        {
            List<IModPrefab> prefabs = new List<IModPrefab>();

            foreach (var prefab in Prefabs)
            {
                prefabs.AddRange(prefab.Value.Values);
            }

            prefabs.AddRange(PrefabManager.Instance.Prefabs.Values);
            return prefabs;
        }

        /// <summary>
        ///     Get all prefabs that were added by a specific mod
        /// </summary>
        /// <param name="modGuid"></param>
        /// <returns></returns>
        public static IEnumerable<IModPrefab> GetPrefabs(string modGuid)
        {
            List<IModPrefab> prefabs = new List<IModPrefab>();
            prefabs.AddRange(Prefabs[modGuid].Values);
            prefabs.AddRange(PrefabManager.Instance.Prefabs.Values.Where(x => x.SourceMod.GUID.Equals(modGuid)));
            return prefabs;
        }

        /// <summary>
        ///     Get an prefab by its name.
        ///     Does not include Vanilla prefabs, see <see cref="PrefabManager.GetPrefab">PrefabManager.GetPrefab(string)</see>
        ///     for those.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static IModPrefab GetPrefab(string name)
        {
            int hash = name.GetStableHashCode();

            if (PrefabManager.Instance.Prefabs.TryGetValue(name, out var customPrefab))
            {
                return customPrefab;
            }

            foreach (var prefab in Prefabs)
            {
                if (prefab.Value.ContainsKey(hash))
                {
                    return prefab.Value[hash];
                }
            }

            return null;
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.OnDestroy)), HarmonyPostfix]
        private static void ZNetSceneOnDestroy()
        {
            Prefabs.Clear();
            Recipes.Clear();
        }

        [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Awake)), HarmonyPostfix]
        private static void FejdStartup_Awake_Postfix()
        {
            FindAndPatchPatches(AccessTools.Method(typeof(ZNetScene), nameof(ZNetScene.Awake)));
            FindAndPatchPatches(AccessTools.Method(typeof(ObjectDB), nameof(ObjectDB.Awake)));
            FindAndPatchPatches(AccessTools.Method(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB)));
            FindAndPatchPatches(AccessTools.Method(typeof(ObjectDB), nameof(ObjectDB.UpdateRegisters)));
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake)), HarmonyPrefix, HarmonyPriority(1000)]
        private static void ObjectDBAwake(ObjectDB __instance)
        {
            // make sure vanilla prefabs are already added to not assign them to the first mod that call this function in a prefix
            __instance.UpdateRegisters();
        }

        private static void FindAndPatchPatches(MethodBase methodInfo)
        {
            PatchPatches(Harmony.GetPatchInfo(methodInfo)?.Prefixes);
            PatchPatches(Harmony.GetPatchInfo(methodInfo)?.Postfixes);
            PatchPatches(Harmony.GetPatchInfo(methodInfo)?.Finalizers);
        }

        private static void PatchPatches(ICollection<Patch> patches)
        {
            if (patches == null)
            {
                return;
            }

            foreach (var patch in patches)
            {
                if (patch.owner == Main.ModGuid)
                {
                    continue;
                }

                if (PatchedMethods.Contains(patch.PatchMethod))
                {
                    continue;
                }

                PatchedMethods.Add(patch.PatchMethod);

                try
                {
                    Main.Harmony.Patch(patch.PatchMethod, PrePatch, PostPatch);
                }
                catch (Exception e)
                {
                    Logger.LogError($"Failed to patch {patch.PatchMethod} from {patch.owner}: {e}");
                }
            }
        }

        private static void BeforePatch(object[] __args, ref Tuple<ZNetSceneState, ObjectDBState> __state)
        {
            ObjectDB objectDB = GetObjectDB(__args);
            ZNetScene zNetScene = GetZNetScene(__args);
            __state = new Tuple<ZNetSceneState, ObjectDBState>(new ZNetSceneState(zNetScene), new ObjectDBState(objectDB));
        }

        private static void AfterPatch(object[] __args, ref Tuple<ZNetSceneState, ObjectDBState> __state)
        {
            if (!__state.Item1.valid && !__state.Item2.valid)
            {
                return;
            }

            var plugin = BepInExUtils.GetPluginInfoFromAssembly(ReflectionHelper.GetCallingAssembly());

            if (plugin == null)
            {
                return;
            }

            __state.Item1.AddNewPrefabs(GetZNetScene(__args), plugin);
            __state.Item2.AddNewPrefabs(GetObjectDB(__args), plugin);
        }

        private static void AddPrefabs(IEnumerable<GameObject> before, IEnumerable<GameObject> after, BepInPlugin plugin)
        {
            AddPrefabs(new HashSet<GameObject>(before), new HashSet<GameObject>(after), plugin);
        }

        private static void AddPrefabs(Dictionary<int, GameObject> before, Dictionary<int, GameObject> after, BepInPlugin plugin)
        {
            AddPrefabs(new HashSet<GameObject>(before.Values), new HashSet<GameObject>(after.Values), plugin);
        }

        private static void AddPrefabs(HashSet<GameObject> before, HashSet<GameObject> after, BepInPlugin plugin)
        {
            if (!Prefabs.ContainsKey(plugin.GUID))
            {
                Prefabs.Add(plugin.GUID, new Dictionary<int, ModPrefab>());
            }

            foreach (var prefab in after)
            {
                if (!prefab)
                {
                    continue;
                }

                if (before.Contains(prefab))
                {
                    continue;
                }

                int hash = prefab.name.GetStableHashCode();

                if (!Prefabs[plugin.GUID].ContainsKey(hash))
                {
                    Prefabs[plugin.GUID].Add(hash, new ModPrefab(prefab, plugin));
                }
            }
        }

        private static void AddRecipes(IEnumerable<Recipe> before, IEnumerable<Recipe> after, BepInPlugin plugin)
        {
            AddRecipes(new HashSet<Recipe>(before), new HashSet<Recipe>(after), plugin);
        }

        private static void AddRecipes(HashSet<Recipe> before, HashSet<Recipe> after, BepInPlugin plugin)
        {
            if (!Recipes.ContainsKey(plugin.GUID))
            {
                Recipes.Add(plugin.GUID, new List<Recipe>());
            }

            foreach (var recipe in after)
            {
                if (before.Contains(recipe))
                {
                    continue;
                }

                if (!Recipes[plugin.GUID].Contains(recipe))
                {
                    Recipes[plugin.GUID].Add(recipe);
                }
            }
        }

        private static ZNetScene GetZNetScene(object[] __args)
        {
            foreach (var arg in __args)
            {
                if (arg is ZNetScene zNetScene)
                {
                    return zNetScene;
                }
            }

            return ZNetScene.instance;
        }

        private static ObjectDB GetObjectDB(object[] __args)
        {
            foreach (var arg in __args)
            {
                if (arg is ObjectDB objectDB)
                {
                    return objectDB;
                }
            }

            return ObjectDB.instance;
        }

        private class ZNetSceneState
        {
            public bool valid;
            public readonly Dictionary<int, GameObject> namedPrefabs;
            public readonly List<GameObject> prefabs;

            public ZNetSceneState(ZNetScene zNetScene)
            {
                valid = (bool)zNetScene;

                if (!valid)
                {
                    return;
                }

                this.namedPrefabs = new Dictionary<int, GameObject>(zNetScene.m_namedPrefabs);
                this.prefabs = new List<GameObject>(zNetScene.m_prefabs);
            }

            public void AddNewPrefabs(ZNetScene zNetScene, PluginInfo plugin)
            {
                if (!valid || !zNetScene)
                {
                    return;
                }

                AddPrefabs(namedPrefabs, zNetScene.m_namedPrefabs, plugin.Metadata);
                AddPrefabs(prefabs, zNetScene.m_prefabs, plugin.Metadata);
            }
        }

        private class ObjectDBState
        {
            public bool valid;
            public List<GameObject> items;
            public List<Recipe> recipes;
            public Dictionary<int, GameObject> itemByHash;

            public ObjectDBState(ObjectDB objectDB)
            {
                valid = (bool)objectDB;

                if (!valid)
                {
                    return;
                }

                items = new List<GameObject>(objectDB.m_items);
                recipes = new List<Recipe>(objectDB.m_recipes);
                itemByHash = new Dictionary<int, GameObject>(objectDB.m_itemByHash);
            }

            public void AddNewPrefabs(ObjectDB objectDB, PluginInfo plugin)
            {
                if (!valid || !objectDB)
                {
                    return;
                }

                AddPrefabs(items, objectDB.m_items, plugin.Metadata);
                AddPrefabs(itemByHash, objectDB.m_itemByHash, plugin.Metadata);
                AddRecipes(recipes, objectDB.m_recipes, plugin.Metadata);
            }
        }
    }
}

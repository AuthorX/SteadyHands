using HutongGames.PlayMaker.Actions;
using Modding;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace SteadyHands
{
    public class GlobalSettingsClass
    {
        public bool needsQuill = true;
        public bool isEnabled = true;
    }

    public class SteadyHandsMod : Mod, IGlobalSettings<GlobalSettingsClass>, IMenuMod, ITogglableMod
    {
        private static SteadyHandsMod? _instance;
        private static GameManager? gm;
        private static GameMap? map;
        private static GameObject? msg;

        public GlobalSettingsClass saveSettings { get; set; } = new GlobalSettingsClass();
        public void OnLoadGlobal(GlobalSettingsClass s) => this.saveSettings = s;
        public GlobalSettingsClass OnSaveGlobal() => this.saveSettings;


        internal static SteadyHandsMod Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new InvalidOperationException($"An instance of {nameof(SteadyHandsMod)} was never constructed");
                }
                return _instance;
            }
        }

        public bool ToggleButtonInsideMenu { get; } = true;

        public override string GetVersion() => GetType().Assembly.GetName().Version.ToString();

        public SteadyHandsMod() : base("SteadyHands")
        {
            _instance = this;
        }

        //public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        public override void Initialize()
        {
            gm = GameManager.instance;
            On.GameMap.Start += GameMap_Start;
            On.SceneManager.AddSceneMapped += SceneManager_AddSceneMapped;
            ModHooks.GetPlayerBoolHook += CheckQuill;
            Log("Initialized");
        }

        private bool CheckQuill(string name, bool orig)
        {
            return (name == "hasQuill" && saveSettings.isEnabled && !saveSettings.needsQuill) ? true : orig;
        }

        private void SceneManager_AddSceneMapped(On.SceneManager.orig_AddSceneMapped orig, SceneManager self)
        {
            orig(self);
            if (saveSettings.isEnabled) ForceUpdateGameMap();
        }

        private void GameMap_Start(On.GameMap.orig_Start orig, GameMap self)
        {
            orig(self);
            map = self;
        }

        private void ForceUpdateGameMap()
        {
            bool updated = (gm?.UpdateGameMap() == true);
            map?.SetupMap();
            LogDebug("UpdateGameMap() = " + updated);
            //if(updated)
            //{
            //    GameObject go = GameObject.Instantiate(msg);
            //    go.SetActive(true);
            //}
        }

        public List<IMenuMod.MenuEntry> GetMenuData(IMenuMod.MenuEntry? toggleButtonEntry)
        {
            return new List<IMenuMod.MenuEntry>
            {
                toggleButtonEntry.GetValueOrDefault(),
                new IMenuMod.MenuEntry {
                    Name = "Require Quill?",
                    // Description = "Automatically adds new areas to the map",
                    Values = new string[] {
                        "On",
                        "Off"
                    },
                    Saver = opt => saveSettings.needsQuill = opt == 0,
                    Loader = () => saveSettings.needsQuill ? 0 : 1
                }
            };
        }

        public void Unload()
        {
            On.GameMap.Start -= GameMap_Start;
            On.SceneManager.AddSceneMapped -= SceneManager_AddSceneMapped;
            ModHooks.GetPlayerBoolHook -= CheckQuill;
        }

        //public override (string, Func<IEnumerator>)[] PreloadSceneHooks()
        //{
        //    Log("PRELOAD SCENE HOOKS");

        //    return new (string, Func<IEnumerator>)[]
        //    {
        //        ("Town", GetMessagePrefab)
        //    };

        //    IEnumerator GetMessagePrefab()
        //    {
        //        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        //        {
        //            if (go.name == "Map Update Msg")
        //            {
        //                Log("Found Map Update Message Prefab");
        //                msg = GameObject.Instantiate(go);
        //                GameObject.DontDestroyOnLoad(msg);
        //                msg.SetActive(false);
        //                msg.name = go.name;
        //            }
        //        }
        //        yield break;
        //    }
        //}

    }
}

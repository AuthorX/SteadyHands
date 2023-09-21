using HutongGames.PlayMaker.Actions;
using Modding;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace SteadyHands
{
    public class SteadyHandsMod : Mod
    {
        private static SteadyHandsMod? _instance;
        private static GameManager? gm;
        private static GameMap? map;
        private static GameObject? msg;

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

        public override string GetVersion() => GetType().Assembly.GetName().Version.ToString();

        public SteadyHandsMod() : base("SteadyHands")
        {
            _instance = this;
        }

        //public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        public override void Initialize()
        {
            Log("Initializing");

            gm = GameManager.instance;
            On.GameMap.Start += GameMap_Start;
            On.GameManager.GetCurrentMapZone += GameMap_GetCurrentMapZone;
            //On.GameMap.WorldMap += GameMap_WorldMap;
            Log("Initialized");
        }

        private void GameMap_Start(On.GameMap.orig_Start orig, GameMap self)
        {
            orig(self);
            map = self;
        }

        //private void GameMap_WorldMap(On.GameMap.orig_WorldMap orig, GameMap self)
        //{
        //    ForceUpdateGameMap();
        //    orig(self);
        //}

        private string GameMap_GetCurrentMapZone(On.GameManager.orig_GetCurrentMapZone orig, GameManager self)
        {
            ForceUpdateGameMap();
            return orig(self);
        }

        private void ForceUpdateGameMap()
        {
            bool updated = (gm?.UpdateGameMap() == true);
            map?.SetupMap();
            //Log("UpdateGameMap() = " + updated);
            //if(updated)
            //{
            //    GameObject go = GameObject.Instantiate(msg);
            //    go.SetActive(true);
            //}
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
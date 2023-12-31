using Modding;
using System;
using Satchel;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using System.Linq;
using UnityEngine;

namespace SteadyHands
{
    public class GlobalSettingsClass
    {
        public bool needsQuill = true;
        public bool needsAreaMap = false;
    }

    public class LocalSettingsClass
    {
        public List<string> seenAreas = new();
    }

    public class SteadyHandsMod : Mod, IGlobalSettings<GlobalSettingsClass>, IMenuMod, ITogglableMod
    {
        private static SteadyHandsMod? _instance;
        private static GameManager gm;
        private static GameMap? map;
        private static PlayMakerFSM? quickmapFSM;
        private static readonly string[] mapBools = {
            "mapCrossroads",
            "mapGreenpath",
            "mapFogCanyon",
            "mapRoyalGardens",
            "mapFungalWastes",
            "mapCity",
            "mapWaterways",
            "mapMines",
            "mapDeepnest",
            "mapCliffs",
            "mapOutskirts",
            "mapRestingGround",
            "mapAbyss"
        };
        private bool ignoreBoolHook = false;

        public GlobalSettingsClass gs { get; set; } = new GlobalSettingsClass();
        public void OnLoadGlobal(GlobalSettingsClass s) => gs = s;
        public GlobalSettingsClass OnSaveGlobal() => gs;

        public LocalSettingsClass ls { get; set; } = new LocalSettingsClass();
        public void OnLoadLocal(LocalSettingsClass s) => ls = s;
        public LocalSettingsClass OnSaveLocal() => ls;


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

        public override void Initialize()
        {
            Log("Initializing");
            gm = GameManager.instance;
            On.GameMap.Start += GameMap_Start;
            On.SceneManager.AddSceneMapped += SceneManager_AddSceneMapped;
            On.RoughMapRoom.OnEnable += RoughMapRoom_OnEnable;
            ModHooks.GetPlayerBoolHook += PlayerBool;
            if (quickmapFSM is null)
                On.PlayMakerFSM.OnEnable += GetFSM;
            else
                EditQuickMapFSM(quickmapFSM);
            //On.GameMap.WorldMap += GameMap_WorldMap;
            IL.GameMap.WorldMap += ILHookCustomMapBools;
            IL.PlayerData.HasMapForScene += ILHookCustomMapBools;

            Log("Initialized");
        }
        public void Unload()
        {
            On.GameMap.Start -= GameMap_Start;
            On.SceneManager.AddSceneMapped -= SceneManager_AddSceneMapped;
            ModHooks.GetPlayerBoolHook -= PlayerBool;
            On.PlayMakerFSM.OnEnable -= GetFSM;
            if (quickmapFSM is not null) RestoreQuickMapFSM(quickmapFSM);

            IL.GameMap.WorldMap -= ILHookCustomMapBools;
            IL.PlayerData.HasMapForScene -= ILHookCustomMapBools;
        }

        private void RoughMapRoom_OnEnable(On.RoughMapRoom.orig_OnEnable orig, RoughMapRoom self)
        {
            orig(self);
            Log("RoughMapRoom_OnEnable: " + self.name);
            bool hasMap = ReflectionHelper.CallMethod<PlayerData, bool>(gm.playerData, "HasMapForScene", self.transform.name);
            Log("Before setting ignore bool, HasMapForScene = " + hasMap);
            ignoreBoolHook = true;
            hasMap = ReflectionHelper.CallMethod<PlayerData,bool>(gm.playerData,"HasMapForScene",self.transform.name);
            Log("After setting ignore bool, HasMapForScene = " + hasMap);
            ignoreBoolHook = false;
            self.GetComponent<SpriteRenderer>().enabled = hasMap || self.fullSpriteDisplayed;
        }


        private void ILHookCustomMapBools(ILContext il)
        {
            ILCursor cursor = new ILCursor(il).Goto(0);
            while (cursor.TryGotoNext(
                MoveType.Before,
                i => i.Match(OpCodes.Ldstr),
                i => i.MatchCallvirt<PlayerData>("GetBool")
                ))
            {
                var boolName = cursor.Next.Operand;
                if (mapBools.Contains(boolName))
                {
                    cursor.Remove();
                    cursor.Emit(OpCodes.Ldstr,boolName + "_custom");
                }
            }
        }

        private bool PlayerBool(string name, bool orig)
        {
            if (name == "hasQuill" && !gs.needsQuill || orig) return true;
            if (name.Contains("_custom"))
            {
                string origName = name.Replace("_custom", "");
                LogDebug("Hooking PlayerBool " + name + ", ignoreBoolHook = " + ignoreBoolHook);
                if (mapBools.Contains(origName))
                {
                    if (ignoreBoolHook) return gm.playerData.GetBool(origName);
                    return gm.playerData.GetBool(origName) || (!gs.needsAreaMap && ls.seenAreas.Contains(origName));
                }
            }
            return orig;
        }

        private void GetFSM(On.PlayMakerFSM.orig_OnEnable orig, PlayMakerFSM self)
        {
            orig(self);
            if (self.name == "Quick Map")
            {
                quickmapFSM = self;
                EditQuickMapFSM(quickmapFSM);
            }
        }

        private void EditQuickMapFSM(PlayMakerFSM fsm)
        {
            quickmapFSM = fsm;
            LogDebug("Editing Quick Map FSM");
            var states = fsm.FsmStates;
            foreach (FsmState state in states)
            {
                var action = state.GetFirstActionOfType<PlayerDataBoolTest>();
                if(action is not null && mapBools.Contains(action.boolName.ToString()))
                    action.boolName += "_custom";
            }
            LogDebug("Finished editing Quick Map FSM");
        }

        private void RestoreQuickMapFSM(PlayMakerFSM fsm)
        {
            LogDebug("Reversing edits to Quick Map FSM");
            var states = fsm.FsmStates;
            foreach (FsmState state in states)
            {
                var action = state.GetFirstActionOfType<PlayerDataBoolTest>();
                if (action is not null && mapBools.Contains(action.boolName.ToString().Replace("_custom","")))
                    action.boolName = action.boolName.ToString().Replace("_custom", "");
            }
            LogDebug("Finished editing Quick Map FSM");
        }

        private void SceneManager_AddSceneMapped(On.SceneManager.orig_AddSceneMapped orig, SceneManager self)
        {
            orig(self);
            string mapName = GetCurrentMapName(self.mapZone.ToString());
            if (mapName != "" && !ls.seenAreas.Contains(mapName)) ls.seenAreas.Add(mapName);
            ForceUpdateGameMap();
        }

        private string GetCurrentMapName(string mapZoneName)
        {
            return mapZoneName switch
            {
                "CLIFFS" => "mapCliffs",
                "CROSSROADS" => "mapCrossroads",
                "GREEN_PATH" or "ACID_LAKE" => "mapGreenpath",
                "ROYAL_GARDENS" => "mapRoyalGardens",
                "FOG_CANYON" => "mapFogCanyon",
                "WASTES" or "QUEENS_STATION" or "MANTIS_VILLAGE" => "mapFungalWastes",
                "DEEPNEST" or "RUINED_TRAMWAY" or "DISTANT_VILLAGE" => "mapDeepnest",
                "OUTSKIRTS" or "HIVE" or "COLOSSEUM" or "WYRMSKIN" => "mapOutskirts",
                "PALACE_GROUNDS" => "",
                "MINES" or "PEAK" => "mapMines",
                "RESTING_GROUNDS" or "GLADE" or "BLUE_LAKE" => "mapRestingGrounds",
                "CITY" or "KINGS_STATION" or "MAGE_TOWER" or "SOUL_SOCIETY" or "LURIENS_TOWER" or "LOVE_TOWER" => "mapCity",
                "ABYSS" or "ABYSS_DEEP" => "mapAbyss",
                "ROYAL_QUARTER" => "",
                "WATERWAYS" or "ISMAS_GROVE" or "GODSEEKER_WASTE" => "mapWaterways",
                _ => "",
            };
        }
        
        private void GameMap_Start(On.GameMap.orig_Start orig, GameMap self)
        {
            orig(self);
            map = self;
        }

        private void ForceUpdateGameMap()
        {
            var updated = gm?.UpdateGameMap();
            map?.SetupMap();
        }

        public List<IMenuMod.MenuEntry> GetMenuData(IMenuMod.MenuEntry? toggleButtonEntry)
        {
            return new List<IMenuMod.MenuEntry>
            {
                toggleButtonEntry.GetValueOrDefault(),
                new IMenuMod.MenuEntry {
                    Name = "Require Quill?",
                    Description = "Require buying Quill before automapping",
                    Values = new string[] {
                        "Yes",
                        "No"
                    },
                    Saver = opt => gs.needsQuill = opt == 0,
                    Loader = () => gs.needsQuill ? 0 : 1
                },
                new IMenuMod.MenuEntry {
                    Name = "Require Area Maps?",
                    Description = "Require buying each area map before automapping",
                    Values = new string[] {
                        "Yes",
                        "No"
                    },
                    Saver = opt => 
                    { 
                        gs.needsAreaMap = opt == 0;
                        if (opt == 1) ForceUpdateGameMap();
                    },
                    Loader = () => gs.needsAreaMap ? 0 : 1
                }
            };
        }

    }
}

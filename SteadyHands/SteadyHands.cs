using Modding;
using System;
using Satchel;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using static PlayMakerUGuiComponentProxy;
using System.Reflection;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using System.Linq;

namespace SteadyHands
{
    public class GlobalSettingsClass
    {
        public bool needsQuill = true;
        public bool needsAreaMap = false;
    }

    public class LocalSettingsClass
    {
        public List<string> seenAreas = new List<string>();
    }

    public class SteadyHandsMod : Mod, IGlobalSettings<GlobalSettingsClass>, IMenuMod, ITogglableMod
    {
        private static SteadyHandsMod? _instance;
        private static GameManager gm = GameManager.instance;
        private static GameMap? map;
        //private static PlayerData playerData = PlayerData.instance;
        private static PlayMakerFSM? quickmapFSM;
        private static string[] mapBools = {
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
        private static string[] mapStates =
        {
            "Crossroads",
            "Greenpath",
            "Cliffs",
            "Fungal Wastes",
            "City",
            "Mines",
            "Resting Grounds",
            "Fog Canyon",
            "Royal Gardens",
            "Deepnest",
            "Waterways",
            "Abyss",
            "Outskirts"
        };

        public GlobalSettingsClass gs { get; set; } = new GlobalSettingsClass();
        public void OnLoadGlobal(GlobalSettingsClass s) => this.gs = s;
        public GlobalSettingsClass OnSaveGlobal() => this.gs;

        public LocalSettingsClass ls { get; set; } = new LocalSettingsClass();
        public void OnLoadLocal(LocalSettingsClass s) => this.ls = s;
        public LocalSettingsClass OnSaveLocal() => this.ls;


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
            On.GameMap.Start += GameMap_Start;
            On.SceneManager.AddSceneMapped += SceneManager_AddSceneMapped;
            ModHooks.GetPlayerBoolHook += PlayerBool;
            On.PlayMakerFSM.OnEnable += GetFSM;
            //On.GameMap.WorldMap += GameMap_WorldMap;
            IL.GameMap.WorldMap += ILHookCustomMapBools;
            IL.PlayerData.HasMapForScene += ILHookCustomMapBools;

            if (quickmapFSM is not null) EditQuickMapFSM(quickmapFSM);

            Log("Initialized");
        }

        private void ILHookCustomMapBools(MonoMod.Cil.ILContext il)
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
                    LogDebug("ILHook for GetBool(\"" + boolName + "\")");
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
                if (mapBools.Contains(origName))
                    return gm.playerData.GetBool(origName) || (!gs.needsAreaMap && ls.seenAreas.Contains(origName));
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

        private void EditQuickMapFSM(PlayMakerFSM self)
        {
            quickmapFSM = self;
            LogDebug("Editing Quick Map FSM");
            var states = self.FsmStates;
            foreach (FsmState state in states)
            {
                var action = state.GetFirstActionOfType<PlayerDataBoolTest>();
                Log("checking list of states, trying to get <PlayerDataBoolTest> action for " + state.Name + ": " + (action is null));
                if(action is not null && mapBools.Contains(action.boolName.ToString()))
                    action.boolName = action.boolName + "_custom";
            }
            LogDebug("Finished editing Quick Map FSM");
        }

        private void SceneManager_AddSceneMapped(On.SceneManager.orig_AddSceneMapped orig, SceneManager self)
        {
            orig(self);
            string mapName = GetCurrentMapName(self.mapZone.ToString());
            LogDebug("Got current mapName: " + mapName);
            if (mapName != "" && !ls.seenAreas.Contains(mapName)) ls.seenAreas.Add(mapName);
            LogDebug("Set " + mapName + " in seenAreas");
            LogDebug(string.Join(", ", ls.seenAreas));
            ForceUpdateGameMap();
        }

        private string GetCurrentMapName(string mapZoneName)
        {
            Log("Got current mapZoneName: " + mapZoneName);
            switch (mapZoneName)
            {
                case "CLIFFS":
                    return "mapCliffs";
                case "CROSSROADS":
                    return "mapCrossroads";
                case "GREEN_PATH":
                case "ACID_LAKE":
                    return "mapGreenpath";
                case "ROYAL_GARDENS":
                    return "mapRoyalGardens";
                case "FOG_CANYON":
                    return "mapFogCanyon";
                case "WASTES":
                case "QUEENS_STATION":
                case "MANTIS_VILLAGE":
                    return "mapFungalWastes";
                case "DEEPNEST":
                case "RUINED_TRAMWAY":
                case "DISTANT_VILLAGE":
                    return "mapDeepnest";
                case "OUTSKIRTS":
                case "HIVE":
                case "COLOSSEUM":
                case "WYRMSKIN":
                    return "mapOutskirts";
                case "PALACE_GROUNDS":
                    return "";
                case "MINES":
                case "PEAK":
                    return "mapMines";
                case "RESTING_GROUNDS":
                case "GLADE":
                case "BLUE_LAKE":
                    return "mapRestingGrounds";
                case "CITY":
                case "KINGS_STATION":
                case "MAGE_TOWER":
                case "SOUL_SOCIETY":
                case "LURIENS_TOWER":
                case "LOVE_TOWER":
                    return "mapCity";
                case "ABYSS":
                case "ABYSS_DEEP":
                    return "mapAbyss";
                case "ROYAL_QUARTER":
                    return "";
                case "WATERWAYS":
                case "ISMAS_GROVE":
                case "GODSEEKER_WASTE":
                    return "mapWaterways";
            default:
                    return "";
            }
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
            Log("UpdateGameMap() = " + updated);
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

        public void Unload()
        {
            On.GameMap.Start -= GameMap_Start;
            On.SceneManager.AddSceneMapped -= SceneManager_AddSceneMapped;
            ModHooks.GetPlayerBoolHook -= PlayerBool;
        }
    }
}

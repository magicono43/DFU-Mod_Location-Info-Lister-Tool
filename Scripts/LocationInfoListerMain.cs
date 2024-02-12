// Project:         LocationInfoListerTool mod for Daggerfall Unity (http://www.dfworkshop.net)
// Copyright:       Copyright (C) 2024 Kirk.O
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Author:          Kirk.O
// Created On: 	    2/5/2024, 7:30 PM
// Last Edit:		2/11/2024, 7:45 PM
// Version:			1.00
// Special Thanks:  
// Modifier:

using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Wenzil.Console;

namespace LocationInfoListerTool
{
    public class LocationInfoListerMain : MonoBehaviour
    {
        static LocationInfoListerMain Instance;

        static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<LocationInfoListerMain>(); // Add script to the scene.

            mod.IsReady = true;
        }

        private void Start()
        {
            Debug.Log("Begin mod init: Location Info Lister Tool");

            Instance = this;

            RegisterLocationInfoListerCommands();

            Debug.Log("Finished mod init: Location Info Lister Tool");
        }

        public static void RegisterLocationInfoListerCommands()
        {
            Debug.Log("[LocationInfoListerTool] Trying to register console commands.");
            try
            {
                ConsoleCommandsDatabase.RegisterCommand(StartDungeonScrapping.name, StartDungeonScrapping.description, StartDungeonScrapping.usage, StartDungeonScrapping.Execute);
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format("Error Registering LocationInfoListerTool Console commands: {0}", e.Message));
            }
        }

        private static class StartDungeonScrapping
        {
            public static readonly string name = "getdunginfo";
            public static readonly string description = "Write All Info For Dungeons Into A Text File";
            public static readonly string usage = "Write All Dungeon Info To A Text File";

            public static string Execute(params string[] args)
            {
                ScrapeAllDungeonInfo();

                return "Wrote Dungeon Info To Text File...";
            }
        }

        #region Methods and Functions

        public static void ScrapeAllDungeonInfo()
        {
            DFRegion regionInfo = new DFRegion();
            int[] foundIndices = new int[0];
            List<int> validRegionIndexes = new List<int>();
            Dictionary<int, int[]> regionValidDungGrabBag = new Dictionary<int, int[]>();
            regionValidDungGrabBag.Clear(); // Attempts to clear dictionary to keep from compile errors about duplicate keys.

            for (int p = 0; p < 62; p++)
            {
                regionInfo = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetRegion(p);
                if (regionInfo.LocationCount <= 0) // Add the if-statements to keep "invalid" regions from being put into grab-bag, also use this for some settings.
                    continue;
                // Get indices for all dungeons of this type
                foundIndices = CollectDungeonIndicesOfType(regionInfo, p);
                if (foundIndices.Length == 0)
                    continue;

                regionValidDungGrabBag.Add(p, foundIndices);
                validRegionIndexes.Add(p);
            }

            Dictionary<string, int> blockCountingList = new Dictionary<string, int>();

            int n = -1;
            foreach (var locList in validRegionIndexes)
            {
                n++;
                //if (n > 3) // Just for testing.
                    //break;

                regionInfo = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetRegion(locList);

                foundIndices = regionValidDungGrabBag[locList];

                string regionName = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetRegionName(locList);
                string filePath = $@"c:\dfutesting\{regionName}" + ".txt";
                StreamWriter writer = new StreamWriter(filePath);
                writer.WriteLine($"Region: {regionName}");
                writer.WriteLine("");

                for (int r = 0; r < foundIndices.Length; r++)
                {
                    DFLocation dungLocation = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetLocation(locList, foundIndices[r]);
                    string dungName = dungLocation.Name;
                    DFLocation.LocationDungeon dungeon = dungLocation.Dungeon;
                    string dungTypeName = GetDungeonTypeName(regionInfo.MapTable[foundIndices[r]].DungeonType, regionInfo.MapTable[foundIndices[r]].Discovered);

                    writer.WriteLine($"Dungeon: {dungName}");
                    writer.WriteLine($"Type: {dungTypeName}");
                    writer.WriteLine($"Total Size: {dungeon.Blocks.Length}");

                    for (int k = 0; k < dungeon.Blocks.Length; k++)
                    {
                        DFLocation.DungeonBlock dunBlock = dungeon.Blocks[k];
                        string blockName = dunBlock.BlockName;

                        if (blockCountingList.ContainsKey(blockName))
                        {
                            blockCountingList[blockName] += 1;
                        }
                        else
                        {
                            blockCountingList.Add(blockName, 1);
                        }

                        writer.WriteLine($"{blockName},   X:{dunBlock.X}, Z:{dunBlock.Z}");
                    }

                    writer.WriteLine("");
                }

                writer.WriteLine("");
                writer.WriteLine("");
                writer.WriteLine("Block Count Breakdown For This Region:");
                writer.WriteLine("");

                var sortedDict = blockCountingList.OrderByDescending(x => x.Value).ToList();

                foreach (var kvp in sortedDict)
                {
                    writer.WriteLine($"{kvp.Key}:  {kvp.Value}");
                }

                writer.WriteLine("");
                writer.WriteLine("END");

                writer.Flush(); // Keeps text-files from trailing off without finishing all they were meant to write.

                blockCountingList.Clear();
                Debug.Log($"Results have been written to {filePath}");
            }

            Debug.Log("Dungeon info scrapping tool has finished running, without issue!");
        }

        public static int[] CollectDungeonIndicesOfType(DFRegion regionData, int regionIndex)
        {
            List<int> foundLocationIndices = new List<int>();

            // Collect all dungeon types
            for (int i = 0; i < regionData.LocationCount; i++)
            {
                // Discard all non-dungeon location types
                if (!IsLocationType(regionData.MapTable[i].LocationType))
                    continue;

                foundLocationIndices.Add(i);
            }

            return foundLocationIndices.ToArray();
        }

        public static bool IsLocationType(DFRegion.LocationTypes locationType)
        {
            // Consider 3 major dungeon types and 2 graveyard types as dungeons
            // Will exclude locations with dungeons, such as Daggerfall, Wayrest, Sentinel
            if (locationType == DFRegion.LocationTypes.DungeonKeep ||
                locationType == DFRegion.LocationTypes.DungeonLabyrinth ||
                locationType == DFRegion.LocationTypes.DungeonRuin ||
                locationType == DFRegion.LocationTypes.Graveyard)
            {
                return true;
            }

            return false;
        }

        public static string GetDungeonTypeName(DFRegion.DungeonTypes dungeonType, bool discovered)
        {
            switch(dungeonType)
            {
                case DFRegion.DungeonTypes.ScorpionNest: return "Scorpion Nest";
                case DFRegion.DungeonTypes.VolcanicCaves: return "Volcanic Caves";
                case DFRegion.DungeonTypes.BarbarianStronghold: return "Barbarian Stronghold";
                case DFRegion.DungeonTypes.DragonsDen: return "Dragon's Den";
                case DFRegion.DungeonTypes.GiantStronghold: return "Giant Stronghold";
                case DFRegion.DungeonTypes.SpiderNest: return "Spider Nest";
                case DFRegion.DungeonTypes.RuinedCastle: return "Ruined Castle";
                case DFRegion.DungeonTypes.HarpyNest: return "Harpy Nest";
                case DFRegion.DungeonTypes.Laboratory: return "Laboratory";
                case DFRegion.DungeonTypes.VampireHaunt: return "Vampire Haunt";
                case DFRegion.DungeonTypes.Coven: return "Coven";
                case DFRegion.DungeonTypes.NaturalCave: return "Natural Cave";
                case DFRegion.DungeonTypes.Mine: return "Mine";
                case DFRegion.DungeonTypes.DesecratedTemple: return "Desecrated Temple";
                case DFRegion.DungeonTypes.Prison: return "Prison";
                case DFRegion.DungeonTypes.HumanStronghold: return "Human Stronghold";
                case DFRegion.DungeonTypes.OrcStronghold: return "Orc Stronghold";
                case DFRegion.DungeonTypes.Crypt: return "Crypt";
                case DFRegion.DungeonTypes.Cemetery:
                    if (discovered) { return "Cemetery"; }
                    else { return "Forgotten Cemetery"; }
                default: return "None";
            }
        }

        #endregion

    }
}
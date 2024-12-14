// Project:         LocationInfoListerTool mod for Daggerfall Unity (http://www.dfworkshop.net)
// Copyright:       Copyright (C) 2024 Kirk.O
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Author:          Kirk.O
// Created On: 	    2/5/2024, 7:30 PM
// Last Edit:		12/13/2024, 7:40 PM
// Version:			1.10
// Special Thanks:  
// Modifier:

using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Formulas;
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
                ConsoleCommandsDatabase.RegisterCommand(StartCityScrapping.name, StartCityScrapping.description, StartCityScrapping.usage, StartCityScrapping.Execute);
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

        private static class StartCityScrapping
        {
            public static readonly string name = "getcityinfo";
            public static readonly string description = "Write All Info For Cities Into A Text File";
            public static readonly string usage = "Write All City Info To A Text File";

            public static string Execute(params string[] args)
            {
                ScrapeAllCityInfo();

                return "Wrote City Info To Text File...";
            }
        }

        #region Dungeon Info Scrapping Methods and Functions

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
                if (!IsDungeonLocationType(regionData.MapTable[i].LocationType))
                    continue;

                foundLocationIndices.Add(i);
            }

            return foundLocationIndices.ToArray();
        }

        public static bool IsDungeonLocationType(DFRegion.LocationTypes locationType)
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

        #region City Info Scrapping Methods and Functions

        public static void ScrapeAllCityInfo()
        {
            DFRegion regionInfo = new DFRegion();
            int[] foundIndices = new int[0];
            List<int> validRegionIndexes = new List<int>();
            Dictionary<int, int[]> regionValidCityGrabBag = new Dictionary<int, int[]>();
            regionValidCityGrabBag.Clear(); // Attempts to clear dictionary to keep from compile errors about duplicate keys.

            for (int p = 0; p < 62; p++)
            {
                regionInfo = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetRegion(p);
                if (regionInfo.LocationCount <= 0) // Add the if-statements to keep "invalid" regions from being put into grab-bag, also use this for some settings.
                    continue;
                // Get indices for all dungeons of this type
                foundIndices = CollectCityIndicesOfType(regionInfo, p);
                if (foundIndices.Length == 0)
                    continue;

                regionValidCityGrabBag.Add(p, foundIndices);
                validRegionIndexes.Add(p);
            }

            int n = -1;
            foreach (var locList in validRegionIndexes)
            {
                n++;
                //if (n > 6) // Just for testing.
                //break;

                regionInfo = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetRegion(locList);

                foundIndices = regionValidCityGrabBag[locList];

                string regionName = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetRegionName(locList);
                string filePath = $@"c:\dfutesting\{regionName}" + ".txt";
                StreamWriter writer = new StreamWriter(filePath);
                writer.WriteLine($"Region: {regionName}");
                writer.WriteLine("");

                for (int r = 0; r < foundIndices.Length; r++)
                {
                    int validBuildingsInLocation = 0;
                    DFLocation cityLocation = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetLocation(locList, foundIndices[r]);
                    string cityName = cityLocation.Name;
                    DFLocation.LocationExterior city = cityLocation.Exterior;
                    string cityTypeName = GetCityTypeName(regionInfo.MapTable[foundIndices[r]].LocationType);

                    if (cityTypeName == "City (Large)")
                        continue;

                    for (int m = 0; m < city.Buildings.Length; m++)
                    {
                        if (FilterOut(city.Buildings[m].BuildingType, city.Buildings[m].FactionId))
                        {
                            continue;
                        }
                        else
                        {
                            ++validBuildingsInLocation;
                            break;
                        }
                    }

                    if (validBuildingsInLocation <= 0)
                        continue;

                    int[] buildingChecklist = { 0, 0, 0, 0 };

                    for (int m = 0; m < city.Buildings.Length; m++)
                    {
                        if (FilterOut(city.Buildings[m].BuildingType, city.Buildings[m].FactionId))
                        {
                            continue;
                        }
                        else
                        {
                            if (city.Buildings[m].BuildingType == DFLocation.BuildingTypes.PawnShop) { ++buildingChecklist[0]; }
                            else if (city.Buildings[m].BuildingType == DFLocation.BuildingTypes.Bank) { ++buildingChecklist[1]; }
                            else if (city.Buildings[m].BuildingType == DFLocation.BuildingTypes.House2 && city.Buildings[m].FactionId == (int)FactionFile.FactionIDs.The_Dark_Brotherhood) { ++buildingChecklist[2]; }
                            else if (city.Buildings[m].BuildingType == DFLocation.BuildingTypes.GuildHall && city.Buildings[m].FactionId == (int)FactionFile.FactionIDs.The_Mages_Guild) { ++buildingChecklist[3]; }
                        }
                    }

                    if (buildingChecklist[1] <= 0 || buildingChecklist[2] <= 0 || buildingChecklist[3] <= 0) { continue; }

                    writer.WriteLine($"Location Name: {cityName}");
                    writer.WriteLine($"Type: {cityTypeName}");
                    writer.WriteLine($"Building Count: {city.Buildings.Length}");
                    writer.WriteLine("");

                    for (int k = 0; k < city.Buildings.Length; k++)
                    {
                        if (FilterOut(city.Buildings[k].BuildingType, city.Buildings[k].FactionId))
                            continue;

                        string buildingName = FormulaHelper.GenerateBuildingName(city.Buildings[k].NameSeed, city.Buildings[k].BuildingType, city.Buildings[k].FactionId, cityName, regionName);
                        string buildingTypeName = GetBuildingTypeName(city.Buildings[k].BuildingType, city.Buildings[k].FactionId);
                        int quality = city.Buildings[k].Quality;

                        writer.WriteLine($"Building Name: {buildingName}");
                        writer.WriteLine($"Type: {buildingTypeName}");
                        writer.WriteLine($"Quality: {quality}");
                        writer.WriteLine("");
                    }

                    writer.WriteLine("");
                    writer.WriteLine("");
                }

                writer.WriteLine("");
                writer.WriteLine("");
                writer.WriteLine("Block Count Breakdown For This Region:");
                writer.WriteLine("");

                writer.WriteLine("");
                writer.WriteLine("END");

                writer.Flush(); // Keeps text-files from trailing off without finishing all they were meant to write.

                Debug.Log($"Results have been written to {filePath}");
            }

            Debug.Log("City info scrapping tool has finished running, without issue!");
        }

        public static int[] CollectCityIndicesOfType(DFRegion regionData, int regionIndex)
        {
            List<int> foundLocationIndices = new List<int>();

            // Collect all dungeon types
            for (int i = 0; i < regionData.LocationCount; i++)
            {
                // Discard all non-dungeon location types
                if (!IsCityLocationType(regionData.MapTable[i].LocationType))
                    continue;

                foundLocationIndices.Add(i);
            }

            return foundLocationIndices.ToArray();
        }

        public static bool IsCityLocationType(DFRegion.LocationTypes locationType)
        {
            if (locationType == DFRegion.LocationTypes.TownCity ||
                locationType == DFRegion.LocationTypes.TownHamlet ||
                locationType == DFRegion.LocationTypes.TownVillage ||
                locationType == DFRegion.LocationTypes.ReligionTemple ||
                locationType == DFRegion.LocationTypes.Tavern)
            {
                return true;
            }

            return false;
        }

        public static string GetCityTypeName(DFRegion.LocationTypes cityType)
        {
            switch (cityType)
            {
                case DFRegion.LocationTypes.TownCity: return "City (Large)";
                case DFRegion.LocationTypes.TownHamlet: return "Hamlet (Medium)";
                case DFRegion.LocationTypes.TownVillage: return "Village (Small)";
                case DFRegion.LocationTypes.ReligionTemple: return "Temple";
                case DFRegion.LocationTypes.Tavern: return "Tavern";
                default: return "None";
            }
        }

        public static bool FilterOut(DFLocation.BuildingTypes buildingType, ushort factionID)
        {
            switch (buildingType)
            {
                case DFLocation.BuildingTypes.Bank:
                case DFLocation.BuildingTypes.PawnShop:
                    return false;
                case DFLocation.BuildingTypes.House1:
                case DFLocation.BuildingTypes.House3:
                case DFLocation.BuildingTypes.House4:
                case DFLocation.BuildingTypes.House5:
                case DFLocation.BuildingTypes.House6:
                case DFLocation.BuildingTypes.HouseForSale:
                case DFLocation.BuildingTypes.Town4:
                case DFLocation.BuildingTypes.Town23:
                case DFLocation.BuildingTypes.Special1:
                case DFLocation.BuildingTypes.Special2:
                case DFLocation.BuildingTypes.Special3:
                case DFLocation.BuildingTypes.Special4:
                    return true;
                case DFLocation.BuildingTypes.Alchemist:
                case DFLocation.BuildingTypes.Armorer:
                //case DFLocation.BuildingTypes.Bank:
                case DFLocation.BuildingTypes.Bookseller:
                case DFLocation.BuildingTypes.ClothingStore:
                case DFLocation.BuildingTypes.FurnitureStore:
                case DFLocation.BuildingTypes.GemStore:
                case DFLocation.BuildingTypes.GeneralStore:
                case DFLocation.BuildingTypes.Library:
                //case DFLocation.BuildingTypes.PawnShop:
                case DFLocation.BuildingTypes.WeaponSmith:
                case DFLocation.BuildingTypes.Tavern:
                case DFLocation.BuildingTypes.Palace:
                //case DFLocation.BuildingTypes.GuildHall:
                case DFLocation.BuildingTypes.Temple:
                    return true;
                case DFLocation.BuildingTypes.House2:
                    if (factionID == (int)FactionFile.FactionIDs.The_Thieves_Guild) { return true; }
                    else if (factionID == (int)FactionFile.FactionIDs.The_Dark_Brotherhood) { return false; }
                    else { return true; }
                case DFLocation.BuildingTypes.GuildHall:
                    if (factionID == (int)FactionFile.FactionIDs.The_Mages_Guild) { return false; }
                    else { return true; }
                default:
                    return false;
            }
        }

        public static string GetBuildingTypeName(DFLocation.BuildingTypes buildingType, ushort factionID)
        {
            switch (buildingType)
            {
                case DFLocation.BuildingTypes.Alchemist: return "Alchemist";
                case DFLocation.BuildingTypes.Armorer: return "Armorer";
                case DFLocation.BuildingTypes.Bank: return "Bank";
                case DFLocation.BuildingTypes.Bookseller: return "Book Store";
                case DFLocation.BuildingTypes.ClothingStore: return "Clothing Store";
                case DFLocation.BuildingTypes.FurnitureStore: return "Furniture Store";
                case DFLocation.BuildingTypes.GemStore: return "Gem Store";
                case DFLocation.BuildingTypes.GeneralStore: return "General Store";
                case DFLocation.BuildingTypes.Library: return "Library";
                case DFLocation.BuildingTypes.PawnShop: return "Pawn Shop";
                case DFLocation.BuildingTypes.WeaponSmith: return "Weapon Smith";
                case DFLocation.BuildingTypes.Tavern: return "Tavern";
                case DFLocation.BuildingTypes.Palace: return "Palace";
                case DFLocation.BuildingTypes.House1:
                case DFLocation.BuildingTypes.House3:
                case DFLocation.BuildingTypes.House4:
                case DFLocation.BuildingTypes.House5:
                case DFLocation.BuildingTypes.House6: return "House";
                case DFLocation.BuildingTypes.House2:
                    if (factionID == (int)FactionFile.FactionIDs.The_Thieves_Guild) { return "Thieves Guildhall"; }
                    else if (factionID == (int)FactionFile.FactionIDs.The_Dark_Brotherhood) { return "Dark Brotherhood Guildhall"; }
                    else { return "House"; }
                case DFLocation.BuildingTypes.GuildHall:
                    if (factionID == (int)FactionFile.FactionIDs.The_Mages_Guild) { return "Mages Guildhall"; }
                    else if (factionID == (int)FactionFile.FactionIDs.The_Fighters_Guild) { return "Fighters Guildhall"; }
                    else if (OwnedByKnightlyOrder(factionID)) { return "Knightly Order Guildhall"; }
                    else { return "Guildhall"; }
                case DFLocation.BuildingTypes.Temple:
                    return "Temple";
                default: return "None";
            }
        }

        public static bool OwnedByKnightlyOrder(ushort factionID)
        {
            switch (factionID)
            {
                case (int)FactionFile.FactionIDs.The_Host_of_the_Horn:
                case (int)FactionFile.FactionIDs.The_Knights_of_the_Dragon:
                case (int)FactionFile.FactionIDs.The_Knights_of_the_Flame:
                case (int)FactionFile.FactionIDs.The_Knights_of_the_Hawk:
                case (int)FactionFile.FactionIDs.The_Knights_of_the_Owl:
                case (int)FactionFile.FactionIDs.The_Knights_of_the_Rose:
                case (int)FactionFile.FactionIDs.The_Knights_of_the_Wheel:
                case (int)FactionFile.FactionIDs.The_Order_of_the_Candle:
                case (int)FactionFile.FactionIDs.The_Order_of_the_Raven:
                case (int)FactionFile.FactionIDs.The_Order_of_the_Scarab:
                    return true;
                default:
                    return false;
            }
        }

        #endregion

    }
}
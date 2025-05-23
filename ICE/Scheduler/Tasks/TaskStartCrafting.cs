﻿using Dalamud.Game.ClientState.Conditions;
using ECommons.GameHelpers;
using ECommons.Logging;
using ECommons.Reflection;
using ECommons.Throttlers;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ECommons.UIHelpers.AddonMasterImplementations.AddonMaster;

namespace ICE.Scheduler.Tasks
{
    internal static class TaskStartCrafting
    {
        public static void Enqueue()
        {
            if (P.Artisan.IsBusy())
            {
                Svc.Log.Debug("Artisan is busy or we stuck in crafting animations, returning...");
                P.TaskManager.EnqueueDelay(1500);
                return;
            }

            Svc.Log.Debug("Artisan is not busy...");
            P.TaskManager.Enqueue(() => P.Artisan.SetEnduranceStatus(false), "Ensuring endurance is off", DConfig);
            P.TaskManager.Enqueue(() => StartCraftingOld(), "Starting old crafting mothod", DConfig);
            // P.TaskManager.Enqueue(StartCrafting, "Starting Crafting Process", DConfig);
            P.TaskManager.EnqueueDelay(1500);
        }

        internal static bool? IsArtisanBusy()
        {
            if (!P.Artisan.IsBusy())
            {
                return true;
            }
            else
            {
                if (EzThrottler.Throttle("Waiting for Artisan to not be busy"))
                    PluginLog.Debug("Waiting for Artisan to not be busy");
            }

            return false;
        }

        internal unsafe static void SetArtisanEndurance(bool enable)
        {
            P.Artisan.SetEnduranceStatus(enable);
        }

        internal static bool? StartCraftingOld()
        {
            // this version is to be depreciated post artisan update. 

            uint currentScore = 0;
            uint goldScore = 0;
            uint silverScore = 0;

            var itemSheet = Svc.Data.GetExcelSheet<Item>();

            if (P.Artisan.GetEnduranceStatus() == false && !IsAddonActive("Synthesis"))
            {
                P.Artisan.SetEnduranceStatus(false);

                if (TryGetAddonMaster<WKSHud>("WKSHud", out var hud) && hud.IsAddonReady && !IsAddonActive("WKSMissionInfomation"))
                {
                    if (EzThrottler.Throttle("Opening Steller Missions"))
                    {
                        PluginLog.Debug("Opening Mission Menu");
                        hud.Mission();
                    }
                }

                var RecipeSheet = Svc.Data.GetExcelSheet<Recipe>();
                var currentMission = CurrentLunarMission;
                bool foundPreCraft = false;
                bool allCrafted = true;

                PluginLog.Debug($"Current Mission: {currentMission} | Found Pre-Craft? {foundPreCraft}");

                if (MoonRecipies[currentMission].PreCrafts)
                {
                    PluginDebug("Pre-crafts are part of the list, checking to see if any need crafting");
                    foreach (var pre in MoonRecipies[currentMission].PreCraftDict)
                    {
                        var itemId = RecipeSheet.GetRow(pre.Key).ItemResult.Value.RowId;
                        var currentAmount = GetItemCount((int)itemId);
                        PluginDebug($"Checking Pre-crafts to see if {itemId} has enough.");
                        PluginDebug($"Item Amount: {currentAmount} | Goal Amount: {pre.Value} | RecipeId: {pre.Key}");

                        if (currentAmount < pre.Value)
                        {
                            foundPreCraft = true; // <--- Mark that a pre-craft is needed!

                            if (EzThrottler.Throttle("Starting pre-craft", 4000))
                            {
                                PluginInfo($"Found an item that needs to be crafted: {itemId}");
                                int craftAmount = pre.Value - currentAmount;
                                P.Artisan.CraftItem(pre.Key, craftAmount);
                            }
                            break; // <-- Important: break out after starting a pre-craft to avoid multiple crafts at once
                        }
                    }
                }

                if (!foundPreCraft)
                {
                    PluginLog.Debug("No pre-crafts remaining! Crafting the main item");
                    foreach (var main in MoonRecipies[currentMission].MainCraftsDict)
                    {
                        var itemId = RecipeSheet.GetRow(main.Key).ItemResult.Value.RowId;
                        PluginLog.Debug($"Checking ItemID: {itemId}");
                        var currentAmount = GetItemCount((int)itemId);
                        PluginLog.Debug($"Current Item Count is: {currentAmount}");

                        PluginLog.Debug($"Checking if the currentAmount: {currentAmount} is < Amount Needed: {main.Value}");

                        if (currentAmount < main.Value)
                        {
                            PluginLog.Debug("Current amount IS < Amount Needed, checking to see if you can craft");
                            if (EzThrottler.Throttle("Starting Main Craft", 4000))
                            {
                                int craftamount = main.Value - currentAmount;
                                PluginLog.Debug($"Telling Artisan to use recipe: {main.Key} | {craftamount}");
                                P.Artisan.CraftItem(main.Key, craftamount);
                                allCrafted = false;
                                break;
                            }
                            PluginLog.Debug($"Currently being throttled, breaking");
                            allCrafted = false;
                            break;
                        }
                    }
                    PluginDebug("all items were crafted!");
                }



                if (TryGetAddonMaster<WKSMissionInfomation>("WKSMissionInfomation", out var z) && z.IsAddonReady && allCrafted)
                {
                    uint tempScore = 0;
                    string currentScoreText = GetNodeText("WKSMissionInfomation", 27);
                    currentScoreText = currentScoreText.Replace(",", ""); // English client comma's
                    currentScoreText = currentScoreText.Replace(" ", ""); // French client spacing
                    currentScoreText = currentScoreText.Replace(".", ""); // French client spacing
                    if (uint.TryParse(currentScoreText, out tempScore))
                    {
                        currentScore = tempScore;
                    }
                    else
                    {
                        currentScore = 0;
                    }

                    goldScore = MissionInfoDict[CurrentLunarMission].GoldRequirement;
                    silverScore = MissionInfoDict[CurrentLunarMission].SilverRequirement;

                    if (currentScore != 0)
                    {
                        PluginDebug("Score != 0");
                        if (currentScore >= silverScore && C.TurninOnSilver)
                        {
                            if (EzThrottler.Throttle("Turning in item"))
                            {
                                z.Report();
                                return true;
                            }
                        }

                        if (C.TurninASAP)
                        {
                            if (EzThrottler.Throttle("Turning in item"))
                            {
                                z.Report();
                                return true;
                            }
                        }

                        if (PlayerNotBusy() && !Svc.Condition[ConditionFlag.PreparingToCraft])
                        {
                            if (EzThrottler.Throttle("Turning in item"))
                            {
                                z.Report();
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        internal static void StartCrafting()
        {
            var (currentScore, goldScore) = GetCurrentScores();

            var itemSheet = Svc.Data.GetExcelSheet<Item>();

            if (!P.Artisan.IsBusy())
            {
                if (TryGetAddonMaster<WKSHud>("WKSHud", out var hud) && hud.IsAddonReady && !IsAddonActive("WKSMissionInfomation"))
                {
                    if (EzThrottler.Throttle("Opening Steller Missions"))
                    {
                        PluginLog.Debug("Opening Mission Menu");
                        hud.Mission();
                    }
                }

                var RecipeSheet = Svc.Data.GetExcelSheet<Recipe>();
                var currentMission = CurrentLunarMission;
                bool foundPreCraft = false;
                bool allCrafted = true;

                PluginDebug($"Current Mission: {currentMission} | Found Pre-Craft? {foundPreCraft}");

                if (MoonRecipies[currentMission].PreCrafts)
                {
                    PluginDebug("Pre-crafts are part of the list, checking to see if any need crafting");
                    foreach (var pre in MoonRecipies[currentMission].PreCraftDict)
                    {
                        var itemId = RecipeSheet.GetRow(pre.Key).ItemResult.Value.RowId;
                        var currentAmount = GetItemCount((int)itemId);
                        PluginDebug($"Checking Pre-crafts to see if {itemId} has enough.");
                        PluginDebug($"Item Amount: {currentAmount} | Goal Amount: {pre.Value} | RecipeId: {pre.Key}");
                        var goalAmount = pre.Value;
                        if (C.CraftMultipleMissionItems)
                        {
                            goalAmount = pre.Value * 2;
                        }

                        if (currentAmount < goalAmount)
                        {
                            foundPreCraft = true; // <--- Mark that a pre-craft is needed!

                            if (EzThrottler.Throttle("Starting pre-craft", 4000))
                            {
                                PluginInfo($"Found an item that needs to be crafted: {itemId}");
                                int craftAmount = goalAmount - currentAmount;
                                P.Artisan.CraftItem(pre.Key, craftAmount);
                                P.TaskManager.EnqueueDelay(1500);
                            }
                            break; // <-- Important: break out after starting a pre-craft to avoid multiple crafts at once
                        }
                    }
                }

                if (!foundPreCraft)
                {
                    PluginDebug("No pre-crafts remaining! Crafting the main item");
                    foreach (var main in MoonRecipies[currentMission].MainCraftsDict)
                    {
                        var itemId = RecipeSheet.GetRow(main.Key).ItemResult.Value.RowId;
                        var currentAmount = GetItemCount((int)itemId);

                        PluginDebug($"[Main Item(s)] ItemId: {itemId} | Current Amount {currentAmount} | Amount Wanted: {main.Value} | RecipeId: {main.Key}");
                        var goalAmount = main.Value;
                        if (C.CraftMultipleMissionItems)
                        {
                            goalAmount = main.Value * 2;
                        }

                        if (currentAmount < goalAmount || (currentScore < goldScore && Svc.Condition[ConditionFlag.PreparingToCraft])) // if not hit gold and there is still some items (aka its still in preparing to craft animation) we want to send it anyway
                        {
                            if (EzThrottler.Throttle("Starting Main Craft", 4000))
                            {
                                int craftamount = goalAmount - currentAmount;
                                PluginDebug($"[Main Item(s)] Telling Artisan to use recipe: {main.Key} | {craftamount}");
                                P.Artisan.CraftItem(main.Key, goalAmount);
                                P.TaskManager.EnqueueDelay(1500);
                                allCrafted = false;
                                break;
                            }
                        }
                    }
                }



                if (TryGetAddonMaster<WKSMissionInfomation>("WKSMissionInfomation", out var z) && z.IsAddonReady && allCrafted)
                {
                    (currentScore, goldScore) = GetCurrentScores();

                    if (currentScore != 0)
                    {
                        PluginDebug("Score != 0");
                        if (PlayerNotBusy() && !Svc.Condition[ConditionFlag.PreparingToCraft])
                        {
                            if (EzThrottler.Throttle("Turning in item"))
                            {
                                z.Report();
                            }
                        }
                    }
                }
            }
        }

        internal static (uint currentScore, uint goldScore) GetCurrentScores()
        {
            if (TryGetAddonMaster<WKSMissionInfomation>("WKSMissionInfomation", out var z) && z.IsAddonReady)
            {
                var goldScore = MissionInfoDict[CurrentLunarMission].GoldRequirement;

                string currentScoreText = GetNodeText("WKSMissionInfomation", 27);
                currentScoreText = currentScoreText.Replace(",", ""); // English client comma's
                currentScoreText = currentScoreText.Replace(" ", ""); // French client spacing
                currentScoreText = currentScoreText.Replace(".", ""); // French client spacing
                if (uint.TryParse(currentScoreText, out uint tempScore))
                {
                    return (tempScore, goldScore);
                }
                else
                {
                    return (0, goldScore);
                }
            }

            return (0, 0);
        }
    }
}

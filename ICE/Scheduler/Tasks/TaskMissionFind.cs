﻿using ECommons.Automation;
using ECommons.Logging;
using ECommons.Throttlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ECommons.UIHelpers.AddonMasterImplementations.AddonMaster;

namespace ICE.Scheduler.Tasks
{
    internal static class TaskMissionFind
    {
        private static uint MissionId = 0;

        public static void Enqueue()
        {
            P.TaskManager.Enqueue(() => UpdateValues());
            P.TaskManager.Enqueue(() => OpenMissionFinder(), "Opening the Mission finder");
            P.TaskManager.Enqueue(() => WeatherButton(), "Selecting Weather");
            P.TaskManager.EnqueueDelay(200);
            P.TaskManager.Enqueue(() => FindWeatherMission(), "Checking to see if weather mission avaialable");
            P.TaskManager.Enqueue(() => BasicMissionButton(), "Selecting Basic Missions");
            P.TaskManager.EnqueueDelay(200);
            P.TaskManager.Enqueue(() => FindBasicMission(), "Finding Basic Mission");
            P.TaskManager.Enqueue(() => FindResetMission(), "Checking for abandon mission");
            P.TaskManager.Enqueue(() => GrabMission(), "Grabbing the mission");
            P.TaskManager.Enqueue(() => CurrentLunarMission != 0);
            P.TaskManager.Enqueue(() => AbandonMission(), "Checking to see if need to leave mission");
            P.TaskManager.Enqueue(() =>
            {
                if (SchedulerMain.Abandon)
                {
                    P.TaskManager.Enqueue(() => CurrentLunarMission == 0);
                    SchedulerMain.Abandon = false;
                }
            });
        }

        internal static bool? UpdateValues()
        {
            SchedulerMain.Abandon = false;
            SchedulerMain.MissionName = string.Empty;
            MissionId = 0;

            return true;
        }

        internal unsafe static bool? WeatherButton()
        {
            if (TryGetAddonMaster<WKSMission>("WKSMission", out var x) && x.IsAddonReady)
            {
                x.ProvisionalMissions();
                return true;
            }
            return false;
        }

        internal unsafe static bool? BasicMissionButton()
        {
            if (MissionId != 0)
            {
                return true;
            }

            if (TryGetAddonMaster<WKSMission>("WKSMission", out var x) && x.IsAddonReady)
            {
                x.BasicMissions();
                return true;
            }
            return false;
        }

        internal unsafe static bool? OpenMissionFinder()
        {
            if (TryGetAddonMaster<WKSMission>("WKSMission", out var mission) && mission.IsAddonReady)
            {
                return true;
            }

            if (TryGetAddonMaster<WKSHud>("WKSHud", out var hud) && hud.IsAddonReady)
            {
                if (EzThrottler.Throttle("Opening Mission Hud", 1000))
                {
                    hud.Mission();
                }
            }

            return false;
        }

        internal unsafe static bool? FindWeatherMission()
        {
            if (TryGetAddonMaster<WKSMission>("WKSMission", out var x) && x.IsAddonReady)
            {
                x.ProvisionalMissions();
                var currentClassJob = GetClassJobId();
                foreach (var m in x.StellerMissions)
                {
                    var weatherMissionEntry = C.EnabledMission.FirstOrDefault(e => e.Id == m.MissionId && MissionInfoDict[e.Id].JobId == currentClassJob);

                    if (weatherMissionEntry == default)
                    {
                        PluginDebug($"weather mission entry is default. Which means id: {weatherMissionEntry}");
                        continue;
                    }

                    if (EzThrottler.Throttle("Selecting Weather Mission"))
                    {
                        PluginLog.Debug($"Mission Name: {m.Name} | MissionId: {weatherMissionEntry.Id} has been found. Setting value for sending");
                        m.Select();
                        SchedulerMain.MissionName = m.Name;
                        MissionId = weatherMissionEntry.Id;
                        return true;
                    }
                }
            }

            if (MissionId == 0)
            {
                PluginLog.Debug("No mission was found under weather, continuing on");
                return true;
            }

            return false;
        }

        internal unsafe static bool? FindBasicMission()
        {
            PluginLog.Debug($"[Basic Mission Start] | Mission Name: {SchedulerMain.MissionName} | MissionId: {MissionId}");
            if (MissionId != 0)
            {
                PluginLog.Debug("You already have a mission found, skipping finding a basic mission");
                return true;
            }


            if (TryGetAddonMaster<WKSMission>("WKSMission", out var x) && x.IsAddonReady)
            {
                foreach (var m in x.StellerMissions)
                {
                    var basicMissionEntry = C.EnabledMission.FirstOrDefault(e => e.Id == m.MissionId);

                    if (basicMissionEntry == default)
                        continue;

                    if (EzThrottler.Throttle("Selecting Basic Mission"))
                    {
                        PluginLog.Debug($"Mission Name: {basicMissionEntry.Name} | MissionId: {basicMissionEntry.Id} has been found. Setting values for sending");
                        SchedulerMain.MissionName = basicMissionEntry.Name;
                        MissionId = basicMissionEntry.Id;
                        m.Select();
                        return true;
                    }
                }
            }

            if (MissionId == 0)
            {
                PluginLog.Debug("No mission was found under basic missions, continuing on");
                return true;
            }

            return false;
        }

        internal unsafe static bool? FindResetMission()
        {
            PluginLog.Debug($"[Reset Mission Finder] Mission Name: {SchedulerMain.MissionName} | MissionId {MissionId}");
            if (MissionId != 0)
            {
                PluginLog.Debug("You already have a mission found, skipping finding a basic mission");
                return true;
            }

            if (TryGetAddonMaster<WKSMission>("WKSMission", out var x) && x.IsAddonReady)
            {
                PluginLog.Debug("found mission was false");
                var currentClassJob = GetClassJobId();
                var ranks = C.EnabledMission
                    .Where(e => MissionInfoDict[e.Id].JobId == currentClassJob)
                    .Select(e => MissionInfoDict[e.Id].Rank)
                    .ToList();
                if (ranks.Count == 0)
                {
                    PluginLog.Debug("No missions selected in UI, would abandon every mission");
                    return false;
                }

                var rankToReset = ranks.Max();

                foreach (var m in x.StellerMissions)
                {
                    var missionEntry = MissionInfoDict.FirstOrDefault(e => e.Key == m.MissionId);

                    if (missionEntry.Value == null || missionEntry.Value.JobId != currentClassJob)
                        continue;

                    PluginLog.Debug($"Mission: {m.Name} | Mission rank: {missionEntry.Value.Rank} | Rank to reset: {rankToReset}");
                    if (missionEntry.Value.Rank == rankToReset || (missionEntry.Value.Rank >= 4 && rankToReset >= 4))
                    {
                        if (EzThrottler.Throttle("Selecting Abandon Mission"))
                        {
                            PluginLog.Debug($"Setting SchedulerMain.MissionName = {m.Name}");
                            m.Select();
                            SchedulerMain.MissionName = m.Name;
                            MissionId = missionEntry.Key;
                            SchedulerMain.Abandon = true;

                            PluginLog.Debug($"Mission Name: {SchedulerMain.MissionName}");

                            return true;
                        }
                    }
                }
            }

            return false;
        }

        internal unsafe static bool? GrabMission()
        {
            PluginLog.Debug($"[Grabbing Mission] Mission Name: {SchedulerMain.MissionName} | MissionId {MissionId}");
            if (TryGetAddonMaster<SelectYesno>("SelectYesno", out var select) && select.IsAddonReady)
            {
                if (EzThrottler.Throttle("Selecting Yes", 250))
                {
                    select.Yes();
                }
            }
            else if (TryGetAddonMaster<WKSMission>("WKSMission", out var x) && x.IsAddonReady)
            {
                if (!MissionInfoDict.ContainsKey(MissionId))
                {
                    PluginLog.Debug($"No values were found for mission id {MissionId}... which is odd. Stopping the process");
                    P.TaskManager.Abort();
                }

                if (EzThrottler.Throttle("Firing off to initiate quest"))
                {
                    Callback.Fire(x.Base, true, 13, MissionId);
                }
            }
            else if (!IsAddonActive("WKSMission"))
            {
                return true;
            }

            return false;
        }

        internal unsafe static bool? AbandonMission()
        {
            if (SchedulerMain.Abandon == false)
            {
                return true;
            }
            else
            {
                if (TryGetAddonMaster<SelectYesno>("SelectYesno", out var select) && select.IsAddonReady)
                {
                    if (EzThrottler.Throttle("Confirming Abandon"))
                    {
                        select.Yes();
                        return true;
                    }
                }
                if (TryGetAddonMaster<WKSMissionInfomation>("WKSMissionInfomation", out var addon) && addon.IsAddonReady)
                {
                    if (EzThrottler.Throttle("Abandoning the mission"))
                        addon.Abandon();
                }
                else if (TryGetAddonMaster<WKSHud>("WKSHud", out var SpaceHud) && SpaceHud.IsAddonReady)
                {
                    if (EzThrottler.Throttle("Opening the mission hud"))
                        SpaceHud.Mission();
                }
            }

            return false;
        }
    }
}

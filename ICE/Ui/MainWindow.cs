﻿// File: ICE/Ui/MainWindow.cs
using ICE.Scheduler;
using Dalamud.Interface.Utility.Raii;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ICE;
using static ICE.Utilities.Data;
using ICE.Ui;
using ImGuiNET;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace ICE.Ui
{
    internal class MainWindow : Window
    {
        /// <summary>
        /// Constructor for the main window. Adjusts window size, flags, and initializes data.
        /// </summary>
        public MainWindow() :
            base($"Ice's Cosmic Exploration {P.GetType().Assembly.GetName().Version} ###ICEMainWindow")
        {
            // No special window flags by default.
            Flags = ImGuiWindowFlags.None;

            // Set up size constraints to ensure window cannot be too small or too large.
            // Increased minimum size to better accommodate larger font sizes.
            SizeConstraints = new()
            {
                MinimumSize = new Vector2(500, 500),
                MaximumSize = new Vector2(2000, 2000)
            };

            // Register this window with Dalamud's window system.
            P.windowSystem.AddWindow(this);

            // Disable pinning of this window.
            AllowPinning = false;
        }

        /// <summary>
        /// Dispose of the window by removing it from the window system.
        /// </summary>
        public void Dispose()
        {
            P.windowSystem.RemoveWindow(this);
        }

        // Available crafting jobs and their IDs.
        private static List<(string Name, uint Id)> jobOptions = new()
        {
            ("CRP", 9),
            ("BSM", 10),
            ("ARM", 11),
            ("GSM", 12),
            ("LTW", 13),
            ("WVR", 14),
            ("ALC", 15),
            ("CUL", 16),
        };

        // Available mission ranks and their identifiers.
        private static List<(uint RankId, string RankName)> rankOptions = new()
        {
            (1, "D"),
            (2, "C"),
            (3, "B"),
            (4, "A"),
        };

        // Index of the currently selected job in jobOptions.
        private static int selectedIndex = 0;
        // ID of the currently selected crafting job.
        private static uint selectedJobId = jobOptions[selectedIndex].Id;

        // Index of the currently selected rank in rankOptions.
        private static int selectedRankIndex = 0;
        // Name of the currently selected rank (for displaying in header).
        private static string selectedRankName = rankOptions[selectedRankIndex].RankName;

        // Configuration booleans bound to checkboxes.
        private static bool delayGrab = C.DelayGrab;
        private static bool silverTurnin = C.TurninOnSilver;
        private static bool craftx2 = C.CraftMultipleMissionItems;
        private static bool turninASAP = C.TurninASAP;
        private static bool hideUnsupported = C.HideUnsupportedMissions;
        private static bool SortByName = C.TableSortByName;

        /// <summary>
        /// Primary draw method. Responsible for drawing the entire UI of the main window.
        /// </summary>
        public override void Draw()
        {
            // Title text for the run controls.
            ImGui.Text("Run");

            // Help marker explaining how missions are selected and run.
            ImGuiEx.HelpMarker(
                "Please note: this will try and run based off of every rank that it can.\n" +
                "So if you have both C & D checkmarks, it will check C first -> Check D for potential Missions.\n" +
                "It will cycle through missions until it finds one that you have selected.\n" +
                "Unsupported missions will be disabled and shown in red; check 'Hide unsupported missions' to filter them out."
            );

            ImGui.Spacing();

            // Start button (disabled while already ticking).
            using (ImRaii.Disabled(SchedulerMain.AreWeTicking))
            {
                if (ImGui.Button("Start"))
                {
                    SchedulerMain.EnablePlugin();
                }
            }

            ImGui.SameLine();

            // Stop button (disabled while not ticking).
            using (ImRaii.Disabled(!SchedulerMain.AreWeTicking))
            {
                if (ImGui.Button("Stop"))
                {
                    SchedulerMain.DisablePlugin();
                }
            }

            // Checkbox: Add delay to grabbing mission.
            if (ImGui.Checkbox("Add delay to grabbing mission", ref delayGrab))
            {
                C.DelayGrab = delayGrab;
                C.Save();
            }

            // Checkbox: Turn in on silver.
            if (ImGui.Checkbox("Turnin on Silver", ref silverTurnin))
            {
                // Ensure mutual exclusivity with Turnin ASAP.
                if (turninASAP)
                {
                    turninASAP = false;
                    C.TurninASAP = false;
                }

                if (silverTurnin != C.TurninOnSilver)
                {
                    C.TurninOnSilver = silverTurnin;
                    C.Save();
                }
            }

            // Checkbox: If Silver is enabled, allow for x2 crafting
            if (silverTurnin)
            {
                ImGui.SameLine();
                if (ImGui.Checkbox("Craft item twice", ref craftx2))
                {
                    if (craftx2 != C.CraftMultipleMissionItems)
                    {
                        C.CraftMultipleMissionItems = craftx2;
                        C.Save();
                    }
                }
            }

            // Checkbox: Turn in ASAP.
            if (ImGui.Checkbox("Turnin ASAP", ref turninASAP))
            {
                // Ensure mutual exclusivity with Turnin on Silver.
                if (silverTurnin)
                {
                    silverTurnin = false;
                    C.TurninOnSilver = false;
                }

                C.TurninASAP = turninASAP;
                C.Save();
            }

            // Checkbox: Hide unsupported missions.
            if (ImGui.Checkbox("Hide unsupported missions", ref hideUnsupported))
            {
                C.HideUnsupportedMissions = hideUnsupported;
                C.Save();
            }

            ImGui.Spacing();

            // Crafting Job selection combo.
            ImGui.SetNextItemWidth(150);
            if (ImGui.BeginCombo("Crafting Job", jobOptions[selectedIndex].Name))
            {
                for (int i = 0; i < jobOptions.Count; i++)
                {
                    bool isSelected = (i == selectedIndex);
                    if (ImGui.Selectable(jobOptions[i].Name, isSelected))
                    {
                        selectedIndex = i;
                        selectedJobId = jobOptions[i].Id;
                    }
                    if (isSelected)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }
                ImGui.EndCombo();
            }

            // Rank selection combo.
            ImGui.SetNextItemWidth(100);
            if (ImGui.BeginCombo("Rank", rankOptions[selectedRankIndex].RankName))
            {
                for (int i = 0; i < rankOptions.Count; i++)
                {
                    bool isSelected = (i == selectedRankIndex);
                    if (ImGui.Selectable(rankOptions[i].RankName, isSelected))
                    {
                        selectedRankIndex = i;
                        selectedRankName = rankOptions[i].RankName;
                    }
                    if (isSelected)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }
                ImGui.EndCombo();
            }

            ImGui.Spacing();

            // Checkbox: Option to sort by ID vs by Name
            if (ImGui.Checkbox("Sort by Name", ref SortByName))
            {
                if (SortByName != C.TableSortByName)
                {
                    C.TableSortByName = SortByName;
                    C.Save();
                }
            }

            // Missions table with four columns: checkbox, ID, dynamic Rank header, Rewards.
            if (ImGui.BeginTable("###MissionList", 4, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders))
            {
                // First column: checkbox (empty header)
                ImGui.TableSetupColumn("##Enable");
                // Second column: ID
                ImGui.TableSetupColumn("ID");
                // Third column: dynamic header showing selected rank missions
                ImGui.TableSetupColumn($"Rank {selectedRankName} Missions");
                // Fourth column: Rewards
                ImGui.TableSetupColumn("Rewards");

                // Render the header row
                ImGui.TableHeadersRow();

                IEnumerable<KeyValuePair<uint, MissionListInfo>> missions = MissionInfoDict;
                if (SortByName)
                    missions = missions.OrderBy(x => x.Value.Name);

                foreach (var entry in missions)
                {
                    // Filter by selected job ID (note: JobId is zero-based, our IDs start at 9)
                    if (entry.Value.JobId != selectedJobId - 1)
                        continue;

                    // Determine if this is an A-rank mission
                    bool isARank = ARankIds.Contains(entry.Value.Rank);
                    // Filter by selected rank
                    if (selectedRankIndex == 3
                        ? !isARank
                        : entry.Value.Rank != rankOptions[selectedRankIndex].RankId)
                        continue;

                    // Skip unsupported missions if the user has chosen to hide them
                    bool unsupported = UnsupportedMissions.Ids.Contains(entry.Key);
                    if (unsupported && hideUnsupported)
                        continue;

                    // Start a new row
                    ImGui.TableNextRow();

                    // Column 0: Enable checkbox
                    ImGui.TableSetColumnIndex(0);
                    bool enabled = C.EnabledMission.Any(x => x.Id == entry.Key);
                    using (ImRaii.Disabled(unsupported))
                    {
                        if (ImGui.Checkbox($"###{entry.Value.Name} + {entry.Key}", ref enabled))
                        {
                            if (enabled)
                            {
                                C.EnabledMission.Add((entry.Key, entry.Value.Name));
                            }
                            else
                            {
                                C.EnabledMission.Remove((entry.Key, entry.Value.Name));
                            }
                            C.Save();
                        }
                    }

                    // Column 1: Mission ID
                    ImGui.TableNextColumn();
                    ImGui.Text($"{entry.Key}");

                    // Column 2: Mission Name
                    ImGui.TableNextColumn();
                    if (unsupported)
                    {
                        ImGui.TextColored(new Vector4(1f, 0f, 0f, 1f), entry.Value.Name);
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Currently not supported");
                        }
                    }
                    else
                    {
                        ImGui.Text($"{entry.Value.Name}");
                    }

                    // Column 3: Rewards
                    ImGui.TableNextColumn();
                    ImGui.Text(
                        string.Join(
                            " | ",
                            entry.Value.ExperienceRewards
                                .Select(exp => ExpDictionary[exp.Type] + ": " + exp.Amount)
                        )
                    );
                }

                ImGui.EndTable();
            }
        }
    }
}

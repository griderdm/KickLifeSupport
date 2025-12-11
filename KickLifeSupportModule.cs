using UnityEngine;

namespace KickLifeSupport
{
    public partial class KickLifeSupportModule : PartModule
    {
        private const float UpdateInterval = 5f;

        [KSPField(isPersistant = true)]
        public float lowO2Time = 0f;
        [KSPField(isPersistant = true)]
        public float lowWaterTime = 0f;
        [KSPField(isPersistant = true)]
        public float lowFoodTime = 0f;
        [KSPField(isPersistant = true)]
        public float cabinCO2 = 0f;

        int wasteId = -1;

        /// <summary>
        /// The amount of air (in liters) available per kerbal.
        /// </summary>
        const double airPerSeat = 2000;

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "Status", groupName = "KICKLS", groupDisplayName = "Life Support")]
        public string lsStatus = "Nominal";

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "CO2 Level", groupName = "KICKLS", groupDisplayName = "Life Support", guiFormat = "P1")]
        public float co2Level = 0f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Scrubber", groupName = "KICKLS", groupDisplayName = "Life Support"), UI_Toggle(disabledText = "Off", enabledText = "On")]
        public bool scrubberEnabled = true;

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "Scrubber Status", groupName = "KICKLS", groupDisplayName = "Life Support")]
        public string scrubberStatus = "On";

        /// <summary>
        /// This is the amount of CO2 scrubbed from the cabin on the last frame.
        /// We'll use this to calculate the scrubber's exothermic release.
        /// </summary>
        public double lastScrubRate = 0;

        public override void OnStart(StartState state)
        {
            PartResourceDefinition wasteDef = PartResourceLibrary.Instance.GetDefinition("Waste");
            if (wasteDef != null) wasteId = wasteDef.id;

            if (HighLogic.LoadedSceneIsFlight)
            {
                if (cabinTemp == 0 || cabinTemp < 200)
                {
                    cabinTemp = (float)(KToC(part.temperature));
                }
            }
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || !vessel.loaded) return;

            if (cabinTemp == 0 || cabinTemp < 200)
            {
                cabinTemp = (float)(KToC(part.temperature));
            }

            // Run thermal physics
            double dt = TimeWarp.fixedDeltaTime;
            double totalFlux = 0;
            double totalECRequest = 0;

            ModuleCommand cmd = part.FindModuleImplementing<ModuleCommand>();
            if (avionicsEnabled)
            {
                // Unlock control
                if (cmd != null && !cmd.isEnabled)
                {
                    cmd.isEnabled = true;
                    vessel.MakeActive();
                }

                totalECRequest += avionicsECRate * dt;
                totalFlux += avionicsHeat;

                // Check if SAS is enabled
                if (vessel.ActionGroups[KSPActionGroup.SAS])
                {
                    totalECRequest += sasECRate * dt;
                    totalFlux += sasHeat;
                }

                if (vessel.ActionGroups[KSPActionGroup.RCS])
                {
                    totalECRequest += rcsECRate * dt;
                    totalFlux += rcsHeat;
                }
            }
            else
            {
                // Lock controls
                if (cmd != null && cmd.isEnabled)
                    cmd.isEnabled = false;

                // We shouldn't turn off SAS/RCS globally for the vessel based on this one control point
                if (!VesselHasActiveCommand(vessel))
                {
                    if (vessel.ActionGroups[KSPActionGroup.SAS])
                        vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                    if (vessel.ActionGroups[KSPActionGroup.RCS])
                        vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, false);

                }
            }

            // Body heat
            int crewCount = part.protoModuleCrew.Count;
            if (crewCount > 0 && KickLifeSupportScenario.Instance != null)
            {
                totalFlux += (crewCount * KickLifeSupportScenario.Instance.kerbalHeat);
            }

            // Scrubber
            if (lastScrubRate > 0)
            {
                totalFlux += (lastScrubRate * liohReactionHeatPerUnit);
            }

            RunThermalLogic(ref totalFlux, ref totalECRequest);
            ApplyHeatFluxToCabinAir(totalFlux);
            if (totalECRequest > 0) part.RequestResource("ElectricCharge", totalECRequest);

            // Heat the hull from the inside
            double airToHullFlux = (cabinTemp - KToC(part.temperature)) * wallCoupling;
            part.AddThermalFlux(airToHullFlux);

            if (KickLifeSupportScenario.Instance != null)
            {
                LifeSupportStatus data = KickLifeSupportScenario.Instance.GetData(vessel.id);
                lsStatus = data.lsStatus;
                cabinCO2 = data.cabinCO2;
                co2Level = (float)(cabinCO2 / (vessel.GetCrewCapacity() * airPerSeat));
                heatFlux = (float)totalFlux;
            }
        }

        #region Scrubber Handling
        /// <summary>
        /// Allows the user to replace the lithium hydroxide canister
        /// </summary>
        [KSPEvent(guiActive = true, guiName = "Reload Scrubber", groupName = "KICKLS", groupDisplayName = "Life Support")]
        public void ReloadScrubber()
        {
            string cartridgePartName = "KickLSLiOHCartridge";
            double cartridgeVolume = 3;

            PartResource lioh = part.Resources["LithiumHydroxide"];
            if (lioh.amount >= lioh.maxAmount * 0.95)
            {
                ScreenMessages.PostScreenMessage("Scrubber is already full.", 3f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            if (ConsumePartFromInventory(cartridgePartName))
            {
                double waste = cartridgeVolume - (lioh.maxAmount - lioh.amount);
                lioh.amount = lioh.maxAmount;
                part.RequestResource(wasteId, -waste);    // Throw away the old cartridge
                ScreenMessages.PostScreenMessage("Scrubber reloaded.", 3f, ScreenMessageStyle.UPPER_CENTER);
            }
            else
            {
                ScreenMessages.PostScreenMessage("No LiOH Cartridges found in Inventory.", 3f, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        bool ConsumePartFromInventory(string partName)
        {
            foreach (Part p in vessel.parts)
            {
                ModuleInventoryPart inventory = p.FindModuleImplementing<ModuleInventoryPart>();
                if (inventory != null)
                {
                    for (int i = 0; i < inventory.InventorySlots; i++)
                    {
                        if (inventory.storedParts.ContainsKey(i))
                        {
                            StoredPart item = inventory.storedParts[i];

                            if (item.partName == partName)
                            {
                                if (item.quantity > 1)
                                {
                                    item.quantity--;
                                }
                                else
                                {
                                    inventory.storedParts.Remove(i);
                                }

                                MonoUtilities.RefreshPartContextWindow(p);
                                GameEvents.onVesselChange.Fire(this.vessel);
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }
        #endregion

        #region Avionics Helpers
        private bool VesselHasActiveCommand(Vessel v)
        {
            foreach (Part p in v.parts)
            {
                if (p == this.part) continue;

                var commands = p.FindModulesImplementing<ModuleCommand>();
                foreach (var cmd in commands)
                {
                    if (IsCommandFunctional(cmd))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool IsCommandFunctional(ModuleCommand cmd)
        {
            if (!cmd.isEnabled) return false;
            if (cmd.part.protoModuleCrew.Count < cmd.minimumCrew) return false;
            if (cmd.hibernation) return false;

            return true;
        }
        #endregion
    }
}
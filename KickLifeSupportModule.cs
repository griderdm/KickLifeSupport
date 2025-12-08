using System.CodeDom;
using System.Xml.Schema;
using UnityEngine;

namespace KickLifeSupport
{
    public class KickLifeSupportModule : PartModule
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

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Climate Control", groupName = "KICKLS", groupDisplayName = "Life Support"), UI_Toggle(disabledText = "Off", enabledText = "On")]
        public bool climateControlEnabled = true;

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "Climate Control Status", groupName = "KICKLS", groupDisplayName = "Life Support")]
        public string climateControlStatus = "On";

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "Cabin Heater", groupName = "KICKLS", groupDisplayName = "Life Support")]
        public string heaterStatus = "Off";

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "Cabin Temp", groupName = "KICKLS", groupDisplayName = "Life Support", guiFormat = "F1", guiUnits = "C")]
        public float cabinTemp = 0f;

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "System Heat Output", groupName = "KICKLS", groupDisplayName = "Life Support", guiFormat = "F3", guiUnits = " kW")]
        public float heatFlux = 0;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Avionics", groupName = "KICKLS", groupDisplayName = "Life Support"), UI_Toggle(disabledText = "Off", enabledText = "On")]
        public bool avionicsEnabled = true;

        [KSPField] public float avionicsECRate = 0.2f;
        [KSPField] public float avionicsHeat = 0.2f;
        [KSPField] public float sasECRate = 0.1f;
        [KSPField] public float sasHeat = 0.1f;
        [KSPField] public float rcsECRate = 0.1f;
        [KSPField] public float rcsHeat = 0.1f;
        [KSPField] public float systemECRate = 0.03f;
        [KSPField] public float systemHeat = 0.03f;
        [KSPField] public float heaterECRate = 0.5f;
        [KSPField] public float heaterHeat = 0.5f;

        const double thermostatTemp = 22;     // 22c
        const double thermostatDeadband = 2;  // 2c;

        /// <summary>
        /// This is the amount of CO2 scrubbed from the cabin on the last frame.
        /// We'll use this to calculate the scrubber's exothermic release.
        /// </summary>
        public double lastScrubRate = 0;

        /// <summary>
        /// The amount of heat released per liter of CO2 reacted with LiOH at STP.
        /// Based on 21.4 kcal per mole of CO2 absorbed
        /// </summary>
        const double liohReactionHeatPerUnit = 4.0;

        public bool isHeaterActive = false;

        public override void OnStart(StartState state)
        {
            PartResourceDefinition wasteDef = PartResourceLibrary.Instance.GetDefinition("Waste");
            if (wasteDef != null) wasteId = wasteDef.id;
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || !vessel.loaded) return;

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

            // Climate control
            if (climateControlEnabled)
            {
                totalFlux += systemHeat;
                float temp = (float)KToC(part.temperature);

                if (temp < (thermostatTemp - (thermostatDeadband / 2)))
                {
                    heaterStatus = "Active";
                    totalFlux += heaterHeat;
                    isHeaterActive = true;
                }
                else if (temp > (thermostatTemp + (thermostatDeadband / 2)))
                {
                    heaterStatus = "Standby";
                    isHeaterActive = false;
                }
            }
            else
            {
                heaterStatus = "Disabled";
                isHeaterActive = false;
            }

            // Apply heat
            if (totalFlux > 0.001) part.AddThermalFlux(totalFlux);
            if (totalECRequest > 0) part.RequestResource("ElectricCharge", totalECRequest);

            if (KickLifeSupportScenario.Instance != null)
            {
                LifeSupportStatus data = KickLifeSupportScenario.Instance.GetData(vessel.id);
                lsStatus = data.lsStatus;
                cabinCO2 = data.cabinCO2;
                co2Level = (float)(cabinCO2 / (vessel.GetCrewCapacity() * airPerSeat));
                cabinTemp = (float)KToC(part.temperature);
                heatFlux = (float)totalFlux;
            }
        }

        double KToC(double k) { return k - 273.15; }
        double CToK(double c) { return c + 273.15; }

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
    }
}
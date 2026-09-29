using System.Diagnostics.Eventing.Reader;
using UnityEngine;

namespace KickLifeSupport
{
    public partial class KickLifeSupportModule : PartModule
    {
        KickLifeSupportSettings gameSettings;

        private const float UpdateInterval = 5f;

        #region Persistent Fields
        [KSPField(isPersistant = true)]
        public float lowO2Time = 0f;
        [KSPField(isPersistant = true)]
        public float lowWaterTime = 0f;
        [KSPField(isPersistant = true)]
        public float lowFoodTime = 0f;
        [KSPField(isPersistant = true)]
        public float cabinCO2 = 0f;
        #endregion

        #region Resource IDs
        int wasteId = -1;
        int liohId = -1;
        int ecId = -1;
        int o2Id = -1;
        #endregion

        /// <summary>
        /// The amount of air (in liters) available per kerbal.
        /// </summary>
        internal const double airPerSeat = 2000;

        #region Module Fields
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Scrubber Type", groupName = "KICKLS", groupDisplayName = "Life Support")]
        [UI_Toggle(disabledText = "LiOH", enabledText = "CDRA")]
        public bool isCDRA = false;

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "Status", groupName = "KICKLS", groupDisplayName = "Life Support")]
        public string lsStatus = "Nominal";

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "CO2 Level", groupName = "KICKLS", groupDisplayName = "Life Support", guiFormat = "P1")]
        public float co2Level = 0f;

        //[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Cabin Pressure", groupName = "KICKLS", groupDisplayName = "Life Support", guiFormat = "F1", guiUnits = " kPa")]
        public float cabinPressure = 101.325f;  // pressure in kPa
        // TODO: Implement cabin pressure simulation

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Scrubber", groupName = "KICKLS", groupDisplayName = "Life Support"), UI_Toggle(disabledText = "Off", enabledText = "On")]
        public bool scrubberEnabled = true;

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "Scrubber Status", groupName = "KICKLS", groupDisplayName = "Life Support")]
        public string scrubberStatus = "On";
        #endregion

        public override void OnStart(StartState state)
        {
            gameSettings = HighLogic.CurrentGame.Parameters.CustomParams<KickLifeSupportSettings>();

            PartResourceDefinition wasteDef = PartResourceLibrary.Instance.GetDefinition("Waste");
            if (wasteDef != null) wasteId = wasteDef.id;
            PartResourceDefinition liohDef = PartResourceLibrary.Instance.GetDefinition("LithiumHydroxide");
            if (liohDef != null) liohId = liohDef.id;
            PartResourceDefinition ecDef = PartResourceLibrary.Instance.GetDefinition("ElectricCharge");
            if (ecDef != null) ecId = ecDef.id;
            PartResourceDefinition o2Def = PartResourceLibrary.Instance.GetDefinition("Oxygen");
            if (o2Def != null) o2Id = o2Def.id;

            if (HighLogic.LoadedSceneIsFlight)
            {
                if (cabinTemp == 0 || cabinTemp < -200)
                {
                    cabinTemp = (float)(KToC(part.temperature));
                }
            }

            if (isCDRA)
            {
                Events["ReloadScrubber"].active = false;
                Events["ReloadScrubber"].guiActive = false;
                SetLiOHResource(false);
            }

            if (HighLogic.LoadedSceneIsEditor)
            {
                Fields["isCDRA"].uiControlEditor.onFieldChanged += (f, o) =>
                {
                    SetLiOHResource(!isCDRA);
                    Events["ReloadScrubber"].active = !isCDRA;
                    Events["ReloadScrubber"].guiActive = !isCDRA;
                };
            }
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || !vessel.loaded) return;

            LifeSupportStatus data;
            if (KickLifeSupportScenario.Instance != null)
                data = KickLifeSupportScenario.Instance.GetData(vessel.id);
            else
                return;

            if (cabinTemp == 0 || cabinTemp < -200)
            {
                cabinTemp = (float)(KToC(part.temperature));
            }

            double dt = TimeWarp.fixedDeltaTime;
            double totalFlux = 0;

            double avionicsEC = avionicsECRate * dt;
            double sasEC = sasECRate * dt;
            double rcsEC = rcsECRate * dt;

            bool lockControls = false;

            ModuleCommand cmd = part.FindModuleImplementing<ModuleCommand>();
            bool isHybernating = (cmd != null && cmd.hibernation);
            if (avionicsEnabled)
            {
                if (!isHybernating)
                {
                    lockControls = false;
                    if (part.RequestResource(ecId, avionicsEC) < avionicsEC * 0.99)
                    {
                        lockControls = true;
                    }
                    else
                    {
                        totalFlux += avionicsHeat;

                        if (vessel.ActionGroups[KSPActionGroup.SAS])
                        {
                            if (part.RequestResource(ecId, sasEC) < sasEC * 0.99)
                            {
                                vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                            }
                            else
                            {
                                totalFlux += sasHeat;
                            }
                        }

                        if (vessel.ActionGroups[KSPActionGroup.RCS])
                        {
                            if (part.RequestResource(ecId, rcsEC) < rcsEC * 0.99)
                            {
                                vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, false);
                            }
                            else
                            {
                                totalFlux += rcsHeat;
                            }
                        }
                    }
                }
                else
                {
                    if (part.RequestResource(ecId, avionicsEC * 0.1) < avionicsEC * 0.1 * 0.99)
                    {
                        lockControls = true;
                    }
                    else
                    {
                        lockControls = false;
                        totalFlux += avionicsHeat * 0.1;
                    }
                }
            }
            else
            {
                lockControls = true;
            }

            // Unlock control only once the avionics actually have power. Re-enabling the command
            // module before the EC check made it flip on and off every physics tick on an empty
            // battery, and vessel.MakeActive() resets the flight camera each time it is called.
            if (!lockControls && !isHybernating && cmd != null && !cmd.isEnabled)
            {
                cmd.isEnabled = true;
            }

            if (lockControls)
            {
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
            if (isCDRA)
            {
                if (data.lastCDRAScrubAmount > 0 && dt > 0)
                {
                    totalFlux += (data.lastCDRAScrubAmount / data.activeCDRAScrubberCount / dt) * cdraHeatPerUnit;
                }
            }
            else
            {
                if (data.lastLiOHScrubAmount > 0 && dt > 0)
                {
                    totalFlux += (data.lastLiOHScrubAmount / data.activeLiOHScrubberCount / dt) * liohReactionHeatPerUnit;
                }
            }

            if (gameSettings.useCabinTempSystem)
            {
                RunThermalLogic(ref totalFlux);

                // Heat the hull from the inside
                double airToHullFlux = (cabinTemp - KToC(part.temperature)) * wallCoupling;
                part.AddThermalFlux(airToHullFlux);
            }
            else
            {
                totalFlux = 0;
            }

            lsStatus = data.lsStatus;
            cabinCO2 = data.cabinCO2;
            co2Level = (float)(cabinCO2 / (vessel.GetCrewCapacity() * airPerSeat));
            heatFlux = (float)totalFlux;

            Events["EqualizeAtmosphere"].active = vessel.atmDensity > 0
                                               && vessel.mainBody.atmosphereContainsOxygen
                                               && VesselHasIntakeAir();
        }

        #region Scrubber Handling

        void SetLiOHResource(bool enabled)
        {
            if (liohId == -1) return;
            PartResource lioh = part.Resources.Get(liohId);
            if (lioh == null) return;

            if (enabled)
            {
                PartResource prefab = part.partInfo?.partPrefab?.Resources.Get(liohId);
                double restore = prefab != null ? prefab.maxAmount : 0.5;
                lioh.maxAmount = restore;
                lioh.amount = restore;
            }
            else
            {
                lioh.amount = 0;
                lioh.maxAmount = 0;
            }
        }

        /// <summary>
        /// Allows the user to replace the lithium hydroxide canister
        /// </summary>
        [KSPEvent(guiActive = true, guiName = "Reload Scrubber", groupName = "KICKLS", groupDisplayName = "Life Support")]
        public void ReloadScrubber()
        {
            string cartridgePartName = "KickLSLiOHCartridge";
            double cartridgeVolume = 1.5;

            PartResource lioh = part.Resources.Get(liohId);
            PartResource waste = part.Resources.Get(wasteId);
            if (lioh.amount >= lioh.maxAmount * 0.95)
            {
                ScreenMessages.PostScreenMessage("Scrubber is already full.", 3f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            double liOHToAdd = lioh.maxAmount - lioh.amount;
            double wasteToStore = cartridgeVolume - liOHToAdd;

            if (waste != null)
            {
                if ((waste.maxAmount - waste.amount) < wasteToStore)
                {
                    ScreenMessages.PostScreenMessage("Cannot reload: Waste storage is full!", 3f, ScreenMessageStyle.UPPER_CENTER);
                    return;
                }
            }

            if (ConsumePartFromInventory(cartridgePartName))
            {
                lioh.amount = lioh.maxAmount;
                part.RequestResource(wasteId, -wasteToStore);    // Throw away the old cartridge
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

        #region Cabin Pressure Relief Valve
        [KSPEvent(guiActive = true, guiName = "Cabin Pressure Relief Valve", groupName = "KICKLS", groupDisplayName = "Life Support")]
        public void EqualizeAtmosphere()
        {
            foreach (Part p in vessel.parts)
            {
                foreach (PartResource r in p.Resources)
                {
                    if (r.info.id == o2Id) r.amount = r.maxAmount;
                }
            }

            if (KickLifeSupportScenario.Instance != null)
            {
                LifeSupportStatus data = KickLifeSupportScenario.Instance.GetData(vessel.id);
                data.cabinCO2 = 0;
            }
            foreach (KickLifeSupportModule m in vessel.FindPartModulesImplementing<KickLifeSupportModule>())
            {
                m.cabinCO2 = 0;
            }

            cabinTemp = (float)KToC(vessel.externalTemperature);
            cabinPressure = (float)vessel.staticPressurekPa;
            ScreenMessages.PostScreenMessage("Cabin Air Equalized", 3f, ScreenMessageStyle.UPPER_CENTER);
        }

        bool VesselHasIntakeAir()
        {
            int intakeAirId = PartResourceLibrary.Instance.GetDefinition("IntakeAir")?.id ?? -1;
            if (intakeAirId == -1) return false;
            foreach (Part p in vessel.parts)
            {
                foreach (PartResource r in p.Resources)
                {
                    if (r.info.id == intakeAirId && r.amount > 0) return true;
                }
            }

            return false;
        }
        #endregion
    }
}
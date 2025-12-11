using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace KickLifeSupport
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, GameScenes.FLIGHT, GameScenes.TRACKSTATION, GameScenes.SPACECENTER)]
    public class KickLifeSupportScenario : ScenarioModule
    {
        const double epsilon = 0.00000001;

        public static KickLifeSupportScenario Instance { get; private set; }

        public Dictionary<Guid, LifeSupportStatus> database = new Dictionary<Guid, LifeSupportStatus>();

        /// <summary>
        /// The total amount of air in liters per seat
        /// </summary>
        const double airPerSeat = 2000;
        /// <summary>
        /// The CO2 concentration (in liters) threshold for warning. This is 3% of airPerKerbal.
        /// </summary>
        const double co2Warning = 0.03;
        /// <summary>
        /// The CO2 concentration (in liters) threshold for fatality. This is 10% of airPerKerbal.
        /// </summary>
        const double co2Fatal = 0.1;
        /// <summary>
        /// The current cabin CO2 amount
        /// </summary>
        float cabinCO2 = 0f;

        #region Resource IDs
        int o2Id = -1;
        int co2Id = -1;
        int foodId = -1;
        int waterId = -1;
        int wasteId = -1;
        int wasteWaterId = -1;
        int lithiumHydroxideId = -1;
        int electricChargeId = -1;
        #endregion

        #region Rates
        // Resource Rates
        float o2RequestRate;
        float co2RequestRate;
        float scrubberRequestRate;
        float foodRequestRate;
        float waterRequestRate;
        float wasteRequestRate;
        float wasteWaterRequestRate;
        float lithiumHydroxideRequestRate;

        // EC Rates
        float scrubberECRequestRate;

        // Grace Periods
        float graceO2;
        float graceWater;
        float graceFood;
        float graceClimate;
        float graceTemp;

        // Heat Generation
        public float kerbalHeat;

        const double minSafeTemp = 5;   // 5c
        const double maxSafeTemp = 45;  // 45c

        #endregion

        public override void OnAwake()
        {
            Instance = this;
            Debug.Log("[KICKLS] Scenario Module Awake");

            GetSettings();
            GetResourceIds();

            Debug.Log($"[KICKLS] O2 ID: {o2Id} | Rate: {o2RequestRate}");
            Debug.Log($"[KICKLS] Water ID: {waterId} | Rate: {waterRequestRate}");
            Debug.Log($"[KICKLS] Food ID: {foodId} | Rate: {foodRequestRate}");

            if (o2Id == -1) Debug.LogError("[KICKLS] CRITICAL: Oxygen Resource ID not found!");
            if (o2RequestRate <= 0) Debug.LogError("[KICKLS] CRITICAL: Oxygen Rate is 0!");
        }

        public void FixedUpdate()
        {
            double currentTime = Planetarium.GetUniversalTime();

            foreach (Vessel v in FlightGlobals.Vessels)
            {
                if (!IsValidVessel(v)) continue;

                LifeSupportStatus data = GetData(v.id);

                // Initialize
                if (data.lastUpdateTime == 0)
                {
                    data.lastUpdateTime = currentTime;
                    continue;
                }

                double deltaTime = currentTime - data.lastUpdateTime;

                if (!v.loaded && deltaTime < 1.0) continue;
                data.lastUpdateTime = currentTime;

                // If time went backwards or there was a big spike, reset.
                if (deltaTime < 0) return;

                int liveCrew = GetLiveCrew(v);
                int crewCapacity = GetCrewCapacity(v);

                float totalCabinCO2 = GetTotalCO2(v);
                data.cabinCO2 = totalCabinCO2;

                if (v == FlightGlobals.ActiveVessel)
                    Debug.Log($"[KICKLS] Processing Active Vessel: {v.vesselName} | Crew: {liveCrew} | dT: {deltaTime}");

                BreatheAir(v, data, deltaTime, liveCrew);
                RunScrubber(v, data, deltaTime, crewCapacity);
                RunClimateControl(v, data, deltaTime);
                EatFood(v, data, deltaTime, liveCrew);
                DrinkWater(v, data, deltaTime, liveCrew);
                MonitorTemperature(v, data, deltaTime);

                CheckConditions(data, v);

                // Redistribute CO2
                if (crewCapacity > 0)
                {
                    SetCabinCO2(v, data.cabinCO2, crewCapacity);
                }
            }
        }

        /// <summary>
        /// Checks if the vessel is valid for our purposes
        /// </summary>
        /// <param name="vessel"></param>
        /// <returns></returns>
        bool IsValidVessel(Vessel vessel)
        {
            if (vessel == null) return false;

            if ((vessel.vesselType == VesselType.Debris) ||
                (vessel.vesselType == VesselType.Flag) ||
                (vessel.vesselType == VesselType.SpaceObject) ||
                (vessel.vesselType == VesselType.Unknown))
            {
                return false;
            }

            // TODO: EVA life support?
            if (vessel.vesselType == VesselType.EVA) return false;

            if (vessel.state == Vessel.State.DEAD) return false;

            if (vessel.GetCrewCount() == 0) return false;

            return true;
        }

        #region Save/Load

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            CleanupDatabase();
            foreach (KeyValuePair<Guid, LifeSupportStatus> entry in database)
            {
                ConfigNode vesselNode = node.AddNode("VESSEL_DATA");
                vesselNode.AddValue("id", entry.Key.ToString());
                entry.Value.Save(vesselNode);
            }

            Debug.Log("[KICKLS] Saved " + database.Count + " vessels.");
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            database.Clear();

            ConfigNode[] vesselNodes = node.GetNodes("VESSEL_DATA");
            foreach (ConfigNode n in vesselNodes)
            {
                string idString = n.GetValue("id");
                if (string.IsNullOrEmpty(idString)) continue;
                Guid id = new Guid(idString);

                LifeSupportStatus data = new LifeSupportStatus();
                data.Load(n);

                database.Add(id, data);
            }

            Debug.Log("[KICKLS] Loaded " + database.Count + " vessels.");
        }

        #endregion

        #region Database Functions
        /// <summary>
        /// Gets a vessel's life support status. If the vessel doesn't have one, make one.
        /// </summary>
        /// <param name="vesselId"></param>
        /// <returns></returns>
        public LifeSupportStatus GetData(Guid vesselId)
        {
            if (database.ContainsKey(vesselId))
            {
                return database[vesselId];
            }

            // If the ship is just launched, undocked, etc
            LifeSupportStatus newStatus = new LifeSupportStatus();
            database.Add(vesselId, newStatus);
            return newStatus;
        }

        void CleanupDatabase()
        {
            List<Guid> idsToRemove = new List<Guid>();

            foreach (Guid id in database.Keys)
            {
                Vessel v = FlightGlobals.FindVessel(id);

                // 1. If v is null, the ship doesn't exist anymore (Terminated/Recovered). DELETE.
                if (v == null)
                {
                    idsToRemove.Add(id);
                    continue;
                }

                // 2. If it's a Flag or Asteroid, we never want to save data for it. DELETE.
                // (Note: We DO keep Debris, in case you dock to it later to salvage supplies)
                if (v.vesselType == VesselType.Flag || v.vesselType == VesselType.SpaceObject || v.vesselType == VesselType.Unknown)
                {
                    idsToRemove.Add(id);
                    continue;
                }

                // 3. DO NOT check Crew Count here. Keep data for empty ships!
            }

            foreach (Guid id in idsToRemove)
            {
                database.Remove(id);
            }
        }

        #endregion

        #region Settings & Resources

        int GetSafeId(string name)
        {
            PartResourceDefinition def = PartResourceLibrary.Instance.GetDefinition(name);
            return (def != null) ? def.id : -1;
        }

        void GetResourceIds()
        {
            o2Id = GetSafeId("Oxygen");
            co2Id = GetSafeId("CarbonDioxide");
            foodId = GetSafeId("Food");
            waterId = GetSafeId("Water");
            wasteId = GetSafeId("Waste");
            wasteWaterId = GetSafeId("WasteWater");
            lithiumHydroxideId = GetSafeId("LithiumHydroxide");
            electricChargeId = GetSafeId("ElectricCharge");
        }

        void GetSettings()
        {
            ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("KICKLS_SETTINGS");
            if (nodes.Length == 0)
            {
                Debug.LogError("[KickLifeSupport] No KICKLS_SETTINGS found");

                // --- DEBUG ---
                ScreenMessages.PostScreenMessage("[KICKLS] ERROR: Settings not found!", 10f, ScreenMessageStyle.UPPER_CENTER);
                // -------------

                this.enabled = false;
                return;
            }

            ConfigNode settings = nodes[0];
            o2RequestRate = GetValue(settings, "OXYGEN_RATE");
            co2RequestRate = GetValue(settings, "CO2_RATE");
            scrubberRequestRate = GetValue(settings, "SCRUBBER_RATE");
            foodRequestRate = GetValue(settings, "FOOD_RATE");
            waterRequestRate = GetValue(settings, "WATER_RATE");
            wasteRequestRate = GetValue(settings, "WASTE_RATE");
            wasteWaterRequestRate = GetValue(settings, "WASTEWATER_RATE");
            lithiumHydroxideRequestRate = GetValue(settings, "LITHIUMHYDROXIDE_RATE");
            scrubberECRequestRate = GetValue(settings, "SCRUBBER_EC_RATE");

            graceO2 = GetValue(settings, "GRACE_OXYGEN");
            graceFood = GetValue(settings, "GRACE_FOOD");
            graceWater = GetValue(settings, "GRACE_WATER");
            graceClimate = GetValue(settings, "GRACE_CLIMATE");
            graceTemp = GetValue(settings, "GRACE_TEMP");

            kerbalHeat = GetValue(settings, "KERBAL_HEAT");
        }

        float GetValue(ConfigNode node, string key)
        {
            if (float.TryParse(node.GetValue(key), out float value))
            {
                return value;
            }
            Debug.LogError("[KickLifeSupport] Invalid value for " + key);
            return 0f;
        }

        float GetTotalCO2(Vessel v)
        {
            float totalCo2 = 0;

            if (v.loaded)
            {
                // --- LOADED VESSEL ---
                List<KickLifeSupportModule> modules = v.FindPartModulesImplementing<KickLifeSupportModule>();

                foreach (KickLifeSupportModule m in modules)
                {
                    totalCo2 += m.cabinCO2;
                }
            }
            else
            {
                // --- UNLOADED VESSEL ---

                foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
                {
                    foreach (ProtoPartModuleSnapshot m in p.modules)
                    {
                        if (m.moduleName == "KickLifeSupportModule")
                        {
                            if (float.TryParse(m.moduleValues.GetValue("cabinCO2"), out float val))
                            {
                                totalCo2 += val;
                            }
                        }
                    }
                }
            }

            return totalCo2;
        }

        void SetCabinCO2(Vessel v, float totalCO2, int crewCapacity)
        {
            if (v.loaded)
            {
                // --- LOADED VESSEL ---
                List<KickLifeSupportModule> modules = v.FindPartModulesImplementing<KickLifeSupportModule>();

                foreach (KickLifeSupportModule m in modules)
                {
                    float share = (float)(m.part.CrewCapacity / crewCapacity);
                    m.cabinCO2 = totalCO2 * share;
                }
            }
            else
            {
                // --- UNLOADED VESSEL ---

                foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
                {
                    if (p.partInfo == null || p.partInfo.partPrefab == null) continue;
                    int pCap = p.partInfo.partPrefab.CrewCapacity;
                    if (pCap == 0) continue;

                    foreach (ProtoPartModuleSnapshot m in p.modules)
                    {
                        if (m.moduleName == "KickLifeSupportModule")
                        {
                            float share = (float)(pCap / crewCapacity);
                            m.moduleValues.SetValue("cabinCO2", totalCO2 * share);
                        }
                    }
                }
            }
        }

        #endregion

        #region Simulation

        void BreatheAir(Vessel v, LifeSupportStatus status, double deltaTime, int crewCount)
        {
            (double co2Produced, double o2Ratio) resparation = ProcessConsumption(v, deltaTime, o2Id, o2RequestRate, co2Id, co2RequestRate, crewCount, false);

            Debug.Log($"[KICKLS] BreatheAir Result -> Produced: {resparation.co2Produced} | Ratio: {resparation.o2Ratio}");

            status.cabinCO2 += (float)resparation.co2Produced;
            if (resparation.o2Ratio < 1.0)
                status.lowO2Time += deltaTime;
            else
                status.lowO2Time = 0;
        }

        void RunScrubber(Vessel v, LifeSupportStatus status, double deltaTime, int totalCrewOnShip)
        {
            double totalCO2Removed = 0;
            double availableCO2 = status.cabinCO2;

            // Rates per seat
            double baseScrubRate = scrubberRequestRate * deltaTime;
            double baseEcRate = scrubberECRequestRate * deltaTime;
            double baseLiohRate = lithiumHydroxideRequestRate * deltaTime;

            // Calculate stiochiometric ratio
            double liohPerLiterCO2 = 0;
            if (scrubberRequestRate > 0)
                liohPerLiterCO2 = lithiumHydroxideRequestRate / scrubberRequestRate;

            if (v.loaded)
            {
                List<KickLifeSupportModule> modules = v.FindPartModulesImplementing<KickLifeSupportModule>();
                foreach (KickLifeSupportModule m in modules)
                {
                    m.lastScrubRate = 0;

                    // Is the scrubber on?
                    if (!m.scrubberEnabled)
                    {
                        m.scrubberStatus = "Off";
                        continue;
                    }

                    int partCapacity = m.part.CrewCapacity;
                    if (partCapacity == 0) partCapacity = 1;

                    // Check that there's enough power to spin the fans
                    double ecReq = baseEcRate * partCapacity;
                    (double amountConsumed, double ratio) ecRes = ConsumeResource(v, electricChargeId, ecReq);

                    if (ecRes.ratio < 0.99)
                    {
                        m.scrubberStatus = "No Power"; // <--- Set Local UI
                        continue;
                    }

                    // Check how much LiOH there is
                    double currentLiOH = 0;
                    PartResource lithiumHydroxide = m.part.Resources.Get(lithiumHydroxideId);
                    if (lithiumHydroxide != null) currentLiOH = lithiumHydroxide.amount;

                    // Get the maximum mechanical scrub rate
                    double maxMechanicalScrub = scrubberRequestRate * partCapacity * deltaTime;

                    // Calculate how much CO2 you can scrub given the LiOH amount
                    double maxChemicalScrub = 0;
                    if (liohPerLiterCO2 > 0)
                        maxChemicalScrub = currentLiOH * liohPerLiterCO2;

                    // Get the max amount of CO2 that can be scrubbed
                    double maxEnvironmentalScrub = availableCO2;

                    // Find out how much can actually get scrubbed. It'll the the smallest value of the three.
                    double actualScrub = Math.Min(maxMechanicalScrub, Math.Min(maxChemicalScrub, maxEnvironmentalScrub));

                    // Scrub!
                    if (actualScrub > epsilon)
                    {
                        double liOHToConsume = actualScrub * liohPerLiterCO2;
                        double liohTaken = m.part.RequestResource(lithiumHydroxideId, liOHToConsume);

                        if (liohTaken < liOHToConsume - epsilon)
                        {
                            m.scrubberStatus = "No LiOH"; // <--- Set Local UI
                        }
                        else
                        {
                            // Success
                            m.part.RequestResource(wasteId, -liohTaken); // Waste
                            totalCO2Removed += (baseScrubRate * partCapacity);
                            m.scrubberStatus = "Active"; // <--- Set Local UI
                        }

                        availableCO2 -= actualScrub;
                        if (deltaTime > 0)
                        {
                            m.lastScrubRate = actualScrub / deltaTime;
                        }
                    }
                    else
                    {
                        m.scrubberStatus = "Standby";
                    }
                }
            }
            else
            {
                // --- UNLOADED VESSEL ---
                // (We don't update UI strings here because no one can see them)

                foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
                {
                    bool isScrubberOn = false;
                    foreach (ProtoPartModuleSnapshot m in p.modules)
                    {
                        if (m.moduleName == "KickLifeSupportModule")
                        {
                            if (bool.TryParse(m.moduleValues.GetValue("scrubberEnabled"), out bool val) && val)
                            {
                                isScrubberOn = true;
                                break;
                            }
                        }
                    }

                    if (!isScrubberOn) continue;

                    int partCapacity = 1;
                    if (p.partInfo != null && p.partInfo.partPrefab != null)
                        partCapacity = p.partInfo.partPrefab.CrewCapacity;
                    if (partCapacity == 0) partCapacity = 1;

                    // 1. EC
                    double ecReq = baseEcRate * partCapacity;
                    (double amountConsumed, double ratio) ecRes = ConsumeResource(v, electricChargeId, ecReq);
                    if (ecRes.ratio < 0.99) continue;

                    // 2. LiOH
                    double liohReq = baseLiohRate * partCapacity;
                    double liohTaken = 0;

                    foreach (ProtoPartResourceSnapshot r in p.resources)
                    {
                        if (r.definition.id == lithiumHydroxideId)
                        {
                            if (r.amount >= liohReq)
                            {
                                r.amount -= liohReq;
                                liohTaken = liohReq;
                            }
                            else
                            {
                                liohTaken = r.amount;
                                r.amount = 0;
                            }
                            break;
                        }
                    }

                    if (liohTaken >= liohReq - epsilon)
                    {
                        // Waste
                        double wasteToAdd = liohTaken;
                        foreach (ProtoPartResourceSnapshot r in p.resources)
                        {
                            if (r.definition.id == wasteId)
                            {
                                double space = r.maxAmount - r.amount;
                                if (space >= wasteToAdd)
                                {
                                    r.amount += wasteToAdd;
                                    wasteToAdd = 0;
                                }
                                else
                                {
                                    r.amount = r.maxAmount;
                                    wasteToAdd -= space;
                                }
                            }
                        }
                        totalCO2Removed += (baseScrubRate * partCapacity);
                    }
                }
            }

            // Apply Total Scrubbing to Cabin
            status.cabinCO2 -= (float)totalCO2Removed;
            if (status.cabinCO2 < 0) status.cabinCO2 = 0;
        }

        void EatFood(Vessel v, LifeSupportStatus status, double deltaTime, int crewCount)
        {
            if (ProcessConsumption(v, deltaTime, foodId, foodRequestRate, wasteId, wasteRequestRate, crewCount, true).resourceARatio < 1.0)
                status.lowFoodTime += deltaTime;
            else
                status.lowFoodTime = 0;
        }

        /// <summary>
        /// Consumes water and makes wastewater
        /// </summary>
        /// <param name="deltaTime"></param>
        void DrinkWater(Vessel v, LifeSupportStatus status, double deltaTime, int crewCount)
        {
            if (ProcessConsumption(v, deltaTime, waterId, waterRequestRate, wasteWaterId, wasteWaterRequestRate, crewCount, true).resourceARatio < 1.0)
                status.lowWaterTime += deltaTime;
            else
                status.lowWaterTime = 0;
        }

        /// <summary>
        /// Calcualtes the cabin CO2 concentration as a percentage
        /// </summary>
        /// <param name="crewCount"></param>
        double CalculateCabinCO2(LifeSupportStatus status, int crewCount)
        {
            if (cabinCO2 < 0) cabinCO2 = 0;
            if (crewCount > 0 && airPerSeat > 0)
                return cabinCO2 / (float)(crewCount * airPerSeat);
            else
                return 0;
        }

        /// <summary>
        /// Runs climate control system (cabin heaters, fans, gylcol loop)
        /// </summary>
        /// <param name="v"></param>
        /// <param name="status"></param>
        /// <param name="deltaTime"></param>
        void RunClimateControl(Vessel v, LifeSupportStatus status, double deltaTime)
        {
            bool climateFailureDetected = false;

            if (v.loaded)
            {
                List<KickLifeSupportModule> modules = v.FindPartModulesImplementing<KickLifeSupportModule>();
                foreach (KickLifeSupportModule m in modules)
                {
                    int capacity = m.part.CrewCapacity;
                    if (capacity == 0) continue;

                    if (!m.climateControlEnabled)
                    {
                        m.climateControlStatus = "Off";
                        if (m.part.protoModuleCrew.Count > 0)
                            climateFailureDetected = true;
                        continue;
                    }

                    double ecReq = m.systemECRate * deltaTime;
                    if (m.isHeaterActive) ecReq += (m.heaterHeat * deltaTime);  // Heater EC usage is the same as the heater strength.
                    (double amountConsumed, double ratio) ecRes = ConsumeResource(v, electricChargeId, ecReq);

                    if (ecRes.ratio < 0.99)
                    {
                        m.climateControlStatus = "No Power";
                        if (m.part.protoModuleCrew.Count > 0)
                            climateFailureDetected = true;
                    }
                    else
                    {
                        m.climateControlStatus = "Nominal";
                    }
                }
            }
            else
            {
                foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
                {
                    if (p.partInfo == null || p.partInfo.partPrefab == null) continue;
                    int capacity = p.partInfo.partPrefab.CrewCapacity;
                    if (capacity == 0) continue;

                    double ecRate = 0.03;
                    bool isClimateOn = false;
                    foreach (ProtoPartModuleSnapshot m in p.modules)
                    {
                        if (m.moduleName == "KickLifeSupportModule")
                        {
                            string val = m.moduleValues.GetValue("climateControlEnabled");
                            if (val == null || (bool.TryParse(val, out bool bVal) && bVal))
                            {
                                isClimateOn = true;
                            }

                            val = m.moduleValues.GetValue("systemECRate");
                            if (val != null) double.TryParse(val, out ecRate);

                                break;
                        }
                    }

                    if (!isClimateOn)
                    {
                        if (p.HasCrew(null))
                            climateFailureDetected = true;
                        continue;
                    }

                    double ecReq = ecRate * capacity;
                    (double amountConsumed, double ratio) ecRes = ConsumeResource(v, electricChargeId, ecReq);
                    if (ecRes.ratio < 0.99)
                    {
                        if (p.HasCrew(null))
                            climateFailureDetected = true;
                    }
                }
            }

            if (climateFailureDetected)
            {
                if (status.lsStatus == "Nominal")
                {
                    status.lsStatus = "Climate Control Failure";
                }
            }
        }

        void MonitorTemperature(Vessel v, LifeSupportStatus status, double deltaTime)
        {
            if (!v.loaded)
            {
                status.tempRangeTime = 0;
                return;
            }

            bool tempIssueDetected = false;

            List<KickLifeSupportModule> modules = v.FindPartModulesImplementing<KickLifeSupportModule>();
            foreach (KickLifeSupportModule module in modules)
            {
                if (module.part.protoModuleCrew.Count == 0) continue;

                if (KToC(module.part.temperature) < minSafeTemp || KToC(module.part.temperature) > maxSafeTemp)
                {
                    tempIssueDetected = true;
                    break;
                }
            }

            if (tempIssueDetected)
            {
                status.tempRangeTime += deltaTime;
            }
            else
            {
                status.tempRangeTime = 0;
            }
        }

        /// <summary>
        /// Checks to see if the crew should die
        /// </summary>
        void CheckConditions(LifeSupportStatus status, Vessel v)
        {
            double co2Level = CalculateCabinCO2(status, GetCrewCapacity(v));

            // PRIORITY 1: CO2 (Immediate Death)
            if (co2Level >= co2Fatal)
            {
                KillCrew(v);
                status.lsStatus = "Fatal CO2 Levels";
                return;
            }
            else if (co2Level >= co2Warning)
            {
                status.lsStatus = "Dangerous CO2 Levels";
                return;
            }

            // PRIORITY 2: Oxygen (Fast Death)
            if (status.lowO2Time >= graceO2)
            {
                KillCrew(v);
                status.lsStatus = "Crew suffocated!";
                return;
            }
            else if (status.lowO2Time > 0)
            {
                status.lsStatus = $"Suffocating! ({graceO2 - status.lowO2Time:F0}s)";
                return;
            }

            if (status.lowClimateTime >= graceClimate)
            {
                KillCrew(v);
                status.lsStatus = "Crew suffocated from stagnant air!";
            }
            else if (status.lowClimateTime > 0)
            {
                status.lsStatus = $"Air Circulation Failed! ({graceClimate - status.lowClimateTime:F0}s)";
            }

            if (status.tempRangeTime >= graceTemp)
            {
                KillCrew(v);
                status.lsStatus = "Fatal temperature!";
            }
            else if (status.tempRangeTime > 0)
            {
                status.lsStatus = $"Cabin Temp Critical! ({graceClimate - status.tempRangeTime:F0}s)";
            }

            // PRIORITY 3: Water (Medium Death)
            if (status.lowWaterTime >= graceWater)
            {
                KillCrew(v);
                status.lsStatus = "Crew dehydrated!";
                return;
            }
            else if (status.lowWaterTime > 0)
            {
                status.lsStatus = $"Thirsty! ({graceWater - status.lowWaterTime:F0}s)";
                return;
            }

            // PRIORITY 4: Food (Slow Death)
            if (status.lowFoodTime >= graceFood)
            {
                KillCrew(v);
                status.lsStatus = "Crew starved!";
                return;
            }
            else if (status.lowFoodTime > 0)
            {
                status.lsStatus = $"Starving! ({graceFood - status.lowFoodTime:F0}s)";
                return;
            }

            // PRIORITY 5: Nominal
            status.lsStatus = "Nominal";
        }
        #endregion

        #region Resources

        /// <summary>
        /// Turns resourceA into resourceB based on crew count.
        /// </summary>
        /// <param name="resourceA">Input resource ID</param>
        /// <param name="resourceARate">Rate of input</param>
        /// <param name="resourceB">Output resource ID</param>
        /// <param name="resourceBRate">Rate of output</param>
        /// <param name="crewCount">Number of live crew</param>
        /// <param name="store">Determines whether to store the output or throw it away</param>
        /// <returns>Returns a Tuple with the amount of resource B produced and the ratio of resource A requested vs returned</returns>
        (double resourceBProduced, double resourceARatio) ProcessConsumption(Vessel v, double deltaTime, int resourceA, float resourceARate, int resourceB, float resourceBRate, int crewCount, bool store = true)
        {
            double rARequestRate = resourceARate * deltaTime * crewCount;
            if (rARequestRate <= epsilon) return (0, 1.0);

            (double consumed, double ratio) resultA  = ConsumeResource(v, resourceA, rARequestRate);
            Debug.Log($"[KICKLS] ConsumeResource returned: {resultA.consumed} (Ratio: {resultA.ratio})");

            double rBProduced = resultA.ratio * resourceBRate * deltaTime * crewCount;
            if (store)
            {
                double amountActuallyAdded = ProduceResource(v, resourceB, rBProduced);

            }
            return (rBProduced, resultA.ratio);
        }

        public double GetResourceTotal(Vessel v, int resourceID)
        {
            double total = 0;
            if (v.loaded)
            {
                foreach (Part p in v.parts)
                {
                    foreach (PartResource r in p.Resources)
                    {
                        if (r.info.id == resourceID) total += r.amount;
                    }
                }
            }
            else
            {
                foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
                {
                    foreach (ProtoPartResourceSnapshot r in p.resources)
                    {
                        if (r.definition.id == resourceID) total += r.amount;
                    }
                }
            }
            return total;
        }

        (double amountConsumed, double ratio) ConsumeResource(Vessel v, int resourceID, double amountToConsume)
        {
            if (amountToConsume <= epsilon) return (0, 1.0);

            double actuallyTaken = 0;

            // 1. Check the Resource Definition
            // We need to know if this resource obeys flow logic.
            PartResourceDefinition def = PartResourceLibrary.Instance.GetDefinition(resourceID);
            if (def == null) return (0, 0);

            // 2. Determine Strategy
            // If it flows (O2/EC), let KSP handle the plumbing (Valves/Crossfeed).
            // If it doesn't flow (LiOH/Waste), we must manually find it on the ship.
            bool useKspApi = (def.resourceFlowMode != ResourceFlowMode.NO_FLOW);

            if (v.loaded && useKspApi)
            {
                // --- STRATEGY A: KSP NATIVE FLOW (Loaded + Flowable) ---
                // This respects valves, fuel lines, and docking ports.
                if (v.rootPart != null)
                {
                    actuallyTaken = v.rootPart.RequestResource(resourceID, amountToConsume);
                }
            }
            else
            {
                // --- STRATEGY B: MANUAL ITERATION ---
                // Used for:
                // 1. Unloaded Vessels (Background processing)
                // 2. NO_FLOW Resources (LiOH, Waste, Food packets)
                //    (Simulates crew manually fetching items from any container)

                if (v.loaded)
                {
                    // Manual Iteration for LOADED vessels
                    foreach (Part p in v.parts)
                    {
                        if (p.Resources == null) continue;

                        foreach (PartResource r in p.Resources)
                        {
                            if (r.info.id == resourceID)
                            {
                                double available = r.amount;
                                double needed = amountToConsume - actuallyTaken;

                                if (available >= needed)
                                {
                                    r.amount -= needed;
                                    actuallyTaken += needed;
                                }
                                else
                                {
                                    r.amount = 0;
                                    actuallyTaken += available;
                                }
                            }
                            if (actuallyTaken >= amountToConsume - epsilon) break;
                        }
                        if (actuallyTaken >= amountToConsume - epsilon) break;
                    }
                }
                else
                {
                    // Manual Iteration for UNLOADED vessels (ProtoSnapshots)
                    foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
                    {
                        foreach (ProtoPartResourceSnapshot r in p.resources)
                        {
                            if (r.definition.id == resourceID)
                            {
                                double available = r.amount;
                                double needed = amountToConsume - actuallyTaken;

                                if (available >= needed)
                                {
                                    r.amount -= needed;
                                    actuallyTaken += needed;
                                }
                                else
                                {
                                    r.amount = 0;
                                    actuallyTaken += available;
                                }
                            }
                            if (actuallyTaken >= amountToConsume - epsilon) break;
                        }
                        if (actuallyTaken >= amountToConsume - epsilon) break;
                    }
                }
            }

            double ratio = (amountToConsume > 0) ? actuallyTaken / amountToConsume : 0;
            return (actuallyTaken, ratio);
        }

        /// <summary>
        /// Universal resource producer. Returns the amount actually added.
        /// </summary>
        double ProduceResource(Vessel v, int resourceID, double amountToProduce)
        {
            if (amountToProduce <= epsilon) return 0;

            PartResourceDefinition def = PartResourceLibrary.Instance.GetDefinition(resourceID);
            if (def == null) return 0;

            bool useKspApi = (def.resourceFlowMode != ResourceFlowMode.NO_FLOW);
            double actuallyAdded = 0;

            if (v.loaded && useKspApi)
            {
                // Use KSP plumbing for fluids (CO2, WasteWater).
                // Requesting a negative amount ADDS the resource.
                // The API returns the amount added as a negative number.
                double result = v.rootPart.RequestResource(resourceID, -amountToProduce);

                // Flip the sign back to positive for our return value
                actuallyAdded = -result;
            }
            else
            {
                // Manual fill for Unloaded OR NO_FLOW items (Solid Waste)
                double remainingToAdd = amountToProduce;

                if (v.loaded)
                {
                    foreach (Part p in v.parts)
                    {
                        foreach (PartResource r in p.Resources)
                        {
                            if (r.info.id == resourceID)
                            {
                                double space = r.maxAmount - r.amount;

                                if (space >= remainingToAdd)
                                {
                                    r.amount += remainingToAdd;
                                    remainingToAdd = 0;
                                }
                                else
                                {
                                    r.amount = r.maxAmount; // Fill it up
                                    remainingToAdd -= space;
                                }
                            }
                            if (remainingToAdd <= epsilon) break;
                        }
                        if (remainingToAdd <= epsilon) break;
                    }
                }
                else
                {
                    // Unloaded Logic (ProtoSnapshots)
                    foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
                    {
                        foreach (ProtoPartResourceSnapshot r in p.resources)
                        {
                            if (r.definition.id == resourceID)
                            {
                                double space = r.maxAmount - r.amount;

                                if (space >= remainingToAdd)
                                {
                                    r.amount += remainingToAdd;
                                    remainingToAdd = 0;
                                }
                                else
                                {
                                    r.amount = r.maxAmount;
                                    remainingToAdd -= space;
                                }
                            }
                            if (remainingToAdd <= epsilon) break;
                        }
                        if (remainingToAdd <= epsilon) break;
                    }
                }

                // Calculate what we managed to push in
                actuallyAdded = amountToProduce - remainingToAdd;
            }

            return actuallyAdded;
        }
        #endregion

        #region Crew Helpers

        int GetLiveCrew(Vessel v)
        {
            // KSP API handles counting crew for both Loaded and Unloaded vessels automatically
            return v.GetCrewCount();
        }

        int GetCrewCapacity(Vessel v)
        {
            if (v.loaded)
            {
                return v.GetCrewCapacity();
            }
            else
            {
                // For background ships, we must sum the capacity of the parts manually
                int capacity = 0;
                foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
                {
                    // partPrefab is the "Master Copy" of the part which holds the static stats
                    if (p.partInfo != null && p.partInfo.partPrefab != null)
                    {
                        capacity += p.partInfo.partPrefab.CrewCapacity;
                    }
                }
                return capacity;
            }
        }

        void KillCrew(Vessel v)
        {
            // 1. Get a copy of the crew list
            // (We need a copy because we are about to modify the vessel's crew list)
            List<ProtoCrewMember> crewList = new List<ProtoCrewMember>(v.GetVesselCrew());

            bool anyoneDied = false;

            foreach (ProtoCrewMember crew in crewList)
            {
                // 2. Mark them as Dead in the main roster
                crew.rosterStatus = ProtoCrewMember.RosterStatus.Dead;
                anyoneDied = true;

                // 3. Remove them from the vessel structure
                if (v.loaded)
                {
                    // LOADED: Remove from the physical seat
                    // Try to find the part via the Kerbal's seat reference first
                    if (crew.seat != null && crew.seat.part != null)
                    {
                        crew.seat.part.RemoveCrewmember(crew);
                    }
                    else
                    {
                        // Fallback: Check all parts
                        foreach (Part p in v.parts)
                        {
                            if (p.protoModuleCrew.Contains(crew))
                            {
                                p.RemoveCrewmember(crew);
                                break;
                            }
                        }
                    }
                }
                else
                {
                    // UNLOADED: Remove from the Save Data (ProtoSnapshot)
                    foreach (ProtoPartSnapshot pps in v.protoVessel.protoPartSnapshots)
                    {
                        if (pps.HasCrew(crew.name))
                        {
                            pps.RemoveCrew(crew.name);
                            break; 
                        }
                    }
                }
            }

            // 4. Update the game state if the player is looking at it
            if (anyoneDied && v.loaded)
            {
                GameEvents.onVesselChange.Fire(v);
            }
        }

        #endregion

        #region Temp Helpers
        double KToC(double k) { return k - 273.15; }
        double CToK(double c) { return c + 273.15; }
        #endregion

        #region I/O

        #endregion
    }
}

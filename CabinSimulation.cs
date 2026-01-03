using System;
using System.Net;
using UnityEngine;

namespace KickLifeSupport
{
    public partial class KickLifeSupportModule : PartModule, IAnalyticTemperatureModifier
    {
        #region Thermal GUI Fields
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Climate Control", groupName = "KICKLS", groupDisplayName = "Life Support"), UI_Toggle(disabledText = "Off", enabledText = "On")]
        public bool climateControlEnabled = true;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Auto-deploy Radiators", groupName = "KICKLS", groupDisplayName = "Life Support"), UI_Toggle(disabledText = "Off", enabledText = "On")]
        public bool autoDeployRadiators = true;

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "Climate Control Status", groupName = "KICKLS", groupDisplayName = "Life Support")]
        public string climateControlStatus = "On";

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Thermostat Setting", groupName = "KICKLS", groupDisplayName = "Life Support")]
        [UI_FloatRange(minValue = 10f, maxValue = 30f, stepIncrement = 0.5f, scene = UI_Scene.All)]
        public float thermostatTemp = 22f;     // 22c

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Cabin Temp", groupName = "KICKLS", groupDisplayName = "Life Support", guiFormat = "F1", guiUnits = "C")]
        public float cabinTemp = 0f;

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "Cabin Heater", groupName = "KICKLS", groupDisplayName = "Life Support")]
        public string heaterStatus = "Off";

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "Auto Radiators", groupName = "KICKLS", groupDisplayName = "Life Support")]
        public string radiatorStatus = "Off";

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Heater Strength", groupName = "KICKLS", groupDisplayName = "Life Support")]
        [UI_FloatRange(minValue = 0f, maxValue = 40f, stepIncrement = 0.5f, scene = UI_Scene.All)]
        public float heaterHeat = 0.5f;

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "Active Flux", groupName = "KICKLS", groupDisplayName = "Life Support", guiFormat = "F3", guiUnits = " kW")]
        public float heatFlux = 0;
        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "Passive Flux", groupName = "KICKLS", groupDisplayName = "Life Support", guiFormat = "F3", guiUnits = " kW")]
        public float passiveFlux = 0;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Avionics", groupName = "KICKLS", groupDisplayName = "Life Support"), UI_Toggle(disabledText = "Off", enabledText = "On")]
        public bool avionicsEnabled = true;
        #endregion

        #region Thermal Rates
        [KSPField] public float avionicsECRate = 0.2f;
        [KSPField] public float avionicsHeat = 0.2f;
        [KSPField] public float sasECRate = 0.1f;
        [KSPField] public float sasHeat = 0.1f;
        [KSPField] public float rcsECRate = 0.1f;
        [KSPField] public float rcsHeat = 0.1f;
        [KSPField] public float systemECRate = 0.03f;
        [KSPField] public float systemHeat = 0.03f;
        #endregion

        #region Thermal Constants
        /// <summary>
        /// The total deadband within which the thermostat operates.
        /// </summary>
        internal const double thermostatDeadband = 2;  // 2c;
        /// <summary>
        /// The amount of heat released per liter of CO2 reacted with LiOH at STP.
        /// Based on 21.4 kcal per mole of CO2 absorbed
        /// </summary>
        internal const double liohReactionHeatPerUnit = 4.0;
        internal const double airSpecificHeat = 1005.0;
        internal const double airDensity = 0.001225;
        internal const double wallCoupling = 0.003;
        #endregion

        #region Thermal Control
        public bool isHeaterActive = false;
        public bool isAutoRadiatorActive = false;
        bool canAutoRadiator = false;
        #endregion

        #region Thermal Helpers
        double KToC(double k) { return k - 273.15; }
        double CToK(double c) { return c + 273.15; }

        void ActivateRadiators()
        {
            foreach (Part p in vessel.parts)
            {
                KickRadiatorControlModule lsRadiator = p.FindModuleImplementing<KickRadiatorControlModule>();
                if (lsRadiator != null)
                {
                    if (!lsRadiator.allowAutoDeploy) continue;
                }
                else
                {
                    continue;
                }

                ModuleActiveRadiator activeRadiator = p.FindModuleImplementing<ModuleActiveRadiator>();
                if (activeRadiator != null)
                {
                    activeRadiator.IsCooling = true;
                }

                ModuleDeployableRadiator deployableRadiator = p.FindModuleImplementing<ModuleDeployableRadiator>();
                if (deployableRadiator != null)
                {
                    deployableRadiator.Extend();
                }
            }
        }

        void DeactiveRadiators()
        {
            foreach (Part p in vessel.parts)
            {
                KickRadiatorControlModule lsRadiator = p.FindModuleImplementing<KickRadiatorControlModule>();
                if (lsRadiator != null)
                {
                    if (!lsRadiator.allowAutoDeploy) continue;
                }
                else
                {
                    continue;
                }

                ModuleActiveRadiator activeRadiator = p.FindModuleImplementing<ModuleActiveRadiator>();
                if (activeRadiator != null)
                {
                    activeRadiator.IsCooling = false;
                }

                ModuleDeployableRadiator deployableRadiator = p.FindModuleImplementing<ModuleDeployableRadiator>();
                if (deployableRadiator != null)
                {
                    deployableRadiator.Retract();
                }
            }
        }
        #endregion

        #region On-Rails Variables
        double cachedAnalyticTemp;
        #endregion

        private float debugTimer = 0f;
        void RunThermalLogic(ref double totalFlux)
        {
            double currentAirTempK = CToK(cabinTemp);
            double dt = TimeWarp.fixedDeltaTime;

            // Passive Simulation
            // Heat moves from Air -> Hull (Cooling) or Hull -> Air (Warming)
            double hullTemp = part.temperature;
            double passiveChange = (hullTemp - currentAirTempK) * wallCoupling * dt;
            currentAirTempK += passiveChange;

            double systemEC = systemECRate * TimeWarp.fixedDeltaTime;
            double heaterEC = heaterHeat *  TimeWarp.fixedDeltaTime;

            if (TimeWarp.CurrentRate > 100f)
            {
                if (climateControlEnabled)
                {
                    if (part.RequestResource(ecId, systemEC) >= systemEC * 0.99)
                    {
                        cabinTemp = thermostatTemp;
                    }
                }

                return;
            }

            // Climate Control
            if (climateControlEnabled)
            {
                if (part.RequestResource(ecId, systemEC) >= systemEC * 0.99)
                {
                    totalFlux += systemHeat;

                    // FIX: Thermostat checks the PREDICTED AIR TEMP, not the Wall Temp!
                    float currentAirC = (float)KToC(currentAirTempK);

                    float lowThreshold = (float)(thermostatTemp - (thermostatDeadband / 2));
                    float highThreshold = (float)(thermostatTemp + (thermostatDeadband / 2));
                    float excessiveThreshold = (float)(thermostatTemp + (thermostatDeadband * 4));

                    // Thermostat Decision
                    if (currentAirC < lowThreshold)
                    {
                        heaterStatus = "Active";
                        isHeaterActive = true;
                        canAutoRadiator = false;
                    }
                    else if (currentAirC > excessiveThreshold)
                    {
                        heaterStatus = "Standby";
                        isHeaterActive = false;
                        canAutoRadiator = true;
                    }
                    else if (currentAirC > highThreshold)
                    {
                        heaterStatus = "Standby";
                        isHeaterActive = false;
                        canAutoRadiator = false;
                    }

                    // Apply Heater
                    if (isHeaterActive)
                    {
                        if (part.RequestResource(ecId, heaterEC) < heaterEC * 0.99)
                        {
                            heaterStatus = "No Power";
                        }
                        else
                        {
                            heaterStatus = "Active";
                            totalFlux += heaterHeat;
                        }
                    }

                    // Radiator Logic
                    if (autoDeployRadiators)
                    {
                        if (canAutoRadiator)
                        {
                            ActivateRadiators();
                            radiatorStatus = "Active";
                        }
                        else
                        {
                            DeactiveRadiators();
                            radiatorStatus = "Standby";
                        }
                    }
                }
                else
                {
                    climateControlStatus = "No Power";
                    heaterStatus = "See CC Status";
                    radiatorStatus = "See CC Status";
                    isHeaterActive = false;
                }
            }
            else
            {
                heaterStatus = "Disabled";
                radiatorStatus = "Disabled";
                isHeaterActive = false;
            }

            double activeChange = 0;

            // Calculate cabin air thermal mass
            double airMass = part.CrewCapacity * airPerSeat * airDensity;
            if (airMass < 1.0) airMass = 5.0;

            // Calculate temp change from flux
            if (System.Math.Abs(totalFlux) > 0.00001)
            {
                double energyJoules = (totalFlux * 1000.0) * dt;
                activeChange = energyJoules / (airMass * airSpecificHeat);
                currentAirTempK += activeChange;
            }

            // Save result
            cabinTemp = (float)KToC(currentAirTempK);

            double passiveEnergyJ = passiveChange * (airMass * airSpecificHeat);
            double passiveKW = (passiveEnergyJ / dt) / 1000.0;
            passiveFlux = (float)passiveKW;

            // Todo: Add a setting for debug data.
            // For now, we'll keep this because the thermal stuff is finicky.
            debugTimer += Time.fixedDeltaTime;
            if (debugTimer > 5f)
            {
                debugTimer = 0f;
                // Calculate the equivalent KW of the passive loss to compare apples-to-apples

                Debug.Log($"[KICKLS THERMAL] Hull: {KToC(hullTemp):F1}C | Air: {cabinTemp:F1}C | Mass: {airMass:F1}kg");
                Debug.Log($"[KICKLS FLUX] IN (Active): {totalFlux:F4} kW || OUT (Passive): {passiveKW:F4} kW");
                Debug.Log($"[KICKLS DELTA] Active Chg: {activeChange:F4} || Passive Chg: {passiveChange:F4}");
            }
        }

        // THIS WHOLE REGION IS SO THAT THE MODULE PLAYS WELL WITH THE THERMAL SYSTEM AT WARP
        #region On-Rails Physics
        public void SetAnalyticTemperature(FlightIntegrator fi, double analyticTemp, double toBeInternal, double toBeSkin)
        {
            cachedAnalyticTemp = toBeInternal;
        }

        public double GetSkinTemperature(out bool lerp)
        {
            lerp = false;
            return -1;
        }

        public double GetInternalTemperature(out bool lerp)
        {
            lerp = false;

            // Simulate Air Temp during warp
            if (climateControlEnabled)
            {
                double targetK = CToK(thermostatTemp);

                // Simple Warp Logic:
                // If the Hull is WARMER than target, the air overheats
                // If the Hull is COLDER than target, we assume the heater is working and holding steady.

                if (cachedAnalyticTemp > targetK)
                    cabinTemp = (float)KToC(cachedAnalyticTemp); // It's hot in here
                else
                    cabinTemp = (float)KToC(targetK); // Heater is maintaining temp
            }
            else
            {
                // Heater off? Air equals Wall.
                cabinTemp = (float)KToC(cachedAnalyticTemp);
            }

            return -1; // Return -1 so we don't mess with KSP's actual thermal system
        }
        #endregion
    }
}

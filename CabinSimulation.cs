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

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "System Heat Output", groupName = "KICKLS", groupDisplayName = "Life Support", guiFormat = "F3", guiUnits = " kW")]
        public float heatFlux = 0;

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
        [KSPField] public float heaterECRate = 0.5f;
        #endregion

        #region Thermal Constants
        const double thermostatDeadband = 2;  // 2c;
        /// <summary>
        /// The amount of heat released per liter of CO2 reacted with LiOH at STP.
        /// Based on 21.4 kcal per mole of CO2 absorbed
        /// </summary>
        const double liohReactionHeatPerUnit = 4.0;
        const double airSpecificHeat = 1005.0;
        const double airDensity = 0.001225;
        const double wallCoupling = 0.0005;
        #endregion

        #region Thermal Variables
        double airTempK;
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

        void RunThermalLogic(ref double totalFlux, ref double ecRequest)
        {
            airTempK = CToK(cabinTemp);
            airTempK += StaticThermalSimulation(airTempK);

            // Climate control
            if (climateControlEnabled)
            {
                totalFlux += systemHeat;
                float temp = (float)KToC(airTempK);
                float lowThreshold = (float)(thermostatTemp - (thermostatDeadband / 2));
                float highThreshold = (float)(thermostatTemp + (thermostatDeadband / 2));
                float excessiveThreshold = (float)(thermostatTemp + (thermostatDeadband * 4));

                if (temp < lowThreshold)
                {
                    heaterStatus = "Active";
                    isHeaterActive = true;
                    canAutoRadiator = false;
                }
                else if (temp > excessiveThreshold)
                {
                    heaterStatus = "Standby";
                    isHeaterActive = false;
                    canAutoRadiator = true;
                }
                else if (temp > highThreshold)
                {
                    heaterStatus = "Standby";
                    isHeaterActive = false;
                    canAutoRadiator = false;
                }

                if (isHeaterActive)
                {
                    heaterStatus = "Active";
                    totalFlux += heaterHeat;
                    ecRequest += (heaterHeat * heaterECRate * Time.fixedDeltaTime);
                }
                else
                {
                    heaterStatus = "Standby";
                    isHeaterActive = false;
                }

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
                heaterStatus = "Disabled";
                radiatorStatus = "Disabled";
                isHeaterActive = false;
            }
        }

        void ApplyHeatFluxToCabinAir(double totalFlux)
        {
            // Apply heat
            if (totalFlux > 0.001)
            {
                airTempK += CalculateTempChange(totalFlux);
                cabinTemp = (float)KToC(airTempK);
            }
        }

        double StaticThermalSimulation(double airTemp)
        {
            double hullTemp = part.temperature;
            return (hullTemp - airTemp) * wallCoupling * Time.fixedDeltaTime;
        }

        double CalculateTempChange(double fluxkW)
        {
            double energy = (fluxkW * 1000.0) * Time.fixedDeltaTime;
            return energy / (part.CrewCapacity * airPerSeat * airDensity * airSpecificHeat);
        }

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
                // If the Hull is WARMER than target, the air overheats (AC not simulated here yet).
                // If the Hull is COLDER than target, we assume the heater is working and holding steady.

                if (cachedAnalyticTemp > targetK)
                    cabinTemp = (float)cachedAnalyticTemp; // It's hot in here
                else
                    cabinTemp = (float)targetK; // Heater is maintaining temp
            }
            else
            {
                // Heater off? Air equals Wall.
                cabinTemp = (float)cachedAnalyticTemp;
            }

            return -1; // Return -1 so we don't mess with KSP's actual thermal system
        }
        #endregion
    }
}

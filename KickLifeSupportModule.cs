using System.CodeDom;
using System.Xml.Schema;
using UnityEngine;

namespace KickLifeSupport
{
    public class KickLifeSupportModule : PartModule
    {
        private float _lastUpdateTime = 0f;
        private float _lastWarningTime = 0f; // Added for spam protection
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

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "Cabin Temp", groupName = "KICKLS", groupDisplayName = "Life Support", guiFormat = "F1", guiUnits = "C")]
        public float cabinTemp = 0f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Avionics", groupName = "KICKLS", groupDisplayName = "Life Support"), UI_Toggle(disabledText = "Off", enabledText = "On")]
        public bool avionicsEnabled = true;

        public override void OnStart(StartState state)
        {
            PartResourceDefinition wasteDef = PartResourceLibrary.Instance.GetDefinition("Waste");
            if (wasteDef != null) wasteId = wasteDef.id;
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;

            if (KickLifeSupportScenario.Instance != null)
            {
                LifeSupportStatus data = KickLifeSupportScenario.Instance.GetData(vessel.id);
                lsStatus = data.lsStatus;
                cabinCO2 = data.cabinCO2;
                co2Level = (float)(cabinCO2 / (vessel.GetCrewCapacity() * airPerSeat));
                cabinTemp = (float)part.temperature - 273.15f;
            }
        }

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

        
    }
}
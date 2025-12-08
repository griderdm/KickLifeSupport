using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KickLifeSupport
{
    public class LifeSupportStatus
    {
        public string lsStatus = "Nominal";
        public float cabinCO2 = 0;
        public bool scrubberEnabled = true;
        public bool climateControlEnabled = true;
        public bool avionicsEnabled = true;
        public string scrubberStatus = "On";

        public double lowO2Time = 0f;
        public double lowWaterTime = 0f;
        public double lowFoodTime = 0f;
        public double lowClimateTime = 0f;
        public double tempRangeTime = 0f;

        public double lastUpdateTime = 0;

        public void Save(ConfigNode node)
        {
            node.AddValue("CabinCO2", cabinCO2);
            node.AddValue("ScrubberEnabled", scrubberEnabled);
            node.AddValue("ClimateControlEnabled", climateControlEnabled);
            node.AddValue("AvionicsEnabled", avionicsEnabled);
            node.AddValue("LowO2Time", lowO2Time);
            node.AddValue("LowWaterTime", lowWaterTime);
            node.AddValue("LowFoodTime", lowFoodTime);
            node.AddValue("LowClimateTime", lowClimateTime);
            node.AddValue("TempRangeTime", tempRangeTime);
        }

        public void Load(ConfigNode node)
        {
            float.TryParse(node.GetValue("CabinCO2"), out cabinCO2);
            bool.TryParse(node.GetValue("ScrubberEnabled"), out scrubberEnabled);
            bool.TryParse(node.GetValue("ClimateControlEnabled"), out climateControlEnabled);
            bool.TryParse(node.GetValue("AvionicsEnabled"), out avionicsEnabled);
            double.TryParse(node.GetValue("LowO2Time"), out lowO2Time);
            double.TryParse(node.GetValue("LowWaterTime"), out lowWaterTime);
            double.TryParse(node.GetValue("LowFoodTime"), out lowFoodTime);
            double.TryParse(node.GetValue("LowClimateTime"), out lowClimateTime);
            double.TryParse(node.GetValue("TempRangeTime"), out tempRangeTime);
        }
    }
}

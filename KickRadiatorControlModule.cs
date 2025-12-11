using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KickLifeSupport
{
    public class KickRadiatorControlModule : PartModule
    {
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Auto Deploy", groupName = "KICKLS", groupDisplayName = "Life Support"),
            UI_Toggle(disabledText = "Ignored", enabledText = "Enabled")]
        public bool allowAutoDeploy = true;
    }
}

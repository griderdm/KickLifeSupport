# KICK Life Support (WIP)

## What Is It?
KICK Life Support is a life support mod for KSP that is more realistic than [TAC](https://spacedock.info/mod/915/TAC%20Life%20Support), but isn't as expansive as [Kerbalism](https://spacedock.info/mod/1774/Kerbalism). Rather than being about the resources, it's about the hardware that makes life support function. It's more immersive for the player, and it even gives you some regular housekeeping tasks to do on your active vessel.
## STANDARD DISCLAIMER
**THIS MOD IS A WORK IN PROGRESS - It's designed explicitly to kill your kerbals when they run out of food, water, oxygen, and lithium hydroxide. There are bound to be bugs that could kill your Kerbals out of nowhere. So don't blame me when they perish.**

**BUT if they do die unexpectedly, file a bug report.**
## Features
Kerbals need food, water, oxygen, and stable temperatures to survive. They're energetic little guys with high metabolisms, so resources go fast. They also make waste products which have to be managed.
### Food & Water
Kerbals eat `Food` and drink `Water`, producing `Waste` and `WasteWater`. There is a small supply onboard a Command Pod (1 liter of each, or about 13.9 hours of `Food` and 9.25 hours of `Water` per Kerbal). If you need more, bring it.
### Oxygen and CarbonDioxide
Kerbals breath `Oxygen` and then exhale `CarbonDioxide`. Unlike other life support mods, `CarbonDioxide` isn't stored in a tank; it builds up in the cabin air. In zero-G, it can form "bubbles" and needs to be stirred throughout the cabin. A cabin fan (part of the `Climate Control` system) keeps the air circulating to prevent CO2 from collecting in one spot. 
#### CO2 Scrubber
To eliminate `CarbonDioxide`, a Command Pod is equipped with a Lithium Hydroxide Scrubber, which pulls the cabin air through a LiOH canister using a fan.

The scrubber removes CO2 from the cabin, generating heat. LiOH is provided by a canister. One `LiOH Scrubber Canister` gives enough LiOH for about 9 hours per Kerbal -- 4.5 hours for two Kerbals, and 3 hours for three Kerbals. That means you need to bring extras if you're having a long mission. Store them in your Command Pod's inventory (they won't be pulled from a Kerbal's inventory). When the cabin runs out of LiOH, you can reload the scrubber if you have a canister onboard. The old canister's volume is converted to Waste stored in the Command Pod's waste tank. As LiOH gets used, it also becomes waste. The scrubber is EC-dependent and will only work if there's power to run the fan.
### Temperature Control
Space is cold, and without something generating heat, the cabin temperature can drop dangerously low. Luckily, a spacecraft is just chock full of heat sources. For one, Kerbals themselves generate body heat. The CO2 scrubber also generates heat when it's in use. Command Pod electronics also generate heat, such as the avionics package, the SAS and RCS computers, and even the environmental control system itself. A cabin heater is used in combination with a thermostat to keep the cabin at a comfortable 22 degrees Celsius. If the temperature climbs dangerously high, radiators can be automatically deployed.
### ElectricCharge and Electronics
Almost everything onboard that is part of the life support system requires EC to run it.

The entire Command Pod uses an Avionics package to allow command and control to occur. When it's on, it consumes EC and generates heat and the pod is controllable. Turn it off, and the pod is no longer controllable, but no heat gets generated and no EC is used.

SAS and RCS also have independent electronics that are on when they are enabled (even if the stability wheels aren't running and the RCS isn't firing). Those electronics now run off of EC and generate a small amount of heat when they are turned on.

**WARNING:** This mod significantly increases power consumption to realistic levels. A standard Command Pod battery will only last about **20 minutes** with all systems active. You **must** plan for power generation (Solar/Fuel Cells) even for short trips, or *lots* of battery.
### Causes of Death
- **CO2 Toxicity:** Immediate death if Cabin CO2 reaches 10%.
- **Suffocation:** Death if Oxygen runs out (Grace period: 2 minutes).
- **Stagnant Air:** Death if Climate Control (Fans) loses power for > 1 hour.
- **Hypothermia/Hyperthermia:** Death if Internal Cabin Temperature drops below 5°C or rises above 45°C (Grace period: 5 minutes).
- **Dehydration:** Death after ~6 Kerbin days without water.
- **Starvation:** Death after ~28 Kerbin days without food.
### Other Features
- Background Processing - Unloaded/on-rails ships continue to work.
- Small amounts of resource per pod - if you need more, bring it with you.
## Prerequesites
- [KSP 1.12](https://store.steampowered.com/app/220200/Kerbal_Space_Program/)
- [ModuleManager 4.2.3](https://forum.kerbalspaceprogram.com/topic/50533-18x-112x-module-manager-423-july-03th-2023-fireworks-season/)
- [Community Resource Pack](https://github.com/UmbraSpaceIndustries/CommunityResourcePack/releases)
## Compatibility/Recommended Mods
- [Real Fuels](https://forum.kerbalspaceprogram.com/topic/58236-18-real-fuels/)
- [Universal Storage 2](https://spacedock.info/mod/2960/Universal%20Storage%20II%20Finalized)
- [System Heat](https://forum.kerbalspaceprogram.com/topic/193909-112x-systemheat-a-replacement-for-the-coreheat-system-july-21/)
## Known Bugs
- None
## Roadmap & Upcoming Features
- UI for background processing
- Carbon Dioxide Removal Assembly (CDRA) - Instead of only using a LiOH scrubber, the CDRA will extract CO2 to allow for storage (or dumping overboard).
- Pressurization system
- Humidity from exhalation and the LiOH scrubber
- Radiation belt and solar radiation
	- Geiger counter
	- Associated experiments
	- Radiation damage/death
- Sleep cycles?
- Meal times?
- EVA life support
- DangIt! support
- kOS support (Addon, thermal support)
- MechJeb2 support (thermal)

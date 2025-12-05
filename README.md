# KICK Life Support

## What Is It?
KICK Life Support is a life support mod for KSP that is more realistic than [TAC](https://spacedock.info/mod/915/TAC%20Life%20Support), but isn't as expansive as [Kerbalism](https://spacedock.info/mod/1774/Kerbalism).
## Features
- Kerbals use Oxygen, Food, and Water, and generate CarbonDioxide, Waste, and WasteWater. They're energetic little guys with high metabolisms, so those resources go fast.
- Each Command Pods has a lithium hydroxide scrubber
	- Store LiOH Scrubber Canisters in the Command Pod inventory
	- When the pod's LithiumHydroxide level runs low or runs out, you can load a new canister. A single canister will last about 9 hours for one Kerbal, 4.5 hours for two Kerbals, and 3 hours for three Kerbals.
	- The old canister becomes Waste.
	- The LiOH scrubber only works when there is LiOH available and electricity to run the fan.
- Background Processing - Unloaded/on-rails ships continue to work.
- Small amounts of resource per pod - if you need more, bring it with you.
## Prerequesites
- KSP 1.12
- [ModuleManager 4.2.3](https://forum.kerbalspaceprogram.com/topic/50533-18x-112x-module-manager-423-july-03th-2023-fireworks-season/)
- [Community Resource Pack](https://github.com/UmbraSpaceIndustries/CommunityResourcePack/releases)
## Compatibility
- Real Fuels
- Universal Storage 2
## Known Bugs
- Very high warp (x10000 and above) will sometimes cause insta-death even when resources exist
- The status text cycles through non-applicable warnings at high speed
## Roadmap & Upcoming Features
- UI for background processing
- Cabin temperature management
	- Crew members generate body heat
	- Pod has an avionics package that uses EC, generates heat
	- SAS and RCS use EC and generate heat when enabled, even when they're not moving the spacecraft
- Radiation belt and solar radiation
	- Geiger counter
	- Associated experiments
- EVA life support
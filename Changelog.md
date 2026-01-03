# Changelog
## (v0.1.2) 1/3/2026
### Changes
- Happy New Year
### Bug Fixes
- Fixed [#1](https://github.com/griderdm/KickLifeSupport/issues/1), hopefully. Cabin temperature was not converted to C when assigned to background ships. Thanks for the catch, @theersink.
## (v0.1.1) 12/20/2025
### Changes
- Added logging to `TryReloadScrubberUnloaded()`
- Removed spammy logging
- Added a version file
### Bug Fixes
- Fixed a bug where background ships did not automatically reload the scrubber
## (v0.1) 12/16/2025 
- Added a KSP settings tab
- Did a bunch of code cleanup and minor bug fixes
- Tweaked EC settings
- Added more documentation
- Initial release
## 12/15/2025
- Made a *bunch* of tweaks, adjustments, and bug fixes.
## 12/9/2025
- Added on-rails thermal calculations.
- Added adjustable thermostat
- Added adjustable cabin heater strength
- Set default heater strength to 2 kW per Kerbal
- Added `KickRadiatorControlModule`, which allows the player to determine whether to allow a radiator to be automatically deployed/activated for cabin thermal control purposes.
- Added support for [System Heat](https://forum.kerbalspaceprogram.com/topic/193909-112x-systemheat-a-replacement-for-the-coreheat-system-july-21/) mod.
### Notes
- The player will still have to attach radiators to the craft to prevent overheating.
- It's not a bad idea for the player to adjust the angle of the spacecraft to help manage the cabin temperature -- away from the sun when the temperature climbs, and towards-ish the sun when the temperature drops. I'll just count it as another housekeeping/mission planning thing.
- I need to do some testing to figure out if there's a pattern for thermal control.
- I've only tested the thermal system so far using a single loop of radiators




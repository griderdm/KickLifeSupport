# Consumables
Most of the resources, including base consumables, are provided by the Community Resource Pack.

Note: Rates per "day" are rates in Kerbal Days (6 hours)
## Food -> Waste
Kerbals consume `Food` at a rate of 0.00002 L/s/kerb (0.072 L/hour/kerb; 0.432 L/day/kerb). `Food` is digested into `Waste`, a generic type of trash, which is collected in an onboard tank. `Waste` can be pumped out to a disposal spacecraft (a disposable resupply vehicle, munar module before jettison, etc). As of the current version, food is consumed on a continuous basis.
## Water -> WasteWater
Kerbals consume `Water` at a rate of 0.00003 L/s/kerb (0.108 L/hour/kerb; 0.648 L/day/kerb). `Water` is digested into `WasteWater`, which is collected in an onboard tank. `WasteWater` can be pumped out to a disposal spacecraft or released into space via an `FTE-1 Drain Valve` or similar.
## Oxygen -> CarbonDioxide
Kerbals consume `Oxygen` at a rate of 0.005 L/s/kerb (18 L/hour/kerb; 108 L/day/kerb). Kerbals expel `CarbonDioxide` into the cabin air where it will build up over time. Kerbals produce CO2 at a rate of 0.0041 L/s/kerb (14.76 L/hour/kerb; 88.56 L/day/seat). The scrubber system extracts `CarbonDioxide` from the cabin air and consumes `LithiumHydroxide` in the process. See the [[Cabin Air System]] for more details.
## LithiumHydroxide -> Waste
The LiOH Scrubber uses `LithiumHydroxide` (labeled `LiOH`) to remove `CarbonDioxide` from the cabin. The `LithiumHydroxide` gets turned to `Waste` by volume. The scrubber uses EC at a rate of 0.05 EC/s/seat (180 EC/hour/seat; 1080 EC/day/seat), and uses `LithiumHydroxide` at a rate of 0.000015 L/s/seat (0.054 L/hour/seat, 0.324 L/day/seat). A single LiOH cartridge holds 0.5 L of `LithiuymHydroxide`. It takes about 3 hours to deplete a single cartridge for a crew of 3.
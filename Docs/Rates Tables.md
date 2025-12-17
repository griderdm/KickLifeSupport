# Rates Tables
## Basic Systems

| System    | Resource In | In Rate (L/s/kerb) | Resource Out  | Out Rate (L/s/kerb) | Notes                                    |
| --------- | ----------- | ------------------ | ------------- | ------------------- | ---------------------------------------- |
| Breathing | Oxygen      | 0.005              | CarbonDioxide | 0.0041              | CarbonDioxide builds up in the cabin air |
| Eating    | Food        | 0.00002            | Waste         | 0.000018            |                                          |
| Drinking  | Water       | 0.00003            | WasteWater    | 0.000028            |                                          |
## Complex Systems
| System   | Res 1 In      | Res 1 In Rate (L/s/seat) | Res 2 In         | Res 2 Rate (L/s/seat) | EC Rate (EC/s) | Resource Out | Out Rate (L/s/seat) | Thermal Flux (kW/seat) | Notes                |
| -------- | ------------- | ------------------------ | ---------------- | --------------------- | -------------- | ------------ | ------------------- | ---------------------- | -------------------- |
| Scrubber | CarbonDioxide | 0.005                    | LithiumHydroxide | 0.000015              | 0.05           | Waste        | 0.000015            | 0.25                   | EC rate is EC/s/seat |
## Electrical Systems
| System          | EC Rate (EC/s) | Thermal Flux (kW) | Notes                                                                                 |
| --------------- | -------------- | ----------------- | ------------------------------------------------------------------------------------- |
| Avionics        | 0.02           | 0.02              | If disabled or no power, pod is not controllable                                      |
| SAS             | 0.01           | 0.01              | If no power, SAS will not work                                                        |
| RCS             | 0.01           | 0.01              | If no power, RCS will not work                                                        |
| Climate Control | 0.003          | 0.03              | If disabled/no power, heater and radiators will not work, and air will not circulate. |
## Heater System
| System | EC Rate (EC/s/kW/seat) | Thermal Flux (kW/seat) | Notes                                                                                              |
| ------ | ---------------------- | ---------------------- | -------------------------------------------------------------------------------------------------- |
| Heater | 1                      | 1                      | This is adjustable in the editor and in-game. Settings default to 1.0 kW of heater power per seat. |

# Cabin Air System
This article is intended to give a detailed breakdown of the cabin air system in KICK Life Support.
## Major Components
- Cabin Air Simulation
- CO2 Concentration
- LiOH Scrubber
- Climate Control 
	- Circulation Fans
	- Thermal Control
## Cabin Air Simulation
To simplify things, the cabin is assumed to have a total volume of 2000 Liters per seat. That means that a Mk1-3 Command Pod has a total internal volume of 6000 Liters regardless of how many kerbals occupy the pod at any given time. A future version will simulate pressure and humidity, but right now, the cabin air has simulated CO2 concentration and temperature.
## CO2 Concentration
The cabin air volume is used to calculate CO2 concentration as a percentage of total air. As Kerbals breathe, CO2 goes into the cabin air, increasing the CO2 concentration bit by bit. When the CO2 concentration reaches 3%, the player is warned. When the CO2 concentration reaches 10%, the Kerbals die.

One important note is CO2 is distributed evenly throughout all occupiable spaces. When your ship docks with an space station, any air onboard mixes with air from the station. If there is a lot of CO2 on the space station, it will "infect" your ship air, and your scrubber will have to work to remove the CO2.
## LiOH Scrubber
Each cabin takes a standard LiOH Cartridge, containing 0.5 Liters of `LithiumHydroxide`. You should bring as many LiOH Cartridges as you think you'll need; store them in the pod's inventory. One cartridge is good for 1 Kerbal for 9.25 hours, 2 Kerbals for 4.63 hours, or 3 Kerbals for 3 hours. The scrubber uses a fan to draw air through the cartridge, which uses up `LithiumHydroxide` and removes `CarbonDioxide` from the cabin air. The fan uses EC, and the reaction generates heat (simulated) and water (in the form of humidity, not simulated yet). When a cartridge runs dry, you can reload the cartridge using the `Reload Scrubber` button on the pod's Part Window.
## Climate Control
The Climate Control system includes two main parts: the air circulation fans, and the thermal control system.
### Circulation Fans
In microgravity, fans must circulate cabin air or CO2 will build up in a bubble around the heads of the crew. To keep the air from going stable, EC is used to drive fans that recirculate the cabin air. The fans generate a small amount of heat.
### Thermal Control
There is an onboard thermostat that allows the player to set the cabin temperature for the Kerbals. Ideal "room temperature" is 22°C, but you can obviously set it to whatever you want. The deadband for the thermostat is set at 2°C; the heater will turn on when the temperature drops below 1°C of the thermostat temperature and turn off again when the temperature rises to 1°C above the thermostat temperature (assuming the Climate Control system is active and there's enough power).

You can also set the heater strength in kW of thermal flux. That same number corresponds to the amount of EC draw per second when the heater is on.

On the Part Window, you can see the total internal and external flux sources. You can use passive thermal control methods to attempt to balance them as a power saving measure.

If the cabin temperature rises above 30°C, onboard radiators will automatically start running (if you've chosen to allow them to).
# :compass:AdvancedCompass

Advanced Compass expands the vanilla compass system by adding a new upgrade chip that turns your compass into a powerful navigation tool.
Once equipped, the new Compass T2 Microchip allows the compass to display nearby points of interest directly on the HUD, helping you locate valuable resources and hidden loot without constantly opening map screens.

## :hammer_and_wrench:Features
### :wrench:New Craftable Equipment
- Adds a new equipment item: Microchip - Compass T2
- Craftable at Craft Station T2
- Optional blueprint unlock support
- Fully integrated into the game's equipment system

### :pick:Ore Vein Detection
#### Displays nearby resource veins directly on the compass:
- Tier 1 ore veins
- Tier 2 ore veins
- Fish ecosystems
- Harvester zones
- Toxic caves and special resource areas
#### Each marker uses the actual resource icon, making targets instantly recognizable.

### :toolbox:Loot Container Tracking
#### Highlights nearby world containers and hidden loot caches:
- Wreck containers
- Scene loot containers
- Special world objects
- Additional custom container types configurable by the player
#### Containers can use either:
- Their actual in-game icons
- Simple question-mark markers

### :triangular_flag_on_post:Progression-Aware Scanning
#### The mod can respect game progression:
- Resource veins can require Information Rockets
- ontainer tracking can require GPS Satellites
- Features unlock naturally alongside your exploration technology
#### Or, if preferred, progression requirements can be disabled entirely.

### :gear:Extensive Configuration
#### Everything can be customized through the configuration file:
- Scan range
- Marker size
- Update frequency
- Required equipment chip
- Container visibility
- GPS/rocket requirements
- Blueprint requirements
- Custom container group support

### :chart_with_upwards_trend:Performance Friendly
- Uses cached scans with configurable refresh intervals
- Only tracks nearby objects
- Automatically hides markers outside scanning range
- Minimal impact on game performance

## :memo:Configuration Options
| Option | What it do |
|----------|--------|
| ScanRange | Maximum detection distance |
| MarkerSize | Compass icon size |
| howAllVeins | Ignore chip requirement |
| ShowContainers | Enable container tracking |
| UseContainerIcons | Use real container icons |
| RequireInfoRocket | Respect exploration progression |
| RequireBlueprintUnlock | Require blueprint unlock |
| UpdateInterval | Scan refresh frequency |


## Installation

1. Download AdvancedCompass.dll from the Releases section
2. Copy the file to the BepInEx/plugins/ folder in the game
3. Launch the game

## Requirements

- BepInEx installed
- Planet Crafter (current version)

## Building from source (for developers)

1. Install the .NET SDK
2. Copy the dependency DLLs to the libs/ folder (listed in .csproj)
3. Run dotnet build -c Release

# Change Log
Changelog for Valheim Mod: 'Wormhole Potion' built on the Jotunn mod stub.

All notable changes to this project will be documented in this file.
 
The format is based on [Keep a Changelog](http://keepachangelog.com/)
and this project adheres to [Semantic Versioning](http://semver.org/).
 
## [0.1.2] - 2026-08-13

### Changed
- Player selection now uses Valheim's synchronized public map positions instead of nearby loaded player objects or network peers.
- Map clicks use a zoom-aware selection radius and target the selected public player record directly.
- Wormhole potions are consumed only after Valheim accepts the teleport.
- Players who have disabled public-position sharing are not available as wormhole targets.
- C# source files are normalized to LF line endings.

### Fixed
- Teleporting now works consistently for server hosts, connected clients, and client-to-client targets, including players far outside the loaded area.
- Invalid map clicks, canceled targeting, and rejected teleports no longer consume the potion.
- Removed the blocking RPC and ZDO position lookup that could time out or fail for distant players.
- Success messages are no longer logged when teleportation fails.

 
## [0.1.1] - 2025-05-06
  
Patched to work with Valheim version 2025-013-10 0.220.4
 
### Added

### Changed

### Fixed
- Error message [Error  : Unity Log] MissingMethodException: Method not found: void .MessageHud.ShowMessage(MessageHud/MessageType,string,int,UnityEngine.Sprite)

## [0.1.0] - 2025-01-19
  
Beta release of the mod to test with friends. Use at your own risk.
 
### Added
- Custom item - Firth's bond potion.
- Custom Status Effect - Wormhole Effect
- Harmony patches for: Player consume item, map mode set, and map left click.

### Changed

### Fixed

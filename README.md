# Retakes Plugin (CS2) - v2.0

A CounterStrikeSharp plugin for Counter-Strike 2 that runs a full Retakes flow with automatic team setup, teleport, bomb planting, and loadouts.

## Current Status

This project was refactored from one large file into a service-based structure with Dependency Injection.

Main areas:

- Game flow services (round lifecycle, team logic, bomb setup, loadouts)
- Spawn services (spawn filtering and random selection)
- Persistence (JSON spawn load/save)
- Shared core state

## Features

- Automatic retake flow on warmup and round events
- Team shuffle and random planter selection
- Role-based spawn teleporting (T plant, T player, CT player)
- Early teleport attempt on round start, with freeze-end fallback
- Automatic planted C4 creation and setup
- Automatic player loadout reset and weapon assignment
- Server cvar setup for retake gameplay
- Spawn save command for live setup from inside the server

## Spawn Format

Spawn points are saved per map in a JSON file named:

- `<mapname>.json`

Spawn place naming pattern:

- `<team>_<site><index>_<type>`

Examples:

- `t_a1_plant`
- `t_a2_player`
- `ct_b1_player`

Valid values:

- `team`: `t` or `ct`
- `site`: `a` or `b`
- `type`: `plant` or `player`

## Installation

1. Install CounterStrikeSharp on your CS2 server.
2. Build this project.
3. Copy plugin output to:
   `game/csgo/addons/counterstrikesharp/plugins/RetakesPlugin`
4. Restart the server or load the plugin.

## Commands

- `css_save <place>`
  Saves a spawn point to the current map JSON file (client-only).

## Notes for Online Servers

- Make sure each map has enough spawn points for your expected player count.
- If warmup transition timing looks off on your server, tune the warmup-end delay in server settings service.
- Test on your target tickrate and real player load before production rollout.

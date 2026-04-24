# Retakes Plugin (CS2)

My second **CounterStrikeSharp (CSS)** project for Counter-Strike 2.

A robust, high-performance **CounterStrikeSharp** plugin for Counter-Strike 2 that runs a full Retakes experience with automatic round flow, bomb setup, team balancing, spawn teleporting, and loadout control.

## 🚀 Current Features (v1.0.0)

* **Automatic Retake Flow:**
  * Initializes and resets retake state on warmup/round events.
  * Chooses site flow and planter automatically.
* **Smart Team Management:**
  * Auto-shuffles teams each round.
  * Balances Terrorists and Counter-Terrorists dynamically.
* **Bomb & Round Setup:**
  * Auto-selects a planter.
  * Spawns and configures planted C4 in plugin flow.
* **Spawn System:**
  * Loads map spawns from JSON (`inferno.json`, fallback to `inferno.json`).
  * Teleports players to role-based spawn points (T plant/T player/CT player).
* **Loadout Control:**
  * Clears current weapons before round setup.
  * Gives role-appropriate weapons and utility gear.
* **Server Configuration Automation:**
  * Applies retake-focused server cvars each round.
  * Enables stable practice settings for consistent gameplay.

## 🛠 Installation

1. Ensure you have [CounterStrikeSharp](https://github.com/rofl0l/CounterStrikeSharp) installed on your server.
2. Build the project and place the plugin output folder into:
   `game/csgo/addons/counterstrikesharp/plugins/RetakesPlugin`
3. Restart the server or load the plugin with the relevant CSS command.

## ⌨️ Commands

* `css_save <place>`: Saves a retake spawn point to JSON (client-only command).

## 🔮 Upcoming Features (v1.1.0 - In Development)

The next version is planned to focus on **better control, analytics, and quality-of-life** updates:

* **Round Analytics:** Better round summaries and server-side metrics.
* **Config Expansion:** Easier tuning for weapons, timing, and team ratios.
* **Admin Utility Commands:** More tools for setup, validation, and live control.
* **Spawn Validation:** Improved checks and clearer diagnostics for invalid/missing spawn entries.

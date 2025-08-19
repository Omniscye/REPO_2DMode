# REPO_2DMode

A BepInEx mod that adds a toggleable 2D mode, flattening the world into a strict 2D look.  
Press **F8** in-game to enable or disable it.

## Technical Details

REPO_2DMode works by attaching Harmony patches and runtime enforcers to squash objects into a flat plane:

- **Enemies & Valuables**  
  Flattened on the **Z-axis** by attaching `_FlatMarker` components to all renderers under each object.  
  Their original scales are cached and restored when the mod is toggled off.

- **Player Avatars**  
  - Locates the `[RIG]` under `Player Visuals` in each `PlayerAvatar`.  
  - Attaches an `AvatarRigFlattener` to squash the **X-axis** scale of the rig.  
  - Also attaches an `AvatarPerRendererFlattener` as a fallback to enforce flatness per renderer.  
  - Original rig scales are cached and restored when toggled off.

- **Flashlights**  
  - Local player: walks the hierarchy `Local Camera → Flashlight Target → Flashlight → Mesh`.  
  - Remote players: falls back to scanning for `Flashlight → Mesh`.  
  - Attaches a `FlashlightMultiScaler` (per avatar) or `FlashlightYEnforcer` (scene-wide) to squash the **Y-axis** scale.  
  - All flashlight meshes restore to their original scale when toggled off.

- **Items**  
  - Only transforms whose names begin with `"Item "` (excluding anything with `"Cart"`) are squashed on the **Z-axis**.  
  - This avoids flattening carts or unrelated props.

- **One-Time Application vs. Continuous Enforcement**  
  - On toggle **ON**, a one-time sweep flattens all existing enemies, valuables, items, avatars, and flashlights.  
  - Harmony patches ensure new spawns (enemies, valuables, players) are also flattened at creation.  
  - No continuous polling is required — enforcement is handled by lightweight MonoBehaviours attached only to affected objects.

- **Toggle OFF**  
  - All `_FlatMarker`, `AvatarRigFlattener`, `AvatarPerRendererFlattener`, and flashlight enforcers are detached.  
  - Every object’s original scale is restored safely.

## Installation
1. Install [BepInEx 5.4.21](https://github.com/BepInEx/BepInEx/releases)
2. Place the compiled DLL into your `BepInEx/plugins` folder
3. Launch the game and press **F8** to toggle 2D mode

## Controls
- **F8** → Toggle 2D mode on/off

## Credits
Created by Omniscye  
Support me: [Ko-fi](https://ko-fi.com/omniscye)

# Heartwell In-Game UI Assets

Generated in the Heartwell menu/cutscene style: dark moss-green panels, aged gold-vine trim, glossy slimes, wet forest lighting, teal bioluminescence, and warm heartwell glow.

Final alpha PNGs live in `final/`. Chroma-key sources live in `source_chroma/`.
Scene-title tiles live in `scene_titles/`. Runtime copies live under `Assets/Resources/UI/InGameGenerated/`.

## Final Assets

- `01_sticky_unlock.png` - Sticky form unlock popup shell.
- `02_wave_unlock.png` - Slime Wave unlock popup shell.
- `04_context_prompt.png` - Reusable subtle key/icon prompt bubble.
- `05_tutorial_cards.png` - Four symbolic tutorial cards as one sliceable sheet.
- `06_checkpoint.png` - Checkpoint/remembered-light banner.
- `07_pickup_discovery.png` - Mushroom and magic-rock pickup discovery banners.
- `08_hazard_overlay.png` - Peripheral hazard/damage overlay.
- `09_respawn.png` - Gentle respawn/modal shell.
- `10_enemy_banner.png` - Enemy encounter lower-third banner shell.
- `11_barrier_prompt.png` - Barrier/objective visual-association popup.
- `12_pause_panel.png` - Pause menu panel with three button slots.
- `13_options_shell.png` - Options/settings panel shell.
- `14_scene_transition.png` - Forest-to-dungeon transition card.
- `15_save_indicator.png` - Tiny save/status badge.
- `16_scene_title_mosswake.png` - Hollow Knight-like scene-title tile for Mosswake.
- `17_scene_title_outer_grove.png` - Hollow Knight-like scene-title tile for Outer Grove.
- `18_scene_title_dungeon_entrance.png` - Hollow Knight-like scene-title tile for Dungeon Entrance.

## Runtime Placement

- `InGameOverlayUI` auto-creates a persistent popup/overlay canvas and loads the generated PNGs from `Resources`.
- No timer HUD is wired or included in the generated runtime asset set.
- Ability pickup popups appear once per discovered material/ability.
- Context prompts are delayed until repeated failed wave attempts.
- Checkpoints show the remembered-light banner plus the small save badge.
- Hazards flash the peripheral overlay; respawn shows a short modal before reload.
- Enemy arenas show the lower-third banner and a subtle barrier prompt.
- Scene transitions show the transition card; gameplay scenes show scene-title tiles on first load.
- Pause/options reuse the generated pause panel and options shell.

## Subtle Hint Rules

- Let the player try first. Start with world cues: glow, framing, repeated shapes, landmarks, sound, and object placement.
- Show UI only after hesitation, repeated failure, or arriving at the relevant affordance.
- Prefer icon sockets, key glyphs, and visual cause/effect over written instructions.
- Keep tutorial cards optional or delayed; do not pause gameplay for basic mechanics unless the player is stuck.
- Use explicit text only for system states where ambiguity is harmful: pause, options, saving, respawn, accessibility.
- Keep every generated art asset text-free; add localized runtime text in Unity when needed.

Reference patterns used: integrated tutorials and visual examples from Game Design Skills, environmental guidance by landmarks/light from World of Level Design, and UI pacing examples from Interface In Game pages for Kena, Overcooked 2, Dragon Quest Builders 2, and Splatoon 2.

Sources:
- https://gamedesignskills.com/game-design/video-game-tutorial/
- https://www.worldofleveldesign.com/categories/level_design_tutorials/alan-wake-guide-the-player.php
- https://interfaceingame.com/games/kena-bridge-of-spirits/
- https://interfaceingame.com/games/overcooked-2/
- https://interfaceingame.com/games/dragon-quest-builders-2/
- https://interfaceingame.com/games/splatoon-2/

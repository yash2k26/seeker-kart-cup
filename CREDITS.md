# Third-party art credits

Everything listed here is CC0 1.0 (a public-domain dedication: free for commercial use, no attribution required) or
SIL OFL. Credited anyway.

## Poly Haven (https://polyhaven.com/license)

| In this repo | Source | Authors |
|---|---|---|
| `Assets/_Game/Art/Textures/Sky/skybox-moonrise.png` (an 8-bit bake of the 2k HDRI, cooled to the game's night palette) | https://polyhaven.com/a/qwantani_moonrise_puresky | Greg Zaal (photography), Jarod Guest (processing) |
| `Assets/_Game/Art/Textures/Surfaces/asphalt_{diff,nor,rough}.png` (the 2k tiles at 1024 px) | https://polyhaven.com/a/asphalt_02 | Rob Tuytel |
| `Assets/_Game/Art/Textures/Surfaces/sand_diff.png` (the 2k tile at 1024 px) | https://polyhaven.com/a/coast_sand_01 | Rob Tuytel |
| `Assets/_Game/Art/Textures/Surfaces/concrete_{diff,nor}.png` (the 2k tiles at 1024 px) | https://polyhaven.com/a/concrete_wall_008 | Charlotte Baglioni (photography), Dario Barresi (processing) |

`Assets/_Game/Art/Textures/Surfaces/puddles.png` is generated (tileable noise), not third-party.

## Kenney (https://kenney.nl, CC0)

The model kits under `Assets/_Game/Art/Models` (Mini Characters, the karts, Racing, Nature, Holiday, Pirate, Survival,
Watercraft, Toy Car) and the UI kit under `Assets/_Game/Art/Textures/UI/Kit`.

## Fonts (SIL Open Font License, via Google Fonts)

Archivo (Omnibus-Type): every word in the game, cut to static files by `tools/fonts/make_archivo.py`.
Titan One (Rodrigo Fuenzalida) and Barlow Semi Condensed (Jeremy Tribby): the KARTEL lockup, which is an image
rendered for the teaser (`tools/brand/make_lockup.py`), not a shipped font.
Anton, Barlow: the earlier interface faces; the files stay for scenes not yet rebuilt.

## Audio

- **Music** (`Assets/_Game/Audio/Music`): six original loops, synthesised in code (`tools/audio/game_music.py`), no samples and no third-party material. Nothing to credit.
- **Item sounds** (`Assets/_Game/Audio/SFX/items`): synthesised in code (`tools/audio/game_sfx.py` and the teaser's own effects). Nothing to credit.
- **Interface sounds, voice-over, jingles** (the rest of `Assets/_Game/Audio/SFX`): Kenney audio packs, CC0.

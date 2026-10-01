#!/usr/bin/env python3
"""The game's typefaces: static cuts of Archivo, the family the teaser is set in (SIL OFL 1.1), taken from its variable
fonts at the widths and weights the interface was laid out for.

    ~/Documents/beach-buggy/teaser/.venv-graphics/bin/python tools/fonts/make_archivo.py

- Archivo-CondensedBlack   (wdth 62, wght 900): the display face. Its capitals run 0.47 em, Anton's ran 0.45, so every
                           title and button keeps the box it was drawn for.
- Archivo-Bold             (wdth 87.5, wght 700) and Archivo-SemiBold (wdth 92, wght 600): the text faces, as wide as the
                           Barlow they replace.
- Archivo-ExpandedBlackItalic (wdth 125, wght 900): the film's own lockup face, for the wordmark and the one big line.
Unity reads a variable font at its default instance only, hence the static files.
"""
import os
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

SRC = os.path.expanduser("~/Documents/beach-buggy/teaser/assets/fonts/archivo/")
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Game", "UI", "Fonts")
CUTS = [("Archivo-CondensedBlack", "Archivo[wdth,wght].ttf", 62, 900),
        ("Archivo-Bold", "Archivo[wdth,wght].ttf", 87.5, 700),
        ("Archivo-SemiBold", "Archivo[wdth,wght].ttf", 92, 600),
        ("Archivo-ExpandedBlackItalic", "Archivo-Italic[wdth,wght].ttf", 125, 900)]
for name, src, wdth, wght in CUTS:
    font = instancer.instantiateVariableFont(TTFont(SRC + src), {"wdth": wdth, "wght": wght})
    family, style = name.split("-")
    for rec in font["name"].names:                      # its own family name, or Unity folds the cuts into one face
        if rec.nameID in (1, 16): rec.string = "Archivo " + style
        elif rec.nameID in (2, 17): rec.string = "Regular"
        elif rec.nameID == 4: rec.string = "Archivo " + style
        elif rec.nameID == 6: rec.string = name
    font.save(os.path.join(OUT, name + ".ttf"))
    print(f"[fonts] {name}.ttf  wdth {wdth} wght {wght}")

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

The three interface cuts get the word space of the faces they replace (Anton 0.23 em, Barlow 0.20): Archivo's own is
0.11 em at this width, and "CAM · CHASE" closed up into one word. The space is also taken out of the kerning: TextMesh
Pro closed "EVERY TRACK" up completely on the Y-space-T pair.
"""
import os
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

SRC = os.path.expanduser("~/Documents/beach-buggy/teaser/assets/fonts/archivo/")
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Game", "UI", "Fonts")
# name, source, wdth, wght, word space in em (None = the font's own)
CUTS = [("Archivo-CondensedBlack", "Archivo[wdth,wght].ttf", 62, 900, 0.22),
        ("Archivo-Bold", "Archivo[wdth,wght].ttf", 87.5, 700, 0.20),
        ("Archivo-SemiBold", "Archivo[wdth,wght].ttf", 92, 600, 0.20),
        ("Archivo-ExpandedBlackItalic", "Archivo-Italic[wdth,wght].ttf", 125, 900, None)]


def word_space(font, em):
    """Sets the space and no-break space to `em` wide and removes both from every kerning pair."""
    cmap = font.getBestCmap()
    spaces = {cmap[cp] for cp in (0x20, 0xA0) if cp in cmap}
    for g in spaces:
        font["hmtx"][g] = (round(em * font["head"].unitsPerEm), font["hmtx"][g][1])
    for lookup in font["GPOS"].table.LookupList.Lookup:
        for st in lookup.SubTable:
            st = getattr(st, "ExtSubTable", st)
            if st.LookupType != 2: continue
            if st.Format == 2:                          # by class: out of both class tables and the first-glyph coverage
                for g in spaces:
                    st.ClassDef1.classDefs.pop(g, None); st.ClassDef2.classDefs.pop(g, None)
                st.Coverage.glyphs = [g for g in st.Coverage.glyphs if g not in spaces]
            else:                                       # by pair: the space's own sets, and it as a second glyph
                keep = [i for i, g in enumerate(st.Coverage.glyphs) if g not in spaces]
                st.Coverage.glyphs = [st.Coverage.glyphs[i] for i in keep]
                st.PairSet = [st.PairSet[i] for i in keep]; st.PairSetCount = len(st.PairSet)
                for ps in st.PairSet:
                    ps.PairValueRecord = [r for r in ps.PairValueRecord if r.SecondGlyph not in spaces]
                    ps.PairValueCount = len(ps.PairValueRecord)


for name, src, wdth, wght, space in CUTS:
    font = instancer.instantiateVariableFont(TTFont(SRC + src), {"wdth": wdth, "wght": wght})
    if space: word_space(font, space)
    family, style = name.split("-")
    for rec in font["name"].names:                      # its own family name, or Unity folds the cuts into one face
        if rec.nameID in (1, 16): rec.string = "Archivo " + style
        elif rec.nameID in (2, 17): rec.string = "Regular"
        elif rec.nameID == 4: rec.string = "Archivo " + style
        elif rec.nameID == 6: rec.string = name
    font.save(os.path.join(OUT, name + ".ttf"))
    print(f"[fonts] {name}.ttf  wdth {wdth} wght {wght}")

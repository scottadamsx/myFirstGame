# Photo reference

The "shot for shot" discipline: before a district gets its fidelity pass, it
gets a photo pass, and every block is checked against the photos.

**Photos live in Google Drive** (this machine has limited disk):
`My Drive/claude-artifacts/stjohns-game-reference/<district>/`

This folder holds only the **manifests** — one CSV per district mapping
address/block → building type, colour, trim, notes. The pipeline consumes
these to assign facades.

Manifest format (`<district>.csv`):

```csv
street,number_range,building_type,colour,trim,notes
Gower St,1-15,jellybean_row_a,#D94F30,white,"steep pitch, bay windows"
```

Shooting checklist per street: straight-on facades, corners, signage,
street furniture, one wide establishing shot from each end.

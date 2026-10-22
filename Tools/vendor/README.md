# Offline model-export helpers

Snapshot from HoennVR commit 45a6fb8, copied for a standalone model exporter.
`hoenngen/species.py` is reduced to clip-role preferences and selection, with
world/data loading removed. `sword.py` supplies shared animation decoding helpers
used by the BDSP converter; the lab does not export Sword models.

Raw game files are read from the user's existing local dumps. They are not
included in this repository. Generated models are local, Git-ignored assets.
See ../../Docs/model-manifest.json and ../../Docs/data-provenance.json.

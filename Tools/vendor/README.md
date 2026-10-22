# Offline model-export helpers

Snapshot from HoennVR commit 45a6fb8, copied for a standalone model exporter.
`hoenngen/species.py` is reduced to clip-role preferences and selection, with
world/data loading removed. `sword.py` supplies shared animation decoding helpers
used by the BDSP converter; the lab does not export Sword models.

Raw game files are read from the user's existing local dumps. They are not
included in this repository. Generated models are local, Git-ignored assets.
See ../../Docs/model-manifest.json and ../../Docs/data-provenance.json.

22 October direct-control follow-up: bdspfx.py snapshots the local BDSP particle/sequence decoder. Tools/export_attack_effects.py overrides its old fallback-only move table with the eight actual native move IDs used by this lab. ZA staging consumes only selected already-decoded emitter sets, timelines and referenced textures; runtime is independent of the source checkout.

"""Species table helpers: which species the slice needs and which clip plays each role."""
import json
import os

DATA = os.path.join(os.path.dirname(__file__), "unused-data")

CLIP_PREFS = {
    'idle': ['defaultwait01_loop', 'wait01_loop', 'battlewait01_loop'],
    'walk': ['walk01_loop'],
    'run': ['run01_loop', 'walk01_loop'],
    'notice': ['notice01', 'glad01'],
    'roar': ['roar01', 'attack01'],
    'sleep': ['sleep01_loop', 'rest01_loop'],
    'eat': ['eat01_loop', 'eat02_loop'],
    'battle': ['battlewait01_loop', 'defaultwait01_loop'],
    'attack': ['attack01', 'attack02'],
    'special': ['rangeattack01', 'rangeattack01_start', 'rangeattack01_loop', 'attack02', 'attack01'],
    'attack03': ['attack03', 'attack03_start', 'attack01'],
    'attackStart': ['attack03_start', 'attack02_start', 'attack01'],
    'attackLoop': ['attack03_loop', 'attack02_loop', 'attack01'],
    'attackEnd': ['attack03_end', 'attack02_end', 'attack01'],
    'rangeStart': ['rangeattack02_start', 'rangeattack01_start', 'rangeattack01'],
    'rangeLoop': ['rangeattack02_loop', 'rangeattack01_loop', 'rangeattack01'],
    'rangeEnd': ['rangeattack02_end', 'rangeattack01_end', 'rangeattack01'],
    'charge': ['charge01_loop', 'charge01_start', 'roar01'],
    'stun': ['stun01_loop', 'stun01_start', 'damage01'],
    'stunStart': ['stun01_start'],
    'stunEnd': ['stun01_end'],
    'mega': ['megaappeal01', 'formchange01', 'roar01'],
    'bossAttack': ['directionattack01_start', 'directionattack01', 'attack03', 'attack01'],
    'bossLoop': ['directionattack01_loop'],
    'bossEnd': ['directionattack01_end'],
    'stepIn': ['stepin02', 'stepin01'],
    'stepOut': ['stepout02', 'stepout01'],
    'stepLeft': ['stepleft02', 'stepleft01'],
    'stepRight': ['stepright02', 'stepright01'],
    'damage': ['damage01', 'damage01_start', 'damage01_loop'],
    'faint': ['down01_start', 'down01', 'down01_loop'],
}


def pick_clips(names, fly=None):
    names = set(names)
    roles = {role: next((prefix+p for prefix in ('', 'swim_', 'fly_') for p in prefs if prefix+p in names), None) for role, prefs in CLIP_PREFS.items()}
    roles['fly'] = fly if fly in names else None  # flying cycles differ per species, so species.json names them
    return roles

# LMUStoryLockFix

Fixes a [LethalMoonUnlocks](https://thunderstore.io/c/lethal-company/p/explodingMods/LethalMoonUnlocks/) (LMU) bug in Discovery Mode where moons you haven't discovered yet get **story-locked forever**. A charter or constellation then only ever offers its default moon (for example, only Experimentation).

## The bug

LMU decides a moon is story-locked if the moon is **hidden and locked** at the moment LMU builds its moon list. But during a run, LMU itself hides and locks every moon you haven't discovered yet, and LethalLevelLoader saves those flags. So when LMU rebuilds its list later, it takes its own discovery hiding for story locks. That happens when:

- you get fired (LMU resets progress mid-session),
- you start a new save in the same game session, or
- you load a save whose LethalLevelLoader data still carries those flags.

The log shows the affected moons with `Story Lock = Locked` in the LMU state table, and a line like `Generated stored moon rotation for 'AST-17' -> [  ]`.

## The fix

Only moons on an allowlist keep their story lock:

- LMU's own built-in story moons (Artifice and Embrion, plus Galetry if LMU's Galetry option is on), and
- the moons listed in the config.

For every other moon, the inferred story lock is dropped. The moon's "originally hidden/locked" state is also cleared, so LMU doesn't write the flags back. Normal discovery hiding is untouched. LMU still hides undiscovered moons, but they can be discovered again.

LMU recomputes story locks every time a save loads, so a save that's already affected recovers on its next load. Rotations refill at the next daily shuffle.

## Config

`BepInEx/config/Gezang.LMUStoryLockFix.cfg`

- `StoryLockedMoons` (default `Embrion, Artifice, Galetry`): moons that may keep a story lock. If another mod is meant to hide and lock a moon until a story event unlocks it, add that moon here.

## Notes

- Only the host needs it. LMU syncs moon state to clients, but having it on clients is harmless.
- Built against LethalMoonUnlocks 2.4.8. If LMU's internals change, the plugin logs an error and patches nothing.

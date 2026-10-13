# Notes

One file per subsystem. Describe what you found, not what the code says
verbatim — a note should stand on its own without pasting the decompiled
source next to it.

Suggested shape for a note:

```markdown
# <Subsystem>

**Build:** <game version analyzed>
**Source:** <which tool / which assembly or asset the finding came from>

## Summary
What this subsystem does, in a paragraph.

## Structures
Field layouts, offsets, enums — as tables.

## Behavior
Control flow and logic, described. Small snippets only where a prose
description genuinely can't carry it.

## Open questions
```

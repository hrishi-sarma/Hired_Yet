AUTOMATIC BUILDING DOOR ORIENTATION

This version no longer requires a DoorPoint child, tags, or marker scripts.
ModelFitter searches the instantiated prefab hierarchy for a child named 'door'
(case-insensitive). It ignores names containing 'frame' and uses the actual door
mesh's position relative to the combined building render bounds to infer which
of the four horizontal sides contains the entrance. That side is rotated toward
local +Z, which is the lot frontage direction used by the building placement code.

Your shown hierarchy is supported:
  h1(Clone)
    door
    door_frame
    house
    platform
    window
    window_frame

The actual 'door' child is prioritized over 'door_frame'. If the door child is
centered in the prefab or its pivot is not on the entrance side, automatic side
inference may be ambiguous; the door mesh/pivot needs to be positioned on the
face where the entrance actually is. Prefabs without a recognizable door keep
the existing fallback rotation behavior.

After replacing scripts, regenerate the world. Existing generated buildings do
not rotate retroactively.

# Save Play Mode Changes in Unity

Unity tool allowing changes made in play mode to be restored upon stopping the game.

We've built a new way of saving changes made in play mode which fixes some issues with the existing solutions. It's not a magic bullet, but it's a huge time saver so we're going to just leave it here for anyone to use.

It approximates the common trick of copy/pasting gameobjects from play mode to edit mode. We couldn't find a way to do this exactly as Unity does, so it serializes and deserializes gameobject hierarchies manually, mostly using UnityEngine.JSONUtility.

### Advantages

The main reason for this is to save newly created or destroyed unity objects (gameobjects and components) as well as serialized fields. Crucially, it'll also maintain object references to objects inside or outside the hierarchies you're saving. 

### Disadvantages

It's a brute force sort of solution. This means:
- You can't currently make exceptions to what's saved (potentially solvable)
- It'll break any references from outside the list of things to save into the list of things to save (potentially solvable)
- I'll make source control commits impossible to read (potentially solvable, but it's a massive undertaking)
- Ruins the undo queue (potentially solvable)
- Breaks prefab connections
- Deselects and closes a previously opened hierarchy (not investigated)
- Cant save anything marked static, since static meshes are combined and don’t have asset files

## Usage

Add the SavePlayModeChanges component to the root of any hierarchies you'd like saved.

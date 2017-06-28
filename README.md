# Save Play Mode Changes in Unity

Unity tool allowing changes made in play mode to be restored upon returning to edit mode.

## Usage

Add the SavePlayModeChanges component to the root of any hierarchies you'd like saved. That's it!

## Method

Unlike other tools (such as PlayModePersist), this approximates the common trick of copy/pasting gameobjects from play mode to edit mode. 

We couldn't find a way to do this exactly as Unity does, so it serializes and deserializes gameobject hierarchies manually, mostly using UnityEngine.JSONUtility.

It's more of a hammer than a scalpel, but despite its drawbacks it can be a huge time saver so we're releasing it for anyone to use and improve.
**This tool is experimental. If something goes wrong, backups of your scenes are saved to a Backups folder on your desktop.**

### Advantages

The main reason for this is to:
- Save newly created or destroyed Unity objects (GameObjects and Components)
- Saves changes made to serialized fields
- Maintains object references _to_ objects inside or outside the hierarchies you're saving.

### Disadvantages

It's a brute force sort of solution. This means:
- You can't currently make exceptions to what's saved
- It'll break any references _from outside the_ list of things to save _into_ the list of things to save
- Makes lots of source control changes
- Breaks prefab connections
- Deselects and closes a previously selected and expanded hierarchy (not investigated)
- Can't save anything marked static, since static meshes are combined and don’t have asset files
- We've not found one in a while, but some components may not save properly

## How it works

The SavePlayModeChangesChecker class finds all references to SavePlayModeChanges components on exiting the game. It serializes the entire hierarchy for those objects, and on entering play mode deletes the old hierarchies and creates the new ones.

## Other issues

- Undoing restored changes can break object references
- Small wait time when exiting play mode if something requires restoring
- Only scenes that are open in edit mode can be restored, and changing scenes in game will prevent the unloaded scenes being saved

## License

SavePlayModeChanges is released under the MIT license. Although we don't require attribution, we'd love to hear feedback, and Twitter follows ([@inklestudios](https://twitter.com/inklestudios)) are always appreciated!

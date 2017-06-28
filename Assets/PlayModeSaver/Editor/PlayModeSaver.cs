using UnityEngine;
using UnityEngine.Serialization;
using UnityEditor;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace PlayModeSaver {
	// These scripts form the meat of the Save Play Mode Changes
	// Unlike the other existing solutions, it works by serializing the entire hierarchy of any gameobjects containing the SavePlayModeChanges script.
	// It behaves like the trick of copy/pasting GameObjects from play mode to edit mode, and deleting the old GameObjects.
	// That means it'll save newly created or destroyed GameObjects and components, and not just changed fields! 
	// Crucially, it'll maintain object references to objects inside or outside the hierarchy you're saving.
	// HOWEVER
	// It doesn't save any references to the GameObjects you've restored from any object outside the list of restored objects.
	// It also doesn't let you exclude certain things.
	
	// Current issues:

	// Destroying the original using DestroyImmediate on exiting play mode takes a bit of time, depending on the amount of stuff. Not sure we can fix this.

	// Only scenes that are open in edit mode can be restored, and changing scenes in game will prevent the unloaded scenes being saved.
	// This is because we only save on exiting play mode. We could instead save on unloading a scene?

	// Creates a lot of source control churn. 
	// This isn't going to be fixable unless we rebuild the deserializer to only make modifications for changes, and that's not an easy job.

	// Can't choose to ignore certain gameobjects, components or fields. 
	// This would require saving the original state on entering play mode, cross reference on exiting play mode to match fields, and restore the original data. 
	// GameObjects, components and fields could all be restored like this, although it'd be complex for anything except two hierarchies which match perfectly.
	// for gameobjects and components you could otherwise maintain the originals and copy them into the new hierarchy, but this wouldn't work for fields.

	// After undoing play mode changes, the name of the redo menu item is "Create Object". I suspect this is a Unity bug.

	/// <summary>
	/// Play mode saver.
	/// Allows saving and restoring of gameobject hierarchies.
	/// </summary>
	public static class PlayModeSaver {

		/// <summary>
		/// Serialize the specified gameObjects and all their children.
		/// </summary>
		/// <param name="gameObjects">Game objects.</param>
		public static SerializedSelection Serialize (IList<GameObject> gameObjects) {
			Serializer serializer = new Serializer(gameObjects);
			return serializer.Serialize();
		}

		// Checks if this data can be deserialized
		public static bool CanDeserialize (SerializedSelection serializedSelection) {
			if(serializedSelection.foundStatic) return false;
			foreach(int index in serializedSelection.indexOfRootGOs) {
				var serializedGameObject = serializedSelection.serializedGameObjects [index];
				Scene scene = EditorSceneManager.GetSceneByPath(serializedGameObject.scenePath);
				if(scene.isLoaded) return true;
			}
			return false;
		}
			
		/// <summary>
		/// Deserialize the specified serializedSelection and optionally destroy the originals.
		/// </summary>
		/// <param name="serializedSelection">Serialized selection.</param>
		/// <param name="destroyOriginals">If set to <c>true</c> destroy originals.</param>
		/// <returns>Returns the root level restored GameObjects</returns>
		public static GameObject[] Deserialize (SerializedSelection serializedSelection, bool destroyOriginals) {
			Deserializer deserializer = new Deserializer(serializedSelection, destroyOriginals);
			var clonedGameObjects = deserializer.Deserialize();
			return clonedGameObjects;
		}


		class Serializer {
			IList<GameObject> rawGameObjects;
			SerializedSelection serializedSelection;
			List<GameObject> rootGameObjectsToCopy;
			List<GameObject> allGameObjectsToCopy;
			List<UnityEngine.Object> allComponentsInGameObjectsToCopyHierarchy;

			public Serializer (IList<GameObject> rawGameObjects) {
				this.rawGameObjects = rawGameObjects;
			}
			public SerializedSelection Serialize () {
				rootGameObjectsToCopy = GetRootGameObjects(rawGameObjects);
				allGameObjectsToCopy = new List<GameObject>();
				rootGameObjectsToCopy.ForEach(x => {
					List<GameObject> tree = new List<GameObject>();
					GetTree(x, ref tree);
					allGameObjectsToCopy.AddRange(tree);
				});
				allComponentsInGameObjectsToCopyHierarchy = GetAllObjects(rootGameObjectsToCopy);

				serializedSelection = new SerializedSelection();
				Serialize(rootGameObjectsToCopy);
				return serializedSelection;
			}

			// Gets all selected gameobjects that aren't parented by another in the selected list
			List<GameObject> GetRootGameObjects (IList<GameObject> gameObjects) {
				List<GameObject> rootGameObjects = new List<GameObject>();
				if(gameObjects.Count == 1) {
					rootGameObjects.Add(gameObjects.First());
				} else {
					foreach(GameObject gameObject in gameObjects) {
						if(gameObjects.Any(x => x != gameObject && !gameObject.transform.IsChildOf(x.transform))) {
							rootGameObjects.Add(gameObject);
						}
					}
				}
				return rootGameObjects;
			}

			List<UnityEngine.Object> GetAllObjects (List<GameObject> gameObjects) {
				List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
				allGameObjectsToCopy.ForEach(x => {
					objects.Add(x.gameObject);
					List<Component> components = x.GetComponents<Component>().ToList();
					components.ForEach(y => {
						objects.Add(y);
					});
				});
				return objects;
			}

			void Serialize (List<GameObject> gameObjectsToSerialize) {
				foreach(GameObject gameObject in gameObjectsToSerialize) {
					serializedSelection.indexOfRootGOs.Add(serializedSelection.serializedGameObjects.Count);
					serializedSelection.idOfRootGOs.Add(gameObject.GetInstanceID());
					SerializeGameObject(gameObject);
				}
			}

			void SerializeGameObject(GameObject gameObject) {
				SerializedGameObject sgo = new SerializedGameObject();
				sgo.serializedData = EditorJsonUtility.ToJson(gameObject, false);
				sgo.savedInstanceIDs = GetInstanceReferenceIDs(gameObject);

				
				sgo.scenePath = gameObject.scene.path;

				sgo.hasParent = gameObject.transform.parent != null;
				sgo.parentID = sgo.hasParent ? gameObject.transform.parent.GetInstanceID() : 0;
				sgo.siblingIndex = gameObject.transform.GetSiblingIndex();
				
				sgo.childCount = gameObject.transform.childCount;
				sgo.indexOfFirstChild = serializedSelection.serializedGameObjects.Count+1;

				foreach(var component in gameObject.GetComponents<Component>()) {
					if(component == null) continue;
					var serializedComponent = SerializeComponent(component);
					sgo.serializedComponents.Add(serializedComponent);
				}

				serializedSelection.serializedGameObjects.Add(sgo);

				if(gameObject.isStatic) {
					serializedSelection.foundStatic = true;
					Debug.LogWarning("PlayModeSaver tried to serialize static GameObject "+gameObject+". This is not allowed.");
				}

				foreach (Transform child in gameObject.transform)
					SerializeGameObject (child.gameObject);
			}

			SerializedComponent SerializeComponent (Component component) {
				SerializedComponent serializedComponent = new SerializedComponent(component.GetType(), EditorJsonUtility.ToJson(component, false));
				serializedComponent.savedInstanceIDs = GetInstanceReferenceIDs(component);
				return serializedComponent;
			}

			List<InstanceReference> GetInstanceReferenceIDs (UnityEngine.Object obj) {
				List<InstanceReference> ids = new List<InstanceReference>();
				SerializedObject so = new SerializedObject(obj);
				var prop = so.GetIterator();
				while (prop.NextVisible (true)) {
					if(prop.propertyType == SerializedPropertyType.ObjectReference) {
						if(prop.objectReferenceValue == null) {
							ids.Add(new InstanceReference());
						} else if (allComponentsInGameObjectsToCopyHierarchy.Contains(prop.objectReferenceValue)) {
							int index = allComponentsInGameObjectsToCopyHierarchy.IndexOf(prop.objectReferenceValue);
							ids.Add(new InstanceReference(index, true));
						} else {
							ids.Add(new InstanceReference(prop.objectReferenceInstanceIDValue, false));
						}
					}
				}
				return ids;
			}

			static void GetTree(GameObject go, ref List<GameObject> gameObjects) {
				gameObjects.Add(go);
				foreach (Transform child in go.transform)
					GetTree (child.gameObject, ref gameObjects);
			}
		}

		class Deserializer {
			SerializedSelection serializedSelection;
			bool destroyOriginals;

			List<UnityEngine.Object> deserializedObjects = new List<UnityEngine.Object>();
			List<DeserializedGameObject> deserializedGameObjects = new List<DeserializedGameObject>();
			List<DeserializedComponent> deserializedComponents = new List<DeserializedComponent>();
			Dictionary<string, Assembly> loadedAssemblies = new Dictionary<string, Assembly>();

			class DeserializedGameObject {
				public SerializedGameObject serializedGameObject;
				public GameObject gameObject;

				public DeserializedGameObject (SerializedGameObject serializedGameObject, GameObject gameObject) {
					this.serializedGameObject = serializedGameObject;
					this.gameObject = gameObject;
				}
			}

			class DeserializedComponent {
				public SerializedComponent serializedComponent;
				public Component component;

				public DeserializedComponent (SerializedComponent serializedComponent, Component component) {
					this.serializedComponent = serializedComponent;
					this.component = component;
				}
			}

			public Deserializer (SerializedSelection serializedSelection, bool destroyOriginals) {
				this.serializedSelection = serializedSelection;
				this.destroyOriginals = destroyOriginals;
			}

			void Reset () {
				deserializedObjects = new List<UnityEngine.Object>();
				deserializedGameObjects = new List<DeserializedGameObject>();
				deserializedComponents = new List<DeserializedComponent>();
				loadedAssemblies = new Dictionary<string, Assembly>();
			}

			public GameObject[] Deserialize () {
				Reset();

				int undoIndex = Undo.GetCurrentGroup();
				Undo.IncrementCurrentGroup();
				Undo.SetCurrentGroupName("Restore Play Mode Changes");

				// Do this first, since otherwise it can interfere with restoring the sibling indices.
				if(destroyOriginals)
					DestroyOriginals();

				GameObject go = null;
				foreach(int index in serializedSelection.indexOfRootGOs) {
					var serializedGameObject = serializedSelection.serializedGameObjects [index];
					Scene scene = EditorSceneManager.GetSceneByPath(serializedGameObject.scenePath);
					if(!scene.isLoaded) continue;

					ReadNodeFromSerializedNodes(index, out go);
				}
				RestoreInternalObjectReferences();

				var deserializedRootGameObjects = deserializedGameObjects.Where(x => serializedSelection.indexOfRootGOs.Contains(x.serializedGameObject.indexOfFirstChild-1)).Select(x => x.gameObject).ToArray();

				// Enforces child index when redoing
				foreach(var g in deserializedRootGameObjects)
					Undo.SetTransformParent (g.transform, g.transform.parent, "Creat");

				Undo.CollapseUndoOperations(undoIndex);
				return deserializedRootGameObjects;
			}

			void DestroyOriginals () {
				foreach(var id in serializedSelection.idOfRootGOs) {
					GameObject originalRootGO = EditorUtility.InstanceIDToObject(id) as GameObject;
					if(originalRootGO == null) continue;
					Undo.DestroyObjectImmediate(originalRootGO);
				}
			}

			int ReadNodeFromSerializedNodes(int index, out GameObject go) {
				var serializedGameObject = serializedSelection.serializedGameObjects [index];
				var newGameObject = RestoreGameObject(serializedGameObject);

				Scene scene = EditorSceneManager.GetSceneByPath(serializedGameObject.scenePath);
				if(!scene.isDirty) EditorSceneManager.MarkSceneDirty(scene);
				Undo.MoveGameObjectToScene(newGameObject, scene, "Move GameObject to scene");
				// The tree needs to be read in depth-first, since that's how we wrote it out.
				for (int i = 0; i != serializedGameObject.childCount; i++) {
					GameObject childGO;
					index = ReadNodeFromSerializedNodes (++index, out childGO);
					childGO.transform.SetParent(newGameObject.transform, false);
				}
				go = newGameObject;
				return index;
			}

			GameObject RestoreGameObject (SerializedGameObject serializedGameObject) {
				GameObject gameObject = new GameObject();
				Undo.RegisterCreatedObjectUndo (gameObject, "Create");

				deserializedObjects.Add(gameObject);
				deserializedGameObjects.Add(new DeserializedGameObject(serializedGameObject, gameObject));
				EditorJsonUtility.FromJsonOverwrite(serializedGameObject.serializedData, gameObject);
				RestoreObjectReference(serializedGameObject.savedInstanceIDs, gameObject);

				RestoreComponents(gameObject, serializedGameObject.serializedComponents);
				return gameObject;
			}

			void RestoreComponents (GameObject go, List<SerializedComponent> serializedComponents) {
				foreach(var serializedComponent in serializedComponents) {
					RestoreComponent(go, serializedComponent);
				}
			}

			void RestoreComponent (GameObject go, SerializedComponent serializedComponent) {
				Component component = null;

				if(!loadedAssemblies.ContainsKey(serializedComponent.assemblyName))
					loadedAssemblies.Add(serializedComponent.assemblyName, Assembly.Load(serializedComponent.assemblyName));
				Type type = loadedAssemblies[serializedComponent.assemblyName].GetType(serializedComponent.typeName);
				Debug.Assert(type != null, "Type '"+serializedComponent.typeName+"' not found in assembly '"+serializedComponent.assemblyName+"'");

				if (type == typeof(Transform)) component = go.transform;
				else component = Undo.AddComponent(go, type);

//				bool restore = true;
//				if(restore) {
					EditorJsonUtility.FromJsonOverwrite(serializedComponent.serializedData, component);
					RestoreObjectReference(serializedComponent.savedInstanceIDs, component);
//				}
				deserializedObjects.Add(component);
				deserializedComponents.Add(new DeserializedComponent(serializedComponent, component));
			}

			void RestoreObjectReference (List<InstanceReference> savedInstanceIDs, UnityEngine.Object obj) {
				SerializedObject so = new SerializedObject(obj);
				var prop = so.GetIterator();
				int i = 0;
				while (prop.NextVisible (true)) {
					if(prop.propertyType == SerializedPropertyType.ObjectReference) {
						if(!savedInstanceIDs[i].isNull && !savedInstanceIDs[i].isInternal) {
							var refObj = EditorUtility.InstanceIDToObject(savedInstanceIDs[i].id);
							if(refObj == null) Debug.LogWarning("Object reference with saved id "+savedInstanceIDs[i]+" on "+obj+" could not be found. This is likely a bug.");
							prop.objectReferenceValue = refObj;
						}
						i++;
					}
				}
				so.ApplyModifiedProperties();
			}

			// Some things can't be restored until all the gameobjects and components have been created. Do them now.
			void RestoreInternalObjectReferences () {
				foreach(var deserializedGameObject in deserializedGameObjects) {
					// The root gameobjects need their parents restored
					if(deserializedGameObject.gameObject.transform.parent == null && deserializedGameObject.serializedGameObject.hasParent) {
						UnityEngine.Object o = EditorUtility.InstanceIDToObject(deserializedGameObject.serializedGameObject.parentID);
						if(o == null || (Transform)o == null) return;
						// Note that this ought to use Undo.SetTransformParent, but you can't currently set worldPositionStays using it.
						deserializedGameObject.gameObject.transform.SetParent((Transform)o, false);
					}
					deserializedGameObject.gameObject.transform.SetSiblingIndex(deserializedGameObject.serializedGameObject.siblingIndex);
				}


				foreach(var deserializedComponent in deserializedComponents) {
					SerializedObject so = new SerializedObject(deserializedComponent.component);
					var prop = so.GetIterator();
					int i = 0;
					while (prop.NextVisible (true)) {
						if(prop.propertyType == SerializedPropertyType.ObjectReference) {
							if(!deserializedComponent.serializedComponent.savedInstanceIDs[i].isNull && deserializedComponent.serializedComponent.savedInstanceIDs[i].isInternal) {
								prop.objectReferenceValue = deserializedObjects[deserializedComponent.serializedComponent.savedInstanceIDs[i].id];
							}
							i++;
						}
					}
					so.ApplyModifiedProperties();
				}
			}
		}

		[System.Serializable]
		public class SerializedSelection {
			public List<int> indexOfRootGOs = new List<int>();
			public List<int> idOfRootGOs = new List<int>();
			public List<SerializedGameObject> serializedGameObjects = new List<SerializedGameObject>();
			public bool foundStatic;
		}

		[System.Serializable]
		public class SerializedGameObject {
			[TextArea]
			public string serializedData;
			public List<InstanceReference> savedInstanceIDs = new List<InstanceReference>();

			public string scenePath;

			public bool hasParent;
			public int parentID;
			public int siblingIndex;
			
			public int childCount;
			public int indexOfFirstChild;

			public List<SerializedComponent> serializedComponents = new List<SerializedComponent>();
		}


		[System.Serializable]
		public class SerializedComponent {
			public string assemblyName;
			public string typeName;
			[TextArea]
			public string serializedData;
			public List<InstanceReference> savedInstanceIDs = new List<InstanceReference>();
			public SerializedComponent (Type type, string serializedData) {
				this.assemblyName = type.Assembly.GetName().Name;
				this.typeName = type.FullName;
				this.serializedData = serializedData;
			}
		}

		// Serializes the instance IDs of any object reference fields. If internal, the index of the object in the serializer list is stored instead.
		[System.Serializable]
		public class InstanceReference {
			public bool isNull;
			public int id;
			public bool isInternal;

			public InstanceReference () {
				isNull = true;
			}

			public InstanceReference (int id, bool isInternal) {
				this.id = id;
				this.isInternal = isInternal;
			}
		}
	}
}

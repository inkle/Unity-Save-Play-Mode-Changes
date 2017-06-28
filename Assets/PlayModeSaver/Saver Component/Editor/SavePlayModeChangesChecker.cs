using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;
using UnityEditor.SceneManagement;

namespace PlayModeSaver {
	[InitializeOnLoad]
	// This class saves any gameobjects containing the SavePlayModeChanges component on exiting play mode, and attempts to load them on entering edit mode right afterwards.
	// If any changes are detected, the entire set of SavePlayModeChanges are loaded in (not just in the hierarchies containing changes), allowing for cross references to remain.
	public static class SavePlayModeChangesChecker {

		const string ppKey = "PersistSerializationClipboard";

		public delegate void OnRestorePlayModeChangesDelegate (GameObject[] restoredRootGameObjects);
		public static event OnRestorePlayModeChangesDelegate OnRestorePlayModeChanges;

		static SavePlayModeChangesChecker () {
			EditorApplication.playmodeStateChanged += OnChangePlayModeState;
		}

		static void OnChangePlayModeState () {
			// Create backups of the scenes before you enter play mode, because this thing is pretty destructive and you can lose work if it goes wrong.
			if(!EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode) {
				if(AnySceneDirty()) {
					EditorSceneManager.SaveOpenScenes();
				}
				for(int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) {
					var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
					string absolutePath = UnityRelativeToAbsolutePath(scene.path);

					string writePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
					writePath = System.IO.Path.Combine(writePath, "Scene Backups");
					if(!System.IO.Directory.Exists(writePath))
						System.IO.Directory.CreateDirectory(writePath);
					System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(writePath);
					foreach (System.IO.FileInfo file in di.GetFiles()) {
					    file.Delete(); 
					}
					writePath = System.IO.Path.Combine(writePath, scene.name+".unity");
					System.IO.File.Copy(absolutePath, writePath, true);
				}
			}

			// Not the most robust way of checking this, but it's not really an issue if we save more than once.
			if (EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) {
				Save();
			} else if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) {
				Load();
			}
		}

		static void Save () {
			var gameObjectsToPersist = GetGameObjectsToPersist();
			var serializedData = PlayModeSaver.Serialize(gameObjectsToPersist);
			string stringSerializedSelection = EditorJsonUtility.ToJson(serializedData);
			EditorPrefs.SetString(ppKey, stringSerializedSelection);
		}

		static GameObject[] GetGameObjectsToPersist () {
			var persist = Object.FindObjectsOfType<SavePlayModeChanges>();
			return persist.Where(x => x.IsValid()).Select(x => x.gameObject).ToArray();
		}

		static void Load () {
			if(!EditorPrefs.HasKey(ppKey)) return;

			string serializedDataString = EditorPrefs.GetString(ppKey);
			EditorPrefs.DeleteKey(ppKey);

			// It's faster to check for differences than to actually restore, so we do that first.
			if(!CheckForChanges(serializedDataString)) return;

			var serializedData = new PlayModeSaver.SerializedSelection();
			EditorJsonUtility.FromJsonOverwrite(serializedDataString, serializedData);

			if(!PlayModeSaver.CanDeserialize(serializedData)) {
				if(serializedData.foundStatic) {
					Debug.LogError("SavePlayModeChangesChecker SerializedSelection data contains a gameobject with the static flag. The static flag combines meshes on mesh filters, and so cannot properly restore them.\nIf you would like to rescue the data, it has been stored in EditorPrefs at key '"+serializedDataString+"'.");
					PlayerPrefs.SetString(ppKey, serializedDataString);
				}
				return;
			}

			EditorUtility.DisplayProgressBar("Save Edit Mode Changes", "Restoring Edit Mode GameObjects...", 0);

			try {
				var restoredGameObjects = PlayModeSaver.Deserialize(serializedData, true);
				LogRestoredData(restoredGameObjects);
				EditorUtility.ClearProgressBar();
				if(OnRestorePlayModeChanges != null) OnRestorePlayModeChanges(restoredGameObjects);
			} catch {
				Debug.LogError("Play mode saver failed to restore data after destroying originals. Scene backups were placed on your desktop which will allow you to recover data.");
				EditorUtility.ClearProgressBar();
			}
		}

		static void LogRestoredData (GameObject[] restoredGameObjects) {
			System.Text.StringBuilder sb = new System.Text.StringBuilder("Save Play Mode Changes restored "+restoredGameObjects.Length+" GameObject hierarchies:");
			foreach(var restoredGameObject in restoredGameObjects) {
				sb.Append("\n");
				sb.Append(restoredGameObject.name);
			}
			Debug.Log(sb.ToString());
		}

		/// <summary>
		/// Checks for changes between the saved play mode data and the edit mode data.
		/// </summary>
		/// <returns><c>true</c>, if for changes was checked, <c>false</c> otherwise.</returns>
		/// <param name="serializedData">Serialized data.</param>
		static bool CheckForChanges (string serializedDataString) {
			// We didn't save anything.
//			if(serializedData.serializedGameObjects.Count == 0) return false;

			// Serialize the actual data, and check to see if they're the same.
			var gameObjectsToPersist = GetGameObjectsToPersist();
			var editModeSerializedData = PlayModeSaver.Serialize(gameObjectsToPersist);

			return (serializedDataString != EditorJsonUtility.ToJson(editModeSerializedData));

			// A wayyy more convoluted version of the same approach
			/*
			if(serializedData.serializedGameObjects.Count != editModeSerializedData.serializedGameObjects.Count) return true;

			for(int i = 0; i < serializedData.serializedGameObjects.Count; i++) {
				var serializedGameObject = serializedData.serializedGameObjects[i];
				var editModeSerializedGameObject = editModeSerializedData.serializedGameObjects[i];
//				Debug.Log(serializedGameObject.serializedData != editModeSerializedGameObject.serializedData);
				if(serializedGameObject.serializedData != editModeSerializedGameObject.serializedData) return true;
//				if(serializedGameObject.childCount != editModeSerializedGameObject.childCount) return true;
				if(serializedGameObject.hasParent != editModeSerializedGameObject.hasParent) return true;
//				if(serializedGameObject.indexOfFirstChild != editModeSerializedGameObject.indexOfFirstChild) return true;
				if(serializedGameObject.parentID != editModeSerializedGameObject.parentID) return true;
//				if(serializedGameObject.savedInstanceIDs != editModeSerializedGameObject.savedInstanceIDs) return true;
				if(serializedGameObject.scenePath != editModeSerializedGameObject.scenePath) return true;
//				if(serializedGameObject.siblingIndex != editModeSerializedGameObject.siblingIndex) return true;

				if(serializedGameObject.serializedComponents.Count != editModeSerializedGameObject.serializedComponents.Count) return true;

				for(int j = 0; j < serializedGameObject.serializedComponents.Count; j++) {
					var serializedComponent = serializedGameObject.serializedComponents[j];
					var editModeSerializedComponent = editModeSerializedGameObject.serializedComponents[j];

					if(serializedComponent.serializedData != editModeSerializedComponent.serializedData) return true;
					if(serializedComponent.savedInstanceIDs.Count != editModeSerializedComponent.savedInstanceIDs.Count) return true;

					for(int k = 0; k < serializedData.serializedGameObjects[i].serializedComponents[j].savedInstanceIDs.Count; k++) {
						var instanceReference = serializedComponent.savedInstanceIDs[k];
						var editModeInstanceReference = editModeSerializedComponent.savedInstanceIDs[k];

						if(instanceReference.isNull != editModeInstanceReference.isNull) return true;
						if(instanceReference.isInternal != editModeInstanceReference.isInternal) return true;
						if(instanceReference.id != editModeInstanceReference.id) return true;
					}
				}
			}

			return false;
			*/
		}

		static bool AnySceneDirty () {
			for(int i = 0; i < EditorSceneManager.loadedSceneCount; i++) {
				if(EditorSceneManager.GetSceneAt(i).isDirty) return true;
			}
			return false;
		}

		static string UnityRelativeToAbsolutePath(string filePath) {
			return Path.Combine(Application.dataPath, filePath.Substring(7));
		}
	}
}
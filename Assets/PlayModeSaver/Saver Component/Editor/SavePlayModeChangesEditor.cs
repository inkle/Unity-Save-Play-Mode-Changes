using UnityEngine;
using UnityEditor;

namespace PlayModeSaver {
	[CustomEditor(typeof(SavePlayModeChanges))]
	public class SavePlayModeChangesEditor : Editor {
		public override void OnInspectorGUI () {
			if(target == null || (SavePlayModeChanges)target == null) return;
			SavePlayModeChanges data = (SavePlayModeChanges)target;
			if(data.AnyDescendentIsStatic()) {
				EditorGUILayout.HelpBox("A descendent is static.\nCannot properly save or restore statics. This component will be ignored.", MessageType.Warning);
			} else if(data.AnyAncestorHasThisComponent()) {
				EditorGUILayout.HelpBox("An ancestor has an active SavePlayModeChanges component.\nThis one will be ignored since the gameobject will be saved by the ancestor.", MessageType.Warning);
			} else {
				if(data.enabled) {
					EditorGUILayout.HelpBox("Saves all changes made during play mode.", MessageType.Info);
				} else {
					EditorGUILayout.HelpBox("Enable to save play mode changes.", MessageType.Warning);
				}
			}
		}
	}
}
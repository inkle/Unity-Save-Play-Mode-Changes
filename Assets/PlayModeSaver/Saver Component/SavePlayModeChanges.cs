using UnityEngine;
using System.Collections.Generic;

namespace PlayModeSaver {
	// This component, when added to a gameobject and enabled, will mark the gameobject and it's hierarchy to be restored after exiting play mode.
	public class SavePlayModeChanges : MonoBehaviour {
		void OnEnable () {}

		public bool IsValid () {
			return enabled && !AnyAncestorHasThisComponent() && !AnyDescendentIsStatic();
		}

		public bool AnyAncestorHasThisComponent () {
			List<Transform> ancestors = GetAllAncestors(transform);
			foreach(var ancestor in ancestors) {
				if(ancestor.GetComponent<SavePlayModeChanges>() && ancestor.GetComponent<SavePlayModeChanges>().enabled) {
					return true;
				}
			}
			return false;
		}

		public bool AnyDescendentIsStatic () {
			List<Transform> descendents = GetAllDescendents(transform);
			foreach(var descendent in descendents) {
				if(descendent.gameObject.isStatic) {
					return true;
				}
			}
			return false;
		}

		List<Transform> GetAllAncestors (Transform _transform) {
			List<Transform> parents = new List<Transform>();
			while (_transform.parent != null) {
				_transform = _transform.parent;
				parents.Add(_transform);
		    }
		    return parents;
		}

		List<Transform> GetAllDescendents(Transform current, List<Transform> transforms = null) {
			if(transforms == null) transforms = new List<Transform>();
			transforms.Add(current);
			for (int i = 0; i < current.childCount; ++i) {
				GetAllDescendents(current.GetChild(i), transforms);
			}
			return transforms;
		}
	}
}
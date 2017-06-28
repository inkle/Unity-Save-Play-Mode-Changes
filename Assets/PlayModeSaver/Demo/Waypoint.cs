using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Waypoint : MonoBehaviour {

	public List<Waypoint> connected;

	void OnDrawGizmos () {
		Gizmos.color = Color.white;
		Gizmos.DrawSphere(transform.position, 0.5f);

		foreach(var other in connected) {
			if(other == null) continue;
			Gizmos.DrawLine(transform.position, other.transform.position);
		}
	}
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Character : MonoBehaviour {

	public Color color = Color.red;
	public float moveSpeed = 1;
	public Waypoint favouriteWaypoint;

	void Update () {
		if(favouriteWaypoint != null)
			transform.position = Vector3.MoveTowards(transform.position, favouriteWaypoint.transform.position, moveSpeed * Time.deltaTime);
	}

	void OnDrawGizmos () {
		Gizmos.color = color;

		Gizmos.DrawSphere(transform.position, 1);
	}
}

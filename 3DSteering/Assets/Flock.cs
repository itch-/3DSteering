using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Flock : MonoBehaviour {

    public SteeringAI target;
    public List<SteeringAI> flockList;
    public float radius = 10f;
    public float alignWeight = 1f;
    public float centerWeight = 1f;
    public float separateWeight = 1f;

    void Start () {
        flockList = new List<SteeringAI>();
        foreach (GameObject go in GameObject.FindObjectsOfType<GameObject>())
        {
            if (go.name.StartsWith("Cube AI flocking"))
            {
                flockList.Add(go.GetComponent<SteeringAI>());
                go.GetComponent<SteeringAI>().SetFlock(this);
            }
        }
	}
}

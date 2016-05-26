using UnityEngine;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SteeringAI : MonoBehaviour
{
    private Rigidbody rbody;
    public bool stationary = false;
    public SteeringAI target;
    public SteeringAI evade;
    private float minSpeed = 4f;
    private float maxSpeed = 10f;
    private float maxForce = 8f;
    private float mass = 30f;

    private float seekDistance = 10f;
    private float arriveDistance = 20f;
    private float stayDistance = 100f;
    private float distanceRamp = 5f;

    private float wandertargetRadius = 2f;
    private float wandertargetDistance = 3f;
    private float wandertargetMaxchange = 10f;
    private float wanderHeight = 10f;
    private float wanderheightMargin = 3f;
    
    public Vector3 velocity = Vector3.forward;
    private Vector3 desired_velocity = Vector3.zero;
    private Vector3 wanderAngle = Vector3.forward;
    private Transform[] rayTargets;

    public Transform futureCollider;

    private Flock flock = null;

	void Start ()
    {
        velocity = Random.onUnitSphere;

        minSpeed *= Time.fixedDeltaTime;
        maxSpeed *= Time.fixedDeltaTime;
        maxForce *= Time.fixedDeltaTime;
        rbody = GetComponent<Rigidbody>();

        GameObject go = new GameObject("future collider");
        go.layer = LayerMask.NameToLayer("FutureObjects");
        if (GetComponent<Collider>().GetType() == typeof(BoxCollider)) {
            go.AddComponent<BoxCollider>();
            go.GetComponent<BoxCollider>().center = GetComponent<BoxCollider>().center;
            go.GetComponent<BoxCollider>().size = GetComponent<BoxCollider>().size;
        } else {
            go.AddComponent<SphereCollider>();
            go.GetComponent<SphereCollider>().center = GetComponent<SphereCollider>().center;
            go.GetComponent<SphereCollider>().radius = GetComponent<SphereCollider>().radius;
        }
        futureCollider = go.transform;
        futureCollider.parent = transform;
        futureCollider.localPosition = Vector3.zero;

        rayTargets = new Transform[9];
        int i = 0;
        foreach (Transform t in transform.FindChild("raytargets"))
        {
            rayTargets[i] = t;
            i++;
        }
    }

    void FixedUpdate() {
        if (!stationary)
        {
            desired_velocity = Vector3.zero;
            if (target == null) {
                Wander();
                StayNear(Vector3.zero);
            } else {
                if (target.stationary) Go(target.transform.position);
                else Pursue(target);
                if (evade != null) Evade(evade);
            }
            if (flock != null) Flock();
            AvoidTerrain();
            AvoidCollision();
            Vector3 steering = desired_velocity - velocity;
            if (steering.sqrMagnitude > maxForce * maxForce) steering = Vector3.Normalize(steering) * maxForce;
            steering = steering / mass;

            velocity = velocity + steering;
            if (velocity.sqrMagnitude > maxSpeed * maxSpeed) velocity = Vector3.Normalize(velocity) * maxSpeed;
            else if (minSpeed > 0 && velocity.sqrMagnitude < minSpeed * minSpeed) velocity = Vector3.Normalize(velocity) * minSpeed;
            Debug.DrawLine(transform.position, transform.position + velocity, Color.blue);

            //transform.rotation = Quaternion.LookRotation(velocity);
            //transform.Translate(velocity, Space.World);

            //rbody.MoveRotation(Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(velocity), Time.deltaTime * 10f));
            rbody.MoveRotation(Quaternion.LookRotation(velocity)); // rotation must be accurate for raytargets
            rbody.MovePosition(transform.position + velocity);
            rbody.velocity = Vector3.zero;
            rbody.angularVelocity = Vector3.zero;

            futureCollider.localPosition = new Vector3(0, 0, velocity.magnitude / maxSpeed * 10f);
        }
    }

    public void SetFlock(Flock f)
    {
        flock = f;
        if (target == null) target = f.target;
    }

    private Vector3 Seek(Vector3 position)
    {
        float distance = Vector3.Distance(position, transform.position);
        float speed = maxSpeed;
        if (distance < seekDistance) speed = maxSpeed * (distance / seekDistance);
        return Vector3.Normalize(position - transform.position) * speed;
    }

    private void Wander() // terrain wandering, not outer space! rotates direction horizontally and stays within x height above terrain
    {
        Vector3 wander_center = velocity.normalized * wandertargetDistance;
        wanderAngle = Quaternion.Euler(0, Random.Range(-wandertargetMaxchange, wandertargetMaxchange), 0) * wanderAngle;
        desired_velocity += Vector3.Normalize(wander_center + wanderAngle * wandertargetRadius) * maxSpeed;

        RaycastHit hitinfo;
        if (Physics.Raycast(transform.position, Vector3.down, out hitinfo, 1000, 1 << 8))
        {
            if (Mathf.Abs(transform.position.y - (hitinfo.point.y + wanderHeight)) > wanderheightMargin) desired_velocity += Seek(new Vector3(futureCollider.position.x, hitinfo.point.y + wanderHeight, futureCollider.position.z));
        }
    }

    private void StayNear(Vector3 position)
    {
        float distance = Vector3.Distance(transform.position, position);
        if (distance > stayDistance)
        {
            desired_velocity += Seek(position) * ((distance - stayDistance) / distanceRamp);
#if UNITY_EDITOR
            if (Selection.activeGameObject == gameObject) Debug.DrawLine(transform.position, position, Color.green);
#endif
        }
    }

    private void Go(Vector3 position)
        {
        float distance = Vector3.Distance(transform.position, position);
        if (distance > arriveDistance)
        {
            desired_velocity += Seek(position) * ((distance - arriveDistance) / distanceRamp);
        } else Wander();
#if UNITY_EDITOR
        if (Selection.activeGameObject == gameObject) Debug.DrawLine(transform.position, position, Color.green);
#endif
    }

    private void Flee(Vector3 position)
    {
        desired_velocity -= Seek(position);
#if UNITY_EDITOR
        if (Selection.activeGameObject == gameObject) Debug.DrawLine(transform.position, position, Color.red);
#endif
    }

    private void Pursue(SteeringAI target)
    {
        float velocity_mod = Vector3.Magnitude(target.transform.position - transform.position) / target.maxSpeed;
        Vector3 future_position = target.transform.position + target.velocity * velocity_mod;
        desired_velocity += Seek(future_position);
#if UNITY_EDITOR
        if (Selection.activeGameObject == gameObject) Debug.DrawLine(transform.position, future_position, Color.green);
#endif
    }

    private void Evade(SteeringAI target)
    {
        float velocity_mod = Vector3.Magnitude(target.transform.position - transform.position) / target.maxSpeed;
        Vector3 future_position = target.transform.position + target.velocity * velocity_mod;
        desired_velocity -= Seek(future_position);
#if UNITY_EDITOR
        if (Selection.activeGameObject == gameObject) Debug.DrawLine(transform.position, future_position, Color.red);
#endif
    }



    private void Flock()
    {
        int buddies = 0;
        Vector3 buddy_velocity = Vector3.zero;
        Vector3 buddy_center = Vector3.zero;
        Vector3 personal_space = Vector3.zero;
        foreach (SteeringAI ai in flock.flockList)
        {
            if (ai != this && Vector3.Distance(transform.position, ai.transform.position) < flock.radius && Vector3.Angle(transform.forward, ai.transform.position - transform.position) < 270f)
            {
                buddy_velocity += ai.velocity;
                buddy_center += ai.transform.position;
                personal_space += Vector3.Normalize(ai.transform.position - transform.position) * (flock.radius / Vector3.Magnitude(ai.transform.position - transform.position));
                buddies++;
            }
        }
        if (buddies == 0) return;

        //Debug.Log("buddy_v = " + buddy_velocity.normalized + " = " + Vector3.Normalize(buddy_velocity / buddies) + " = " + (buddy_velocity / buddies));
        //buddy_velocity = Vector3.Normalize(buddy_velocity / buddies);
        buddy_velocity = buddy_velocity.normalized;
        buddy_center = Vector3.Normalize(buddy_center / buddies * (flock.radius / (1f - Vector3.Magnitude(buddy_center - transform.position))) - transform.position);
        personal_space = Vector3.Normalize(personal_space / buddies - transform.position) * -1f;

        Vector3 flock_velocity = buddy_velocity * flock.alignWeight + buddy_center * flock.centerWeight + personal_space * flock.separateWeight;
        desired_velocity += flock_velocity.normalized * 0.25f;
#if UNITY_EDITOR
        if (Selection.activeGameObject == gameObject)
        {
            Debug.DrawLine(transform.position, transform.position + buddy_velocity * 4f, Color.blue);
            Debug.DrawLine(transform.position, transform.position + buddy_center * 4f, Color.blue);
            Debug.DrawLine(transform.position, transform.position + personal_space * 4f, Color.blue);
            Debug.DrawLine(transform.position, transform.position + flock_velocity, Color.green);
        }
#endif
    }




    private void AvoidTerrain() // different from collision because it steers with the hit normal
    {
        /*
        float length = ai.velocity.magnitude / ai.maxSpeed * 10;
        Vector3 lookahead = transform.position + ai.velocity.normalized * length;
        Debug.DrawLine(transform.position, lookahead, Color.yellow);

        RaycastHit[] hitinfo = Physics.SphereCastAll(transform.position, 2f, lookahead - transform.position, length, 1 << 8);
        foreach (RaycastHit hit in hitinfo)
        {
            if (hit.transform.gameObject != gameObject)
            {
                desired_velocity += hit.normal * ai.maxSpeed;
                Debug.DrawLine(transform.position, hit.point, Color.red);
                Debug.DrawLine(transform.position, transform.position + hit.normal * 10f, Color.green);
                break;
            }
        }
        */

        for (int i = 0; i < 9; i++)
        {
            float length = velocity.magnitude / maxSpeed * 3f;
            if (i == 4) length = velocity.magnitude / maxSpeed * 10f;
            
            RaycastHit hitinfo;
            if (Physics.Raycast(transform.position, rayTargets[i].position - transform.position, out hitinfo, length * 2, 1 << 8)) // length*2 wut???
            {
                float strength = length / Vector3.Magnitude(transform.position - hitinfo.point) * 20f;
                desired_velocity += hitinfo.normal * maxSpeed * strength;
#if UNITY_EDITOR
                if (Selection.activeGameObject == gameObject)
                {
                    Debug.DrawLine(transform.position, transform.position + (rayTargets[i].position - transform.position) * length, Color.white);
                    Debug.DrawLine(transform.position, hitinfo.point, Color.red);
                    Debug.DrawLine(hitinfo.point, hitinfo.point + hitinfo.normal * 5f, Color.green);
                }
#endif
            }
        }
    }

    private void AvoidCollision()
    {
        /*
        float length = ai.velocity.magnitude / ai.maxSpeed * 10;
        Vector3 lookahead = transform.position + ai.velocity.normalized * length;
#if UNITY_EDITOR
        if (Selection.activeGameObject == gameObject)
        {
            Debug.DrawLine(transform.position, lookahead, Color.yellow);
        }
#endif

        RaycastHit[] hitinfo = Physics.SphereCastAll(transform.position, 1.5f, lookahead - transform.position, length, 1 << 10);
        //RaycastHit[] hitinfo = Physics.SphereCastAll(transform.position, 1.5f, lookahead - transform.position, length, 1 << 9 | 1 << 10);
        foreach (RaycastHit hit in hitinfo)
        {
            if (hit.transform.gameObject != gameObject && hit.transform.gameObject != ai.futureCollider.gameObject)
            {
                Vector3 hitpoint = hit.point;
                Vector3 direction = hitpoint - hit.transform.position;
                if (hit.point == Vector3.zero) // this is a physics bug, gotta handle it
                {
                    hitpoint = hit.transform.position;
                    direction = transform.position - hitpoint;
                }
                //desired_velocity += hitinfo.normal * Vector3.Magnitude(transform.position - hitinfo.point) * ai.maxSpeed;
                desired_velocity += Vector3.Normalize(direction) * ai.maxSpeed;
#if UNITY_EDITOR
                if (Selection.activeGameObject == gameObject)
                {
                    Debug.DrawLine(transform.position, hitpoint, Color.red);
                    Debug.DrawLine(hitpoint, hitpoint + direction * 5f, Color.green);
                }
#endif
                break;
            }
        }
        */


        /*
        for (int i = 0; i < 9; i++)
        {
            float length = ai.velocity.magnitude / ai.maxSpeed * 2f;
            if (i == 4) length = ai.velocity.magnitude / ai.maxSpeed * 5f;
            Debug.DrawLine(transform.position, transform.position + (rayTargets[i].position - transform.position) * length, Color.white);
            
            RaycastHit hitinfo;
            if (Physics.Raycast(transform.position, rayTargets[i].position - transform.position, out hitinfo, length * 2, 1 << 9))
            {
                desired_velocity += Vector3.Normalize(hitinfo.point - hitinfo.transform.position) * ai.maxSpeed;
                Debug.DrawLine(transform.position, hitinfo.point, Color.red);
                Debug.DrawLine(transform.position, (hitinfo.point - hitinfo.transform.position) * 10f, Color.green);
            }
        }
        */


        for (int i = 0; i < 9; i++)
        //for (int i = 4; i < 5; i++) // only check forward. good enough for small obstacles, not large ones
        {
            float length = velocity.magnitude / maxSpeed * 3f;
            if (i == 4) length = velocity.magnitude / maxSpeed * 10f;

            RaycastHit hitinfo;
            if (Physics.SphereCast(transform.position, 0.7f, rayTargets[i].position - transform.position, out hitinfo, length * 2, 1 << 9 | 1 << 10)) // length*2 wut???
            //if (Physics.Raycast(transform.position, rayTargets[i].position - transform.position, out hitinfo, length * 2, 1 << 9 | 1 << 10)) // length*2 wut???
            {
                if (hitinfo.transform != futureCollider && hitinfo.transform != transform)
                {
                    float strength = length / Vector3.Magnitude(transform.position - hitinfo.point) * 15f;
                    desired_velocity += Vector3.Normalize(hitinfo.point - hitinfo.transform.position + hitinfo.normal) * maxSpeed * strength; // away from center AND normal
#if UNITY_EDITOR
                    if (Selection.activeGameObject == gameObject)
                    {
                        Debug.DrawLine(transform.position, transform.position + (rayTargets[i].position - transform.position) * length, Color.white);
                        Debug.DrawLine(transform.position, hitinfo.point, Color.red);
                        Debug.DrawLine(hitinfo.point, hitinfo.point + Vector3.Normalize(hitinfo.point - hitinfo.transform.position + hitinfo.normal) * 5f, Color.green);
                    }
#endif
                }
            }
        }
    }

    void OnCollisionEnter(Collision collision) {
        Debug.Log("Collision! impulse force " + collision.impulse);
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.GetComponent<Collider>().enabled = false;
        go.transform.position = collision.contacts[0].point;
        go.GetComponent<MeshRenderer>().sharedMaterial.color = Color.red;

        CollisionDamage(collision);
    }

    void OnCollisioStay(Collision collision) {
        CollisionDamage(collision);
    }

    float hitpoints = 100f;
    private void CollisionDamage(Collision collision) {
        hitpoints -= collision.impulse.magnitude * 40f;
        if (hitpoints < 0f) {
            Debug.Log("Boid destroyed!");
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.GetComponent<Collider>().enabled = false;
            go.transform.position = collision.contacts[0].point;
            go.transform.localScale = Vector3.one * 2f;
            go.GetComponent<MeshRenderer>().sharedMaterial.color = Color.red;

            if (flock != null) flock.flockList.Remove(this);
            Destroy(gameObject);
        }
    }
}

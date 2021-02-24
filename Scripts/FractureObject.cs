using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FractureObject : MonoBehaviour
{

    public float mass = 100;
    public float jointBreakForce = 0;

    Vector3 CoM = Vector3.zero;

    public AudioClip onFractureSound;
    public GameObject onFractureVFX;

    public AudioClip onChunkCollideSound;
    public GameObject onChunkCollideVFX;

    public float chunkCollisionVelocityToCreateVFX = 5;

    bool fractureInitialized = false;

    List<ContactPoint> contactPoints;

    Vector3 normal;

    // Start is called before the first frame update
    void Start()
    {
        InitFracture();
    }

    void Update()
    {
    }

    public void InitFracture()
    {
        if (GetComponent<Rigidbody>())
            GetComponent<Rigidbody>().mass = mass;

        ChunkRuntimeInfo[] chunks = GetComponentsInChildren<ChunkRuntimeInfo>(true);
        for (int i = 0; i < chunks.Length; i++)
        {
            chunks[i].onFracture += OnFracture;

            Rigidbody rigidbody = chunks[i].gameObject.AddComponent<Rigidbody>();
            rigidbody.mass = mass / chunks.Length;

            JointMonitor jointMonitor = chunks[i].gameObject.AddComponent<JointMonitor>();
            jointMonitor.onJointRemove += OnJointRemove;

            Bounds chunkBounds = chunks[i].GetComponent<Renderer>().bounds;

            Collider[] colliders = Physics.OverlapBox(chunkBounds.center, chunkBounds.extents * 1.1f, Quaternion.identity);
            foreach (Collider collider in colliders)
            {
                if (collider.transform.parent == transform)
                {
                    Transform chunk2 = collider.transform;
                    Rigidbody rigidbody2 = chunk2.GetComponent<Rigidbody>();

                    bool alreadyConnected = false;
                    foreach (FixedJoint joint in chunk2.GetComponents<FixedJoint>())
                        if (joint.connectedBody == chunks[i].GetComponent<Rigidbody>())
                            alreadyConnected = true;

                    Vector3 deltaPos = collider.transform.GetComponent<Renderer>().bounds.center - chunkBounds.center;
                    Vector3 extents = chunkBounds.extents;
                    if (
                        (deltaPos.x > -extents.x && deltaPos.x < extents.x) &&
                        (deltaPos.y > -extents.y && deltaPos.y < extents.y) &&
                        (deltaPos.z > extents.z))
                    {
                        chunks[i].forwardConnections.Add(rigidbody2);
                    }
                    else if (
                        (deltaPos.x > -extents.x && deltaPos.x < extents.x) &&
                        (deltaPos.y > -extents.y && deltaPos.y < extents.y) &&
                        (deltaPos.z < -extents.z))
                    {
                        chunks[i].backwardConnections.Add(rigidbody2);
                    }
                    else if (deltaPos.y > extents.y)
                    {
                        chunks[i].topConnections.Add(rigidbody2);
                    }
                    else if (deltaPos.y < -extents.y)
                    {
                        chunks[i].bottomConnections.Add(rigidbody2);
                    }
                    else if (deltaPos.x < -extents.x)
                    {
                        chunks[i].leftConnections.Add(rigidbody2);
                    }
                    else if (deltaPos.x > extents.x)
                    {
                        chunks[i].rightConnections.Add(rigidbody2);
                    }
                    else
                        continue; // diagonal orientation?

                    if (!alreadyConnected)
                    {
                        FixedJoint joint = chunks[i].gameObject.AddComponent<FixedJoint>();

                        joint.connectedBody = rigidbody2;
                        joint.breakForce = jointBreakForce / 2;
                        //joint.enablePreprocessing = false;
                    }

                }
            }
            chunks[i].Init();
            chunks[i].gameObject.SetActive(false);
        }

        fractureInitialized = true;
    }

    void ActivateFracture(Vector3 impulse)
    {
        Destroy(GetComponent<MeshFilter>());
        Destroy(GetComponent<MeshRenderer>());
        Destroy(GetComponent<ConstantForce>());
        Destroy(GetComponent<Rigidbody>());
        foreach (Collider rootCollider in GetComponents<Collider>())
            Destroy(rootCollider);

        ChunkRuntimeInfo[] chunks = GetComponentsInChildren<ChunkRuntimeInfo>(true);
        foreach (ChunkRuntimeInfo chunk in chunks)
        {
            chunk.gameObject.SetActive(true);
        }

        
        foreach (ContactPoint contact in contactPoints)
        {
            Collider[] colliders = Physics.OverlapSphere(contact.point, 0.3f);
            if (colliders.Length == 0)
                continue;

            foreach (Collider collider in colliders)
            {
                if (collider.transform.parent == transform)
                {
                    collider.GetComponent<Rigidbody>().velocity += impulse;
                }
            }
        }

        foreach (ChunkRuntimeInfo chunk in chunks)
        {
            chunk.gameObject.layer = LayerMask.NameToLayer("Fracture");
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        List<ContactPoint> contacts = new List<ContactPoint>();
        collision.GetContacts(contacts);

        Vector3 l_normal = Vector3.zero;
        foreach (ContactPoint point in contacts)
            l_normal += point.normal;

        normal = l_normal;
    }

    private void OnCollisionStay(Collision collision)
    {

        Vector3 impulse = Vector3.zero;

        FractureObject otherObject = collision.gameObject.GetComponent<FractureObject>();
        if (otherObject)
        {
            impulse = (mass + otherObject.mass) * collision.relativeVelocity;
        }
        else if (collision.rigidbody && GetComponent<Rigidbody>())
        {
            impulse = (mass + collision.rigidbody.mass) * collision.relativeVelocity;
        }
        else
        {
            impulse = mass * collision.relativeVelocity;
        }

        float force = (impulse * Vector3.Dot(impulse.normalized, normal.normalized) / Time.fixedDeltaTime).magnitude;

        if (force >= jointBreakForce)
        {
            contactPoints = new List<ContactPoint>();
            collision.GetContacts(contactPoints);
            ActivateFracture(impulse);
        }
    }

    void OnFracture(ChunkRuntimeInfo chunk)
    {
        if (onFractureVFX)
            Instantiate(onFractureVFX, chunk.transform.position, Quaternion.identity);

        if (onFractureSound)
            AudioSource.PlayClipAtPoint(onFractureSound, chunk.transform.position);
    }

    void OnJointRemove(Rigidbody rigidbody)
    {
        foreach (ChunkRuntimeInfo chunk in GetComponentsInChildren<ChunkRuntimeInfo>())
            chunk.OnJointRemove(rigidbody);
    }
}
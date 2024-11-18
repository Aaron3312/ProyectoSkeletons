using UnityEngine;

public class CubeController : MonoBehaviour
{
    public int cubeId;
    public bool isCarried = false;
    public bool isTargeted = false;
    public bool isInPlaneB = false; // Track if cube is in the "B" plane

    void Start()
    {
        // Extract ID from the name (Cube_X)
        string[] nameParts = gameObject.name.Split('_');
        if (nameParts.Length > 1 && int.TryParse(nameParts[1], out int id))
        {
            cubeId = id;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("PlaneB"))
        {
            isInPlaneB = true;
            Debug.Log($"Cube {cubeId} entered Plane B");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("PlaneB"))
        {
            isInPlaneB = false;
            Debug.Log($"Cube {cubeId} exited Plane B");
        }
    }
}

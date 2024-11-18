using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class RobotAgent : MonoBehaviour
{
    public int id;
    public bool hasCube;
    public float stoppingDistance = 1.5f;
    public float heightCheckDistance = 0.5f;  // Distancia para verificar altura
    public float jumpForce = 5f;     // Fuerza del salto
    public LayerMask stairsMask;     // Layer para las escaleras
    
    private GameObject carriedCube;
    private GameObject targetedCube;
    private bool isMoving = false;
    private bool isJumping = false;
    private static Dictionary<int, int> cubeTargets = new Dictionary<int, int>();
    private static object lockObject = new object();
    private Rigidbody rb;
    
    // NavMesh components
    private NavMeshAgent navAgent;
    public Animator animator;
    private Transform attachmentPoint;

    void Start()
    {
        // Get required components
        navAgent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        attachmentPoint = transform.Find("AttachmentPoint");
        
        // Configure NavMeshAgent
        if (navAgent != null)
        {
            navAgent.stoppingDistance = stoppingDistance;
            navAgent.speed = 15f;
            navAgent.angularSpeed = 120f;
            navAgent.acceleration = 8f;
            navAgent.baseOffset = 0f;  // Ajusta la altura base del agente
            navAgent.radius = 0.3f;      // Ajusta el radio del agente
            navAgent.autoTraverseOffMeshLink = false;  // Desactivamos para manejar manualmente
        }

        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        // Set initial random position on NavMesh
        Vector3 randomPos = GetRandomNavMeshPosition();
        if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, 10f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
        }
    }

    void Update()
    {
        if (navAgent != null && animator != null)
        {
            float speed = navAgent.velocity.magnitude / navAgent.speed;
            animator.SetFloat("Speed", speed);
            isMoving = speed > 0.1f;
        }

        // Verificar si necesitamos manejar un off-mesh link
        if (navAgent.isOnOffMeshLink)
        {
            StartCoroutine(HandleOffMeshLink());
        }

        // Detectar obstáculos altos
        if (isMoving && !isJumping)
        {
            CheckForHighObstacle();
        }
    }

    void CheckForHighObstacle()
    {
        RaycastHit hit;
        Vector3 rayStart = transform.position + Vector3.up * 0.1f;
        Vector3 rayDirection = navAgent.velocity.normalized;

        Debug.DrawRay(rayStart, rayDirection * heightCheckDistance, Color.red);

        if (Physics.Raycast(rayStart, rayDirection, out hit, heightCheckDistance, stairsMask))
        {
            if (hit.point.y > transform.position.y + 0.3f)
            {
                StartCoroutine(JumpSequence());
            }
        }
    }

    void Jump()
    {
        if (rb != null && !rb.isKinematic)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    IEnumerator JumpSequence()
    {
        if (isJumping) yield break;

        isJumping = true;
        
        // Desactivar temporalmente el NavMeshAgent
        navAgent.enabled = false;
        
        // Aplicar el salto
        if (rb != null)
        {
            rb.AddForce(Vector3.up * jumpForce + navAgent.velocity.normalized * jumpForce/2, ForceMode.Impulse);
        }
        
        // Esperar hasta que el salto termine
        yield return new WaitForSeconds(0.5f);
        
        // Esperar hasta que estemos cerca del suelo
        while (!IsGrounded())
        {
            yield return null;
        }
        
        // Reactivar el NavMeshAgent
        navAgent.enabled = true;
        isJumping = false;
    }

    bool IsGrounded()
    {
        return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.2f);
    }

    IEnumerator HandleOffMeshLink()
    {
        // Desactivar temporalmente el NavMeshAgent
        navAgent.enabled = false;
        
        OffMeshLinkData linkData = navAgent.currentOffMeshLinkData;
        Vector3 startPos = transform.position;
        Vector3 endPos = linkData.endPos;
        
        // Calcular la trayectoria del salto
        float jumpTime = 0.5f;
        float elapsedTime = 0f;
        
        while (elapsedTime < jumpTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / jumpTime;
            
            // Crear una trayectoria parabólica
            Vector3 center = (startPos + endPos) * 0.5f;
            center.y += heightCheckDistance;
            
            Vector3 startRelCenter = startPos - center;
            Vector3 endRelCenter = endPos - center;
            
            transform.position = Vector3.Slerp(startRelCenter, endRelCenter, t) + center;
            yield return null;
        }
        
        // Reactivar el NavMeshAgent
        transform.position = endPos;
        navAgent.enabled = true;
        navAgent.CompleteOffMeshLink();
    }


    public void MoveToCube(GameObject cube)
    {
        if (!hasCube && cube != null)
        {
            var cubeController = cube.GetComponent<CubeController>();
            if (cubeController != null && TryClaimCube(cubeController.cubeId))
            {
                targetedCube = cube;
                StopAllCoroutines();
                StartCoroutine(MoveToTarget(cube.transform.position));
                Debug.Log($"Robot {id} moving to pick up cube {cubeController.cubeId}");
            }
            else
            {
                Explore();
            }
        }
    }

    public void MoveToDeliveryZone(Vector3 deliveryZone)
    {
        if (hasCube)
        {
            StopAllCoroutines();
            Vector3 dropoffPoint = GetRandomPointAroundPosition(deliveryZone, 2f);
            StartCoroutine(MoveToTarget(dropoffPoint));
            Debug.Log($"Robot {id} moving to delivery zone with cube");
        }
    }

    public void Explore()
    {
        Vector3 randomPos = GetRandomNavMeshPosition();
        StopAllCoroutines();
        StartCoroutine(MoveToTarget(randomPos));
    }

    Vector3 GetRandomNavMeshPosition()
    {
        Vector3 randomPos = new Vector3(
            Random.Range(-20f, 20f),
            0,
            Random.Range(-10f, 10f)
        );

        if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, 20f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return transform.position;
    }

    Vector3 GetRandomPointAroundPosition(Vector3 center, float radius)
    {
        Vector3 randomPos = center + Random.insideUnitSphere * radius;
        randomPos.y = center.y;

        if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, radius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return center;
    }

    IEnumerator MoveToTarget(Vector3 target)
    {
        if (navAgent != null)
        {
            navAgent.SetDestination(target);
            
            while (navAgent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                yield return new WaitForSeconds(0.1f);
            }

            // Wait until we're done calculating the path
            while (navAgent.pathStatus == NavMeshPathStatus.PathPartial)
            {
                yield return null;
            }

            // Wait until we reach the destination
            while (navAgent.pathStatus == NavMeshPathStatus.PathComplete &&
                   !navAgent.isStopped &&
                   navAgent.remainingDistance > navAgent.stoppingDistance)
            {
                yield return null;
            }

            // Handle arrival at destination
            if (!hasCube && targetedCube != null)
            {
                if (Vector3.Distance(transform.position, targetedCube.transform.position) < stoppingDistance * 1.2f)
                {
                    yield return StartCoroutine(PickupCubeAnimation(targetedCube));
                }
                else
                {
                    var cubeController = targetedCube.GetComponent<CubeController>();
                    if (cubeController != null)
                    {
                        ReleaseCube(cubeController.cubeId);
                    }
                    targetedCube = null;
                }
            }
            else if (transform.position.x > 10)
            {
                yield return StartCoroutine(DropCubeAnimation());
            }
        }
    }


    IEnumerator PickupCubeAnimation(GameObject cube)
    {
        var cubeController = cube.GetComponent<CubeController>();
        if (cubeController == null || cubeController.isCarried || attachmentPoint == null)
        {
            Debug.Log("Cannot pick up cube: controller is null, cube is carried, or attachment point not found");
            yield break;
        }

        var rigidbody = cube.GetComponent<Rigidbody>();
        if (rigidbody != null)
        {
            rigidbody.isKinematic = true;
        }

        cube.transform.SetParent(attachmentPoint);
        cube.transform.localPosition = Vector3.zero;
        cube.transform.localRotation = Quaternion.identity;
        
        carriedCube = cube;
        cubeController.isCarried = true;
        hasCube = true;
        targetedCube = null;
        
        Debug.Log($"Robot {id} picked up cube {cubeController.cubeId} at attachment point");
    }

    IEnumerator DropCubeAnimation()
    {
        if (carriedCube == null) yield break;

        var cubeController = carriedCube.GetComponent<CubeController>();
        if (cubeController == null) yield break;

        carriedCube.transform.SetParent(null);

        var rigidbody = carriedCube.GetComponent<Rigidbody>();
        if (rigidbody != null)
        {
            rigidbody.isKinematic = false;
        }

        Vector3 dropPosition = new Vector3(transform.position.x, 0.1f, transform.position.z);
        carriedCube.transform.position = dropPosition;

        cubeController.isCarried = false;
        ReleaseCube(cubeController.cubeId);
        Debug.Log($"Robot {id} dropped cube {cubeController.cubeId}");
        carriedCube = null;
        hasCube = false;
    }

    private bool TryClaimCube(int cubeId)
    {
        lock (lockObject)
        {
            if (cubeTargets.TryGetValue(cubeId, out int targetingAgent))
            {
                if (targetingAgent == id)
                    return true;
                return false;
            }

            cubeTargets[cubeId] = id;
            Debug.Log($"Robot {id} claimed cube {cubeId}");
            return true;
        }
    }

    private void ReleaseCube(int cubeId)
    {
        lock (lockObject)
        {
            if (cubeTargets.ContainsKey(cubeId) && cubeTargets[cubeId] == id)
            {
                cubeTargets.Remove(cubeId);
                Debug.Log($"Robot {id} released cube {cubeId}");
            }
        }
    }

    public void PutCube()
    {
        if (hasCube)
        {
            StopAllCoroutines();
            StartCoroutine(DropCubeAnimation());
        }
    }

    GameObject FindNearestCube()
    {
        GameObject nearest = null;
        float minDistance = float.MaxValue;

        foreach (GameObject cube in GameObject.FindGameObjectsWithTag("Cube"))
        {
            var cubeController = cube.GetComponent<CubeController>();
            if (cubeController == null) continue;

            if (cubeController.isCarried || 
                cubeController.isInPlaneB ||
                (cubeTargets.ContainsKey(cubeController.cubeId) && cubeTargets[cubeController.cubeId] != id))
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, cube.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = cube;
            }
        }

        return nearest;
    }
}
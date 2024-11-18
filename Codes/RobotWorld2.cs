using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Text;

public class RobotWorld : MonoBehaviour
{
    public GameObject robotPrefab;
    public GameObject cubePrefab;
    public GameObject planePrefab;
    public GameObject planePrefabFinal;
    private List<RobotAgent> robots = new List<RobotAgent>();
    private List<GameObject> cubes = new List<GameObject>();
    private GameObject planeA, planeB;
    private int nextRobotId = 0; // Add counter for unique IDs

    
    [System.Serializable]
    public class WorldState
    {
        // Change the dictionary to a serializable format
        public List<AgentStateEntry> agentStates = new List<AgentStateEntry>();
    }

    [System.Serializable]
    public class AgentStateEntry
    {
        public string id;
        public AgentState state;
    }

    [System.Serializable]
    public class AgentState
    {
        public Vector3 position;
        public bool has_cube;
        public List<CubeInfo> available_cubes;
    }

    [System.Serializable]
    public class CubeInfo
    {
        public int id;
        public Vector3 position;
        public bool is_carried;
    }

    [System.Serializable]
    public class Decision
    {
        public string decision;
        public CubeInfo target_cube;
        public string target;
    }

    [System.Serializable]
    public class PythonResponse
    {
        public List<Decision> decisions;
    }

    void CreateInitialSetup()
    {
        // Crear planos
        planeA = Instantiate(planePrefab, new Vector3(-15, -1.19f, 0), Quaternion.identity);
        planeB = Instantiate(planePrefabFinal, new Vector3(30, -1.19f, 0), Quaternion.identity);
        planeA.GetComponent<Renderer>().material.color = Color.red;
        planeB.GetComponent<Renderer>().material.color = Color.blue;

        // Crear robots con IDs únicos
        for (int i = 0; i < 5; i++)
        {
            Vector3 position = new Vector3(Random.Range(-5f, 5f), 0, Random.Range(-5f, 5f));
            GameObject robotObj = Instantiate(robotPrefab, position, Quaternion.identity);
            RobotAgent robot = robotObj.AddComponent<RobotAgent>();
            robot.id = nextRobotId++; // Asignar ID único y incrementar
            robot.name = $"Robot_{robot.id}"; // Nombrar el objeto con su ID
            robots.Add(robot);
            Debug.Log($"Created Robot with ID: {robot.id} at position {position}"); // Agregar log para debugging
        }

        // Crear cubos en plano rojo
        for (int i = 0; i < 10; i++)
        {
            Vector3 position = new Vector3(
                Random.Range(-20f, -10f),
                0,
                Random.Range(-10f, 10f)
            );
            GameObject cube = Instantiate(cubePrefab, position, Quaternion.identity);
            cube.name = $"Cube_{i}";
            cubes.Add(cube);
        }

        StartCoroutine(DecisionLoop());
    }

    WorldState GetCurrentWorldState()
    {
        WorldState state = new WorldState();
        
        foreach (RobotAgent robot in robots)
        {
            List<CubeInfo> availableCubes = new List<CubeInfo>();
            foreach (GameObject cube in cubes)
            {
                var cubeController = cube.GetComponent<CubeController>();

                // Only add cubes that are not carried and are outside Plane B
                if (cubeController != null && !cubeController.isCarried && !cubeController.isInPlaneB)
                {
                    availableCubes.Add(new CubeInfo
                    {
                        id = cubeController.cubeId,
                        position = cube.transform.position,
                        is_carried = false
                    });
                }
            }

            state.agentStates.Add(new AgentStateEntry
            {
                id = robot.id.ToString(),
                state = new AgentState
                {
                    position = robot.transform.position,
                    has_cube = robot.hasCube,
                    available_cubes = availableCubes
                }
            });

            // Logging for debugging
            // Debug.Log($"Agent {robot.id} state added to world state. Has cube: {robot.hasCube}");
        }

        // Log to check the complete state
        return state;
    }


     IEnumerator DecisionLoop()
    {
        while (true)
        {
            WorldState currentState = GetCurrentWorldState();
            string jsonState = JsonUtility.ToJson(currentState);
            // Debug.Log($"Sending state: {jsonState}"); // Add logging for debugging

            using (UnityWebRequest www = new UnityWebRequest("http://localhost:5000/get_decisions", "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonState);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    string responseText = www.downloadHandler.text;
                    // Debug.Log($"Received response: {responseText}"); // Add logging for debugging
                    PythonResponse response = JsonUtility.FromJson<PythonResponse>(responseText);
                    ExecuteDecisions(response.decisions);
                }
                else
                {
                    Debug.LogError($"Error: {www.error}");
                }
            }

            yield return new WaitForSeconds(1f);
        }
    }
    void ExecuteDecisions(List<Decision> decisions)
    {
        for (int i = 0; i < decisions.Count; i++)
        {
            Decision decision = decisions[i];
            RobotAgent robot = robots[i];

            switch (decision.decision)
            {
                case "get_cube":
                    if (decision.target_cube != null)
                    {
                        robot.MoveToCube(cubes[decision.target_cube.id]);
                    }
                    break;

                case "deliver_cube":
                    robot.MoveToDeliveryZone(planeB.transform.position);
                    break;
                
                case "put_cube":
                    robot.PutCube();
                    break;

                case "explore":
                    robot.Explore();
                    break;
            }
        }
    }

    void Start()
    {
        CreateInitialSetup();
    }
}


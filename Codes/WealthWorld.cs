using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;


//############################## WealthAgent #############################





//############################ Wealth World ###############################

// The WealthModel World behavior as a WebClient
public class WealthWorld : MonoBehaviour
{

    // Define a list of all Agents (of type Wealth Agent)
    List<WealthAgent> agentList = new List<WealthAgent>();
    // Flag if the simulation is still running
    bool simRunning = true;

    // Setup of the WealthWorld
    // It recieves a Setup Info from the server
    private void modelSetup(WorldInfo worldInfoSetup0)
    {
        // Fills the list of agents with the determined number of agents
        WealthAgent currentAgent;
        for(int i = 0; i < worldInfoSetup0.agents; i++)
        {
            // Creating a new WealthAgent, with its id and wealth
            currentAgent = new WealthAgent(i+1,worldInfoSetup0.wealths);
            // Add the agent to the list
            agentList.Add(currentAgent);
            Debug.Log("Agent "+currentAgent.id+" created.");
        }
    }


    // Step of the WealthWorld
    // It recieves a Step Info from the server
    private void modelStep(WorldInfo worldInfoStep0)
    {
        // It takes the actions returned from the server
        int wealthAgent1 = 0; // Define a 'From whom give a coin'
        int wealthAgent2 = 0; // Define a 'To who give a coin'

        // For every two actions in the actions list
        // (Since actions come in pairs)
        for(int i=0; i<worldInfoStep0.actions.Count; i+=2)
        {
            // Take the id of the agents from each side of the transfer
            wealthAgent1 = worldInfoStep0.actions[i]-1;
            wealthAgent2 = worldInfoStep0.actions[i+1]-1;

            // Give a coin
            agentList[wealthAgent1].wealth -= 1;
            agentList[wealthAgent2].wealth += 1;
        }
    }


    // An utility to create a Python-like list from all wealths
    private string wealthListToString()
    {
        // It creates a python-like list, for easy management
        // as a string.
        // This list collects each agent's wealth
        string str = "[";
        foreach(WealthAgent agent in agentList)
        {
            str += agent.wealth.ToString();
            str += ",";
        }
        str += "]";
        return str;
    }


    // The structure to hold response Info from the server
    public class WorldInfo
    {
        public int agents = 0; // Number of agents
        public int wealths = 0; // Intial wealth
        public List<int> actions; // list of next actions
        public bool running = true; // Still running flag
    }    


//--------------------------------------- Requests -------------------------------------

    // Definition of a GET Request
    IEnumerator GetRequest()
    {
        // Connection to localhost, port 5000
        using (UnityWebRequest www = UnityWebRequest.Get("http://localhost:5000/setup"))
        {
            yield return www.SendWebRequest();

            // If request succeded:
            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Received: " + www.downloadHandler.text); // Debug and see the response
                // Take the JSON response and save it in a WorldInfo structure (see JsonUtility docs)
                WorldInfo worldInfoSetup = JsonUtility.FromJson<WorldInfo>(www.downloadHandler.text.Replace('\'', '\"'));

                Debug.Log("worldInfoSetup: agents = " + worldInfoSetup.agents); // Debug and see the number of agents
                // Call the model setup procedure, using the response (as a WorldInfo structure)
                modelSetup(worldInfoSetup);
            }
            // If request failed:
            else
            {
                Debug.Log("Error: " + www.error);
            }
        } // The 'using' block ensures www.Dispose() is called when this block is exited

    }

    // Definition of a GET Request
    IEnumerator PostRequest()
    {
        // Create a WWWForm
        WWWForm form = new WWWForm();
        // Adding the field of a list of agents' wealth (using wealthListToString, defined above)
        form.AddField("wealthList",wealthListToString());

        // Connection to localhost, port 5000
        using (UnityWebRequest www = UnityWebRequest.Post("http://localhost:5000/step", form))
        {
            // Send the request
            yield return www.SendWebRequest();

            // If request succeded:
            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("POST Success: " + www.downloadHandler.text); // Debug and see the response
                // Take the JSON response and save it in a WorldInfo structure (see JsonUtility docs)
                WorldInfo worldInfoStep = JsonUtility.FromJson<WorldInfo>(www.downloadHandler.text.Replace('\'', '\"'));

                // if the running trigger from the JSON response is True then:
                if (worldInfoStep.running)
                {
                    // Call the model step procedure, using the response (as a WorldInfo structure)
                    modelStep(worldInfoStep);
                }
                // if the running trigger from the JSON response is False then:
                else
                {
                    Debug.Log("Ending simulation on server..."); // Show a closing message
                    simRunning = false; // Change running flag
                }
                

            }
            // If request failed:
            else
            {
                Debug.Log("POST Error: " + www.error);

                Debug.Log("Ending simulation on server..."); // Show a closing message
                simRunning = false;// Change running flag
            }
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Start the GET request as a 'asynchronous' routine
        StartCoroutine(GetRequest());
        
    }
private Coroutine postRequestCoroutine;

void Update()
{
    // Check if simulation is still running
    if (simRunning)
    {
        // Start the POST request as a 'asynchronous' routine
        if (postRequestCoroutine == null)
        {
            postRequestCoroutine = StartCoroutine(PostRequestCoroutine());
        }
    }
    else
    {
        // Stop the coroutine if the simulation is not running
        if (postRequestCoroutine != null)
        {
            StopCoroutine(postRequestCoroutine);
            postRequestCoroutine = null;
        }
    }
}

private IEnumerator PostRequestCoroutine()
{
    yield return PostRequest();

    // Wait for the next frame based on the desired simulation FPS
    float waitTime = 1f / 30f;
    yield return new WaitForSeconds(waitTime);

    // Start the next POST request
    postRequestCoroutine = StartCoroutine(PostRequestCoroutine());
}
}

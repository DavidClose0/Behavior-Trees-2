using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic; // Required for List
using UnityEngine.SceneManagement;

public class Agent : MonoBehaviour
{
    private NavMeshAgent navMeshAgent;
    public Transform doorPosition; // Target near the door
    public Transform roomPosition; // Target inside the room
    public Door doorScript;
    public Transform grenadeTransform; // Assign the grenade object in the scene
    public GameObject grenadePrefab; // Assign the grenade prefab
    public GameObject explosionPrefab; // Assign the explosion effect prefab
    public float grenadeMuzzleVelocity = 15f; // Speed for grenade throw

    private Task rootTask;
    private Coroutine behaviorCoroutine = null; // Keep track of the running coroutine
    private FiringSolution firingSolution;

    void Start()
    {
        this.navMeshAgent = GetComponent<NavMeshAgent>();
        firingSolution = new FiringSolution(); // Instantiate Firing Solution helper

        if (navMeshAgent == null || doorPosition == null || roomPosition == null || doorScript == null || grenadeTransform == null || grenadePrefab == null || explosionPrefab == null)
        {
            Debug.LogError("Please assign ALL required components/prefabs in the Inspector: NavMeshAgent, Door Position, Room Position, Door Script, Grenade Transform, Grenade Prefab, Explosion Prefab!");
            this.enabled = false; // Disable script if setup is incomplete
            return;
        }

        BuildBehaviorTree();
    }

    void BuildBehaviorTree()
    {
        // Using Lists for easier construction
        var openDoorSequence = new Sequence("Handle Open Door", new List<Task> {
            new IsDoorOpen(doorScript),
            new MoveIntoRoom(navMeshAgent, roomPosition)
        });

        var lockedDoorActions = new Sequence("Locked Door Actions", new List<Task> {
            new GetGrenade(navMeshAgent, grenadeTransform), // Go get grenade
            new ThrowGrenade(navMeshAgent, transform, doorPosition, doorScript, grenadePrefab, explosionPrefab, firingSolution, grenadeMuzzleVelocity),
            new Wait(4.0f), // Wait for explosion/settle
            new MoveIntoRoom(navMeshAgent, roomPosition) // Move in
        });

        var lockedDoorSequence = new Sequence("Handle Locked Door", new List<Task> {
            new IsDoorLocked(doorScript), // Condition
            lockedDoorActions           // Actions
        });


        var closedDoorActions = new Sequence("Closed Door Actions", new List<Task> {
            new MoveTo(navMeshAgent, doorPosition), // Move to door
            new Wait(1.0f), // Wait briefly
            new OpenDoorTask(doorScript), // Open it
            new Wait(1.0f), // Wait briefly
            new MoveIntoRoom(navMeshAgent, roomPosition) // Move in
        });

        // This sequence doesn't need an explicit check if it's the last fallback
        var closedDoorSequence = new Sequence("Handle Closed Door", new List<Task> {
             // No initial condition needed if it's the default/last case
             closedDoorActions
        });

        // Alternative for closed door: Explicitly check it's closed AND not locked
        /*
        var closedDoorSequenceExplicit = new Sequence("Handle Closed Door Explicit", new List<Task> {
            new Inverter(new IsDoorOpen(doorScript)), // NOT Open
            new Inverter(new IsDoorLocked(doorScript)), // NOT Locked
            closedDoorActions
        });
        */


        rootTask = new Selector("Root Selector", new List<Task> {
            openDoorSequence,
            lockedDoorSequence,
            closedDoorSequence // Use the one without explicit checks if it's the last resort
            // closedDoorSequenceExplicit // Use this if you prefer explicit checks
        });
    }


    void Update()
    {
        // Start behavior on Space press if not already running
        if (Input.GetKeyDown(KeyCode.Space) && behaviorCoroutine == null)
        {
            Debug.Log("Space pressed - Starting Behavior Tree");
            doorScript.AllowUserInput(false); // Disable manual door control
            // Reset the tree state before starting a new execution
            if (rootTask != null)
            {
                Debug.Log("Resetting Behavior Tree state.");
                rootTask.Reset(); // Ensure tree starts fresh
            }
            // Ensure agent is ready to move
            if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.isStopped = false;
            }
            behaviorCoroutine = StartCoroutine(ExecuteBehaviorTree());
        }

        // Reload scene on R press
        if (Input.GetKeyDown(KeyCode.R))
        {
            // Ensure tree is stopped and agent reset before reload
            StopBehaviorTree();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // Optional: Stop behavior tree (e.g., on Escape key)
        if (Input.GetKeyDown(KeyCode.Escape) && behaviorCoroutine != null)
        {
            StopBehaviorTree();
            Debug.Log("Behavior Tree manually stopped.");
        }
    }

    IEnumerator ExecuteBehaviorTree()
    {
        Debug.Log("Starting behavior tree execution coroutine");
        TaskStatus treeStatus = TaskStatus.RUNNING; // Assume running initially

        while (treeStatus == TaskStatus.RUNNING) // Loop while the tree is running
        {
            if (rootTask == null)
            {
                Debug.LogError("Root task is null, cannot execute.");
                yield break; // Exit coroutine
            }

            // Run one tick of the behavior tree
            // Debug.Log("-- Tick --"); // Optional: Log each tick
            treeStatus = rootTask.Run();

            if (treeStatus == TaskStatus.RUNNING)
            {
                // If still running, wait for the next frame before running the next tick.
                yield return null;
            }
            // No need for else block here, loop condition handles SUCCESS/FAILURE exit
        }

        // Loop finished, tree returned SUCCESS or FAILURE
        if (treeStatus == TaskStatus.SUCCESS)
        {
            Debug.Log("Behavior Tree execution completed: SUCCESS!");
        }
        else // Must be FAILURE
        {
            Debug.LogWarning("Behavior Tree execution completed: FAILURE!");
        }

        Debug.Log("Behavior Tree execution coroutine finished.");
        behaviorCoroutine = null; // Mark as not running
                                  // Optional: Re-enable user input for door
                                  // doorScript.AllowUserInput(true);
    }

    // Public method to stop the behavior tree
    public void StopBehaviorTree()
    {
        if (behaviorCoroutine != null)
        {
            StopCoroutine(behaviorCoroutine);
            behaviorCoroutine = null;
            Debug.Log("Behavior Tree Coroutine Stopped");

            // Reset agent state if necessary (e.g., stop movement)
            if (navMeshAgent != null && navMeshAgent.isOnNavMesh && navMeshAgent.isActiveAndEnabled)
            {
                Debug.Log("Stopping NavMeshAgent.");
                navMeshAgent.isStopped = true; // Stop movement
                navMeshAgent.ResetPath();     // Clear current path
            }
            // Reset the tree state as well
            if (rootTask != null)
            {
                Debug.Log("Resetting Behavior Tree state after stopping.");
                rootTask.Reset();
            }
            // Optional: Re-enable user input for door
            // if(doorScript != null) doorScript.AllowUserInput(true);
        }
    }

    void OnDestroy()
    {
        // Ensure the coroutine is stopped if the agent object is destroyed
        StopBehaviorTree();
    }
}
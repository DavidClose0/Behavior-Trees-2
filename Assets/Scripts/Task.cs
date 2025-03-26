using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System; // Required for Nullable

public enum TaskStatus
{
    SUCCESS,
    FAILURE,
    RUNNING
}

public abstract class Task
{
    public string name = "Task";

    public Task() { }
    public Task(string name)
    {
        this.name = name;
    }

    // Changed return type
    public abstract TaskStatus Run();

    public virtual void Reset() { }
}

public class Sequence : Task
{
    public List<Task> children = new List<Task>();
    private int currentChildIndex = 0; // Must persist across ticks

    public Sequence() : base("Sequence") { }
    public Sequence(string name) : base(name) { }
    public Sequence(string name, List<Task> taskList) : base(name)
    {
        children = taskList;
    }

    // Changed return type and logic
    public override TaskStatus Run()
    {
        if (children.Count == 0)
            return TaskStatus.SUCCESS;

        // Start or continue from the current child index
        while (currentChildIndex < children.Count)
        {
            Task currentChild = children[currentChildIndex];
            TaskStatus childStatus = currentChild.Run();

            switch (childStatus)
            {
                case TaskStatus.FAILURE:
                    // Child failed, sequence fails. Reset and report failure.
                    Reset();
                    return TaskStatus.FAILURE;
                case TaskStatus.RUNNING:
                    // Child is running, sequence is running. Do not increment index.
                    return TaskStatus.RUNNING;
                case TaskStatus.SUCCESS:
                    // Child succeeded, move to the next one.
                    currentChildIndex++;
                    break; // Continue the while loop
            }
        }

        // All children succeeded in sequence. Reset and report success.
        Reset();
        return TaskStatus.SUCCESS;
    }

    public override void Reset()
    {
        currentChildIndex = 0;
        foreach (var child in children)
        {
            child.Reset(); // Reset children
        }
    }
}

public class Selector : Task
{
    public List<Task> children = new List<Task>();
    // Add state for resuming running child (Prioritized Selector)
    private int currentlyRunningChildIndex = -1; // -1 means no child is currently running

    public Selector() : base("Selector") { }
    public Selector(string name) : base(name) { }
    public Selector(string name, List<Task> taskList) : base(name)
    {
        children = taskList;
    }

    // Changed return type and logic (Implementing Prioritized Selector)
    public override TaskStatus Run()
    {
        if (children.Count == 0)
            return TaskStatus.FAILURE;

        // If a child was running previously, start from it
        int startIndex = (currentlyRunningChildIndex != -1) ? currentlyRunningChildIndex : 0;

        for (int i = startIndex; i < children.Count; i++)
        {
            Task currentChild = children[i];
            TaskStatus childStatus = currentChild.Run();

            switch (childStatus)
            {
                case TaskStatus.SUCCESS:
                    // Child succeeded, Selector succeeds. Reset state and report success.
                    ResetRunningChild();
                    return TaskStatus.SUCCESS;
                case TaskStatus.RUNNING:
                    // Child is running, Selector is running. Remember which child and report running.
                    currentlyRunningChildIndex = i;
                    return TaskStatus.RUNNING;
                case TaskStatus.FAILURE:
                    // Child failed, continue to the next child.
                    // Ensure the previously running child's state is reset if it just failed.
                    if (i == currentlyRunningChildIndex)
                    {
                        currentlyRunningChildIndex = -1; // No longer running
                                                         // Optional: currentChild.Reset(); // Reset the child that just failed
                    }
                    continue; // Go to the next iteration
            }
        }

        // All children failed, Selector fails. Reset state and report failure.
        ResetRunningChild();
        return TaskStatus.FAILURE;
    }

    private void ResetRunningChild()
    {
        if (currentlyRunningChildIndex != -1)
        {
            // Optional: Explicitly reset the child that was running if needed
            // children[currentlyRunningChildIndex].Reset();
            currentlyRunningChildIndex = -1;
        }
    }

    public override void Reset()
    {
        ResetRunningChild();
        // Reset all children
        foreach (var child in children)
        {
            child.Reset();
        }
    }
}


public class Inverter : Task
{
    public Task child;

    public Inverter(Task child) : base("Inverter")
    {
        this.child = child;
    }

    // Changed return type and logic
    public override TaskStatus Run()
    {
        if (child == null) return TaskStatus.FAILURE;

        TaskStatus childStatus = child.Run();
        switch (childStatus)
        {
            case TaskStatus.SUCCESS:
                return TaskStatus.FAILURE; // Invert SUCCESS to FAILURE
            case TaskStatus.FAILURE:
                return TaskStatus.SUCCESS; // Invert FAILURE to SUCCESS
            case TaskStatus.RUNNING:
                return TaskStatus.RUNNING; // RUNNING remains RUNNING
            default:
                return TaskStatus.FAILURE; // Should not happen
        }
    }

    public override void Reset()
    {
        if (child != null) child.Reset();
    }
}


public class OpenDoorTask : Task
{
    private Door door;

    public OpenDoorTask(Door door) : base("Open Door Task")
    {
        this.door = door;
    }

    // Changed return type
    public override TaskStatus Run()
    {
        if (door == null)
        {
            Debug.LogError("OpenDoorTask: Door reference is null.");
            return TaskStatus.FAILURE;
        }

        // Only run once effectively, but check state first
        if (!door.IsOpen())
        {
            if (door.IsLocked())
            {
                Debug.Log("OpenDoorTask: Cannot open, door is locked.");
                return TaskStatus.FAILURE; // Cannot open a locked door this way
            }
            door.OpenDoor();
            Debug.Log("Opening door via Task.");
        }
        else
        {
            Debug.Log("Door already open, OpenDoorTask succeeds immediately.");
        }
        return TaskStatus.SUCCESS; // Action is considered successful (instantaneous or already done)
    }
}

public class IsDoorOpen : Task
{
    private Door door;

    public IsDoorOpen(Door door) : base("Is Door Open")
    {
        this.door = door;
    }

    // Changed return type
    public override TaskStatus Run()
    {
        if (door == null) return TaskStatus.FAILURE;
        bool isDoorOpen = door.IsOpen();
        // Debug.Log($"IsDoorOpen Check: {isDoorOpen}"); // Optional debug
        return isDoorOpen ? TaskStatus.SUCCESS : TaskStatus.FAILURE;
    }
}

public class IsDoorLocked : Task
{
    private Door door;

    public IsDoorLocked(Door door) : base("Is Door Locked")
    {
        this.door = door;
    }

    // Changed return type
    public override TaskStatus Run()
    {
        if (door == null) return TaskStatus.FAILURE;
        bool isDoorLocked = door.IsLocked();
        // Debug.Log($"IsDoorLocked Check: {isDoorLocked}"); // Optional debug
        return isDoorLocked ? TaskStatus.SUCCESS : TaskStatus.FAILURE;
    }
}

public class MoveTo : Task
{
    protected NavMeshAgent agent;
    protected Transform targetPosition;
    protected bool hasStartedMoving = false;
    protected bool pathInvalid = false; // Flag for path issues

    public MoveTo(NavMeshAgent agent, Transform targetPosition) : base("Move To")
    {
        this.agent = agent;
        this.targetPosition = targetPosition;
    }

    public override TaskStatus Run()
    {
        // If we already determined the path is invalid, fail immediately
        if (pathInvalid)
        {
            return TaskStatus.FAILURE;
        }

        // --- Basic Validity Checks ---
        if (agent == null || targetPosition == null) { /* ... Error handling ... */ return TaskStatus.FAILURE; }
        if (!agent.isActiveAndEnabled) { /* ... Error handling ... */ return TaskStatus.FAILURE; }
        if (!agent.isOnNavMesh) { /* ... Error handling ... */ return TaskStatus.FAILURE; }

        // --- Check if Already at Destination ---
        float distanceToTargetSqr = (agent.transform.position - targetPosition.position).sqrMagnitude;
        // Check if close AND agent has either no path or has stopped (low velocity)
        if (distanceToTargetSqr <= agent.stoppingDistance * agent.stoppingDistance && !agent.pathPending)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude < 0.1f * 0.1f)
            {
                // Debug.Log($"MoveTo ({name}): Already at/near destination {targetPosition.position}. SUCCESS.");
                ResetStateFlags();

                // --- ADDED LINE: Explicitly stop the agent ---
                if (!agent.isStopped)
                {
                    // Debug.Log($"MoveTo ({name}): Setting agent.isStopped = true on success (already near).");
                    agent.isStopped = true;
                }
                // --- END ADDED LINE ---

                return TaskStatus.SUCCESS;
            }
        }


        // --- Start Moving or Recalculate Path ---
        if (!hasStartedMoving)
        {
            // Ensure agent control is enabled
            if (agent.isStopped) { agent.isStopped = false; }
            if (!agent.updateRotation) { agent.updateRotation = true; }

            // Attempt to set the destination
            if (agent.SetDestination(targetPosition.position))
            {
                hasStartedMoving = true;
                return TaskStatus.RUNNING;
            }
            else
            {
                // ... (Error handling for SetDestination failure) ...
                pathInvalid = true;
                ResetStateFlags();
                if (!agent.isStopped) agent.isStopped = true; // Stop on failure too
                return TaskStatus.FAILURE;
            }
        }

        // --- Check Path Status and Arrival (if already moving) ---
        if (agent.pathPending) { return TaskStatus.RUNNING; }
        else
        {
            // Check for invalid or partial paths
            if (agent.pathStatus == NavMeshPathStatus.PathInvalid || agent.pathStatus == NavMeshPathStatus.PathPartial)
            {
                // ... (Error handling for invalid/partial path) ...
                pathInvalid = true;
                ResetStateFlags();
                agent.ResetPath();
                if (!agent.isStopped) agent.isStopped = true; // Stop on failure
                return TaskStatus.FAILURE;
            }

            // Path is valid, check if we've reached the destination
            if (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance)
            {
                if (agent.velocity.sqrMagnitude < 0.1f * 0.1f)
                {
                    // Debug.Log($"MoveTo ({name}): Reached destination. Remaining distance: {agent.remainingDistance}. SUCCESS.");
                    ResetStateFlags();

                    // --- ADDED LINE: Explicitly stop the agent ---
                    if (!agent.isStopped)
                    {
                        // Debug.Log($"MoveTo ({name}): Setting agent.isStopped = true on success (arrival).");
                        agent.isStopped = true;
                    }
                    // --- END ADDED LINE ---

                    return TaskStatus.SUCCESS;
                }
                // Else: Close but still adjusting - keep RUNNING
            }
        }

        // If none of the above conditions caused SUCCESS or FAILURE, we are still moving.
        return TaskStatus.RUNNING;
    }

    // Helper to reset specific state flags related to a single run attempt
    protected void ResetStateFlags()
    {
        hasStartedMoving = false;
        // pathInvalid is reset only by the main Reset() method
    }

    // Full Reset for the task (e.g., when sequence restarts or BT resets)
    public override void Reset()
    {
        // Debug.Log($"MoveTo ({name}): Reset called.");
        hasStartedMoving = false;
        pathInvalid = false; // Reset path validity on full task reset

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            // Stop the agent and clear its path if it was moving or stopped by this task
            if (agent.hasPath || agent.isStopped)
            {
                // Debug.Log($"MoveTo ({name}): Resetting NavMeshAgent path and ensuring stopped state.");
                agent.isStopped = true; // Stop movement immediately
                agent.ResetPath();     // Clear current path data
            }

            // Ensure NavMeshAgent rotation control is re-enabled after reset
            if (!agent.updateRotation)
            {
                // Debug.Log($"MoveTo ({name}) Reset: Re-enabling NavMeshAgent.updateRotation.");
                agent.updateRotation = true;
            }

            // Prepare agent for potential immediate next move command by ensuring isStopped is false
            // (Run() method will handle setting destination)
            // Debug.Log($"MoveTo ({name}) Reset: Ensuring NavMeshAgent.isStopped = false for next run.");
            agent.isStopped = false;
        }
    }
}

public class GetGrenade : MoveTo
{
    private bool grenadeCollected = false;
    private GameObject grenadeObjectToCollect; // Store the GameObject reference

    public GetGrenade(NavMeshAgent agent, Transform grenadePosition)
        : base(agent, grenadePosition) // grenadePosition is stored in base.targetPosition
    {
        name = "Get Grenade";
        if (grenadePosition != null)
        {
            this.grenadeObjectToCollect = grenadePosition.gameObject; // Get the GameObject
        }
    }

    public override TaskStatus Run()
    {
        // Prevent running if already collected or target is gone
        if (grenadeCollected || grenadeObjectToCollect == null)
        {
            // If it was collected, this task is done.
            // If the object is null (destroyed elsewhere?), this task fails.
            return grenadeCollected ? TaskStatus.SUCCESS : TaskStatus.FAILURE;
        }

        // Run the base MoveTo logic
        TaskStatus moveResult = base.Run();

        // If movement succeeded, collect (destroy) the grenade
        if (moveResult == TaskStatus.SUCCESS)
        {
            if (!grenadeCollected && grenadeObjectToCollect != null)
            {
                Debug.Log($"Agent reached grenade location ({grenadeObjectToCollect.name}). Collecting (Destroying)...");
                GameObject.Destroy(grenadeObjectToCollect);
                grenadeCollected = true;
                grenadeObjectToCollect = null; // Clear reference
                // base.targetPosition will now also be effectively null if checked, handle this if MoveTo is called again.
            }
            // Return SUCCESS because we reached the location and collected
            return TaskStatus.SUCCESS;
        }
        else if (moveResult == TaskStatus.FAILURE)
        {
            Debug.LogError("Agent failed to reach grenade location.");
        }
        // else it's RUNNING

        return moveResult; // Return RUNNING or FAILURE from MoveTo
    }

    public override void Reset()
    {
        base.Reset();
        grenadeCollected = false;
        // We cannot "un-destroy" the grenade, so if the tree resets
        // and tries to run GetGrenade again, it should fail fast if grenadeObjectToCollect is null.
        // Re-finding the reference might be needed in complex scenarios,
        // but here we assume it's gone for good once collected.
        // Let's re-acquire the reference from the base class IF it still exists (unlikely if destroyed)
        if (base.targetPosition != null)
        {
            grenadeObjectToCollect = base.targetPosition.gameObject;
        }
        else
        {
            grenadeObjectToCollect = null;
        }
    }
}

public class MoveIntoRoom : MoveTo
{
    public MoveIntoRoom(NavMeshAgent agent, Transform roomPosition)
        : base(agent, roomPosition)
    {
        name = "Move Into Room";
    }
    // Optionally override Run if you need specific logging on SUCCESS/FAILURE
    public override TaskStatus Run()
    {
        TaskStatus result = base.Run();
        if (result == TaskStatus.SUCCESS)
        {
            Debug.Log("Agent moved into room.");
        }
        else if (result == TaskStatus.FAILURE)
        {
            Debug.LogError("Agent failed to move into room.");
        }
        return result;
    }
}


public class Wait : Task
{
    private float waitDuration;
    private float startTime = -1f; // Use -1 to indicate not started

    public Wait(float duration) : base("Wait")
    {
        this.waitDuration = duration;
    }

    // Changed return type
    public override TaskStatus Run()
    {
        // Start timer on first run
        if (startTime < 0)
        {
            startTime = Time.time;
            Debug.Log($"Wait: Starting wait for {waitDuration} seconds.");
            return TaskStatus.RUNNING; // Timer just started
        }

        // Check if duration has passed
        if (Time.time >= startTime + waitDuration)
        {
            Debug.Log($"Wait: Wait finished ({waitDuration} seconds).");
            Reset(); // Reset timer for potential reuse
            return TaskStatus.SUCCESS; // Wait completed
        }
        else
        {
            // Still waiting
            return TaskStatus.RUNNING;
        }
    }

    public override void Reset()
    {
        startTime = -1f; // Reset start time
    }
}

public class ThrowGrenade : Task
{
    // Add NavMeshAgent reference
    private NavMeshAgent agent;
    private Transform agentTransform;
    private Transform targetDoorPosition;
    private Door doorScript;
    private GameObject grenadePrefab;
    private GameObject explosionPrefab;
    private FiringSolution firingSolution;
    private float muzzleVelocity;
    private bool grenadeThrown = false;

    public float rotationSpeed = 180f;
    public float facingThreshold = 5.0f;

    // Update constructor to accept NavMeshAgent
    public ThrowGrenade(NavMeshAgent agent, Transform agentTransform, Transform doorTarget, Door door, GameObject grenadePrefab, GameObject explosionPrefab, FiringSolution fs, float muzzleV)
        : base("Throw Grenade")
    {
        this.agent = agent; // Store agent reference
        this.agentTransform = agentTransform;
        this.targetDoorPosition = doorTarget;
        this.doorScript = door;
        this.grenadePrefab = grenadePrefab;
        this.explosionPrefab = explosionPrefab;
        this.firingSolution = fs;
        this.muzzleVelocity = muzzleV;
    }

    public override TaskStatus Run()
    {
        if (grenadeThrown) return TaskStatus.SUCCESS;

        // Check agent reference as well
        if (agent == null || agentTransform == null || targetDoorPosition == null || doorScript == null || grenadePrefab == null || firingSolution == null)
        {
            Debug.LogError("ThrowGrenade: Missing references (including NavMeshAgent)!");
            return TaskStatus.FAILURE;
        }

        // --- Stop NavMeshAgent control ---
        // Stop movement AND rotation updates before manual control
        if (!agent.isStopped)
        {
            // Debug.Log("ThrowGrenade: Stopping NavMeshAgent.");
            agent.isStopped = true; // Stop movement processing
        }
        // Explicitly disable NavMeshAgent's rotation control
        // Store original value to restore later if needed, though MoveTo will likely re-enable it.
        // bool originalUpdateRotation = agent.updateRotation; // Optional: Store if you need finer control
        if (agent.updateRotation)
        {
            // Debug.Log("ThrowGrenade: Disabling NavMeshAgent.updateRotation.");
            agent.updateRotation = false;
        }


        // --- Rotation Logic ---
        Vector3 targetDirection = targetDoorPosition.position - agentTransform.position;
        targetDirection.y = 0;

        if (targetDirection.sqrMagnitude < 0.01f)
        {
            Debug.Log("ThrowGrenade: Agent too close to target. Proceeding.");
        }
        else
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            float angleDifference = Quaternion.Angle(agentTransform.rotation, targetRotation);

            if (angleDifference > facingThreshold)
            {
                agentTransform.rotation = Quaternion.RotateTowards(
                    agentTransform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
                // Still rotating, ensure agent remains stopped and doesn't update rotation
                return TaskStatus.RUNNING;
            }
            // Else: Facing target
        }

        // --- Throwing Logic ---
        // (Calculation and instantiation code remains the same)
        Vector3 startPos = agentTransform.position + agentTransform.forward * 0.5f + Vector3.up * 1.0f;
        Vector3 targetPos = targetDoorPosition.position;
        Nullable<Vector3> launchDir = firingSolution.Calculate(startPos, targetPos, muzzleVelocity, Physics.gravity);

        if (launchDir.HasValue)
        {
            // ... (Instantiate and setup grenade) ...
            Debug.Log("ThrowGrenade: Firing solution found. Throwing grenade.");
            GameObject grenadeInstance = GameObject.Instantiate(grenadePrefab, startPos, Quaternion.identity);
            Rigidbody rb = grenadeInstance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = launchDir.Value * muzzleVelocity;
                Grenade grenadeLogic = grenadeInstance.GetComponent<Grenade>();
                if (grenadeLogic != null)
                {
                    grenadeLogic.doorScript = this.doorScript;
                    grenadeLogic.explosionPrefab = this.explosionPrefab;
                }
                else { Debug.LogWarning("ThrowGrenade: Grenade prefab missing Grenade script."); }
            }
            else
            {
                Debug.LogError("ThrowGrenade: Grenade prefab missing Rigidbody!");
                if (grenadeInstance != null) GameObject.Destroy(grenadeInstance);
                // Before returning FAILURE, restore agent state if needed? Or let Reset handle it?
                // For now, failure means failure.
                return TaskStatus.FAILURE;
            }

        }
        else
        {
            Debug.LogWarning("ThrowGrenade: No firing solution found for target!");
            // Before returning FAILURE, restore agent state if needed?
            return TaskStatus.FAILURE;
        }

        grenadeThrown = true;
        Debug.Log("ThrowGrenade Task SUCCESS.");

        // IMPORTANT: Leave the agent stopped (isStopped = true) and with updateRotation = false.
        // The *next* MoveTo task (`MoveIntoRoom`) should be responsible for
        // setting agent.isStopped = false and agent.updateRotation = true (if needed)
        // when it starts executing its movement.

        return TaskStatus.SUCCESS;
    }

    public override void Reset()
    {
        grenadeThrown = false;
        // When the task resets, ensure the NavMeshAgent is put back into a usable state
        // in case the sequence was interrupted mid-rotation.
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            // Restore NavMeshAgent control if it was disabled
            if (!agent.updateRotation)
            {
                // Debug.Log("ThrowGrenade Reset: Re-enabling NavMeshAgent.updateRotation.");
                agent.updateRotation = true;
            }
            // Ensure the agent is not left stopped indefinitely *unless*
            // the overall BT logic requires it. Usually, Reset means ready for next run.
            if (agent.isStopped)
            {
                // Debug.Log("ThrowGrenade Reset: Setting NavMeshAgent.isStopped = false.");
                // agent.isStopped = false; // MoveTo will handle this when it runs
            }
        }
    }
}
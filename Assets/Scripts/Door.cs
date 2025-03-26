using UnityEngine;
using TMPro;
using System.Collections; // Keep this if needed elsewhere

public class Door : MonoBehaviour
{
    private bool isOpen;
    private bool isLocked;
    private Quaternion closedRotation; // Store the initial/closed rotation
    public float openAngle = 90f;      // Angle to rotate when opening
    public TextMeshProUGUI doorText;
    private bool allowUserInput = true;
    // public float bargeForce = 10f; // No longer used by agent

    private Rigidbody rb; // Reference to the Rigidbody
    private Transform doorVisual; // Reference to the door visual transform (e.g., the mesh part)

    private void Start()
    {
        isOpen = false;
        isLocked = false;
        closedRotation = transform.rotation; // Assumes the door starts closed

        // Find the Rigidbody on this GameObject or a child (adjust if needed)
        rb = GetComponentInChildren<Rigidbody>(); // More robust: searches children too
        if (rb == null)
        {
            Debug.LogError("Door: Rigidbody component not found on the door or its children. Physics interactions (like grenade) will fail.", this.gameObject);
        }
        else
        {
            // Recommended: Set constraints on the Rigidbody in the Inspector
            // e.g., Freeze Position X, Y, Z; Freeze Rotation X, Z
            // This makes it act like a hinged door.
        }


        // Find the door visual, assuming it's the first child or the object itself if no children
        if (transform.childCount > 0)
        {
            // A common setup is an empty parent at the hinge point, and the visual mesh as a child.
            doorVisual = transform.GetChild(0);
        }
        else
        {
            // If the script/rigidbody is directly on the visual mesh
            doorVisual = transform;
            // Debug.LogWarning("Door: No child object found. Assuming script is on the visual mesh itself.", this.gameObject);
        }

        UpdateText();
    }

    public bool IsOpen()
    {
        // Consider adding a check based on angle if physics are involved
        // For now, rely on the flag set by OpenDoor/ApplyGrenadeForce
        return isOpen;
    }

    public bool IsLocked()
    {
        return isLocked;
    }

    public void SetLocked(bool locked)
    {
        isLocked = locked;
        if (isLocked) // Ensure door is closed if locked
        {
            isOpen = false;
            SetRotation(false); // Snap to closed rotation
        }
        UpdateText();
    }

    public void SetOpen(bool open) // Less used now, OpenDoor preferred
    {
        if (isLocked && open) return; // Cannot open if locked
        isOpen = open;
        SetRotation(isOpen);
        UpdateText();
    }

    public void AllowUserInput(bool allow)
    {
        allowUserInput = allow;
    }

    void Update()
    {
        if (allowUserInput)
        {
            // Keyboard input for door control (for testing)
            if (Input.GetKeyDown(KeyCode.O)) // Open
            {
                if (!isLocked)
                {
                    isOpen = true;
                    SetRotation(true);
                    UpdateText();
                }
            }
            if (Input.GetKeyDown(KeyCode.C)) // Close
            {
                isOpen = false;
                isLocked = false; // Closing typically unlocks unless explicitly locked after
                SetRotation(false);
                UpdateText();
            }
            if (Input.GetKeyDown(KeyCode.L)) // Lock (implies closing)
            {
                isOpen = false;
                isLocked = true;
                SetRotation(false);
                UpdateText();
            }
        }

        // Optional: Update isOpen based on rotation if physics are active
        // float currentAngle = Quaternion.Angle(closedRotation, transform.rotation);
        // if (currentAngle > openAngle * 0.9f) isOpen = true;
        // else if (currentAngle < 5f) isOpen = false; // Hysteresis
    }

    // Called by OpenDoorTask
    public void OpenDoor()
    {
        if (isLocked)
        {
            Debug.Log("Door: Cannot open, it's locked.");
            return;
        }
        if (isOpen) return; // Already open

        isOpen = true;
        isLocked = false; // Opening unlocks
        SetRotation(true); // Use kinematic rotation for standard open
        UpdateText();
        Debug.Log("Door: Opened.");
    }

    // Called by the Grenade script
    public void ApplyGrenadeForce(Vector3 explosionPosition, float forceAmount)
    {
        isLocked = false; // Grenade breaks the lock
        isOpen = true;   // Grenade forces it open
        UpdateText();

        if (rb != null && doorVisual != null)
        {
            Vector3 doorTargetPoint = rb.worldCenterOfMass;
            Vector3 forceDirection = (doorTargetPoint - explosionPosition).normalized;

            rb.isKinematic = false; // Ensure physics is enabled
            rb.AddForceAtPosition(forceDirection * forceAmount, doorTargetPoint, ForceMode.Impulse);

            // We don't call SetRotation here - let physics take over for the explosion effect.
            // The isOpen flag is set mainly for the BT logic check.
            Debug.Log("Door: Applied grenade force and set isOpen = true.");
        }
        else
        {
            Debug.LogWarning("Door: Cannot apply grenade force. Rigidbody or Door Visual missing/not assigned.", this.gameObject);
            // Fallback: If no Rigidbody, maybe just set rotation directly?
            // SetRotation(true);
        }
    }


    // Use this for non-physics rotation changes (manual open/close)
    private void SetRotation(bool openTargetState)
    {
        if (rb != null)
        {
            // If using physics, make kinematic before manually setting rotation
            // However, for explosions we want physics, so only make kinematic for OpenDoor/SetLocked
            // rb.isKinematic = true; // Be careful with this if combining physics and kinematic changes
        }

        if (openTargetState)
        {
            // Rotate to open position (around parent's local Y-axis)
            transform.rotation = closedRotation * Quaternion.Euler(0, openAngle, 0);
        }
        else
        {
            // Rotate back to closed position
            transform.rotation = closedRotation;
        }
    }

    private void UpdateText()
    {
        if (doorText == null) return;

        if (isOpen)
        {
            doorText.text = "The door is open.";
        }
        else if (!isLocked)
        {
            doorText.text = "The door is closed.";
        }
        else
        {
            doorText.text = "The door is locked.";
        }
    }
}
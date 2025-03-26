using UnityEngine;
using System.Collections;

public class Grenade : MonoBehaviour
{
    public Door doorScript;
    public GameObject explosionPrefab;
    public float explosionForce = 15f;
    public float explosionEffectDuration = 1.0f;
    public float delayBeforeCollisionCheck = 0.1f;

    private Rigidbody rb;
    private bool collisionCheckEnabled = false;
    private float timeThrown;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        timeThrown = Time.time;
        // Disable collision check initially
        StartCoroutine(EnableCollisionCheck());
    }

    IEnumerator EnableCollisionCheck()
    {
        yield return new WaitForSeconds(delayBeforeCollisionCheck);
        collisionCheckEnabled = true;
    }

    void OnCollisionEnter(Collision collision)
    {
        // Only explode if collision check is enabled and we haven't already exploded
        if (collisionCheckEnabled && doorScript != null)
        {
            Debug.Log("Grenade collided with: " + collision.gameObject.name);
            Explode();
        }
    }

    void Explode()
    {
        if (doorScript != null)
        {
            Debug.Log("Grenade applying force to door.");
            doorScript.ApplyGrenadeForce(transform.position, explosionForce);
        }

        if (explosionPrefab != null)
        {
            GameObject explosionInstance = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            // Destroy explosion after duration
            Destroy(explosionInstance, explosionEffectDuration);
        }

        // Prevent further explosions
        doorScript = null;

        // Destroy the grenade itself
        Destroy(gameObject);
    }
}
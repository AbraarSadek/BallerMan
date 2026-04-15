using UnityEngine;

/// <summary>
/// Attach to the Hand child GameObject.
/// Forwards ball collision events up to the BlockAgent on the root Rig,
/// since Unity's OnCollisionEnter fires on the collider's own GameObject,
/// not on parent GameObjects.
/// </summary>
public class HandCollider : MonoBehaviour
{
    public BlockAgent agent;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ball"))
            agent.OnHandCollision();
    }
}

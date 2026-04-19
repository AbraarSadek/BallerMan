using UnityEngine;


public class HandCollider : MonoBehaviour
{
    public BlockAgent agent;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ball"))
            agent.OnHandCollision();
    }
}

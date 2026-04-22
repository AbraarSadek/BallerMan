using UnityEngine;

public class GoalTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Player ball scores and gets destroyed
        if (other.CompareTag("Playerball"))
        {
            ScoreManager.Instance.AddPlayerPoint();
            Destroy(other.gameObject);
        }

        // AI ball scores and teleports to (0,0,0)
        else if (other.CompareTag("ball"))
        {
            ScoreManager.Instance.AddAgentPoint();

            // Reset movement
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Teleport ball
            other.transform.position = Vector3.zero;
        }
    }
}
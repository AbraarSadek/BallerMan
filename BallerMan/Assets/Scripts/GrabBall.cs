using UnityEngine;

public class GrabBall : MonoBehaviour
{
    public GameObject ball;
    private Rigidbody ballRigidbody;
    public float grabDistance = 0.5f;
    private GameObject hand;
    public bool grabbed {get; private set;}
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (!ball)
        {
            Debug.LogError("Ball not assigned in GrabBall script.");
            return;
        }
        ballRigidbody = ball.GetComponent<Rigidbody>();
        hand = gameObject;
        
    }

    // Update is called once per frame
    void Update()
    {
        if (ball && grabbed)
        {
            ball.transform.position = hand.transform.position;
            ballRigidbody.linearVelocity = Vector3.zero;
        }
    }

    public bool Grab()
    {
        if (ball.transform.position.magnitude - hand.transform.position.magnitude < grabDistance)
        {
            ball.transform.SetParent(hand.transform);
            grabbed = true;
            return true;
        }

        return false;
    }

    public void Release(Vector3 throwVelocity)
    {
        ball.transform.SetParent(null);
        ballRigidbody.linearVelocity = throwVelocity;
        grabbed = false;
    }

    public void Release()
    {
        Release(Vector3.zero);
    }
}

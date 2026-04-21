using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class Ball : MonoBehaviour
{
    public float minImpactVelocity = 0.5f;
    private Rigidbody rb;
    private XRGrabInteractable grabInteractable;
    public float throwBoost = 3.5f;
    public bool inHand = false;
    public bool computerBall = false;


    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<XRGrabInteractable>();
    }


    void OnEnable()
    {
        if (grabInteractable == null) return;
        grabInteractable.selectEntered.AddListener(OnHold);
        grabInteractable.selectExited.AddListener(OnRelease);
    }

    void OnDisable()
    {
        if (grabInteractable == null) return;
        grabInteractable.selectEntered.RemoveListener(OnHold);
        grabInteractable.selectExited.RemoveListener(OnRelease);
    }

    void OnHold(SelectEnterEventArgs args)
    {
        inHand = true;
    }
    void OnRelease(SelectExitEventArgs args)
    {
        rb.linearVelocity *= throwBoost;
        inHand = false;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (inHand)
        {
            return;
        }


        if (rb.linearVelocity.magnitude >= minImpactVelocity)
        {
            ContactPoint contact = collision.contacts[0];

        }
    }
}

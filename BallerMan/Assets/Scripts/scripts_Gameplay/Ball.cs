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

    [Header("Out of Bounds Reset")]
    public Transform spawnPoint;
    public float outOfBoundsY = -5f;
    public float outOfBoundsRadius = 15f;
    [Tooltip("Optional — if set, StartNewEpisode() is called so ML agents reset and re-grab the ball.")]
    public BasketballEnvController envController;

    private Vector3 _spawnPosition;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    void Start()
    {
        _spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
    }

    void Update()
    {
        if (inHand) return;

        Vector3 pos = transform.position;
        Vector2 xzOffset = new Vector2(pos.x - _spawnPosition.x, pos.z - _spawnPosition.z);

        if (pos.y < outOfBoundsY || xzOffset.magnitude > outOfBoundsRadius)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            transform.position = _spawnPosition;
            transform.rotation = Quaternion.identity;

            if (envController != null)
                envController.StartNewEpisode();
        }
    }


    void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnHold);
        grabInteractable.selectExited.AddListener(OnRelease);
    }

    void OnDisable()
    {
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

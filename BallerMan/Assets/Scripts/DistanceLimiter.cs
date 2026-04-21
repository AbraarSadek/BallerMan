using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class HoldDistanceLimiter : MonoBehaviour
{
    public float maxDistance = 0.6f; // max allowed from hand

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab;
    private Transform handTransform;

    void Awake()
    {
        grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        handTransform = args.interactorObject.transform;
    }

    void OnRelease(SelectExitEventArgs args)
    {
        handTransform = null;
    }

    void Update()
    {
        if (handTransform == null) return;

        float dist = Vector3.Distance(transform.position, handTransform.position);

        if (dist > maxDistance)
        {
            grab.interactionManager.SelectExit(
                (UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)grab.firstInteractorSelecting,
                grab
            );
        }
    }
}
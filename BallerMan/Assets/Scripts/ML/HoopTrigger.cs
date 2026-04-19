using System;
using UnityEngine;


public class HoopTrigger : MonoBehaviour
{
    [Tooltip("Tag used to identify the ball GameObject.")]
    public string ballTag = "Ball";

    [Tooltip("If true, only scores when the ball is moving downward (prevents side-entry exploits).")]
    public bool requireDownwardVelocity = true;

    [Tooltip("Minimum downward velocity (negative Y) required to count as a score.")]
    public float minDownwardVelocity = -0.5f;

    /// <summary>Fired once per episode when a valid score is detected.</summary>
    public event Action OnScore;

    private bool _scoredThisEpisode;

    /// <summary>Call this at the start of each new episode to re-arm the trigger.</summary>
    public void ResetForNewEpisode()
    {
        _scoredThisEpisode = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_scoredThisEpisode) return;
        if (!other.CompareTag(ballTag)) return;

        if (requireDownwardVelocity)
        {
            Rigidbody rb = other.attachedRigidbody;
            if (rb == null || rb.linearVelocity.y > minDownwardVelocity) return;
        }

        _scoredThisEpisode = true;
        OnScore?.Invoke();
    }
}

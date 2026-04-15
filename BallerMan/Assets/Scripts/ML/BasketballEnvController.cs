using System.Collections;
using UnityEngine;

/// <summary>
/// Central episode coordinator for ML-Agents basketball training.
/// Place this on an empty GameObject in the MLTraining scene.
/// Wire up all references in the Inspector.
/// </summary>
public class BasketballEnvController : MonoBehaviour
{
    [Header("Agents")]
    public ThrowAgent throwAgent;
    public BlockAgent blockAgent;

    [Header("Ball")]
    public Rigidbody ballRigidbody;
    public Transform ballSpawnPoint;

    [Header("Hoop")]
    public HoopTrigger hoopTrigger;

    [Header("Out-of-Bounds Settings")]
    [Tooltip("Y position below which the ball is considered out of bounds (e.g. fell off the court edge).")]
    public float outOfBoundsY = -5f;
    [Tooltip("XZ radius from the origin beyond which the ball is considered out of bounds.")]
    public float outOfBoundsRadius = 20f;

    [Header("Ground Detection")]
    [Tooltip("World Y of the court floor. Set this to match your Plane's world Y position.")]
    public float courtFloorY = 0f;
    [Tooltip("Ball must drop within this margin of courtFloorY (after throw) to trigger ground-hit end.")]
    public float groundMargin = 0.4f;

    [Header("Episode Settings")]
    public float maxEpisodeDurationSeconds = 10f;

    private float _episodeStartTime;
    private bool _throwHasOccurred;
    private bool _episodeEnding;

    private void Start()
    {
        hoopTrigger.OnScore += OnHoopScore;
        StartNewEpisode();
    }

    private void OnDestroy()
    {
        if (hoopTrigger != null)
            hoopTrigger.OnScore -= OnHoopScore;
    }

    private void Update()
    {
        if (_episodeEnding) return;

        Vector3 ballPos = ballRigidbody.transform.position;
        bool outOfBounds = ballPos.y < outOfBoundsY ||
                           new Vector2(ballPos.x, ballPos.z).magnitude > outOfBoundsRadius;

        // End episode early if ball hits the court floor after being thrown —
        // no need to wait out the full timeout for a clearly-failed throw.
        bool hitGround = _throwHasOccurred &&
                         ballPos.y <= courtFloorY + groundMargin &&
                         ballRigidbody.linearVelocity.y <= 0f;

        bool timedOut = Time.time - _episodeStartTime > maxEpisodeDurationSeconds;

        if (outOfBounds || hitGround || timedOut)
        {
            _episodeEnding = true;
            if (_throwHasOccurred)
            {
                throwAgent.OnBallOutOfBounds();
                blockAgent.OnBallOutOfBounds();
            }
            else
            {
                throwAgent.OnEpisodeTimeout();
            }
            StartNewEpisode();
        }
    }

    public void StartNewEpisode()
    {
        _throwHasOccurred = false;
        _episodeEnding = false;
        _episodeStartTime = Time.time;

        hoopTrigger.ResetForNewEpisode();
        ResetBall();

        // Ending an agent's episode triggers its OnEpisodeBegin automatically.
        throwAgent.EpisodeInterrupted();
        blockAgent.EpisodeInterrupted();
    }

    public void ResetBall()
    {
        // Must be non-kinematic before zeroing velocity; ThrowAgent holds ball kinematic.
        ballRigidbody.isKinematic     = false;
        ballRigidbody.linearVelocity  = Vector3.zero;
        ballRigidbody.angularVelocity = Vector3.zero;
        ballRigidbody.transform.SetParent(null);
        ballRigidbody.transform.position = ballSpawnPoint.position;
        ballRigidbody.transform.rotation = Quaternion.identity;
    }

    /// <summary>Called by ThrowAgent when it releases the ball.</summary>
    public void NotifyThrowOccurred()
    {
        _throwHasOccurred = true;
        blockAgent.OnThrowOccurred();
    }

    /// <summary>Called by BlockAgent when its paddle touches the ball.</summary>
    public void NotifyBlockHit()
    {
        if (_episodeEnding) return;
        _episodeEnding = true;
        throwAgent.OnBlockHit();
        // BlockAgent rewards itself internally in OnCollisionEnter.
        StartCoroutine(EndEpisodeNextFrame());
    }

    private void OnHoopScore()
    {
        if (_episodeEnding) return;
        _episodeEnding = true;
        throwAgent.OnScored();
        blockAgent.OnBallScored();
        StartCoroutine(EndEpisodeNextFrame());
    }

    // Wait one frame so reward calls are registered before the episode resets.
    private IEnumerator EndEpisodeNextFrame()
    {
        yield return null;
        StartNewEpisode();
    }
}

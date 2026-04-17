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
    [Tooltip("Drag the court floor (Plane) Transform here — its Y position is used as the floor level.")]
    public Transform courtFloor;
    [Tooltip("Ball must drop within this margin above the floor Y (after throw) to trigger ground-hit end.")]
    public float groundMargin = 0.4f;

    [Header("Diverge Detection")]
    [Tooltip("If ball moves this many metres further from hoop than its closest approach, end the episode.")]
    public float divergeThreshold = 2.5f;

    [Header("Episode Settings")]
    public float maxEpisodeDurationSeconds = 10f;

    private float _episodeStartTime;
    private bool _throwHasOccurred;
    private bool _episodeEnding;
    private float _closestDistToHoop;

    /// <summary>Minimum distance the ball has reached from the hoop this episode (set after throw).</summary>
    public float ClosestDistToHoop => _closestDistToHoop;

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

        Vector3 ballPos   = ballRigidbody.transform.position;
        Vector3 envCenter = transform.position;
        Vector3 hoopPos   = hoopTrigger.transform.position;

        bool outOfBounds = ballPos.y < outOfBoundsY ||
                           new Vector2(ballPos.x - envCenter.x, ballPos.z - envCenter.z).magnitude > outOfBoundsRadius;

        bool hitGround = false;
        bool diverging  = false;

        if (_throwHasOccurred)
        {
            // Track closest approach for proximity reward
            float dist = Vector3.Distance(ballPos, hoopPos);
            if (dist < _closestDistToHoop)
                _closestDistToHoop = dist;

            // Ground hit: ball reached floor level — no velocity check so rolling is caught too
            float floorY = courtFloor != null ? courtFloor.position.y : 0f;
            hitGround = ballPos.y <= floorY + groundMargin;

            // Diverging: ball is significantly further from hoop than its closest approach,
            // meaning it has peaked and is heading away — episode is over regardless of timeout
            diverging = dist > _closestDistToHoop + divergeThreshold;
        }

        bool timedOut = Time.time - _episodeStartTime > maxEpisodeDurationSeconds;

        if (outOfBounds || hitGround || diverging || timedOut)
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
        _throwHasOccurred    = false;
        _episodeEnding       = false;
        _episodeStartTime    = Time.time;
        _closestDistToHoop   = float.MaxValue;

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

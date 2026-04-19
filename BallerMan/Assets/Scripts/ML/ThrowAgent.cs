using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;


public class ThrowAgent : Agent
{
    [Header("References")]
    public BasketballEnvController envController;
    public Transform armTransform;
    public Transform handTransform;
    public Rigidbody ballRigidbody;
    public Transform hoopTransform;

    [Header("Body Movement")]
    [Tooltip("Maximum lateral (XZ) speed in metres per second.")]
    public float maxBodyMoveSpeed = 3f;
    [Tooltip("Agent is penalised per step when further than this from its episode spawn point.")]
    public float moveRadius = 3f;

    [Header("Spawn Randomisation")]
    [Tooltip("Each episode the agent spawns at a random point within this radius of its rest position.")]
    public float spawnRadius = 2f;

    [Header("Arm Settings")]
    public float maxArmSpeed = 360f;

    [Header("Jump Settings")]
    [Tooltip("Upward velocity applied when jumping.")]
    public float jumpForce = 8f;
    [Tooltip("Gravity applied to the agent body. Negative = downward.")]
    public float gravity = -20f;

    [Header("Throw Settings")]
    [Tooltip("Multiplier applied to arm tip velocity at release. Match Ball.cs throwBoost (3.5).")]
    public float throwBoostMultiplier = 3.5f;

    // Episode state
    private bool _hasBall;
    private bool _hasReleased;
    private Vector3 _prevHandPosition;

    // Manual jump physics (no Rigidbody needed)
    private float _jumpVelocityY;
    private bool _isGrounded;
    private float _groundY;

    // Reset state captured at Initialize
    private Vector3 _bodyRestPosition;
    private Vector3 _armRestLocalPosition;
    private Quaternion _armRestLocalRotation;

    // Per-episode spawn (varies each episode within spawnRadius)
    private Vector3 _episodeSpawnPosition;

    public override void Initialize()
    {
        _bodyRestPosition     = transform.position;
        _groundY              = transform.position.y;
        _armRestLocalPosition = armTransform.localPosition;
        _armRestLocalRotation = armTransform.localRotation;
    }

    public override void OnEpisodeBegin()
    {
        _hasBall     = true;
        _hasReleased = false;

        // Random spawn within spawnRadius of the rest position (XZ only)
        Vector2 offset = Random.insideUnitCircle * spawnRadius;
        _episodeSpawnPosition = _bodyRestPosition + new Vector3(offset.x, 0f, offset.y);

        transform.position = _episodeSpawnPosition;
        transform.rotation = Quaternion.identity;

        // Reset jump state
        _jumpVelocityY = 0f;
        _isGrounded    = true;

        // Reset arm
        armTransform.localPosition = _armRestLocalPosition;
        armTransform.localRotation = _armRestLocalRotation;

        // Reset ball — must be non-kinematic before zeroing velocity
        ballRigidbody.isKinematic     = false;
        ballRigidbody.transform.SetParent(null);
        ballRigidbody.linearVelocity  = Vector3.zero;
        ballRigidbody.angularVelocity = Vector3.zero;
        ballRigidbody.isKinematic     = true;
        ballRigidbody.transform.SetParent(handTransform);
        ballRigidbody.transform.localPosition = Vector3.zero;
        ballRigidbody.transform.localRotation = Quaternion.identity;

        _prevHandPosition = handTransform.position;
    }

    /// <summary>
    /// 31 floats total:
    ///   [0-2]   Ball position relative to hand (3)
    ///   [3-5]   Ball linearVelocity (3)
    ///   [6-8]   Hand world position (3)
    ///   [9-12]  Arm local rotation quaternion (4)
    ///   [13-15] Hoop position relative to agent root (3)
    ///   [16-18] Hoop direction normalized from hand (3)
    ///   [19]    Hoop distance from hand (1)
    ///   [20]    Step fraction (1)
    ///   [21]    _hasReleased flag (1)
    ///   [22-24] Arm velocity approximation (3)
    ///   [25]    _hasBall flag (1)
    ///   [26]    Vertical jump velocity (1)
    ///   [27]    Is grounded flag (1)
    ///   [28-30] Body position relative to episode spawn (3)
    /// </summary>
    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 handPos = handTransform.position;
        Vector3 hoopPos = hoopTransform.position;

        sensor.AddObservation(ballRigidbody.transform.position - handPos);                       // 3
        sensor.AddObservation(ballRigidbody.linearVelocity);                                     // 3
        sensor.AddObservation(handPos);                                                           // 3
        sensor.AddObservation(armTransform.localRotation);                                        // 4

        sensor.AddObservation(hoopPos - transform.position);                                      // 3

        Vector3 toHoop = hoopPos - handPos;
        sensor.AddObservation(toHoop.magnitude > 0.01f ? toHoop.normalized : Vector3.forward);   // 3
        sensor.AddObservation(toHoop.magnitude);                                                  // 1

        sensor.AddObservation(MaxStep > 0 ? StepCount / (float)MaxStep : 0f);                   // 1
        sensor.AddObservation(_hasReleased ? 1f : 0f);                                           // 1

        Vector3 armVelocity = (handPos - _prevHandPosition) / Time.fixedDeltaTime;
        sensor.AddObservation(armVelocity);                                                       // 3

        sensor.AddObservation(_hasBall ? 1f : 0f);                                              // 1
        sensor.AddObservation(_jumpVelocityY);                                                    // 1
        sensor.AddObservation(_isGrounded ? 1f : 0f);                                            // 1

        sensor.AddObservation(transform.position - _episodeSpawnPosition);                        // 3
        // Total: 31
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveX         = actions.ContinuousActions[0];
        float moveZ         = actions.ContinuousActions[1];
        float pitchDelta    = actions.ContinuousActions[2] * maxArmSpeed * Time.fixedDeltaTime;
        float yawDelta      = actions.ContinuousActions[3] * maxArmSpeed * Time.fixedDeltaTime;
        float rollDelta     = actions.ContinuousActions[4] * maxArmSpeed * Time.fixedDeltaTime;
        float releaseSignal = actions.ContinuousActions[5];
        float jumpSignal    = actions.ContinuousActions[6];

        // --- Lateral body movement ---
        Vector3 pos = transform.position;
        pos.x += moveX * maxBodyMoveSpeed * Time.fixedDeltaTime;
        pos.z += moveZ * maxBodyMoveSpeed * Time.fixedDeltaTime;

        // Soft penalty for wandering too far from episode spawn
        Vector2 xzDrift = new Vector2(pos.x - _episodeSpawnPosition.x, pos.z - _episodeSpawnPosition.z);
        if (xzDrift.magnitude > moveRadius)
            AddReward(-0.005f);

        // --- Rotate arm ---
        armTransform.Rotate(pitchDelta, yawDelta, rollDelta, Space.Self);

        // --- Manual jump physics ---
        if (jumpSignal > 0.5f && _isGrounded)
        {
            _jumpVelocityY = jumpForce;
            _isGrounded    = false;
        }

        _jumpVelocityY += gravity * Time.fixedDeltaTime;
        pos.y += _jumpVelocityY * Time.fixedDeltaTime;

        if (pos.y <= _groundY)
        {
            pos.y          = _groundY;
            _jumpVelocityY = 0f;
            _isGrounded    = true;
        }

        transform.position = pos;

        // --- Release ---
        if (_hasBall && !_hasReleased && releaseSignal > 0.5f)
            ExecuteThrow();

        AddReward(-0.001f);

        _prevHandPosition = handTransform.position;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var kb = Keyboard.current;
        var c  = actionsOut.ContinuousActions;
        c[0] = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);  // A/D → bodyX
        c[1] = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);  // W/S → bodyZ
        c[2] = 0f;  // pitch
        c[3] = 0f;  // yaw
        c[4] = (kb.eKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed ? 1f : 0f);  // Q/E → roll
        c[5] = kb.fKey.isPressed ? 1f : 0f;                                      // F → release
        c[6] = kb.spaceKey.isPressed ? 1f : 0f;                                  // Space → jump
    }

    private void ExecuteThrow()
    {
        _hasReleased = true;
        _hasBall     = false;

        // Release velocity includes arm rotation and body momentum
        Vector3 releaseVelocity = (handTransform.position - _prevHandPosition) / Time.fixedDeltaTime;

        ballRigidbody.transform.SetParent(null);
        ballRigidbody.isKinematic    = false;
        ballRigidbody.linearVelocity = releaseVelocity * throwBoostMultiplier;

        // Small arc bonus at release — encourages upward throws needed to reach the hoop
        float arcBonus = Mathf.Clamp01(releaseVelocity.normalized.y) * 0.05f;
        AddReward(arcBonus);

        envController.NotifyThrowOccurred();
    }

    /// <summary>
    /// Proximity reward: exponential falloff from hoop. Max ~1.0 when ball passes through,
    /// decays smoothly with distance so every improvement in aim is rewarded.
    /// Only given if ball was actually thrown.
    /// </summary>
    private void GiveProximityReward()
    {
        if (!_hasReleased) return;
        float closest = envController.ClosestDistToHoop;
        if (closest >= float.MaxValue) return;
        // exp(-d * 0.5): d=0 → 1.0, d=1m → 0.61, d=2m → 0.37, d=5m → 0.08
        AddReward(Mathf.Exp(-closest * 0.5f));
    }

    // ---- Callbacks from BasketballEnvController ----

    public void OnScored()
    {
        GiveProximityReward();
        AddReward(2f);
        // EndEpisode() — controller calls EndEpisode/EpisodeInterrupted centrally via StartNewEpisode()
    }

    public void OnBallOutOfBounds()
    {
        GiveProximityReward();
        // EndEpisode() — controller calls EndEpisode/EpisodeInterrupted centrally via StartNewEpisode()
    }

    public void OnBlockHit()
    {
        GiveProximityReward();
        AddReward(-0.1f);
    }

    public void OnEpisodeTimeout()
    {
        GiveProximityReward();
        // EndEpisode() — controller calls EndEpisode/EpisodeInterrupted centrally via StartNewEpisode()
    }
}

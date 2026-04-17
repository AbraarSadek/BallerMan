using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ML-Agents throwing agent for basketball.
///
/// Rig hierarchy expected:
///   Rig  (this script + BehaviorParameters + DecisionRequester)
///   |    NO Rigidbody on root — position controlled directly
///   ├── Base  (visual body mesh)
///   └── Arm   (armTransform — rotates pitch/yaw/roll each step)
///       └── Hand  (handTransform — ball attaches here)
///
/// The Hand child does NOT need a Rigidbody.
/// The Ball Rigidbody is set kinematic while held, non-kinematic on release.
///
/// Observation space: 29 floats
/// Action space: 5 continuous (pitch, yaw, roll, release, jump)
/// </summary>
public class ThrowAgent : Agent
{
    [Header("References")]
    public BasketballEnvController envController;
    public Transform armTransform;
    public Transform handTransform;
    public Rigidbody ballRigidbody;
    public Transform hoopTransform;

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

    public override void Initialize()
    {
        _bodyRestPosition     = transform.position;
        _groundY              = transform.position.y;
        _armRestLocalPosition = armTransform.localPosition;
        _armRestLocalRotation = armTransform.localRotation;
    }

    public override void OnEpisodeBegin()
    {
        _hasBall = true;
        _hasReleased = false;

        // Reset body position directly — no Rigidbody needed
        transform.position = _bodyRestPosition;
        transform.rotation = Quaternion.identity;

        // Reset jump state
        _jumpVelocityY = 0f;
        _isGrounded    = true;

        // Reset arm
        armTransform.localPosition = _armRestLocalPosition;
        armTransform.localRotation = _armRestLocalRotation;

        // Reset ball — must be non-kinematic before zeroing velocity
        // (ball may still be kinematic if episode ended before throw)
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
    /// 29 floats total:
    ///   [0-2]  Ball position relative to hand (3)
    ///   [3-5]  Ball linearVelocity (3)
    ///   [6-8]  Hand world position (3)
    ///   [9-12] Arm local rotation quaternion (4)
    ///   [13-15] Hoop position relative to agent root (3)
    ///   [16-18] Hoop direction normalized from hand (3)
    ///   [19]   Hoop distance from hand (1)
    ///   [20]   Step fraction (1)
    ///   [21]   _hasReleased flag (1)
    ///   [22-24] Arm velocity approximation (3)
    ///   [25]   _hasBall flag (1)
    ///   [26]   Vertical jump velocity (1)
    ///   [27]   Is grounded flag (1)
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
        // Total: 29
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float pitchDelta    = actions.ContinuousActions[0] * maxArmSpeed * Time.fixedDeltaTime;
        float yawDelta      = actions.ContinuousActions[1] * maxArmSpeed * Time.fixedDeltaTime;
        float rollDelta     = actions.ContinuousActions[2] * maxArmSpeed * Time.fixedDeltaTime;
        float releaseSignal = actions.ContinuousActions[3];
        float jumpSignal    = actions.ContinuousActions[4];

        // --- Rotate arm (child transform — no Rigidbody interference) ---
        armTransform.Rotate(pitchDelta, yawDelta, rollDelta, Space.Self);

        // --- Manual jump physics ---
        if (jumpSignal > 0.5f && _isGrounded)
        {
            _jumpVelocityY = jumpForce;
            _isGrounded    = false;
        }

        _jumpVelocityY += gravity * Time.fixedDeltaTime;

        Vector3 pos = transform.position;
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
        c[0] = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        c[1] = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        c[2] = (kb.eKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed ? 1f : 0f);
        c[3] = kb.fKey.isPressed ? 1f : 0f;
        c[4] = kb.spaceKey.isPressed ? 1f : 0f;
    }

    private void ExecuteThrow()
    {
        _hasReleased = true;
        _hasBall     = false;

        // Release velocity includes both arm rotation and upward body momentum from jump
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
        AddReward(2f);   // Extra bonus for actually going through the hoop
        EndEpisode();
    }

    public void OnBallOutOfBounds()
    {
        GiveProximityReward();
        EndEpisode();
    }

    public void OnBlockHit()
    {
        GiveProximityReward();
        AddReward(-0.1f);  // Penalty for being blocked
    }

    public void OnEpisodeTimeout()
    {
        GiveProximityReward();
        EndEpisode();
    }
}

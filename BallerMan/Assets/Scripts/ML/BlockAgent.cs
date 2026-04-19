using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;


public class BlockAgent : Agent
{
    [Header("References")]
    public BasketballEnvController envController;
    public Transform armTransform;
    public Transform handTransform;
    public Rigidbody ballRigidbody;
    public Transform hoopTransform;

    [Header("Body Movement")]
    [Tooltip("Maximum lateral (XZ) speed in metres per second.")]
    public float maxBodyMoveSpeed = 4f;

    [Header("Thrower Avoidance")]
    [Tooltip("The ThrowAgent transform — block agent is penalised for getting closer than minDistToThrower.")]
    public Transform throwerTransform;
    [Tooltip("Minimum allowed distance from the thrower before penalty kicks in.")]
    public float minDistToThrower = 1.5f;
    [Tooltip("Reward applied per step while inside minDistToThrower.")]
    public float throwerProximityPenalty = -0.02f;

    [Header("Jump Settings")]
    [Tooltip("Upward velocity applied when jumping.")]
    public float jumpForce = 6f;
    [Tooltip("Gravity applied to the agent body. Negative = downward.")]
    public float gravity = -20f;

    [Header("Arm")]
    [Tooltip("Maximum arm rotation speed in degrees per second.")]
    public float maxArmSpeed = 180f;

    // Episode state
    private bool _ballHasBeenReleased;
    private bool _blockHandled;

    // Manual jump physics
    private float _jumpVelocityY;
    private bool _isGrounded;
    private float _groundY;

    // Reset state captured at Initialize
    private Vector3 _bodyRestPosition;
    private Quaternion _bodyRestRotation;
    private Quaternion _armRestLocalRotation;

    public override void Initialize()
    {
        _bodyRestPosition     = transform.position;
        _bodyRestRotation     = transform.rotation;
        _groundY              = transform.position.y;
        _armRestLocalRotation = armTransform.localRotation;
    }

    public override void OnEpisodeBegin()
    {
        _ballHasBeenReleased = false;
        _blockHandled = false;

        // Reset body position and rotation to initial scene placement
        transform.position = _bodyRestPosition;
        transform.rotation = _bodyRestRotation;

        // Reset jump state
        _jumpVelocityY = 0f;
        _isGrounded    = true;

        // Reset arm
        armTransform.localRotation = _armRestLocalRotation;
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 handPos     = handTransform.position;
        Vector3 ballPos     = ballRigidbody.transform.position;
        Vector3 hoopPos     = hoopTransform.position;
        Vector3 bodyPos     = transform.position;
        Vector3 throwerPos  = throwerTransform != null ? throwerTransform.position : bodyPos;

        // [0-2] Body velocity approximation — use jump velocity on Y, zeros on XZ
        sensor.AddObservation(new Vector3(0f, _jumpVelocityY, 0f));                // 3
        sensor.AddObservation(_isGrounded ? 1f : 0f);                               // 1
        sensor.AddObservation(armTransform.localRotation);                           // 4
        sensor.AddObservation(handPos);                                              // 3
        sensor.AddObservation(ballPos - handPos);                                    // 3
        sensor.AddObservation(ballRigidbody.linearVelocity);                        // 3
        sensor.AddObservation(hoopPos - bodyPos);                                    // 3
        sensor.AddObservation(Vector3.Distance(ballPos, hoopPos));                   // 1
        sensor.AddObservation(MaxStep > 0 ? 1f - StepCount / (float)MaxStep : 0f); // 1
        sensor.AddObservation(ballRigidbody.linearVelocity.magnitude);              // 1
        sensor.AddObservation(ballPos.y - hoopPos.y);                                // 1
        sensor.AddObservation(bodyPos - throwerPos);                                
        
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveX    = actions.ContinuousActions[0];
        float moveZ    = actions.ContinuousActions[1];
        float jumpSig  = actions.ContinuousActions[2];
        float armPitch = actions.ContinuousActions[3];
        float armYaw   = actions.ContinuousActions[4];

        // --- Lateral body movement (direct position, no Rigidbody) ---
        Vector3 pos = transform.position;
        pos.x += moveX * maxBodyMoveSpeed * Time.fixedDeltaTime;
        pos.z += moveZ * maxBodyMoveSpeed * Time.fixedDeltaTime;

        // --- Thrower proximity penalty — discourages crowding the thrower ---
        if (throwerTransform != null)
        {
            float distToThrower = Vector3.Distance(pos, throwerTransform.position);
            if (distToThrower < minDistToThrower)
                AddReward(throwerProximityPenalty);
        }

        // --- Manual jump physics ---
        if (jumpSig > 0.5f && _isGrounded)
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

        // --- Arm rotation ---
        float pitchDelta = armPitch * maxArmSpeed * Time.fixedDeltaTime;
        float yawDelta   = armYaw   * maxArmSpeed * Time.fixedDeltaTime;
        armTransform.Rotate(pitchDelta, yawDelta, 0f, Space.Self);

        // --- Per-step existence penalty ---
        AddReward(-0.001f);

        // --- Proximity shaping after ball is released ---
        if (_ballHasBeenReleased)
        {
            float dist = Vector3.Distance(handTransform.position, ballRigidbody.transform.position);
            AddReward(0.05f / (1f + dist));
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var kb = Keyboard.current;
        var c = actionsOut.ContinuousActions;
        c[0] = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);                       // A/D  → X
        c[1] = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);                       // W/S  → Z
        c[2] = kb.spaceKey.isPressed ? 1f : 0f;                                                       // Space → jump
        c[3] = (kb.upArrowKey.isPressed ? 1f : 0f) - (kb.downArrowKey.isPressed ? 1f : 0f);         // ↑/↓  → arm pitch
        c[4] = (kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.leftArrowKey.isPressed ? 1f : 0f);      // ←/→  → arm yaw
    }

    /// <summary>Called by HandCollider.cs when the hand touches the ball.</summary>
    public void OnHandCollision()
    {
        if (_blockHandled) return;
        _blockHandled = true;

        AddReward(1.0f);
        envController.NotifyBlockHit();
    }

    // ---- Callbacks from BasketballEnvController ----

    public void OnThrowOccurred()
    {
        _ballHasBeenReleased = true;
    }

    public void OnBallScored()
    {
        AddReward(-1.0f);
        // EndEpisode() — controller calls EndEpisode/EpisodeInterrupted centrally via StartNewEpisode()
    }

    public void OnBallOutOfBounds()
    {
        // Neutral — ball missed hoop without being blocked
        // EndEpisode() — controller calls EndEpisode/EpisodeInterrupted centrally via StartNewEpisode()
    }
}

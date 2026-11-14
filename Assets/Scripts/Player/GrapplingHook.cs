using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastDelivery.Player
{
    [RequireComponent(typeof(PlayerController), typeof(CharacterController), typeof(Rigidbody))]
    [RequireComponent(typeof(LineRenderer), typeof(PlayerInput))]
    public class GrapplingHook : MonoBehaviour
    {
        private const float KillPlaneYThreshold = -45f;

        [SerializeField] private SwingConfig config;
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private Transform ropeOrigin;

        private PlayerController player;
        private Rigidbody rigidbodyComponent;
        private LineRenderer lineRenderer;
        private Transform cam;
        private InputAction grappleAction;
        private InputAction jumpAction;
        private InputAction moveAction;
        private InputAction reelInAction;
        private InputAction reelOutAction;
        private Collider[] anchorBuffer;
        private Collider[] overlapBuffer;

        private bool isAttached;
        private bool attachRequested;
        private bool releaseRequested;
        private bool isReelInHeld;
        private bool isReelOutHeld;
        private float obstructionTimer;
        private Vector3 lastValidPosition;
        private Vector3 anchorPoint;
        private Transform anchorTransform;
        private Transform targetedAnchorTransform;
        private Vector3 targetedAnchorPoint;
        private float currentLength;

        public bool IsAttached => isAttached;
        public Vector3 AnchorPoint => anchorPoint;
        public float ObstructionTimer => obstructionTimer;
        public Vector3 LastValidPosition => lastValidPosition;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            rigidbodyComponent = GetComponent<Rigidbody>();
            lineRenderer = GetComponent<LineRenderer>();
            if (playerInput == null) playerInput = GetComponent<PlayerInput>();

            anchorBuffer = new Collider[32];
            overlapBuffer = new Collider[8];
            lineRenderer.positionCount = 2;
            lineRenderer.enabled = false;

            if (ropeOrigin == null) ropeOrigin = transform;
            if (Camera.main != null) cam = Camera.main.transform;

            if (config == null)
            {
                Debug.LogError("GrapplingHook requires a SwingConfig.", this);
                enabled = false;
                return;
            }

            if (playerInput == null)
            {
                Debug.LogError("GrapplingHook requires a PlayerInput component.", this);
                enabled = false;
                return;
            }

            grappleAction = playerInput.actions.FindAction("Grapple", false);
            jumpAction = playerInput.actions.FindAction("Jump", false);
            moveAction = playerInput.actions.FindAction("Move", false);
            reelInAction = playerInput.actions.FindAction("ReelIn", false);
            reelOutAction = playerInput.actions.FindAction("ReelOut", false);

            if (grappleAction == null || jumpAction == null || moveAction == null || reelInAction == null || reelOutAction == null)
            {
                Debug.LogError("GrapplingHook requires Grapple, Jump, Move, ReelIn, and ReelOut input actions.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (grappleAction != null)
            {
                grappleAction.performed += OnGrapplePerformed;
                grappleAction.canceled += OnGrappleCanceled;
            }

            if (jumpAction != null)
            {
                jumpAction.performed += OnJumpPerformed;
            }

            if (reelInAction != null)
            {
                reelInAction.performed += OnReelInPerformed;
                reelInAction.canceled += OnReelInCanceled;
            }

            if (reelOutAction != null)
            {
                reelOutAction.performed += OnReelOutPerformed;
                reelOutAction.canceled += OnReelOutCanceled;
            }
        }

        private void OnDisable()
        {
            if (grappleAction != null)
            {
                grappleAction.performed -= OnGrapplePerformed;
                grappleAction.canceled -= OnGrappleCanceled;
            }

            if (jumpAction != null)
            {
                jumpAction.performed -= OnJumpPerformed;
            }

            if (reelInAction != null)
            {
                reelInAction.performed -= OnReelInPerformed;
                reelInAction.canceled -= OnReelInCanceled;
            }

            if (reelOutAction != null)
            {
                reelOutAction.performed -= OnReelOutPerformed;
                reelOutAction.canceled -= OnReelOutCanceled;
            }

            isReelInHeld = false;
            isReelOutHeld = false;
            attachRequested = false;
            releaseRequested = false;
            obstructionTimer = 0f;
            if (player != null)
            {
                player.SetGrappleTarget(null);
            }
        }

        private void Update()
        {
            if (cam == null && Camera.main != null) cam = Camera.main.transform;

            if (isAttached)
            {
                if (anchorTransform != null)
                {
                    anchorPoint = anchorTransform.position;
                    player.SetGrappleTarget(anchorPoint);
                }
                lineRenderer.SetPosition(0, ropeOrigin.position);
                lineRenderer.SetPosition(1, anchorPoint);
            }
            else
            {
                UpdateAnchorTargeting();
            }
        }

        private void FixedUpdate()
        {
            if (attachRequested)
            {
                attachRequested = false;
                TryAttach();
            }

            if (!isAttached) return;

            // T028 [US4/Polish]: Player falls below level / swings into kill plane.
            // Auto-release before respawn triggers so boundary/kill-plane system handles respawn cleanly.
            if (rigidbodyComponent.position.y < KillPlaneYThreshold)
            {
                Release();
                return;
            }

            if (releaseRequested || anchorTransform == null || !anchorTransform.gameObject.activeInHierarchy)
            {
                releaseRequested = false;
                Release();
                return;
            }

            // T020 [US4]: Safety check to guarantee ContinuousDynamic during swing mode
            if (rigidbodyComponent.collisionDetectionMode != CollisionDetectionMode.ContinuousDynamic)
            {
                rigidbodyComponent.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            anchorPoint = anchorTransform.position;
            player.SetGrappleTarget(anchorPoint);

            Vector3 playerPos = rigidbodyComponent.position;

            // T019 [US4]: Stuck-state detection and recovery
            bool isOverlapping = CheckGeometryOverlap(playerPos);
            if (!isOverlapping)
            {
                lastValidPosition = playerPos;
            }
            else if (rigidbodyComponent.velocity.magnitude < config.stuckVelocityThreshold)
            {
                rigidbodyComponent.position = lastValidPosition;
                transform.position = lastValidPosition;
                rigidbodyComponent.velocity = Vector3.zero;
                rigidbodyComponent.AddForce(Vector3.up * config.stuckRecoveryImpulse, ForceMode.Impulse);
                Release();
                return;
            }

            // T018 [US4]: Tether obstruction detection
            Vector3 toAnchor = anchorPoint - playerPos;
            float distanceToAnchor = toAnchor.magnitude;
            bool isObstructed = false;

            if (distanceToAnchor > 0.001f)
            {
                Vector3 dirToAnchor = toAnchor / distanceToAnchor;
                if (Physics.Raycast(playerPos, dirToAnchor, out RaycastHit hit, distanceToAnchor, config.obstacleLayerMask, QueryTriggerInteraction.Ignore))
                {
                    bool hitAnchor = anchorTransform != null && (hit.collider.transform == anchorTransform || hit.collider.transform.IsChildOf(anchorTransform));
                    bool hitSelf = hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform);
                    if (!hitAnchor && !hitSelf)
                    {
                        isObstructed = true;
                    }
                }
            }

            if (isObstructed)
            {
                obstructionTimer += Time.fixedDeltaTime;
                if (obstructionTimer >= config.obstructionGracePeriod)
                {
                    Release();
                    return;
                }
            }
            else
            {
                obstructionTimer = 0f;
            }

            ApplySpiderSwingPhysics();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isAttached) return;

            // T028: Auto-release if player swings into a kill plane or boundary trigger volume before respawn
            if (other != null && other.isTrigger)
            {
                if (other.CompareTag("Respawn") ||
                    other.CompareTag("Finish") ||
                    other.gameObject.name.IndexOf("Kill", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    other.gameObject.name.IndexOf("Boundary", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    other.gameObject.name.IndexOf("Death", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    other.gameObject.name.IndexOf("Fall", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    other.gameObject.name.IndexOf("Hazard", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Release();
                }
            }
        }

        private void OnGrapplePerformed(InputAction.CallbackContext context)
        {
            if (!isAttached) attachRequested = true;
        }

        private void OnGrappleCanceled(InputAction.CallbackContext context)
        {
            if (isAttached) releaseRequested = true;
        }

        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            if (!isAttached) return;

            // T026: If player is grounded / resting on ground when attached, Jump initiates swing motion instead of releasing
            if (CheckGroundedInSwing())
            {
                float jumpVelocityY = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y * config.swingGravityMultiplier) * 2.2f);
                Vector3 forwardDir = GetForwardDirection();
                rigidbodyComponent.velocity = new Vector3(forwardDir.x * 4f, jumpVelocityY, forwardDir.z * 4f);
                return;
            }

            releaseRequested = true;
        }

        private void OnReelInPerformed(InputAction.CallbackContext context)
        {
            isReelInHeld = true;
        }

        private void OnReelInCanceled(InputAction.CallbackContext context)
        {
            isReelInHeld = false;
        }

        private void OnReelOutPerformed(InputAction.CallbackContext context)
        {
            isReelOutHeld = true;
        }

        private void OnReelOutCanceled(InputAction.CallbackContext context)
        {
            isReelOutHeld = false;
        }

        private void TryAttach()
        {
            if (isAttached) return;
            if (player == null || !player.HasGrappleTarget || !player.CurrentGrapplePoint.HasValue) return;
            if (targetedAnchorTransform == null || !targetedAnchorTransform.gameObject.activeInHierarchy) return;

            anchorTransform = targetedAnchorTransform;
            anchorPoint = player.CurrentGrapplePoint.Value;
            float initialDistance = Vector3.Distance(rigidbodyComponent.position, anchorPoint);
            currentLength = Mathf.Clamp(initialDistance, config.minTetherLength, config.maxTetherLength);

            Vector3 toAnchorVector = anchorPoint - rigidbodyComponent.position;
            Vector3 horizontalOffset = new Vector3(toAnchorVector.x, 0f, toAnchorVector.z);

            // T025: Zero-length pendulum arm edge case (anchor directly above)
            bool isDirectlyAbove = horizontalOffset.sqrMagnitude < 0.0625f && toAnchorVector.y > 0.1f;

            // T026: Grounded at same height edge case
            bool isGroundedAtSameHeight = player.IsGrounded && Mathf.Abs(toAnchorVector.y) < 1.0f;

            isAttached = true;
            obstructionTimer = 0f;
            lastValidPosition = rigidbodyComponent.position;
            player.EnterSwingMode();

            if (rigidbodyComponent.collisionDetectionMode != CollisionDetectionMode.ContinuousDynamic)
            {
                rigidbodyComponent.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            if (isDirectlyAbove)
            {
                // T025: Ensure tether clamps to minTetherLength and player hangs vertically; reel-out begins the swing
                currentLength = config.minTetherLength;
                rigidbodyComponent.velocity = new Vector3(0f, Mathf.Min(rigidbodyComponent.velocity.y, 0f), 0f);
            }
            else if (isGroundedAtSameHeight)
            {
                // T026: Player grounded at same height — tether engages at equilibrium; no artificial velocity injected
            }
            else
            {
                Vector3 toAnchor = toAnchorVector.normalized;
                Vector3 forwardDir = GetForwardDirection();
                rigidbodyComponent.velocity += (toAnchor * 6f) + (forwardDir * 5f) + (Vector3.up * 3.5f);
            }

            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, ropeOrigin.position);
            lineRenderer.SetPosition(1, anchorPoint);
        }

        public void Release()
        {
            if (!isAttached) return;

            Vector3 forwardDir = GetForwardDirection();
            Vector3 baseVelocity = rigidbodyComponent.velocity * config.releaseBoostMultiplier;
            Vector3 launchVelocity = baseVelocity + (forwardDir * config.releaseForwardBoost);
            launchVelocity.y = Mathf.Max(baseVelocity.y, 0f) + config.releaseUpwardBoost;

            isAttached = false;
            attachRequested = false;
            releaseRequested = false;
            isReelInHeld = false;
            isReelOutHeld = false;
            obstructionTimer = 0f;
            anchorPoint = Vector3.zero;
            anchorTransform = null;
            lineRenderer.enabled = false;
            player.SetGrappleTarget(null);
            player.ExitSwingMode(launchVelocity);
        }

        private void UpdateAnchorTargeting()
        {
            if (config == null || player == null) return;

            Vector3 playerPos = transform.position;
            Vector3 camForward = cam != null ? cam.forward : transform.forward;

            float totalWeight = config.viewConeWeight + config.distanceWeight;
            float normViewConeWeight = totalWeight > 0.0001f ? (config.viewConeWeight / totalWeight) : 0.5f;
            float normDistanceWeight = totalWeight > 0.0001f ? (config.distanceWeight / totalWeight) : 0.5f;

            int count = Physics.OverlapSphereNonAlloc(
                playerPos,
                config.maxAnchorRange,
                anchorBuffer,
                config.anchorLayerMask,
                QueryTriggerInteraction.Ignore);

            Collider bestCandidate = null;
            Vector3 bestPoint = Vector3.zero;
            float bestScore = float.MinValue;

            for (int i = 0; i < count; i++)
            {
                Collider candidate = anchorBuffer[i];
                anchorBuffer[i] = null;

                if (candidate == null || !candidate.gameObject.activeInHierarchy || !candidate.CompareTag("GrapplePoint"))
                {
                    continue;
                }

                Vector3 candidatePoint = candidate.bounds.center;
                Vector3 toCandidate = candidatePoint - playerPos;
                float distance = toCandidate.magnitude;
                if (distance <= 0.001f || distance > config.maxAnchorRange)
                {
                    continue;
                }

                Vector3 dirToCandidate = toCandidate / distance;

                float dot = Vector3.Dot(camForward, dirToCandidate);
                if (dot <= 0f)
                {
                    continue;
                }

                float viewConeScore = Mathf.Clamp01(dot);

                if (Physics.Raycast(playerPos, dirToCandidate, out RaycastHit hit, distance, config.obstacleLayerMask, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider != candidate && !hit.collider.transform.IsChildOf(candidate.transform) && hit.collider.transform != transform)
                    {
                        continue;
                    }
                }

                float distanceFactor = 1f - Mathf.Clamp01(distance / config.maxAnchorRange);
                float combinedScore = (normViewConeWeight * viewConeScore) + (normDistanceWeight * distanceFactor);

                if (combinedScore > bestScore)
                {
                    bestScore = combinedScore;
                    bestCandidate = candidate;
                    bestPoint = candidatePoint;
                }
                else if (Mathf.Approximately(combinedScore, bestScore))
                {
                    if (bestCandidate == null || candidate.GetInstanceID() < bestCandidate.GetInstanceID())
                    {
                        bestScore = combinedScore;
                        bestCandidate = candidate;
                        bestPoint = candidatePoint;
                    }
                }
            }

            if (bestCandidate != null)
            {
                targetedAnchorTransform = bestCandidate.transform;
                targetedAnchorPoint = bestPoint;
                player.SetGrappleTarget(bestPoint);
            }
            else
            {
                targetedAnchorTransform = null;
                targetedAnchorPoint = Vector3.zero;
                player.SetGrappleTarget(null);
            }
        }

        private void ApplySpiderSwingPhysics()
        {
            Vector3 toAnchor = anchorPoint - rigidbodyComponent.position;
            float distance = toAnchor.magnitude;
            if (distance <= Mathf.Epsilon) return;

            Vector3 ropeDirection = toAnchor / distance;
            Vector3 planarOffset = new Vector3(toAnchor.x, 0f, toAnchor.z);
            bool isHangingVertically = planarOffset.sqrMagnitude < 0.0625f && ropeDirection.y > 0.95f;

            bool reelIn = isReelInHeld || (reelInAction != null && reelInAction.IsPressed());
            bool reelOut = isReelOutHeld || (reelOutAction != null && reelOutAction.IsPressed());
            Vector2 moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

            if (reelIn)
            {
                currentLength -= config.reelInSpeed * Time.fixedDeltaTime;
            }
            if (reelOut)
            {
                currentLength += config.reelOutSpeed * Time.fixedDeltaTime;
                if (isHangingVertically)
                {
                    // T025: Reel-out begins the swing from hanging vertically
                    Vector3 forwardDir = GetForwardDirection();
                    rigidbodyComponent.AddForce(forwardDir * (config.forwardSwingDrive * 0.5f), ForceMode.Acceleration);
                }
            }
            currentLength = Mathf.Clamp(currentLength, config.minTetherLength, config.maxTetherLength);

            rigidbodyComponent.AddForce(Physics.gravity * config.swingGravityMultiplier, ForceMode.Acceleration);

            // In dynamic swing, apply web pull and forward swing drive. Skip when hanging vertically at rest with zero input.
            if (!isHangingVertically || moveInput.sqrMagnitude > 0.01f || reelOut)
            {
                rigidbodyComponent.AddForce(ropeDirection * config.webPullForce, ForceMode.Acceleration);

                Vector3 forwardDir = GetForwardDirection();
                Vector3 forwardTangent = Vector3.ProjectOnPlane(forwardDir, ropeDirection);
                if (forwardTangent.sqrMagnitude > 0.001f)
                {
                    rigidbodyComponent.AddForce(forwardTangent.normalized * config.forwardSwingDrive, ForceMode.Acceleration);
                }
            }

            // T027: Reel-in clamps tether at minTetherLength before contact with anchor geometry
            if (distance <= config.minTetherLength)
            {
                rigidbodyComponent.position = anchorPoint - ropeDirection * config.minTetherLength;
                Vector3 velocity = rigidbodyComponent.velocity;
                float inwardVelocity = Vector3.Dot(velocity, ropeDirection);
                if (inwardVelocity > 0f)
                {
                    rigidbodyComponent.velocity = velocity - ropeDirection * inwardVelocity;
                }
            }
            else if (distance > currentLength)
            {
                rigidbodyComponent.position = anchorPoint - ropeDirection * currentLength;

                Vector3 velocity = rigidbodyComponent.velocity;
                float outwardVelocity = Vector3.Dot(velocity, -ropeDirection);
                if (outwardVelocity > 0f)
                {
                    rigidbodyComponent.velocity = velocity + ropeDirection * outwardVelocity;
                }
            }

            Vector3 planarVel = new Vector3(rigidbodyComponent.velocity.x, 0f, rigidbodyComponent.velocity.z);
            if (planarVel.sqrMagnitude > 0.25f)
            {
                Quaternion targetRot = Quaternion.LookRotation(planarVel.normalized, Vector3.up);
                rigidbodyComponent.MoveRotation(Quaternion.Slerp(rigidbodyComponent.rotation, targetRot, 10f * Time.fixedDeltaTime));
            }

            rigidbodyComponent.velocity *= config.swingDamping;
            ApplySteering(ropeDirection);
        }

        private void ApplySteering(Vector3 ropeDirection)
        {
            if (moveAction == null) return;
            Vector2 moveInput = moveAction.ReadValue<Vector2>();
            if (moveInput.sqrMagnitude < 0.01f) return;

            Vector3 cameraForward = cam == null ? transform.forward : Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 cameraRight = cam == null ? transform.right : Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
            Vector3 inputDirection = cameraForward * moveInput.y + cameraRight * moveInput.x;
            Vector3 tangentDirection = Vector3.ProjectOnPlane(inputDirection, ropeDirection);
            if (tangentDirection.sqrMagnitude < 0.0001f) return;

            rigidbodyComponent.AddForce(tangentDirection.normalized * (config.swingSteerForce * moveInput.magnitude), ForceMode.Acceleration);
        }

        private bool CheckGeometryOverlap(Vector3 position)
        {
            if (!Physics.CheckSphere(position, 0.3f, config.obstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            int count = Physics.OverlapSphereNonAlloc(position, 0.3f, overlapBuffer, config.obstacleLayerMask, QueryTriggerInteraction.Ignore);
            bool hasObstacleOverlap = false;

            for (int i = 0; i < count; i++)
            {
                Collider col = overlapBuffer[i];
                overlapBuffer[i] = null;

                if (col != null && col.transform != transform && !col.transform.IsChildOf(transform))
                {
                    hasObstacleOverlap = true;
                }
            }

            return hasObstacleOverlap;
        }

        private bool CheckGroundedInSwing()
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            return Physics.SphereCast(origin, 0.45f, Vector3.down, out _, 0.4f, config.obstacleLayerMask, QueryTriggerInteraction.Ignore);
        }

        private Vector3 GetForwardDirection()
        {
            if (cam == null) return transform.forward;
            Vector3 camForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            return camForward.sqrMagnitude > 0.001f ? camForward.normalized : transform.forward;
        }
    }
}

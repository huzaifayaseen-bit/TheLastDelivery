using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastDelivery.Player
{
    [RequireComponent(typeof(PlayerController), typeof(CharacterController), typeof(Rigidbody))]
    [RequireComponent(typeof(LineRenderer), typeof(PlayerInput))]
    public class GrapplingHook : MonoBehaviour
    {
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
        private Collider[] anchorBuffer;

        private bool isAttached;
        private bool attachRequested;
        private bool releaseRequested;
        private Vector3 anchorPoint;
        private Transform anchorTransform;
        private float currentLength;

        public bool IsAttached => isAttached;
        public Vector3 AnchorPoint => anchorPoint;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            rigidbodyComponent = GetComponent<Rigidbody>();
            lineRenderer = GetComponent<LineRenderer>();
            if (playerInput == null) playerInput = GetComponent<PlayerInput>();

            anchorBuffer = new Collider[32];
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

            if (grappleAction == null || jumpAction == null || moveAction == null)
            {
                Debug.LogError("GrapplingHook requires Grapple, Jump, and Move input actions.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (grappleAction == null || jumpAction == null) return;

            grappleAction.performed += OnGrapplePerformed;
            grappleAction.canceled += OnGrappleCanceled;
            jumpAction.performed += OnJumpPerformed;
        }

        private void OnDisable()
        {
            if (grappleAction == null || jumpAction == null) return;

            grappleAction.performed -= OnGrapplePerformed;
            grappleAction.canceled -= OnGrappleCanceled;
            jumpAction.performed -= OnJumpPerformed;
        }

        private void Update()
        {
            if (cam == null && Camera.main != null) cam = Camera.main.transform;

            if (isAttached)
            {
                lineRenderer.SetPosition(0, ropeOrigin.position);
                lineRenderer.SetPosition(1, anchorPoint);
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

            if (releaseRequested || anchorTransform == null || !anchorTransform.gameObject.activeInHierarchy)
            {
                releaseRequested = false;
                Release();
                return;
            }

            anchorPoint = anchorTransform.position;
            ApplySpiderSwingPhysics();
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
            if (isAttached) releaseRequested = true;
        }

        private void TryAttach()
        {
            if (isAttached || !TryFindBestAnchor(out Transform targetTransform, out Vector3 targetPoint)) return;

            anchorTransform = targetTransform;
            anchorPoint = targetPoint;
            float initialDistance = Vector3.Distance(rigidbodyComponent.position, anchorPoint);
            currentLength = Mathf.Clamp(initialDistance * 0.85f, config.minTetherLength, config.maxTetherLength);

            isAttached = true;
            player.SetGrappleTarget(anchorPoint);
            player.EnterSwingMode();

            Vector3 toAnchor = (anchorPoint - rigidbodyComponent.position).normalized;
            Vector3 forwardDir = GetForwardDirection();
            rigidbodyComponent.velocity += (toAnchor * 6f) + (forwardDir * 5f) + (Vector3.up * 3.5f);

            lineRenderer.enabled = true;
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
            anchorPoint = Vector3.zero;
            anchorTransform = null;
            lineRenderer.enabled = false;
            player.SetGrappleTarget(null);
            player.ExitSwingMode(launchVelocity);
        }

        private bool TryFindBestAnchor(out Transform bestTransform, out Vector3 bestPoint)
        {
            bestTransform = null;
            bestPoint = Vector3.zero;
            float bestScore = float.MinValue;

            Vector3 playerPosition = rigidbodyComponent.position;
            Vector3 lookDir = cam != null ? cam.forward : transform.forward;

            int count = Physics.OverlapSphereNonAlloc(
                playerPosition,
                config.maxAnchorRange,
                anchorBuffer,
                config.anchorLayerMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider candidate = anchorBuffer[i];
                if (candidate == null || !candidate.gameObject.activeInHierarchy || !candidate.CompareTag("GrapplePoint")) continue;

                Vector3 candidatePoint = candidate.bounds.center;
                Vector3 toCandidate = candidatePoint - playerPosition;
                float distance = toCandidate.magnitude;
                if (distance <= 0.5f) continue;

                Vector3 dirToCandidate = toCandidate / distance;
                float alignment = Vector3.Dot(lookDir, dirToCandidate);
                if (alignment < 0.05f) continue;

                float normalizedDist = 1f - Mathf.Clamp01(distance / config.maxAnchorRange);
                float score = (alignment * config.viewConeWeight) + (normalizedDist * config.distanceWeight);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTransform = candidate.transform;
                    bestPoint = candidatePoint;
                }
            }

            return bestTransform != null;
        }

        private void ApplySpiderSwingPhysics()
        {
            Vector3 toAnchor = anchorPoint - rigidbodyComponent.position;
            float distance = toAnchor.magnitude;
            if (distance <= Mathf.Epsilon) return;

            Vector3 ropeDirection = toAnchor / distance;

            if (currentLength > config.minTetherLength)
            {
                currentLength = Mathf.MoveTowards(currentLength, config.minTetherLength, config.reelInSpeed * Time.fixedDeltaTime);
            }

            rigidbodyComponent.AddForce(Physics.gravity * config.swingGravityMultiplier, ForceMode.Acceleration);
            rigidbodyComponent.AddForce(ropeDirection * config.webPullForce, ForceMode.Acceleration);

            Vector3 forwardDir = GetForwardDirection();
            Vector3 forwardTangent = Vector3.ProjectOnPlane(forwardDir, ropeDirection);
            if (forwardTangent.sqrMagnitude > 0.001f)
            {
                rigidbodyComponent.AddForce(forwardTangent.normalized * config.forwardSwingDrive, ForceMode.Acceleration);
            }

            if (distance > currentLength)
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
            Vector2 moveInput = moveAction.ReadValue<Vector2>();
            if (moveInput.sqrMagnitude < 0.01f) return;

            Vector3 cameraForward = cam == null ? transform.forward : Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 cameraRight = cam == null ? transform.right : Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
            Vector3 inputDirection = cameraForward * moveInput.y + cameraRight * moveInput.x;
            Vector3 tangentDirection = Vector3.ProjectOnPlane(inputDirection, ropeDirection);
            if (tangentDirection.sqrMagnitude < 0.0001f) return;

            rigidbodyComponent.AddForce(tangentDirection.normalized * (config.swingSteerForce * moveInput.magnitude), ForceMode.Acceleration);
        }

        private Vector3 GetForwardDirection()
        {
            if (cam == null) return transform.forward;
            Vector3 camForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            return camForward.sqrMagnitude > 0.001f ? camForward.normalized : transform.forward;
        }
    }
}
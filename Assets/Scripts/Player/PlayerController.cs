using UnityEngine;
using UnityEngine.InputSystem;
using TheLastDelivery.Interfaces;

namespace TheLastDelivery.Player
{
    [RequireComponent(typeof(CharacterController), typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour, IPlayerTelemetry
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float airSteerMultiplier = 0.65f;
        [SerializeField] private float rotationSpeed = 14f;

        [Header("Jump & Gravity")]
        [SerializeField] private float jumpHeight = 2f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float groundedGravity = -2f;

        [Header("Ground Detection Snapping")]
        [SerializeField] private float groundCheckOffset = 0.15f;
        [SerializeField] private LayerMask groundMask = ~0;

        private CharacterController controller;
        private Rigidbody rigidbodyComponent;
        private CapsuleCollider capsuleCollider;
        private Animator animatorComponent;
        private PlayerInput playerInput;
        private InputAction moveAction;
        private InputAction jumpAction;

        private Vector3 verticalVelocity;
        private Vector3 externalVelocity;
        private Transform cam;
        private bool isGrounded;
        private bool wasGrounded;
        private bool isLandingIsTrigger;
        private CollisionDetectionMode groundedCollisionDetectionMode;
        private Vector3? currentGrapplePoint;

        private int speedHash;
        private int verticalVelocityHash;
        private int isGroundedHash;
        private int isSwingingHash;
        private int isLandingHash;

        public Vector3 Position => transform.position;
        public Vector3 Velocity => IsSwinging ? rigidbodyComponent.velocity : controller.velocity;
        public bool IsGrounded => isGrounded;
        public bool IsSwinging { get; private set; }
        public float CurrentSpeed
        {
            get
            {
                Vector3 currentVelocity = Velocity;
                return new Vector3(currentVelocity.x, 0f, currentVelocity.z).magnitude;
            }
        }

        public Vector3? CurrentGrapplePoint => currentGrapplePoint;
        public bool HasGrappleTarget => currentGrapplePoint.HasValue;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            rigidbodyComponent = GetComponent<Rigidbody>();
            capsuleCollider = GetComponent<CapsuleCollider>();
            if (capsuleCollider != null) capsuleCollider.enabled = false;
            animatorComponent = GetComponentInChildren<Animator>();
            playerInput = GetComponent<PlayerInput>();
            groundedCollisionDetectionMode = rigidbodyComponent.collisionDetectionMode;

            rigidbodyComponent.isKinematic = true;
            rigidbodyComponent.useGravity = false;
            rigidbodyComponent.constraints = RigidbodyConstraints.FreezeRotation;

            speedHash = Animator.StringToHash(AnimatorParameterNames.Speed);
            verticalVelocityHash = Animator.StringToHash(AnimatorParameterNames.VerticalVelocity);
            isGroundedHash = Animator.StringToHash(AnimatorParameterNames.IsGrounded);
            isSwingingHash = Animator.StringToHash(AnimatorParameterNames.IsSwinging);
            isLandingHash = Animator.StringToHash(AnimatorParameterNames.IsLanding);

            if (animatorComponent != null && animatorComponent.runtimeAnimatorController != null)
            {
                foreach (AnimatorControllerParameter param in animatorComponent.parameters)
                {
                    if (param.nameHash == isLandingHash)
                    {
                        isLandingIsTrigger = (param.type == AnimatorControllerParameterType.Trigger);
                        break;
                    }
                }
            }

            if (playerInput != null && playerInput.actions != null)
            {
                moveAction = playerInput.actions.FindAction("Move", false);
                jumpAction = playerInput.actions.FindAction("Jump", false);
            }

            if (Camera.main != null) cam = Camera.main.transform;
        }

        private void Update()
        {
            if (cam == null && Camera.main != null) cam = Camera.main.transform;

            if (!IsSwinging)
            {
                CheckGrounded();
                HandleLocomotionAndJump();
            }
            else
            {
                isGrounded = false;
            }

            UpdateAnimator();
        }

        private void CheckGrounded()
        {
            float radius = controller.radius;
            Vector3 origin = transform.position + controller.center;
            float distance = (controller.height / 2f) - radius + groundCheckOffset;

            bool sphereGrounded = verticalVelocity.y <= 0f &&
                Physics.SphereCast(origin, radius * 0.9f, Vector3.down, out _, distance, groundMask, QueryTriggerInteraction.Ignore);

            isGrounded = controller.isGrounded || sphereGrounded;
        }

        private void HandleLocomotionAndJump()
        {
            Vector2 rawInput = moveAction != null
                ? moveAction.ReadValue<Vector2>()
                : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            Vector3 camForward = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 camRight = cam != null ? Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized : Vector3.right;
            Vector3 moveDir = (camForward * rawInput.y + camRight * rawInput.x);

            float currentSpeed = isGrounded ? moveSpeed : moveSpeed * airSteerMultiplier;
            Vector3 horizontalMove = Vector3.zero;

            if (moveDir.sqrMagnitude >= 0.01f)
            {
                moveDir.Normalize();
                Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                horizontalMove = moveDir * currentSpeed;
            }

            if (externalVelocity.sqrMagnitude > 0.01f)
            {
                horizontalMove += externalVelocity;
                float drag = isGrounded ? 8f : 1.2f;
                externalVelocity = Vector3.Lerp(externalVelocity, Vector3.zero, drag * Time.deltaTime);
                if (externalVelocity.sqrMagnitude <= 0.01f)
                {
                    externalVelocity = Vector3.zero;
                }
            }

            if (isGrounded && verticalVelocity.y < 0f)
            {
                verticalVelocity.y = groundedGravity;
            }

            bool jumpPressed = jumpAction != null ? jumpAction.WasPressedThisFrame() : Input.GetKeyDown(KeyCode.Space);
            if (jumpPressed && isGrounded)
            {
                verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalVelocity.y += gravity * Time.deltaTime;

            Vector3 totalMove = horizontalMove + verticalVelocity;
            controller.Move(totalMove * Time.deltaTime);
        }

        private void UpdateAnimator()
        {
            if (animatorComponent == null) return;

            bool isLanding = !wasGrounded && isGrounded;
            animatorComponent.SetFloat(speedHash, CurrentSpeed);
            animatorComponent.SetFloat(verticalVelocityHash, Velocity.y);
            animatorComponent.SetBool(isGroundedHash, isGrounded);
            animatorComponent.SetBool(isSwingingHash, IsSwinging);
            if (isLandingIsTrigger)
            {
                if (isLanding)
                {
                    animatorComponent.SetTrigger(isLandingHash);
                }
            }
            else
            {
                animatorComponent.SetBool(isLandingHash, isLanding);
            }
            wasGrounded = isGrounded;
        }

        public void LaunchWithVelocity(Vector3 releaseVelocity)
        {
            externalVelocity = new Vector3(releaseVelocity.x, 0f, releaseVelocity.z);
            verticalVelocity.y = releaseVelocity.y;
        }

        public void EnterSwingMode()
        {
            if (IsSwinging) return;

            Vector3 inheritedVelocity = controller.velocity + externalVelocity;
            externalVelocity = Vector3.zero;
            controller.enabled = false;
            if (capsuleCollider != null) capsuleCollider.enabled = true;

            rigidbodyComponent.position = transform.position;
            rigidbodyComponent.isKinematic = false;
            rigidbodyComponent.useGravity = false;
            rigidbodyComponent.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbodyComponent.velocity = inheritedVelocity;
            IsSwinging = true;
            isGrounded = false;
        }

        public void ExitSwingMode(Vector3 launchVelocity)
        {
            if (!IsSwinging) return;

            if (capsuleCollider != null) capsuleCollider.enabled = false;
            rigidbodyComponent.velocity = Vector3.zero;
            rigidbodyComponent.angularVelocity = Vector3.zero;
            rigidbodyComponent.isKinematic = true;
            rigidbodyComponent.collisionDetectionMode = groundedCollisionDetectionMode;
            controller.enabled = true;
            IsSwinging = false;
            LaunchWithVelocity(launchVelocity);
        }

        internal void SetGrappleTarget(Vector3? grapplePoint)
        {
            currentGrapplePoint = grapplePoint;
        }
    }
}
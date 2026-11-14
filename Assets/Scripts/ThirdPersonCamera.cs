using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastDelivery.Player
{
    public class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private float distance = 7.5f;
        [SerializeField] private float height = 1.6f;
        [SerializeField] private float mouseSensitivity = 0.15f;
        [SerializeField] private float followSmoothness = 15f;
        [SerializeField] private float minPitch = -25f;
        [SerializeField] private float maxPitch = 65f;

        private InputAction lookAction;
        private float yaw;
        private float pitch = 12f;

        private void Start()
        {
            if (target == null)
            {
                GameObject playerObj = GameObject.Find("Player");
                if (playerObj != null) target = playerObj.transform;
            }

            if (playerInput == null && target != null)
            {
                playerInput = target.GetComponent<PlayerInput>();
            }

            if (playerInput != null && playerInput.actions != null)
            {
                lookAction = playerInput.actions.FindAction("Look", false);
            }

            if (target != null)
            {
                yaw = target.eulerAngles.y;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector2 lookDelta = lookAction != null
                ? lookAction.ReadValue<Vector2>()
                : new Vector2(Input.GetAxis("Mouse X") * 10f, Input.GetAxis("Mouse Y") * 10f);

            yaw += lookDelta.x * mouseSensitivity;
            pitch -= lookDelta.y * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focusPoint = target.position + Vector3.up * height;
            Vector3 desiredPosition = focusPoint - (rotation * Vector3.forward * distance);

            transform.position = Vector3.Lerp(transform.position, desiredPosition, followSmoothness * Time.deltaTime);
            transform.LookAt(focusPoint);
        }
    }
}
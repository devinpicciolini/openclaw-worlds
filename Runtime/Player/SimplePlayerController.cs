using UnityEngine;

namespace OpenClawWorlds.Player
{
    /// <summary>
    /// Drop-in first-person WASD + mouse-look controller.
    /// Attach to a GameObject — auto-adds <see cref="CharacterController"/>.
    ///
    /// Controls:
    /// - WASD to move, Shift to sprint
    /// - Mouse to look around (cursor locked)
    /// - Space to jump
    /// - Tab opens chat (movement + look auto-blocked while chat is open)
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class SimplePlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Walk speed in units/sec")]
        public float moveSpeed = 5f;
        [Tooltip("Sprint multiplier (hold Shift)")]
        public float sprintMultiplier = 1.8f;
        [Tooltip("Jump height")]
        public float jumpHeight = 1.2f;
        [Tooltip("Gravity applied each frame")]
        public float gravity = -20f;

        [Header("Mouse Look")]
        [Tooltip("Mouse sensitivity")]
        public float lookSensitivity = 2.5f;
        [Tooltip("Minimum vertical angle (look down)")]
        public float minPitch = -60f;
        [Tooltip("Maximum vertical angle (look up)")]
        public float maxPitch = 75f;

        [Header("Camera")]
        [Tooltip("If null, uses Camera.main")]
        public Camera playerCamera;
        [Tooltip("Camera offset from player pivot (eye height)")]
        public Vector3 cameraOffset = new Vector3(0, 1.6f, 0);

        /// <summary>
        /// Set to true to suppress all input (e.g. while a UI is focused).
        /// The built-in chat UI sets this automatically.
        /// When true, the cursor is unlocked so the player can interact with UI.
        /// </summary>
        public static bool InputBlocked
        {
            get => _inputBlocked;
            set
            {
                _inputBlocked = value;
                // Unlock cursor for UI, lock for gameplay
                Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = value;
            }
        }
        static bool _inputBlocked;

        CharacterController cc;
        float yaw;
        float pitch;
        float verticalVelocity;
        bool cameraReady;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (cc == null)
                cc = gameObject.AddComponent<CharacterController>();

            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0, 0.9f, 0);
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.4f;
        }

        void Start()
        {
            // Initialize yaw from current facing
            yaw = transform.eulerAngles.y;
            pitch = 0f;

            // Find camera — try assigned, then Camera.main, then search
            if (playerCamera == null)
                playerCamera = Camera.main;
            if (playerCamera == null)
            {
#if UNITY_2023_1_OR_NEWER
                playerCamera = FindAnyObjectByType<Camera>();
#else
                playerCamera = FindObjectOfType<Camera>();
#endif
            }

            AttachCamera();

            // Lock cursor for FPS gameplay
            if (!_inputBlocked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void AttachCamera()
        {
            if (playerCamera == null) return;

            playerCamera.transform.SetParent(transform);
            playerCamera.transform.localPosition = cameraOffset;
            playerCamera.transform.localRotation = Quaternion.identity;
            cameraReady = true;
            Debug.Log($"[SimplePlayerController] Camera attached: {playerCamera.name}");
        }

        void Update()
        {
            // Retry camera attachment if it wasn't ready at Start
            if (!cameraReady)
            {
                if (playerCamera == null) playerCamera = Camera.main;
                if (playerCamera != null) AttachCamera();
            }

            if (_inputBlocked) return;

            HandleMouseLook();
            HandleMovement();
        }

        void HandleMouseLook()
        {
            // Always-on mouse look when cursor is locked (standard FPS behavior)
            float mx = Input.GetAxis("Mouse X") * lookSensitivity;
            float my = Input.GetAxis("Mouse Y") * lookSensitivity;

            yaw += mx;
            pitch -= my;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            transform.rotation = Quaternion.Euler(0, yaw, 0);

            if (playerCamera != null)
                playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        void HandleMovement()
        {
            // ── Grounded check ──
            bool grounded = cc.isGrounded;

            if (grounded && verticalVelocity < 0)
                verticalVelocity = -2f; // small downward force to stay grounded

            // ── Jump ──
            if (grounded && Input.GetKeyDown(KeyCode.Space))
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            // ── Horizontal movement ──
            float h = Input.GetAxisRaw("Horizontal"); // A/D
            float v = Input.GetAxisRaw("Vertical");   // W/S

            Vector3 dir = transform.right * h + transform.forward * v;
            if (dir.sqrMagnitude > 1f)
                dir.Normalize();

            float speed = moveSpeed;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                speed *= sprintMultiplier;

            // ── Gravity ──
            verticalVelocity += gravity * Time.deltaTime;

            // ── Apply ──
            Vector3 move = dir * speed + Vector3.up * verticalVelocity;
            cc.Move(move * Time.deltaTime);
        }

        void OnDisable()
        {
            // Unlock cursor if controller is disabled
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}

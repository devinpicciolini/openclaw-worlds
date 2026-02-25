using UnityEngine;

namespace OpenClawWorlds.Player
{
    /// <summary>
    /// Drop-in third-person WASD + mouse-orbit controller.
    /// Attach to a GameObject — auto-adds <see cref="CharacterController"/>.
    ///
    /// Controls:
    /// - WASD to move, Shift to sprint
    /// - Mouse to orbit camera around the player
    /// - Scroll wheel to zoom in/out
    /// - Space to jump
    /// - Tab opens chat (movement + look auto-blocked while chat is open)
    /// - E to interact with nearby NPCs
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
        public float minPitch = -10f;
        [Tooltip("Maximum vertical angle (look up)")]
        public float maxPitch = 75f;

        [Header("Camera — Third Person")]
        [Tooltip("If null, uses Camera.main")]
        public Camera playerCamera;
        [Tooltip("Default distance behind the player")]
        public float cameraDistance = 6f;
        [Tooltip("Minimum zoom distance")]
        public float cameraDistanceMin = 2f;
        [Tooltip("Maximum zoom distance")]
        public float cameraDistanceMax = 15f;
        [Tooltip("Height offset above the player pivot")]
        public float cameraHeightOffset = 2f;
        [Tooltip("Scroll wheel zoom speed")]
        public float zoomSpeed = 2f;
        [Tooltip("How fast the camera smooths to target position")]
        public float cameraSmoothSpeed = 10f;

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
        float pitch = 20f; // start slightly looking down at the player
        float verticalVelocity;
        float currentDistance;
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
            currentDistance = cameraDistance;
        }

        void Start()
        {
            // Initialize yaw from current facing
            yaw = transform.eulerAngles.y;

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

            SetupCamera();

            // Lock cursor for gameplay
            if (!_inputBlocked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void SetupCamera()
        {
            if (playerCamera == null) return;

            // Detach camera from any parent so we can position it freely
            playerCamera.transform.SetParent(null);
            cameraReady = true;
            Debug.Log($"[SimplePlayerController] Camera ready (third-person): {playerCamera.name}");
        }

        void Update()
        {
            // Retry camera setup if it wasn't ready at Start
            if (!cameraReady)
            {
                if (playerCamera == null) playerCamera = Camera.main;
                if (playerCamera != null) SetupCamera();
            }

            if (_inputBlocked) return;

            HandleMouseLook();
            HandleMovement();
        }

        void LateUpdate()
        {
            if (!cameraReady || playerCamera == null || _inputBlocked) return;
            UpdateCameraPosition();
        }

        void HandleMouseLook()
        {
            float mx = Input.GetAxis("Mouse X") * lookSensitivity;
            float my = Input.GetAxis("Mouse Y") * lookSensitivity;

            yaw += mx;
            pitch += my; // inverted: mouse up = look down at player
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            // Scroll wheel zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                currentDistance -= scroll * zoomSpeed;
                currentDistance = Mathf.Clamp(currentDistance, cameraDistanceMin, cameraDistanceMax);
            }
        }

        void UpdateCameraPosition()
        {
            // Pivot point: player position + height offset
            Vector3 pivot = transform.position + Vector3.up * cameraHeightOffset;

            // Camera orbits around the pivot
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 offset = rotation * new Vector3(0, 0, -currentDistance);
            Vector3 targetPos = pivot + offset;

            // Smooth camera movement
            playerCamera.transform.position = Vector3.Lerp(
                playerCamera.transform.position, targetPos,
                cameraSmoothSpeed * Time.deltaTime);

            // Always look at the pivot
            playerCamera.transform.LookAt(pivot);
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

            // ── Horizontal movement relative to camera facing ──
            float h = Input.GetAxisRaw("Horizontal"); // A/D
            float v = Input.GetAxisRaw("Vertical");   // W/S

            // Get camera-relative forward/right (flattened to XZ plane)
            Vector3 camForward = playerCamera != null ? playerCamera.transform.forward : transform.forward;
            Vector3 camRight = playerCamera != null ? playerCamera.transform.right : transform.right;
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 dir = camRight * h + camForward * v;
            if (dir.sqrMagnitude > 1f)
                dir.Normalize();

            // Rotate player to face movement direction
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(dir), 10f * Time.deltaTime);

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

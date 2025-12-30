using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(Rigidbody))]
public class LobbyPlayerController : MonoBehaviourPun
{
    [Header("Input System (assign via PlayerInput events or inspector)")]
    [SerializeField] private Vector2 moveInput;
    [SerializeField] private Vector2 lookInput;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float acceleration = 25f;
    [SerializeField] private float maxHorizontalSpeed = 7f;

    [Header("Bump / Push")]
    [SerializeField] private float pushImpulse = 2.5f;

    [Header("Obstacle bumps")]
    [SerializeField] private float obstacleBumpImpulse = 10.0f;
    [SerializeField] private float obstacleBumpMinSpeed = 2.0f;
    [SerializeField] private float obstacleBumpUpwardImpulse = 1.0f;

    // Cooldown to prevent repeated bumps
    [SerializeField] private float obstacleBumpCooldown = 0.2f;
    private float nextObstacleBumpTime;

    private Rigidbody rb;
    private Camera cam;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cam = Camera.main;
    }

    private void Start()
    {
        if (!photonView.IsMine)
        {
            // Remote avatars: keep collider for bumping, but don't simulate local input.
            // (Optional) reduce smoothing.
            rb.interpolation = RigidbodyInterpolation.None;
        }
    }

    // ---- PlayerInput Unity Events (Invoke Unity Events mode) ----
    public void OnMove(InputAction.CallbackContext ctx)
    {
        if (!photonView.IsMine) return;
        moveInput = ctx.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext ctx)
    {
        if (!photonView.IsMine) return;
        lookInput = ctx.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        if (!photonView.IsMine) return;
        if (!cam) cam = Camera.main;
        if (!cam) return;

        // Convert moveInput to world-space relative to camera (XZ plane)
        Vector3 camForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
        Vector3 desiredDir = (camForward * moveInput.y + camRight * moveInput.x);
        if (desiredDir.sqrMagnitude > 1f) desiredDir.Normalize();

        Vector3 currentVel = rb.linearVelocity;
        Vector3 desiredVel = desiredDir * moveSpeed;

        Vector3 velChange = desiredVel - new Vector3(currentVel.x, 0f, currentVel.z);
        Vector3 accelStep = Vector3.ClampMagnitude(velChange, acceleration * Time.fixedDeltaTime);

        rb.AddForce(new Vector3(accelStep.x, 0f, accelStep.z), ForceMode.VelocityChange);

        // Clamp horizontal speed
        Vector3 horiz = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (horiz.magnitude > maxHorizontalSpeed)
        {
            Vector3 clamped = horiz.normalized * maxHorizontalSpeed;
            rb.linearVelocity = new Vector3(clamped.x, rb.linearVelocity.y, clamped.z);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!photonView.IsMine) return;

        // 1) Player-to-player bump (only if the OTHER object has a Rigidbody)
        Rigidbody otherRb = collision.rigidbody;
        if (otherRb != null)
        {
            Vector3 pushDirection = Vector3.ProjectOnPlane((otherRb.position - rb.position), Vector3.up).normalized;
            if (pushDirection.sqrMagnitude > 0.001f)
                otherRb.AddForce(pushDirection * pushImpulse, ForceMode.Impulse);

            return;
        }

        // 2) Obstacle bump (walls usually have NO rigidbody, so we handle them here)
        int obstacleMask = LayerMask.GetMask("Obstacles");
        bool isObstacle = ((1 << collision.gameObject.layer) & obstacleMask) != 0;
        if (!isObstacle) return;

        // Cooldown gate
        if (Time.time < nextObstacleBumpTime) return;
        nextObstacleBumpTime = Time.time + obstacleBumpCooldown;

        float speed = collision.relativeVelocity.magnitude;
        if (speed < obstacleBumpMinSpeed) return;

        // Contact normal points out of the obstacle (toward the player)
        ContactPoint cp = collision.GetContact(0);
        Vector3 awayFromWall = Vector3.ProjectOnPlane(cp.normal, Vector3.up).normalized;
        if (awayFromWall.sqrMagnitude < 0.001f) return;

        // Cancel some into-wall velocity so we actually bounce instead of slide
        Vector3 v = rb.linearVelocity;
        Vector3 intoWall = Vector3.Project(v, -awayFromWall);
        rb.linearVelocity = v - intoWall * 0.6f;

        // Apply bump impulse
        rb.AddForce(awayFromWall * obstacleBumpImpulse, ForceMode.Impulse);

        // Optional tiny hop
        if (obstacleBumpUpwardImpulse > 0f)
            rb.AddForce(Vector3.up * obstacleBumpUpwardImpulse, ForceMode.Impulse);

        Debug.Log($"[BUMP] Hit obstacle '{collision.gameObject.name}' speed={speed:F1}");
    }
}

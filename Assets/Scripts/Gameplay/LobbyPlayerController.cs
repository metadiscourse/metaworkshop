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

        Rigidbody otherRb = collision.rigidbody;
        if (!otherRb) return;

        Vector3 pushDir = Vector3.ProjectOnPlane((otherRb.position - rb.position), Vector3.up).normalized;
        if (pushDir.sqrMagnitude < 0.001f) return;

        otherRb.AddForce(pushDir * pushImpulse, ForceMode.Impulse);
    }
}

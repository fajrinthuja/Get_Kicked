using UnityEngine;

/// <summary>
/// BallController handles all ball physics, sprite-sheet animation,
/// and the visual spin effect when kicked.
///
/// Setup in Unity:
///  1. Create a new GameObject "Ball" with this component.
///  2. Add a Rigidbody2D   (Gravity Scale ~2.5, Collision Detection: Continuous)
///  3. Add a CircleCollider2D
///  4. Add a SpriteRenderer — assign any frame from ball.all_ as the default sprite.
///  5. Set the sprite sheet to Multiple, slice it 8 columns × 8 rows
///     (Unity will auto-slice the 64 frames: ball.all__0 … ball.all__63).
///  6. Drag all 64 sprites into the BallSprites array in the Inspector.
///  7. Assign a PhysicsMaterial2D with Bounciness ~0.4 and Friction ~0.3.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class BallController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector fields
    // ─────────────────────────────────────────────────────────────────────────
    [Header("Sprites (drag all 64 frames in order)")]
    [SerializeField] private Sprite[] ballSprites;

    [Header("Kick Force")]
    [Tooltip("Horizontal speed applied on kick (m/s)")]
    [SerializeField] private float kickHorizontalForce = 6f;
    [Tooltip("Vertical (upward) speed applied on kick (m/s)")]
    [SerializeField] private float kickVerticalForce   = 5f;
    [Tooltip("Extra random angle spread (degrees) for a natural feel")]
    [SerializeField] private float kickAngleSpread     = 8f;

    [Header("Spin / Roll")]
    [Tooltip("Frames per second of sprite animation while moving")]
    [SerializeField] private float rollingFPS          = 16f;
    [Tooltip("How fast the visual spin decays after landing")]
    [SerializeField] private float spinDecay           = 3f;

    [Header("Physics Drag")]
    [Tooltip("Linear drag applied while the ball is in the air")]
    [SerializeField] private float airDrag             = 0.1f;
    [Tooltip("Linear drag applied when the ball is on the ground")]
    [SerializeField] private float groundDrag           = 3f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer;
    [Tooltip("How far below the ball centre to raycast for the ground")]
    [SerializeField] private float groundCheckDist     = 0.42f;

    // ─────────────────────────────────────────────────────────────────────────
    //  Private state
    // ─────────────────────────────────────────────────────────────────────────
    private Rigidbody2D   rb;
    private SpriteRenderer sr;
    private CircleCollider2D col;

    private float  frameTimer     = 0f;
    private float  currentFrame   = 0f;
    private float  visualSpinRate = 0f;   // signed frames/sec for spin direction
    private bool   isGrounded     = false;

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        rb  = GetComponent<Rigidbody2D>();
        sr  = GetComponent<SpriteRenderer>();
        col = GetComponent<CircleCollider2D>();

        if (rb != null)
        {
            rb.interpolation            = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode   = CollisionDetectionMode2D.Continuous;
        }
    }

    private void Update()
    {
        CheckGrounded();
        UpdateDrag();
        UpdateSpriteAnimation();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API — called by PlayerController
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Apply a kick impulse to the ball.
    /// <param name="facingRight">Which direction the player is facing.</param>
    /// </summary>
    public void ReceiveKick(bool facingRight)
    {
        if (rb == null) return;

        // Wipe existing velocity for a clean, snappy kick
        rb.linearVelocity = Vector2.zero;

        // Direction + slight random vertical angle
        float dir        = facingRight ? 1f : -1f;
        float angleJitter = Random.Range(-kickAngleSpread * 0.5f, kickAngleSpread * 0.5f);
        float rad         = angleJitter * Mathf.Deg2Rad;

        float vx = dir  * (kickHorizontalForce * Mathf.Cos(rad) - kickVerticalForce * Mathf.Sin(rad) * 0.15f);
        float vy = kickVerticalForce * Mathf.Cos(rad) + Mathf.Abs(kickHorizontalForce * Mathf.Sin(rad) * 0.15f);

        rb.AddForce(new Vector2(vx, vy), ForceMode2D.Impulse);

        // Spin: fast in direction of travel, decays over time naturally via drag
        visualSpinRate = dir * rollingFPS * 2.5f;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void CheckGrounded()
    {
        if (col == null) return;
        Vector2 origin = (Vector2)transform.position;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDist, groundLayer);
        isGrounded = hit.collider != null;
    }

    private void UpdateDrag()
    {
        if (rb == null) return;
        rb.linearDamping = isGrounded ? groundDrag : airDrag;
    }

    private void UpdateSpriteAnimation()
    {
        if (ballSprites == null || ballSprites.Length == 0) return;
        if (sr == null) return;

        float speed = rb != null ? rb.linearVelocity.magnitude : 0f;

        // ── While airborne: use visualSpinRate (fast, set on kick) ──
        // ── While grounded: derive spin from horizontal velocity    ──
        float effectiveSpinRate;
        if (isGrounded)
        {
            float hVel = rb != null ? rb.linearVelocity.x : 0f;
            effectiveSpinRate = hVel * (rollingFPS / 3f);   // tune rolling vs fps

            // Decay the airborne spin toward ground-roll rate
            visualSpinRate = Mathf.MoveTowards(visualSpinRate, effectiveSpinRate,
                                               spinDecay * Time.deltaTime * rollingFPS);
        }
        else
        {
            // In air: gradually slow the spin rate (Magnus effect feel)
            visualSpinRate = Mathf.MoveTowards(visualSpinRate, 0f,
                                               spinDecay * 0.4f * Time.deltaTime * rollingFPS);
            effectiveSpinRate = visualSpinRate;
        }

        // Only animate if actually moving
        if (Mathf.Abs(speed) < 0.05f && Mathf.Abs(effectiveSpinRate) < 0.1f) return;

        frameTimer += Mathf.Abs(effectiveSpinRate) * Time.deltaTime;

        // frameTimer accumulates; advance integer frames
        while (frameTimer >= 1f)
        {
            currentFrame = (currentFrame + Mathf.Sign(effectiveSpinRate) + ballSprites.Length)
                           % ballSprites.Length;
            frameTimer -= 1f;
        }

        int idx = Mathf.Clamp((int)currentFrame, 0, ballSprites.Length - 1);
        sr.sprite = ballSprites[idx];
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Debug gizmo
    // ─────────────────────────────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * groundCheckDist);
    }
}

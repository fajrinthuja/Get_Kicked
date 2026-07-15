using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 10f;
    [Tooltip("Layer mask for ground detection")]
    [SerializeField] private LayerMask groundLayer;
    [Tooltip("Offset below the player to check for ground")]
    [SerializeField] private Vector2 groundCheckOffset = new Vector2(0f, -0.35f);
    [SerializeField] private float groundCheckRadius = 0.15f;

    [Header("Kick Settings")]
    [Tooltip("How close the ball must be for the kick to connect (world units)")]
    [SerializeField] private float kickReach   = 1.4f;
    [Tooltip("Delay in seconds after Space is pressed before the kick force is applied.\n" +
             "Match this to the frame in your Kick animation where the foot connects.")]
    [SerializeField] private float kickHitDelay = 0.2f;

    [Header("Ball Reference")]
    [Tooltip("Drag the Ball GameObject here, or leave empty to auto-find by tag 'Ball'")]
    [SerializeField] private BallController ball;

    [Header("Components")]
    [SerializeField] private Rigidbody2D     rb;
    [SerializeField] private Animator        animator;
    [SerializeField] private SpriteRenderer  spriteRenderer;

    private float horizontalInput  = 0f;
    private bool  kickTriggered    = false;
    private bool  jumpTriggered    = false;
    private bool  isGrounded       = false;
    private bool  facingRight      = true;
    private float kickHitTimer     = -1f;   // countdown; fires ball when it hits 0

    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (rb             == null) rb             = GetComponent<Rigidbody2D>();
        if (animator       == null) animator       = GetComponent<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        if (rb             == null) Debug.LogWarning("PlayerController: Rigidbody2D is missing!",   this);
        if (animator       == null) Debug.LogWarning("PlayerController: Animator is missing!",       this);
        if (spriteRenderer == null) Debug.LogWarning("PlayerController: SpriteRenderer is missing!", this);

        // Auto-find ball by tag if not assigned
        if (ball == null)
        {
            GameObject ballObj = GameObject.FindGameObjectWithTag("Ball");
            if (ballObj != null) ball = ballObj.GetComponent<BallController>();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void Update()
    {
        ReadInput();
        CheckGround();
        FlipSprite();
        UpdateAnimator();
        TickKickHit();
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        // Horizontal movement
        rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, rb.linearVelocity.y);

        // Jump: apply vertical impulse
        if (jumpTriggered && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f); // reset Y before impulse
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            jumpTriggered = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Input
    // ─────────────────────────────────────────────────────────────────────────
    private void ReadInput()
    {
        kickTriggered = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            horizontalInput = 0f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                horizontalInput = 1f;
            else if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                horizontalInput = -1f;

            // Jump on Up Arrow or W key
            if (Keyboard.current.upArrowKey.wasPressedThisFrame ||
                Keyboard.current.wKey.wasPressedThisFrame)
            {
                jumpTriggered = true;
            }

            if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
            {
                kickTriggered = true;
            }
            return;
        }
#endif
        horizontalInput = Input.GetAxisRaw("Horizontal");

        // Jump on Up Arrow or W key (legacy input)
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            jumpTriggered = true;

        if (Input.GetButtonDown("Fire1") || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            kickTriggered = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Ground check
    // ─────────────────────────────────────────────────────────────────────────
    private void CheckGround()
    {
        Vector2 checkPos = (Vector2)transform.position + groundCheckOffset;
        isGrounded = Physics2D.OverlapCircle(checkPos, groundCheckRadius, groundLayer);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Sprite flip
    // ─────────────────────────────────────────────────────────────────────────
    private void FlipSprite()
    {
        if (spriteRenderer == null || horizontalInput == 0f) return;
        facingRight          = horizontalInput > 0f;
        spriteRenderer.flipX = !facingRight;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Animator
    // ─────────────────────────────────────────────────────────────────────────
    private void UpdateAnimator()
    {
        if (animator == null) return;

        animator.SetFloat("Speed", Mathf.Abs(horizontalInput));
        animator.SetBool("IsGrounded", isGrounded);

        if (jumpTriggered && isGrounded)
        {
            animator.SetTrigger("Jump");
        }

        if (kickTriggered)
        {
            animator.SetTrigger("Kick");

            // Start the delayed hit window only if ball is close enough
            if (IsBallInReach())
                kickHitTimer = kickHitDelay;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Kick hit timing
    // ─────────────────────────────────────────────────────────────────────────
    private void TickKickHit()
    {
        if (kickHitTimer < 0f) return;

        kickHitTimer -= Time.deltaTime;
        if (kickHitTimer <= 0f)
        {
            kickHitTimer = -1f;

            // Double-check range at the moment of impact (ball may have rolled away)
            if (IsBallInReach() && ball != null)
                ball.ReceiveKick(facingRight);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private bool IsBallInReach()
    {
        if (ball == null) return false;
        float dist = Vector2.Distance(transform.position, ball.transform.position);
        return dist <= kickReach;
    }

    // Draw kick reach radius in the Scene view
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, kickReach);

        // Draw ground check area
        Gizmos.color = Color.green;
        Vector2 checkPos = (Vector2)transform.position + groundCheckOffset;
        Gizmos.DrawWireSphere(checkPos, groundCheckRadius);
    }
}

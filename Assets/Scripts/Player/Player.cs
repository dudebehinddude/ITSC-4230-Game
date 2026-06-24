using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Player : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float jumpForce = 200f;
    [Tooltip("Caps downward speed so fast falls stay stable against tile collisions.")]
    [SerializeField] private float maxFallSpeed = 24f;

    [Header("Ground / Wall Detection")]
    [Tooltip("Layers treated as solid floor and walls. Defaults to everything; the player's own collider and triggers are always ignored.")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [Tooltip("How far below the feet to probe for ground.")]
    [SerializeField] private float groundCheckDistance = 0.08f;
    [Tooltip("How far out from the sides to probe for walls.")]
    [SerializeField] private float wallCheckDistance = 0.08f;

    [Header("Jump Feel")]
    [Tooltip("Releasing jump while still rising cuts upward velocity by this fraction. Lower = shorter taps, higher = more 'hold to jump higher'.")]
    [Range(0f, 1f)]
    [SerializeField] private float jumpCutMultiplier = 0.75f;
    [Tooltip("Seconds after takeoff before releasing jump is allowed to shorten the jump.")]
    [SerializeField] private float minJumpHoldTime = 0.12f;
    [Tooltip("Grace period after leaving a ledge during which you can still jump.")]
    [SerializeField] private float coyoteTime = 0.1f;
    [Tooltip("How early a jump press is remembered before landing.")]
    [SerializeField] private float jumpBufferTime = 0.1f;

    [Header("Wall Slide")]
    [Tooltip("Downward acceleration while sliding against a wall. Keep below gravity so it eases into a slide instead of snapping to a fixed speed.")]
    [SerializeField] private float wallSlideAcceleration = 14f;
    [Tooltip("Maximum downward speed while wall sliding (a slow descent, slower than a free fall).")]
    [SerializeField] private float maxWallSlideSpeed = 4f;

    [Header("Wall Jump")]
    [Tooltip("Horizontal push away from the wall when wall jumping.")]
    [SerializeField] private float wallJumpHorizontalForce = 12f;
    [Tooltip("Upward push when wall jumping.")]
    [SerializeField] private float wallJumpVerticalForce = 14f;
    [Tooltip("Seconds after a wall jump during which horizontal input is ignored, so the push isn't cancelled instantly.")]
    [SerializeField] private float wallJumpControlLock = 0.15f;

    private Rigidbody2D rb;
    private Collider2D col;

    private InputAction moveAction;
    private InputAction jumpAction;

    private float horizontalInput;
    private bool jumpReleased;
    private bool jumpCutQueued;

    private float lastGroundedTime = -1f;
    private float lastJumpPressedTime = -1f;
    private float jumpStartedTime = -1f;
    private float controlLockUntil = -1f;
    private int wallDirection; // -1 = wall on left, 1 = wall on right, 0 = none
    private bool canCutJump; // only ground jumps can be cut short
    private bool wallSliding;
    private float wallSlideVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        moveAction = InputSystem.actions.FindAction("Move");
        jumpAction = InputSystem.actions.FindAction("Jump");
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        jumpAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        jumpAction?.Disable();
    }

    private void Update()
    {
        horizontalInput = moveAction != null ? moveAction.ReadValue<Vector2>().x : 0f;

        if (jumpAction != null && jumpAction.WasPressedThisFrame())
        {
            lastJumpPressedTime = Time.time;
        }

        if (jumpAction != null && jumpAction.WasReleasedThisFrame())
        {
            jumpReleased = true;
        }
    }

    private void FixedUpdate()
    {
        bool grounded = IsGrounded();
        wallDirection = GetWallDirection();

        if (grounded)
        {
            lastGroundedTime = Time.time;
        }

        HandleJump(grounded);
        HandleHorizontalMovement();
        HandleWallSlide(grounded);
        CapFallSpeed();

        if (jumpReleased)
        {
            jumpCutQueued = true;
        }

        // Cut jump short if the player releases the jump button early (variable height jumps)
        if (canCutJump
            && jumpCutQueued
            && rb.linearVelocity.y > 0f
            && Time.time - jumpStartedTime >= minJumpHoldTime)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
            canCutJump = false;
            jumpCutQueued = false;
        }

        if (rb.linearVelocity.y <= 0f)
        {
            canCutJump = false;
            jumpCutQueued = false;
        }

        jumpReleased = false;
    }

    private void HandleJump(bool grounded)
    {
        bool jumpBuffered = Time.time - lastJumpPressedTime <= jumpBufferTime;
        if (!jumpBuffered)
        {
            return;
        }

        bool canGroundJump = Time.time - lastGroundedTime <= coyoteTime;

        if (canGroundJump)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            canCutJump = true;
            jumpCutQueued = false;
            jumpStartedTime = Time.time;
            ConsumeJump();
        }
        else if (!grounded && wallDirection != 0)
        {
            // Push up and away from the wall. Fixed arc: not affected by hold time.
            float pushX = -wallDirection * wallJumpHorizontalForce;
            rb.linearVelocity = new Vector2(pushX, wallJumpVerticalForce);
            controlLockUntil = Time.time + wallJumpControlLock;
            canCutJump = false;
            jumpCutQueued = false;
            ConsumeJump();
        }
    }

    private void ConsumeJump()
    {
        // Prevent the same press from re-triggering on the next frame.
        lastJumpPressedTime = -999f;
        lastGroundedTime = -999f;
    }

    private void HandleHorizontalMovement()
    {
        // Keep the wall-jump arc intact for a brief window.
        if (Time.time < controlLockUntil)
        {
            return;
        }

        float walk = horizontalInput;

        if (WouldHitWall(walk))
        {
            walk = 0f;
        }

        rb.linearVelocity = new Vector2(walk * moveSpeed, rb.linearVelocity.y);
    }

    private void CapFallSpeed()
    {
        if (rb.linearVelocity.y < -maxFallSpeed)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
        }
    }

    // Drives a gentle downward slide while pressing into a wall in the air. Collider
    // friction would otherwise pin the player to the wall, so we override the vertical
    // velocity from our own accumulator. Ramping it up (instead of clamping to a fixed
    // speed) keeps it from feeling like riding a moving platform.
    private void HandleWallSlide(bool grounded)
    {
        bool pushingIntoWall = wallDirection != 0
            && Mathf.Abs(horizontalInput) > 0.01f
            && (int)Mathf.Sign(horizontalInput) == wallDirection;

        bool shouldSlide = !grounded
            && pushingIntoWall
            && Time.time >= controlLockUntil // don't fight a fresh wall jump
            && rb.linearVelocity.y <= 0.01f; // only while descending, never kill a rise

        if (!shouldSlide)
        {
            wallSliding = false;
            return;
        }

        if (!wallSliding)
        {
            wallSlideVelocity = Mathf.Min(0f, rb.linearVelocity.y);
            wallSliding = true;
        }

        wallSlideVelocity = Mathf.Max(wallSlideVelocity - wallSlideAcceleration * Time.fixedDeltaTime, -maxWallSlideSpeed);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, wallSlideVelocity);
    }

    private bool IsGrounded()
    {
        Bounds b = col.bounds;
        Vector2 size = new Vector2(b.size.x * 0.9f, groundCheckDistance);
        Vector2 center = new Vector2(b.center.x, b.min.y - groundCheckDistance * 0.5f);
        return OverlapSolid(center, size);
    }

    private int GetWallDirection()
    {
        Bounds b = col.bounds;
        Vector2 size = new Vector2(wallCheckDistance, b.size.y * 0.9f);

        Vector2 rightCenter = new Vector2(b.max.x + wallCheckDistance * 0.5f, b.center.y);
        if (OverlapSolid(rightCenter, size))
        {
            return 1;
        }

        Vector2 leftCenter = new Vector2(b.min.x - wallCheckDistance * 0.5f, b.center.y);
        if (OverlapSolid(leftCenter, size))
        {
            return -1;
        }

        return 0;
    }

    private bool WouldHitWall(float walk)
    {
        if (Mathf.Abs(walk) < 0.01f || wallDirection == 0)
        {
            return false;
        }

        return (int)Mathf.Sign(walk) == wallDirection;
    }

    private bool OverlapSolid(Vector2 center, Vector2 size)
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, groundLayer);
        foreach (Collider2D hit in hits)
        {
            if (hit != null && hit != col && !hit.isTrigger)
            {
                return true;
            }
        }

        return false;
    }

    public void Teleport(Vector2 position)
    {
        Vector2 resolved = ResolvePositionOnGround(position);
        rb.position = resolved;
        rb.linearVelocity = Vector2.zero;
        transform.position = new Vector3(resolved.x, resolved.y, transform.position.z);
    }

    private Vector2 ResolvePositionOnGround(Vector2 position)
    {
        Bounds bounds = col.bounds;
        float footOffset = transform.position.y - bounds.min.y;
        var origin = new Vector2(position.x, position.y + 2f);
        const float probeDistance = 12f;

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, probeDistance, groundLayer);
        float bestY = float.NegativeInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D hit = hits[i];
            if (hit.collider == null || hit.collider == col || hit.collider.isTrigger)
            {
                continue;
            }

            float candidateY = hit.point.y + footOffset + 0.02f;
            if (candidateY <= position.y + 0.75f && candidateY > bestY)
            {
                bestY = candidateY;
            }
        }

        if (float.IsNegativeInfinity(bestY))
        {
            return position;
        }

        return new Vector2(position.x, bestY);
    }

    public void ApplyCutsceneForce(Vector2 force, float controlLockSeconds)
    {
        rb.AddForce(force, ForceMode2D.Impulse);
        if (controlLockSeconds > 0f)
        {
            controlLockUntil = Mathf.Max(controlLockUntil, Time.time + controlLockSeconds);
        }
    }
}

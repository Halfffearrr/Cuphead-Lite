using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    public Rigidbody2D rb;
    public Animator animator;
    public AudioSource audioSource;
    public Collider2D playerCollider2D;
    public PlayerStateMachine stateMachine;
    public PlayerGroundState groundState;
    public PlayerCrouchState crouchState;
    public PlayerJumpState jumpState;
    public Transform playerTransform;

    public float speed = 8f;
    public float jumpForce = 15f;
    public float bulletSpeed = 10f;
    public float superBulletSpeed = 15f;
    public float bulletLife = 2f;
    public float fireRate = 0.2f;
    public int pointsNeededForSuper = 100;
    public int maxSuperCount = 3;
    private int maxPoints;
    private int points;

public bool facingRight = true;
    public bool isGrounded = false;
    public bool isDucking = false;
    public bool canFire = true; 
    public bool shotFiredThisPress = false; 
    public bool isPoweredUp = false; 
    public float powerUpDuration = 5f;
    public bool isAimLocked = false;

    public Transform duckShootPoint;
    private Vector3 defaultFirePointPos;

    private float duckTime = 0f;
    public bool inDuckIdle = false;

    public Transform firePoint;
    public GameObject bulletPrefab;
    public GameObject superBulletPrefab;
    public GameObject fireDustPrefab;
    public GameObject superFireDustPrefab;
    public GameObject jumpSmokePrefab;
    public LayerMask groundLayer;
    public Vector2 groundCheckSize = new Vector2(0.5f, 0.1f);
    public Transform groundCheckPoint;
    public Transform groundCheck;

    private string currentAimTrigger = "";
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        playerCollider2D = GetComponent<Collider2D>();
        groundState = new PlayerGroundState(this);
        crouchState = new PlayerCrouchState(this);
        jumpState = new PlayerJumpState(this);
        stateMachine = new PlayerStateMachine(groundState);
        maxPoints = pointsNeededForSuper * maxSuperCount;
        playerTransform = transform;

        if (firePoint != null)
            defaultFirePointPos = firePoint.localPosition;

        Health health = GetComponent<Health>();
        if (health == null)
        {
            health = gameObject.AddComponent<Health>();
            health.healthCollider2D = playerCollider2D;
            health.health = 3;
            health.invulnerabilityTime = 6f;
        }
    }

    private bool isDashing = false;
    private Coroutine dashCoroutine = null;
    private void Update()
    {
        isGrounded = CheckGrounded();
        animator.SetBool("isGrounded", isGrounded);
        stateMachine.Update();

        // Dash
        if (Input.GetKeyDown(KeyCode.H) && !isDashing)
        {
            if (dashCoroutine != null) StopCoroutine(dashCoroutine);
            dashCoroutine = StartCoroutine(Dash());
        }

        // Aim lock logic
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isAimLocked = true;
            animator.Play("Idle");
        }
        if (Input.GetKeyUp(KeyCode.Space))
        {
            isAimLocked = false;
            animator.SetBool("Straight", false);
            if (firePoint != null)
                firePoint.localPosition = defaultFirePointPos;
        }

        // 方向动画和firePoint逻辑
        if (isAimLocked)
        {
            UpdateAimAnimationAndFirePoint();

            // 允许左右切换朝向
            float moveInput = Input.GetAxisRaw("Horizontal");
            if (moveInput > 0 && !facingRight)
            {
                Flip();
            }
            else if (moveInput < 0 && facingRight)
            {
                Flip();
            }

            // 锁定模式下，禁止跳跃/下蹲/dash/移动
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            animator.SetFloat("Speed", 0);
            isDucking = false;
            animator.SetBool("isDucking", false);
        }
        else
        {
            // 不在锁定模式下
            if (!Input.GetKey(KeyCode.Space))
            {
                if (Input.GetKeyDown(KeyCode.S))
                {
                    animator.Play("Duck");
                }
                isDucking = Input.GetKey(KeyCode.S);
                animator.SetBool("isDucking", isDucking);

                if (Input.GetKeyDown(KeyCode.K) && isGrounded)
                {
                    Jump();
                }

                HandleMovement();
            }
            else
            {
                // Space被按住时，禁止下蹲、移动、跳跃
                isDucking = false;
                animator.SetBool("isDucking", false);
                animator.SetFloat("Speed", 0);
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
        }

        // Shoot logic
        if (Input.GetKeyDown(KeyCode.J))
        {
            if (isAimLocked)
            {
                Vector2 shootDir = GetShootDirection();
                FireBulletInDirection(shootDir);
            }
            else
            {
                if (isDucking)
                {
                    animator.ResetTrigger("DuckShoot");
                    animator.SetTrigger("DuckShoot");
                }
                else
                {
                    animator.ResetTrigger("Aim");
                    animator.SetTrigger("Aim");
                }
                FireBullet();
            }
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            PowerUp();
        }
        if (Input.GetKeyDown(KeyCode.L))
        {
            animator.SetTrigger("super");
            ShootSuperBullet();
        }
    }
    // Updates aim animation and firePoint position based on aiming direction (5-way: straight, up, down, diagonal up, diagonal down)
    private IEnumerator Dash()
{
    isDashing = true;
    animator.SetTrigger("dash");

    float dashDuration = 0.2f;
    float dashDistance = 3.5f;
    float dashSpeed = dashDistance / dashDuration;

    float originalGravity = rb.gravityScale;
    rb.gravityScale = 0;

    Vector2 dashVelocity = new Vector2(facingRight ? dashSpeed : -dashSpeed, 0);
    rb.linearVelocity = dashVelocity;

    yield return new WaitForSeconds(dashDuration);

    rb.gravityScale = originalGravity;
    isDashing = false;
}
    // 重置所有方向动画参数
    private void ResetAllAimBools()
    {
        animator.SetBool("Straight", false);
        animator.SetBool("Up", false);
        animator.SetBool("Down", false);
        animator.SetBool("DiagonalUp", false);
        animator.SetBool("DiagonalDown", false);
    }

    // 支持多方向长按检测和动画控制的 UpdateAimAnimationAndFirePoint
    private void UpdateAimAnimationAndFirePoint()
    {
        // 检测各方向长按
        bool holdA = Input.GetKey(KeyCode.A);
        bool holdD = Input.GetKey(KeyCode.D);
        bool holdW = Input.GetKey(KeyCode.W);
        bool holdS = Input.GetKey(KeyCode.S);
        // 斜上：W+D 或 W+A，斜下：S+D 或 S+A
        bool holdDiagonalUp = holdW && (holdD || holdA);
        bool holdDiagonalDown = holdS && (holdD || holdA);
        // 仅上（非斜上）
        bool holdUp = holdW && !holdDiagonalUp;
        // 仅下（非斜下）
        bool holdDown = holdS && !holdDiagonalDown;
        // 仅直线（A 或 D, 非斜上/斜下/上/下）
        bool holdStraight = (holdD || holdA) && !holdDiagonalUp && !holdDiagonalDown && !holdUp && !holdDown;

        // 优化动画参数，只激活一个方向
        ResetAllAimBools();
        if (holdDiagonalUp)
            animator.SetBool("DiagonalUp", true);
        else if (holdDiagonalDown)
            animator.SetBool("DiagonalDown", true);
        else if (holdUp)
            animator.SetBool("Up", true);
        else if (holdDown)
            animator.SetBool("Down", true);
        else if (holdStraight)
            animator.SetBool("Straight", true);

        // firePoint 偏移
        if (firePoint != null)
        {
            if (isAimLocked)
            {
                Vector3 offset = Vector3.zero;
                if (holdDiagonalUp)
                    offset = new Vector3(0.7f, 0.5f, 0f);
                else if (holdDiagonalDown)
                    offset = new Vector3(0.7f, -0.5f, 0f);
                else if (holdUp)
                    offset = new Vector3(0f, 0.6f, 0f);
                else if (holdDown)
                    offset = new Vector3(0f, -0.5f, 0f);
                else if (holdStraight)
                    offset = new Vector3(0f, 0f, 0f);
                // 其他未按方向，offset保持zero
                if (!facingRight)
                {
                    offset.x = -offset.x;
                }
                firePoint.localPosition = offset;
            }
            else
            {
                firePoint.localPosition = defaultFirePointPos;
            }
        }
    }
    // Fire a bullet in a specific direction, with firePoint offset
    public void FireBulletInDirection(Vector2 direction)
    {
        if (canFire && firePoint != null && bulletPrefab != null)
        {
            canFire = false;
            Invoke(nameof(ResetFire), fireRate);

            // Offset fire point is already set by UpdateAimAnimationAndFirePoint
            Vector3 spawnPos = firePoint.position;
            GameObject bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
            Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
            bulletRb.linearVelocity = direction.normalized * bulletSpeed;

            float scaleX = facingRight ? 1 : -1;
            bullet.transform.localScale = new Vector3(scaleX, 1, 1);

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            bullet.transform.rotation = Quaternion.Euler(0, 0, angle);

            SpriteRenderer sr = bullet.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.flipX = scaleX < 0;
            }
            if (fireDustPrefab != null && firePoint != null)
            {
                GameObject fireDust = Instantiate(fireDustPrefab, firePoint.position, Quaternion.identity);
                ParticleSystem ps = fireDust.GetComponent<ParticleSystem>();
                if (ps != null) ps.Play();
                Destroy(fireDust, 0.2f);
            }
            bullet.tag = "PlayerBullet";
            Destroy(bullet, bulletLife);
        }
    }

    // 基于Y轴位置判断是否接触地面
    private bool CheckGrounded()
    {
        return playerTransform.position.y <= -5.78f;
    }

    // 可视化groundCheck检测点
    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.black;
            Gizmos.DrawWireSphere(groundCheck.position, 0.2f);
        }
    }

    public void HandleMovement()
    {
        float moveInput = Input.GetAxisRaw("Horizontal");
        rb.linearVelocity = new Vector2(moveInput * speed, rb.linearVelocity.y);

        if (moveInput > 0 && !facingRight)
        {
            Flip();
        }
        else if (moveInput < 0 && facingRight)
        {
            Flip();
        }

        animator.SetFloat("Speed", Mathf.Abs(moveInput));
    }

    public void Flip()
    {
        facingRight = !facingRight;
        transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
    }

    public void Duck(bool duck)
    {
        isDucking = duck;
        if (isDucking)
        {
            if (duckTime >= 0.5f)
            {
                animator.SetBool("isDucking", true);
            }
            stateMachine.ChangeState(crouchState);
        }
        else if (isGrounded)
        {
            animator.SetBool("isDucking", false);
            stateMachine.ChangeState(groundState);
        }
    }

    public void Jump()
    {
        if (isGrounded)
        {
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            isGrounded = false;
            animator.SetTrigger("jump");
            stateMachine.ChangeState(jumpState);
        }
    }

    public void FireBullet()
    {
        // 使用 duckShootPoint 发射下蹲子弹和 dust
        Transform shootPoint = isDucking && duckShootPoint != null ? duckShootPoint : firePoint;
        if (canFire && shootPoint != null && bulletPrefab != null)
        {
            canFire = false;
            Invoke(nameof(ResetFire), fireRate);

            float direction = Mathf.Sign(transform.localScale.x);
            Vector2 fireDir = new Vector2(direction, 0);

            GameObject bullet = Instantiate(bulletPrefab, shootPoint.position, Quaternion.identity);
            Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
            bulletRb.linearVelocity = fireDir.normalized * bulletSpeed;

            bullet.transform.localScale = new Vector3(direction, 1, 1);

            float angle = Mathf.Atan2(fireDir.y, fireDir.x) * Mathf.Rad2Deg;
            bullet.transform.rotation = Quaternion.Euler(0, 0, angle);

            SpriteRenderer sr = bullet.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.flipX = direction < 0;
            }
            if (fireDustPrefab != null && shootPoint != null)
            {
                GameObject fireDust = Instantiate(fireDustPrefab, shootPoint.position, Quaternion.identity);
                ParticleSystem ps = fireDust.GetComponent<ParticleSystem>();
                if (ps != null) ps.Play();
                Destroy(fireDust, 0.2f);
            }
            bullet.tag = "PlayerBullet";
            Destroy(bullet, bulletLife);
        }
    }

    public void ShootSuperBullet()
    {
        if (canFire && firePoint != null && superBulletPrefab != null)
        {
            canFire = false;
            Invoke(nameof(ResetFire), fireRate);

            // 播放 super 动画已在 Update() 中处理

            float direction = Mathf.Sign(transform.localScale.x);
            Vector2 fireDir = new Vector2(direction, 0);

            GameObject bullet = Instantiate(superBulletPrefab, firePoint.position, Quaternion.identity);
            Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
            bulletRb.linearVelocity = fireDir.normalized * superBulletSpeed;

            bullet.transform.localScale = new Vector3(direction, 1, 1);

            float angle = Mathf.Atan2(fireDir.y, fireDir.x) * Mathf.Rad2Deg;
            bullet.transform.rotation = Quaternion.Euler(0, 0, angle);

            SpriteRenderer sr = bullet.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.flipX = direction < 0;
            }

            bullet.tag = "PlayerBullet";
            Destroy(bullet, bulletLife);

            // 使用 superFireDust 特效
            if (superFireDustPrefab != null && firePoint != null)
            {
                GameObject fireDust = Instantiate(superFireDustPrefab, firePoint.position, Quaternion.identity);
                ParticleSystem ps = fireDust.GetComponent<ParticleSystem>();
                if (ps != null) ps.Play();
                Destroy(fireDust, 0.6f);
            }
        }
    }

    private Vector2 GetShootDirection()
    {
        // Recognize aiming direction for 8-way shooting
        bool up = Input.GetKey(KeyCode.W);
        bool down = Input.GetKey(KeyCode.S);
        bool left = Input.GetKey(KeyCode.A);
        bool right = Input.GetKey(KeyCode.D);
        Vector2 dir = Vector2.zero;
        if (up) dir += Vector2.up;
        if (down) dir += Vector2.down;
        if (right) dir += Vector2.right;
        if (left) dir += Vector2.left;
        if (dir == Vector2.zero)
        {
            dir = facingRight ? Vector2.right : Vector2.left;
        }
        return dir.normalized;
    }

    private void PowerUp()
    {
        if (!isPoweredUp)
        {
            isPoweredUp = true;
            bulletSpeed *= 1.5f;
            Invoke(nameof(ResetPowerUp), powerUpDuration);
            animator.SetTrigger("powerUp");
            Debug.Log("PowerUp activated!");
        }
    }

    private void ResetPowerUp()
    {
        isPoweredUp = false;
        bulletSpeed /= 1.5f;
        Debug.Log("PowerUp ended.");
    }

    private void ResetFire()
    {
        canFire = true;
    }

    private void OnCollisionExit2D(Collision2D other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            isGrounded = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("EnemyBullet"))
        {
            Health health = GetComponent<Health>();
            if (health != null)
            {
                health.takeDamage();
            }
            Destroy(other.gameObject);
        }
    }

    public void Shoot()
    {
        FireBullet();
    }
}

public class PlayerStateMachine
{
    private PlayerState currentState;

    public PlayerStateMachine(PlayerState initialState)
    {
        currentState = initialState;
        currentState.Enter();
    }

    public void Update()
    {
        currentState.Update();
    }

    public void ChangeState(PlayerState newState)
    {
        currentState.Exit();
        currentState = newState;
        currentState.Enter();
    }
}

public abstract class PlayerState
{
    protected PlayerController player;

    protected PlayerState(PlayerController player)
    {
        this.player = player;
    }

    public virtual void Enter() { }
    public virtual void Update() { }
    public virtual void Exit() { }
}

public class PlayerGroundState : PlayerState
{
    public PlayerGroundState(PlayerController player) : base(player) { }

    public override void Update()
    {
        if (!player.isGrounded)
        {
            player.stateMachine.ChangeState(player.jumpState);
        }
        else if (player.isDucking)
        {
            player.stateMachine.ChangeState(player.crouchState);
        }
    }
}

public class PlayerCrouchState : PlayerState
{
    public PlayerCrouchState(PlayerController player) : base(player) { }

    public override void Enter()
    {
        player.animator.SetBool("isDucking", true);
    }

    public override void Update()
    {
        if (Input.GetKeyDown(KeyCode.J) && player.inDuckIdle)
        {
            player.Shoot();
            player.animator.SetTrigger("DuckShoot");
        }
        if (!Input.GetKey(KeyCode.S) && player.isGrounded)
        {
            player.stateMachine.ChangeState(player.groundState);
        }
    }

    public override void Exit()
    {
        player.animator.SetBool("isDucking", false);
    }
}

public class PlayerJumpState : PlayerState
{
    private GameObject jumpSmoke;

    public PlayerJumpState(PlayerController player) : base(player) { }

    public override void Enter()
    {
        player.animator.SetTrigger("jump");
        if (player.jumpSmokePrefab != null)
        {
            jumpSmoke = Object.Instantiate(player.jumpSmokePrefab, player.groundCheckPoint.position, Quaternion.identity);
        }
    }

    public override void Update()
    {
        if (player.isGrounded && player.rb.linearVelocity.y <= 0)
        {
            player.stateMachine.ChangeState(player.groundState);
        }
    }

    public override void Exit()
    {
        if (jumpSmoke != null)
        {
            ParticleSystem ps = jumpSmoke.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
            }
            Object.Destroy(jumpSmoke, 1f);
        }
    }
}
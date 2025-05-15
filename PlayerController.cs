using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour
{
    public Health health;
    // Audio clips for sound effects
    public AudioClip jumpClip;
    public AudioClip landClip;
    public AudioClip shootClip;
    public AudioClip superClip;
    public AudioClip dashClip;
    public AudioClip deathClip;
    private bool canUseSuperBullet = true;
    private bool infiniteSuperMode = false;
    private int normalBulletDamage = 1;
    private int superBulletDamage = 100;
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

    private bool isDead = false;

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

        health = GetComponent<Health>();
        if (health == null)
        {
            health = gameObject.AddComponent<Health>();
            health.healthCollider2D = playerCollider2D;
            health.health = 1;
            health.invulnerabilityTime = 2f;
        }
    }

    private bool isDashing = false;
    private Coroutine dashCoroutine = null;

    private void Update()
    {
        // 死亡检测和动画
        if (health != null && health.health <= 0 && !isDead)
        {
            isDead = true;
            animator.SetTrigger("Die");
            if (deathClip != null && audioSource != null)
                audioSource.PlayOneShot(deathClip);
            StartCoroutine(HandleDeath());
            return;
        }

        isGrounded = CheckGrounded();
        animator.SetBool("isGrounded", isGrounded);
        stateMachine.Update();

        if (Input.GetKey(KeyCode.C) && Input.GetKeyDown(KeyCode.Y))
        {
            Debug.Log("启动");
            EnableInfiniteSuper();
        }

        if (Input.GetKeyDown(KeyCode.H) && !isDashing)
        {
            if (dashCoroutine != null) StopCoroutine(dashCoroutine);
            dashCoroutine = StartCoroutine(Dash());
        }

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

        if (isAimLocked)
        {
            UpdateAimAnimationAndFirePoint();

            float moveInput = Input.GetAxisRaw("Horizontal");
            if (moveInput > 0 && !facingRight)
            {
                Flip();
            }
            else if (moveInput < 0 && facingRight)
            {
                Flip();
            }

            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            animator.SetFloat("Speed", 0);
            isDucking = false;
            animator.SetBool("isDucking", false);
        }
        else
        {
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
                isDucking = false;
                animator.SetBool("isDucking", false);
                animator.SetFloat("Speed", 0);
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
        }

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
        if (Input.GetKeyDown(KeyCode.L) && isPoweredUp)
        {
            ShootSuperBullet();
        }
    }

    private void EnableInfiniteSuper()
    {
        canUseSuperBullet = true;
        isPoweredUp = true;
        infiniteSuperMode = true;
        Debug.Log("Super bullets are now infinite! Can fire: " + canFire);
    }

    private IEnumerator Dash()
    {
        if (dashClip != null) audioSource.PlayOneShot(dashClip);
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

    private void ResetAllAimBools()
    {
        animator.SetBool("Straight", false);
        animator.SetBool("Up", false);
        animator.SetBool("Down", false);
        animator.SetBool("DiagonalUp", false);
        animator.SetBool("DiagonalDown", false);
    }

    private void UpdateAimAnimationAndFirePoint()
    {
        bool holdA = Input.GetKey(KeyCode.A);
        bool holdD = Input.GetKey(KeyCode.D);
        bool holdW = Input.GetKey(KeyCode.W);
        bool holdS = Input.GetKey(KeyCode.S);
        bool holdDiagonalUp = holdW && (holdD || holdA);
        bool holdDiagonalDown = holdS && (holdD || holdA);
        bool holdUp = holdW && !holdDiagonalUp;
        bool holdDown = holdS && !holdDiagonalDown;
        bool holdStraight = (holdD || holdA) && !holdDiagonalUp && !holdDiagonalDown && !holdUp && !holdDown;

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

    public void FireBulletInDirection(Vector2 direction)
    {
        if (canFire && firePoint != null && bulletPrefab != null)
        {
            if (shootClip != null) audioSource.PlayOneShot(shootClip);
            canFire = false;
            Invoke(nameof(ResetFire), fireRate);

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
            Bullet bulletScript = bullet.GetComponent<Bullet>();
            if (bulletScript != null)
            {
                bulletScript.damage = normalBulletDamage;
            }
            Destroy(bullet, bulletLife);
        }
    }

    private bool CheckGrounded()
    {
        return playerTransform.position.y <= -5.78f;
    }

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
            if (jumpClip != null) audioSource.PlayOneShot(jumpClip);
            isGrounded = false;
            animator.SetTrigger("jump");
            stateMachine.ChangeState(jumpState);
        }
    }

    public void FireBullet()
    {
        Transform shootPoint = isDucking && duckShootPoint != null ? duckShootPoint : firePoint;
        if (canFire && shootPoint != null && bulletPrefab != null)
        {
            if (shootClip != null) audioSource.PlayOneShot(shootClip);
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
            Bullet bulletScript = bullet.GetComponent<Bullet>();
            if (bulletScript != null)
            {
                bulletScript.damage = normalBulletDamage;
            }
            Destroy(bullet, bulletLife);
        }
    }

    public void ShootSuperBullet()
    {
        if (canFire && (isPoweredUp || infiniteSuperMode) && firePoint != null && superBulletPrefab != null)
        {
            if (superClip != null) audioSource.PlayOneShot(superClip);
            animator.SetTrigger("super");

            canFire = false;
            if (!infiniteSuperMode) isPoweredUp = false;
            Invoke(nameof(ResetFire), fireRate);

            float direction = Mathf.Sign(transform.localScale.x);
            Vector2 fireDir = new Vector2(direction, 0);

            GameObject bullet = Instantiate(superBulletPrefab, firePoint.position, Quaternion.identity);
            Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
            bulletRb.linearVelocity = fireDir.normalized * superBulletSpeed;

            bullet.transform.localScale = new Vector3(direction, 1, 1);
            bullet.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(fireDir.y, fireDir.x) * Mathf.Rad2Deg);

            SpriteRenderer sr = bullet.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.flipX = direction < 0;
            }

            bullet.tag = "PlayerBullet";

            Bullet bulletScript = bullet.GetComponent<Bullet>();
            if (bulletScript != null)
            {
                bulletScript.damage = superBulletDamage;
            }

            Destroy(bullet, bulletLife);

            if (superFireDustPrefab != null && firePoint != null)
            {
                GameObject fireDust = Instantiate(superFireDustPrefab, firePoint.position, Quaternion.identity);
                ParticleSystem ps = fireDust.GetComponent<ParticleSystem>();
                if (ps != null) ps.Play();
                Destroy(fireDust, 0.6f);
            }
        }
        else
        {
            Debug.Log("Cannot shoot Super Bullet, conditions not met.");
        }
    }

    private Vector2 GetShootDirection()
    {
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
        if (!isPoweredUp && canUseSuperBullet)
        {
            isPoweredUp = true;
            canUseSuperBullet = false;
            bulletSpeed *= 1.5f;
            animator.SetTrigger("powerUp");
            Debug.Log("PowerUp activated!");

            StartCoroutine(ResetPowerUpAfterDelay(powerUpDuration));
            StartCoroutine(ResetSuperBulletCooldown(5f));
        }
    }

    private IEnumerator ResetPowerUpAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        isPoweredUp = false;
        bulletSpeed /= 1.5f;
        Debug.Log("PowerUp ended.");
    }

    private IEnumerator ResetSuperBulletCooldown(float cooldown)
    {
        yield return new WaitForSeconds(cooldown);
        canUseSuperBullet = true;
        Debug.Log("SuperBullet ready again.");
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

    // Play landing sound when colliding with ground
    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            isGrounded = true;
            if (landClip != null) audioSource.PlayOneShot(landClip);
        }
    }

    public void Shoot()
    {
        FireBullet();
    }

    private IEnumerator HandleDeath()
    {
        yield return new WaitForSeconds(3f); // 等待死亡动画和音效播放完成
        Destroy(gameObject);
        SceneManager.LoadScene("MainMenu");
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
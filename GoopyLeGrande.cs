using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
public class GoopyLeGrande : MonoBehaviour
{
    // 组件
    // 地面检测
    public Transform groundCheck;
    public LayerMask groundLayer;
    private Rigidbody2D rigidbody2D;
    private Animator animator;
    private AudioSource audioSource;
    private SpriteRenderer spriteRenderer;

    // 玩家引用
    public Transform player;

    // 移动和跳跃参数
    public float jumpForce = 20.0f;
    public float jumpUpForce = 24.0f;
    public float jumpInterval = 0.2f;
    private float lastJumpTime;
    private bool facingRight = true;

    // 攻击参数
    public Transform punchPoint1; // 拳击点1
    public Transform punchPoint2; // 拳击点2
    public GameObject punchEffectPrefab; // 拳击效果预制体
    public float punchRate = 0.5f; // 拳击频率
    public float punchDistance = 1f; // 拳击触发距离
    public GameObject attack1;
    public GameObject attack2;

    // 音效
    public AudioClip introSound; // 入场音效
    public AudioClip punchSound; // 拳击音效
    public AudioClip hurtSound; // 受伤音效
    public AudioClip death1Sound; // 第一阶段死亡音效
    public AudioClip death2Sound; // 第二阶段死亡音效

    // 闪光效果
    public float flashDuration = 0.1f;

    // 形态控制
    public enum Phase { Phase1, Phase2, Phase3 }
    public Phase currentPhase = Phase.Phase1;
    [Header("调试 - 当前生命值")]
    public int health = 200; // 每阶段健康值（可调整）
    private bool isActive = true; // 是否可移动/攻击
    public bool isInPhase2 = false;

    // 墓碑 sprite
    public Sprite tombstoneSprite;

    private bool canFire = true;  // 允许射击

    private void Start()
    {
        // 初始化组件
        rigidbody2D = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // 配置 Rigidbody2D
        rigidbody2D.mass = 100f;
        rigidbody2D.linearDamping = 10f;
        rigidbody2D.sharedMaterial = new PhysicsMaterial2D { friction = 1f, bounciness = 0f };
        rigidbody2D.constraints = RigidbodyConstraints2D.FreezeRotation;

        // 初始化时间
        lastJumpTime = Time.time;

        // 播放入场动画和音效
        animator.SetTrigger("intro");
        animator.SetBool("airUp", false);
        animator.SetBool("airDown", false);
        animator.SetBool("attackUp", false);
        animator.SetBool("attackDown", false);
        animator.SetBool("isAttacking", false);
        playIntroSound();

        if (attack1 != null)
            attack1.SetActive(false);
        if (attack2 != null)
            attack2.SetActive(false);
    }

    private bool isGrounded()
    {
        return Physics2D.OverlapCircle(groundCheck.position, 0.2f, groundLayer);
    }

    private void Update()
    {
        if (player == null || !isActive) return;

        float clampedX = Mathf.Clamp(transform.position.x, -8f, 8f);
        transform.position = new Vector3(clampedX, transform.position.y, transform.position.z);

        
        // 判断敌人朝向
        Vector2 directionToPlayer = player.position - transform.position;
        if (Mathf.Abs(directionToPlayer.x) > 0.2f) 
        {
            if (directionToPlayer.x < 0 && facingRight) 
            {
                facingRight = false;
                Flip();
            }
            else if (directionToPlayer.x > 0 && !facingRight) 
            {
                facingRight = true;
                Flip();
            }
        }

        // 判断玩家状态并设置动画
    //Animator playerAnim = player.GetComponent<Animator>();
    //    if (playerAnim != null)
    //    {
     //       if (playerAnim.GetBool("isDucking"))
     //       {
     //           animator.SetBool(currentPhase == Phase.Phase1 ? "airDown" : "attackDown", true);
       //         animator.SetBool(currentPhase == Phase.Phase1 ? "airUp" : "attackUp", false);
       //         return;
      //      }
       //     else if (playerAnim.GetCurrentAnimatorStateInfo(0).IsName("jump"))
       //     {
       //         animator.SetBool(currentPhase == Phase.Phase1 ? "airUp" : "attackUp", true);
       //         animator.SetBool(currentPhase == Phase.Phase1 ? "airDown" : "attackDown", false);
       //         if (currentPhase == Phase.Phase1)
       //         {
       //             animator.SetTrigger("upDownTransition");
       //         }
       //         return;
       //     }
       //     else
         //   {
        //        animator.SetBool(currentPhase == Phase.Phase1 ? "airDown" : "attackDown", false);
         //       animator.SetBool(currentPhase == Phase.Phase1 ? "airUp" : "attackUp", false);
         //   }
     //   }
        // 设置 isGrounded Animator 参数
        animator.SetBool("isGrounded", isGrounded());

        // 如果在地面上，自动跳跃
        if (isGrounded())  // 在地面时才跳跃
        {
            animator.SetBool("airUp", false);
            animator.SetBool("airDown", false);
            Jump();  // 触发跳跃逻辑
        }
        else
        {
            // 空中状态动画
            animator.SetBool("airUp", true); // 跳跃时显示空中动画
            animator.SetBool("airDown", false);
        }

        // 攻击动作
        float distanceToPlayer = Mathf.Abs(directionToPlayer.x);
        if (distanceToPlayer <= punchDistance)
        {
            StartAttack();
        }

        if (currentPhase == Phase.Phase3 && !isActive && Input.GetKeyDown(KeyCode.Return))
        {
            SceneManager.LoadScene("Win");
        }
    }

    // 翻转敌人朝向
    private void Flip()
    {
        // 翻转
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (facingRight ? -1 : 1);
        transform.localScale = scale;
    }

    // 判断是否在地面
    private bool IsGrounded()
    {
        if (groundCheck == null) return false;
        return Physics2D.OverlapCircle(groundCheck.position, 0.2f, groundLayer);
    }

    // 添加跳跃行为
    private void Jump()  // 这里控制跳跃逻辑
    {
        if (Time.time - lastJumpTime >= jumpInterval && isGrounded())  // 控制跳跃间隔
        {
            // 增强跳跃力和水平速度
            rigidbody2D.linearVelocity = new Vector2(facingRight ? 10f : -10f, jumpUpForce * 1.8f);
            lastJumpTime = Time.time;
            animator.SetTrigger(currentPhase == Phase.Phase1 ? "jump1" : "jump2");  // 设置动画
        }
    }

    // 开始攻击
    public void StartAttack()
    {
        animator.SetBool("isAttacking", true);
        audioSource.PlayOneShot(punchSound);
        if (attack1 != null)
            StartCoroutine(EnableAttackWhenAnimation("PunchDown", attack1));

        if (attack2 != null)
            StartCoroutine(EnableAttackWhenAnimation("AttackDown", attack2));

        Invoke("StopAttack", punchRate);
    }

    // 停止攻击
    public void StopAttack()
    {
        animator.SetBool("isAttacking", false);
        if (attack1 != null)
            attack1.SetActive(false);
        if (attack2 != null)
            attack2.SetActive(false);
    }

    // 用于记录本次攻击已受伤的玩家
    private HashSet<GameObject> damagedPlayers = new HashSet<GameObject>();

    // 触发拳击效果
    public void TriggerPunch()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(punchPoint1.position, 1f);
        damagedPlayers.Clear(); // 每次攻击前清空
        foreach (Collider2D hit in hits)
        {
            if (hit.CompareTag("Player") && !damagedPlayers.Contains(hit.gameObject))
            {
                var health = hit.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    health.takeDamage(1);
                    damagedPlayers.Add(hit.gameObject);
                }
            }
        }

        if (punchPoint1 != null)
            punchPoint1.GetComponent<PunchSpawn>().SpawnPunch(punchRate);
        if (punchPoint2 != null)
            punchPoint2.GetComponent<PunchSpawn>().SpawnPunch(punchRate);
    }

    // 停止拳击
    public void StopPunch()
    {
        if (punchPoint1 != null)
            punchPoint1.GetComponent<PunchSpawn>().StopSpawning();
        if (punchPoint2 != null)
            punchPoint2.GetComponent<PunchSpawn>().StopSpawning();
    }

    // 音效播放
    public void playIntroSound()
    {
        audioSource.PlayOneShot(introSound);
    }

    public void playPunchSound()
    {
        audioSource.PlayOneShot(punchSound);
    }

    public void playHurtSound()
    {
        audioSource.PlayOneShot(hurtSound);
    }

    public void playDeath1Sound()
    {
        audioSource.PlayOneShot(death1Sound);
    }

    public void playDeath2Sound()
    {
        audioSource.PlayOneShot(death2Sound);
    }

    // 无敌标志
    private bool isInvulnerable = false;

    // 受到伤害
    public void TakeDamage(int damage)
    {
        if (isInvulnerable) return; // 如果在无敌帧则不受伤

        health -= damage;
        StartCoroutine(FlashDamage());
        StartCoroutine(TemporaryInvulnerability(flashDuration)); // 无敌和闪烁同步
        if (currentPhase == Phase.Phase2)
        {
            animator.SetTrigger("hurt");
        }
        playHurtSound();

        if (health <= 0)
        {
            if (currentPhase == Phase.Phase1)
            {
                animator.SetTrigger("death1");
                currentPhase = Phase.Phase2;
                health = 500;  // 第二阶段的生命值
                EnterPhase2(); // 进入第二阶段
            }
            else if (currentPhase == Phase.Phase2)
            {
                animator.SetTrigger("death");
                currentPhase = Phase.Phase3;
                health = 6; // 设置墓碑阶段血量
                EnterTombstonePhase();
            }
            else if (currentPhase == Phase.Phase3)
            {
                StartCoroutine(LoadWinAfterDelay(2f));
                //Destroy(gameObject); // 墓碑形态销毁
            }
        }
    }

    // 添加 EnterPhase2 方法
    public void EnterPhase2()
    {
        // 第二阶段逻辑：例如血量重置，动画切换，状态更新等
        currentPhase = Phase.Phase2;
        health = 500;  // 第二阶段的生命值
        isInPhase2 = true;  // 标记当前处于第二阶段

        // 播放第二阶段的进入动画
        animator.SetTrigger("Phase2Enter");  // 假设你有一个 Phase2 的进入动画

        // 恢复透明度
        spriteRenderer.color = new Color(1f, 1f, 1f, 1f);  // 不透明

        // 触发 Phase2 的动画
        animator.SetBool("attackUp", true);  // 设置攻击动画
        animator.SetBool("attackDown", false);  // 设置攻击向下动画（可以根据需要调节）
        animator.SetTrigger("hurt");  // 设置受伤动画，确保在 Phase2 时有此触发
        animator.SetTrigger("death2");  // 设置第二阶段死亡动画

        // 初始无敌1秒
        StartCoroutine(TemporaryInvulnerability(1f));
    }

    // 进入墓碑形态
    private void EnterTombstonePhase()
    {
        isActive = false;
        rigidbody2D.linearVelocity = Vector2.zero;
        rigidbody2D.gravityScale = 0;
        rigidbody2D.constraints = RigidbodyConstraints2D.FreezeAll;
        spriteRenderer.sprite = tombstoneSprite;
        animator.enabled = false;
        StopPunch();
        // 墓碑阶段生命值已在TakeDamage里设置为6
    }

    // 闪烁受伤效果
    private IEnumerator FlashDamage()
    {
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = new Color(1f, 1f, 1f, 0.3f);  // 设置半透明
        yield return new WaitForSeconds(flashDuration);
        spriteRenderer.color = originalColor;
    }

    // 碰撞检测
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            // Ignore collision to allow overlap
            Physics2D.IgnoreCollision(GetComponent<Collider2D>(), collision, true);
        }

        if (collision.CompareTag("PlayerBullet"))
        {
            Bullet bullet = collision.GetComponent<Bullet>();
            if (bullet != null)
            {
                TakeDamage(bullet.damage);
            }
            Destroy(collision.gameObject);
        }
    }
    // 可选：调试显示地面检测范围
    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, 0.2f);
        }
    }
    // 延迟启用 attack1
    private void EnableAttack1()
    {
        if (attack1 != null)
            attack1.SetActive(true);
    }
    private void EnableAttack2()
    {
        if (attack2 != null)
            attack2.SetActive(true);
    }
    // 临时无敌协程
    private IEnumerator TemporaryInvulnerability(float duration)
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(duration);
        isInvulnerable = false;
    }

    // 确保超级子弹伤害为100
    public void ShootSuperBullet(Transform firePoint, GameObject superBulletPrefab, float superBulletSpeed, float bulletLife, float fireRate, ref bool isPoweredUp)
    {
        if (canFire && isPoweredUp && firePoint != null && superBulletPrefab != null)
        {
            canFire = false;
            isPoweredUp = false;
            Invoke(nameof(ResetFire), fireRate);

            float direction = Mathf.Sign(transform.localScale.x);
            Vector2 fireDir = new Vector2(direction, 0);

            GameObject bullet = Instantiate(superBulletPrefab, firePoint.position, Quaternion.identity);
            Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
            bulletRb.linearVelocity = fireDir.normalized * superBulletSpeed;

            bullet.transform.localScale = new Vector3(direction, 1, 1);
            bullet.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(fireDir.y, fireDir.x) * Mathf.Rad2Deg);
            bullet.tag = "PlayerBullet";

            // 设置超级子弹伤害为100
            Bullet bulletScript = bullet.GetComponent<Bullet>();
            if (bulletScript != null)
            {
                bulletScript.damage = 100;
            }

            Destroy(bullet, bulletLife);
        }
    }

    private void ResetFire()
    {
        canFire = true;  // 重新启用攻击
    }

    // 协程：在动画状态为指定名称时启用攻击对象
    private IEnumerator EnableAttackWhenAnimation(string animationName, GameObject attackObject)
    {
        yield return new WaitForSeconds(2f);
        if (animator.GetCurrentAnimatorStateInfo(0).IsName(animationName))
            attackObject.SetActive(true);
    }
    // 在墓碑阶段延迟加载胜利场景
    private IEnumerator LoadWinAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene("Win");
    }
}
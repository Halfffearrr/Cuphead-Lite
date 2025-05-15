using UnityEngine.Events;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
     public int maxHealth = 3; 
    private int currentHealth;
    private SpriteRenderer spriteRenderer;
    public float flashDuration = 0.1f;
    public HealthUI healthUI;
    public UnityEvent<int> onHealthChanged = new UnityEvent<int>();

    void Start()
    {
        currentHealth = maxHealth;
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (healthUI != null)
            healthUI.update(currentHealth);
        onHealthChanged.Invoke(currentHealth);
    }

    // 敌人调用此函数来扣血
    public void takeDamage(int damage)
    {
        currentHealth -= damage;
        if (healthUI != null)
            healthUI.update(currentHealth);
        StartCoroutine(FlashDamage());
        onHealthChanged.Invoke(currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("Player Died!");
        // 你可以在这里添加死亡动画、场景重载等
        gameObject.SetActive(false);
    }

    // 被攻击后闪烁
    private System.Collections.IEnumerator FlashDamage()
    {
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = new Color(1, 0, 0, 0.5f);
        yield return new WaitForSeconds(flashDuration);
        spriteRenderer.color = originalColor;
    }

    // 获取当前生命值（用于UI）
    public int GetHealth()
    {
        return currentHealth;
    }
}

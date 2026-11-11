using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small world-space health bar that appears above an enemy when it is hurt.
/// Added automatically by Enemy - no setup needed.
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    private Enemy enemy;
    private GameObject barObject;
    private UIBar bar;
    private CanvasGroup group;
    private Transform camT;
    private float height;
    private float showTimer;
    private int lastHealth;

    public void Init(Enemy owner, float heightOffset)
    {
        enemy = owner;
        height = heightOffset;
        lastHealth = owner.health;

        barObject = new GameObject("EnemyHealthBar", typeof(RectTransform));

        Canvas canvas = barObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        RectTransform rt = barObject.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100f, 14f);
        rt.localScale = Vector3.one * 0.01f;

        group = barObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        bar = UIFactory.CreateBar(
            rt, "Bar",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(100f, 14f),
            new Color(0f, 0f, 0f, 0.65f),
            new Color(0.9f, 0.2f, 0.2f, 1f), 2f);

        bar.Set(enemy.HealthNormalized);
        UpdatePose();
    }

    private void LateUpdate()
    {
        if (enemy == null || barObject == null)
        {
            Cleanup();
            return;
        }

        if (enemy.IsDead)
        {
            Cleanup();
            return;
        }

        if (enemy.health != lastHealth)
        {
            lastHealth = enemy.health;
            showTimer = 3f;
            bar.Set(enemy.HealthNormalized);
        }

        showTimer -= Time.deltaTime;
        group.alpha = Mathf.Clamp01(showTimer);

        UpdatePose();
    }

    private void UpdatePose()
    {
        barObject.transform.position = enemy.transform.position + Vector3.up * height;

        if (camT == null && Camera.main != null)
            camT = Camera.main.transform;

        if (camT != null)
            barObject.transform.rotation = camT.rotation;
    }

    private void Cleanup()
    {
        if (barObject != null)
            Destroy(barObject);

        Destroy(this);
    }

    private void OnDestroy()
    {
        if (barObject != null)
            Destroy(barObject);
    }
}

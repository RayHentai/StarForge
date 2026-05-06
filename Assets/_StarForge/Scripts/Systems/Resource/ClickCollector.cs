// Assets/_StarForge/Scripts/Systems/Resource/ClickCollector.cs
// 职责：玩家点击星球表面，触发资源采集
// 挂载位置：挂在星球地面的 GameObject 上，需要有 Collider2D 组件

using UnityEngine;
using TMPro; // 需要在 Package Manager 中安装 TextMeshPro

public class ClickCollector : MonoBehaviour
{
    [Header("采集设置")]
    [SerializeField] private string resourceId = "ore";    // 采集的资源类型
    [SerializeField] private float collectAmount = 1f;     // 每次点击采集量
    [SerializeField] private float clickCooldown = 0.2f;   // 点击冷却（防止刷屏）

    [Header("视觉反馈")]
    [SerializeField] private GameObject floatingTextPrefab; // 飘字预制体（可选）
    [SerializeField] private Transform floatingTextSpawnPos; // 飘字生成位置

    [Header("点击特效")]
    [SerializeField] private GameObject clickEffectPrefab;     // 可选；不填则代码生成
    [SerializeField] private Color clickEffectColor = Color.white;
    [SerializeField] private float clickEffectStartScale = 0.3f;
    [SerializeField] private float clickEffectEndScale = 1f;
    [SerializeField] private float clickEffectDuration = 0.35f;
    [SerializeField] private int clickEffectSortingOrder = 100;

    private float lastClickTime;
    private Camera mainCamera;

    private static Sprite _cachedCircleSprite;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        // 检测鼠标左键点击
        if (!Input.GetMouseButtonDown(0)) return;

        // 冷却检查
        if (Time.time - lastClickTime < clickCooldown) return;

        // 射线检测：点击位置是否命中这个 GameObject
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        worldPos.z = 0f;
        var hit = Physics2D.Raycast(worldPos, Vector2.zero);

        if (hit.collider == null || hit.collider.gameObject != gameObject) return;

        // 命中：采集资源
        Collect(worldPos);
    }

    void Collect(Vector3 worldPos)
    {
        lastClickTime = Time.time;
        ResourceManager.Instance.Add(resourceId, collectAmount);

        SpawnClickEffect(worldPos);

        // 显示飘字（如果设置了预制体）
        if (floatingTextPrefab != null)
            ShowFloatingText($"+{collectAmount} {ResourceManager.Instance.GetDisplayName(resourceId)}");

        // 播放动画（简单缩放反馈）
        StopAllCoroutines();
        StartCoroutine(BounceEffect());
    }

    void SpawnClickEffect(Vector3 worldPos)
    {
        GameObject obj;
        SpriteRenderer sr;

        if (clickEffectPrefab != null)
        {
            obj = Instantiate(clickEffectPrefab, worldPos, Quaternion.identity);
            sr = obj.GetComponent<SpriteRenderer>();
        }
        else
        {
            obj = new GameObject("ClickEffect");
            obj.transform.position = worldPos;
            sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = GetOrCreateCircleSprite();
            sr.sortingOrder = clickEffectSortingOrder;
        }

        if (sr != null) sr.color = clickEffectColor;

        var fader = obj.GetComponent<ClickEffectFader>() ?? obj.AddComponent<ClickEffectFader>();
        fader.duration = clickEffectDuration;
        fader.startScale = clickEffectStartScale;
        fader.endScale = clickEffectEndScale;
        fader.startColor = clickEffectColor;
    }

    static Sprite GetOrCreateCircleSprite()
    {
        if (_cachedCircleSprite != null) return _cachedCircleSprite;
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float r = size * 0.5f;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - r + 0.5f, dy = y - r + 0.5f;
            float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy)); // 1px 软边
            pixels[y * size + x] = new Color(1f, 1f, 1f, a);
        }

        tex.SetPixels(pixels);
        tex.Apply();
        _cachedCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _cachedCircleSprite;
    }

    void ShowFloatingText(string text)
    {
        Vector3 spawnPos = floatingTextSpawnPos != null
            ? floatingTextSpawnPos.position
            : transform.position + Vector3.up;

        var obj = Instantiate(floatingTextPrefab, spawnPos, Quaternion.identity);

        // 如果飘字对象上有 TMP 组件，设置文字
        var tmp = obj.GetComponentInChildren<TextMeshPro>();
        if (tmp != null) tmp.text = text;

        Destroy(obj, 1.5f); // 1.5秒后销毁
    }

    System.Collections.IEnumerator BounceEffect()
    {
        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = originalScale * 1.15f;

        // 缩放到目标
        float elapsed = 0f;
        while (elapsed < 0.08f)
        {
            transform.localScale = Vector3.Lerp(originalScale, targetScale, elapsed / 0.08f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 弹回原始
        elapsed = 0f;
        while (elapsed < 0.1f)
        {
            transform.localScale = Vector3.Lerp(targetScale, originalScale, elapsed / 0.1f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localScale = originalScale;
    }
}

/// <summary>
/// 点击圆圈特效：在自身 GameObject 上推进缩放与淡出，避免被 ClickCollector.StopAllCoroutines 打断。
/// </summary>
public class ClickEffectFader : MonoBehaviour
{
    public float duration = 0.35f;
    public float startScale = 0.3f;
    public float endScale = 1f;
    public Color startColor = Color.white;

    private SpriteRenderer _sr;
    private float _elapsed;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        transform.localScale = Vector3.one * startScale;
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / duration);
        float ease = 1f - (1f - t) * (1f - t); // EaseOutQuad
        transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, ease);
        if (_sr != null)
        {
            var c = startColor;
            c.a = startColor.a * (1f - t);
            _sr.color = c;
        }

        if (t >= 1f) Destroy(gameObject);
    }
}

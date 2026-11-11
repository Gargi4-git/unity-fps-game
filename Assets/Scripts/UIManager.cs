using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Builds and drives the whole UI from code:
/// crosshair, hit markers, health bar, ammo counter, reload bar,
/// enemies / score / timer, pickup prompt, messages, damage vignette,
/// pause menu (sensitivity + volume), game-over and victory screens.
///
/// Keeps the original API (UIManager.Instance, HitUI, InstantiateHitUI).
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Tooltip("Optional: your own hit overlay prefab. If empty, a built-in red vignette is used.")]
    public GameObject HitUI;

    public Color accent = new Color(0.30f, 0.80f, 1f, 1f);

    // ---- runtime references ----
    private Canvas canvas;
    private GameObject canvasObject;
    private GameObject hudRoot;
    private GameObject crosshairRoot;
    private GameObject pausePanel;
    private GameObject gameOverPanel;
    private GameObject victoryPanel;

    private Image vignette;
    private UIBar healthBar;
    private Image healthTrail;
    private Text healthText;
    private Text weaponText;
    private Text ammoText;
    private Text reloadText;
    private UIBar reloadBar;
    private Text infoText;
    private Text timerText;
    private Text promptText;
    private Text messageText;
    private Text popupText;
    private Text pauseControlsText;
    private Text gameOverStatsText;
    private Text victoryStatsText;
    private Slider sensitivitySlider;
    private Slider volumeSlider;
    private Image[] hitMarkerParts;
    private RectTransform hitMarkerRoot;

    private PlayerHealth playerHealth;
    private PlayerShooting playerShooting;
    private UnityEngine.InputSystem.PlayerInput playerInput;

    // ---- animation state ----
    private float displayedHealth = 1f;
    private float trailHealth = 1f;
    private float hurtAlpha = 0f;
    private float hitMarkerTimer = 0f;
    private float hitMarkerDuration = 0.15f;
    private float messageTimer = 0f;
    private float messageDuration = 3f;
    private float popupTimer = 0f;
    private float searchTimer = 0f;
    private GameState lastState = (GameState)(-1);

    // ---- cached strings (avoid per-frame garbage) ----
    private int lastAmmo = int.MinValue;
    private int lastMag = int.MinValue;
    private string lastWeapon = null;
    private int lastHealthValue = int.MinValue;
    private int lastEnemies = int.MinValue;
    private int lastScore = int.MinValue;
    private int lastKills = int.MinValue;
    private int lastWave = int.MinValue;
    private int lastSeconds = int.MinValue;
    private string lastPrompt = null;

    private static readonly Color ColHealthGood = new Color(0.25f, 0.85f, 0.35f, 1f);
    private static readonly Color ColHealthBad = new Color(0.95f, 0.2f, 0.2f, 1f);

    // ===================================================================
    // Setup
    // ===================================================================

    private void Awake()
    {
        Instance = this;
        BuildUI();
        EnsureEventSystem();
    }

    private void Start()
    {
        FindPlayerReferences();
        ShowMessage("ELIMINATE ALL ENEMIES", 3.5f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (canvasObject != null)
            Destroy(canvasObject);
    }

    private void FindPlayerReferences()
    {
        if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerShooting == null) playerShooting = FindFirstObjectByType<PlayerShooting>();
        if (playerInput == null)
            playerInput = FindFirstObjectByType<UnityEngine.InputSystem.PlayerInput>();
    }

    private void EnsureEventSystem()
    {
        EventSystem es = FindFirstObjectByType<EventSystem>();

        if (es == null)
        {
            GameObject go = new GameObject("EventSystem");
            es = go.AddComponent<EventSystem>();
        }

        // The project uses the new Input System, so the UI needs its module.
        if (es.GetComponent<InputSystemUIInputModule>() == null)
        {
            StandaloneInputModule old = es.GetComponent<StandaloneInputModule>();
            if (old != null) Destroy(old);

            InputSystemUIInputModule module = es.gameObject.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }
    }

    private void BuildUI()
    {
        // Own root canvas so layout is independent of any canvas in the scene.
        canvasObject = new GameObject("GameHUD Canvas");

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();

        BuildHUD(canvasRect);
        BuildPausePanel(canvasRect);
        BuildGameOverPanel(canvasRect);
        BuildVictoryPanel(canvasRect);

        pausePanel.SetActive(false);
        gameOverPanel.SetActive(false);
        victoryPanel.SetActive(false);
    }

    // ------------------------------------------------------------------ HUD

    private void BuildHUD(Transform parent)
    {
        RectTransform root = UIFactory.CreateRect("HUD", parent);
        UIFactory.Stretch(root);
        hudRoot = root.gameObject;

        Vector2 center = new Vector2(0.5f, 0.5f);

        // Damage vignette (full screen).
        vignette = UIFactory.CreateImage("DamageVignette", root, new Color(1f, 0f, 0f, 0f));
        vignette.sprite = UIFactory.GetVignetteSprite();
        UIFactory.Stretch(vignette.rectTransform);

        // ---- crosshair ----
        RectTransform cross = UIFactory.CreateRect("Crosshair", root);
        UIFactory.Anchor(cross, center, center, Vector2.zero, new Vector2(100f, 100f));
        crosshairRoot = cross.gameObject;

        Color cc = new Color(1f, 1f, 1f, 0.9f);
        MakeCrosshairBar(cross, new Vector2(-13f, 0f), new Vector2(12f, 3f), cc);
        MakeCrosshairBar(cross, new Vector2(13f, 0f), new Vector2(12f, 3f), cc);
        MakeCrosshairBar(cross, new Vector2(0f, 13f), new Vector2(3f, 12f), cc);
        MakeCrosshairBar(cross, new Vector2(0f, -13f), new Vector2(3f, 12f), cc);
        MakeCrosshairBar(cross, Vector2.zero, new Vector2(3f, 3f), cc);

        // ---- hit marker (4 diagonal ticks) ----
        hitMarkerRoot = UIFactory.CreateRect("HitMarker", cross);
        UIFactory.Anchor(hitMarkerRoot, center, center, Vector2.zero, new Vector2(100f, 100f));
        hitMarkerParts = new Image[4];

        for (int i = 0; i < 4; i++)
        {
            float angle = 45f + 90f * i;
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            Image tick = UIFactory.CreateImage("Tick" + i, hitMarkerRoot, Color.white);
            UIFactory.Anchor(tick.rectTransform, center, center, dir * 20f, new Vector2(14f, 3f));
            tick.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            hitMarkerParts[i] = tick;
        }

        hitMarkerRoot.gameObject.SetActive(false);

        // ---- reload indicator (under crosshair) ----
        reloadText = UIFactory.CreateText("ReloadText", root, "RELOADING", 26,
            TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f, 1f));
        UIFactory.Anchor(reloadText.rectTransform, center, center, new Vector2(0f, -70f), new Vector2(400f, 40f));

        reloadBar = UIFactory.CreateBar(root, "ReloadBar", center, center,
            new Vector2(0f, -98f), new Vector2(180f, 8f),
            new Color(0f, 0f, 0f, 0.55f), new Color(1f, 0.85f, 0.3f, 1f), 1f);

        reloadText.gameObject.SetActive(false);
        reloadBar.root.gameObject.SetActive(false);

        // ---- health (bottom-left) ----
        Text healthLabel = UIFactory.CreateText("HealthLabel", root, "HEALTH", 24,
            TextAnchor.LowerLeft, accent);
        UIFactory.Anchor(healthLabel.rectTransform, Vector2.zero, Vector2.zero,
            new Vector2(54f, 100f), new Vector2(300f, 30f));

        healthBar = UIFactory.CreateBar(root, "HealthBar", Vector2.zero, Vector2.zero,
            new Vector2(50f, 50f), new Vector2(420f, 48f),
            new Color(0f, 0f, 0f, 0.6f), ColHealthGood, 4f);

        // White "damage trail" behind the fill.
        healthTrail = UIFactory.CreateImage("Trail", healthBar.root, new Color(1f, 1f, 1f, 0.55f));
        healthTrail.rectTransform.anchorMin = Vector2.zero;
        healthTrail.rectTransform.anchorMax = Vector2.one;
        healthTrail.rectTransform.offsetMin = new Vector2(4f, 4f);
        healthTrail.rectTransform.offsetMax = new Vector2(-4f, -4f);
        healthTrail.rectTransform.SetSiblingIndex(0);

        healthText = UIFactory.CreateText("HealthValue", healthBar.root, "100", 32,
            TextAnchor.MiddleCenter, Color.white);
        UIFactory.Stretch(healthText.rectTransform);

        // ---- ammo (bottom-right) ----
        Vector2 br = new Vector2(1f, 0f);

        weaponText = UIFactory.CreateText("WeaponName", root, "", 28,
            TextAnchor.LowerRight, accent);
        UIFactory.Anchor(weaponText.rectTransform, br, br, new Vector2(-54f, 130f), new Vector2(420f, 36f));

        ammoText = UIFactory.CreateText("Ammo", root, "", 70,
            TextAnchor.LowerRight, Color.white);
        UIFactory.Anchor(ammoText.rectTransform, br, br, new Vector2(-54f, 44f), new Vector2(520f, 90f));

        // ---- info (top-left) ----
        infoText = UIFactory.CreateText("Info", root, "", 34,
            TextAnchor.UpperLeft, Color.white);
        UIFactory.Anchor(infoText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(54f, -40f), new Vector2(500f, 160f));
        infoText.lineSpacing = 1.1f;

        // ---- timer (top-center) ----
        timerText = UIFactory.CreateText("Timer", root, "00:00", 40,
            TextAnchor.UpperCenter, Color.white);
        UIFactory.Anchor(timerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -36f), new Vector2(300f, 50f));

        // ---- banner message ----
        messageText = UIFactory.CreateText("Message", root, "", 60,
            TextAnchor.MiddleCenter, Color.white);
        UIFactory.Anchor(messageText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -190f), new Vector2(1400f, 90f));

        // ---- pickup prompt ----
        promptText = UIFactory.CreateText("Prompt", root, "", 36,
            TextAnchor.MiddleCenter, Color.white);
        UIFactory.Anchor(promptText.rectTransform, center, center, new Vector2(0f, -190f), new Vector2(1000f, 50f));

        // ---- score popup ----
        popupText = UIFactory.CreateText("ScorePopup", root, "", 40,
            TextAnchor.MiddleLeft, new Color(1f, 0.9f, 0.3f, 1f));
        UIFactory.Anchor(popupText.rectTransform, center, center, new Vector2(70f, 40f), new Vector2(300f, 50f));
        popupText.gameObject.SetActive(false);
    }

    private void MakeCrosshairBar(Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        Image img = UIFactory.CreateImage("Bar", parent, color);
        UIFactory.Anchor(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);

        Shadow s = img.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, 0.8f);
        s.effectDistance = new Vector2(1.5f, -1.5f);
    }

    // ---------------------------------------------------------------- menus

    private RectTransform CreateOverlay(string name, Transform parent, Color dim)
    {
        Image bg = UIFactory.CreateImage(name, parent, dim);
        bg.raycastTarget = true;   // blocks clicks to anything behind
        UIFactory.Stretch(bg.rectTransform);
        return bg.rectTransform;
    }

    private Text CreateCenterText(Transform parent, string content, int size, Vector2 pos, Color color)
    {
        Text t = UIFactory.CreateText("Text", parent, content, size, TextAnchor.MiddleCenter, color);
        UIFactory.Anchor(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(1400f, size * 1.6f));
        return t;
    }

    private void BuildPausePanel(Transform parent)
    {
        RectTransform panel = CreateOverlay("PausePanel", parent, new Color(0f, 0f, 0f, 0.72f));
        pausePanel = panel.gameObject;

        CreateCenterText(panel, "PAUSED", 110, new Vector2(0f, 330f), Color.white);

        CreateCenterText(panel, "MOUSE SENSITIVITY", 28, new Vector2(0f, 210f), accent);
        sensitivitySlider = UIFactory.CreateSlider(panel, new Vector2(0f, 168f), 520f,
            0.02f, 0.6f, 0.15f, accent, OnSensitivityChanged);

        CreateCenterText(panel, "VOLUME", 28, new Vector2(0f, 98f), accent);
        volumeSlider = UIFactory.CreateSlider(panel, new Vector2(0f, 56f), 520f,
            0f, 1f, 1f, accent, OnVolumeChanged);

        Vector2 btn = new Vector2(440f, 72f);
        UIFactory.CreateButton(panel, "RESUME", new Vector2(0f, -50f), btn, OnResumeClicked);
        UIFactory.CreateButton(panel, "RESTART", new Vector2(0f, -140f), btn, OnRestartClicked);
        UIFactory.CreateButton(panel, "QUIT", new Vector2(0f, -230f), btn, OnQuitClicked);

        pauseControlsText = CreateCenterText(panel, "", 24, new Vector2(0f, -350f),
            new Color(1f, 1f, 1f, 0.7f));
    }

    private void BuildGameOverPanel(Transform parent)
    {
        RectTransform panel = CreateOverlay("GameOverPanel", parent, new Color(0.25f, 0f, 0f, 0.8f));
        gameOverPanel = panel.gameObject;

        CreateCenterText(panel, "YOU DIED", 130, new Vector2(0f, 300f), new Color(1f, 0.25f, 0.25f, 1f));

        gameOverStatsText = CreateCenterText(panel, "", 40, new Vector2(0f, 80f), Color.white);
        gameOverStatsText.lineSpacing = 1.2f;

        Vector2 btn = new Vector2(440f, 72f);
        UIFactory.CreateButton(panel, "TRY AGAIN", new Vector2(0f, -190f), btn, OnRestartClicked);
        UIFactory.CreateButton(panel, "QUIT", new Vector2(0f, -280f), btn, OnQuitClicked);
    }

    private void BuildVictoryPanel(Transform parent)
    {
        RectTransform panel = CreateOverlay("VictoryPanel", parent, new Color(0f, 0.12f, 0.05f, 0.8f));
        victoryPanel = panel.gameObject;

        CreateCenterText(panel, "MISSION COMPLETE", 120, new Vector2(0f, 300f), new Color(0.3f, 1f, 0.5f, 1f));

        victoryStatsText = CreateCenterText(panel, "", 40, new Vector2(0f, 80f), Color.white);
        victoryStatsText.lineSpacing = 1.2f;

        Vector2 btn = new Vector2(440f, 72f);
        UIFactory.CreateButton(panel, "PLAY AGAIN", new Vector2(0f, -190f), btn, OnRestartClicked);
        UIFactory.CreateButton(panel, "QUIT", new Vector2(0f, -280f), btn, OnQuitClicked);
    }

    // -------------------------------------------------------- button events

    private void OnResumeClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.Resume();
    }

    private void OnRestartClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.Restart();
        else Time.timeScale = 1f;
    }

    private void OnQuitClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.Quit();
        else Application.Quit();
    }

    private void OnSensitivityChanged(float value)
    {
        if (PlayerLook.Instance != null)
            PlayerLook.Instance.lookSensitivity = value;

        PlayerPrefs.SetFloat("LookSensitivity", value);
    }

    private void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("MasterVolume", value);
    }

    // ===================================================================
    // Public API
    // ===================================================================

    // Original API: called by PlayerHealth when the player is hit.
    public void InstantiateHitUI()
    {
        hurtAlpha = 0.65f;

        if (HitUI == null)
            return;

        Transform parent = GetComponentInParent<Canvas>() != null
            ? transform
            : canvas.transform;

        GameObject hit = Instantiate(HitUI, parent);

        if (hit.GetComponent<DestroyAfterTime>() == null)
            Destroy(hit, 1f);
    }

    public void ShowHitMarker(bool kill)
    {
        hitMarkerDuration = kill ? 0.35f : 0.15f;
        hitMarkerTimer = hitMarkerDuration;

        Color c = kill ? new Color(1f, 0.2f, 0.2f, 1f) : Color.white;
        foreach (Image tick in hitMarkerParts)
            tick.color = c;

        hitMarkerRoot.gameObject.SetActive(true);
    }

    public void ShowMessage(string message, float duration = 3f)
    {
        messageText.text = message;
        Color mc = messageText.color;
        mc.a = 1f;
        messageText.color = mc;
        messageDuration = Mathf.Max(0.5f, duration);
        messageTimer = messageDuration;
    }

    public void ShowScorePopup(int score)
    {
        popupText.text = "+" + score;
        popupTimer = 1f;
        popupText.gameObject.SetActive(true);
    }

    // ===================================================================
    // Per-frame update
    // ===================================================================

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        searchTimer -= dt;
        if ((playerHealth == null || playerShooting == null) && searchTimer <= 0f)
        {
            FindPlayerReferences();
            searchTimer = 0.5f;
        }

        GameState state = GameManager.CurrentState;

        if (state != lastState)
        {
            ApplyState(state);
            lastState = state;
        }

        UpdateHealth(dt);
        UpdateVignette(dt);
        UpdateAmmo();
        UpdateInfo();
        UpdatePrompt(state);
        UpdateFading(dt);
    }

    private void ApplyState(GameState state)
    {
        bool playing = state == GameState.Playing;

        pausePanel.SetActive(state == GameState.Paused);
        gameOverPanel.SetActive(state == GameState.GameOver);
        victoryPanel.SetActive(state == GameState.Victory);

        crosshairRoot.SetActive(playing);
        hudRoot.SetActive(state != GameState.GameOver && state != GameState.Victory);

        if (state == GameState.Paused)
        {
            if (PlayerLook.Instance != null)
                sensitivitySlider.SetValueWithoutNotify(PlayerLook.Instance.lookSensitivity);

            volumeSlider.SetValueWithoutNotify(AudioListener.volume);
            pauseControlsText.text = BuildControlsHint();
        }
        else if (state == GameState.GameOver)
        {
            gameOverStatsText.text = BuildStats();
        }
        else if (state == GameState.Victory)
        {
            victoryStatsText.text = BuildStats();
        }
    }

    private string BuildStats()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return "";

        int seconds = Mathf.FloorToInt(gm.ElapsedTime);

        string text =
            "KILLS   " + gm.Kills + "\n" +
            "SCORE   " + gm.Score + "\n" +
            "ACCURACY   " + gm.AccuracyPercent + "%\n" +
            "TIME   " + (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00") + "\n" +
            "BEST   " + gm.BestScore;

        if (gm.NewBestScore)
            text += "   <color=#FFE04D>NEW BEST!</color>";

        return text;
    }

    private string BuildControlsHint()
    {
        return
            "WASD Move   |   " + GetKeyName("Jump", "SPACE") + " Jump   |   SHIFT Sprint   |   " +
            GetKeyName("Shoot", "LMB") + " Shoot   |   " + GetKeyName("Reload", "R") + " Reload\n" +
            GetKeyName("PickUp", "E") + " Pick up   |   " + GetKeyName("Drop", "G") + " Drop   |   ESC Pause";
    }

    // Reads the real key from the Input Actions so prompts never lie.
    private string GetKeyName(string actionName, string fallback)
    {
        if (playerInput == null)
            playerInput = FindFirstObjectByType<UnityEngine.InputSystem.PlayerInput>();

        if (playerInput == null || playerInput.actions == null)
            return fallback;

        InputAction action = playerInput.actions.FindAction(actionName, false);

        if (action == null)
        {
            foreach (InputAction a in playerInput.actions)
            {
                if (a.name.Replace(" ", "").ToLowerInvariant() == actionName.ToLowerInvariant())
                {
                    action = a;
                    break;
                }
            }
        }

        if (action == null)
            return fallback;

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding b = action.bindings[i];

            if (b.isComposite || b.isPartOfComposite || string.IsNullOrEmpty(b.path))
                continue;

            if (b.path.StartsWith("<Keyboard>") || b.path.StartsWith("<Mouse>"))
                return action.GetBindingDisplayString(i).ToUpperInvariant();
        }

        return fallback;
    }

    private void UpdateHealth(float dt)
    {
        if (playerHealth == null)
            return;

        float target = playerHealth.health / (float)Mathf.Max(1, playerHealth.maxHealth);

        // Fill follows quickly, white trail catches up slowly.
        displayedHealth = Mathf.MoveTowards(displayedHealth, target, 3f * dt);

        if (trailHealth < displayedHealth)
            trailHealth = displayedHealth;
        else
            trailHealth = Mathf.MoveTowards(trailHealth, displayedHealth, 0.5f * dt);

        healthBar.Set(displayedHealth);
        healthTrail.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(trailHealth), 1f);
        healthBar.fillImage.color = Color.Lerp(ColHealthBad, ColHealthGood, displayedHealth * 1.4f - 0.2f);

        if (playerHealth.health != lastHealthValue)
        {
            lastHealthValue = playerHealth.health;
            healthText.text = playerHealth.health.ToString();
        }
    }

    private void UpdateVignette(float dt)
    {
        hurtAlpha = Mathf.MoveTowards(hurtAlpha, 0f, 1.2f * dt);

        float lowHealthAlpha = 0f;

        if (playerHealth != null && !playerHealth.IsDead)
        {
            float pct = playerHealth.health / (float)Mathf.Max(1, playerHealth.maxHealth);

            if (pct < 0.3f)
                lowHealthAlpha = 0.25f + 0.12f * Mathf.Sin(Time.unscaledTime * 5f);
        }

        if (playerHealth != null && playerHealth.IsDead)
            lowHealthAlpha = 0.6f;

        Color c = vignette.color;
        c.a = Mathf.Clamp01(Mathf.Max(hurtAlpha, lowHealthAlpha));
        vignette.color = c;
    }

    private void UpdateAmmo()
    {
        Gun gun = playerShooting != null ? playerShooting.gun : null;

        if (gun == null)
        {
            if (lastWeapon != "")
            {
                lastWeapon = "";
                lastAmmo = int.MinValue;
                weaponText.text = "UNARMED";
                ammoText.text = "<size=50>-- / --</size>";
            }

            reloadText.gameObject.SetActive(false);
            reloadBar.root.gameObject.SetActive(false);
            return;
        }

        string weaponName = gun.DisplayName.ToUpperInvariant();

        if (weaponName != lastWeapon)
        {
            lastWeapon = weaponName;
            weaponText.text = weaponName;
        }

        if (gun.CurrentAmmo != lastAmmo || gun.MagSize != lastMag)
        {
            lastAmmo = gun.CurrentAmmo;
            lastMag = gun.MagSize;

            bool low = lastAmmo <= Mathf.Max(1, lastMag / 4);
            string col = low ? "#FF5555" : "#FFFFFF";

            ammoText.text = "<color=" + col + ">" + lastAmmo + "</color><size=40> / " + lastMag + "</size>";
        }

        bool reloading = gun.IsReloading;
        reloadText.gameObject.SetActive(reloading);
        reloadBar.root.gameObject.SetActive(reloading);

        if (reloading)
            reloadBar.Set(gun.ReloadProgress);
    }

    private void UpdateInfo()
    {
        GameManager gm = GameManager.Instance;
        int enemies = Enemy.AliveCount;
        int score = gm != null ? gm.Score : 0;
        int kills = gm != null ? gm.Kills : 0;

        WaveSpawner spawner = WaveSpawner.Instance;
        int wave = spawner != null ? spawner.CurrentWave : -1;

        if (enemies != lastEnemies || score != lastScore || kills != lastKills || wave != lastWave)
        {
            lastEnemies = enemies;
            lastScore = score;
            lastKills = kills;
            lastWave = wave;

            string text = "ENEMIES  <color=#FF6A6A>" + enemies + "</color>\n" +
                          "KILLS  " + kills + "\n" +
                          "SCORE  <color=#FFE04D>" + score + "</color>";

            if (spawner != null && wave > 0)
                text = "WAVE  " + wave + " / " + spawner.totalWaves + "\n" + text;

            infoText.text = text;
        }

        if (gm != null)
        {
            int seconds = Mathf.FloorToInt(gm.ElapsedTime);

            if (seconds != lastSeconds)
            {
                lastSeconds = seconds;
                timerText.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
            }
        }
    }

    private void UpdatePrompt(GameState state)
    {
        string prompt = "";

        if (state == GameState.Playing && playerShooting != null)
        {
            PickUp pickup = playerShooting.CurrentPickup;

            if (pickup != null)
                prompt = "[" + GetKeyName("PickUp", "E") + "]  PICK UP  " + pickup.DisplayName.ToUpperInvariant();
            else if (playerShooting.gun == null)
                prompt = "FIND A WEAPON";
        }

        if (prompt != lastPrompt)
        {
            lastPrompt = prompt;
            promptText.text = prompt;
        }
    }

    private void UpdateFading(float dt)
    {
        // banner message
        if (messageTimer > 0f)
        {
            messageTimer -= dt;
            Color c = messageText.color;
            c.a = Mathf.Clamp01(messageTimer / 0.6f);
            messageText.color = c;

            if (messageTimer <= 0f)
                messageText.text = "";
        }

        // hit marker
        if (hitMarkerTimer > 0f)
        {
            hitMarkerTimer -= dt;

            if (hitMarkerTimer <= 0f)
                hitMarkerRoot.gameObject.SetActive(false);
            else
                hitMarkerRoot.localScale = Vector3.one * (1f + 0.4f * (hitMarkerTimer / hitMarkerDuration));
        }

        // score popup floats up and fades
        if (popupTimer > 0f)
        {
            popupTimer -= dt;

            if (popupTimer <= 0f)
            {
                popupText.gameObject.SetActive(false);
            }
            else
            {
                popupText.rectTransform.anchoredPosition = new Vector2(70f, 40f + (1f - popupTimer) * 50f);
                Color c = popupText.color;
                c.a = Mathf.Clamp01(popupTimer * 2f);
                popupText.color = c;
            }
        }
    }
}

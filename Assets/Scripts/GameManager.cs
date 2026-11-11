using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum GameState
{
    Playing,
    Paused,
    Dying,      // short slow-motion after the player dies
    GameOver,
    Victory
}


public class GameManager : MonoBehaviour
{
    private static GameManager instance;

    public static GameManager Instance { get { return instance; } }

    /// <summary>True while the game is actually running (not paused / dead / won).</summary>
    public static bool IsPlaying
    {
        get { return instance == null || instance.State == GameState.Playing; }
    }

    public static GameState CurrentState
    {
        get { return instance != null ? instance.State : GameState.Playing; }
    }

    public bool winWhenAllEnemiesDead = true;
    public float victoryDelay = 1.5f;
    public float deathSlowMoTime = 1.2f;

    public GameState State { get; private set; }
    public int Kills { get; private set; }
    public int Score { get; private set; }
    public int ShotsFired { get; private set; }
    public int ShotsHit { get; private set; }
    public float ElapsedTime { get; private set; }
    public int BestScore { get; private set; }
    public bool NewBestScore { get; private set; }

    public int AccuracyPercent
    {
        get
        {
            if (ShotsFired <= 0) return 0;
            return Mathf.Clamp(Mathf.RoundToInt(ShotsHit * 100f / ShotsFired), 0, 100);
        }
    }

    private bool sawEnemies;
    private bool victoryPending;

    // ------------------------------------------------------------ bootstrap

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // Scenes loaded later (Restart) need the managers re-created too.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        EnsureManagers();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureManagers();
    }

    private static void EnsureManagers()
    {
        if (FindFirstObjectByType<PlayerHealth>() == null)
            return;

        if (FindFirstObjectByType<GameManager>() == null)
            new GameObject("GameManager").AddComponent<GameManager>();

        if (FindFirstObjectByType<UIManager>() == null)
            new GameObject("UIManager").AddComponent<UIManager>();
    }

    // ----------------------------------------------------------------- life

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        State = GameState.Playing;
        BestScore = PlayerPrefs.GetInt("BestScore", 0);

        Time.timeScale = 1f;
        AudioListener.pause = false;
        ApplyCursor(true);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }

    private void Update()
    {
        HandlePauseInput();

        if (State != GameState.Playing)
            return;

        ElapsedTime += Time.unscaledDeltaTime;

        int alive = Enemy.AliveCount;

        if (alive > 0)
            sawEnemies = true;

        bool spawnerDone = WaveSpawner.Instance == null || WaveSpawner.Instance.AllWavesDone;

        if (winWhenAllEnemiesDead && sawEnemies && alive == 0 && spawnerDone && !victoryPending)
            StartCoroutine(VictoryRoutine());
    }

    private void HandlePauseInput()
    {
        bool pressed =
            (Keyboard.current != null &&
             (Keyboard.current.escapeKey.wasPressedThisFrame ||
              Keyboard.current.pKey.wasPressedThisFrame)) ||
            (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);

        if (!pressed)
            return;

        if (State == GameState.Playing) Pause();
        else if (State == GameState.Paused) Resume();
    }

    // -------------------------------------------------------------- control

    public void Pause()
    {
        if (State != GameState.Playing) return;

        State = GameState.Paused;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        ApplyCursor(false);
    }

    public void Resume()
    {
        if (State != GameState.Paused) return;

        State = GameState.Playing;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        ApplyCursor(true);
    }

    public void GameOver()
    {
        if (State == GameState.Dying || State == GameState.GameOver || State == GameState.Victory)
            return;

        StartCoroutine(GameOverRoutine());
    }

    private IEnumerator GameOverRoutine()
    {
        State = GameState.Dying;
        Time.timeScale = 0.3f;

        yield return new WaitForSecondsRealtime(deathSlowMoTime);

        FinishRun(GameState.GameOver);
    }

    private IEnumerator VictoryRoutine()
    {
        victoryPending = true;

        yield return new WaitForSecondsRealtime(victoryDelay);

        // Make sure nothing new appeared in the meantime.
        if (State == GameState.Playing && Enemy.AliveCount == 0)
            FinishRun(GameState.Victory);
        else
            victoryPending = false;
    }

    private void FinishRun(GameState result)
    {
        State = result;
        Time.timeScale = 0f;
        ApplyCursor(false);

        if (Score > BestScore)
        {
            BestScore = Score;
            NewBestScore = true;
            PlayerPrefs.SetInt("BestScore", BestScore);
            PlayerPrefs.Save();
        }
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        Scene scene = SceneManager.GetActiveScene();

#if UNITY_EDITOR
        // Lets Restart work in the editor even if the scene isn't in Build Settings.
        if (scene.buildIndex < 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path, new LoadSceneParameters(LoadSceneMode.Single));
            return;
        }
#endif

        SceneManager.LoadScene(scene.buildIndex);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void ApplyCursor(bool gameplay)
    {
        Cursor.lockState = gameplay ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !gameplay;
    }

    // --------------------------------------------------------------- events

    public static void NotifyEnemyKilled(int score)
    {
        if (instance == null) return;

        instance.Kills++;
        instance.Score += score;

        if (UIManager.Instance != null)
            UIManager.Instance.ShowScorePopup(score);
    }

    public static void NotifyShotFired()
    {
        if (instance != null) instance.ShotsFired++;
    }

    public static void NotifyShotHit()
    {
        if (instance != null) instance.ShotsHit++;
    }
}

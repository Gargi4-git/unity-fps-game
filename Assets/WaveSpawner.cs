using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// OPTIONAL. Add to an empty GameObject to get wave-based gameplay.
/// The game is won after the last wave is wiped out.
/// If you don't add one, the game is won when all enemies in the scene die.
/// </summary>
public class WaveSpawner : MonoBehaviour
{
    public static WaveSpawner Instance;

    public GameObject enemyPrefab;
    public Transform[] spawnPoints;

    public int totalWaves = 3;
    public int firstWaveSize = 3;
    public int enemiesAddedPerWave = 2;
    public float startDelay = 3f;
    public float timeBetweenWaves = 5f;
    public float spawnInterval = 0.6f;
    public bool alertSpawnedEnemies = true;

    public int CurrentWave { get; private set; }
    public bool AllWavesDone { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning("WaveSpawner has no Enemy Prefab assigned - disabled.", this);
            AllWavesDone = true;
            return;
        }

        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        yield return new WaitForSeconds(startDelay);

        for (int wave = 1; wave <= totalWaves; wave++)
        {
            CurrentWave = wave;

            if (UIManager.Instance != null)
                UIManager.Instance.ShowMessage("WAVE " + wave + " / " + totalWaves, 3f);

            int count = firstWaveSize + (wave - 1) * enemiesAddedPerWave;

            for (int i = 0; i < count; i++)
            {
                while (!GameManager.IsPlaying)
                    yield return null;

                SpawnOne();
                yield return new WaitForSeconds(spawnInterval);
            }

            while (Enemy.AliveCount > 0)
                yield return null;

            if (wave < totalWaves)
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowMessage("WAVE CLEARED", 2.5f);

                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }

        AllWavesDone = true;
    }

    private void SpawnOne()
    {
        Vector3 position = transform.position;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform p = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (p != null) position = p.position;
        }

        NavMeshHit hit;
        if (NavMesh.SamplePosition(position, out hit, 3f, NavMesh.AllAreas))
            position = hit.position;

        GameObject go = Instantiate(enemyPrefab, position, Quaternion.identity);

        if (alertSpawnedEnemies)
        {
            Enemy e = go.GetComponent<Enemy>();
            if (e != null) e.Alert();
        }
    }
}

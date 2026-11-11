using UnityEngine;

/// <summary>
/// Pooled sound-effect player. Auto-creates itself if the scene has none,
/// so AudioManager.Instance is never null.
/// </summary>
public class AudioManager : MonoBehaviour
{
    private static AudioManager instance;

    public static AudioManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<AudioManager>();

                if (instance == null)
                {
                    GameObject go = new GameObject("AudioManager");
                    instance = go.AddComponent<AudioManager>();
                }
            }

            return instance;
        }
    }

    public int poolSize = 16;
    public float pitchVariation = 0.04f;

    private AudioSource[] pool;
    private int nextIndex;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        AudioListener.volume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        BuildPool();
    }

    private void BuildPool()
    {
        if (pool != null)
            return;

        pool = new AudioSource[Mathf.Max(4, poolSize)];

        for (int i = 0; i < pool.Length; i++)
        {
            GameObject go = new GameObject("SFX_" + i);
            go.transform.SetParent(transform, false);

            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            pool[i] = source;
        }
    }

    // 2D sound (UI, player weapon, footsteps).
    public void PlaySFX(AudioClip audioClip, float volume = 1f)
    {
        Play(audioClip, volume, false, Vector3.zero);
    }

    // 3D sound at a world position (enemy gunfire, explosions).
    public void PlaySFXAt(AudioClip audioClip, Vector3 position, float volume = 1f)
    {
        Play(audioClip, volume, true, position);
    }

    private void Play(AudioClip clip, float volume, bool spatial, Vector3 position)
    {
        if (clip == null)
            return;

        if (pool == null)
            BuildPool();

        AudioSource source = GetFreeSource();
        source.Stop();

        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);

        if (spatial)
        {
            source.transform.position = position;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 3f;
            source.maxDistance = 45f;
        }
        else
        {
            source.spatialBlend = 0f;
        }

        source.Play();
    }

    private AudioSource GetFreeSource()
    {
        for (int i = 0; i < pool.Length; i++)
        {
            int index = (nextIndex + i) % pool.Length;

            if (!pool[index].isPlaying)
            {
                nextIndex = (index + 1) % pool.Length;
                return pool[index];
            }
        }

        // All busy: steal the oldest.
        AudioSource stolen = pool[nextIndex];
        nextIndex = (nextIndex + 1) % pool.Length;
        return stolen;
    }
}

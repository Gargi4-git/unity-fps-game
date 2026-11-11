using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour, IDamageable
{
    // ---- registry (used by GameManager / WaveSpawner) ----
    public static readonly List<Enemy> All = new List<Enemy>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        All.Clear();
    }

    public static int AliveCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < All.Count; i++)
                if (All[i] != null && !All[i].IsDead) n++;
            return n;
        }
    }

    // ---- stats ----
    public int maxHealth = 100;
    public int health = 100;
    public int scoreValue = 100;

    // ---- weapon ----
    public GameObject bulletPrefab;
    public Transform bulletSpawnPoint;
    public GameObject weaponFlash;
    public float bloom;
    public float fireRate;
    public float lastShootTime = 0f;
    public AudioClip shootingSFX;

    // ---- feedback ----
    public Material HitMat;
    public AudioClip hurtSFX;
    public AudioClip deathSFX;

    // ---- AI ----
    public int currentPointIndex = 0;
    public Vector3 currentTarget;
    public float positionThreshold = 1f;
    public float idleTime = 5f;
    public float attackDistance = 5f;
    public float maxVisionDistance = 20f;
    public float minChasingHealth = 30f;
    [Tooltip("Total field of view in degrees.")]
    public float fieldOfView = 160f;
    [Tooltip("Player is always noticed inside this distance.")]
    public float closeSenseRange = 3f;
    public float eyeHeight = 1f;
    public float turnSpeed = 8f;
    [Tooltip("Delay between spotting the player and the first shot.")]
    public float reactionTime = 0.3f;
    public float fleeDuration = 6f;
    public float searchTime = 3f;
    public float alertDuration = 5f;

    // ---- death ----
    public float destroyDelay = 4f;
    public GameObject dropPrefab;
    [Range(0f, 1f)] public float dropChance = 0.3f;

    public Transform[] patrolPoints;

    public enum State
    {
        Idle,
        Patrolling,
        Chasing,
        Attacking,
        Fleeing,
        Dead
    }

    public State state = State.Idle;

    // State pattern: the object that holds the behaviour of the current state.
    private EnemyState currentState;

    public bool IsDead { get { return state == State.Dead; } }
    public float HealthNormalized { get { return Mathf.Clamp01(health / (float)Mathf.Max(1, maxHealth)); } }

    private Rigidbody rb;
    private NavMeshAgent agent;
    private Renderer[] renderers;
    private Material[][] originalMaterials;
    private Coroutine blinkRoutine;

    private Transform playerTransform;
    private Collider playerCollider;
    private bool canSeePlayer;
    private Vector3 lastKnownPlayerPosition;

    private float idleTimeCounter;
    private float alertTimer;
    private float attackStartTime;
    private float fleeTimer;
    private Vector3 fleeTarget;
    private bool fledOnce;
    private bool alertOnStart;
    private bool warnedMissingWeapon;

    private void Awake()
    {
        if (health > maxHealth)
            maxHealth = health;

        TransitionTo(new IdleState(), State.Idle);
    }

    private void OnEnable()
    {
        if (!All.Contains(this))
            All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        agent = GetComponent<NavMeshAgent>();

        // The NavMeshAgent drives movement, so the Rigidbody must not fight it.
        // (It's switched back to physics when the enemy dies.)
        if (rb != null)
            rb.isKinematic = true;

        CacheRenderers();

        if (agent == null)
        {
            Debug.LogError("Enemy needs a NavMeshAgent!", this);
            enabled = false;
            return;
        }

        FindPlayer();

        if (playerTransform == null)
        {
            Debug.LogError("No GameObject tagged Player was found!", this);
            enabled = false;
            return;
        }

        lastKnownPlayerPosition = playerTransform.position;

        FindPatrolPoints();

        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            // Random start so enemies don't all walk in a conga line.
            currentPointIndex = Random.Range(0, patrolPoints.Length);
            currentTarget = patrolPoints[currentPointIndex].position;
        }
        else
        {
            Debug.LogWarning("No patrol points found. Enemy cannot patrol.", this);
        }

        idleTimeCounter = idleTime;

        float top = transform.position.y;
        foreach (Renderer r in renderers)
            top = Mathf.Max(top, r.bounds.max.y);

        float height = top - transform.position.y + 0.35f;
        if (height < 1.2f) height = 2.1f;

        gameObject.AddComponent<EnemyHealthBar>().Init(this, height);

        if (alertOnStart)
            Alert();
    }

    private void FindPlayer()
    {
        GameObject playerObject = null;

        try { playerObject = GameObject.FindWithTag("Player"); }
        catch (UnityException) { }

        if (playerObject == null)
        {
            PlayerHealth ph = FindFirstObjectByType<PlayerHealth>();
            if (ph != null) playerObject = ph.gameObject;
        }

        if (playerObject == null)
            return;

        playerTransform = playerObject.transform;
        playerCollider = playerObject.GetComponentInChildren<Collider>();
    }

    private void FindPatrolPoints()
    {
        if (patrolPoints != null && patrolPoints.Length > 0)
            return;

        GameObject patrolParent = null;

        try { patrolParent = GameObject.FindWithTag("PatrolPoint"); }
        catch (UnityException) { }

        if (patrolParent == null)
            return;

        // Only direct children count as points.
        int count = patrolParent.transform.childCount;
        patrolPoints = new Transform[count];

        for (int i = 0; i < count; i++)
            patrolPoints[i] = patrolParent.transform.GetChild(i);
    }

    private void CacheRenderers()
    {
        Renderer[] all = GetComponentsInChildren<Renderer>();
        List<Renderer> valid = new List<Renderer>();

        foreach (Renderer r in all)
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                continue;
            valid.Add(r);
        }

        renderers = valid.ToArray();
        originalMaterials = new Material[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
            originalMaterials[i] = renderers[i].sharedMaterials;
    }

    // ------------------------------------------------------------ main loop

    private void Update()
    {
        if (state == State.Dead || playerTransform == null || agent == null)
            return;

        if (!GameManager.IsPlaying)
            return;

        if (!agent.enabled || !agent.isOnNavMesh)
            return;

        if (alertTimer > 0f)
            alertTimer -= Time.deltaTime;

        LookForPlayer();

        currentState.Update();

        FacePlayer();
    }

    private Vector3 PlayerAimPoint
    {
        get
        {
            if (playerCollider != null)
                return playerCollider.bounds.center;

            return playerTransform.position + Vector3.up * 0.5f;
        }
    }

    private void LookForPlayer()
    {
        canSeePlayer = false;

        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3 to = PlayerAimPoint - origin;
        float dist = to.magnitude;

        if (dist > maxVisionDistance || dist < 0.01f)
            return;

        // Field of view (ignored when alerted or when the player is very close).
        if (alertTimer <= 0f && dist > closeSenseRange)
        {
            Vector3 flat = to;
            flat.y = 0f;

            if (flat.sqrMagnitude > 0.01f &&
                Vector3.Angle(transform.forward, flat) > fieldOfView * 0.5f)
                return;
        }

        // Line of sight. Ignores the enemy's own colliders.
        RaycastHit hit;
        if (GameUtil.RaycastIgnoring(origin, to / dist, dist + 0.5f, transform, out hit))
        {
            if (hit.transform.IsChildOf(playerTransform))
                canSeePlayer = true;
        }

        if (!canSeePlayer)
            return;

        lastKnownPlayerPosition = playerTransform.position;

        if (state == State.Idle || state == State.Patrolling)
            TransitionTo(new ChasingState(), State.Chasing);
    }

    // --------------------------------------------------------------- states

    // State pattern ------------------------------------------------------
    // Each state is its own class. Enemy.Update() only calls
    // currentState.Update(); the states decide when to switch (TransitionTo).

    private abstract class EnemyState
    {
        protected Enemy enemy;

        public void SetContext(Enemy enemy)
        {
            this.enemy = enemy;
        }

        public virtual void Enter()
        {
        }

        public virtual void Exit()
        {
        }

        public abstract void Update();
    }

    private sealed class IdleState : EnemyState
    {
        public override void Update()
        {
            enemy.agent.ResetPath();
            enemy.idleTimeCounter -= Time.deltaTime;

            if (enemy.idleTimeCounter <= 0f)
            {
                if (enemy.patrolPoints != null && enemy.patrolPoints.Length > 0)
                    enemy.TransitionTo(new PatrollingState(), State.Patrolling);
                else
                    enemy.idleTimeCounter = enemy.idleTime;
            }
        }
    }

    private sealed class PatrollingState : EnemyState
    {
        public override void Update()
        {
            if (enemy.patrolPoints == null || enemy.patrolPoints.Length == 0)
            {
                enemy.TransitionTo(new IdleState(), State.Idle);
                return;
            }

            if (FlatDistance(enemy.transform.position, enemy.currentTarget) < enemy.positionThreshold)
            {
                if (Random.Range(0, 100) < 10)
                {
                    enemy.idleTimeCounter = enemy.idleTime;
                    enemy.TransitionTo(new IdleState(), State.Idle);
                    return;
                }

                enemy.currentPointIndex = (enemy.currentPointIndex + 1) % enemy.patrolPoints.Length;
                enemy.currentTarget = enemy.patrolPoints[enemy.currentPointIndex].position;
            }

            enemy.agent.SetDestination(enemy.currentTarget);
        }
    }

    private sealed class ChasingState : EnemyState
    {
        public override void Update()
        {
            if (enemy.ShouldFlee())
            {
                enemy.StartFlee();
                return;
            }

            enemy.agent.SetDestination(enemy.lastKnownPlayerPosition);

            float distance = Vector3.Distance(enemy.transform.position, enemy.playerTransform.position);

            if (enemy.canSeePlayer && distance <= enemy.attackDistance)
            {
                enemy.TransitionTo(new AttackingState(), State.Attacking);
                enemy.attackStartTime = Time.time;
                return;
            }

            // Reached the last place we saw the player and nobody is there:
            // look around for a bit, then go back to patrolling.
            if (!enemy.canSeePlayer && enemy.alertTimer <= 0f &&
                !enemy.agent.pathPending &&
                enemy.agent.remainingDistance <= enemy.agent.stoppingDistance + enemy.positionThreshold)
            {
                enemy.idleTimeCounter = enemy.searchTime;
                enemy.TransitionTo(new IdleState(), State.Idle);
            }
        }
    }

    private sealed class AttackingState : EnemyState
    {
        public override void Update()
        {
            enemy.idleTimeCounter = enemy.idleTime;
            enemy.agent.ResetPath();

            if (enemy.ShouldFlee())
            {
                enemy.StartFlee();
                return;
            }

            if (enemy.canSeePlayer && Time.time >= enemy.attackStartTime + enemy.reactionTime)
                enemy.Shoot();

            float distance = Vector3.Distance(enemy.transform.position, enemy.playerTransform.position);

            if (!enemy.canSeePlayer || distance > enemy.attackDistance)
                enemy.TransitionTo(new ChasingState(), State.Chasing);
        }
    }

    private sealed class FleeingState : EnemyState
    {
        public override void Update()
        {
            enemy.fleeTimer -= Time.deltaTime;
            enemy.agent.SetDestination(enemy.fleeTarget);

            bool arrived = !enemy.agent.pathPending &&
                           enemy.agent.remainingDistance <= enemy.agent.stoppingDistance + enemy.positionThreshold;

            if (enemy.fleeTimer <= 0f || arrived)
            {
                enemy.idleTimeCounter = 1f;

                if (enemy.canSeePlayer)
                    enemy.TransitionTo(new ChasingState(), State.Chasing);
                else
                    enemy.TransitionTo(new IdleState(), State.Idle);
            }
        }
    }

    private sealed class DeadState : EnemyState
    {
        public override void Update()
        {
        }
    }

    private void TransitionTo(EnemyState nextState, State stateValue)
    {
        if (currentState != null && currentState.GetType() == nextState.GetType())
            return;

        if (currentState != null)
            currentState.Exit();

        currentState = nextState;
        currentState.SetContext(this);
        state = stateValue;

        currentState.Enter();
    }

    // A badly hurt enemy runs away once; if it gets cornered it fights back.
    private bool ShouldFlee()
    {
        return !fledOnce && health < minChasingHealth;
    }

    private void StartFlee()
    {
        fledOnce = true;
        TransitionTo(new FleeingState(), State.Fleeing);
        fleeTimer = fleeDuration;
        fleeTarget = PickFleePoint();
        agent.SetDestination(fleeTarget);
    }

    private Vector3 PickFleePoint()
    {
        // Prefer the patrol point farthest away from the player.
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            Transform farthest = patrolPoints[0];
            float best = -1f;

            foreach (Transform p in patrolPoints)
            {
                float d = Vector3.Distance(p.position, playerTransform.position);
                if (d > best) { best = d; farthest = p; }
            }

            return farthest.position;
        }

        Vector3 away = transform.position - playerTransform.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;

        Vector3 candidate = transform.position + away.normalized * 10f;
        NavMeshHit hit;

        if (NavMesh.SamplePosition(candidate, out hit, 6f, NavMesh.AllAreas))
            return hit.position;

        return transform.position;
    }

    private void FacePlayer()
    {
        if (!canSeePlayer)
            return;

        // While walking, the agent turns us itself.
        if (state != State.Attacking && agent.velocity.sqrMagnitude > 0.05f)
            return;

        Vector3 dir = playerTransform.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            return;

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(dir),
            turnSpeed * Time.deltaTime);
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    /// <summary>Makes this enemy aware of the player's position (e.g. when hit or spawned in a wave).</summary>
    public void Alert()
    {
        if (state == State.Dead)
            return;

        if (playerTransform == null)
        {
            alertOnStart = true;
            return;
        }

        alertTimer = alertDuration;
        lastKnownPlayerPosition = playerTransform.position;

        if (state == State.Idle || state == State.Patrolling)
            TransitionTo(new ChasingState(), State.Chasing);
    }

    // --------------------------------------------------------------- damage

    public void TakeDamage(int amount)
    {
        if (state == State.Dead || amount <= 0)
            return;

        health -= amount;

        Alert();

        if (health <= 0)
        {
            health = 0;
            Die();
            return;
        }

        AudioManager.Instance.PlaySFXAt(hurtSFX, transform.position, 0.6f);

        if (HitMat != null && renderers != null && renderers.Length > 0)
        {
            if (blinkRoutine != null)
                StopCoroutine(blinkRoutine);

            blinkRoutine = StartCoroutine(Blink());
        }
    }

    private IEnumerator Blink()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;

            Material[] mats = new Material[originalMaterials[i].Length];
            for (int j = 0; j < mats.Length; j++) mats[j] = HitMat;
            renderers[i].sharedMaterials = mats;
        }

        yield return new WaitForSeconds(0.1f);

        RestoreMaterials();
        blinkRoutine = null;
    }

    private void RestoreMaterials()
    {
        if (renderers == null) return;

        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                renderers[i].sharedMaterials = originalMaterials[i];
    }

    private void Die()
    {
        if (state == State.Dead)
            return;

        TransitionTo(new DeadState(), State.Dead);

        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        RestoreMaterials();

        if (agent != null)
        {
            if (agent.enabled && agent.isOnNavMesh)
                agent.ResetPath();

            agent.enabled = false;
        }

        if (rb != null)
        {
            // Let physics topple the body over.
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.None;
            rb.AddForce(-transform.forward * 2f + Vector3.up, ForceMode.Impulse);
            rb.AddTorque(transform.right * -3f, ForceMode.Impulse);
        }
        else
        {
            StartCoroutine(FallOver());
        }

        AudioManager.Instance.PlaySFXAt(deathSFX, transform.position, 0.8f);

        GameManager.NotifyEnemyKilled(scoreValue);

        if (dropPrefab != null && Random.value <= dropChance)
            Instantiate(dropPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);

        Destroy(gameObject, destroyDelay);
    }

    private IEnumerator FallOver()
    {
        Quaternion start = transform.rotation;
        Quaternion end = Quaternion.AngleAxis(-90f, transform.right) * start;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / 0.5f;
            transform.rotation = Quaternion.Slerp(start, end, t);
            yield return null;
        }

        // The corpse shouldn't block bullets or the player.
        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;
    }

    // ------------------------------------------------------------- shooting

    private void Shoot()
    {
        if (bulletPrefab == null || bulletSpawnPoint == null)
        {
            if (!warnedMissingWeapon)
            {
                Debug.LogWarning("Enemy has no Bullet Prefab / Bullet Spawn Point assigned.", this);
                warnedMissingWeapon = true;
            }
            return;
        }

        if (Time.time <= lastShootTime + fireRate)
            return;

        Vector3 origin = bulletSpawnPoint.position;
        Vector3 directionToPlayer = (PlayerAimPoint - origin).normalized;

        Quaternion bulletRotation = Quaternion.LookRotation(directionToPlayer);

        float maxInaccuracy = 10f;
        float currentInaccuracy = bloom * maxInaccuracy;

        float randomYaw = Random.Range(-currentInaccuracy, currentInaccuracy);
        float randomPitch = Random.Range(-currentInaccuracy, currentInaccuracy);

        bulletRotation *= Quaternion.Euler(randomPitch, randomYaw, 0f);

        AudioManager.Instance.PlaySFXAt(shootingSFX, origin, 0.5f);

        GameObject b = Instantiate(bulletPrefab, origin, bulletRotation);
        Bullet bullet = b.GetComponent<Bullet>();
        if (bullet != null) bullet.SetOwner(gameObject);

        if (weaponFlash != null)
        {
            GameObject flash = Instantiate(weaponFlash, origin, bulletRotation, bulletSpawnPoint);
            Destroy(flash, 0.15f);   // used to leak forever
        }

        lastShootTime = Time.time;
    }
}

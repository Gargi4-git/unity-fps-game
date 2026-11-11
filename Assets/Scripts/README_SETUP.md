# Setup / what changed

Drop this folder over your old `Scripts` folder (keep the .meta files so
all your scene/prefab references stay linked).

## Required once
1. **Tag** your player object `Player` (you probably already did).
2. Keep a `PatrolPoint` tag on the parent of your patrol points (optional).
3. **Add your scene to Build Settings** (File > Build Profiles / Build Settings)
   so the Restart button works in a build. (In the editor it works anyway.)
4. Input Actions names used (Send Messages mode): Look, Movement, Jump,
   Shoot, Reload, PickUp, Drop. ESC / P / Gamepad Start = pause (hard-wired).
5. Bullet prefabs: Rigidbody + Collider + `Bullet`. The old tags
   `PlayerBullet` / `EnemyBullet` are no longer needed.
6. Enemy prefab: NavMeshAgent + Collider + `Enemy`.

The HUD, pause menu, game-over/victory screens, EventSystem, GameManager
and AudioManager are all created automatically at runtime.

## Optional
* `WaveSpawner` on an empty object (assign Enemy Prefab + spawn points) -> waves.
  Without it you win by killing every enemy in the scene.
* `HealthPickup` on a prefab -> assign to Enemy > Drop Prefab.
* Gun: set `Weapon Name`, `Reload SFX`, `Empty SFX`, `Spread`.
* Enemy: `Hurt SFX`, `Death SFX`, `Score Value`, `Field Of View`.

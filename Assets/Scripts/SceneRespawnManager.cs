using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class SceneRespawnManager : MonoBehaviour
{
    private const string SpawnMarkerName = "spawn";
    private static readonly Dictionary<string, Vector3> CheckpointsByScene = new Dictionary<string, Vector3>(StringComparer.Ordinal);

    private static SceneRespawnManager instance;
    private static bool shouldPlaceAtSpawnAfterLoad;

    [Header("Scene Reload")]
    [SerializeField] private KeyCode reloadSceneKey = KeyCode.F5;
    [SerializeField, Min(0f)] private float respawnReloadDelay = 0.25f;

    [Header("Spawn Markers")]
    [SerializeField] private bool placePlayerAtSpawnOnSceneLoad = true;
    [SerializeField] private bool makeSpawnMarkersTriggers = true;

    private Coroutine respawnRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void RespawnCurrentScene()
    {
        EnsureInstance().BeginRespawn();
    }

    public static void ReloadCurrentSceneAtCheckpoint()
    {
        EnsureInstance().BeginRespawn(0f);
    }

    public static void SetCheckpoint(Scene scene, Vector3 position)
    {
        if (!scene.IsValid())
            return;

        string sceneKey = GetSceneKey(scene);
        CheckpointsByScene[sceneKey] = position;
    }

    private static SceneRespawnManager EnsureInstance()
    {
        if (instance != null)
            return instance;

        SceneRespawnManager existing = FindFirstObjectByType<SceneRespawnManager>();
        if (existing != null)
        {
            instance = existing;
            return instance;
        }

        GameObject managerObject = new GameObject("Scene Respawn Manager");
        instance = managerObject.AddComponent<SceneRespawnManager>();
        DontDestroyOnLoad(managerObject);
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void Update()
    {
        if (reloadSceneKey == KeyCode.None || respawnRoutine != null)
            return;

        if (Input.GetKeyDown(reloadSceneKey) && FindPlayer() != null)
            ReloadCurrentSceneAtCheckpoint();
    }

    private void BeginRespawn()
    {
        BeginRespawn(respawnReloadDelay);
    }

    private void BeginRespawn(float delay)
    {
        if (respawnRoutine != null)
            return;

        shouldPlaceAtSpawnAfterLoad = true;
        respawnRoutine = StartCoroutine(ReloadActiveSceneAfterDelay(Mathf.Max(0f, delay)));
    }

    private IEnumerator ReloadActiveSceneAfterDelay(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.buildIndex >= 0)
            SceneManager.LoadScene(activeScene.buildIndex);
        else
            SceneManager.LoadScene(activeScene.name);

        respawnRoutine = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ConfigureSpawnMarkers(scene);

        if (shouldPlaceAtSpawnAfterLoad || placePlayerAtSpawnOnSceneLoad)
        {
            PlacePlayerAtSceneSpawn(scene);
            shouldPlaceAtSpawnAfterLoad = false;
        }
    }

    private void ConfigureSpawnMarkers(Scene scene)
    {
        if (!scene.IsValid())
            return;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            ConfigureSpawnMarkersInChildren(roots[i].transform);
    }

    private void ConfigureSpawnMarkersInChildren(Transform root)
    {
        if (root == null)
            return;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (!IsSpawnMarkerName(candidate.name))
                continue;

            SpawnCheckpoint checkpoint = candidate.GetComponent<SpawnCheckpoint>();
            if (checkpoint == null)
                checkpoint = candidate.gameObject.AddComponent<SpawnCheckpoint>();

            checkpoint.ConfigureRuntimeMarker(makeSpawnMarkersTriggers);
        }
    }

    private void PlacePlayerAtSceneSpawn(Scene scene)
    {
        SlimePlayerAbilities player = FindPlayer();
        if (player == null)
            return;

        Vector3 spawnPosition;
        if (!TryGetSpawnPosition(scene, out spawnPosition))
            return;

        MovePlayerTo(player, spawnPosition);
    }

    private bool TryGetSpawnPosition(Scene scene, out Vector3 spawnPosition)
    {
        string sceneKey = GetSceneKey(scene);
        if (CheckpointsByScene.TryGetValue(sceneKey, out spawnPosition))
            return true;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (TryFindFirstSpawnInChildren(roots[i].transform, out spawnPosition))
                return true;
        }

        spawnPosition = Vector3.zero;
        return false;
    }

    private bool TryFindFirstSpawnInChildren(Transform root, out Vector3 spawnPosition)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (!IsSpawnMarkerName(transforms[i].name))
                continue;

            spawnPosition = transforms[i].position;
            return true;
        }

        spawnPosition = Vector3.zero;
        return false;
    }

    private static void MovePlayerTo(SlimePlayerAbilities player, Vector3 position)
    {
        Transform playerTransform = player.transform;
        Vector3 delta = position - playerTransform.position;
        Rigidbody[] bodies = player.GetComponentsInChildren<Rigidbody>(true);
        Vector3[] bodyPositions = new Vector3[bodies.Length];

        for (int i = 0; i < bodies.Length; i++)
            bodyPositions[i] = bodies[i].position + delta;

        playerTransform.position = position;

        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody body = bodies[i];
            if (body == null)
                continue;

            body.position = bodyPositions[i];
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.Sleep();
        }

        Physics.SyncTransforms();
    }

    private static SlimePlayerAbilities FindPlayer()
    {
        return FindFirstObjectByType<SlimePlayerAbilities>();
    }

    private static bool IsSpawnMarkerName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        string trimmedName = objectName.Trim();
        if (string.Equals(trimmedName, SpawnMarkerName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!trimmedName.StartsWith(SpawnMarkerName, StringComparison.OrdinalIgnoreCase))
            return false;

        char suffix = trimmedName.Length > SpawnMarkerName.Length
            ? trimmedName[SpawnMarkerName.Length]
            : '\0';

        return suffix == ' ' || suffix == '(' || suffix == '_' || suffix == '-';
    }

    private static string GetSceneKey(Scene scene)
    {
        return !string.IsNullOrEmpty(scene.path) ? scene.path : scene.name;
    }
}

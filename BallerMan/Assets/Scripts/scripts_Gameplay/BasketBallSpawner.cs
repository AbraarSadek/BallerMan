using UnityEngine;
using System.Collections;

public class BasketBallSpawner : MonoBehaviour
{
    [Header("Prefab To Spawn")]
    public GameObject prefabToSpawn;

    [Header("Spawn Timing")]
    public float minSpawnTime = 1f;
    public float maxSpawnTime = 5f;

    [Header("Spawn Zone Size")]
    public Vector3 spawnArea = new Vector3(10f, 1f, 10f);

    void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        while (true)
        {
            // Wait random amount of time
            float waitTime = Random.Range(minSpawnTime, maxSpawnTime);
            yield return new WaitForSeconds(waitTime);

            SpawnObject();
        }
    }

    void SpawnObject()
    {
        // Pick random point inside spawn box
        Vector3 randomPosition = transform.position + new Vector3(
            Random.Range(-spawnArea.x / 2, spawnArea.x / 2),
            Random.Range(-spawnArea.y / 2, spawnArea.y / 2),
            Random.Range(-spawnArea.z / 2, spawnArea.z / 2)
        );

        Instantiate(prefabToSpawn, randomPosition, Quaternion.identity);
    }

    // Draw spawn zone in Scene view
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, spawnArea);
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TreePlacer : MonoBehaviour
{
    [Header("Main Settings")]
    public Collider groundCollider;
    public GameObject[] treePrefabs;
    [Range(0.1f, 10f)] public float treeDensity = 0.1f; // Trees per 100 sq units
    [Range(0f, 60f)] public float maxSlopeAngle = 45f;
//  public LayerMask groundLayer;

    [Header("Placement Settings")]
    public float minScale = 0.8f;
    public float maxScale = 1.2f;
    public float minHeight = 10f;
    public float maxHeight = 200f;

    [Header("Optimization")]
    public int maxTreesPerFrame = 20;
    public int maxAttemptsFactor = 10;

    private Bounds meshBounds;
    private float surfaceArea;
    private int targetTreeCount;
    private int placedTrees;
    private int totalAttempts;
    private int maxAttempts;

    public bool isStartPlacing = false;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if(isStartPlacing)
        {
            if (placedTrees >= targetTreeCount || totalAttempts >= maxAttempts)
                return;

            GenerateTreesPerFrame();
        }
    }

    void GenerateTreesPerFrame()
    {
        int treesPlacedThisFrame = 0;
        int attemptsThisFrame = 0;
        int maxAttemptsThisFrame = maxTreesPerFrame * 3;

        while (treesPlacedThisFrame < maxTreesPerFrame &&
               attemptsThisFrame < maxAttemptsThisFrame &&
               placedTrees < targetTreeCount &&
               totalAttempts < maxAttempts)
        {
            attemptsThisFrame++;
            totalAttempts++;

            if (TryPlaceTree())
                treesPlacedThisFrame++;
        }

        if (placedTrees >= targetTreeCount)
            Debug.Log($"Forest generation complete: {placedTrees} trees placed");
        else if (totalAttempts >= maxAttempts)
            Debug.LogWarning($"Generation stopped: Max attempts reached ({placedTrees}/{targetTreeCount} trees placed)");
    }

    bool TryPlaceTree()
    {
        // Generate random point above ground
        Vector3 rayOrigin = new Vector3(
            Random.Range(meshBounds.min.x, meshBounds.max.x),
            meshBounds.max.y + Random.Range(minHeight, maxHeight),
            Random.Range(meshBounds.min.z, meshBounds.max.z)
        );

        // Cast ray downward
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxHeight * 2/*, groundLayer*/))
        {
            // Validate position
            if (IsValidPosition(hit) && hit.transform.gameObject == gameObject)
            {
                PlaceTree(hit.point);
                return true;
            }
        }

        return false;
    }

    bool IsValidPosition(RaycastHit hit)
    {
        // Slope check
        float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
        return slopeAngle <= maxSlopeAngle;
    }

    void PlaceTree(Vector3 position)
    {
        if (treePrefabs.Length == 0) return;

        GameObject treePrefab = treePrefabs[Random.Range(0, treePrefabs.Length)];
        GameObject tree = Instantiate(treePrefab, position, Quaternion.identity);

        // Random rotation and scale
        tree.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        float scale = Random.Range(minScale, maxScale);
        tree.transform.localScale = new Vector3(scale, scale, scale);

        // Optimization
        tree.isStatic = true;
        tree.transform.SetParent(transform);
        placedTrees++;
    }

    public void GenerateTree(Landuse curLanduse, GameObject[] treePrefabssets)
    {
        groundCollider = curLanduse.GetComponent<Collider>();

        if (groundCollider == null)
        {
            Debug.LogError("Ground collider not assigned on landuse: " + curLanduse.Id);
            return;
        }

        treePrefabs = treePrefabssets;

        InitializeGeneration();

        isStartPlacing = true;
    }

    void InitializeGeneration()
    {
        // Calculate surface area
        meshBounds = groundCollider.bounds;
        surfaceArea = CalculateSurfaceArea(meshBounds);

        // Calculate target tree count based on density
        targetTreeCount = Mathf.RoundToInt(surfaceArea * treeDensity / 100f);
        Debug.Log($"Generating forest: {targetTreeCount} trees on {surfaceArea:F0} sq units");

        placedTrees = 0;
        totalAttempts = 0;
        maxAttempts = targetTreeCount * maxAttemptsFactor;
    }

    float CalculateSurfaceArea(Bounds bounds)
    {
        // Approximate surface area using bounding box
        Vector3 size = bounds.size;
        return (size.x * size.z) + (size.x * size.y) + (size.y * size.z);
    }
}

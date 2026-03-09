using UnityEngine;

public class AgressiveOptimise : MonoBehaviour
{
    public bool isMastOptimise = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(isMastOptimise)
        {
            // В консоли Unity:
            GeneratorController.SetAggressiveMode();
            BuildingMakerOptimizations.SetAggressiveSettings();

            isMastOptimise = false;
        }

    }
}

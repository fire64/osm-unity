using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class InteractiveObject : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float interactionRange = 10f;
    public float approachDistance = 0.5f;
    public abstract bool IsAvailable();
    public abstract void Interact(AdvancedAIPedestrianController pedestrian);
    public abstract void StopInteract(AdvancedAIPedestrianController pedestrian);
    public abstract Vector3 GetApproachPosition();
    public abstract Quaternion GetApproachRotation();

    public abstract float GetDurationAction();
}

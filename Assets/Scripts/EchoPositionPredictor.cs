using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EchoPositionPredictor : MonoBehaviour
{
    [Header("Prediction Settings")]
    [SerializeField] private float predictionTime = 0.1f;

    private Vector3 previousPosition;
    private Vector3 predictedPosition;
    private float lastUpdateTime;

    void Start()
    {
        previousPosition = transform.position;
        lastUpdateTime = Time.time;
    }
    
    void Update()
    {
        if (float.IsNaN(predictedPosition.x) || float.IsNaN(predictedPosition.y) || float.IsNaN(predictedPosition.z) ||
            float.IsInfinity(predictedPosition.x) || float.IsInfinity(predictedPosition.y) || float.IsInfinity(predictedPosition.z))
        {
            return; 
        }
        
        transform.position = predictedPosition;
    }

    public void OnServerPositionChanged(Vector3 newPosition)
    {
        predictedPosition = PredictLinearForwardPosition(newPosition, Time.time - lastUpdateTime);

        lastUpdateTime = Time.time;
        previousPosition = newPosition;
    }

    private Vector3 PredictLinearForwardPosition(Vector3 newPosition, float timeDelta)
    {
        float distanceMoved = Vector3.Distance(previousPosition, newPosition);
        float velocity = distanceMoved / timeDelta;
        
        Vector3 movementDirection = (newPosition - previousPosition).normalized;
        float predictedDistance = velocity * predictionTime;
        
        return newPosition + (movementDirection * predictedDistance);
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EchoPositionPredictor : MonoBehaviour
{
    [Header("Prediction Settings")]
    [SerializeField] private float predictionTime = 4.5f; // Predict 4.5 seconds ahead (adjust as needed)

    private Vector3 previousPosition;
    private Vector3 predictedPosition;
    private float lastUpdateTime;

    private bool hasReceivedUpdate = false;

    void Start()
    {
        previousPosition = transform.position;
        lastUpdateTime = Time.time;
    }
    
    void LateUpdate()
    {
        if (hasReceivedUpdate)
        {
            transform.position = predictedPosition;
            hasReceivedUpdate = false;
        }
    }

    public void OnServerPositionChanged(Vector3 newPosition)
    {
        predictedPosition = PredictLinearForwardPosition(newPosition, Time.time - lastUpdateTime);

        lastUpdateTime = Time.time;
        previousPosition = newPosition;
        hasReceivedUpdate = true;
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
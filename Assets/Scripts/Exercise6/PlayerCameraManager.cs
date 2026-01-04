using Unity.Netcode;
using UnityEngine;

public class PlayerCameraManager : NetworkBehaviour
{
    [SerializeField] private Camera topDownCamera;
    [SerializeField] private Camera thirdPersonCamera;
    [SerializeField] private Follow topDownCameraFollowScript;
    [SerializeField] private Follow thirdPersonCameraFollowScript;
    [SerializeField] private GameObject thirdPersonCameraTarget;

    public override void OnNetworkSpawn()
    {
        var isMine = IsOwner;

        // Enable only for local player, disable for others
        if (topDownCamera != null)
            topDownCamera.enabled = isMine;

        if (thirdPersonCamera != null)
            thirdPersonCamera.enabled = isMine;

        if (topDownCameraFollowScript != null)
        {
            topDownCameraFollowScript.enabled = isMine;
            if (isMine)
                topDownCameraFollowScript.target = gameObject;
        }

        if (thirdPersonCameraFollowScript != null)
        {
            thirdPersonCameraFollowScript.enabled = isMine;
            if (isMine)
                thirdPersonCameraFollowScript.target = thirdPersonCameraTarget;
        }
    }
}
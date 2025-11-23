using ParrelSync;
using UnityEngine;

namespace Kart
{
    public class Globals : MonoBehaviour
    {
        public static NetworkingRole networkingRole = NetworkingRole.None;
        public static NetworkingRole networkingRoleSuperset = NetworkingRole.None;

        private void Awake()
        {
            //Is this unity editor instance opening a clone project?
            if (ClonesManager.IsClone())
            {
                Debug.Log("This is a clone project.");
                // Get the custom argument for this clone project.  
                var customArgument = ClonesManager.GetArgument();
                // Do whatever you need with the argument string.
                Debug.Log("The custom argument of this clone project is: " + customArgument);
                switch (customArgument)
                {
                    case "IsShard1":
                        networkingRole = NetworkingRole.IsShard1;
                        networkingRoleSuperset = NetworkingRole.IsShard;
                        break;

                    case "IsShard2":
                        networkingRole = NetworkingRole.IsShard2;
                        networkingRoleSuperset = NetworkingRole.IsShard;
                        break;

                    case "IsClient1":
                        networkingRole = NetworkingRole.IsClient1;
                        networkingRoleSuperset = NetworkingRole.IsClient;
                        break;

                    case "IsClient2":
                        networkingRole = NetworkingRole.IsClient2;
                        networkingRoleSuperset = NetworkingRole.IsClient;
                        break;
                }
            }
            else
            {
                networkingRole = NetworkingRole.IsProxy;
                Debug.Log("The custom argument of this clone project is: " + networkingRole);
            }
        }
    }

    public enum NetworkingRole
    {
        None,
        IsProxy,
        IsShard1,
        IsShard2,
        IsClient1,
        IsClient2,
        IsShard,
        IsClient
    }
}
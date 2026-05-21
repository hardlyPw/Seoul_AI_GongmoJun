using Unity.Netcode.Components;
using UnityEngine;

namespace Seoul.Network.Game
{
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}

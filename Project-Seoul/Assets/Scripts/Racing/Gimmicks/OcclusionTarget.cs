using UnityEngine;

namespace Seoul.Network.Game
{
    public sealed class OcclusionTarget : MonoBehaviour
    {
        [SerializeField] private Renderer[] renderers;

        public Renderer[] Renderers
        {
            get => renderers;
            set => renderers = value;
        }
    }
}

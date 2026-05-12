using UnityEditor.ShaderGraph.Serialization;
using UnityEngine;

namespace UnityEditor.Rendering.Universal.ShaderGraph
{
    [System.Serializable]
    internal sealed class CanvasBlendData : JsonObject
    {
        [SerializeField]
        private AlphaMode blendMode = AlphaMode.Premultiply;
        [SerializeField]
        private bool allowMaterialBlendOverride;

        public AlphaMode BlendMode
        {
            get => blendMode;
            set => blendMode = value;
        }

        public bool AllowMaterialBlendOverride
        {
            get => allowMaterialBlendOverride;
            set => allowMaterialBlendOverride = value;
        }
    }
}

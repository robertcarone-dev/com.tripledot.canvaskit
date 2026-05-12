using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tripledot.CanvasKit
{
    [Serializable]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class CanvasKitResourcesURP : IRenderPipelineResources
    {
        [SerializeField, HideInInspector]
        private int version;

        int IRenderPipelineGraphicsSettings.version => version;

        bool IRenderPipelineGraphicsSettings.isAvailableInPlayerBuild => true;

        [SerializeField]
        [ResourcePath("Shaders/TextMeshPro/TextLayerCore.shader")]
        private Shader textMeshProCoreShader;

        public Shader TextMeshProCoreShader {
            get => textMeshProCoreShader;
            set => this.SetValueAndNotify(ref textMeshProCoreShader, value);
        }
    }
}

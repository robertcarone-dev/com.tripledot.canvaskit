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
        [SerializeField]
        [HideInInspector]
        private int version;

        int IRenderPipelineGraphicsSettings.version => version;

        bool IRenderPipelineGraphicsSettings.isAvailableInPlayerBuild => true;

        [SerializeField]
        [ResourcePath("Shaders/TextMeshPro/TextLayerCore.shader")]
        private Shader textMeshProCoreShader;
        [SerializeField]
        [ResourcePath("Shaders/ImageLattice.shader")]
        private Shader imageLatticeShader;
        [SerializeField]
        [ResourcePath("Materials/ImageLatticeDefault.mat")]
        private Material imageLatticeDefaultMaterial;

        public Shader TextMeshProCoreShader {
            get => textMeshProCoreShader;
            set => this.SetValueAndNotify(ref textMeshProCoreShader, value);
        }

        public Shader ImageLatticeShader {
            get => imageLatticeShader;
            set => this.SetValueAndNotify(ref imageLatticeShader, value);
        }

        public Material ImageLatticeDefaultMaterial {
            get => imageLatticeDefaultMaterial;
            set => this.SetValueAndNotify(ref imageLatticeDefaultMaterial, value);
        }
    }
}
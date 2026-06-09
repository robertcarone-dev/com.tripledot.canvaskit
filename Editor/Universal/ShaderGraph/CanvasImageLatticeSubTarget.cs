using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

namespace UnityEditor.Rendering.Universal.ShaderGraph
{
    internal sealed class CanvasImageLatticeSubTarget : UniversalCanvasSubTarget
    {
        private static readonly GUID SourceCodeGuid = new GUID("c8ae7aa10ab64e5895e84d030c6ec721");
        private const string CanvasImageLatticePass = "Packages/com.tripledot.canvaskit/Editor/Universal/ShaderGraph/Includes/CanvasKitImageLatticePass.hlsl";

        protected override IncludeCollection postgraphIncludes => new IncludeCollection {
            { CanvasImageLatticePass, IncludeLocation.Postgraph },
        };

        public CanvasImageLatticeSubTarget()
        {
            displayName = "Canvas Image Lattice";
        }

        public override void Setup(ref TargetSetupContext context)
        {
            base.Setup(ref context);
            context.AddAssetDependency(SourceCodeGuid, AssetCollection.Flags.SourceDependency);
        }

        public override void CollectShaderProperties(PropertyCollector collector, GenerationMode generationMode)
        {
            base.CollectShaderProperties(collector, generationMode);
            collector.AddShaderProperty(Properties.ClipRect);
            collector.AddShaderProperty(Properties.UIMaskSoftnessX);
            collector.AddShaderProperty(Properties.UIMaskSoftnessY);
            collector.AddShaderProperty(Properties.AlphaTex);
            collector.AddShaderProperty(Properties.LatticeGrid);
        }

        public override PassDescriptor GenerateUIPassDescriptor(bool isSRP)
        {
            var passDescriptor = base.GenerateUIPassDescriptor(isSRP);
            passDescriptor.pragmas = new PragmaCollection {
                { Pragma.Target(ShaderModel.Target30) },
                { Pragma.Vertex("vert") },
                { Pragma.Fragment("frag") },
            };
            return passDescriptor;
        }

        private static class Properties
        {
            public static readonly Vector4ShaderProperty ClipRect = new Vector4ShaderProperty {
                overrideReferenceName = "_ClipRect",
                displayName = "Clip Rect",
                hidden = true,
                generatePropertyBlock = true,
                value = new Vector4(-32767f, -32767f, 32767f, 32767f),
                overrideHLSLDeclaration = false,
            };

            public static readonly Vector1ShaderProperty UIMaskSoftnessX = new Vector1ShaderProperty {
                overrideReferenceName = "_UIMaskSoftnessX",
                displayName = "UI Mask Softness X",
                hidden = true,
                generatePropertyBlock = true,
                value = 1f,
                overrideHLSLDeclaration = false,
            };

            public static readonly Vector1ShaderProperty UIMaskSoftnessY = new Vector1ShaderProperty {
                overrideReferenceName = "_UIMaskSoftnessY",
                displayName = "UI Mask Softness Y",
                hidden = true,
                generatePropertyBlock = true,
                value = 1f,
                overrideHLSLDeclaration = false,
            };

            public static readonly Texture2DShaderProperty AlphaTex = new Texture2DShaderProperty {
                overrideReferenceName = "_AlphaTex",
                displayName = "Alpha Texture",
                hidden = true,
                generatePropertyBlock = true,
                defaultType = Texture2DShaderProperty.DefaultType.White,
                value = new SerializableTexture(),
                overrideHLSLDeclaration = false,
            };

            public static readonly Vector4ShaderProperty LatticeGrid = new Vector4ShaderProperty {
                overrideReferenceName = "_LatticeGrid",
                displayName = "Lattice Grid",
                hidden = true,
                generatePropertyBlock = true,
                value = new Vector4(3f, 3f, 0f, 0f),
                overrideHLSLDeclaration = true,
                hlslDeclarationOverride = HLSLDeclaration.UnityPerMaterial,
            };
        }
    }
}

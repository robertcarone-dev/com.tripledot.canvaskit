using System;
using UnityEditor.Rendering.Canvas.ShaderGraph;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using ShaderGraphBlend = UnityEditor.ShaderGraph.Blend;

namespace UnityEditor.Rendering.Universal.ShaderGraph
{
    internal sealed class CanvasBlendSubTarget :
        UniversalCanvasSubTarget,
        IRequiresData<CanvasBlendData>
    {
        private static readonly GUID SourceCodeGuid = new GUID("b6b2ee10318b443b9cda81758d49228d");
        private const string CanvasBlendPass = "Packages/com.tripledot.canvaskit/Editor/Universal/ShaderGraph/Includes/CanvasKitBlendPass.hlsl";

        private static readonly KeywordDescriptor kMaterialOverrideDefine = CreateDefine("_CANVASKIT_BLEND_MATERIAL_OVERRIDE");
        private static readonly KeywordDescriptor kAlphaDefine = CreateDefine("_CANVASKIT_BLEND_ALPHA");
        private static readonly KeywordDescriptor kPremultiplyDefine = CreateDefine("_CANVASKIT_BLEND_PREMULTIPLY");
        private static readonly KeywordDescriptor kAdditiveDefine = CreateDefine("_CANVASKIT_BLEND_ADDITIVE");
        private static readonly KeywordDescriptor kMultiplyDefine = CreateDefine("_CANVASKIT_BLEND_MULTIPLY");

        private CanvasBlendData blendData;

        CanvasBlendData IRequiresData<CanvasBlendData>.data
        {
            get => blendData;
            set => blendData = value;
        }

        internal AlphaMode BlendMode
        {
            get => EnsureBlendData().BlendMode;
            set => EnsureBlendData().BlendMode = value;
        }

        internal bool AllowMaterialBlendOverride
        {
            get => EnsureBlendData().AllowMaterialBlendOverride;
            set => EnsureBlendData().AllowMaterialBlendOverride = value;
        }

        protected override IncludeCollection postgraphIncludes => new IncludeCollection {
            { CanvasBlendPass, IncludeLocation.Postgraph },
        };

        public CanvasBlendSubTarget()
        {
            displayName = "Canvas (Blend Modes)";
        }

        public override void Setup(ref TargetSetupContext context)
        {
            base.Setup(ref context);
            context.AddAssetDependency(SourceCodeGuid, AssetCollection.Flags.SourceDependency);

            var universalRPType = typeof(UniversalRenderPipelineAsset);
            if (!context.HasCustomEditorForRenderPipeline(universalRPType)) {
                context.AddCustomEditorForRenderPipeline(typeof(CanvasBlendShaderGUI).FullName, universalRPType);
            }
        }

        public override void GetFields(ref TargetFieldContext context)
        {
            base.GetFields(ref context);

            if (AllowMaterialBlendOverride) {
                return;
            }

            switch (BlendMode) {
                case AlphaMode.Premultiply:
                    context.AddField(UniversalFields.BlendPremultiply);
                    break;
                case AlphaMode.Additive:
                    context.AddField(UniversalFields.BlendAdd);
                    break;
                case AlphaMode.Multiply:
                    context.AddField(UniversalFields.BlendMultiply);
                    break;
                default:
                    context.AddField(Fields.BlendAlpha);
                    break;
            }
        }

        protected override DefineCollection GetAdditionalDefines()
        {
            var result = base.GetAdditionalDefines();

            if (AllowMaterialBlendOverride) {
                result.Add(kMaterialOverrideDefine, 1);
                return result;
            }

            switch (BlendMode) {
                case AlphaMode.Alpha:
                    result.Add(kAlphaDefine, 1);
                    break;
                case AlphaMode.Premultiply:
                    result.Add(kPremultiplyDefine, 1);
                    break;
                case AlphaMode.Additive:
                    result.Add(kAdditiveDefine, 1);
                    break;
                case AlphaMode.Multiply:
                    result.Add(kMultiplyDefine, 1);
                    break;
            }

            return result;
        }

        public override PassDescriptor GenerateUIPassDescriptor(bool isSRP)
        {
            var passDescriptor = base.GenerateUIPassDescriptor(isSRP);
            passDescriptor.renderStates = GenerateRenderStateDeclaration();
            return passDescriptor;
        }

        public override void CollectShaderProperties(PropertyCollector collector, GenerationMode generationMode)
        {
            base.CollectShaderProperties(collector, generationMode);

            if (!AllowMaterialBlendOverride) {
                return;
            }

            AddHiddenFloatProperty(collector, Property.BlendMode, (float)BlendMode, HLSLDeclaration.UnityPerMaterial);
            AddHiddenFloatProperty(collector, Property.SrcBlend, 1.0f);
            AddHiddenFloatProperty(collector, Property.DstBlend, 0.0f);
            AddHiddenFloatProperty(collector, Property.SrcBlendAlpha, 1.0f);
            AddHiddenFloatProperty(collector, Property.DstBlendAlpha, 0.0f);
        }

        public override void GetPropertiesGUI(ref TargetPropertyGUIContext context, Action onChange, Action<string> registerUndo)
        {
            context.AddProperty("Blending Mode", new EnumField(AlphaMode.Premultiply) { value = BlendMode }, evt => {
                if (Equals(BlendMode, evt.newValue)) {
                    return;
                }

                registerUndo("Change Canvas Blend Mode");
                BlendMode = (AlphaMode)evt.newValue;
                onChange();
            });

            context.AddProperty("Allow Material Blend Override", new Toggle { value = AllowMaterialBlendOverride }, evt => {
                if (Equals(AllowMaterialBlendOverride, evt.newValue)) {
                    return;
                }

                registerUndo("Change Canvas Material Blend Override");
                AllowMaterialBlendOverride = evt.newValue;
                onChange();
            });

            base.GetPropertiesGUI(ref context, onChange, registerUndo);
        }

        private RenderStateCollection GenerateRenderStateDeclaration()
        {
            var result = new RenderStateCollection {
                { RenderState.Cull(Cull.Off) },
                { RenderState.ZWrite(ZWrite.Off) },
                { RenderState.ZTest(CanvasUniforms.ZTest) },
                { RenderState.ColorMask(CanvasUniforms.ColorMask) },
                {
                    RenderState.Stencil(new StencilDescriptor {
                        Ref = CanvasUniforms.Ref,
                        Comp = CanvasUniforms.Comp,
                        Pass = CanvasUniforms.Pass,
                        ReadMask = CanvasUniforms.ReadMask,
                        WriteMask = CanvasUniforms.WriteMask,
                    })
                },
            };

            if (AllowMaterialBlendOverride) {
                result.Add(RenderState.Blend("[_SrcBlend]", "[_DstBlend]", "[_SrcBlendAlpha]", "[_DstBlendAlpha]"));
            } else {
                result.Add(CreateBlendRenderState(BlendMode));
            }

            return result;
        }

        private static RenderStateDescriptor CreateBlendRenderState(AlphaMode mode)
        {
            switch (mode) {
                case AlphaMode.Alpha:
                    return RenderState.Blend(ShaderGraphBlend.SrcAlpha, ShaderGraphBlend.OneMinusSrcAlpha, ShaderGraphBlend.One, ShaderGraphBlend.OneMinusSrcAlpha);
                case AlphaMode.Premultiply:
                    return RenderState.Blend(ShaderGraphBlend.One, ShaderGraphBlend.OneMinusSrcAlpha, ShaderGraphBlend.One, ShaderGraphBlend.OneMinusSrcAlpha);
                case AlphaMode.Additive:
                    return RenderState.Blend(ShaderGraphBlend.SrcAlpha, ShaderGraphBlend.One, ShaderGraphBlend.One, ShaderGraphBlend.One);
                case AlphaMode.Multiply:
                    return RenderState.Blend(ShaderGraphBlend.DstColor, ShaderGraphBlend.Zero, ShaderGraphBlend.Zero, ShaderGraphBlend.One);
                default:
                    return RenderState.Blend(ShaderGraphBlend.One, ShaderGraphBlend.OneMinusSrcAlpha, ShaderGraphBlend.One, ShaderGraphBlend.OneMinusSrcAlpha);
            }
        }

        private CanvasBlendData EnsureBlendData()
        {
            return blendData ??= new CanvasBlendData();
        }

        private static KeywordDescriptor CreateDefine(string referenceName)
        {
            return new KeywordDescriptor {
                displayName = referenceName,
                referenceName = referenceName,
                type = KeywordType.Boolean,
                definition = KeywordDefinition.Predefined,
                scope = KeywordScope.Local,
                stages = KeywordShaderStage.All,
            };
        }

        private static void AddHiddenFloatProperty(PropertyCollector collector, string referenceName, float defaultValue, HLSLDeclaration declaration)
        {
            collector.AddShaderProperty(new Vector1ShaderProperty {
                floatType = FloatType.Default,
                hidden = true,
                overrideHLSLDeclaration = true,
                hlslDeclarationOverride = declaration,
                value = defaultValue,
                generatePropertyBlock = true,
                overrideReferenceName = referenceName,
            });
        }

        private static void AddHiddenFloatProperty(PropertyCollector collector, string referenceName, float defaultValue)
        {
            AddHiddenFloatProperty(collector, referenceName, defaultValue, HLSLDeclaration.DoNotDeclare);
        }
    }
}

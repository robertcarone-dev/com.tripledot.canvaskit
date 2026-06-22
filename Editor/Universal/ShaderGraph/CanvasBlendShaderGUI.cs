using System;
using UnityEditor.ShaderGraph.Drawing;
using UnityEngine;
using EngineBlendMode = UnityEngine.Rendering.BlendMode;

namespace UnityEditor.Rendering.Universal.ShaderGraph
{
    public sealed class CanvasBlendShaderGUI : UnityEditor.ShaderGUI
    {
        [Flags]
        private enum Expandable
        {
            SurfaceOptions = 1 << 0,
            SurfaceInputs = 1 << 1,
        }

        private static class Styles
        {
            public static readonly GUIContent SurfaceOptions = L10n.TextContent("Surface Options");
            public static readonly GUIContent SurfaceInputs = L10n.TextContent("Surface Inputs");
            public static readonly GUIContent BlendingMode = L10n.TextContent("Blending Mode");
            public static readonly string[] BlendModeNames = Enum.GetNames(typeof(AlphaMode));
        }

        private readonly MaterialHeaderScopeList materialScopeList = new MaterialHeaderScopeList(uint.MaxValue);
        private bool firstTimeApply = true;
        private MaterialEditor materialEditor;
        private MaterialProperty[] properties;
        private MaterialProperty blendModeProperty;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            this.materialEditor = materialEditor;
            this.properties = properties;
            blendModeProperty = FindProperty(Property.BlendMode, properties, false);

            var material = materialEditor.target as Material;
            if (firstTimeApply) {
                OnOpenGUI();
                ValidateMaterial(material);
                firstTimeApply = false;
            }

            materialScopeList.DrawHeaders(materialEditor, material);
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            material.shaderKeywords = null;
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            UpdateMaterialBlendMode(material);
        }

        public override void ValidateMaterial(Material material)
        {
            UpdateMaterialBlendMode(material);
        }

        public static void UpdateMaterialBlendMode(Material material)
        {
            if (!material.HasProperty(Property.BlendMode)) {
                return;
            }

            var mode = (AlphaMode)material.GetFloat(Property.BlendMode);
            GetBlendFactors(mode, out var srcBlend, out var dstBlend, out var srcBlendAlpha, out var dstBlendAlpha);

            SetFloatIfPresent(material, Property.SrcBlend, (float)srcBlend);
            SetFloatIfPresent(material, Property.DstBlend, (float)dstBlend);
            SetFloatIfPresent(material, Property.SrcBlendAlpha, (float)srcBlendAlpha);
            SetFloatIfPresent(material, Property.DstBlendAlpha, (float)dstBlendAlpha);
        }

        internal static void GetBlendFactors(
            AlphaMode mode,
            out EngineBlendMode srcBlend,
            out EngineBlendMode dstBlend,
            out EngineBlendMode srcBlendAlpha,
            out EngineBlendMode dstBlendAlpha)
        {
            srcBlend = EngineBlendMode.One;
            dstBlend = EngineBlendMode.OneMinusSrcAlpha;
            srcBlendAlpha = EngineBlendMode.One;
            dstBlendAlpha = EngineBlendMode.OneMinusSrcAlpha;

            switch (mode) {
                case AlphaMode.Alpha:
                    srcBlend = EngineBlendMode.SrcAlpha;
                    dstBlend = EngineBlendMode.OneMinusSrcAlpha;
                    srcBlendAlpha = EngineBlendMode.One;
                    dstBlendAlpha = EngineBlendMode.OneMinusSrcAlpha;
                    break;
                case AlphaMode.Premultiply:
                    srcBlend = EngineBlendMode.One;
                    dstBlend = EngineBlendMode.OneMinusSrcAlpha;
                    srcBlendAlpha = EngineBlendMode.One;
                    dstBlendAlpha = EngineBlendMode.OneMinusSrcAlpha;
                    break;
                case AlphaMode.Additive:
                    srcBlend = EngineBlendMode.SrcAlpha;
                    dstBlend = EngineBlendMode.One;
                    srcBlendAlpha = EngineBlendMode.One;
                    dstBlendAlpha = EngineBlendMode.One;
                    break;
                case AlphaMode.Multiply:
                    srcBlend = EngineBlendMode.DstColor;
                    dstBlend = EngineBlendMode.Zero;
                    srcBlendAlpha = EngineBlendMode.Zero;
                    dstBlendAlpha = EngineBlendMode.One;
                    break;
            }
        }

        private void OnOpenGUI()
        {
            if (blendModeProperty != null) {
                materialScopeList.RegisterHeaderScope(Styles.SurfaceOptions, (uint)Expandable.SurfaceOptions, DrawSurfaceOptions);
            }

            materialScopeList.RegisterHeaderScope(Styles.SurfaceInputs, (uint)Expandable.SurfaceInputs, DrawSurfaceInputs);
        }

        private void DrawSurfaceOptions(Material material)
        {
            EditorGUI.BeginChangeCheck();
            var selected = EditorGUILayout.Popup(Styles.BlendingMode, (int)blendModeProperty.floatValue, Styles.BlendModeNames);
            if (EditorGUI.EndChangeCheck()) {
                blendModeProperty.floatValue = selected;
                UpdateMaterialBlendMode(material);
            }
        }

        private void DrawSurfaceInputs(Material material)
        {
            ShaderGraphPropertyDrawers.DrawShaderGraphGUI(materialEditor, properties);
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName)) {
                material.SetFloat(propertyName, value);
            }
        }
    }
}
using System;
using UnityEditor.ShaderGraph;
using UnityEngine.Rendering;

namespace UnityEditor.Rendering.Universal.ShaderGraph
{
    internal static class CreateCanvasBlendShaderGraph
    {
        [MenuItem("Assets/Create/Shader Graph/URP/Canvas (Blend) Shader Graph", priority = CoreUtils.Sections.section5 + CoreUtils.Priorities.assetsCreateShaderMenuPriority + 1)]
        public static void CreateCanvasGraph()
        {
            var target = (UniversalTarget)Activator.CreateInstance(typeof(UniversalTarget));
            target.TrySetActiveSubTarget(typeof(CanvasBlendSubTarget));

            var blockDescriptors = new[] {
                BlockFields.SurfaceDescription.BaseColor,
                BlockFields.SurfaceDescription.Emission,
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
            };

            GraphUtil.CreateNewGraphWithOutputs(new Target[] { target }, blockDescriptors);
        }
    }
}
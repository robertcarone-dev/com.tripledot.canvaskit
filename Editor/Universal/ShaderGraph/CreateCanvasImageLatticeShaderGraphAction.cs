using UnityEditor.ProjectWindowCallback;
using UnityEditor.ShaderGraph;
using UnityEngine;

namespace UnityEditor.Rendering.Universal.ShaderGraph
{
    internal sealed class CreateCanvasImageLatticeShaderGraphAction : EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            CreateCanvasImageLatticeShaderGraph.CreateCanvasImageLatticeGraphAtPath(pathName);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Shader>(pathName);
        }
    }
}
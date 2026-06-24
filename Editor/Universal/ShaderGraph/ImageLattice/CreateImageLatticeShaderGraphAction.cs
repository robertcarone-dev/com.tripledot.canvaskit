using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace UnityEditor.Rendering.Universal.ShaderGraph
{
    internal sealed class CreateImageLatticeShaderGraphAction : EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            CreateImageLatticeShaderGraph.CreateCanvasImageLatticeGraphAtPath(pathName);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Shader>(pathName);
        }
    }
}
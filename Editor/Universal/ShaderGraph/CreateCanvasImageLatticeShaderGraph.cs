using System;
using System.Linq;
using UnityEditor.Graphing;
using UnityEditor.ProjectWindowCallback;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEditor.Rendering.Universal.ShaderGraph
{
    internal static class CreateCanvasImageLatticeShaderGraph
    {
        private const string DefaultGraphName = "New Canvas Image Lattice Shader Graph.shadergraph";

        [MenuItem("Assets/Create/Shader Graph/URP/Canvas Image Lattice Shader Graph", priority = CoreUtils.Sections.section5 + CoreUtils.Priorities.assetsCreateShaderMenuPriority + 2)]
        public static void CreateCanvasImageLatticeGraph()
        {
            var graphItem = ScriptableObject.CreateInstance<CreateCanvasImageLatticeShaderGraphAction>();
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, graphItem, DefaultGraphName, null, null);
        }

        internal static Shader CreateCanvasImageLatticeGraphAtPath(string path)
        {
            var graph = CreateCanvasImageLatticeGraphData();
            FileUtilities.WriteShaderGraphToDisk(path, graph);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Shader>(path);
        }

        internal static GraphData CreateCanvasImageLatticeGraphData()
        {
            var graph = new GraphData();
            graph.AddContexts();
            graph.InitializeOutputs(CreateTargets(), CreateBlockDescriptors());
            var category = CategoryData.DefaultCategory();
            graph.AddCategory(category);
            AddDefaultSpriteSample(graph, category);
            graph.path = "Canvas Kit";
            return graph;
        }

        private static Target[] CreateTargets()
        {
            var target = (UniversalTarget)Activator.CreateInstance(typeof(UniversalTarget));
            target.TrySetActiveSubTarget(typeof(CanvasImageLatticeSubTarget));
            return new Target[] { target };
        }

        private static BlockFieldDescriptor[] CreateBlockDescriptors()
        {
            return new[] {
                BlockFields.SurfaceDescription.BaseColor,
                BlockFields.SurfaceDescription.Emission,
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
            };
        }

        private static void AddDefaultSpriteSample(GraphData graph, CategoryData category)
        {
            var mainTexProperty = new Texture2DShaderProperty {
                displayName = "MainTex",
                overrideReferenceName = "_MainTex",
                generatePropertyBlock = true,
                defaultType = Texture2DShaderProperty.DefaultType.White,
                value = new SerializableTexture(),
            };
            graph.AddGraphInput(mainTexProperty);
            category.InsertItemIntoCategory(mainTexProperty);

            var propertyNode = new PropertyNode {
                property = mainTexProperty,
            };
            propertyNode.drawState = WithPosition(propertyNode.drawState, new Rect(-560f, 160f, 120f, 34f));

            var sampleNode = new SampleTexture2DNode();
            sampleNode.drawState = WithPosition(sampleNode.drawState, new Rect(-360f, 100f, 180f, 250f));

            var splitNode = new SplitNode();
            splitNode.drawState = WithPosition(splitNode.drawState, new Rect(-140f, 260f, 120f, 120f));

            graph.AddNode(propertyNode);
            graph.AddNode(sampleNode);
            graph.AddNode(splitNode);

            graph.Connect(propertyNode.GetSlotReference(PropertyNode.OutputSlotId), sampleNode.GetSlotReference(SampleTexture2DNode.TextureInputId));
            graph.Connect(sampleNode.GetSlotReference(SampleTexture2DNode.OutputSlotRGBAId), splitNode.GetSlotReference(SplitNode.InputSlotId));
            graph.Connect(sampleNode.GetSlotReference(SampleTexture2DNode.OutputSlotRGBAId), FindBlock(graph, BlockFields.SurfaceDescription.BaseColor).GetSlotReference(0));
            graph.Connect(splitNode.GetSlotReference(SplitNode.OutputSlotAId), FindBlock(graph, BlockFields.SurfaceDescription.Alpha).GetSlotReference(0));
        }

        private static BlockNode FindBlock(GraphData graph, BlockFieldDescriptor descriptor)
        {
            return graph.GetNodes<BlockNode>().First(block => block.descriptor == descriptor);
        }

        private static DrawState WithPosition(DrawState drawState, Rect position)
        {
            drawState.position = position;
            return drawState;
        }
    }
}
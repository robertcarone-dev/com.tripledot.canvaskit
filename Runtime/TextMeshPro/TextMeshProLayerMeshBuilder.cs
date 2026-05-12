using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tripledot.CanvasKit
{
    internal sealed class TextMeshProLayerMeshBuilder
    {
        #region Fields

        private readonly List<Vector3> vertices = new List<Vector3>(256);
        private readonly List<Vector3> normals = new List<Vector3>(256);
        private readonly List<Color32> colors = new List<Color32>(256);
        private readonly List<Vector4> uv0Upload = new List<Vector4>(256);
        private readonly List<Vector4> uv1Upload = new List<Vector4>(256);
        private readonly List<Vector4> uv2Upload = new List<Vector4>(256);
        private readonly List<List<int>> submeshTriangles = new List<List<int>>();
        private readonly List<VisibleGlyph> visibleGlyphs = new List<VisibleGlyph>(128);

        private Vector3 boundsMin;
        private Vector3 boundsMax;
        private bool hasBounds;

        #endregion

        #region Nested Types

        private readonly struct VisibleGlyph
        {
            internal readonly TMP_MeshInfo MeshInfo;
            internal readonly TMP_CharacterInfo Character;
            internal readonly Rect GlyphUv;
            internal readonly float AtlasWidth;
            internal readonly float AtlasHeight;

            internal VisibleGlyph(TMP_MeshInfo meshInfo, TMP_CharacterInfo character, Rect glyphUv, float atlasWidth, float atlasHeight)
            {
                MeshInfo = meshInfo;
                Character = character;
                GlyphUv = glyphUv;
                AtlasWidth = atlasWidth;
                AtlasHeight = atlasHeight;
            }
        }

        #endregion

        #region Internal API

        internal void Build(Mesh mesh, TMP_TextInfo textInfo, IList<TextMeshProLayerData> layers, float sdfPaddingLimit)
        {
            mesh.Clear(false);
            
            vertices.Clear();
            normals.Clear();
            colors.Clear();
            uv0Upload.Clear();
            uv1Upload.Clear();
            uv2Upload.Clear();
            visibleGlyphs.Clear();
            hasBounds = false;
            
            CollectVisibleGlyphs(textInfo);
            
            var expectedVertexCount = visibleGlyphs.Count * layers.Count * 4;
            mesh.indexFormat = expectedVertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            EnsureUploadCapacity(expectedVertexCount);
            EnsureTriangleBuffers(layers.Count);

            for (int materialSlot = 0; materialSlot < layers.Count; materialSlot++) {
                var layer = GetRenderLayerForSlot(layers, materialSlot);
                var triangles = submeshTriangles[materialSlot];
                triangles.Clear();
                EnsureTriangleCapacity(triangles, visibleGlyphs.Count * 6);

                for (int i = 0; i < visibleGlyphs.Count; i++) {
                    AddCharacterQuad(visibleGlyphs[i], layer, sdfPaddingLimit, triangles);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            
            BuildPaintUVs();
            mesh.SetUVs(0, uv0Upload);
            mesh.SetUVs(1, uv1Upload);
            mesh.SetUVs(2, uv2Upload);
            
            mesh.subMeshCount = layers.Count;
            for (int i = 0; i < layers.Count; i++) {
                mesh.SetTriangles(submeshTriangles[i], i, false);
            }

            mesh.bounds = hasBounds 
                ? new Bounds((boundsMin + boundsMax) * 0.5f, boundsMax - boundsMin)
                : default;
        }

        #endregion

        #region Glyph Collection

        private void CollectVisibleGlyphs(TMP_TextInfo textInfo)
        {
            for (int characterIndex = 0; characterIndex < textInfo.characterCount; characterIndex++) {
                var character = textInfo.characterInfo[characterIndex];
                if (!ShouldRenderCharacter(character)) {
                    continue;
                }

                var meshInfo = textInfo.meshInfo[character.materialReferenceIndex];
                var vertexIndex = character.vertexIndex;
                if (meshInfo.vertices == null || meshInfo.uvs0 == null || meshInfo.vertices.Length < vertexIndex + 4 || meshInfo.uvs0.Length < vertexIndex + 4) {
                    continue;
                }

                if (!TryGetGlyphUvRect(character, out var glyphUv)) {
                    continue;
                }

                visibleGlyphs.Add(new VisibleGlyph(meshInfo, character, glyphUv, GetAtlasWidth(character), GetAtlasHeight(character)));
            }
        }

        #endregion

        #region Mesh Building

        private void AddCharacterQuad(VisibleGlyph glyph, TextMeshProLayerData layer, float sdfPaddingLimit, List<int> triangles)
        {
            var meshInfo = glyph.MeshInfo;
            var character = glyph.Character;
            var vertexIndex = character.vertexIndex;

            var glyphUv = glyph.GlyphUv;
            var atlasWidth = glyph.AtlasWidth;
            var atlasHeight = glyph.AtlasHeight;
            var localUnitsPerAtlasPixel = GetLocalUnitsPerAtlasPixel(meshInfo, vertexIndex, atlasWidth, atlasHeight);
            var layerPadding = layer.GetVisualPadding(sdfPaddingLimit, localUnitsPerAtlasPixel);
            
            var offset = new Vector3(layer.GeometryOffset.x, layer.GeometryOffset.y, 0f);
            var targetUv0 = new Vector2(glyphUv.xMin - layerPadding.x / atlasWidth, glyphUv.yMin - layerPadding.w / atlasHeight);
            var targetUv1 = new Vector2(targetUv0.x, glyphUv.yMax + layerPadding.z / atlasHeight);
            var targetUv2 = new Vector2(glyphUv.xMax + layerPadding.y / atlasWidth, targetUv1.y);
            var targetUv3 = new Vector2(targetUv2.x, targetUv0.y);
            var safeUv = GetSafeGlyphUvRect(glyphUv, sdfPaddingLimit, atlasWidth, atlasHeight);

            var start = vertices.Count;
            AddMappedVertex(meshInfo, vertexIndex, 0, targetUv0, safeUv, offset);
            AddMappedVertex(meshInfo, vertexIndex, 1, targetUv1, safeUv, offset);
            AddMappedVertex(meshInfo, vertexIndex, 2, targetUv2, safeUv, offset);
            AddMappedVertex(meshInfo, vertexIndex, 3, targetUv3, safeUv, offset);

            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
            triangles.Add(start);
        }

        private void AddMappedVertex(TMP_MeshInfo meshInfo, int vertexIndex, int cornerIndex, Vector2 targetUv, Vector4 safeUv, Vector3 offset)
        {
            var sourceVertices = meshInfo.vertices;
            var sourceNormals = meshInfo.normals;
            var sourceColors = meshInfo.colors32;
            var sourceUv0 = meshInfo.uvs0;
            
            var vertex = MapUvToSourceQuad(sourceVertices, sourceUv0, vertexIndex, targetUv) + offset;
            vertices.Add(vertex);
            Encapsulate(vertex);
            
            normals.Add(sourceNormals != null && sourceNormals.Length > vertexIndex + cornerIndex ? sourceNormals[vertexIndex + cornerIndex] : Vector3.back);
            colors.Add(sourceColors != null && sourceColors.Length > vertexIndex + cornerIndex ? sourceColors[vertexIndex + cornerIndex] : (Color32)Color.white);
            
            var sourceUv = sourceUv0[vertexIndex + cornerIndex];
            uv0Upload.Add(new Vector4(targetUv.x, targetUv.y, sourceUv.z, sourceUv.w));
            uv1Upload.Add(safeUv);
        }

        #endregion

        #region Utility

        private static Vector3 MapUvToSourceQuad(Vector3[] sourceVertices, Vector4[] sourceUv0, int vertexIndex, Vector2 targetUv)
        {
            var blUv = sourceUv0[vertexIndex + 0];
            var tlUv = sourceUv0[vertexIndex + 1];
            var trUv = sourceUv0[vertexIndex + 2];
            
            var sourceWidth = trUv.x - blUv.x;
            var sourceHeight = tlUv.y - blUv.y;
            if (Mathf.Abs(sourceWidth) < 0.000001f || Mathf.Abs(sourceHeight) < 0.000001f) {
                return sourceVertices[vertexIndex];
            }

            var u = (targetUv.x - blUv.x) / sourceWidth;
            var v = (targetUv.y - blUv.y) / sourceHeight;
            var bottom = Vector3.LerpUnclamped(sourceVertices[vertexIndex + 0], sourceVertices[vertexIndex + 3], u);
            var top = Vector3.LerpUnclamped(sourceVertices[vertexIndex + 1], sourceVertices[vertexIndex + 2], u);
            
            return Vector3.LerpUnclamped(bottom, top, v);
        }

        private static Vector2 GetLocalUnitsPerAtlasPixel(TMP_MeshInfo meshInfo, int vertexIndex, float atlasWidth, float atlasHeight)
        {
            var sourceVertices = meshInfo.vertices;
            var sourceUv0 = meshInfo.uvs0;
            
            var localWidth = Vector2.Distance(sourceVertices[vertexIndex + 0], sourceVertices[vertexIndex + 3]);
            var localHeight = Vector2.Distance(sourceVertices[vertexIndex + 0], sourceVertices[vertexIndex + 1]);
            
            var atlasPixelWidth = Mathf.Abs(sourceUv0[vertexIndex + 3].x - sourceUv0[vertexIndex + 0].x) * atlasWidth;
            var atlasPixelHeight = Mathf.Abs(sourceUv0[vertexIndex + 1].y - sourceUv0[vertexIndex + 0].y) * atlasHeight;
            
            return new Vector2(GetLocalUnitsPerAtlasPixel(localWidth, atlasPixelWidth), GetLocalUnitsPerAtlasPixel(localHeight, atlasPixelHeight));
        }

        private static float GetLocalUnitsPerAtlasPixel(float localSize, float atlasPixelSize)
        {
            return atlasPixelSize > 0.000001f ? Mathf.Max(0.000001f, localSize / atlasPixelSize) : 1f;
        }

        private static bool TryGetGlyphUvRect(TMP_CharacterInfo character, out Rect uvRect)
        {
            uvRect = default;
            
            var glyph = character.alternativeGlyph != null ? character.alternativeGlyph : character.textElement?.glyph;
            var fontAsset = character.fontAsset;
            
            var atlasWidth = fontAsset != null ? fontAsset.atlasWidth : 0;
            var atlasHeight = fontAsset != null ? fontAsset.atlasHeight : 0;
            if (glyph == null || atlasWidth <= 0 || atlasHeight <= 0) {
                return false;
            }

            var glyphRect = glyph.glyphRect;
            uvRect = Rect.MinMaxRect(
                glyphRect.x / (float)atlasWidth,
                glyphRect.y / (float)atlasHeight,
                (glyphRect.x + glyphRect.width) / (float)atlasWidth,
                (glyphRect.y + glyphRect.height) / (float)atlasHeight);
            
            return uvRect.width > 0f && uvRect.height > 0f;
        }

        private static Vector4 GetSafeGlyphUvRect(Rect glyphUv, float sdfPaddingLimit, float atlasWidth, float atlasHeight)
        {
            return new Vector4(
                Mathf.Clamp01(glyphUv.xMin - sdfPaddingLimit / atlasWidth),
                Mathf.Clamp01(glyphUv.yMin - sdfPaddingLimit / atlasHeight),
                Mathf.Clamp01(glyphUv.xMax + sdfPaddingLimit / atlasWidth),
                Mathf.Clamp01(glyphUv.yMax + sdfPaddingLimit / atlasHeight));
        }

        private static float GetAtlasWidth(TMP_CharacterInfo character)
        {
            return Mathf.Max(1f, character.fontAsset != null ? character.fontAsset.atlasWidth : 1f);
        }

        private static float GetAtlasHeight(TMP_CharacterInfo character)
        {
            return Mathf.Max(1f, character.fontAsset != null ? character.fontAsset.atlasHeight : 1f);
        }

        private static bool ShouldRenderCharacter(TMP_CharacterInfo character)
        {
            // v1 layer meshes are single-atlas: TMP fallback/submaterial glyphs need grouped
            // materials and textures before they can be rendered correctly.
            return character is { isVisible: true, elementType: TMP_TextElementType.Character, materialReferenceIndex: 0 };
        }

        private static TextMeshProLayerData GetRenderLayerForSlot(IList<TextMeshProLayerData> layers, int materialSlot)
        {
            return layers[layers.Count - 1 - materialSlot];
        }

        private void EnsureUploadCapacity(int vertexCount)
        {
            var capacity = Mathf.Max(0, vertexCount);
            EnsureCapacity(vertices, capacity);
            EnsureCapacity(normals, capacity);
            EnsureCapacity(colors, capacity);
            EnsureCapacity(uv0Upload, capacity);
            EnsureCapacity(uv1Upload, capacity);
            EnsureCapacity(uv2Upload, capacity);
        }

        private void EnsureTriangleBuffers(int count)
        {
            while (submeshTriangles.Count < count) {
                submeshTriangles.Add(new List<int>(384));
            }

            for (int i = count; i < submeshTriangles.Count; i++) {
                submeshTriangles[i].Clear();
            }
        }

        private static void EnsureTriangleCapacity(List<int> triangles, int capacity)
        {
            if (triangles.Capacity < capacity) {
                triangles.Capacity = capacity;
            }
        }

        private static void EnsureCapacity<T>(List<T> list, int capacity)
        {
            if (list.Capacity < capacity) {
                list.Capacity = capacity;
            }
        }

        private void Encapsulate(Vector3 vertex)
        {
            if (!hasBounds) {
                boundsMin = vertex;
                boundsMax = vertex;
                hasBounds = true;
                return;
            }

            boundsMin = Vector3.Min(boundsMin, vertex);
            boundsMax = Vector3.Max(boundsMax, vertex);
        }

        private void BuildPaintUVs()
        {
            uv2Upload.Clear();
            
            var size = boundsMax - boundsMin;
            var width = Mathf.Max(size.x, 0.0001f);
            var height = Mathf.Max(size.y, 0.0001f);
            
            for (int i = 0; i < vertices.Count; i++) {
                var vertex = vertices[i];
                var atlasUv = uv0Upload[i];
                uv0Upload[i] = new Vector4(atlasUv.x, atlasUv.y, width, atlasUv.w);
                uv2Upload.Add(new Vector4((vertex.x - boundsMin.x) / width, (vertex.y - boundsMin.y) / height, height, 0f));
            }
        }

        #endregion
    }
}

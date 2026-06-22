using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Tripledot.CanvasKit
{
    public enum ImageLatticeRaycastMode
    {
        ImageDefault = 0,
        DeformedVisibleArea = 1
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    [Icon("Packages/com.tripledot.canvaskit/Editor Default Resources/Icons/ImageLattice/ComponentIcon.png")]
    [AddComponentMenu("UI (Canvas)/Image Lattice", 12)]
    public sealed class ImageLattice : UIBehaviour, IMeshModifier, IMaterialModifier, ICanvasRaycastFilter
    {
        public const int MinControlPointsPerAxis = 2;
        public const int MaxControlPointsPerAxis = 5;
        public const int MinSegmentsPerCell = 1;
        public const int MaxSegmentsPerCell = 4;

        private const int DefaultControlPointsPerAxis = 3;
        private const int DefaultSegmentsPerCell = 3;
        private const int PackedLatticePointCount = (MaxControlPointsPerAxis * MaxControlPointsPerAxis + 1) / 2;
        private const AdditionalCanvasShaderChannels RequiredCanvasChannels = AdditionalCanvasShaderChannels.TexCoord1;
        private const string ShaderName = "UI/Tripledot/Image Lattice";

        private static readonly Vector4[] LatticePointBuffer = new Vector4[PackedLatticePointCount];

        [SerializeField, NotKeyable]
        private int controlColumns = DefaultControlPointsPerAxis;
        [SerializeField, NotKeyable]
        private int controlRows = DefaultControlPointsPerAxis;
        [SerializeField, NotKeyable]
        private int segmentsPerCell = DefaultSegmentsPerCell;
        [SerializeField, NotKeyable]
        private bool latticeInitialized;
        [SerializeField, NotKeyable]
        private int storageControlColumns;
        [SerializeField, NotKeyable]
        private int storageControlRows;
        [SerializeField, NotKeyable]
        private ImageLatticeRaycastMode raycastMode = ImageLatticeRaycastMode.ImageDefault;
        [SerializeField]
        private LatticePointStorage latticePointStorage;
        [SerializeField, NotKeyable, HideInInspector]
        private int horizontalSubdivisions = -1;
        [SerializeField, NotKeyable, HideInInspector]
        private int verticalSubdivisions = -1;
        [SerializeField, NotKeyable, HideInInspector]
        private int surfaceResolution = -1;
        [SerializeField, NotKeyable, HideInInspector]
        private int storageHorizontalSubdivisions = -1;
        [SerializeField, NotKeyable, HideInInspector]
        private int storageVerticalSubdivisions = -1;
        [SerializeField, NotKeyable, HideInInspector]
        private int latticeColumns = -1;
        [SerializeField, NotKeyable, HideInInspector]
        private int latticeRows = -1;
        [SerializeField, NotKeyable, HideInInspector]
        private int subdivisionsPerCell = -1;
        [SerializeField, NotKeyable, HideInInspector]
        private int latticeStorageColumns = -1;
        [SerializeField, NotKeyable, HideInInspector]
        private int latticeStorageRows = -1;

        private Image image;
        private RectTransform rectTransform;
        private Material runtimeMaterial;
        private Material runtimeSourceMaterial;
        private Shader runtimeSourceShader;
        private bool runtimeUsesSourceShader;
        private int meshSignatureControlColumns;
        private int meshSignatureControlRows;
        private int meshSignatureSegmentsPerCell;
        private bool hasMeshSignature;
        private Vector2[] raycastRowA = Array.Empty<Vector2>();
        private Vector2[] raycastRowB = Array.Empty<Vector2>();

        public int ControlColumns {
            get {
                EnsureLattice();
                return controlColumns;
            }
            set => SetControlGrid(value, controlRows);
        }

        public int ControlRows {
            get {
                EnsureLattice();
                return controlRows;
            }
            set => SetControlGrid(controlColumns, value);
        }

        public int SegmentsPerCell {
            get {
                EnsureLattice();
                return segmentsPerCell;
            }
            set {
                ValidateSegmentsPerCell(value);
                EnsureLattice();
                if (segmentsPerCell != value) {
                    segmentsPerCell = value;
                    image.SetVerticesDirty();
                    CaptureMeshSignature();
                }
            }
        }

        public int ControlPointColumns => ControlColumns;

        public int ControlPointRows => ControlRows;

        public ImageLatticeRaycastMode RaycastMode {
            get => raycastMode;
            set => raycastMode = value;
        }

        public Vector2 GetLatticePoint(int x, int y)
        {
            EnsureLattice();
            ValidatePointIndex(x, y);
            return GetStoredLatticePoint(x, y);
        }

        public void SetLatticePoint(int x, int y, Vector2 point)
        {
            EnsureLattice();
            ValidatePointIndex(x, y);
            if (GetStoredLatticePoint(x, y) == point) {
                return;
            }

            SetStoredLatticePoint(x, y, point);
            UpdateRuntimeMaterialPayloadOrDirtyImage();
        }

        public void ResetLattice()
        {
            EnsureLattice();
            WriteIdentityPoints(controlColumns, controlRows);
            UpdateRuntimeMaterialPayloadOrDirtyImage();
        }

        public void ModifyMesh(Mesh mesh)
        {
            if (!IsActive()) {
                return;
            }

            using (var vertexHelper = new VertexHelper(mesh)) {
                ModifyMesh(vertexHelper);
                vertexHelper.FillMesh(mesh);
            }
        }

        public void ModifyMesh(VertexHelper vertexHelper)
        {
            if (!IsActive()) {
                return;
            }

            if (image.type == Image.Type.Simple) {
                EnsureLattice();
                EnsureCanvasShaderChannels();
                BuildMesh(vertexHelper, image);
            }
        }

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!IsActive()) {
                return baseMaterial;
            }

            if (image.type != Image.Type.Simple) {
                ReleaseRuntimeMaterial();
                return baseMaterial;
            }

            var useSourceShader = IsLatticeMaterial(baseMaterial);
            if (!useSourceShader && HasExplicitMaterial(image)) {
                ReleaseRuntimeMaterial();
                return baseMaterial;
            }

            EnsureLattice();
            EnsureRuntimeMaterial(baseMaterial, useSourceShader);
            ApplyLatticeMaterial(runtimeMaterial, image);

            return runtimeMaterial;
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (raycastMode != ImageLatticeRaycastMode.DeformedVisibleArea || !isActiveAndEnabled) {
                return true;
            }

            if (image.type != Image.Type.Simple ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var localPoint)) {
                return true;
            }

            return ContainsDeformedLocalPoint(GetLatticeLocalRect(), localPoint);
        }

        internal Rect GetLatticeLocalRect()
        {
            EnsureLattice();

            var dimensions = GetDrawingDimensions(image, image.preserveAspect);
            return Rect.MinMaxRect(dimensions.x, dimensions.y, dimensions.z, dimensions.w);
        }

        internal Vector2 EvaluateLattice(Vector2 uv)
        {
            EnsureLattice();
            return ImageLatticeUtility.Evaluate(this, ControlPointColumns, ControlPointRows, uv);
        }

        internal void UpdateRuntimeMaterialPayloadOrDirtyImage()
        {
            EnsureLattice();

            if (!IsActive() || image.type != Image.Type.Simple) {
                image.SetMaterialDirty();
                return;
            }

            if (HasExplicitMaterial(image) && !IsLatticeMaterial(image.material)) {
                image.SetMaterialDirty();
                return;
            }

            if (runtimeMaterial == null) {
                image.SetMaterialDirty();
                return;
            }

            ApplyLatticeMaterial(runtimeMaterial, image);
        }

        internal Vector2 GetStoredLatticePointUnchecked(int index)
        {
            return latticePointStorage.GetPoint(index);
        }

        internal static string GetSerializedPointFieldName(int pointIndex)
        {
            if (pointIndex < 0 || pointIndex >= MaxControlPointsPerAxis * MaxControlPointsPerAxis) {
                throw new ArgumentOutOfRangeException(nameof(pointIndex));
            }

            return $"point{pointIndex:00}";
        }

        internal static string GetSerializedPointComponentName(int pointIndex, string component)
        {
            if (pointIndex < 0 || pointIndex >= MaxControlPointsPerAxis * MaxControlPointsPerAxis) {
                throw new ArgumentOutOfRangeException(nameof(pointIndex));
            }

            if (component != "x" && component != "y") {
                throw new ArgumentException("Lattice point component must be x or y.", nameof(component));
            }

            return component;
        }

        internal static string GetSerializedPointComponentPropertyPath(int pointIndex, string component)
        {
            return $"{nameof(latticePointStorage)}.{GetSerializedPointFieldName(pointIndex)}.{GetSerializedPointComponentName(pointIndex, component)}";
        }

        internal void CopyPackedLatticePointsTo(Vector4[] destination)
        {
            EnsureLattice();
            unsafe {
                fixed (Vector4* destinationPtr = destination) {
                    var packedCount = Mathf.Min(destination.Length, PackedLatticePointCount);
                    if (packedCount > 0) {
                        latticePointStorage.CopyPackedTo(destinationPtr, packedCount);
                    }

                    if (destination.Length > PackedLatticePointCount) {
                        UnsafeUtility.MemClear(destinationPtr + PackedLatticePointCount, (destination.Length - PackedLatticePointCount) * sizeof(float) * 4);
                    }
                }
            }
        }

        protected override void Awake()
        {
            base.Awake();
            image = GetComponent<Image>();
            rectTransform = (RectTransform)transform;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            image = GetComponent<Image>();
            rectTransform = (RectTransform)transform;
            EnsureLattice();
            EnsureCanvasShaderChannels();
            CaptureMeshSignature();
            image.SetVerticesDirty();
            image.SetMaterialDirty();
        }

        protected override void OnDisable()
        {
            image.SetVerticesDirty();
            image.SetMaterialDirty();
            ReleaseRuntimeMaterial();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            ReleaseRuntimeMaterial();
            base.OnDestroy();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            EnsureCanvasShaderChannels();
        }

        protected override void OnDidApplyAnimationProperties()
        {
            base.OnDidApplyAnimationProperties();
            EnsureLattice();
            UpdateRuntimeMaterialPayloadOrDirtyImage();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            image = GetComponent<Image>();
            rectTransform = (RectTransform)transform;
            EnsureLattice();

            if (HasMeshSignatureChanged()) {
                image.SetVerticesDirty();
            }

            CaptureMeshSignature();
            UpdateRuntimeMaterialPayloadOrDirtyImage();
        }
#endif

        private void SetControlGrid(int columns, int rows)
        {
            ValidateControlPointCount(columns);
            ValidateControlPointCount(rows);
            EnsureLattice();

            if (controlColumns == columns && controlRows == rows) {
                return;
            }

            controlColumns = columns;
            controlRows = rows;
            WriteIdentityPoints(controlColumns, controlRows);
            image.SetVerticesDirty();
            CaptureMeshSignature();
            UpdateRuntimeMaterialPayloadOrDirtyImage();
        }

        private bool ContainsDeformedLocalPoint(Rect geometryRect, Vector2 localPoint)
        {
            EnsureLattice();

            var controlPointColumns = ControlPointColumns;
            var controlPointRows = ControlPointRows;
            var xSegments = GetSegmentCount(0f, 1f, controlPointColumns, SegmentsPerCell);
            var ySegments = GetSegmentCount(0f, 1f, controlPointRows, SegmentsPerCell);
            var rowLength = xSegments + 1;
            EnsureRaycastRows(rowLength);

            FillRaycastRow(raycastRowA, xSegments, 0f, geometryRect);
            for (var y = 0; y < ySegments; y++) {
                var v = (y + 1) / (float)ySegments;
                FillRaycastRow(raycastRowB, xSegments, v, geometryRect);

                for (var x = 0; x < xSegments; x++) {
                    var bottomLeft = raycastRowA[x];
                    var topLeft = raycastRowB[x];
                    var topRight = raycastRowB[x + 1];
                    var bottomRight = raycastRowA[x + 1];

                    if (PointInTriangle(localPoint, bottomLeft, topLeft, topRight) ||
                        PointInTriangle(localPoint, topRight, bottomRight, bottomLeft)) {
                        return true;
                    }
                }

                (raycastRowA, raycastRowB) = (raycastRowB, raycastRowA);
            }

            return false;
        }

        private void EnsureRaycastRows(int rowLength)
        {
            if (raycastRowA.Length < rowLength) {
                raycastRowA = new Vector2[rowLength];
            }

            if (raycastRowB.Length < rowLength) {
                raycastRowB = new Vector2[rowLength];
            }
        }

        private void FillRaycastRow(Vector2[] row, int xSegments, float v, Rect geometryRect)
        {
            var size = new Vector2(geometryRect.width, geometryRect.height);
            for (var x = 0; x <= xSegments; x++) {
                var u = x / (float)xSegments;
                var latticeUv = new Vector2(u, v);
                var deformedUv = ImageLatticeUtility.Evaluate(this, ControlPointColumns, ControlPointRows, latticeUv);
                row[x] = new Vector2(
                    geometryRect.xMin + deformedUv.x * size.x,
                    geometryRect.yMin + deformedUv.y * size.y);
            }
        }

        private bool HasMeshSignatureChanged()
        {
            return hasMeshSignature &&
                   (meshSignatureControlColumns != controlColumns ||
                    meshSignatureControlRows != controlRows ||
                    meshSignatureSegmentsPerCell != segmentsPerCell);
        }

        private void CaptureMeshSignature()
        {
            meshSignatureControlColumns = controlColumns;
            meshSignatureControlRows = controlRows;
            meshSignatureSegmentsPerCell = segmentsPerCell;
            hasMeshSignature = true;
        }

        private void EnsureLattice()
        {
            MigrateLegacySerializedFields();
            ClampSerializedConfiguration();

            if (!latticeInitialized) {
                WriteIdentityPoints(controlColumns, controlRows);
                return;
            }

            if (storageControlColumns == controlColumns && storageControlRows == controlRows) {
                return;
            }

            WriteIdentityPoints(controlColumns, controlRows);
        }

        private void MigrateLegacySerializedFields()
        {
            if (latticeColumns >= MinControlPointsPerAxis) {
                controlColumns = ClampControlPointCount(latticeColumns);
                latticeColumns = -1;
            }

            if (latticeRows >= MinControlPointsPerAxis) {
                controlRows = ClampControlPointCount(latticeRows);
                latticeRows = -1;
            }

            if (subdivisionsPerCell >= MinSegmentsPerCell) {
                segmentsPerCell = ClampSegmentsPerCell(subdivisionsPerCell);
                subdivisionsPerCell = -1;
            }

            if (latticeStorageColumns >= MinControlPointsPerAxis) {
                storageControlColumns = ClampControlPointCount(latticeStorageColumns);
                latticeStorageColumns = -1;
            }

            if (latticeStorageRows >= MinControlPointsPerAxis) {
                storageControlRows = ClampControlPointCount(latticeStorageRows);
                latticeStorageRows = -1;
            }

            if (horizontalSubdivisions >= MinControlPointsPerAxis - 1) {
                controlColumns = ClampControlPointCount(horizontalSubdivisions + 1);
                horizontalSubdivisions = -1;
            }

            if (verticalSubdivisions >= MinControlPointsPerAxis - 1) {
                controlRows = ClampControlPointCount(verticalSubdivisions + 1);
                verticalSubdivisions = -1;
            }

            if (surfaceResolution >= MinSegmentsPerCell) {
                segmentsPerCell = ClampSegmentsPerCell(surfaceResolution);
                surfaceResolution = -1;
            }

            if (storageHorizontalSubdivisions >= MinControlPointsPerAxis - 1) {
                storageControlColumns = ClampControlPointCount(storageHorizontalSubdivisions + 1);
                storageHorizontalSubdivisions = -1;
            }

            if (storageVerticalSubdivisions >= MinControlPointsPerAxis - 1) {
                storageControlRows = ClampControlPointCount(storageVerticalSubdivisions + 1);
                storageVerticalSubdivisions = -1;
            }
        }

        private void ClampSerializedConfiguration()
        {
            controlColumns = ClampControlPointCount(controlColumns);
            controlRows = ClampControlPointCount(controlRows);
            segmentsPerCell = ClampSegmentsPerCell(segmentsPerCell);
        }

        private void WriteIdentityPoints(int controlPointColumns, int controlPointRows)
        {
            for (var y = 0; y < controlPointRows; y++) {
                for (var x = 0; x < controlPointColumns; x++) {
                    latticePointStorage.SetPoint(GetPointIndex(x, y, controlPointColumns), GetIdentityPoint(x, y, controlPointColumns, controlPointRows));
                }
            }

            latticeInitialized = true;
            storageControlColumns = controlColumns;
            storageControlRows = controlRows;
        }

        private Vector2 GetStoredLatticePoint(int x, int y)
        {
            return latticePointStorage.GetPoint(GetPointIndex(x, y, ControlPointColumns));
        }

        private void SetStoredLatticePoint(int x, int y, Vector2 value)
        {
            latticePointStorage.SetPoint(GetPointIndex(x, y, ControlPointColumns), value);
        }

        private void ValidatePointIndex(int x, int y)
        {
            if (x < 0 || x >= ControlPointColumns) {
                throw new ArgumentOutOfRangeException(nameof(x));
            }

            if (y < 0 || y >= ControlPointRows) {
                throw new ArgumentOutOfRangeException(nameof(y));
            }
        }

        private void EnsureCanvasShaderChannels()
        {
            if (image) {
                CanvasUtility.EnsureChannels(image.canvas, RequiredCanvasChannels);
            }
        }

        private void BuildMesh(VertexHelper vertexHelper, Image graphicImage)
        {
            vertexHelper.Clear();
            BuildSimple(vertexHelper, graphicImage, graphicImage.preserveAspect);
        }

        private void BuildSimple(VertexHelper vertexHelper, Image graphicImage, bool preserveAspect)
        {
            var dimensions = GetDrawingDimensions(graphicImage, preserveAspect);
            var geometryRect = Rect.MinMaxRect(dimensions.x, dimensions.y, dimensions.z, dimensions.w);
            if (geometryRect.width <= 0f || geometryRect.height <= 0f) {
                return;
            }

            var sprite = GetActiveSprite(graphicImage);
            var uv = sprite != null ? DataUtility.GetOuterUV(sprite) : new Vector4(0f, 0f, 1f, 1f);
            AddTessellatedPatch(vertexHelper, graphicImage,
                new Vector2(dimensions.x, dimensions.y), new Vector2(dimensions.z, dimensions.w),
                new Vector2(uv.x, uv.y), new Vector2(uv.z, uv.w), geometryRect);
        }

        private void AddTessellatedPatch(VertexHelper vertexHelper, Image graphicImage, Vector2 positionMin, Vector2 positionMax,
            Vector2 uvMin, Vector2 uvMax, Rect geometryRect)
        {
            var normalizedMin = PositionToLatticeUv(positionMin, geometryRect);
            var normalizedMax = PositionToLatticeUv(positionMax, geometryRect);
            var xSegments = GetSegmentCount(normalizedMin.x, normalizedMax.x, ControlPointColumns, SegmentsPerCell);
            var ySegments = GetSegmentCount(normalizedMin.y, normalizedMax.y, ControlPointRows, SegmentsPerCell);
            var startIndex = vertexHelper.currentVertCount;
            var color = (Color32)graphicImage.color;
            var size = new Vector2(geometryRect.width, geometryRect.height);

            for (var y = 0; y <= ySegments; y++) {
                var v = y / (float)ySegments;
                for (var x = 0; x <= xSegments; x++) {
                    var u = x / (float)xSegments;
                    var position = new Vector2(Mathf.Lerp(positionMin.x, positionMax.x, u), Mathf.Lerp(positionMin.y, positionMax.y, v));
                    var uv = new Vector2(Mathf.Lerp(uvMin.x, uvMax.x, u), Mathf.Lerp(uvMin.y, uvMax.y, v));
                    var latticeUv = PositionToLatticeUv(position, geometryRect);

                    vertexHelper.AddVert(
                        position: new Vector3(position.x, position.y, 0f),
                        color: color,
                        uv0: new Vector4(uv.x, uv.y, 0f, 0f),
                        uv1: new Vector4(latticeUv.x, latticeUv.y, size.x, size.y),
                        normal: Vector3.back,
                        tangent: UIVertex.simpleVert.tangent);
                }
            }

            var stride = xSegments + 1;
            for (var y = 0; y < ySegments; y++) {
                for (var x = 0; x < xSegments; x++) {
                    var bottomLeft = startIndex + y * stride + x;
                    var topLeft = bottomLeft + stride;
                    var topRight = topLeft + 1;
                    var bottomRight = bottomLeft + 1;

                    vertexHelper.AddTriangle(bottomLeft, topLeft, topRight);
                    vertexHelper.AddTriangle(topRight, bottomRight, bottomLeft);
                }
            }
        }

        private void EnsureRuntimeMaterial(Material sourceMaterial, bool useSourceShader)
        {
            var sourceShader = sourceMaterial.shader;
            if (runtimeMaterial != null &&
                runtimeSourceMaterial == sourceMaterial &&
                runtimeSourceShader == sourceShader &&
                runtimeUsesSourceShader == useSourceShader) {
                runtimeMaterial.CopyPropertiesFromMaterial(sourceMaterial);
                return;
            }

            ReleaseRuntimeMaterial();
            runtimeSourceMaterial = sourceMaterial;
            runtimeSourceShader = sourceShader;
            runtimeUsesSourceShader = useSourceShader;
            runtimeMaterial = CreateRuntimeMaterial(sourceMaterial, useSourceShader);
            runtimeMaterial.name = name + " (Image Lattice Material)";
        }

        private Material CreateRuntimeMaterial(Material sourceMaterial, bool useSourceShader)
        {
            var material = useSourceShader
                ? new Material(sourceMaterial)
                : CreateDefaultLatticeMaterial();

            material.hideFlags = HideFlags.HideAndDontSave;
            if (!useSourceShader) {
                material.CopyPropertiesFromMaterial(sourceMaterial);
            }

            return material;
        }

        private Material CreateDefaultLatticeMaterial()
        {
            if (GraphicsSettings.TryGetRenderPipelineSettings<CanvasKitResourcesURP>(out var resources) &&
                resources.ImageLatticeDefaultMaterial != null) {
                return new Material(resources.ImageLatticeDefaultMaterial);
            }

            return new Material(ResolveShader());
        }

        private void ApplyLatticeMaterial(Material material, Image graphicImage)
        {
            CopyPackedLatticePointsTo(LatticePointBuffer);

            var sprite = GetActiveSprite(graphicImage);
            var alphaTexture = sprite != null ? sprite.associatedAlphaSplitTexture : null;
            material.SetTexture(ShaderIds.MainTex, graphicImage.mainTexture);
            material.SetTexture(ShaderIds.AlphaTex, alphaTexture != null ? alphaTexture : Texture2D.whiteTexture);
            material.SetVector(ShaderIds.LatticeGrid, new Vector4(ControlPointColumns, ControlPointRows, 0f, 0f));
            material.SetVectorArray(ShaderIds.LatticePoints, LatticePointBuffer);
        }

        private void ReleaseRuntimeMaterial()
        {
            CoreUtils.Destroy(runtimeMaterial);
            runtimeMaterial = null;
            runtimeSourceMaterial = null;
            runtimeSourceShader = null;
            runtimeUsesSourceShader = false;
        }

        private static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            const float epsilon = 0.00001f;
            if (Mathf.Abs(Cross(b - a, c - a)) <= epsilon) {
                return false;
            }

            var d1 = Cross(point - a, b - a);
            var d2 = Cross(point - b, c - b);
            var d3 = Cross(point - c, a - c);
            var hasNegative = d1 < -epsilon || d2 < -epsilon || d3 < -epsilon;
            var hasPositive = d1 > epsilon || d2 > epsilon || d3 > epsilon;
            return !(hasNegative && hasPositive);
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static void ValidateControlPointCount(int value)
        {
            if (value < MinControlPointsPerAxis || value > MaxControlPointsPerAxis) {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Control point count must be between {MinControlPointsPerAxis} and {MaxControlPointsPerAxis}.");
            }
        }

        private static void ValidateSegmentsPerCell(int value)
        {
            if (value < MinSegmentsPerCell || value > MaxSegmentsPerCell) {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Segments per cell must be between {MinSegmentsPerCell} and {MaxSegmentsPerCell}.");
            }
        }

        private static int ClampControlPointCount(int value)
        {
            return Mathf.Clamp(value, MinControlPointsPerAxis, MaxControlPointsPerAxis);
        }

        private static int ClampSegmentsPerCell(int value)
        {
            return Mathf.Clamp(value, MinSegmentsPerCell, MaxSegmentsPerCell);
        }

        private static Shader ResolveShader()
        {
            if (GraphicsSettings.TryGetRenderPipelineSettings<CanvasKitResourcesURP>(out var resources) &&
                resources.ImageLatticeShader != null) {
                return resources.ImageLatticeShader;
            }

            var shader = Shader.Find(ShaderName);
            if (shader != null) {
                return shader;
            }

            throw new InvalidOperationException(
                $"Failed to resolve required image shader '{ShaderName}'. " +
                $"Make sure the image lattice shader is assigned in the {nameof(CanvasKitResourcesURP)} asset.");
        }

        private static bool HasExplicitMaterial(Image graphicImage)
        {
            var material = graphicImage.material;
            return material != graphicImage.defaultMaterial &&
                   material != Image.defaultETC1GraphicMaterial;
        }

        internal static bool IsLatticeMaterial(Material material)
        {
            return material.HasProperty(ShaderIds.LatticeGrid);
        }

        private static Sprite GetActiveSprite(Image graphicImage)
        {
            return graphicImage.overrideSprite != null ? graphicImage.overrideSprite : graphicImage.sprite;
        }

        private static Vector4 GetDrawingDimensions(Image graphicImage, bool preserveAspect)
        {
            var sprite = GetActiveSprite(graphicImage);
            var padding = sprite != null ? DataUtility.GetPadding(sprite) : Vector4.zero;
            var size = sprite != null ? new Vector2(sprite.rect.width, sprite.rect.height) : Vector2.zero;

            var rect = graphicImage.GetPixelAdjustedRect();
            var spriteWidth = Mathf.Max(1, Mathf.RoundToInt(size.x));
            var spriteHeight = Mathf.Max(1, Mathf.RoundToInt(size.y));
            var dimensions = new Vector4(
                padding.x / spriteWidth,
                padding.y / spriteHeight,
                (spriteWidth - padding.z) / spriteWidth,
                (spriteHeight - padding.w) / spriteHeight);

            if (preserveAspect && size.sqrMagnitude > 0f) {
                PreserveSpriteAspectRatio(graphicImage, ref rect, size);
            }

            return new Vector4(
                rect.x + rect.width * dimensions.x,
                rect.y + rect.height * dimensions.y,
                rect.x + rect.width * dimensions.z,
                rect.y + rect.height * dimensions.w);
        }

        private static void PreserveSpriteAspectRatio(Image graphicImage, ref Rect rect, Vector2 spriteSize)
        {
            var spriteRatio = spriteSize.x / spriteSize.y;
            var rectRatio = rect.width / rect.height;

            if (spriteRatio > rectRatio) {
                var oldHeight = rect.height;
                rect.height = rect.width / spriteRatio;
                rect.y += (oldHeight - rect.height) * graphicImage.rectTransform.pivot.y;
            } else {
                var oldWidth = rect.width;
                rect.width = rect.height * spriteRatio;
                rect.x += (oldWidth - rect.width) * graphicImage.rectTransform.pivot.x;
            }
        }

        private static Vector2 PositionToLatticeUv(Vector2 position, Rect rect)
        {
            return new Vector2(
                rect.width > 0f ? (position.x - rect.xMin) / rect.width : 0f,
                rect.height > 0f ? (position.y - rect.yMin) / rect.height : 0f);
        }

        private static int GetSegmentCount(float min, float max, int controlPointCount, int segmentsPerCell)
        {
            var latticeCells = Mathf.Max(1, controlPointCount - 1);
            return Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(max - min) * latticeCells * segmentsPerCell));
        }

        private static Vector2 GetIdentityPoint(int x, int y, int controlPointColumns, int controlPointRows)
        {
            var u = controlPointColumns > 1 ? x / (float)(controlPointColumns - 1) : 0f;
            var v = controlPointRows > 1 ? y / (float)(controlPointRows - 1) : 0f;
            return new Vector2(u, v);
        }

        private static int GetPointIndex(int x, int y, int controlPointColumns)
        {
            return y * controlPointColumns + x;
        }

        /// <summary>
        /// This sucks, but Unity's animation system doesn't key array or list fields that have structs,
        /// so we have to store lattice points in individual serialized fields to be animatable.
        /// </summary>
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct LatticePointStorage
        {
            [SerializeField]
            private Vector2 point00;
            [SerializeField]
            private Vector2 point01;
            [SerializeField]
            private Vector2 point02;
            [SerializeField]
            private Vector2 point03;
            [SerializeField]
            private Vector2 point04;
            [SerializeField]
            private Vector2 point05;
            [SerializeField]
            private Vector2 point06;
            [SerializeField]
            private Vector2 point07;
            [SerializeField]
            private Vector2 point08;
            [SerializeField]
            private Vector2 point09;
            [SerializeField]
            private Vector2 point10;
            [SerializeField]
            private Vector2 point11;
            [SerializeField]
            private Vector2 point12;
            [SerializeField]
            private Vector2 point13;
            [SerializeField]
            private Vector2 point14;
            [SerializeField]
            private Vector2 point15;
            [SerializeField]
            private Vector2 point16;
            [SerializeField]
            private Vector2 point17;
            [SerializeField]
            private Vector2 point18;
            [SerializeField]
            private Vector2 point19;
            [SerializeField]
            private Vector2 point20;
            [SerializeField]
            private Vector2 point21;
            [SerializeField]
            private Vector2 point22;
            [SerializeField]
            private Vector2 point23;
            [SerializeField]
            private Vector2 point24;

            public Vector2 GetPoint(int index)
            {
                CheckIndex(index);
                fixed (Vector2* points = &point00) {
                    return points[index];
                }
            }

            public void SetPoint(int index, Vector2 value)
            {
                CheckIndex(index);
                fixed (Vector2* points = &point00) {
                    points[index] = value;
                }
            }

            public void CopyPackedTo(Vector4* destination, int vectorCount)
            {
                if (vectorCount <= 0) {
                    return;
                }

                var destinationByteCount = vectorCount * sizeof(float) * 4;
                var sourceByteCount = MaxControlPointsPerAxis * MaxControlPointsPerAxis * sizeof(float) * 2;
                var copyByteCount = Math.Min(destinationByteCount, sourceByteCount);
                fixed (Vector2* points = &point00) {
                    UnsafeUtility.MemClear(destination, destinationByteCount);
                    UnsafeUtility.MemCpy(destination, points, copyByteCount);
                }
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            private static void CheckIndex(int index)
            {
                if ((uint)index >= MaxControlPointsPerAxis * MaxControlPointsPerAxis) {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
            }
        }

        private static class ShaderIds
        {
            public static readonly int MainTex = Shader.PropertyToID("_MainTex");
            public static readonly int AlphaTex = Shader.PropertyToID("_AlphaTex");
            public static readonly int LatticeGrid = Shader.PropertyToID("_LatticeGrid");
            public static readonly int LatticePoints = Shader.PropertyToID("_LatticePoints");
        }
    }
}
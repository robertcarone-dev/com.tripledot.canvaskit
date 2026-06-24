using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    internal static class ImageLatticeSerializedPointUtility
    {
        private const string LatticePointStoragePropertyName = "latticePointStorage";
        private const string LatticePointPropertyPrefix = LatticePointStoragePropertyName + ".point";

        private static readonly MethodInfo InAnimationRecordingMethod = typeof(AnimationMode).GetMethod("InAnimationRecording", BindingFlags.Public | BindingFlags.Static);
        private static readonly List<PropertyModification> AnimationModifications = new List<PropertyModification>(ImageLattice.MaxControlPointsPerAxis * ImageLattice.MaxControlPointsPerAxis * 2);

        public static bool ApplyPoints(ImageLattice image, Vector2[] points)
        {
            var pointCount = image.ControlPointColumns * image.ControlPointRows;
            if (points.Length != pointCount) {
                throw new ArgumentException("Point array must match the current lattice grid.", nameof(points));
            }

            var serializedObject = new SerializedObject(image);
            serializedObject.Update();

            var storageProperty = serializedObject.FindProperty(LatticePointStoragePropertyName);
            if (storageProperty == null) {
                throw new InvalidOperationException("Lattice point storage serialized property could not be resolved.");
            }

            var changed = false;
            AnimationModifications.Clear();
            for (var i = 0; i < pointCount; i++) {
                var pointProperty = storageProperty.FindPropertyRelative(GetSerializedPointFieldName(i));
                if (pointProperty == null) {
                    throw new InvalidOperationException("Lattice point serialized property could not be resolved.");
                }

                var xProperty = pointProperty.FindPropertyRelative(GetSerializedPointComponentName("x"));
                var yProperty = pointProperty.FindPropertyRelative(GetSerializedPointComponentName("y"));
                if (xProperty == null || yProperty == null) {
                    throw new InvalidOperationException("Lattice point component serialized properties could not be resolved.");
                }

                var next = points[i];
                if (xProperty.floatValue != next.x) {
                    changed = true;
                    xProperty.floatValue = next.x;
                    AddAnimationModification(image, GetPointComponentPropertyPath(i, "x"), next.x);
                }

                if (yProperty.floatValue != next.y) {
                    changed = true;
                    yProperty.floatValue = next.y;
                    AddAnimationModification(image, GetPointComponentPropertyPath(i, "y"), next.y);
                }
            }

            if (!changed) {
                AnimationModifications.Clear();
                return false;
            }

            serializedObject.ApplyModifiedProperties();
            RegisterAnimationModifications(image);
            AnimationModifications.Clear();
            return true;
        }

        public static string GetPointComponentPropertyPath(int pointIndex, string component)
        {
            return GetSerializedPointComponentPropertyPath(pointIndex, component);
        }

        internal static string GetSerializedPointFieldName(int pointIndex)
        {
            if (pointIndex is < 0 or >= ImageLattice.MaxControlPointsPerAxis * ImageLattice.MaxControlPointsPerAxis) {
                throw new ArgumentOutOfRangeException(nameof(pointIndex));
            }

            return $"point{pointIndex:00}";
        }

        internal static string GetSerializedPointComponentName(string component)
        {
            if (component != "x" && component != "y") {
                throw new ArgumentException("Lattice point component must be x or y.", nameof(component));
            }

            return component;
        }

        internal static string GetSerializedPointComponentPropertyPath(int pointIndex, string component)
        {
            return $"{LatticePointStoragePropertyName}.{GetSerializedPointFieldName(pointIndex)}.{GetSerializedPointComponentName(component)}";
        }

        internal static bool TryGetEditorCurveBinding(ImageLattice image, PropertyModification modification, out EditorCurveBinding binding)
        {
            if (AnimationUtility.PropertyModificationToEditorCurveBinding(modification, image.gameObject, out binding) != null) {
                return true;
            }

            if (!IsLatticePointComponentPropertyPath(modification.propertyPath)) {
                binding = default;
                return false;
            }

            binding = EditorCurveBinding.FloatCurve(string.Empty, typeof(ImageLattice), modification.propertyPath);
            return true;
        }

        private static void AddAnimationModification(ImageLattice image, string propertyPath, float value)
        {
            AnimationModifications.Add(new PropertyModification {
                target = image,
                propertyPath = propertyPath,
                value = value.ToString("R", CultureInfo.InvariantCulture)
            });
        }

        private static void RegisterAnimationModifications(ImageLattice image)
        {
            if (!IsAnimationRecording()) {
                return;
            }

            for (var i = 0; i < AnimationModifications.Count; i++) {
                var modification = AnimationModifications[i];
                if (TryGetEditorCurveBinding(image, modification, out var binding)) {
                    AnimationMode.AddPropertyModification(binding, modification, true);
                }
            }
        }

        private static bool IsAnimationRecording()
        {
            if (!AnimationMode.InAnimationMode() || InAnimationRecordingMethod == null) {
                return false;
            }

            return InAnimationRecordingMethod.Invoke(null, null) is true;
        }

        private static bool IsLatticePointComponentPropertyPath(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath)) {
                return false;
            }

            if (!propertyPath.StartsWith(LatticePointPropertyPrefix, StringComparison.Ordinal)) {
                return false;
            }

            return propertyPath.EndsWith(".x", StringComparison.Ordinal) ||
                   propertyPath.EndsWith(".y", StringComparison.Ordinal);
        }
    }
}

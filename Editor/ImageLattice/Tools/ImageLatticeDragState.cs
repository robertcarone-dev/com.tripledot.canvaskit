using System;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    internal sealed class ImageLatticeDragState
    {
        public bool Active { get; private set; }
        public Vector2[] SourcePoints { get; private set; }
        public Vector2[] DestinationPoints { get; private set; }
        public Vector2 Pivot { get; private set; }
        public Vector3 StartPositionWorld { get; private set; }
        public Vector3 CurrentPositionWorld { get; set; }
        public Quaternion StartRotation { get; private set; }
        public Quaternion CurrentRotation { get; set; }
        public Vector3 CurrentScale { get; set; }
        public Rect StartBounds { get; set; }
        public bool HasStartBounds { get; set; }

        public void Begin(ImageLattice image, string undoName, Vector2[] points, Vector2 pivot, Vector3 positionWorld, Quaternion rotation, Vector3 scale)
        {
            if (Active) {
                return;
            }

            Active = true;
            SourcePoints = new Vector2[points.Length];
            DestinationPoints = new Vector2[points.Length];
            Array.Copy(points, SourcePoints, points.Length);
            Array.Copy(points, DestinationPoints, points.Length);
            Pivot = pivot;
            StartPositionWorld = positionWorld;
            CurrentPositionWorld = positionWorld;
            StartRotation = rotation;
            CurrentRotation = rotation;
            CurrentScale = scale;
            StartBounds = default;
            HasStartBounds = false;
            ImageLatticeToolState.RecordTransform(image, undoName);
        }

        public void End()
        {
            Active = false;
            SourcePoints = null;
            DestinationPoints = null;
            HasStartBounds = false;
        }
    }
}
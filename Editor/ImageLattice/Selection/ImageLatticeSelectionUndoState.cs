using System.Collections.Generic;
using UnityEngine;

namespace Tripledot.CanvasKit.Editor
{
    internal sealed class ImageLatticeSelectionUndoState : ScriptableObject
    {
        [SerializeField]
        private ImageLattice activeImage;
        [SerializeField]
        private List<int> selectedPoints = new List<int>();
        [SerializeField]
        private List<int> selectedCells = new List<int>();
        [SerializeField]
        private int activePointIndex = -1;
        [SerializeField]
        private int activeCellIndex = -1;

        public ImageLattice ActiveImage => activeImage;
        public IReadOnlyList<int> SelectedPoints => selectedPoints;
        public IReadOnlyList<int> SelectedCells => selectedCells;
        public int ActivePointIndex => activePointIndex;
        public int ActiveCellIndex => activeCellIndex;

        public void Store(ImageLattice image, ICollection<int> pointSelection, ICollection<int> cellSelection, int pointIndex, int cellIndex)
        {
            activeImage = image;
            activePointIndex = pointIndex;
            activeCellIndex = cellIndex;
            selectedPoints.Clear();
            selectedPoints.AddRange(pointSelection);
            selectedCells.Clear();
            selectedCells.AddRange(cellSelection);
        }
    }
}
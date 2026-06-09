#ifndef CANVASKIT_IMAGE_LATTICE_DEFORM_INCLUDED
#define CANVASKIT_IMAGE_LATTICE_DEFORM_INCLUDED

float4 _LatticePoints[13];

int GetImageLatticeControlPointColumns()
{
    return clamp(int(_LatticeGrid.x + 0.5), 2, 5);
}

int GetImageLatticeControlPointRows()
{
    return clamp(int(_LatticeGrid.y + 0.5), 2, 5);
}

int GetImageLatticeIndex(int x, int y, int controlPointColumns)
{
    return y * controlPointColumns + x;
}

float2 GetStoredImageLatticePoint(int x, int y, int controlPointColumns)
{
    int index = GetImageLatticeIndex(x, y, controlPointColumns);
    float4 packedPoints = _LatticePoints[index / 2];
    return (index & 1) == 0 ? packedPoints.xy : packedPoints.zw;
}

void GetImageLatticeExtrapolationAxis(int value, int count, out int lower, out int upper, out float t)
{
    if (value < 0) {
        lower = 0;
        upper = 1;
        t = value;
        return;
    }

    if (value >= count) {
        lower = count - 2;
        upper = count - 1;
        t = value - (count - 2);
        return;
    }

    lower = value;
    upper = value;
    t = 0.0;
}

float2 GetImageLatticePoint(int x, int y, int controlPointColumns, int controlPointRows)
{
    bool inRangeX = x >= 0 && x < controlPointColumns;
    bool inRangeY = y >= 0 && y < controlPointRows;
    if (inRangeX && inRangeY) {
        return GetStoredImageLatticePoint(x, y, controlPointColumns);
    }

    int x0; int x1; int y0; int y1;
    float tx; float ty;
    if (inRangeX) {
        GetImageLatticeExtrapolationAxis(y, controlPointRows, y0, y1, ty);
        return lerp(GetStoredImageLatticePoint(x, y0, controlPointColumns), GetStoredImageLatticePoint(x, y1, controlPointColumns), ty);
    }

    if (inRangeY) {
        GetImageLatticeExtrapolationAxis(x, controlPointColumns, x0, x1, tx);
        return lerp(GetStoredImageLatticePoint(x0, y, controlPointColumns), GetStoredImageLatticePoint(x1, y, controlPointColumns), tx);
    }

    GetImageLatticeExtrapolationAxis(x, controlPointColumns, x0, x1, tx);
    GetImageLatticeExtrapolationAxis(y, controlPointRows, y0, y1, ty);

    float2 row0 = lerp(GetStoredImageLatticePoint(x0, y0, controlPointColumns), GetStoredImageLatticePoint(x1, y0, controlPointColumns), tx);
    float2 row1 = lerp(GetStoredImageLatticePoint(x0, y1, controlPointColumns), GetStoredImageLatticePoint(x1, y1, controlPointColumns), tx);
    return lerp(row0, row1, ty);
}

int GetImageLatticeCell(float value, int count, out float t)
{
    float scaled = saturate(value) * (count - 1);
    int cell = min(int(floor(scaled)), count - 2);
    t = scaled - cell;
    return cell;
}

float2 ImageLatticeCatmullRom(float2 p0, float2 p1, float2 p2, float2 p3, float t)
{
    float t2 = t * t;
    float t3 = t2 * t;
    return 0.5 * ((2.0 * p1) +
        (-p0 + p2) * t +
        (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * t2 +
        (-p0 + 3.0 * p1 - 3.0 * p2 + p3) * t3);
}

float2 SampleImageLatticeRow(int x, int y, float t, int controlPointColumns, int controlPointRows)
{
    float2 p0 = GetImageLatticePoint(x - 1, y, controlPointColumns, controlPointRows);
    float2 p1 = GetImageLatticePoint(x, y, controlPointColumns, controlPointRows);
    float2 p2 = GetImageLatticePoint(x + 1, y, controlPointColumns, controlPointRows);
    float2 p3 = GetImageLatticePoint(x + 2, y, controlPointColumns, controlPointRows);
    return ImageLatticeCatmullRom(p0, p1, p2, p3, t);
}

float2 EvaluateImageLattice(float2 uv)
{
    int controlPointColumns = GetImageLatticeControlPointColumns();
    int controlPointRows = GetImageLatticeControlPointRows();

    float tx; float ty;
    int x = GetImageLatticeCell(uv.x, controlPointColumns, tx);
    int y = GetImageLatticeCell(uv.y, controlPointRows, ty);

    float2 p0 = SampleImageLatticeRow(x, y - 1, tx, controlPointColumns, controlPointRows);
    float2 p1 = SampleImageLatticeRow(x, y, tx, controlPointColumns, controlPointRows);
    float2 p2 = SampleImageLatticeRow(x, y + 1, tx, controlPointColumns, controlPointRows);
    float2 p3 = SampleImageLatticeRow(x, y + 2, tx, controlPointColumns, controlPointRows);

    return ImageLatticeCatmullRom(p0, p1, p2, p3, ty);
}

float2 ApplyImageLatticeDeformation(float2 positionOS, float4 latticeData)
{
    float2 latticeUV = latticeData.xy;
    float2 latticeSize = max(abs(latticeData.zw), float2(0.0001, 0.0001));
    float2 deformedUV = EvaluateImageLattice(latticeUV);
    return positionOS + (deformedUV - latticeUV) * latticeSize;
}

#endif // CANVASKIT_IMAGE_LATTICE_DEFORM_INCLUDED

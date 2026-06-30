// TargetSelectionSystem.cs - Luna/Playable Ads Compatible Version
// Changes: Removed IJobParallelFor, RaycastCommand.ScheduleBatch, NativeArray job arrays.
//          Uses plain Physics.Raycast on the main thread â€” identical picking logic.

using UnityEngine;

// â”€â”€ Shared data struct (simplified for Luna/WebGL) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
public struct PieceTargetData
{
    public int Index;
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Extents;
    public CubeShooterColor Color;
    public bool IsBulletIncoming;
    public bool IsActive;
}

// â”€â”€ Static utility â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
public static class TargetSelectionSystem
{
    /// <summary>
    /// Finds the nearest visible cube of <paramref name="targetColor"/>.
    /// Replaces the original Job-based batched-raycast version with plain
    /// Physics.Raycast calls â€” fully Luna/WebGL compatible.
    /// </summary>
    public static int GetObjectToShoot(
        Camera mainCamera,
        CubeShooterColor targetColor,
        PieceTargetData[] inputPieces,
        ObjectBaseMono[] pieceReferences)
    {
        if (inputPieces == null || inputPieces.Length == 0) return -1;

        // Pre-compute frustum planes once per call
        Matrix4x4 mat = mainCamera.projectionMatrix * mainCamera.worldToCameraMatrix;
        Plane[] frustumPlanes = new Plane[6];
        frustumPlanes[0] = new Plane(new Vector3(mat.m30 + mat.m00, mat.m31 + mat.m01, mat.m32 + mat.m02).normalized, 0); frustumPlanes[0].distance = (mat.m33 + mat.m03) / new Vector3(mat.m30 + mat.m00, mat.m31 + mat.m01, mat.m32 + mat.m02).magnitude;
        frustumPlanes[1] = new Plane(new Vector3(mat.m30 - mat.m00, mat.m31 - mat.m01, mat.m32 - mat.m02).normalized, 0); frustumPlanes[1].distance = (mat.m33 - mat.m03) / new Vector3(mat.m30 - mat.m00, mat.m31 - mat.m01, mat.m32 - mat.m02).magnitude;
        frustumPlanes[2] = new Plane(new Vector3(mat.m30 + mat.m10, mat.m31 + mat.m11, mat.m32 + mat.m12).normalized, 0); frustumPlanes[2].distance = (mat.m33 + mat.m13) / new Vector3(mat.m30 + mat.m10, mat.m31 + mat.m11, mat.m32 + mat.m12).magnitude;
        frustumPlanes[3] = new Plane(new Vector3(mat.m30 - mat.m10, mat.m31 - mat.m11, mat.m32 - mat.m12).normalized, 0); frustumPlanes[3].distance = (mat.m33 - mat.m13) / new Vector3(mat.m30 - mat.m10, mat.m31 - mat.m11, mat.m32 - mat.m12).magnitude;
        frustumPlanes[4] = new Plane(new Vector3(mat.m30 + mat.m20, mat.m31 + mat.m21, mat.m32 + mat.m22).normalized, 0); frustumPlanes[4].distance = (mat.m33 + mat.m23) / new Vector3(mat.m30 + mat.m20, mat.m31 + mat.m21, mat.m32 + mat.m22).magnitude;
        frustumPlanes[5] = new Plane(new Vector3(mat.m30 - mat.m20, mat.m31 - mat.m21, mat.m32 - mat.m22).normalized, 0); frustumPlanes[5].distance = (mat.m33 - mat.m23) / new Vector3(mat.m30 - mat.m20, mat.m31 - mat.m21, mat.m32 - mat.m22).magnitude;

        Vector3 camPos = mainCamera.transform.position;
        Vector3 camFwd = mainCamera.transform.forward;
        Vector3 camRight = mainCamera.transform.right;
        Vector3 camUp = mainCamera.transform.up;
        bool isOrtho = mainCamera.orthographic;

        int count = inputPieces.Length;

        for (int i = 0; i < count; i++)
        {
            var data = inputPieces[i];
            if (data.IsBulletIncoming || data.Color != targetColor || !data.IsActive) continue;

            Vector3 piecePos = new Vector3(data.Position.x, data.Position.y, data.Position.z);
            Vector3 extents = new Vector3(data.Extents.x, data.Extents.y, data.Extents.z);

            // Frustum cull
            if (!IsInsideFrustum(frustumPlanes, piecePos, extents)) continue;

            // 5-point visibility check (centre + 4 corners)
            float r = Mathf.Min(extents.x, Mathf.Min(extents.y, extents.z)) * 0.5f;
            Vector3 rightOff = camRight * r;
            Vector3 upOff = camUp * r;

            bool visible = false;
            for (int p = 0; p < 5; p++)
            {
                Vector3 targetPoint = piecePos;
                if (p == 1) targetPoint = piecePos + rightOff + upOff;
                else if (p == 2) targetPoint = piecePos - rightOff + upOff;
                else if (p == 3) targetPoint = piecePos + rightOff - upOff;
                else if (p == 4) targetPoint = piecePos - rightOff - upOff;

                Vector3 origin, direction;
                float distance;

                if (isOrtho)
                {
                    float dFwd = Vector3.Dot(targetPoint - camPos, camFwd);
                    origin = targetPoint - camFwd * dFwd;
                    direction = camFwd;
                    distance = dFwd;
                }
                else
                {
                    Vector3 toTarget = targetPoint - camPos;
                    distance = toTarget.magnitude;
                    direction = toTarget / distance;
                    origin = camPos;
                }

                if (distance <= 0f) continue;

                // Cast the ray â€” check if it hits the piece or nothing at all
                // Layer mask ignores 'Ignore Raycast' and 'UI' to save CPU
                int layerMask = ~((1 << 2) | (1 << 5));
                if (!Physics.Raycast(origin, direction, out RaycastHit hit, distance, layerMask))
                {
                    // Nothing hit â†’ piece surface is fully visible
                    visible = true;
                    break;
                }

                if (pieceReferences != null && pieceReferences[i] != null)
                {
                    Transform hitTr = hit.collider.transform;
                    Transform pieceTr = pieceReferences[i].transform;
                    if (hitTr == pieceTr || hitTr.IsChildOf(pieceTr))
                    {
                        visible = true;
                        break;
                    }
                }
            }

            if (visible) return i;
        }

        return -1;
    }

    // -----------------------------------------------------------------------
    // Frustum helper (OBB-aware via projected extent)
    // -----------------------------------------------------------------------
    private static bool IsInsideFrustum(Plane[] planes, Vector3 center, Vector3 extents)
    {
        for (int i = 0; i < 6; i++)
        {
            float d = planes[i].GetDistanceToPoint(center);
            // Projected extent along plane normal
            float r = Mathf.Abs(extents.x * planes[i].normal.x)
                    + Mathf.Abs(extents.y * planes[i].normal.y)
                    + Mathf.Abs(extents.z * planes[i].normal.z);
            if (d + r < 0f) return false;
        }
        return true;
    }
}

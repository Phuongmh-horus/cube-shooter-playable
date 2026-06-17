// IDPickingSystem.cs - Luna/Playable Ads Compatible Version
// Changes: Removed RenderTexture, CommandBuffer, Texture2D.ReadPixels GPU readback.
//          GPU readback (ReadPixels) is unreliable and slow on WebGL / Luna.

using UnityEngine;

public class IDPickingSystem
{
    private bool _isInitialized = false;
    private Plane[] _frustumPlanes = new Plane[6];

    public void Initialize()
    {
        _isInitialized = true;
    }

    /// <summary>Called once at load — kept for API compatibility.</summary>
    public void PrepareCache(ObjectBaseMono[] pieces) { /* no GPU cache needed */ }

    public void Release()
    {
        _isInitialized = false;
    }

    public int GetTarget(
        Camera cam,
        CubeShooterColor targetColor,
        PieceTargetData[] cachedData,
        ObjectBaseMono[] pieces)
    {
        if (!_isInitialized || cachedData == null || cachedData.Length == 0) return -1;
        int count = pieces.Length;
        if (count == 0) return -1;

        Matrix4x4 mat = cam.projectionMatrix * cam.worldToCameraMatrix;
        _frustumPlanes[0] = new Plane(new Vector3(mat.m30 + mat.m00, mat.m31 + mat.m01, mat.m32 + mat.m02).normalized, 0); _frustumPlanes[0].distance = (mat.m33 + mat.m03) / new Vector3(mat.m30 + mat.m00, mat.m31 + mat.m01, mat.m32 + mat.m02).magnitude;
        _frustumPlanes[1] = new Plane(new Vector3(mat.m30 - mat.m00, mat.m31 - mat.m01, mat.m32 - mat.m02).normalized, 0); _frustumPlanes[1].distance = (mat.m33 - mat.m03) / new Vector3(mat.m30 - mat.m00, mat.m31 - mat.m01, mat.m32 - mat.m02).magnitude;
        _frustumPlanes[2] = new Plane(new Vector3(mat.m30 + mat.m10, mat.m31 + mat.m11, mat.m32 + mat.m12).normalized, 0); _frustumPlanes[2].distance = (mat.m33 + mat.m13) / new Vector3(mat.m30 + mat.m10, mat.m31 + mat.m11, mat.m32 + mat.m12).magnitude;
        _frustumPlanes[3] = new Plane(new Vector3(mat.m30 - mat.m10, mat.m31 - mat.m11, mat.m32 - mat.m12).normalized, 0); _frustumPlanes[3].distance = (mat.m33 - mat.m13) / new Vector3(mat.m30 - mat.m10, mat.m31 - mat.m11, mat.m32 - mat.m12).magnitude;
        _frustumPlanes[4] = new Plane(new Vector3(mat.m30 + mat.m20, mat.m31 + mat.m21, mat.m32 + mat.m22).normalized, 0); _frustumPlanes[4].distance = (mat.m33 + mat.m23) / new Vector3(mat.m30 + mat.m20, mat.m31 + mat.m21, mat.m32 + mat.m22).magnitude;
        _frustumPlanes[5] = new Plane(new Vector3(mat.m30 - mat.m20, mat.m31 - mat.m21, mat.m32 - mat.m22).normalized, 0); _frustumPlanes[5].distance = (mat.m33 - mat.m23) / new Vector3(mat.m30 - mat.m20, mat.m31 - mat.m21, mat.m32 - mat.m22).magnitude;
        Vector3 camPos = cam.transform.position;

        int bestIndex = -1;
        float bestDistSqr = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            var data = cachedData[i];
            if (!data.IsActive || data.IsBulletIncoming || data.Color != targetColor) continue;

            if (pieces[i] == null || !pieces[i].gameObject.activeInHierarchy) continue;

            Vector3 pos = new Vector3(data.Position.x, data.Position.y, data.Position.z);
            Vector3 extents = new Vector3(data.Extents.x, data.Extents.y, data.Extents.z);

            // Frustum cull (OBB-aware via projected radius)
            if (!IsInsideFrustum(_frustumPlanes, pos, extents)) continue;

            // Screen-space check — must project to a valid viewport position
            Vector3 screenPos = cam.WorldToViewportPoint(pos);
            if (screenPos.z <= 0f ||
                screenPos.x < 0f || screenPos.x > 1f ||
                screenPos.y < 0f || screenPos.y > 1f) continue;

            float distSqr = (camPos - pos).sqrMagnitude;
            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static bool IsInsideFrustum(Plane[] planes, Vector3 center, Vector3 extents)
    {
        for (int i = 0; i < 6; i++)
        {
            float d = planes[i].GetDistanceToPoint(center);
            float r = Mathf.Abs(extents.x * planes[i].normal.x)
                    + Mathf.Abs(extents.y * planes[i].normal.y)
                    + Mathf.Abs(extents.z * planes[i].normal.z);
            if (d + r < 0f) return false;
        }
        return true;
    }
}
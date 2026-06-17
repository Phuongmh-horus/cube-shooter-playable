// MathTargetPickingSystem.cs - Luna/Playable Ads Compatible Version
// Changes: Removed Unity.Burst, Unity.Jobs, Unity.Mathematics dependencies.

using UnityEngine;

public class MathTargetPickingSystem
{
    private int[] _candidateIndices = new int[2000];
    private float[] _candidateDistances = new float[2000];
    private bool _isInitialized = false;

    public void Initialize()
    {
        if (_isInitialized) return;
        // Pre-allocate candidate arrays (matching original NativeArray sizes)
        _candidateIndices = new int[2000];
        _candidateDistances = new float[2000];
        _isInitialized = true;
    }

    public void Release()
    {
        _isInitialized = false;
    }

    public int GetTarget(
        Camera cam,
        CubeShooterColor targetColor,
        PieceTargetData[] cachedData,
        ObjectBaseMono[] pieces,
        Transform parent)
    {
        if (!_isInitialized || cachedData == null || cachedData.Length == 0 || parent == null) return -1;
        int count = pieces.Length;
        if (count == 0) return -1;

        // ---- Build frustum planes in Parent Local Space ----
        Matrix4x4 mat = cam.projectionMatrix * cam.worldToCameraMatrix;
        Plane[] worldPlanes = new Plane[6];
        worldPlanes[0] = new Plane(new Vector3(mat.m30 + mat.m00, mat.m31 + mat.m01, mat.m32 + mat.m02).normalized, 0); worldPlanes[0].distance = (mat.m33 + mat.m03) / new Vector3(mat.m30 + mat.m00, mat.m31 + mat.m01, mat.m32 + mat.m02).magnitude;
        worldPlanes[1] = new Plane(new Vector3(mat.m30 - mat.m00, mat.m31 - mat.m01, mat.m32 - mat.m02).normalized, 0); worldPlanes[1].distance = (mat.m33 - mat.m03) / new Vector3(mat.m30 - mat.m00, mat.m31 - mat.m01, mat.m32 - mat.m02).magnitude;
        worldPlanes[2] = new Plane(new Vector3(mat.m30 + mat.m10, mat.m31 + mat.m11, mat.m32 + mat.m12).normalized, 0); worldPlanes[2].distance = (mat.m33 + mat.m13) / new Vector3(mat.m30 + mat.m10, mat.m31 + mat.m11, mat.m32 + mat.m12).magnitude;
        worldPlanes[3] = new Plane(new Vector3(mat.m30 - mat.m10, mat.m31 - mat.m11, mat.m32 - mat.m12).normalized, 0); worldPlanes[3].distance = (mat.m33 - mat.m13) / new Vector3(mat.m30 - mat.m10, mat.m31 - mat.m11, mat.m32 - mat.m12).magnitude;
        worldPlanes[4] = new Plane(new Vector3(mat.m30 + mat.m20, mat.m31 + mat.m21, mat.m32 + mat.m22).normalized, 0); worldPlanes[4].distance = (mat.m33 + mat.m23) / new Vector3(mat.m30 + mat.m20, mat.m31 + mat.m21, mat.m32 + mat.m22).magnitude;
        worldPlanes[5] = new Plane(new Vector3(mat.m30 - mat.m20, mat.m31 - mat.m21, mat.m32 - mat.m22).normalized, 0); worldPlanes[5].distance = (mat.m33 - mat.m23) / new Vector3(mat.m30 - mat.m20, mat.m31 - mat.m21, mat.m32 - mat.m22).magnitude;

        Vector4[] localPlanes = new Vector4[6];
        for (int i = 0; i < 6; i++)
        {
            Plane p = worldPlanes[i];
            Vector3 pointOnPlane = p.normal * -p.distance;
            Vector3 localPoint = parent.InverseTransformPoint(pointOnPlane);
            Vector3 localNormal = parent.InverseTransformDirection(p.normal).normalized;
            float localDist = -Vector3.Dot(localNormal, localPoint);
            localPlanes[i] = new Vector4(localNormal.x, localNormal.y, localNormal.z, localDist);
        }

        // ---- Convert camera params to Parent Local Space ----
        Vector3 localCamPos = parent.InverseTransformPoint(cam.transform.position);
        Vector3 localCamFwd = parent.InverseTransformDirection(cam.transform.forward).normalized;
        Vector3 localCamRight = parent.InverseTransformDirection(cam.transform.right).normalized;
        Vector3 localCamUp = parent.InverseTransformDirection(cam.transform.up).normalized;
        bool isOrthographic = cam.orthographic;

        // ---- Step 1: Gather candidates (color + active + frustum filter) ----
        int candidateCount = 0;
        for (int i = 0; i < count; i++)
        {
            var piece = cachedData[i];
            if (!piece.IsActive || piece.IsBulletIncoming || piece.Color != targetColor) continue;

            // Sphere frustum test
            float radius = Mathf.Max(piece.Extents.x, Mathf.Max(piece.Extents.y, piece.Extents.z)) * 1.74f;
            if (!IsInsideFrustum(localPlanes, piece.Position, radius)) continue;

            float dist = Vector3.Distance(new Vector3(piece.Position.x, piece.Position.y, piece.Position.z), localCamPos);
            if (candidateCount < _candidateIndices.Length)
            {
                _candidateIndices[candidateCount] = i;
                _candidateDistances[candidateCount] = dist;
                candidateCount++;
            }
        }

        // ---- Step 2: Simple insertion-sort by distance (tiny list, fast) ----
        for (int i = 0; i < candidateCount - 1; i++)
        {
            for (int j = i + 1; j < candidateCount; j++)
            {
                if (_candidateDistances[j] < _candidateDistances[i])
                {
                    float tmpD = _candidateDistances[i]; _candidateDistances[i] = _candidateDistances[j]; _candidateDistances[j] = tmpD;
                    int tmpI = _candidateIndices[i]; _candidateIndices[i] = _candidateIndices[j]; _candidateIndices[j] = tmpI;
                }
            }
        }

        // ---- Step 3: Check up to 40 nearest candidates for visibility ----
        int maxCheck = Mathf.Min(candidateCount, 40);
        int bestIndex = -1;

        for (int c = 0; c < maxCheck; c++)
        {
            int idx = _candidateIndices[c];
            var targetPiece = cachedData[idx];
            float targetDist = _candidateDistances[c];

            float r = Mathf.Min(targetPiece.Extents.x, Mathf.Min(targetPiece.Extents.y, targetPiece.Extents.z)) * 0.5f;
            Vector3 rightOff = localCamRight * r;
            Vector3 upOff = localCamUp * r;
            Vector3 tPos = new Vector3(targetPiece.Position.x, targetPiece.Position.y, targetPiece.Position.z);

            bool anyVisible = false;

            for (int pIndex = 0; pIndex < 5; pIndex++)
            {
                Vector3 pt = tPos;
                if (pIndex == 1) pt = tPos + rightOff + upOff;
                else if (pIndex == 2) pt = tPos - rightOff + upOff;
                else if (pIndex == 3) pt = tPos + rightOff - upOff;
                else if (pIndex == 4) pt = tPos - rightOff - upOff;

                Vector3 rayOrigin, rayDir;
                float rayDist;

                if (isOrthographic)
                {
                    float dAlongFwd = Vector3.Dot(pt - localCamPos, localCamFwd);
                    rayOrigin = pt - localCamFwd * dAlongFwd;
                    rayDir = localCamFwd;
                    rayDist = dAlongFwd;
                }
                else
                {
                    Vector3 toTarget = pt - localCamPos;
                    rayDist = toTarget.magnitude;
                    rayDir = toTarget / rayDist;
                    rayOrigin = localCamPos;
                }

                bool occluded = false;

                for (int j = 0; j < count; j++)
                {
                    if (j == idx) continue;
                    var blocker = cachedData[j];
                    if (!blocker.IsActive) continue;

                    // Fast sphere rejection
                    Vector3 bPos = new Vector3(blocker.Position.x, blocker.Position.y, blocker.Position.z);
                    Vector3 originToB = bPos - rayOrigin;
                    float tBlocker = Vector3.Dot(originToB, rayDir);
                    float maxRadius = Mathf.Max(blocker.Extents.x, Mathf.Max(blocker.Extents.y, blocker.Extents.z)) * 1.74f;

                    if (tBlocker < -maxRadius || tBlocker > rayDist + maxRadius) continue;

                    float distToRaySqr = (originToB - rayDir * tBlocker).sqrMagnitude;
                    if (distToRaySqr > maxRadius * maxRadius) continue;

                    float t;
                    if (IntersectRayOBB(rayOrigin, rayDir, bPos,
                                        blocker.Rotation,
                                        new Vector3(blocker.Extents.x, blocker.Extents.y, blocker.Extents.z),
                                        out t))
                    {
                        if (t > 0f && t < rayDist - 0.01f)
                        {
                            occluded = true;
                            break;
                        }
                    }
                }

                if (!occluded) { anyVisible = true; break; }
            }

            if (anyVisible)
            {
                bestIndex = idx;
                break;
            }
        }

        // Validate the result
        if (bestIndex != -1 && (pieces[bestIndex] == null || !pieces[bestIndex].gameObject.activeInHierarchy))
            return -1;

        return bestIndex;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static bool IsInsideFrustum(Vector4[] planes, Vector3 center, float radius)
    {
        for (int i = 0; i < 6; i++)
        {
            float d = planes[i].x * center.x + planes[i].y * center.y + planes[i].z * center.z + planes[i].w;
            if (d + radius < 0f) return false;
        }
        return true;
    }

    private static bool IntersectRayOBB(
        Vector3 rayOrigin, Vector3 rayDir,
        Vector3 boxPos, Quaternion boxRot, Vector3 boxExtents,
        out float t)
    {
        t = -1f;
        Quaternion invRot = Quaternion.Inverse(boxRot);
        Vector3 localOrigin = invRot * (rayOrigin - boxPos);
        Vector3 localDir = invRot * rayDir;

        // Avoid divide-by-zero
        float invX = Mathf.Abs(localDir.x) > 1e-8f ? 1f / localDir.x : float.MaxValue;
        float invY = Mathf.Abs(localDir.y) > 1e-8f ? 1f / localDir.y : float.MaxValue;
        float invZ = Mathf.Abs(localDir.z) > 1e-8f ? 1f / localDir.z : float.MaxValue;

        float t0x = (-boxExtents.x - localOrigin.x) * invX;
        float t1x = (boxExtents.x - localOrigin.x) * invX;
        float t0y = (-boxExtents.y - localOrigin.y) * invY;
        float t1y = (boxExtents.y - localOrigin.y) * invY;
        float t0z = (-boxExtents.z - localOrigin.z) * invZ;
        float t1z = (boxExtents.z - localOrigin.z) * invZ;

        float tminX = Mathf.Min(t0x, t1x), tmaxX = Mathf.Max(t0x, t1x);
        float tminY = Mathf.Min(t0y, t1y), tmaxY = Mathf.Max(t0y, t1y);
        float tminZ = Mathf.Min(t0z, t1z), tmaxZ = Mathf.Max(t0z, t1z);

        float maxTmin = Mathf.Max(tminX, Mathf.Max(tminY, tminZ));
        float minTmax = Mathf.Min(tmaxX, Mathf.Min(tmaxY, tmaxZ));

        if (maxTmin <= minTmax && minTmax >= 0f)
        {
            t = maxTmin >= 0f ? maxTmin : minTmax;
            return true;
        }
        return false;
    }
}
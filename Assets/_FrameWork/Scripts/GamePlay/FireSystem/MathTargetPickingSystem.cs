// MathTargetPickingSystem.cs - Luna/Playable Ads Compatible Version
// Changes: Removed Unity.Burst, Unity.Jobs, Unity.Mathematics dependencies.

using UnityEngine;

public class MathTargetPickingSystem
{
    private int[] _candidateIndices = new int[2000];
    private float[] _candidateDistances = new float[2000];
    private byte[] _occlusionCache = new byte[2000]; // 0: Unknown, 1: Visible, 2: Occluded
    private int[] _validBlockersCache = new int[2000];
    private bool _isInitialized = false;

    // Caching per frame
    private int _lastFrame = -1;
    private Vector4[] _localPlanes = new Vector4[6];
    private Vector3 _localCamPos;
    private Vector3 _localCamFwd;
    private Vector3 _localCamRight;
    private Vector3 _localCamUp;
    private bool _isOrthographic;

    public void Initialize()
    {
        if (_isInitialized) return;
        _candidateIndices = new int[2000];
        _candidateDistances = new float[2000];
        _occlusionCache = new byte[2000];
        _isInitialized = true;
    }

    public void Release()
    {
        _isInitialized = false;
    }

    public int GetTarget(Camera cam, CubeShooterColor targetColor, PieceTargetData[] cachedData, ObjectBaseMono[] pieces, Transform parent)
    {
        int count = pieces.Length;
        if (count == 0) return -1;

        if (_occlusionCache.Length < count)
            System.Array.Resize(ref _occlusionCache, count + 500);

        // ---- Build frustum planes & cam params in Parent Local Space ONCE per frame ----
        if (Time.frameCount != _lastFrame)
        {
            _lastFrame = Time.frameCount;
            System.Array.Clear(_occlusionCache, 0, count); // Reset occlusion cache for the new frame

            Matrix4x4 mat = cam.projectionMatrix * cam.worldToCameraMatrix;
            
            // Avoid new Vector4[] array allocation to achieve 0 GC per frame
            Vector4 p0 = new Vector4(mat.m30 + mat.m00, mat.m31 + mat.m01, mat.m32 + mat.m02, mat.m33 + mat.m03);
            Vector4 p1 = new Vector4(mat.m30 - mat.m00, mat.m31 - mat.m01, mat.m32 - mat.m02, mat.m33 - mat.m03);
            Vector4 p2 = new Vector4(mat.m30 + mat.m10, mat.m31 + mat.m11, mat.m32 + mat.m12, mat.m33 + mat.m13);
            Vector4 p3 = new Vector4(mat.m30 - mat.m10, mat.m31 - mat.m11, mat.m32 - mat.m12, mat.m33 - mat.m13);
            Vector4 p4 = new Vector4(mat.m30 + mat.m20, mat.m31 + mat.m21, mat.m32 + mat.m22, mat.m33 + mat.m23);
            Vector4 p5 = new Vector4(mat.m30 - mat.m20, mat.m31 - mat.m21, mat.m32 - mat.m22, mat.m33 - mat.m23);

            ProcessPlane(0, ref p0, parent);
            ProcessPlane(1, ref p1, parent);
            ProcessPlane(2, ref p2, parent);
            ProcessPlane(3, ref p3, parent);
            ProcessPlane(4, ref p4, parent);
            ProcessPlane(5, ref p5, parent);

            _localCamPos = parent.InverseTransformPoint(cam.transform.position);
            _localCamFwd = parent.InverseTransformDirection(cam.transform.forward).normalized;
            _localCamRight = parent.InverseTransformDirection(cam.transform.right).normalized;
            _localCamUp = parent.InverseTransformDirection(cam.transform.up).normalized;
            _isOrthographic = cam.orthographic;
        }

        // ---- Step 1: Gather candidates (color + active + frustum filter) ----
        int candidateCount = 0;
        for (int i = 0; i < count; i++)
        {
            var piece = cachedData[i];
            if (!piece.IsActive || piece.IsBulletIncoming || piece.Color != targetColor) continue;

            float radius = Mathf.Max(piece.Extents.x, Mathf.Max(piece.Extents.y, piece.Extents.z)) * 1.74f;
            if (!IsInsideFrustum(_localPlanes, piece.Position, radius)) continue;

            float dist = Vector3.Distance(new Vector3(piece.Position.x, piece.Position.y, piece.Position.z), _localCamPos);
            if (candidateCount < _candidateIndices.Length)
            {
                _candidateIndices[candidateCount] = i;
                _candidateDistances[candidateCount] = dist;
                candidateCount++;
            }
        }

        // ---- Step 2: Native Sort by distance (O(N log N) -> Zero GC) ----
        if (candidateCount > 1)
        {
            QuickSortCandidates(_candidateDistances, _candidateIndices, 0, candidateCount - 1);
        }

        // ---- Step 3: Check up to 40 nearest candidates for visibility ----
        int maxCheck = Mathf.Min(candidateCount, 40);
        int bestIndex = -1;

        for (int c = 0; c < maxCheck; c++)
        {
            int idx = _candidateIndices[c];

            // Use frame cache to avoid duplicate raycasting for the same piece
            if (_occlusionCache[idx] == 2) continue; // Already tested and is occluded
            if (_occlusionCache[idx] == 1) // Already tested and is visible
            {
                bestIndex = idx;
                break;
            }

            var targetPiece = cachedData[idx];
            float targetDist = _candidateDistances[c];

            float r = Mathf.Min(targetPiece.Extents.x, Mathf.Min(targetPiece.Extents.y, targetPiece.Extents.z)) * 0.5f;
            Vector3 rightOff = _localCamRight * r;
            Vector3 upOff = _localCamUp * r;
            Vector3 tPos = new Vector3(targetPiece.Position.x, targetPiece.Position.y, targetPiece.Position.z);

            bool anyVisible = false;

            // Tối ưu hóa: Lọc blocker bằng broadphase hình trụ (cylinder) quanh tia trung tâm
            Vector3 centerRayOrigin, centerRayDir;
            float centerRayDist;
            if (_isOrthographic)
            {
                float dAlongFwd = Vector3.Dot(tPos - _localCamPos, _localCamFwd);
                centerRayOrigin = tPos - _localCamFwd * dAlongFwd;
                centerRayDir = _localCamFwd;
                centerRayDist = dAlongFwd;
            }
            else
            {
                Vector3 toTarget = tPos - _localCamPos;
                centerRayDist = toTarget.magnitude;
                centerRayDir = toTarget / centerRayDist;
                centerRayOrigin = _localCamPos;
            }

            int validBlockersCount = 0;
            for (int j = 0; j < count; j++)
            {
                if (j == idx) continue;
                var blocker = cachedData[j];
                if (!blocker.IsActive) continue;

                Vector3 bPos = new Vector3(blocker.Position.x, blocker.Position.y, blocker.Position.z);
                Vector3 originToB = bPos - centerRayOrigin;
                float tBlocker = Vector3.Dot(originToB, centerRayDir);
                float maxRadius = Mathf.Max(blocker.Extents.x, Mathf.Max(blocker.Extents.y, blocker.Extents.z)) * 1.74f;

                if (tBlocker < -maxRadius || tBlocker > centerRayDist + maxRadius) continue;

                float distToRaySqr = (originToB - centerRayDir * tBlocker).sqrMagnitude;
                float maxDist = maxRadius + r * 1.45f; // r is half extent, diagonal max deviation is r*sqrt(2) approx 1.414
                if (distToRaySqr > maxDist * maxDist) continue;

                if (validBlockersCount < _validBlockersCache.Length)
                {
                    _validBlockersCache[validBlockersCount++] = j;
                }
            }

            // Kiểm tra 5 điểm (tâm + 4 góc) để chính xác tuyệt đối như ban đầu
            for (int pIndex = 0; pIndex < 5; pIndex++)
            {
                Vector3 pt = tPos;
                if (pIndex == 1) pt = tPos + rightOff + upOff;
                else if (pIndex == 2) pt = tPos - rightOff + upOff;
                else if (pIndex == 3) pt = tPos + rightOff - upOff;
                else if (pIndex == 4) pt = tPos - rightOff - upOff;

                Vector3 rayOrigin, rayDir;
                float rayDist;

                if (_isOrthographic)
                {
                    float dAlongFwd = Vector3.Dot(pt - _localCamPos, _localCamFwd);
                    rayOrigin = pt - _localCamFwd * dAlongFwd;
                    rayDir = _localCamFwd;
                    rayDist = dAlongFwd;
                }
                else
                {
                    Vector3 toTarget = pt - _localCamPos;
                    rayDist = toTarget.magnitude;
                    rayDir = toTarget / rayDist;
                    rayOrigin = _localCamPos;
                }

                bool occluded = false;

                for (int v = 0; v < validBlockersCount; v++)
                {
                    int j = _validBlockersCache[v];
                    var blocker = cachedData[j];
                    Vector3 bPos = new Vector3(blocker.Position.x, blocker.Position.y, blocker.Position.z);

                    float t;
                    if (IntersectRayOBB(rayOrigin, rayDir, bPos,
                                        blocker.Rotation,
                                        new Vector3(blocker.Extents.x, blocker.Extents.y, blocker.Extents.z) * 0.9f,
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
                _occlusionCache[idx] = 1;
                bestIndex = idx;
                break;
            }
            else
            {
                _occlusionCache[idx] = 2;
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

    private void ProcessPlane(int index, ref Vector4 wP, Transform parent)
    {
        float mag = new Vector3(wP.x, wP.y, wP.z).magnitude;
        wP /= mag;
        Vector3 worldNormal = new Vector3(wP.x, wP.y, wP.z);
        Vector3 pointOnWorldPlane = worldNormal * -wP.w;
        Vector3 localPoint = parent.InverseTransformPoint(pointOnWorldPlane);
        Vector3 localNormal = parent.InverseTransformDirection(worldNormal).normalized;
        _localPlanes[index] = new Vector4(localNormal.x, localNormal.y, localNormal.z, -Vector3.Dot(localNormal, localPoint));
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

        if (maxTmin <= minTmax)
        {
            t = maxTmin;
            return true;
        }
        return false;
    }

    private void QuickSortCandidates(float[] dists, int[] indices, int left, int right)
    {
        if (left < right)
        {
            int pivot = Partition(dists, indices, left, right);
            QuickSortCandidates(dists, indices, left, pivot - 1);
            QuickSortCandidates(dists, indices, pivot + 1, right);
        }
    }

    private int Partition(float[] dists, int[] indices, int left, int right)
    {
        float pivotDist = dists[right];
        int i = left - 1;
        for (int j = left; j < right; j++)
        {
            if (dists[j] < pivotDist)
            {
                i++;
                float tempDist = dists[i]; dists[i] = dists[j]; dists[j] = tempDist;
                int tempIdx = indices[i]; indices[i] = indices[j]; indices[j] = tempIdx;
            }
        }
        float tD = dists[i + 1]; dists[i + 1] = dists[right]; dists[right] = tD;
        int tI = indices[i + 1]; indices[i + 1] = indices[right]; indices[right] = tI;
        return i + 1;
    }
}
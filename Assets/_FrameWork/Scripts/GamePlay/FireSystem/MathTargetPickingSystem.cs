using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

public class MathTargetPickingSystem
{
    private struct MathRaycastVisibilityJob : IJob
    {
        [ReadOnly] public NativeArray<PieceTargetData> Pieces;
        [ReadOnly] public NativeArray<float4> FrustumPlanes;
        public CubeShooterColor TargetColor;

        public Matrix4x4 ParentWorldToLocal;
        public float3 WorldCameraPos;
        public float3 WorldCameraForward;
        public float3 WorldCameraRight;
        public float3 WorldCameraUp;
        public bool IsOrthographic;

        public NativeArray<int> ResultIndex;
        public NativeArray<int> CandidateIndices;
        public NativeArray<float> CandidateDistances;

        public void Execute()
        {
            // 1. Chuyển đổi thông số Camera từ không gian World sang không gian Local của Parent
            float3 localCameraPos = math.transform(ParentWorldToLocal, WorldCameraPos);
            float3 localCameraForward = math.rotate(ParentWorldToLocal, WorldCameraForward);
            float3 localCameraRight = math.rotate(ParentWorldToLocal, WorldCameraRight);
            float3 localCameraUp = math.rotate(ParentWorldToLocal, WorldCameraUp);

            int candidateCount = 0;

            // 2. Tìm tất cả các ứng viên hợp lệ (Cùng màu, chưa bị nhắm, còn sống và nằm trong Camera Frustum)
            for (int i = 0; i < Pieces.Length; i++)
            {
                var piece = Pieces[i];
                if (!piece.IsActive || piece.IsBulletIncoming || piece.Color != TargetColor) continue;

                // Mở rộng bán kính kiểm tra một chút để đảm bảo an toàn với các khối bị quay chéo
                float radius = math.cmax(piece.Extents) * 1.74f;
                if (!IsInsideFrustum(piece.Position, radius)) continue;

                float distToCam = math.length(piece.Position - localCameraPos);

                CandidateIndices[candidateCount] = i;
                CandidateDistances[candidateCount] = distToCam;
                candidateCount++;
            }

            // 3. Sắp xếp ứng viên theo khoảng cách đến Camera (Gần nhất -> Xa nhất)
            // Vì candidateCount thường rất nhỏ (vài chục Cube cùng màu), Bubble Sort là quá nhanh và tiết kiệm trên Burst
            for (int i = 0; i < candidateCount - 1; i++)
            {
                for (int j = i + 1; j < candidateCount; j++)
                {
                    if (CandidateDistances[j] < CandidateDistances[i])
                    {
                        // Swap distance
                        float tempDist = CandidateDistances[i];
                        CandidateDistances[i] = CandidateDistances[j];
                        CandidateDistances[j] = tempDist;

                        // Swap index
                        int tempIdx = CandidateIndices[i];
                        CandidateIndices[i] = CandidateIndices[j];
                        CandidateIndices[j] = tempIdx;
                    }
                }
            }

            int bestIndex = -1;

            // 4. Lần lượt kiểm tra từ khối gần nhất. Tối ưu: Chỉ kiểm tra tối đa 40 khối gần nhất
            // Nếu 40 khối gần nhất đều bị che, tỷ lệ rất cao là toàn bộ màu đó đang bị lấp bên trong.
            int maxCandidatesToCheck = math.min(candidateCount, 40);
            for (int c = 0; c < maxCandidatesToCheck; c++)
            {
                int i = CandidateIndices[c];
                var targetPiece = Pieces[i];
                float targetDist = CandidateDistances[c];

                float r = math.cmin(targetPiece.Extents) * 0.5f;
                float3 rightOff = localCameraRight * r;
                float3 upOff = localCameraUp * r;

                bool anyPointVisible = false;

                // Kiểm tra 5 điểm (1 Tâm + 4 Góc) để chắc chắn nó có lộ diện ít nhất 1 phần không
                for (int pIndex = 0; pIndex < 5; pIndex++)
                {
                    float3 pt = targetPiece.Position;
                    if (pIndex == 1) pt += rightOff + upOff;
                    else if (pIndex == 2) pt += -rightOff + upOff;
                    else if (pIndex == 3) pt += rightOff - upOff;
                    else if (pIndex == 4) pt += -rightOff - upOff;

                    float3 rayOrigin;
                    float3 rayDir;
                    float rayDist;

                    if (IsOrthographic)
                    {
                        float distAlongForward = math.dot(pt - localCameraPos, localCameraForward);
                        rayOrigin = pt - localCameraForward * distAlongForward;
                        rayDir = localCameraForward;
                        rayDist = distAlongForward;
                    }
                    else
                    {
                        float3 vectorToTarget = pt - localCameraPos;
                        rayDist = math.length(vectorToTarget);
                        rayDir = vectorToTarget / rayDist;
                        rayOrigin = localCameraPos;
                    }

                    bool isRayOccluded = false;

                    for (int j = 0; j < Pieces.Length; j++)
                    {
                        if (i == j) continue;

                        var blocker = Pieces[j];
                        if (!blocker.IsActive) continue; // Bỏ qua ngay các Cube đã bị xóa

                        // --- FAST SPHERE REJECTION ---
                        float3 originToBlocker = blocker.Position - rayOrigin;
                        float tBlocker = math.dot(originToBlocker, rayDir);
                        float maxRadius = math.cmax(blocker.Extents) * 1.74f;

                        // Nếu Blocker nằm sau tia bắn, hoặc xa hơn cả mục tiêu -> Chắc chắn không cản đường
                        if (tBlocker < -maxRadius || tBlocker > rayDist + maxRadius) continue;

                        // Nếu tia bắn trượt hoàn toàn ra ngoài vòng tròn bao quát của Blocker
                        float distToRaySqr = math.lengthsq(originToBlocker - rayDir * tBlocker);
                        if (distToRaySqr > maxRadius * maxRadius) continue;
                        // ------------------------------

                        float t;
                        if (IntersectRayOBB(rayOrigin, rayDir, blocker.Position, blocker.Rotation, blocker.Extents, out t))
                        {
                            if (t > 0f && t < rayDist - 0.01f) // Cản trước khi đến mục tiêu
                            {
                                isRayOccluded = true;
                                break;
                            }
                        }
                    }

                    // Nếu có ít nhất 1 trong 5 điểm không bị ai che -> Mục tiêu này có thể bắn được!
                    if (!isRayOccluded)
                    {
                        anyPointVisible = true;
                        break;
                    }
                }

                // Nhờ sắp xếp theo khoảng cách từ trước, mục tiêu hiển thị đầu tiên CHẮC CHẮN là mục tiêu gần nhất
                if (anyPointVisible)
                {
                    bestIndex = i;
                    break;
                }
            }

            ResultIndex[0] = bestIndex;
        }

        private bool IsInsideFrustum(float3 center, float radius)
        {
            for (int i = 0; i < 6; i++)
            {
                float4 plane = FrustumPlanes[i];
                float d = math.dot(plane.xyz, center) + plane.w;
                if (d + radius < 0) return false;
            }
            return true;
        }

        private bool IntersectRayOBB(float3 rayOrigin, float3 rayDir, float3 boxPos, quaternion boxRot, float3 boxExtents, out float t)
        {
            t = -1f;

            quaternion invRot = math.inverse(boxRot);
            float3 localOrigin = math.mul(invRot, rayOrigin - boxPos);
            float3 localDir = math.mul(invRot, rayDir);

            float3 invDir = 1.0f / localDir;
            float3 t0 = (-boxExtents - localOrigin) * invDir;
            float3 t1 = (boxExtents - localOrigin) * invDir;

            float3 tmin = math.min(t0, t1);
            float3 tmax = math.max(t0, t1);

            float max_tmin = math.cmax(tmin);
            float min_tmax = math.cmin(tmax);

            if (max_tmin <= min_tmax && min_tmax >= 0)
            {
                t = max_tmin >= 0 ? max_tmin : min_tmax;
                return true;
            }

            return false;
        }
    }

    private NativeArray<float4> _frustumPlanes;
    private NativeArray<int> _resultIndex;
    private NativeArray<int> _candidateIndices;
    private NativeArray<float> _candidateDistances;
    private Plane[] _planesArray;
    private bool _isInitialized = false;

    public void Initialize()
    {
        if (_isInitialized) return;
        _frustumPlanes = new NativeArray<float4>(6, Allocator.Persistent);
        _resultIndex = new NativeArray<int>(1, Allocator.Persistent);
        _candidateIndices = new NativeArray<int>(2000, Allocator.Persistent); // Hỗ trợ lên đến 2000 candidate cùng lúc
        _candidateDistances = new NativeArray<float>(2000, Allocator.Persistent);
        _planesArray = new Plane[6];
        _isInitialized = true;
    }

    public void Release()
    {
        if (_frustumPlanes.IsCreated) _frustumPlanes.Dispose();
        if (_resultIndex.IsCreated) _resultIndex.Dispose();
        if (_candidateIndices.IsCreated) _candidateIndices.Dispose();
        if (_candidateDistances.IsCreated) _candidateDistances.Dispose();
        _isInitialized = false;
    }

    public int GetTarget(Camera cam, CubeShooterColor targetColor, NativeArray<PieceTargetData> cachedData, ObjectBaseMono[] pieces, Matrix4x4 parentWorldToLocal)
    {
        if (!_isInitialized || !cachedData.IsCreated) return -1;
        int count = pieces.Length;
        if (count == 0) return -1;

        GeometryUtility.CalculateFrustumPlanes(cam, _planesArray);
        for (int i = 0; i < 6; i++)
        {
            // Chuyển mặt phẳng Frustum vào hệ toạ độ Local của Parent để đồng bộ
            Plane p = _planesArray[i];

            // Lấy 1 điểm trên mặt phẳng
            Vector3 pointOnPlane = p.normal * -p.distance;
            // Chuyển điểm và pháp tuyến sang Local
            Vector3 localPoint = parentWorldToLocal.MultiplyPoint3x4(pointOnPlane);
            Vector3 localNormal = parentWorldToLocal.MultiplyVector(p.normal).normalized;

            // Tính lại distance trong Local Space
            float localDistance = -Vector3.Dot(localNormal, localPoint);
            _frustumPlanes[i] = new float4(localNormal, localDistance);
        }

        _resultIndex[0] = -1;

        var job = new MathRaycastVisibilityJob
        {
            Pieces = cachedData,
            FrustumPlanes = _frustumPlanes,
            TargetColor = targetColor,
            ParentWorldToLocal = parentWorldToLocal,
            WorldCameraPos = cam.transform.position,
            WorldCameraForward = cam.transform.forward,
            WorldCameraRight = cam.transform.right,
            WorldCameraUp = cam.transform.up,
            IsOrthographic = cam.orthographic,
            ResultIndex = _resultIndex,
            CandidateIndices = _candidateIndices,
            CandidateDistances = _candidateDistances
        };

        job.Run();

        int result = _resultIndex[0];

        if (result != -1 && (pieces[result] == null || !pieces[result].gameObject.activeInHierarchy))
        {
            return -1;
        }

        return result;
    }
}

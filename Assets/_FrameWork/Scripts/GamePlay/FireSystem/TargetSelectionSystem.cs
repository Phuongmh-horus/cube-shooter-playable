using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

// Định nghĩa struct PieceTargetData theo yêu cầu
public struct PieceTargetData
{
    public int Index;               // Index của Cube trong danh sách gốc (để truy xuất ngược lại Object)
    public float3 Position;         // Vị trí tâm (Center) của Cube trong không gian Local của Parent
    public quaternion Rotation;     // Góc quay của Cube trong không gian Local của Parent
    public float3 Extents;          // Bán kính/nửa kích thước (bounds.extents hoặc transform.localScale / 2)
    public CubeShooterColor Color;  // Màu sắc hiện tại của Cube
    public bool IsBulletIncoming;   // Cờ đánh dấu Cube đã được nhắm bắn hay chưa
    public bool IsActive;           // Đánh dấu Cube còn sống hay không
}

public static class TargetSelectionSystem
{
    // Bước 1: Job chuẩn bị các tia Raycast (Lọc Frustum và Color trước để giảm tải tia ray)
    private struct PrepareRaycastJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<PieceTargetData> InputData;
        [ReadOnly] public NativeArray<float4> FrustumPlanes;
        [ReadOnly] public CubeShooterColor TargetColor;

        public float3 CameraPos;
        public float3 CameraForward;
        public float3 CameraRight;
        public float3 CameraUp;
        public bool IsOrthographic;

        // Lưu maxDistance của từng mục tiêu để so sánh xem tia đụng bản thân hay đụng vật khác
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float> TargetDistances;

        // Output tạo lệnh Raycast
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<RaycastCommand> RaycastCommands;

        public void Execute(int index)
        {
            var data = InputData[index];
            int baseIndex = index * 5;

            // 1. Kiểm tra màu và trạng thái bị nhắm
            if (data.IsBulletIncoming || data.Color != TargetColor || !IsInsideFrustum(data.Position, data.Extents))
            {
                DisableRays(baseIndex);
                return;
            }

            // 2. Chuẩn bị 5 tia Raycast (1 tâm + 4 góc)
            // Bán kính offset an toàn bằng 50% kích thước nhỏ nhất của Cube để đảm bảo luôn bắn trúng khối hộp dù xoay góc nào
            float r = math.cmin(data.Extents) * 0.5f;
            float3 rightOff = CameraRight * r;
            float3 upOff = CameraUp * r;

            SetupRay(baseIndex + 0, data.Position); // Tâm
            SetupRay(baseIndex + 1, data.Position + rightOff + upOff); // Góc trên phải
            SetupRay(baseIndex + 2, data.Position - rightOff + upOff); // Góc trên trái
            SetupRay(baseIndex + 3, data.Position + rightOff - upOff); // Góc dưới phải
            SetupRay(baseIndex + 4, data.Position - rightOff - upOff); // Góc dưới trái
        }

        private void DisableRays(int baseIndex)
        {
            for (int i = 0; i < 5; i++)
            {
                TargetDistances[baseIndex + i] = -1f;
                RaycastCommands[baseIndex + i] = default;
            }
        }

        private void SetupRay(int rayIndex, float3 targetPoint)
        {
            float3 origin;
            float3 direction;
            float distanceToPoint;

            if (IsOrthographic)
            {
                float distAlongForward = math.dot(targetPoint - CameraPos, CameraForward);
                origin = targetPoint - CameraForward * distAlongForward;
                direction = CameraForward;
                distanceToPoint = distAlongForward;
            }
            else
            {
                float3 vectorToTarget = targetPoint - CameraPos;
                distanceToPoint = math.length(vectorToTarget);
                direction = vectorToTarget / distanceToPoint;
                origin = CameraPos;
            }

            TargetDistances[rayIndex] = distanceToPoint;
            RaycastCommands[rayIndex] = new RaycastCommand(origin, direction, distanceToPoint, Physics.DefaultRaycastLayers);
        }

        private bool IsInsideFrustum(float3 center, float3 extents)
        {
            for (int i = 0; i < 6; i++)
            {
                float4 plane = FrustumPlanes[i];
                // Tính toán khoảng cách
                float d = math.dot(plane.xyz, center) + plane.w;
                float r = math.dot(extents, math.abs(plane.xyz));
                // Nếu nằm hoàn toàn ở chiều âm của mặt phẳng -> Nằm ngoài Camera
                if (d + r < 0) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Tìm Cube hợp lệ trên màn hình, không bị che bởi vật cản. Hỗ trợ cả 2D và 3D.
    /// </summary>
    public static int GetObjectToShoot(
        Camera mainCamera,
        CubeShooterColor targetColor,
        NativeArray<PieceTargetData> inputPieces,
        ObjectBaseMono[] pieceReferences,
        NativeArray<RaycastCommand> raycastCommands,
        NativeArray<RaycastHit> raycastHits,
        NativeArray<float> targetDistances,
        NativeArray<float4> frustumPlanes)
    {
        if (!inputPieces.IsCreated || inputPieces.Length == 0 || !frustumPlanes.IsCreated) return -1;

        // 1. Trích xuất Frustum
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
        for (int i = 0; i < 6; i++)
        {
            frustumPlanes[i] = new float4(planes[i].normal, planes[i].distance);
        }

        int count = inputPieces.Length;

        // 2. Chạy Job chuẩn bị danh sách các tia Raycast cần bắn
        PrepareRaycastJob prepareJob = new PrepareRaycastJob
        {
            InputData = inputPieces,
            FrustumPlanes = frustumPlanes,
            TargetColor = targetColor,
            CameraPos = mainCamera.transform.position,
            CameraForward = mainCamera.transform.forward,
            CameraRight = mainCamera.transform.right,
            CameraUp = mainCamera.transform.up,
            IsOrthographic = mainCamera.orthographic,
            TargetDistances = targetDistances,
            RaycastCommands = raycastCommands
        };

        JobHandle prepareHandle = prepareJob.Schedule(count, 64);

        // 3. Đưa danh sách tia vào Physics Engine chạy đồng loạt (Batching - Cực kỳ tối ưu)
        // Lưu ý: Có 5 lệnh Raycast cho mỗi Cube (tâm + 4 góc)
        JobHandle raycastHandle = RaycastCommand.ScheduleBatch(raycastCommands, raycastHits, 32, prepareHandle);

        // Buộc hệ thống tính toán xong ngay trong frame này
        raycastHandle.Complete();

        // 4. Lọc kết quả tìm vật không bị che
        int selectedIndex = -1;
        for (int i = 0; i < count; i++)
        {
            bool isVisible = false;

            // Duyệt qua 5 tia của từng Cube (tâm + 4 góc ngoài)
            for (int j = 0; j < 5; j++)
            {
                int rayIndex = i * 5 + j;
                float distToPoint = targetDistances[rayIndex];

                if (distToPoint > 0f) // Nếu khối Cube vượt qua Frustum và Color
                {
                    RaycastHit hit = raycastHits[rayIndex];

                    // Nếu tia xuyên thấu (không đụng collider nào) -> Phần bề mặt này lộ diện!
                    if (hit.distance == 0f && hit.collider == null)
                    {
                        isVisible = true;
                        break;
                    }

                    // Đã đụng vào một Collider. Kiểm tra chính xác xem Collider đó có thuộc về Cube này không
                    if (hit.collider != null && pieceReferences != null && pieceReferences[i] != null)
                    {
                        Transform hitTransform = hit.collider.transform;
                        Transform targetTransform = pieceReferences[i].transform;

                        // Nếu vật bị chạm chính là Transform của Cube hoặc là con của Cube
                        if (hitTransform == targetTransform || hitTransform.IsChildOf(targetTransform))
                        {
                            isVisible = true;
                            break;
                        }
                    }
                }
            }

            // Nếu ÍT NHẤT 1 trong 5 tia (1 điểm trên bề mặt) có thể nhìn thấy được -> Chọn bắn!
            if (isVisible)
            {
                selectedIndex = i;
                break;
            }
        }

        return selectedIndex;
    }
}

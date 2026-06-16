using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Unity.Collections;

public class GPUOcclusionSystem
{
    public struct CubeData
    {
        public float3 position;
        public float3 extents;
    }

    private ComputeShader _computeShader;
    private int _kernel;
    private CommandBuffer _cmd;
    private Camera _cam;

    private ComputeBuffer _cubeDataBuffer;
    private ComputeBuffer _resultBuffer;
    private int[] _resultArray;

    private bool _isInitialized = false;

    public void Initialize(Camera camera)
    {
        if (_isInitialized) return;
        
        _cam = camera;
        _computeShader = Resources.Load<ComputeShader>("VisibilityCheck");
        if (_computeShader == null)
        {
            Debug.LogError("[GPUOcclusion] Không tìm thấy VisibilityCheck.compute trong thư mục Resources!");
            return;
        }

        _kernel = _computeShader.FindKernel("CSMain");
        
        if (_cam != null)
            _cam.depthTextureMode |= DepthTextureMode.Depth;

        _isInitialized = true;
    }

    public void Release()
    {
        if (_cam != null && _cmd != null)
        {
            _cam.RemoveCommandBuffer(CameraEvent.AfterDepthTexture, _cmd);
        }
        
        if (_cubeDataBuffer != null) _cubeDataBuffer.Release();
        if (_resultBuffer != null) _resultBuffer.Release();
        
        _cubeDataBuffer = null;
        _resultBuffer = null;
        _cmd = null;
        _isInitialized = false;
    }

    // Được gọi mỗi frame từ Model3DController.Update
    public void Tick(int count, NativeArray<PieceTargetData> cachedData)
    {
        if (!_isInitialized || _cam == null || count == 0) return;

        // Khởi tạo/Rebuild buffer nếu số lượng thay đổi
        if (_cubeDataBuffer == null || _cubeDataBuffer.count != count)
        {
            RebuildBuffers(count);
        }

        // 1. Chép dữ liệu vị trí mới nhất vào GPU
        CubeData[] inputData = new CubeData[count];
        for (int i = 0; i < count; i++)
        {
            inputData[i] = new CubeData
            {
                position = cachedData[i].Position,
                extents = cachedData[i].Extents
            };
        }
        _cubeDataBuffer.SetData(inputData);

        // 2. Cập nhật Ma trận của Camera cho frame này
        _computeShader.SetMatrix("_MatrixV", _cam.worldToCameraMatrix);
        Matrix4x4 gpuProj = GL.GetGPUProjectionMatrix(_cam.projectionMatrix, true);
        _computeShader.SetMatrix("_MatrixVP", gpuProj * _cam.worldToCameraMatrix);
        
        float far = _cam.farClipPlane;
        float near = _cam.nearClipPlane;
        float zc0, zc1;
        if (SystemInfo.usesReversedZBuffer)
        {
            zc0 = -1.0f + far / near;
            zc1 = 1.0f;
        }
        else
        {
            zc0 = 1.0f - far / near;
            zc1 = far / near;
        }
        _computeShader.SetVector("_ZBufferParams", new Vector4(zc0, zc1, zc0 / far, zc1 / far));
        
        _computeShader.SetVector("_CameraRight", _cam.transform.right);
        _computeShader.SetVector("_CameraUp", _cam.transform.up);
    }

    private void RebuildBuffers(int count)
    {
        if (_cam != null && _cmd != null)
        {
            _cam.RemoveCommandBuffer(CameraEvent.AfterDepthTexture, _cmd);
        }
        
        if (_cubeDataBuffer != null) _cubeDataBuffer.Release();
        if (_resultBuffer != null) _resultBuffer.Release();

        _cubeDataBuffer = new ComputeBuffer(count, 24);
        _resultBuffer = new ComputeBuffer(count, 4);
        _resultArray = new int[count];

        _computeShader.SetBuffer(_kernel, "_CubeDataBuffer", _cubeDataBuffer);
        _computeShader.SetBuffer(_kernel, "_ResultBuffer", _resultBuffer);
        _computeShader.SetInt("_Count", count);

        // Đăng ký CommandBuffer chạy tự động MỖI FRAME ngay sau khi Camera render xong Depth
        _cmd = new CommandBuffer { name = "GPUOcclusionCheck" };
        _cmd.SetComputeTextureParam(_computeShader, _kernel, "DepthTexture", new RenderTargetIdentifier("_CameraDepthTexture"));
        _cmd.DispatchCompute(_computeShader, _kernel, Mathf.CeilToInt(count / 64f), 1, 1);
        
        _cam.AddCommandBuffer(CameraEvent.AfterDepthTexture, _cmd);
    }

    public int GetTarget(CubeShooterColor targetColor, NativeArray<PieceTargetData> cachedData, ObjectBaseMono[] pieceReferences)
    {
        if (!_isInitialized || _resultBuffer == null) return -1;
        int count = cachedData.Length;
        if (count == 0) return -1;

        // Lấy kết quả từ frame trước (đã được GPU tính toán xong)
        _resultBuffer.GetData(_resultArray);

        int selectedIndex = -1;
        float minDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            // _resultArray[i] == 1 nghĩa là hoàn toàn hiển thị (nhìn thấy được)
            if (_resultArray[i] == 1 && pieceReferences[i] != null) 
            {
                var data = cachedData[i];
                if (data.Color == targetColor && !data.IsBulletIncoming)
                {
                    float dist = Vector3.Distance(_cam.transform.position, data.Position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        selectedIndex = i;
                    }
                }
            }
        }

        return selectedIndex;
    }
}

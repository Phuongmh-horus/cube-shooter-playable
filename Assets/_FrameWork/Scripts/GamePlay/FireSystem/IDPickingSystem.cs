using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class IDPickingSystem
{
    private RenderTexture _idTexture;
    private Texture2D _readbackTexture;
    private CommandBuffer _cmd;
    private Material _idMaterial;

    private int _width = 128;
    private int _height = 128;
    private MaterialPropertyBlock _block;

    private struct RenderData
    {
        public Mesh mesh;
        public Transform transform;
        public Renderer renderer;
    }

    private RenderData[][] _renderCache;
    private int[] _visiblePixelCountsCache;
    private Color32[] _pixelsCache;
    private int _lastRenderFrame = -1;

    public void Initialize()
    {
        Shader shader = Shader.Find("Hidden/IDPicker");
        if (shader == null)
        {
            Debug.LogError("[IDPicking] Không tìm thấy shader Hidden/IDPicker trong thư mục Resources!");
            return;
        }
        _idMaterial = new Material(shader);

        // Thiết lập ZTest tương thích với cấu hình chiều sâu phần cứng (Reversed-Z trên PC/Console/Modern Mobile, Standard Z trên WebGL/GLES2)
        // CompareFunction: 4 = LEqual (Standard), 7 = GEqual (Reversed-Z)
        float zTestValue = SystemInfo.usesReversedZBuffer ? 7f : 4f;
        _idMaterial.SetFloat("_ZTest", zTestValue);

        _cmd = new CommandBuffer { name = "IDPicking" };
        _block = new MaterialPropertyBlock();
    }

    public void PrepareCache(ObjectBaseMono[] pieces)
    {
        int count = pieces.Length;
        if (_renderCache == null || _renderCache.Length < count)
        {
            _renderCache = new RenderData[count][];
        }

        for (int i = 0; i < count; i++)
        {
            if (pieces[i] == null) continue;

            Renderer[] rnds = pieces[i].GetComponentsInChildren<Renderer>(true);
            System.Collections.Generic.List<RenderData> rDataList = new System.Collections.Generic.List<RenderData>();
            foreach (var r in rnds)
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    rDataList.Add(new RenderData { mesh = mf.sharedMesh, transform = r.transform, renderer = r });
                }
            }
            _renderCache[i] = rDataList.ToArray();
        }
    }

    public void Release()
    {
        if (_idTexture != null)
        {
            _idTexture.Release();
            _idTexture = null;
        }
        if (_readbackTexture != null)
        {
            Object.Destroy(_readbackTexture);
            _readbackTexture = null;
        }
        if (_idMaterial != null)
        {
            Object.Destroy(_idMaterial);
            _idMaterial = null;
        }
        if (_cmd != null)
        {
            _cmd.Dispose();
            _cmd = null;
        }
        _renderCache = null;
        _visiblePixelCountsCache = null;
        _pixelsCache = null;
        _lastRenderFrame = -1;
    }

    public int GetTarget(Camera cam, CubeShooterColor targetColor, NativeArray<PieceTargetData> cachedData, ObjectBaseMono[] pieces)
    {
        if (_idMaterial == null) return -1;
        int count = pieces.Length;
        if (count == 0) return -1;

        if (_renderCache == null || _renderCache.Length < count)
        {
            _renderCache = new RenderData[count][];
        }

        int currentFrame = Application.isPlaying ? Time.frameCount : -1;
        if (currentFrame == -1 || _lastRenderFrame != currentFrame)
        {
            _lastRenderFrame = currentFrame;

            // Tự động điều chỉnh kích thước RenderTexture & Texture2D theo tỷ lệ màn hình (Aspect Ratio) của Camera
            // Tự động điều chỉnh kích thước RenderTexture & Texture2D theo tỷ lệ màn hình thực tế (pixelWidth / pixelHeight) của Camera
            float aspect = cam.pixelHeight > 0 ? (float)cam.pixelWidth / cam.pixelHeight : cam.aspect;
            int targetHeight = Mathf.Max(16, Mathf.RoundToInt(_width / aspect));
            if (_idTexture == null || _readbackTexture == null || _idTexture.width != _width || _idTexture.height != targetHeight)
            {
                if (_idTexture != null) _idTexture.Release();
                if (_readbackTexture != null) Object.Destroy(_readbackTexture);

                // Sử dụng RenderTextureReadWrite.Linear để bỏ qua hoàn toàn các cơ chế sRGB/Linear Color Space conversion của Unity
                _idTexture = new RenderTexture(_width, targetHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                _idTexture.filterMode = FilterMode.Point;
                
                _readbackTexture = new Texture2D(_width, targetHeight, TextureFormat.RGBA32, false, true);
                _readbackTexture.filterMode = FilterMode.Point;
            }

            _cmd.Clear();
            _cmd.SetRenderTarget(_idTexture);
            
            float clearDepth = SystemInfo.usesReversedZBuffer ? 0f : 1f;
            _cmd.ClearRenderTarget(true, true, Color.black, clearDepth); // Đen (0,0,0) là Nền vũ trụ (Background)

            // Đồng bộ hoàn toàn góc nhìn với Camera chính (sử dụng GL.GetGPUProjectionMatrix để tương thích API đồ họa và RenderTexture)
            Matrix4x4 gpuProj = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);
            _cmd.SetViewProjectionMatrices(cam.worldToCameraMatrix, gpuProj);

            // Chuẩn bị lệnh vẽ tất cả các Cube bằng một màu đặc trưng duy nhất (ID)
            for (int i = 0; i < count; i++)
            {
                if (pieces[i] == null || !pieces[i].gameObject.activeInHierarchy) continue;

                if (_renderCache[i] == null)
                {
                    Renderer[] rnds = pieces[i].GetComponentsInChildren<Renderer>(true);
                    System.Collections.Generic.List<RenderData> rDataList = new System.Collections.Generic.List<RenderData>();
                    foreach (var r in rnds)
                    {
                        MeshFilter mf = r.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null)
                        {
                            rDataList.Add(new RenderData { mesh = mf.sharedMesh, transform = r.transform, renderer = r });
                        }
                    }
                    _renderCache[i] = rDataList.ToArray();
                }

                var rDatas = _renderCache[i];
                if (rDatas.Length > 0)
                {
                    // Gán ID = i + 1 (Tránh dính màu đen 0 của Background)
                    int id = i + 1;
                    Color idColor = new Color(
                        ((id) & 0xFF) / 255f,
                        ((id >> 8) & 0xFF) / 255f,
                        ((id >> 16) & 0xFF) / 255f,
                        1f
                    );

                    // Dùng SetVector thay vì SetColor để bỏ qua các cơ chế tự động gamma ↔ linear conversion của Unity
                    _block.SetVector("_ColorID", idColor);

                    for (int j = 0; j < rDatas.Length; j++)
                    {
                        var rData = rDatas[j];
                        if (rData.renderer.enabled)
                        {
                            _cmd.DrawMesh(rData.mesh, rData.transform.localToWorldMatrix, _idMaterial, 0, 0, _block);
                        }
                    }
                }
            }

            // Bắt Card đồ họa thực thi lệnh vẽ ngay lập tức
            Graphics.ExecuteCommandBuffer(_cmd);

            // Chép toàn bộ bức ảnh vừa vẽ từ Card đồ họa về RAM của CPU
            RenderTexture active = RenderTexture.active;
            RenderTexture.active = _idTexture;
            _readbackTexture.ReadPixels(new Rect(0, 0, _width, _readbackTexture.height), 0, 0);
            _readbackTexture.Apply();
            RenderTexture.active = active;

            NativeArray<Color32> pixels = _readbackTexture.GetRawTextureData<Color32>();

            if (_pixelsCache == null || _pixelsCache.Length != pixels.Length)
            {
                _pixelsCache = new Color32[pixels.Length];
            }
            pixels.CopyTo(_pixelsCache);

            // Mảng đánh dấu các ID đang có mặt trên màn hình
            if (_visiblePixelCountsCache == null || _visiblePixelCountsCache.Length < count + 1)
            {
                _visiblePixelCountsCache = new int[count + 1];
            }
            else
            {
                System.Array.Clear(_visiblePixelCountsCache, 0, count + 1);
            }

            for (int p = 0; p < _pixelsCache.Length; p++)
            {
                Color32 c = _pixelsCache[p];
                if (c.r != 0 || c.g != 0 || c.b != 0) // Loại bỏ Nền đen
                {
                    // Dịch ngược màu RGB về số nguyên (ID)
                    int id = c.r | (c.g << 8) | (c.b << 16);
                    if (id >= 1 && id <= count)
                    {
                        _visiblePixelCountsCache[id]++;
                    }
                }
            }
        }

        int selectedIndex = -1;
        float minDistanceSqr = float.MaxValue;
        Vector3 camPos = cam.transform.position;

        // Chọn mục tiêu thỏa mãn (cùng màu, chưa bị nhắm) và GẦN CAMERA NHẤT
        for (int i = 0; i < count; i++)
        {
            // Kiểm tra xem ID (i + 1) của khối này có xuất hiện trên ảnh đủ nhiều để loại trừ nhiễu răng cưa (aliasing) không
            if (_visiblePixelCountsCache[i + 1] >= 8)
            {
                var data = cachedData[i];
                if (data.Color == targetColor && !data.IsBulletIncoming)
                {
                    float distSqr = (camPos - (Vector3)data.Position).sqrMagnitude;
                    if (distSqr < minDistanceSqr)
                    {
                        minDistanceSqr = distSqr;
                        selectedIndex = i;
                    }
                }
            }
        }

        return selectedIndex;
    }
}

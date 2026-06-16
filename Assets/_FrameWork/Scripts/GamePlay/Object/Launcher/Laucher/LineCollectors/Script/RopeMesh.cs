using UnityEngine;



#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GogoGaga.OptimizedRopesAndCables
{
    [RequireComponent(typeof(MeshFilter)), RequireComponent(typeof(MeshRenderer)), RequireComponent(typeof(Rope))]
    public class RopeMesh : MonoBehaviour
    {
        [Range(3, 25)] public int OverallDivision = 6;
        [Range(0.01f, 10)] public float ropeWidth = 0.3f;
        [Range(3, 20)] public int radialDivision = 8;
        [Tooltip("For now only base color is applied")]
        public Material material;
        [Tooltip("Tiling density per meter of the rope")]
        public float tilingPerMeter = 1.0f;

        private Rope rope;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh ropeMesh;
        private bool isStartOrEndPointMissing;

        // Cached arrays to avoid GC allocations
        private Vector3[] verticesArray;
        private int[] trianglesArray;
        private Vector2[] uvsArray;

        // Cached cos/sin values to avoid redundant Mathf.Cos/Sin calls
        private float[] cosCache;
        private float[] sinCache;

        // Cached values to avoid redundant mesh generation
        private Vector3 lastStartPointPos;
        private Vector3 lastEndPointPos;
        private Vector3 lastCurrentValue;
        private bool lastHasStartPoint;
        private bool lastHasEndPoint;
        private float lastRopeWidth;
        private int lastRadialDivision;
        private int lastOverallDivision;
        private bool forceUpdateMesh = true;

        [Header("ROPE TEXTURE EQUALIZER")]
        public Rope RopeLogic;
        public Transform StartPoint;
        public Transform EndPoint;
        public Color FirstHalfColor;
        public Color SecondHalfColor;
        public float ThresholdDistance = 1.0f;
        public float ThresholdOffsetY = -0.5f;
        public int UpdateRate = 5;
        private int frameCounter = 0;
        private MaterialPropertyBlock block;

        public void OnValidate()
        {
            InitializeComponents();
            if (rope.IsPrefab)
                return;

            SubscribeToRopeEvents();
            if (meshRenderer && material)
            {
                meshRenderer.material = material;
            }
            // We are using delay call to generate mesh to avoid errors in the editor
#if UNITY_EDITOR
            EditorApplication.delayCall += DelayedGenerateMesh;
#endif
        }
        private void Awake()
        {
            InitializeComponents();
            SubscribeToRopeEvents();
            forceUpdateMesh = true;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
#if UNITY_EDITOR
                EditorApplication.delayCall += DelayedGenerateMesh;
#endif
            }
            SubscribeToRopeEvents();
            ReOffsetTextureTwoHalf();
            forceUpdateMesh = true;
        }

        private void OnDisable()
        {
            UnsubscribeFromRopeEvents();
#if UNITY_EDITOR
            EditorApplication.delayCall -= DelayedGenerateMesh;
#endif
        }

        private void InitializeComponents()
        {
            if (!rope)
                rope = GetComponent<Rope>();
            if (!meshFilter)
                meshFilter = GetComponent<MeshFilter>();
            if (!meshRenderer)
                meshRenderer = GetComponent<MeshRenderer>();

            CheckEndPoints();
        }

        private void CheckEndPoints()
        {
            // Check if start and end points are assigned
            if (gameObject.scene.rootCount == 0)
            {
                isStartOrEndPointMissing = false;
                return;
            }

            if (rope.StartPoint == null || rope.EndPoint == null)
            {
                isStartOrEndPointMissing = true;
                Debug.LogError("StartPoint or EndPoint is not assigned.", gameObject);
            }
            else
            {
                isStartOrEndPointMissing = false;
            }
        }

        private void SubscribeToRopeEvents()
        {
            UnsubscribeFromRopeEvents();
            if (rope != null)
            {
                rope.OnPointsChanged += GenerateMesh;
            }
        }

        private void UnsubscribeFromRopeEvents()
        {
            if (rope != null)
            {
                rope.OnPointsChanged -= GenerateMesh;
            }
        }

        public void CreateRopeMesh(Vector3[] points, float radius, int segmentsPerWire)
        {
            // Validate input
            if (points == null || points.Length < 2)
            {
                Debug.LogError("Need at least two points to create a rope mesh.", gameObject);
                return;
            }

            if (ropeMesh == null)
            {
                ropeMesh = new Mesh { name = "RopeMesh" };
                meshFilter.mesh = ropeMesh;
            }
            else
            {
                ropeMesh.Clear();
            }

            // 1. Calculate required sizes and allocate/resize arrays if needed
            int tubeVertCount = points.Length * (segmentsPerWire + 1);
            int capVertCount = 2 * (segmentsPerWire + 2);
            int totalVertices = tubeVertCount + capVertCount;

            int tubeTriCount = (points.Length - 1) * segmentsPerWire * 6;
            int capTriCount = 2 * segmentsPerWire * 3;
            int totalTriangles = tubeTriCount + capTriCount;

            if (verticesArray == null || verticesArray.Length != totalVertices)
            {
                verticesArray = new Vector3[totalVertices];
                uvsArray = new Vector2[totalVertices];
            }
            if (trianglesArray == null || trianglesArray.Length != totalTriangles)
            {
                trianglesArray = new int[totalTriangles];
            }

            // 2. Cache Cos/Sin values for segmentsPerWire
            if (cosCache == null || cosCache.Length != segmentsPerWire + 1)
            {
                cosCache = new float[segmentsPerWire + 1];
                sinCache = new float[segmentsPerWire + 1];
                for (int j = 0; j <= segmentsPerWire; j++)
                {
                    float angle = j * Mathf.PI * 2f / segmentsPerWire;
                    cosCache[j] = Mathf.Cos(angle);
                    sinCache[j] = Mathf.Sin(angle);
                }
            }

            // 3. Cache worldToLocal matrix to avoid calling transform.InverseTransformPoint in the loop
            Matrix4x4 worldToLocal = transform.worldToLocalMatrix;

            float currentLength = 0f;
            int vIndex = 0;
            int tIndex = 0;

            // Generate vertices and UVs for each segment along the points
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 direction = i < points.Length - 1 ? points[i + 1] - points[i] : points[i] - points[i - 1];
                Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);

                // Create vertices around a circle at this point
                for (int j = 0; j <= segmentsPerWire; j++)
                {
                    Vector3 offset = new Vector3(cosCache[j], sinCache[j], 0) * radius;
                    verticesArray[vIndex] = worldToLocal.MultiplyPoint3x4(points[i] + rotation * offset);

                    float u = (float)j / segmentsPerWire;
                    float v = currentLength * tilingPerMeter;
                    uvsArray[vIndex] = new Vector2(u, v);
                    vIndex++;
                }

                if (i < points.Length - 1)
                {
                    currentLength += Vector3.Distance(points[i], points[i + 1]);
                }
            }

            // Generate triangles for each segment
            for (int i = 0; i < points.Length - 1; i++)
            {
                for (int j = 0; j < segmentsPerWire; j++)
                {
                    int current = i * (segmentsPerWire + 1) + j;
                    int next = current + 1;
                    int nextSegment = current + segmentsPerWire + 1;
                    int nextSegmentNext = nextSegment + 1;

                    trianglesArray[tIndex++] = current;
                    trianglesArray[tIndex++] = next;
                    trianglesArray[tIndex++] = nextSegment;

                    trianglesArray[tIndex++] = next;
                    trianglesArray[tIndex++] = nextSegmentNext;
                    trianglesArray[tIndex++] = nextSegment;
                }
            }

            // Generate vertices, triangles, and UVs for the start cap
            int startCapCenterIndex = vIndex;
            verticesArray[vIndex] = worldToLocal.MultiplyPoint3x4(points[0]);
            uvsArray[vIndex] = new Vector2(0.5f, 0); // Center of the cap
            vIndex++;

            Quaternion startRotation = Quaternion.LookRotation(points[1] - points[0]);
            for (int j = 0; j <= segmentsPerWire; j++)
            {
                Vector3 offset = new Vector3(cosCache[j], sinCache[j], 0) * radius;
                verticesArray[vIndex] = worldToLocal.MultiplyPoint3x4(points[0] + startRotation * offset);

                if (j < segmentsPerWire)
                {
                    trianglesArray[tIndex++] = startCapCenterIndex;
                    trianglesArray[tIndex++] = startCapCenterIndex + j + 1;
                    trianglesArray[tIndex++] = startCapCenterIndex + j + 2;
                }

                uvsArray[vIndex] = new Vector2((cosCache[j] + 1) / 2, (sinCache[j] + 1) / 2);
                vIndex++;
            }

            // Generate vertices, triangles, and UVs for the end cap
            int endCapCenterIndex = vIndex;
            verticesArray[vIndex] = worldToLocal.MultiplyPoint3x4(points[points.Length - 1]);
            uvsArray[vIndex] = new Vector2(0.5f, currentLength * tilingPerMeter); // Center of the cap
            vIndex++;

            Quaternion endRotation = Quaternion.LookRotation(points[points.Length - 1] - points[points.Length - 2]);
            for (int j = 0; j <= segmentsPerWire; j++)
            {
                Vector3 offset = new Vector3(cosCache[j], sinCache[j], 0) * radius;
                verticesArray[vIndex] = worldToLocal.MultiplyPoint3x4(points[points.Length - 1] + endRotation * offset);

                if (j < segmentsPerWire)
                {
                    trianglesArray[tIndex++] = endCapCenterIndex;
                    trianglesArray[tIndex++] = endCapCenterIndex + j + 1;
                    trianglesArray[tIndex++] = endCapCenterIndex + j + 2;
                }

                uvsArray[vIndex] = new Vector2((cosCache[j] + 1) / 2, (sinCache[j] + 1) / 2);
                vIndex++;
            }

            ropeMesh.vertices = verticesArray;
            ropeMesh.triangles = trianglesArray;
            ropeMesh.uv = uvsArray;
            ropeMesh.RecalculateNormals();
        }

        private bool NeedUpdateMesh()
        {
            if (forceUpdateMesh) return true;

            bool hasStart = rope.StartPoint != null;
            bool hasEnd = rope.EndPoint != null;

            if (hasStart != lastHasStartPoint || hasEnd != lastHasEndPoint)
                return true;

            if (hasStart && rope.StartPoint.position != lastStartPointPos)
                return true;

            if (hasEnd && rope.EndPoint.position != lastEndPointPos)
                return true;

            if (rope.CurrentValue != lastCurrentValue)
                return true;

            if (!Mathf.Approximately(ropeWidth, lastRopeWidth) || radialDivision != lastRadialDivision || OverallDivision != lastOverallDivision)
                return true;

            return false;
        }

        void GenerateMesh()
        {
            if (this == null || rope == null || meshFilter == null)
            {
                return;
            }

            // Kiểm tra trực tiếp thay vì dùng cached bool (isStartOrEndPointMissing)
            // vì điểm đầu/cuối có thể bị null lúc runtime (despawn) mà bool không được cập nhật
            if (rope.StartPoint == null || rope.EndPoint == null)
            {
                if (meshFilter.sharedMesh != null)
                {
                    meshFilter.sharedMesh.Clear();
                }
                lastHasStartPoint = false;
                lastHasEndPoint = false;
                forceUpdateMesh = false;
                return;
            }

            Vector3[] points = new Vector3[OverallDivision + 1];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = rope.GetPointAt(i / (float)OverallDivision);
            }
            CreateRopeMesh(points, ropeWidth, radialDivision);

            // Update cached values to avoid redundant generation
            lastStartPointPos = rope.StartPoint.position;
            lastEndPointPos = rope.EndPoint.position;
            lastCurrentValue = rope.CurrentValue;
            lastHasStartPoint = true;
            lastHasEndPoint = true;
            lastRopeWidth = ropeWidth;
            lastRadialDivision = radialDivision;
            lastOverallDivision = OverallDivision;
            forceUpdateMesh = false;
        }

        void Update()
        {
            if (rope.IsPrefab) return;

            if (Application.isPlaying)
            {
                if (NeedUpdateMesh())
                {
                    GenerateMesh();
                }
            }

            if (frameCounter > UpdateRate)
            {
                ReOffsetTextureTwoHalf();
                frameCounter = 0;
            }
            frameCounter++;
        }

        private void DelayedGenerateMesh()
        {
            if (this != null)
            {
                GenerateMesh();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromRopeEvents();
#if UNITY_EDITOR
            EditorApplication.delayCall -= DelayedGenerateMesh;
#endif

            if (meshRenderer != null)
                Destroy(meshRenderer);
            if (meshFilter != null)
                Destroy(meshFilter);
        }

        #region rope texture re-offset
        public void SetColor(Color _color)
        {
            if (meshRenderer != null)
            {
                // Create a MaterialPropertyBlock to set the color
                if (block == null)
                {
                    block = new MaterialPropertyBlock();
                    meshRenderer.GetPropertyBlock(block);
                }
                block.SetColor("_Color", _color);

                // Apply the color to all renderers in the pipe part
                meshRenderer.SetPropertyBlock(block);
            }
        }

        public void SetColor(Color _firstColor, Color _secondColor)
        {
            FirstHalfColor = _firstColor;
            SecondHalfColor = _secondColor;
            if (meshRenderer != null)
            {
                // Create a MaterialPropertyBlock to set the color
                if (block == null)
                {
                    block = new MaterialPropertyBlock();
                    meshRenderer.GetPropertyBlock(block);
                }
                block.SetColor("_TwoHalfColorsColor1", FirstHalfColor);
                block.SetColor("_TwoHalfColorsColor2", SecondHalfColor);

                // Apply the color to all renderers in the pipe part
                meshRenderer.SetPropertyBlock(block);
            }
        }

        public void ReOffsetTextureTwoHalf()
        {
            if (RopeLogic == null || RopeLogic.StartPoint == null || RopeLogic.EndPoint == null) return;
            if (block == null)
            {
                block = new MaterialPropertyBlock();
                meshRenderer.GetPropertyBlock(block);

                // Re-apply two-half colors only when the block is newly created
                // to avoid redundant updates every frame.
                block.SetColor("_TwoHalfColorsColor1", FirstHalfColor);
                block.SetColor("_TwoHalfColorsColor2", SecondHalfColor);
            }

            // Calculate the actual arc length of the rope
            float actualLength = 0f;
            Vector3 prevP = RopeLogic.GetPointAt(0);
            for (int i = 1; i <= OverallDivision; i++)
            {
                Vector3 p = RopeLogic.GetPointAt(i / (float)OverallDivision);
                actualLength += Vector3.Distance(prevP, p);
                prevP = p;
            }

            // In CreateRopeMesh, the V coordinate of the UV goes up to (actualLength * tilingPerMeter)
            float maxV = actualLength * tilingPerMeter;

            // Scale the mask texture so that its UV range always exactly covers the whole rope length.
            // This ensures both colors are always visible regardless of how short or stretched the rope is.
            float scaleY = maxV > 0.001f ? 1f / maxV : 1f;

            // We set offset to 0 so the UV range is precisely [0, 1]. 
            // If we use ThresholdOffsetY (e.g. -0.5), the UV would be [-0.5, 0.5], 
            // which clamps to the first half of the texture (only Red).
            block.SetVector("_TwoHalfColorsMaskTex_ST", new Vector4(1f, scaleY, 0f, 0f));
            meshRenderer.SetPropertyBlock(block);
        }

        public void SetColorGreen()
        {
            SetColor(Color.green);
        }
        public void SetColorRed()
        {
            SetColor(Color.red);
        }
        #endregion
    }
}
// GPUOcclusionSystem.cs - Luna/Playable Ads Compatible Version
// Changes: ComputeShader and CommandBuffer are NOT supported on WebGL / Luna.
//          This class is now a no-op stub. Actual occlusion/visibility checking
//          is handled entirely by MathTargetPickingSystem (CPU ray-OBB test),

using UnityEngine;

public class GPUOcclusionSystem
{
    public struct CubeData
    {
        public Vector3 position;
        public Vector3 extents;
    }

    private bool _isInitialized = false;

    /// <summary>
    /// No-op on Luna/WebGL. ComputeShader is unsupported.
    /// </summary>
    public void Initialize(Camera camera)
    {
        // ComputeShaders are not available in WebGL / Luna builds.
        // Visibility is handled by MathTargetPickingSystem instead.
        _isInitialized = true;
    }

    /// <summary>No-op.</summary>
    public void Release()
    {
        _isInitialized = false;
    }
    public void Tick(int count, PieceTargetData[] cachedData) { }

    /// <summary>
    /// Always returns -1 — callers should use MathTargetPickingSystem.GetTarget() instead.
    /// </summary>
    public int GetTarget(CubeShooterColor targetColor, PieceTargetData[] cachedData, ObjectBaseMono[] pieceReferences)
    {
        return -1;
    }
}
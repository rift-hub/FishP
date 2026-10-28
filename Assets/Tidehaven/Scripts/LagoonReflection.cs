using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways, DefaultExecutionOrder(280)]
public sealed class LagoonReflection : MonoBehaviour
{
    public Camera source;
    public Renderer water;
    public LayerMask reflectedLayers = 1 << 30;
    [Range(128, 1024)] public int resolution = 512;
    [Range(10, 60)] public int refreshRate = 24;
    Camera mirror;
    RenderTexture texture;
    MaterialPropertyBlock properties;
    readonly UniversalRenderPipeline.SingleCameraRequest request = new UniversalRenderPipeline.SingleCameraRequest();
    double nextRender;
    static readonly int TextureId = Shader.PropertyToID("_LagoonReflection");
    static readonly int MatrixId = Shader.PropertyToID("_LagoonReflectionVP");
    static readonly int ReadyId = Shader.PropertyToID("_ReflectionReady");

    void LateUpdate()
    {
        if (!source || !water || !water.enabled) return;
        double now = Time.realtimeSinceStartupAsDouble;
        if (now < nextRender) return;
        nextRender = now + 1.0 / refreshRate;
        if (!mirror)
        {
            var go = new GameObject("Lagoon reflection camera") { hideFlags = HideFlags.HideAndDontSave };
            mirror = go.AddComponent<Camera>();
            mirror.enabled = false;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
        }
        int height = Mathf.Max(64, Mathf.RoundToInt(resolution / source.aspect));
        if (!texture || texture.width != resolution || texture.height != height)
        {
            ReleaseTexture();
            texture = new RenderTexture(resolution, height, 16, RenderTextureFormat.ARGBHalf)
            { name = "Lagoon reflection", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            texture.Create();
        }
        mirror.CopyFrom(source);
        mirror.enabled = false;
        mirror.clearFlags = CameraClearFlags.SolidColor;
        mirror.backgroundColor = Color.clear;
        mirror.cullingMask = reflectedLayers;
        mirror.allowMSAA = false;
        mirror.useOcclusionCulling = false;
        mirror.farClipPlane = Mathf.Min(source.farClipPlane, 90);
        float level = transform.position.y;
        var reflection = Matrix4x4.identity;
        reflection.m11 = -1;
        reflection.m13 = 2 * level;
        var position = source.transform.position;
        position.y = 2 * level - position.y;
        mirror.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.Reflect(source.transform.forward, Vector3.up), Vector3.Reflect(source.transform.up, Vector3.up)));
        mirror.worldToCameraMatrix = source.worldToCameraMatrix * reflection;
        mirror.projectionMatrix = source.projectionMatrix;
        Vector3 clipPoint = mirror.worldToCameraMatrix.MultiplyPoint(new Vector3(0, level + .04f, 0));
        Vector3 clipNormal = mirror.worldToCameraMatrix.MultiplyVector(Vector3.up).normalized;
        mirror.projectionMatrix = mirror.CalculateObliqueMatrix(new Vector4(clipNormal.x, clipNormal.y, clipNormal.z, -Vector3.Dot(clipPoint, clipNormal)));
        request.destination = texture;
        bool oldCulling = GL.invertCulling;
        try
        {
            GL.invertCulling = !oldCulling;
            RenderPipeline.SubmitRenderRequest(mirror, request);
        }
        finally { GL.invertCulling = oldCulling; }
        if (properties == null) properties = new MaterialPropertyBlock();
        water.GetPropertyBlock(properties);
        properties.SetTexture(TextureId, texture);
        properties.SetMatrix(MatrixId, GL.GetGPUProjectionMatrix(mirror.projectionMatrix, true) * mirror.worldToCameraMatrix);
        properties.SetFloat(ReadyId, 1);
        water.SetPropertyBlock(properties);
    }
    void ReleaseTexture()
    {
        if (!texture) return;
        texture.Release();
        if (Application.isPlaying) Destroy(texture); else DestroyImmediate(texture);
        texture = null;
    }
    void OnDisable()
    {
        if (water)
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            water.GetPropertyBlock(properties); properties.SetFloat(ReadyId, 0); water.SetPropertyBlock(properties);
        }
        ReleaseTexture();
        if (mirror) { if (Application.isPlaying) Destroy(mirror.gameObject); else DestroyImmediate(mirror.gameObject); }
    }
}

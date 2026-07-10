using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//[ExecuteAlways]
public class PlanarReflectionManager : MonoBehaviour
{
   public Camera _mainCamera = null;
    public Camera _reflectionCamera = null;
    public Transform _planar = null;
    [Range(0, 1)] public float _reflectionFactor = 0.5f;
    private const float ClipPlaneOffset = 0.001f;
    
    //private Material _planarMaterial = null;
    //private RenderTexture _reflectionRenderTarget = null;
    
    void Start()
    {
        _reflectionCamera.enabled = false;
        _reflectionCamera.useOcclusionCulling = false;
    }

    void OnDisable()
    {
        _reflectionCamera.ResetWorldToCameraMatrix();
        _reflectionCamera.ResetProjectionMatrix();
    }
    
    void LateUpdate()
    {
        RenderReflection();
        //_planarMaterial.SetFloat(Shader.PropertyToID("_ReflectionFactor"),_reflectionFactor);
    }
    
    private void RenderReflection()
    {
        Vector3 planePosition = _planar.position;
        Vector3 planeNormal = _planar.up.normalized;
        Vector4 plane = new Vector4(
            planeNormal.x,
            planeNormal.y,
            planeNormal.z,
            -Vector3.Dot(planeNormal, planePosition));

        Matrix4x4 reflectionMatrix = CalculateReflectionMatrix(plane);
        Vector3 cameraPosition = reflectionMatrix.MultiplyPoint(_mainCamera.transform.position);
        Vector3 cameraForward = Vector3.Reflect(_mainCamera.transform.forward, planeNormal);
        Vector3 cameraUp = Vector3.Reflect(_mainCamera.transform.up, planeNormal);

        _reflectionCamera.nearClipPlane = _mainCamera.nearClipPlane;
        _reflectionCamera.farClipPlane = _mainCamera.farClipPlane;
        _reflectionCamera.orthographic = _mainCamera.orthographic;
        _reflectionCamera.orthographicSize = _mainCamera.orthographicSize;
        _reflectionCamera.transform.SetPositionAndRotation(
            cameraPosition,
            Quaternion.LookRotation(cameraForward, cameraUp));

        _reflectionCamera.worldToCameraMatrix = _mainCamera.worldToCameraMatrix * reflectionMatrix;
        _reflectionCamera.projectionMatrix = _mainCamera.projectionMatrix;
        Vector4 clipPlane = CameraSpacePlane(planePosition, planeNormal);
        _reflectionCamera.projectionMatrix = _reflectionCamera.CalculateObliqueMatrix(clipPlane);

        bool previousInvertCulling = GL.invertCulling;
        try
        {
            GL.invertCulling = !previousInvertCulling;
            _reflectionCamera.Render();
        }
        finally
        {
            GL.invertCulling = previousInvertCulling;
        }
    }

    private Vector4 CameraSpacePlane(Vector3 position, Vector3 normal)
    {
        Vector3 offsetPosition = position - normal * ClipPlaneOffset;
        Matrix4x4 viewMatrix = _reflectionCamera.worldToCameraMatrix;
        Vector3 cameraPosition = viewMatrix.MultiplyPoint(offsetPosition);
        Vector3 cameraNormal = viewMatrix.MultiplyVector(normal).normalized;

        return new Vector4(
            cameraNormal.x,
            cameraNormal.y,
            cameraNormal.z,
            -Vector3.Dot(cameraPosition, cameraNormal));
    }

    private static Matrix4x4 CalculateReflectionMatrix(Vector4 plane)
    {
        Matrix4x4 matrix = Matrix4x4.identity;
        matrix.m00 = 1f - 2f * plane.x * plane.x;
        matrix.m01 = -2f * plane.x * plane.y;
        matrix.m02 = -2f * plane.x * plane.z;
        matrix.m03 = -2f * plane.w * plane.x;
        matrix.m10 = -2f * plane.y * plane.x;
        matrix.m11 = 1f - 2f * plane.y * plane.y;
        matrix.m12 = -2f * plane.y * plane.z;
        matrix.m13 = -2f * plane.w * plane.y;
        matrix.m20 = -2f * plane.z * plane.x;
        matrix.m21 = -2f * plane.z * plane.y;
        matrix.m22 = 1f - 2f * plane.z * plane.z;
        matrix.m23 = -2f * plane.w * plane.z;
        return matrix;
    }
}

using UnityEngine;

[ExecuteAlways]
[ImageEffectAllowedInSceneView]
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
internal sealed class GenshinAtmosphereCamera : MonoBehaviour
{
    [SerializeField] private Light sceneSun;
    [SerializeField] private Material skyMaterial;
    [SerializeField] private Material gradingMaterial;

    private static readonly int SunDirectionId = Shader.PropertyToID("_ES_SunDirection");

    private void OnPreCull()
    {
        // Use the scene light for both Game and Scene views, rather than a second independent sun.
        Vector3 direction = -sceneSun.transform.forward;
        skyMaterial.SetVector(SunDirectionId, new Vector4(direction.x, direction.y, direction.z, 0));
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(source, destination, gradingMaterial);
    }
}

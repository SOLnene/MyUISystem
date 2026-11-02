using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
internal sealed class GenshinEnvironmentLighting : MonoBehaviour
{
    [SerializeField] private Texture2D environmentBRDF;

    private static readonly int BrdfId = Shader.PropertyToID("_GenshinEnvBRDF");
    private static readonly int TexelSizeId = Shader.PropertyToID("_GenshinEnvBRDFTexelSize");
    private static readonly int EnabledId = Shader.PropertyToID("_GenshinEnvironmentEnabled");
    private static GenshinEnvironmentLighting owner;

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
#if UNITY_EDITOR
        // Runtime scene events do not report active-scene changes in Edit mode.
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
#endif
        BindForActiveScene();
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
#if UNITY_EDITOR
        EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
#endif
        ReleaseEnvironment();
    }

    private void OnActiveSceneChanged(Scene previous, Scene next)
    {
        BindForActiveScene();
    }

    private void BindForActiveScene()
    {
        if (gameObject.scene != SceneManager.GetActiveScene())
        {
            ReleaseEnvironment();
            return;
        }

        // The LUT is a required scene dependency. Its dimensions also preserve the native UV padding.
        Vector2 texelSize = environmentBRDF.texelSize;
        Shader.SetGlobalTexture(BrdfId, environmentBRDF);
        Shader.SetGlobalVector(TexelSizeId, new Vector4(texelSize.x, texelSize.y, environmentBRDF.width, environmentBRDF.height));
        Shader.SetGlobalFloat(EnabledId, 1);
        owner = this;
        DynamicGI.UpdateEnvironment();
    }

    private void ReleaseEnvironment()
    {
        // Additive scene callbacks may bind a new owner before the old scene is disabled.
        if (owner != this)
            return;

        Shader.SetGlobalFloat(EnabledId, 0);
        Shader.SetGlobalTexture(BrdfId, null);
        owner = null;
    }
}

using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Components.CharacterDetail
{
    public class CharacterIconButton : MonoBehaviour
    {
        [SerializeField]
        Button button;
        [SerializeField]
        Image icon;

        public Button Button => button;

        public void SetInteractable(bool interactable)
        {
            button.interactable = interactable;
        }

        public void LoadIcon(string characterKey)
        {
            string iconAddress = CharacterVisualAddressResolver.ResolveIcon(characterKey);
            IconLoader.SetSpriteAsync(icon, iconAddress, this.GetCancellationTokenOnDestroy()).Forget();
        }
    }
}

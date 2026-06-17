using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Components.CharacterDetail
{
    public class CharacterDetailTopView : MonoBehaviour
    {
        [SerializeField]
        TextMeshProUGUI nameText;
        [SerializeField]
        Button closeBtn;

        public void Bind(string name, Action onClose)
        {
            nameText.text = name;

            closeBtn.onClick.RemoveAllListeners();
            closeBtn.onClick.AddListener(() => onClose?.Invoke());
        }

        void OnDestroy()
        {
            closeBtn.onClick.RemoveAllListeners();
        }
    }
}

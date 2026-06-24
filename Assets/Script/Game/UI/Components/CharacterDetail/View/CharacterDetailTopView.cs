using System;
using System.Collections.Generic;
using Game.Domain.Character;
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
        [SerializeField]
        RectTransform characterButtonRoot;
        [SerializeField]
        Button characterButtonPrefab;

        readonly List<Button> characterButtons = new List<Button>();

        public void Bind(
            string name,
            IReadOnlyList<CharacterModel> characters,
            CharacterModel currentCharacter,
            Action<CharacterModel> onCharacterClick,
            Action onClose)
        {
            nameText.text = name;
            BindCharacterButtons(characters, currentCharacter, onCharacterClick);

            closeBtn.onClick.RemoveAllListeners();
            closeBtn.onClick.AddListener(() => onClose?.Invoke());
        }

        void BindCharacterButtons(
            IReadOnlyList<CharacterModel> characters,
            CharacterModel currentCharacter,
            Action<CharacterModel> onCharacterClick)
        {
            ClearCharacterButtons();

            if (characterButtonRoot == null || characterButtonPrefab == null || characters == null)
            {
                return;
            }

            bool prefabIsTemplateChild = characterButtonPrefab.transform.IsChildOf(characterButtonRoot);
            if (prefabIsTemplateChild)
            {
                characterButtonPrefab.gameObject.SetActive(false);
            }

            ClearCharacterButtonRoot(prefabIsTemplateChild);

            for (int i = 0; i < characters.Count; i++)
            {
                CharacterModel character = characters[i];
                Button button = Instantiate(characterButtonPrefab, characterButtonRoot);
                button.gameObject.SetActive(true);
                button.interactable = character != currentCharacter;

                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = character.Name.Value;
                }

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onCharacterClick?.Invoke(character));
                characterButtons.Add(button);
            }
        }

        void ClearCharacterButtons()
        {
            for (int i = 0; i < characterButtons.Count; i++)
            {
                if (characterButtons[i] != null)
                {
                    Destroy(characterButtons[i].gameObject);
                }
            }

            characterButtons.Clear();
        }

        void ClearCharacterButtonRoot(bool keepPrefabTemplate)
        {
            for (int i = characterButtonRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = characterButtonRoot.GetChild(i);
                if (keepPrefabTemplate && child == characterButtonPrefab.transform)
                {
                    continue;
                }

                Destroy(child.gameObject);
            }
        }

        void OnDestroy()
        {
            ClearCharacterButtons();
            closeBtn.onClick.RemoveAllListeners();
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class CharacterDetailEquipPageView : MonoBehaviour
{
    [SerializeField]
    TextMeshProUGUI nameText;
    [SerializeField]
    TextMeshProUGUI firstStatValueText;
    [SerializeField]
    TextMeshProUGUI secondStatValueText;
    [SerializeField]
    TextMeshProUGUI levelText;
    [SerializeField]
    GameObject[] rareStars;
    [SerializeField]
    Image[] promoteStars;
    [SerializeField]
    TextMeshProUGUI refineLevelText;
    [SerializeField]
    TextMeshProUGUI descText;


    CharacterDetailEquipPageViewModel vm;
    CompositeDisposable disposable;
    
    public void Bind(CharacterDetailEquipPageViewModel viewModel)
    {
        vm = viewModel;
        viewModel.currentWeapon.Subscribe(
            _ =>
            {
                RefreshView();
            }).AddTo(disposable);
    }

    public void RefreshView()
    {
        
    }

}

using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class CharacterDetailViewModel : IDisposable
{
    public CharacterDetailContentViewModel contentViewModel;
    
    CompositeDisposable disposable = new CompositeDisposable();
    public CharacterModel model;
    public CharacterDetailViewModel(CharacterModel model)
    {
        this.model = model;

        contentViewModel = new CharacterDetailContentViewModel(model);
    }
    

    public void Dispose()
    {
        

        disposable.Dispose();
    }
}

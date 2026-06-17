using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Store/Store Database")]
public class StoreDatabase : ScriptableObject
{
    [SerializeField]
    List<StoreItemDefinition> allItems = new();

    public IReadOnlyList<StoreItemDefinition> Items => allItems;
}

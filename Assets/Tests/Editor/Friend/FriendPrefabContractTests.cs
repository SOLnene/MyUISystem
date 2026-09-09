using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public class FriendPrefabContractTests
{
    const string FriendViewPath = "Assets/AssetsPackage/UI/Prefab/Friend/FriendView.prefab";
    const string FriendItemPath = "Assets/AssetsPackage/UI/Prefab/Friend/FriendItem.prefab";
    const string FriendViewAddress = "ui/view/friendview";

    [Serializable]
    class UIConfigEntry
    {
        public string uiType;
        public string assetPath;
        public string uiAddress;
        public string uiLayer;
        public bool isWindow;
    }

    [Serializable]
    class UIConfigWrapper
    {
        public UIConfigEntry[] entries;
    }

    [Test]
    public void FriendViewPrefab_HasRequiredReferences()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FriendViewPath);
        Assert.NotNull(prefab);

        FriendView friendView = prefab.GetComponent<FriendView>();
        Assert.NotNull(friendView);
        AssertObjectReference(friendView, "closeButton");
        AssertObjectReference(friendView, "tabGroup");
        AssertObjectReference(friendView, "titleText");
        AssertObjectReference(friendView, "friendCountText");
        AssertObjectReference(friendView, "addFriendPage");
        AssertObjectReference(friendView, "recentPlayersPage");
        AssertObjectReference(friendView, "friendListView");

        FriendListView listView = prefab.GetComponentInChildren<FriendListView>(true);
        Assert.NotNull(listView);
        AssertObjectReference(listView, "content");

        UnityEngine.Object itemPrefab = GetObjectReference(listView, "itemPrefab");
        Assert.AreEqual(FriendItemPath, AssetDatabase.GetAssetPath(itemPrefab));
    }

    [Test]
    public void FriendItemPrefab_HasRequiredReferences()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FriendItemPath);
        Assert.NotNull(prefab);

        FriendItemView itemView = prefab.GetComponent<FriendItemView>();
        Assert.NotNull(itemView);
        AssertObjectReference(itemView, "avatarIcon");
        AssertObjectReference(itemView, "playerNameText");
        AssertObjectReference(itemView, "levelText");
        AssertObjectReference(itemView, "presenceText");
        AssertObjectReference(itemView, "signatureText");
        AssertObjectReference(itemView, "chatButton");
    }

    [Test]
    public void FriendView_IsRegisteredInUIConfigAndAddressables()
    {
        string configPath = Path.Combine(
            Application.dataPath,
            "AssetsPackage/UI/UIConfig.json");
        string configJson = File.ReadAllText(configPath);
        UIConfigWrapper config = JsonUtility.FromJson<UIConfigWrapper>(
            $"{{\"entries\":{configJson}}}");
        UIConfigEntry friendEntry = Array.Find(
            config.entries,
            entry => entry.uiType == nameof(UIType.FriendView));

        Assert.NotNull(friendEntry);
        Assert.AreEqual(FriendViewPath, friendEntry.assetPath);
        Assert.AreEqual(FriendViewAddress, friendEntry.uiAddress);
        Assert.AreEqual("NormalLayer", friendEntry.uiLayer);
        Assert.IsFalse(friendEntry.isWindow);

        string prefabGuid = AssetDatabase.AssetPathToGUID(FriendViewPath);
        var addressableEntry = AddressableAssetSettingsDefaultObject.Settings
            .FindAssetEntry(prefabGuid);
        Assert.NotNull(addressableEntry);
        Assert.AreEqual(FriendViewAddress, addressableEntry.address);
    }

    static void AssertObjectReference(UnityEngine.Object target, string propertyName)
    {
        Assert.NotNull(GetObjectReference(target, propertyName), propertyName);
    }

    static UnityEngine.Object GetObjectReference(
        UnityEngine.Object target,
        string propertyName)
    {
        var serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        Assert.NotNull(property, propertyName);
        return property.objectReferenceValue;
    }
}

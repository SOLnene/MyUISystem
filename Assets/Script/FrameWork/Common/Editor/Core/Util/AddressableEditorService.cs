using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

/// <summary>
/// 设置addressable路径
/// </summary>
public static class AddressableEditorService 
{
   public static void Register(
      string assetPath,
      string address,
      params string[] labels)
   {
      var settings = AddressableAssetSettingsDefaultObject.Settings;
      if (settings == null)
      {
         Debug.LogWarning("Addressables Settings 不存在，跳过 Addressable 标记");
         return;
      }
      string guid = AssetDatabase.AssetPathToGUID(assetPath);

      var entry = settings.FindAssetEntry(guid) 
         ?? settings.CreateOrMoveEntry(guid, settings.DefaultGroup);

      entry.address = address;
      if (labels != null)
      {
         foreach (var label in labels)
         {
            entry.SetLabel(label, true, true);
         }
      }
   }
}

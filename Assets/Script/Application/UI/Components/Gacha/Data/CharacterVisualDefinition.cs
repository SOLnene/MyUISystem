using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Character Visual Definition")]
//角色到图片映射
public class CharacterVisualDefinition: ScriptableObject
{
    public string characterKey;      // 逻辑ID（唯一）
    public bool hasIcon = true;       // 是否存在头像
    public bool hasDetailImage = true;// 是否存在详情图
}

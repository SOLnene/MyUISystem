using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色突破材料规则入口：
/// 仅负责绑定「角色 key」与一组可复用的阶级突破规则资产。
/// </summary>
[CreateAssetMenu(menuName = "Game/Promote/Character Promote Definition")]
public class CharacterPromoteDefinition : ScriptableObject
{
    /// <summary>
    /// 对应的角色 key（建议与 CharacterDefinition.key 一致）
    /// </summary>
    public string characterKey;

    /// <summary>
    /// 每一阶的突破规则资产引用列表（按 rank 升序配置）
    /// </summary>
    public List<PromoteRankRule> rankRules = new List<PromoteRankRule>();
}


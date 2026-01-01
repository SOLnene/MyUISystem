using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// 游戏的入口
/// </summary>
public class Launcher : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(StartGameFlow());
    }

    IEnumerator StartGameFlow()
    {
        // 入口只负责调用流程管理器
        yield return FlowManager.Instance.StartGameFlow();
    }
}

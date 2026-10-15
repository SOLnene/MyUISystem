using UnityEngine;

internal static class CharacterSwitchParticlePlayback
{
    internal static void Play(ParticleSystem root, CharacterSwitchElementPalette.Colors colors)
    {
        // Unity 不允许对仍在存活的系统改种子，重新采样前必须清空上一轮。
        root.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem system in systems)
        {
            Color tint = system.name == "Glow" ? colors.Glow
                : system.name == "Motes" ? colors.Motes : colors.Core;
            var main = system.main;
            Color minimum = main.startColor.colorMin;
            Color maximum = main.startColor.colorMax;
            // 配色已归一化：重新生成 RGB，避免用上一轮颜色累乘，也保留原 Alpha。
            Color minimumTint = tint * minimum.maxColorComponent;
            Color maximumTint = tint * maximum.maxColorComponent;
            minimumTint.a = minimum.a;
            maximumTint.a = maximum.a;
            main.startColor = new ParticleSystem.MinMaxGradient(minimumTint, maximumTint);
        }
        // Play(false) generates each primary layer's automatic seed before Glow starts.
        foreach (ParticleSystem system in systems)
        {
            if (system.name != "Glow")
            {
                system.Play(false);
            }
        }

        // 先独立采样各层，再让 Glow 复用父亮芯的种子，避免依赖组件遍历顺序。
        foreach (ParticleSystem system in systems)
        {
            if (system.name == "Glow")
            {
                system.randomSeed = system.transform.parent.GetComponent<ParticleSystem>().randomSeed;
                system.Play(false);
            }
        }
    }
}

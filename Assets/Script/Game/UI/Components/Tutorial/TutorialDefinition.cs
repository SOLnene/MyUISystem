using System;
using System.Collections.Generic;
using UnityEngine;

internal enum TutorialCompletionMode
{
    // 用目标出现确认界面导航结果；用业务信号确认真正执行成功的操作。
    TargetRegistered,
    Signal,
}

internal enum TutorialSignalId
{
    None,
    CharacterQuickFillCompleted,
    CharacterEnhanceCompleted,
}

internal readonly struct TutorialSignalEvent : IEvent
{
    public readonly TutorialSignalId SignalId;

    public TutorialSignalEvent(TutorialSignalId signalId)
    {
        SignalId = signalId;
    }
}

[Serializable]
internal sealed class TutorialStepData
{
    // Runner 只消费校验后的强类型数据，不在步骤执行期间反复解析 JSON 字符串。
    readonly string message;
    readonly TutorialTargetId targetId;
    readonly TutorialCompletionMode completionMode;
    readonly TutorialTargetId completionTargetId;
    readonly TutorialSignalId completionSignalId;

    internal TutorialStepData(
        string message,
        TutorialTargetId targetId,
        TutorialCompletionMode completionMode,
        TutorialTargetId completionTargetId,
        TutorialSignalId completionSignalId)
    {
        this.message = message;
        this.targetId = targetId;
        this.completionMode = completionMode;
        this.completionTargetId = completionTargetId;
        this.completionSignalId = completionSignalId;
    }

    internal string Message => message;
    internal TutorialTargetId TargetId => targetId;
    internal TutorialCompletionMode CompletionMode => completionMode;
    internal TutorialTargetId CompletionTargetId => completionTargetId;
    internal TutorialSignalId CompletionSignalId => completionSignalId;
}

internal sealed class TutorialDefinition
{
    readonly string tutorialId;
    readonly TutorialStepData[] steps;

    internal TutorialDefinition(string tutorialId, TutorialStepData[] steps)
    {
        this.tutorialId = tutorialId;
        this.steps = steps;
    }

    internal string TutorialId => tutorialId;
    internal IReadOnlyList<TutorialStepData> Steps =>
        steps ?? Array.Empty<TutorialStepData>();
}

[Serializable]
internal sealed class TutorialDefinitionJson
{
    // JsonUtility 需要可序列化字段，因此 DTO 与只读运行时模型分离。
    public string tutorialId;
    public TutorialStepJson[] steps;
}

[Serializable]
internal sealed class TutorialStepJson
{
    public string message;
    public string targetId;
    public string completionMode;
    public string completionTargetId;
    public string completionSignalId;
}

internal static class TutorialDefinitionLoader
{
    internal static TutorialDefinition Load(TextAsset config)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        TutorialDefinitionJson definition =
            JsonUtility.FromJson<TutorialDefinitionJson>(config.text);
        if (definition == null || string.IsNullOrWhiteSpace(definition.tutorialId))
        {
            throw new InvalidOperationException(
                $"Tutorial config {config.name} has no tutorialId.");
        }

        if (definition.steps == null || definition.steps.Length == 0)
        {
            throw new InvalidOperationException(
                $"Tutorial config {config.name} has no steps.");
        }

        var steps = new TutorialStepData[definition.steps.Length];
        for (int index = 0; index < definition.steps.Length; index++)
        {
            // 配置错误在教程启动时立即失败，避免运行到中途才表现为无响应。
            TutorialStepJson step = definition.steps[index]
                ?? throw new InvalidOperationException(
                    $"Tutorial config {config.name} has an empty step at index {index}.");
            TutorialTargetId targetId = ParseEnum<TutorialTargetId>(
                step.targetId,
                nameof(step.targetId),
                config,
                index);
            TutorialCompletionMode completionMode =
                ParseEnum<TutorialCompletionMode>(
                    step.completionMode,
                    nameof(step.completionMode),
                    config,
                    index);
            TutorialTargetId completionTargetId = ParseOptionalEnum(
                step.completionTargetId,
                TutorialTargetId.None,
                nameof(step.completionTargetId),
                config,
                index);
            TutorialSignalId completionSignalId = ParseOptionalEnum(
                step.completionSignalId,
                TutorialSignalId.None,
                nameof(step.completionSignalId),
                config,
                index);

            if (completionMode == TutorialCompletionMode.TargetRegistered
                && completionTargetId == TutorialTargetId.None)
            {
                throw new InvalidOperationException(
                    $"Tutorial config {config.name} step {index} requires a completionTargetId.");
            }

            if (completionMode == TutorialCompletionMode.Signal
                && completionSignalId == TutorialSignalId.None)
            {
                throw new InvalidOperationException(
                    $"Tutorial config {config.name} step {index} requires a completionSignalId.");
            }

            steps[index] = new TutorialStepData(
                step.message ?? string.Empty,
                targetId,
                completionMode,
                completionTargetId,
                completionSignalId);
        }

        return new TutorialDefinition(definition.tutorialId, steps);
    }

    static T ParseEnum<T>(
        string value,
        string fieldName,
        TextAsset config,
        int stepIndex)
        where T : struct, Enum
    {
        if (Enum.TryParse(value, true, out T parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException(
            $"Tutorial config {config.name} step {stepIndex} has invalid {fieldName}: {value}.");
    }

    static T ParseOptionalEnum<T>(
        string value,
        T defaultValue,
        string fieldName,
        TextAsset config,
        int stepIndex)
        where T : struct, Enum
    {
        return string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : ParseEnum<T>(value, fieldName, config, stepIndex);
    }
}

using System;
using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using Game.Domain.Character;
using UniRx;
using UnityEngine;
/*
 * 【业务层】
GachaService / GachaDomain
    └── 负责随机、保底、掉落规则

【流程层】
GachaViewModel
    ├── 执行抽卡
    ├── 和业务层交互
    ├── 把“原始结果”转成 ViewModel
    ├── 创建 Session
    └── 通知 UI：有一次新抽卡开始了

【展示层】
SingleResultViewModel   ← 单个逐个展示
GachaResultViewModel    ← 汇总展示

 */
public class GachaViewModel : IDisposable
{
    CompositeDisposable disposable = new CompositeDisposable();
    // 抽卡命令，参数为抽卡数量
    public ReactiveCommand<int> drawCommand = new ReactiveCommand<int>();
    // 最近一次抽到的物品列表
    public ReactiveCollection<GachaEntryViewModel> lastDrawnItems = new ReactiveCollection<GachaEntryViewModel>();
    // 是否正在抽卡
    // isDrawing:
    // - 流程互斥（防止并行 Draw）
    // - UI 防重入（按钮灰掉）
    // ⚠ 后续可能拆为 isBusy / canInput
    public ReactiveProperty<bool> isDrawing = new ReactiveProperty<bool>(false);
    GachaSessionViewModel currentSession;
    bool disposed;
    internal Subject<string> DrawFailed { get; } = new Subject<string>();
    
    public IReadOnlyList<GachaPoolUIConfig> PoolConfigs { get; }
    public IReadOnlyReactiveProperty<GachaPoolUIConfig> CurrentPoolConfig => currentPoolConfig;
    readonly ReactiveProperty<GachaPoolUIConfig> currentPoolConfig;
    readonly List<GachaPoolUIConfig> poolConfigs = new List<GachaPoolUIConfig>();
    
    public Subject<GachaSessionViewModel> OnSessionStarted { get; } = new Subject<GachaSessionViewModel>();

    readonly IGachaService gachaService;
    readonly IGachaVisualProvider visualProvider;
    
    public GachaViewModel(
        IGachaService service,
        IGachaVisualProvider provider,
        GachaPoolUIConfigDatabase configDatabase)
    {
        gachaService = service;
        visualProvider = provider;
        poolConfigs.AddRange(configDatabase.Configs);
        PoolConfigs = poolConfigs;
        currentPoolConfig = new ReactiveProperty<GachaPoolUIConfig>(configDatabase.defaultConfig);

        //currentIndex.Subscribe(_ => UpdateHasNext()).AddTo(disposable);
        lastDrawnItems.ObserveCountChanged().Subscribe(_ => UpdateHasNext()).AddTo(disposable);
        
        drawCommand
            .Where(_ => currentPoolConfig.Value != null && !isDrawing.Value &&
                        (currentSession == null || currentSession.Phase.Value == GachaSessionPhase.Finished))
            .Subscribe(
                count =>
                {
                    Draw(count).Forget();
                }).AddTo(disposable);
        currentPoolConfig.Where(config => config != null).Subscribe(
            config =>Debug.Log($"切换卡池到 {config.gachaKey}"))
            .AddTo(disposable);
    }

    async UniTaskVoid Draw(int count)
    {
        isDrawing.Value = true;
        try
        {
            lastDrawnItems.Clear();

            var gachaKey = currentPoolConfig.Value.gachaKey; // 快照
            var result = await GachaDrawOperation.ExecuteAsync(gachaService, count, gachaKey);
            if (disposed) return;
            int resultIndex = 0;

            foreach (var e in result.Entries)
            {
                CharacterModel character = null;
                InventoryItem item = null;

                if (result.ServerConfirmed)
                {
                    if (e.entryType == GachaEntryType.Character)
                        character = GameContext.Instance.CharacterRepository.GetByKey(e.entryKey);
                    else if (e.entryType == GachaEntryType.Equip)
                        GameContext.Instance.InventoryRepository.TryGetItem(result.AwardedInstanceIds[resultIndex], out item);
                }
                else if (e.entryType == GachaEntryType.Character)
                {
                    var definition = GameDatabase.CharacterDatabase.Get(e.entryKey);
                    character = GameContext.Instance.CharacterRepository.GetByKey(e.entryKey);
                    if (character != null)
                    {
                        character.AddTalentToken(1);
                    }
                    else
                    {
                        character = GameContext.Instance.CharacterRepository.Add(definition);
                    }
                }
                else if (e.entryType == GachaEntryType.Equip)
                {
                    var definition = GameDatabase.ItemDatabase.GetItemByKey(e.entryKey) as EquipDefinition;
                    item = new EquipItem(definition);
                    GameContext.Instance.InventoryRepository.AddItem(item);
                }

                var vm = new GachaEntryViewModel(e,visualProvider, character, item);
                lastDrawnItems.Add(vm);
                resultIndex++;
            }

            if (!result.ServerConfirmed)
            {
                AchievementProgressService.Instance.AddProgress(
                    AchievementProgressKeys.GachaDraw,
                    count);
                GameSaveCoordinator.Instance.MarkDirty();
            }

            currentSession?.Dispose();
            currentSession = new GachaSessionViewModel(lastDrawnItems);
            isDrawing.Value = false;
            //开始展示流程
            //TODO：再包一层
            //UIManager.Instance.Open(UIType.GachaResultDetailPopup,sessionVM);
            //ShowNext();
            OnSessionStarted.OnNext(currentSession);
        }
        catch (Exception exception)
        {
            if (!disposed) DrawFailed.OnNext(GachaDrawOperation.Feedback(exception));
            Debug.LogWarning($"Gacha request failed: {exception.Message}");
        }
        finally
        {
            isDrawing.Value = false;
        }
    }

    internal void ApplyPoolConfigs(IReadOnlyList<GachaPoolUIConfig> configs)
    {
        // 先清除旧选择；开放列表为空或请求失败时不能继续抽旧池。
        currentPoolConfig.Value = null;
        poolConfigs.Clear();
        for (int i = 0; i < configs.Count; i++)
            poolConfigs.Add(configs[i]);
        if (poolConfigs.Count > 0)
            currentPoolConfig.Value = poolConfigs[0];
    }

    public void SwitchPool(GachaPoolUIConfig config)
    {
        if (config == null || !poolConfigs.Contains(config) || currentPoolConfig.Value == config)
            return;

        currentPoolConfig.Value = config;
        Debug.Log($"切换卡池：{config.gachaKey}");
    }
    
    public void ShowNext()
    {
        
    }
    
    /*public bool HasNext()
    {
        hasNext.Value = currentIndex.Value >= 0 && currentIndex.Value < lastDrawnItems.Count - 1;
        return currentIndex.Value >= 0 && currentIndex.Value < lastDrawnItems.Count - 1;
    }*/
    
    void UpdateHasNext()
    {
       
    }
    
    
    public void CloseResult()
    {
        
    }
    
    public void Dispose()
    {
        disposed = true;
        currentSession?.Dispose();
        disposable.Dispose();
    }
}

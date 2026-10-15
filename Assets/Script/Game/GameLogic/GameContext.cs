using Cysharp.Threading.Tasks;
using System.Threading;

public class GameContext: Singleton<GameContext>
{
    // 串行化会话切换，避免两个候选同时读取并争抢提交点。
    readonly SemaphoreSlim sessionGate = new(1, 1);
    GameSession currentSession;

    public BackpackViewModel BackpackVM => currentSession?.BackpackVM;

    public InventoryRepository InventoryRepository => currentSession?.InventoryRepository;
    public CharacterRepository CharacterRepository => currentSession?.CharacterRepository;
    public TeamRepository TeamRepository => currentSession?.TeamRepository;
    public StoreDatabase StoreDatabase => currentSession?.StoreDatabase;
    //全项目只有一个实现
    public GachaService GachaService => currentSession?.GachaService;
    internal StorePurchaseService StorePurchaseService => currentSession?.StorePurchaseService;
    internal AchievementService AchievementService => currentSession?.AchievementService;
    internal SaveLoadResult LastSaveLoadResult { get; private set; } = SaveLoadResult.NotFound;
    internal bool CanSave => currentSession != null && currentSession.CanSave;
    internal string ActiveSaveDirectory => currentSession?.SaveDirectory;
    //可能有多个不同的实现
    public IGachaVisualProvider GachaVisualProvider => currentSession?.GachaVisualProvider;

    public async UniTask Init()
    {
        if (currentSession != null)
        {
            return;
        }

        string saveDirectory = SaveProfileManager.Instance.ActiveProfileDirectory;
        if (!string.IsNullOrEmpty(saveDirectory) &&
            await TryStartProfileAsync(saveDirectory) &&
            (LastSaveLoadResult == SaveLoadResult.NotFound || GameSaveSystem.NeedsResave))
        {
            GameSaveCoordinator.Instance.MarkDirty();
        }
    }

    internal async UniTask<bool> TryStartProfileAsync(string saveDirectory, CancellationToken cancellationToken = default)
    {
        await sessionGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (currentSession?.SaveDirectory == saveDirectory)
            {
                return true;
            }

            await GameDatabase.Init();
            cancellationToken.ThrowIfCancellationRequested();
            // 候选初始化期间保留旧会话，失败时可以无损重试其他存档。
            var candidateSession = new GameSession(saveDirectory);
            var previousCurrencies = GameEconomy.Instance.ExportSaveData();
            var previousAchievements = AchievementProgressService.Instance.ExportSaveData();
            var previousTutorial = TutorialProgressService.ExportSaveData();
            try
            {
                await candidateSession.InitializeAsync();
                // 候选还会初始化全局进度；取消时恢复它们，不能只丢弃仓库。
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch
            {
                candidateSession.Dispose();
                if (cancellationToken.IsCancellationRequested)
                {
                    GameEconomy.Instance.ImportSaveData(previousCurrencies);
                    AchievementProgressService.Instance.ImportSaveData(previousAchievements);
                    TutorialProgressService.ImportSaveData(previousTutorial);
                }
                throw;
            }

            LastSaveLoadResult = candidateSession.LoadResult;
            if (!candidateSession.CanSave)
            {
                candidateSession.Dispose();
                return false;
            }

            GameSession previousSession = currentSession;
            // 这是会话的唯一提交点；从这里开始外部属性才会转发到新状态。
            currentSession = candidateSession;
            previousSession?.Dispose();
            return true;
        }
        finally
        {
            sessionGate.Release();
        }
    }

    internal bool TryRequestInitialTestItems()
    {
        return currentSession != null && currentSession.TryRequestInitialTestItems();
    }
}

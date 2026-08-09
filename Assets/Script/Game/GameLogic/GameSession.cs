using System;
using Cysharp.Threading.Tasks;

// 一份存档对应一套运行时仓库和服务；未提交的候选会话不会暴露给其他系统。
internal sealed class GameSession : IDisposable
{
    internal string SaveDirectory { get; }
    internal BackpackViewModel BackpackVM { get; private set; }
    internal InventoryRepository InventoryRepository { get; }
    internal CharacterRepository CharacterRepository { get; }
    internal TeamRepository TeamRepository { get; }
    internal StoreDatabase StoreDatabase { get; }
    internal GachaService GachaService { get; }
    internal StorePurchaseService StorePurchaseService { get; }
    internal AchievementService AchievementService { get; private set; }
    internal IGachaVisualProvider GachaVisualProvider { get; }
    internal SaveLoadResult LoadResult { get; private set; } = SaveLoadResult.NotFound;
    internal bool CanSave => LoadResult == SaveLoadResult.Success
                             || LoadResult == SaveLoadResult.NotFound
                             || LoadResult == SaveLoadResult.RecoveredFromBackup;

    bool initialTestItemsRequested;

    internal GameSession(string saveDirectory)
    {
        SaveDirectory = saveDirectory;
        InventoryRepository = new InventoryRepository();
        StoreDatabase = GameDatabase.StoreDatabase;
        CharacterRepository = new CharacterRepository();
        TeamRepository = new TeamRepository();
        GachaService = new GachaService(
            new GachaPoolProvider(GameDatabase.GachaPoolDatabase));
        StorePurchaseService = new StorePurchaseService(
            new StorePurchaseRepository());
        GachaVisualProvider = new GachaVisualProvider(
            GameDatabase.CharaVisualDatabase);
    }

    internal async UniTask InitializeAsync()
    {
        // 先把存档应用到候选仓库，只有完整初始化成功后 GameContext 才会提交本会话。
        LoadResult = GameSaveSystem.LoadFromDirectory(
            SaveDirectory,
            GameEconomy.Instance,
            InventoryRepository,
            CharacterRepository,
            TeamRepository,
            GachaService,
            StorePurchaseService);
        if (!CanSave)
        {
            return;
        }

        if (LoadResult == SaveLoadResult.NotFound)
        {
            GameEconomy.Instance.ImportSaveData(null);
            AchievementProgressService.Instance.ImportSaveData(null);
            TutorialProgressService.ImportSaveData(null);
        }

        AchievementService = new AchievementService(GameDatabase.ItemDatabase);
        await AchievementService.InitializeAsync();
        BackpackVM = new BackpackViewModel(InventoryRepository);
    }

    internal bool TryRequestInitialTestItems()
    {
        if (LoadResult != SaveLoadResult.NotFound || initialTestItemsRequested)
        {
            return false;
        }

        initialTestItemsRequested = true;
        return true;
    }

    public void Dispose()
    {
        // 候选失败或切换会话时，释放其创建的订阅和异步资源。
        BackpackVM?.Dispose();
        AchievementService?.Dispose();
    }
}

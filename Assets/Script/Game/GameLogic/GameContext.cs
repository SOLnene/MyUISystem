using Cysharp.Threading.Tasks;
public class GameContext: Singleton<GameContext>
{
    AsyncLazy initializeTask;
    bool initialTestItemsRequested;

    public BackpackViewModel BackpackVM { get; private set; }

    public InventoryRepository InventoryRepository { get; private set; }
    public CharacterRepository CharacterRepository { get; private set; }
    public StoreDatabase StoreDatabase { get; private set; }
    //全项目只有一个实现
    public GachaService GachaService { get; private set; }
    internal StorePurchaseService StorePurchaseService { get; private set; }
    internal AchievementService AchievementService { get; private set; }
    internal SaveLoadResult LastSaveLoadResult { get; private set; } = SaveLoadResult.NotFound;
    internal bool CanSave => LastSaveLoadResult == SaveLoadResult.Success
                             || LastSaveLoadResult == SaveLoadResult.NotFound
                             || LastSaveLoadResult == SaveLoadResult.RecoveredFromBackup;
    //可能有多个不同的实现
    public IGachaVisualProvider GachaVisualProvider { get; private set; }
    public UniTask Init()
    {
        initializeTask ??= UniTask.Lazy(Initialize);
        return initializeTask.Task;
    }

    async UniTask Initialize()
    {
        await GameDatabase.Init();
        //backpackVM = new BackpackViewModel();
        //todo:改为使用 Installer + DI 容器注入
        InventoryRepository = new InventoryRepository();
        StoreDatabase = GameDatabase.StoreDatabase;
        CharacterRepository = new CharacterRepository();
        GachaPoolProvider poolProvider = new GachaPoolProvider(GameDatabase.GachaPoolDatabase);
        GachaService = new GachaService(poolProvider);
        StorePurchaseService = new StorePurchaseService(new StorePurchaseRepository());
        GachaVisualProvider = new GachaVisualProvider(GameDatabase.CharaVisualDatabase);

        LastSaveLoadResult = GameSaveSystem.LoadCurrentGame();
        AchievementService = new AchievementService(GameDatabase.ItemDatabase);
        await AchievementService.InitializeAsync();
        BackpackVM = new BackpackViewModel(InventoryRepository);
        if (CanSave && (LastSaveLoadResult == SaveLoadResult.NotFound || GameSaveSystem.NeedsResave))
        {
            GameSaveCoordinator.Instance.MarkDirty();
        }
    }

    internal bool TryRequestInitialTestItems()
    {
        if (LastSaveLoadResult != SaveLoadResult.NotFound || initialTestItemsRequested)
        {
            return false;
        }

        initialTestItemsRequested = true;
        return true;
    }
}

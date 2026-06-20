using Cysharp.Threading.Tasks;
public class GameContext: Singleton<GameContext>
{
    public BackpackViewModel BackpackVM { get; private set; }

    public InventoryRepository InventoryRepository { get; private set; }
    public CharacterRepository CharacterRepository { get; private set; }
    public StoreDatabase StoreDatabase { get; private set; }
    //全项目只有一个实现
    public GachaService GachaService { get; private set; }
    //可能有多个不同的实现
    public IGachaVisualProvider GachaVisualProvider { get; private set; }
    public async UniTask Init()
    {
        await GameDatabase.Init();
        //backpackVM = new BackpackViewModel();
        //todo:改为使用 Installer + DI 容器注入
        InventoryRepository = new InventoryRepository();
        StoreDatabase = GameDatabase.StoreDatabase;
        CharacterRepository = new CharacterRepository();
        GameSaveSystem.TryLoadCurrentGame();
        BackpackVM = new BackpackViewModel(InventoryRepository);

        LocalGachaSchedule gachaSchedule = new LocalGachaSchedule();
        GachaPoolProvider poolProvider = new GachaPoolProvider(GameDatabase.GachaPoolDatabase, gachaSchedule);
        GachaService = new GachaService(poolProvider);
        GachaVisualProvider = new GachaVisualProvider(GameDatabase.CharaVisualDatabase);
    }
}

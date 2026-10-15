using Cysharp.Threading.Tasks;
using UnityEngine;

// 仅供 UICreate 场景使用，集中准备该场景运行 UI 所需的游戏状态。
public sealed class UICreateBootstrap : MonoBehaviour
{
    [SerializeField]
    GameObject designCanvas;

    async void Start()
    {
        designCanvas.SetActive(false);

        ResourceManager.Instance.Init();
        await ResourceManager.Instance.InitAsync();
        await UIManager.Instance.InitUIConfig();
        await GameDatabase.Init();

        bool opensHubDirectly = SaveProfileManager.Instance.ActiveProfile != null;
        if (SaveProfileManager.Instance.ActiveProfile == null)
        {
            UIManager.Instance.Open(UIType.LoginView);
            if (await UniTask.WaitUntil(
                () => GameContext.Instance.CanSave &&
                    (!CloudGameSaveSession.IsCloudSource(GameContext.Instance.ActiveSaveDirectory) ||
                        (CloudGameSaveSession.Current?.IsReady ?? false)),
                cancellationToken: this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow())
                return;
        }
        else
        {
            await GameContext.Instance.Init();
        }

        // 新账号仅发放一次初始测试物品，读取已有云存档时不重复赠送。
        if (GameContext.Instance.TryRequestInitialTestItems())
            foreach (InventoryItem item in ItemFactory.CreateTestItems())
            {
                GameContext.Instance.BackpackVM.AddItem(item);
            }

        if (opensHubDirectly)
        {
            UIManager.Instance.Open(UIType.HubRoot);
        }
    }
}

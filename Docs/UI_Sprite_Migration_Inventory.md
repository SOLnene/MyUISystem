# UI Sprite 整理与迁移清单

> 盘点日期：2026-08-24  
> 当前阶段：只读盘点，尚未移动资源、修改 Addressable 地址或调整 Group。  
> 迁移前必须重新执行一次数量、重名和 GUID 检查，避免文档过期。

## 1. 整理目标

将项目内第一方 UI Sprite 统一到一个根目录，结束以下两套目录并存的状态：

```text
Assets/AssetsPackage/UI/Sprite
Assets/Art/Sprite
```

建议最终统一为：

```text
Assets/Art/UI/Sprites
├─ Common
│  ├─ Background
│  ├─ Button
│  ├─ Decoration
│  ├─ Frame
│  ├─ Icon
│  ├─ ItemSlot
│  └─ TouchIcon
├─ Login
├─ Hub
├─ MainMenu
├─ Character
│  ├─ Icon
│  ├─ Portrait
│  ├─ Talent
│  └─ Detail
├─ Backpack
├─ Equipment
│  └─ Icon
├─ Item
│  ├─ Currency
│  ├─ ExpBook
│  ├─ Material
│  └─ Other
├─ Gacha
│  ├─ Background
│  ├─ Banner
│  ├─ EquipmentIcon
│  └─ Tab
├─ Store
├─ Achievement
├─ Reward
├─ Tutorial
└─ VFX
```

目录按“实际功能归属”组织。被多个功能复用的资源进入 `Common`，不能仅根据当前文件夹名分类。

## 2. 当前资源总量

本次盘点范围共 531 张 UI 图片：

| 当前根目录 | 数量 | Addressable 条目 | 被 prefab/scene/asset 序列化引用 | 未发现 Addressable 且未发现序列化引用 |
|---|---:|---:|---:|---:|
| `Assets/AssetsPackage/UI/Sprite` | 255 | 221 | 26 | 20 |
| `Assets/Art/Sprite` | 276 | 17 | 84 | 182 |
| 合计 | 531 | 238 | 110 | 202 |

注意：最后一列只能作为“待确认候选”，不能直接判定资源未使用。代码动态加载、JSON 地址引用、尚未接入的预留资源不会出现在 Unity YAML 序列化引用扫描中。

两个根目录之间目前没有发现同名图片，因此不存在直接的文件覆盖冲突。

## 3. AssetsPackage Sprite 明细

### 3.1 数量

```text
Assets/AssetsPackage/UI/Sprite                     8
Assets/AssetsPackage/UI/Sprite/Backpack           18
Assets/AssetsPackage/UI/Sprite/Gacha              177
├─ Gacha 根目录                                    6
├─ Chara/AvatarIcon                               71
├─ Chara/AvatarImg                                67
├─ Equip                                          30
└─ Tab                                             3
Assets/AssetsPackage/UI/Sprite/Item               52
├─ Item 根目录                                     3
├─ Currency                                        7
├─ Equip                                           20
├─ ExpBook                                          3
└─ Material                                        19
```

### 3.2 可整批迁移目录

以下目录语义明确，可以保留文件名和 GUID 整批迁移：

| 当前目录 | 建议目标目录 | 数量 | 说明 |
|---|---|---:|---|
| `Gacha/Chara/AvatarIcon` | `Character/Icon` | 71 | 已被人物槽位、Hub 和抽卡共同使用，不应继续归为 Gacha 专用资源 |
| `Gacha/Chara/AvatarImg` | `Character/Portrait` | 67 | 角色立绘属于角色视觉资源 |
| `Gacha/Equip` | `Gacha/EquipmentIcon` | 30 | 文件名为 Gacha 专用武器展示图，先保留在 Gacha 功能内 |
| `Gacha/Tab` | `Gacha/Tab` | 3 | 抽卡页签专用 |
| `Item/Currency` | `Item/Currency` | 7 | 货币图标 |
| `Item/ExpBook` | `Item/ExpBook` | 3 | 经验书图标 |
| `Item/Material` | `Item/Material` | 19 | 材料图标 |
| `Item/Equip` | `Equipment/Icon` | 20 | 实际语义是装备图标，不应继续放在 Item/Equip |

以上合计 220 张，约占 AssetsPackage Sprite 的 86%，适合先通过 `AssetDatabase.MoveAsset` 批量迁移。

### 3.3 Gacha 根目录 6 张

```text
UI_Gacha_A017_Up2.png
UI_Gacha_A022_Up1.png
UI_Gacha_A100_Up2.png
UI_Gacha_A134_Up2.png
UI_Tab_GachaShowPanel_A033.png
UI_Tab_GachaShowPanel_A039.png
```

初步建议迁入 `Gacha/Banner`。正式迁移前需要先解决 `gachapoolimagecharacter` 重复地址，确认每张 Banner 对应的 Pool ID。

### 3.4 Sprite 根目录 8 张

```text
UI_AranaraBook_Story_Pic_Line.png
UI_Img_SlotBgBack.png
UI_Img_SlotBgSky1.png
UI_Img_SlotFrame.png
UI_Img_SlotFrame_FullRect.png
UI_Img_SlotMask_FullRect.png
UI_Item_Bg.png
UI_RobotGacha_FacePage_Line.png
```

初步分类：

| 文件 | 建议目标 |
|---|---|
| `UI_Item_Bg.png` | `Common/ItemSlot` |
| `UI_Img_SlotBgBack.png` | `Common/ItemSlot` |
| `UI_Img_SlotBgSky1.png` | `Common/ItemSlot` |
| `UI_Img_SlotFrame.png` | `Common/ItemSlot` |
| `UI_Img_SlotFrame_FullRect.png` | `Common/ItemSlot` |
| `UI_Img_SlotMask_FullRect.png` | `Common/ItemSlot` |
| `UI_AranaraBook_Story_Pic_Line.png` | `Common/Decoration`，迁移前确认实际引用 |
| `UI_RobotGacha_FacePage_Line.png` | `Gacha/Decoration`，迁移前确认实际引用 |

`UI_Item_Bg.png` 当前至少被 7 个 prefab 使用，明确属于通用资源。

### 3.5 Backpack 目录不能整包归为 Backpack

`Backpack` 目录有 18 张图片，其中 13 张未发现 Addressable 或序列化引用，但不能直接删除。

已确认的跨功能资源：

- `UI_Img_BgColor_Item.png` 至少被 ItemSlot、商店、购买弹窗、角色天赋和信息面板等 7 个 prefab 使用，应迁入 `Common/ItemSlot`。
- ItemSlot 背景、描边和文字底板需要按实际 prefab 引用迁入 `Common/ItemSlot`。
- 只服务背包分类或背包界面的图片才保留在 `Backpack`。

因此该目录需要生成逐文件引用表后再迁移，不能直接整体拖入目标 `Backpack`。

## 4. 现有 Art/Sprite 明细

```text
Assets/Art/Sprite/Background                  27
Assets/Art/Sprite/Background/Frame            65
Assets/Art/Sprite/Background/Gacha            10
Assets/Art/Sprite/Background/MainMenu          1
Assets/Art/Sprite/Background/Sprite            1
Assets/Art/Sprite/CharaDetail                 16
Assets/Art/Sprite/Icon                        16
Assets/Art/Sprite/Icon/BackpackCategory        8
Assets/Art/Sprite/Icon/Character               4
Assets/Art/Sprite/Icon/MainMenu                 9
Assets/Art/Sprite/Icon/Talent                   7
Assets/Art/Sprite/Icon/TouchIcon               64
Assets/Art/Sprite/Other                       28
Assets/Art/Sprite/Particle                     4
Assets/Art/Sprite/StoreItem                   16
```

### 4.1 初步目标映射

| 当前目录 | 建议目标目录 | 判断状态 |
|---|---|---|
| `Background/Frame` | `Common/Frame` | 大部分可直接迁移，仍需识别功能专用大背景 |
| `Background/Gacha` | `Gacha/Background` | 可整批迁移 |
| `Background/MainMenu` | `MainMenu/Background` | 可整批迁移 |
| `CharaDetail` | `Character/Detail` | 可整批迁移 |
| `Icon/BackpackCategory` | `Backpack/Icon` | 可整批迁移 |
| `Icon/Character` | `Character/Icon` | 可与历史角色图标统一 |
| `Icon/MainMenu` | `MainMenu/Icon` | 可整批迁移 |
| `Icon/Talent` | `Character/Talent` | 可整批迁移 |
| `Icon/TouchIcon` | `Common/TouchIcon` | 可整批迁移 |
| `StoreItem` | `Store/Item` | 可整批迁移 |
| `Particle` | `VFX` | 先确认这些 PNG 是否只作为粒子贴图使用 |
| `Background` 根目录 | 按 Common/功能重新分类 | 需要逐文件判断 |
| `Icon` 根目录 | 按 Common/功能重新分类 | 需要逐文件判断 |
| `Other` | 禁止整体迁移 | 必须逐文件判断，整理完成后不应保留 `Other` |

## 5. 已确认的通用资源候选

以下资源被多个不同 prefab 使用，应优先归入 `Common`，不能按当前文件夹名归入单一功能：

| 当前资源 | 已发现序列化引用数 | 建议目标 |
|---|---:|---|
| `AssetsPackage/UI/Sprite/UI_Item_Bg.png` | 7 | `Common/ItemSlot` |
| `AssetsPackage/UI/Sprite/Backpack/UI_Img_BgColor_Item.png` | 7 | `Common/ItemSlot` |
| `Art/Sprite/Icon/UI_Icon_Reputation_Star.png` | 6 | `Common/Icon` 或 `Character/Icon`，需确认语义 |
| `Art/Sprite/Background/Frame/UI_BtnFrame_W52.png` | 5 | `Common/Button` |
| `Art/Sprite/Icon/UI_IconStar.png` | 4 | `Common/Icon` |
| `Art/Sprite/Background/UI_Common_Btn.png` | 4 | `Common/Button` |

## 6. Addressable 现状与异常

### 6.1 当前覆盖

- AssetsPackage Sprite 中 221/255 张已有 Addressable Entry。
- Art/Sprite 中只有 17/276 张已有 Addressable Entry。
- 移动物理文件时保持 Address、Group 和 Label 不变，仅改变 Asset Path。

### 6.2 确定异常

1. `gachapoolimagecharacter` 同时指向两张不同图片，必须在改 Banner 地址前确认各自 Pool ID。
2. 以下三个资源已经位于 `Assets/Art/Sprite`，但 Addressable key 仍是旧物理路径：

   ```text
   Assets/Sprite/Backpack/UI_IconStar.png
   Assets/Sprite/Backpack/UI_IconStarShadow.png
   Assets/AssetsPackage/UI/Sprite/TouchIcon/UI_TouchIcon_Plus.png
   ```

3. 迁移 Sprite 时不处理这些 key；等物理目录稳定后，再统一替换为语义地址。
4. `GachaResultRevealView` 的残留 Addressable 条目和 `UIStartView` 地址不一致不属于 Sprite 迁移，但应在后续 Addressable 正确性批次处理。

## 7. 需要同步更新的编辑器扫描路径

Sprite 迁移完成后需要修改以下默认目录：

```text
Assets/Script/Game/Editor/Windows/CharacterVisualGeneratorFromSpriteWindow.cs
Assets/Script/Game/Editor/Windows/CharacterGeneratorFromSpriteWindow.cs
Assets/Script/Game/Editor/Windows/ItemDefinitionGeneratorFromSpriteWindow.cs
Assets/Script/FrameWork/Common/Editor/AddressBatchRenamer.cs
```

建议新路径：

```text
角色图片：Assets/Art/UI/Sprites/Character
装备图片：Assets/Art/UI/Sprites/Equipment/Icon
物品图片：Assets/Art/UI/Sprites/Item
```

运行时代码中仍存在旧的 Plus 图标 Addressable key。这属于地址迁移问题，不应在纯物理迁移批次中顺手修改。

## 8. 推荐迁移批次

### 批次 A：建立目录并迁移明确的角色资源

- 创建 `Assets/Art/UI/Sprites` 目标结构。
- 迁移 71 张角色 Icon。
- 迁移 67 张角色 Portrait。
- 合并当前 `Art/Sprite/Icon/Character` 的 4 张新角色图标。
- 不修改 Addressable key。

### 批次 B：迁移 Gacha、Item 和 Equipment 明确目录

- 迁移 Gacha EquipmentIcon、Tab 和 Background。
- 确认 Banner Pool ID 后迁移 6 张 Banner。
- 迁移 Currency、ExpBook、Material 和 Equipment Icon。
- 同步修改相关编辑器生成器默认目录。

### 批次 C：迁移现有 Art/Sprite 明确目录

- Character Detail。
- Backpack Category Icon。
- MainMenu Icon/Background。
- Talent Icon。
- TouchIcon。
- Store Item。

### 批次 D：处理 Common 与歧义资源

- 分析 `Backpack`、`Background` 根目录、`Icon` 根目录和 `Other`。
- 多功能复用资源迁入 `Common`。
- 无引用资源进入待确认清单，不删除。
- 完成后不保留 `Other`、旧 `Art/Sprite` 和旧 `AssetsPackage/UI/Sprite`。

## 9. 每个批次的强制规则

1. 使用 Unity Project 窗口或 `AssetDatabase.MoveAsset`，禁止直接覆盖目标文件。
2. 移动前检查目标路径和文件名冲突；发现冲突立即停止。
3. 保留原 `.meta` 和 GUID。
4. 同一个批次不修改文件名、Addressable 地址、Group 或 Label。
5. 不删除“未发现引用”的图片。
6. 批次完成后检查 Addressable Entry 数量没有减少。
7. 随机抽查迁移资源：GUID、Address、Group 与迁移前一致。
8. 打开相关 prefab 检查 Missing Sprite。
9. 运行对应功能场景。
10. 每个批次独立提交，便于回滚。

## 10. 后续验证清单

- [ ] 两个旧根目录的图片总数与迁移后目标目录总数一致。
- [ ] 531 个原始图片 GUID 均仍能解析到有效资源。
- [ ] Addressable Entry 数量未因移动减少。
- [ ] Addressable 地址在物理迁移阶段保持不变。
- [ ] 所有 prefab/scene/asset 中的 Sprite GUID 引用仍有效。
- [ ] Backpack、Character、Gacha、Store、Achievement、Reward 主要界面无 Missing Sprite。
- [ ] 角色和物品生成器能扫描新目录。
- [ ] 旧目录只剩空文件夹和 `.meta` 后再移除。
- [ ] 物理目录迁移提交完成后，才开始 Addressable 地址规范化。

## 11. 暂不执行的工作

- 不处理模型 Texture。
- 不处理第三方插件资源。
- 不修改 Sprite Import Settings。
- 不重建 Sprite Atlas。
- 不调整 Addressable Group 或 Remote/Local 设置。
- 不重命名图片。
- 不删除无引用候选。
- 不移动 UI prefab 或 UIConfig.json。


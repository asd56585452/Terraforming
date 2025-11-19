# Inventory UI 設置指南

## 概述
InventoryUI 系統提供了一個位於右下角的3格快速inventory顯示，使用反射機制避免編譯依賴問題。

## 簡單設置步驟

### 1. 創建UI結構
在您的Canvas中創建以下結構：

```
Canvas
└── InventoryUI (添加 InventoryUI.cs 腳本)
    ├── Slot1
    │   ├── Background (Image組件)
    │   ├── ItemIcon (Image組件)  
    │   └── Number (Text組件)
    ├── Slot2
    │   ├── Background (Image組件)
    │   ├── ItemIcon (Image組件)
    │   └── Number (Text組件)
    └── Slot3
        ├── Background (Image組件)
        ├── ItemIcon (Image組件)
        └── Number (Text組件)
```

### 2. 設置位置（右下角）
- 選擇InventoryUI GameObject
- 在RectTransform中設置Anchor為右下角
- 調整位置，例如：
  - Pos X: -120
  - Pos Y: 80

### 3. 配置InventoryUI腳本
在InventoryUI組件中設置以下陣列：

**Slot Images (陣列大小: 3)**
- 拖入 Slot1/ItemIcon, Slot2/ItemIcon, Slot3/ItemIcon

**Slot Backgrounds (陣列大小: 3)**  
- 拖入 Slot1/Background, Slot2/Background, Slot3/Background

**Slot Numbers (陣列大小: 3)**
- 拖入 Slot1/Number, Slot2/Number, Slot3/Number

### 4. 顏色設置
在InventoryUI組件中可以自定義各種工具類型的顏色：

- **Empty Color**: 空槽位顏色 (預設: 灰色)
- **Oxygen Tank Color**: 氧氣瓶顏色 (預設: 青色)
- **Wrench Color**: 扳手顏色 (預設: 灰色)
- **Flashlight Color**: 手電筒顏色 (預設: 黃色)
- **Repair Kit Color**: 修理包顏色 (預設: 綠色) 
- **Rope Color**: 繩子顏色 (預設: 棕色)
- **Other Color**: 其他工具顏色 (預設: 白色)

## 功能特色

### ? 自動顯示
- 系統會自動偵測inventory變化
- 即時更新UI顯示
- 使用顏色代碼區分不同工具類型

### ? 視覺反饋
- 空槽位：灰色顯示，數字為白色
- 有物品槽位：對應工具顏色，數字為黃色
- 按下1, 2, 3鍵時會有脈衝動畫效果

### ? 無編譯依賴
- 使用反射機制獲取inventory資料
- 避免循環依賴問題
- 即使其他腳本有問題也能正常運行

## 工具顏色對照表

| 工具類型 | 顏色 | 說明 |
|---------|------|------|
| OxygenTank | 青色 (Cyan) | 氧氣瓶 |
| Wrench | 灰色 (Gray) | 扳手 |
| Flashlight | 黃色 (Yellow) | 手電筒 |
| RepairKit | 綠色 (Green) | 修理包 |
| Rope | 棕色 (Brown) | 繩子 |
| Other | 白色 (White) | 其他工具 |

## 測試方法

1. 在場景中放置一些CollectibleTool物件
2. 確保這些物件有正確的ToolItem設置
3. 執行遊戲
4. 撿起物品並測試inventory功能：
   - 按E撿起物品到手上
   - 按1, 2, 3將物品放入inventory
   - 再次按1, 2, 3將物品取回手上

## 故障排除

### UI不顯示
- 檢查Canvas是否存在
- 確認InventoryUI腳本已正確附加
- 檢查Player GameObject是否有"Player"標籤

### 顏色不正確
- 確認ToolItem的toolType欄位設置正確
- 檢查InventoryUI中的顏色設置

### 動畫不工作
- 確保Slot Backgrounds陣列已正確設置
- 檢查GameObject是否處於active狀態

### 找不到玩家
- 確保玩家GameObject有"Player"標籤
- 確認PlayerSwimmingController組件已附加

## 擴展建議

### 添加圖示支持
如果您想要顯示實際的圖示而不是顏色：
1. 在ToolItem中設置icon欄位
2. 修改InventoryUI.UpdateSlot()方法
3. 優先顯示icon，沒有icon時才顯示顏色

### 添加數量顯示
對於可堆疊的物品：
1. 在UI中添加數量Text組件
2. 修改InventoryUI以支持數量顯示

### 添加工具提示
滑鼠懸停顯示物品詳細資訊：
1. 添加EventTrigger組件
2. 實現OnPointerEnter/Exit事件
3. 顯示包含物品名稱和描述的提示框

這個簡化的系統提供了基本但完整的inventory UI功能，可以根據需要進一步擴展！
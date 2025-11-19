# 如何連接您的現有UI到Inventory系統

## 步驟 1: 找到您的UI組件

根據截圖，您已經有3個inventory box在右下角。請找到這些UI元素：

1. **找到右下角的3個方框**
2. **確認它們是Image組件** 
3. **記下它們的GameObject名稱**

## 步驟 2: 設置InventoryUI腳本

### 2.1 找到合適的父物件
在您的UI hierarchy中找到包含這3個inventory box的父物件，並在其上添加 `InventoryUI.cs` 腳本。

### 2.2 配置腳本參數

在InventoryUI組件中設置以下陣列：

#### **Slot Images (陣列大小: 3)**
- 拖入您的3個inventory box的Image組件
- 順序: 左到右為 索引0, 1, 2

#### **Slot Backgrounds (陣列大小: 3)** 
- 拖入包含這些Image的GameObject
- 用於動畫效果

#### **Slot Numbers (陣列大小: 3)**
- 如果您有數字顯示，拖入Text組件
- 如果沒有，保持為空

## 步驟 3: 確保Player設置正確

1. **檢查Player標籤**: 確保您的Player GameObject有 "Player" 標籤
2. **檢查腳本**: 確保Player上有 `PlayerSwimmingController` 腳本

## 步驟 4: 創建ToolItem資產並設置Icon

### 4.1 創建ToolItem
```
右鍵點擊 Project → Create → Ocean Game → Tool Item
```

### 4.2 設置Icon
您有兩種選擇：

**選項A: 使用Sprite (推薦)**
- 將您的圖片設置為Sprite (2D and UI)
- 在ToolItem的 `icon` 欄位拖入Sprite

**選項B: 使用Texture2D**
- 在ToolItem的 `iconTexture` 欄位拖入Texture2D
- 系統會自動轉換為Sprite

### 4.3 設置工具類型
在ToolItem中設置正確的 `toolType`：
- OxygenTank (氧氣瓶)
- Wrench (扳手)
- Flashlight (手電筒)
- RepairKit (修理包)
- Rope (繩子)
- Other (其他)

## 步驟 5: 測試設置

1. **執行遊戲**
2. **放置一些CollectibleTool在場景中**
3. **確保CollectibleTool有正確的ToolItem設置**
4. **測試以下功能**:
   - 撿起物品 (按E)
   - 放入inventory (按1, 2, 3)
   - 取出物品 (再次按1, 2, 3)
   - 檢查UI是否顯示正確的icon

## 快速問題解決

### Icon不顯示
- 檢查ToolItem是否有設置icon或iconTexture
- 確認圖片導入設置正確 (Sprite 2D and UI)
- 檢查InventoryUI的Slot Images陣列是否正確設置

### UI位置不對
- 調整InventoryUI父物件的RectTransform
- 確認Anchor設置為右下角

### 顏色顯示而非Icon
- 這是正常的fallback行為
- 設置正確的icon就會顯示圖片

## 範例ToolItem設置

```
Tool Name: "Oxygen Tank"
Description: "Provides 50 oxygen when used"
Icon: [您的氧氣瓶Sprite]
Tool Type: OxygenTank
Max Durability: 1 (一次性使用)
Oxygen Amount: 50
```

## UI預期效果

設置完成後，您的右下角3個box應該會：
- **空槽位**: 顯示為透明或灰色
- **有物品**: 顯示對應的icon圖片
- **使用時**: 有脈衝動畫效果
- **數字顯示**: 有物品時數字變黃色

如果有任何設置問題，請檢查Console是否有錯誤訊息！
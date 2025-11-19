# Inspector設置示範

## InventoryUI 組件設置

當您將InventoryUI腳本附加到GameObject後，Inspector中應該顯示以下欄位：

### ?? **UI Slots**
```
┌─ UI Slots ────────────────────────────┐
│  Slot Images         Size: 3          │
│  ├─ Element 0    [拖入第1個box的Image]  │
│  ├─ Element 1    [拖入第2個box的Image]  │
│  └─ Element 2    [拖入第3個box的Image]  │
│                                       │
│  Slot Backgrounds    Size: 3          │
│  ├─ Element 0    [拖入第1個box的GameObject] │
│  ├─ Element 1    [拖入第2個box的GameObject] │
│  └─ Element 2    [拖入第3個box的GameObject] │
│                                       │
│  Slot Numbers        Size: 3          │
│  ├─ Element 0    [可選: 數字Text組件]   │
│  ├─ Element 1    [可選: 數字Text組件]   │
│  └─ Element 2    [可選: 數字Text組件]   │
└───────────────────────────────────────┘
```

### ?? **Colors**
```
┌─ Colors ──────────────────────────────┐
│  Empty Color         [灰色 - 空槽位]    │
│  Oxygen Tank Color   [青色 - 氧氣瓶]    │
│  Wrench Color        [灰色 - 扳手]      │
│  Flashlight Color    [黃色 - 手電筒]    │
│  Repair Kit Color    [綠色 - 修理包]    │
│  Rope Color          [棕色 - 繩子]      │
│  Other Color         [白色 - 其他]      │
└───────────────────────────────────────┘
```

## ToolItem 組件設置

創建ToolItem資產後，Inspector顯示：

### ?? **Tool Information**
```
┌─ Tool Information ────────────────────┐
│  Tool Name          "Oxygen Tank"     │
│  Description        "提供氧氣補充"      │
│  Icon              [拖入Sprite]        │
│  Icon Texture      [或拖入Texture2D]   │
└───────────────────────────────────────┘
```

### ?? **Tool Properties**
```
┌─ Tool Properties ─────────────────────┐
│  Tool Type         OxygenTank ▼       │
│  Max Durability    1                  │
│  Is Repairable     ?                  │
└───────────────────────────────────────┘
```

### ?? **Usage**
```
┌─ Usage ───────────────────────────────┐
│  Can Use Underwater  ?                │
│  Use Cooldown       1                 │
└───────────────────────────────────────┘
```

### ?? **Oxygen Tank Settings**
```
┌─ Oxygen Tank Settings ────────────────┐
│  Oxygen Amount      50                │
└───────────────────────────────────────┘
```

## 圖片設置提示

### ?? **導入設置**
當您將圖片拖入Project時，請確認設置：

1. **選擇圖片** → **Inspector**
2. **Texture Type** → `Sprite (2D and UI)`
3. **Sprite Mode** → `Single`
4. **Pixels Per Unit** → `100` (預設)
5. **Filter Mode** → `Bilinear`
6. **點擊Apply**

### ??? **建議圖片規格**
- **尺寸**: 64x64 或 128x128 像素
- **格式**: PNG (支援透明背景)
- **背景**: 透明背景效果最佳
- **風格**: 簡潔明瞭的icon風格

## 設置檢查清單

### ? **基本設置**
- [ ] InventoryUI腳本已附加到合適的GameObject
- [ ] Player GameObject有"Player"標籤
- [ ] PlayerSwimmingController已附加到Player
- [ ] 3個UI Image組件已設置到Slot Images陣列

### ? **Icon設置** 
- [ ] 已創建ToolItem資產
- [ ] 圖片已設置為Sprite (2D and UI)
- [ ] ToolItem的icon或iconTexture已設置
- [ ] ToolType已正確選擇

### ? **測試功能**
- [ ] 可以撿起物品 (E鍵)
- [ ] 可以放入inventory (1,2,3鍵)
- [ ] UI顯示正確的icon/顏色
- [ ] 有脈衝動畫效果

## 常見問題解答

### ? **為什麼顯示顏色而不是icon？**
- 這是fallback機制，表示沒有找到icon
- 檢查ToolItem是否設置了icon或iconTexture
- 確認圖片導入設置正確

### ? **UI在哪裡顯示？**
- 預設會自動偵測Player並顯示UI
- 如果沒有顯示，檢查Player標籤和腳本設置

### ? **可以使用Texture2D嗎？**
- 可以！系統會自動轉換為Sprite
- 同時設置icon和iconTexture時，優先使用icon

### ? **動畫不工作？**
- 檢查Slot Backgrounds陣列是否正確設置
- 確認GameObject處於active狀態

現在您應該能夠看到您的inventory box顯示漂亮的icon了！ ??
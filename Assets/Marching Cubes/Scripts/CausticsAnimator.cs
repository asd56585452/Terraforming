using UnityEngine;

// 這個腳本負責持續平移材質的貼圖座標 (UV Offset)，
// 並設置投射光的顏色。
public class CausticsAnimator : MonoBehaviour
{
    [Tooltip("請將 CausticsMaterial 拖曳到此處")]
    public Material causticsMaterial;

    [Tooltip("控制水紋的流動速度和方向 (X, Y)")]
    public Vector2 scrollSpeed = new Vector2(0.05f, 0.05f);

    // 新增：用於在 Inspector 中調整投射光的顏色
    [Tooltip("Caustics 投射光的顏色和亮度")]
    public Color causticsColor = new Color(0.8f, 0.9f, 1.0f, 1.0f); // 預設為淺藍白色

    private Vector2 uvOffset = Vector2.zero;
    
    // Projector Shader 通常使用這個屬性來接收顏色
    private static readonly int ColorID = Shader.PropertyToID("_Color");
    private static readonly int MainTexOffset = Shader.PropertyToID("_MainTex");

    void Start()
    {
        // 確保材質被連結
        if (causticsMaterial == null)
        {
            Debug.LogError("Caustics Material 未連結到 Caustics Animator 腳本上！", this);
            return;
        }

        // 在遊戲開始時，將 Inspector 設定的顏色應用到材質上
        // Projector/Multiply Shader 通常使用內建的 _Color 屬性來進行顏色相乘
        causticsMaterial.SetColor(ColorID, causticsColor);
    }
    
    void Update()
    {
        if (causticsMaterial == null)
            return;
        
        // 1. 計算新的 UV Offset (隨時間累積位移)
        uvOffset += scrollSpeed * Time.deltaTime; 

        // 2. 應用新的 Offset 到材質上
        causticsMaterial.SetTextureOffset(MainTexOffset, uvOffset);

        // 3. 避免數值無限增大
        if (uvOffset.x > 1.0f || uvOffset.y > 1.0f)
        {
            uvOffset.x %= 1.0f;
            uvOffset.y %= 1.0f;
        }
    }

    void OnDisable()
    {
        if (causticsMaterial != null)
        {
            causticsMaterial.SetTextureOffset(MainTexOffset, Vector2.zero);
        }
    }
}
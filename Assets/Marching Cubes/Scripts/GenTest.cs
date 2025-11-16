using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

public class GenTest : MonoBehaviour
{

	[Header("Init Settings")]
    public int terrainSeed = 0;
    public Vector3Int numChunks = new Vector3Int(4, 4, 4);

    public Vector3Int numPointsPerAxis = new Vector3Int(10, 10, 10);
    public Vector3 boundsSize = new Vector3(10, 10, 10);
    public float isoLevel = 0f;
    public bool invertDensity = false;
    public bool useFlatShading;

	public float noiseScale;
	public float noiseHeightMultiplier;
	public bool blurMap;
	public int blurRadius = 3;

    [Header("Cave Generation")]
    public bool generateCaves = true;
    public int caveSeed = 0;
    public int numWalkers = 10;
    public int walkLength = 500;
    public float tunnelRadius = 2.5f;
    public float digStrength = 1.0f; // 用來控制挖掘強度
    public float movement = 2.0f;

    [Header("MainCave Generation")]
    public bool generateMainCaveSystem = true;
    public float mainCaveRadius = 15f; 
    public float maindigStrength = 1.0f; 


    [Header("References")]
	public ComputeShader meshCompute;
	public ComputeShader densityCompute;
	public ComputeShader blurCompute;
	public ComputeShader editCompute;
	public Material material;


	// Private
	ComputeBuffer triangleBuffer;
	ComputeBuffer triCountBuffer;
	[HideInInspector] public RenderTexture rawDensityTexture;
	[HideInInspector] public RenderTexture processedDensityTexture;
	Chunk[] chunks;

	VertexData[] vertexDataArray;

	int totalVerts;

	// Stopwatches
	System.Diagnostics.Stopwatch timer_fetchVertexData;
	System.Diagnostics.Stopwatch timer_processVertexData;
	RenderTexture originalMap;

	void Start()
	{
        if (invertDensity)
        {
            digStrength *= -1;
            isoLevel*= -1;
            maindigStrength *= -1;
        }
        InitTextures();
		CreateBuffers();

		CreateChunks();

		var sw = System.Diagnostics.Stopwatch.StartNew();
		GenerateAllChunks();
		Debug.Log("Generation Time: " + sw.ElapsedMilliseconds + " ms");

		ComputeHelper.CreateRenderTexture3D(ref originalMap, processedDensityTexture);
		ComputeHelper.CopyRenderTexture3D(processedDensityTexture, originalMap);

	}

	void InitTextures()
	{

        // Explanation of texture size:
        // Each pixel maps to one point.
        // Each chunk has "numPointsPerAxis" points along each axis
        // The last points of each chunk overlap in space with the first points of the next chunk
        // Therefore we need one fewer pixel than points for each added chunk
        Vector3Int size = new Vector3Int(
        numChunks.x * (numPointsPerAxis.x - 1) + 1,
        numChunks.y * (numPointsPerAxis.y - 1) + 1,
        numChunks.z * (numPointsPerAxis.z - 1) + 1
		);
        Create3DTexture(ref rawDensityTexture, size, "Raw Density Texture");
		Create3DTexture(ref processedDensityTexture, size, "Processed Density Texture");

		if (!blurMap)
		{
			processedDensityTexture = rawDensityTexture;
		}

        densityCompute.SetInts("textureSize", size.x, size.y, size.z);
        editCompute.SetInts("textureSize", size.x, size.y, size.z);
        blurCompute.SetInts("textureSize", size.x, size.y, size.z);
        meshCompute.SetInts("textureSize", size.x, size.y, size.z);

        // Set textures on compute shaders
        densityCompute.SetTexture(0, "DensityTexture", rawDensityTexture);
		editCompute.SetTexture(0, "EditTexture", rawDensityTexture);
		blurCompute.SetTexture(0, "Source", rawDensityTexture);
		blurCompute.SetTexture(0, "Result", processedDensityTexture);
		meshCompute.SetTexture(0, "DensityTexture", (blurCompute) ? processedDensityTexture : rawDensityTexture);
	}
    // (將以下程式碼加入到 GenTest 類別內任何地方，例如在 Start() 之前或之後)
    [ContextMenu("Force Generate Terrain in Editor")]
    void ForceGenerateInEditor()
    {
        // 執行 Start() 中的初始化和生成邏輯
        if (invertDensity)
        {
            digStrength *= -1;
            isoLevel *= -1;
            maindigStrength *= -1;
        }
        
        InitTextures();
        CreateBuffers();
        CreateChunks();

        // 核心的生成呼叫
        GenerateAllChunks();
        
        // 複製貼圖（如果需要）
        ComputeHelper.CreateRenderTexture3D(ref originalMap, processedDensityTexture);
        ComputeHelper.CopyRenderTexture3D(processedDensityTexture, originalMap);
        
        Debug.Log("Terrain Generated in Editor!");
    }

    // ... (腳本其他部分保持不變)
	void GenerateAllChunks()
	{
		// Create timers:
		timer_fetchVertexData = new System.Diagnostics.Stopwatch();
		timer_processVertexData = new System.Diagnostics.Stopwatch();

		totalVerts = 0;
		ComputeDensity();


		for (int i = 0; i < chunks.Length; i++)
		{
			GenerateChunk(chunks[i]);
		}
		Debug.Log("Total verts " + totalVerts);

		// Print timers:
		Debug.Log("Fetch vertex data: " + timer_fetchVertexData.ElapsedMilliseconds + " ms");
		Debug.Log("Process vertex data: " + timer_processVertexData.ElapsedMilliseconds + " ms");
		Debug.Log("Sum: " + (timer_fetchVertexData.ElapsedMilliseconds + timer_processVertexData.ElapsedMilliseconds));


	}

	void ComputeDensity()
	{
        // Get points (each point is a vector4: xyz = position, w = density)
        Vector3Int textureSize = new Vector3Int(rawDensityTexture.width, rawDensityTexture.height, rawDensityTexture.volumeDepth);

        System.Random prng = new System.Random(terrainSeed);
        Vector3 offset = new Vector3((float)prng.NextDouble(), (float)prng.NextDouble(), (float)prng.NextDouble()) * 10000;
        densityCompute.SetVector("seedOffset", offset);

        densityCompute.SetBool("invertDensity", invertDensity);

        // textureSize 已經在 InitTextures 中設定
        // densityCompute.SetInts("textureSize", textureSize.x, textureSize.y, textureSize.z);

        densityCompute.SetVector("planetSize", boundsSize);
        densityCompute.SetFloat("noiseHeightMultiplier", noiseHeightMultiplier);
        densityCompute.SetFloat("noiseScale", noiseScale);

        ComputeHelper.Dispatch(densityCompute, textureSize.x, textureSize.y, textureSize.z);

        RunRandomWalkers();

        ProcessDensityMap();
    }

	void ProcessDensityMap()
	{
		if (blurMap)
		{
            Vector3Int size = new Vector3Int(rawDensityTexture.width, rawDensityTexture.height, rawDensityTexture.volumeDepth);

            // textureSize 已經在 InitTextures 中設定好了
            // blurCompute.SetInts("textureSize", size.x, size.y, size.z); 

            blurCompute.SetInts("brushCentre", 0, 0, 0);
            blurCompute.SetInt("blurRadius", blurRadius);
            //blurCompute.SetInt("brushRadius", 0); // 在全圖模糊時，筆刷半徑應為0

            // 使用正確的三維尺寸進行 Dispatch
            ComputeHelper.Dispatch(blurCompute, size.x, size.y, size.z);
        }
	}

	void GenerateChunk(Chunk chunk)
	{


		// Marching cubes
		Vector3Int numVoxelsPerAxis = new Vector3Int(numPointsPerAxis.x - 1, numPointsPerAxis.y - 1, numPointsPerAxis.z - 1);
    int marchKernel = 0;

    // textureSize 已經在 InitTextures 中設定
    meshCompute.SetInts("numPointsPerAxis", numPointsPerAxis.x, numPointsPerAxis.y, numPointsPerAxis.z);
    meshCompute.SetFloat("isoLevel", isoLevel);
    meshCompute.SetVector("planetSize", boundsSize);
    triangleBuffer.SetCounterValue(0);
    meshCompute.SetBuffer(marchKernel, "triangles", triangleBuffer);

    Vector3 chunkCoord = new Vector3(chunk.id.x * (numPointsPerAxis.x - 1), chunk.id.y * (numPointsPerAxis.y - 1), chunk.id.z * (numPointsPerAxis.z - 1));
    meshCompute.SetVector("chunkCoord", chunkCoord);

    ComputeHelper.Dispatch(meshCompute, numVoxelsPerAxis.x, numVoxelsPerAxis.y, numVoxelsPerAxis.z, marchKernel);

		// Create mesh
		int[] vertexCountData = new int[1];
		triCountBuffer.SetData(vertexCountData);
		ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);

		timer_fetchVertexData.Start();
		triCountBuffer.GetData(vertexCountData);

		int numVertices = vertexCountData[0] * 3;

		// Fetch vertex data from GPU

		triangleBuffer.GetData(vertexDataArray, 0, 0, numVertices);

		timer_fetchVertexData.Stop();

		//CreateMesh(vertices);
		timer_processVertexData.Start();
		chunk.CreateMesh(vertexDataArray, numVertices, useFlatShading);
		timer_processVertexData.Stop();
	}

	void Update()
	{

		// TODO: move somewhere more sensible
		// For URP shaders, texture names need underscore prefix when declared in Properties
		// Try both names for compatibility with old and new shaders
		if (material.HasProperty("_DensityTex"))
		{
			material.SetTexture("_DensityTex", originalMap);
		}
		else
		{
			// Fallback for old built-in shader
			material.SetTexture("DensityTex", originalMap);
		}
		material.SetFloat("oceanRadius", 200.0f);
        material.SetVector("planetBoundsSize", boundsSize);

        /*
		if (Input.GetKeyDown(KeyCode.G))
		{
			Debug.Log("Generate");
			GenerateAllChunks();
		}
		*/
    }



	void CreateBuffers()
	{
        int numPoints = numPointsPerAxis.x * numPointsPerAxis.y * numPointsPerAxis.z;
        Vector3Int numVoxelsPerAxis = new Vector3Int(numPointsPerAxis.x - 1, numPointsPerAxis.y - 1, numPointsPerAxis.z - 1);
        int numVoxels = numVoxelsPerAxis.x * numVoxelsPerAxis.y * numVoxelsPerAxis.z;
        int maxTriangleCount = numVoxels * 5;
        int maxVertexCount = maxTriangleCount * 3;

        triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        triangleBuffer = new ComputeBuffer(maxVertexCount, ComputeHelper.GetStride<VertexData>(), ComputeBufferType.Append);
        vertexDataArray = new VertexData[maxVertexCount];
    }

	void ReleaseBuffers()
	{
		ComputeHelper.Release(triangleBuffer, triCountBuffer);
	}

	void OnDestroy()
	{
		ReleaseBuffers();
		foreach (Chunk chunk in chunks)
		{
			chunk.Release();
		}
	}


    void CreateChunks()
    {
        chunks = new Chunk[numChunks.x * numChunks.y * numChunks.z];
        Vector3 chunkSize = new Vector3(boundsSize.x / numChunks.x, boundsSize.y / numChunks.y, boundsSize.z / numChunks.z);
        int i = 0;

        for (int y = 0; y < numChunks.y; y++)
        {
            for (int x = 0; x < numChunks.x; x++)
            {
                for (int z = 0; z < numChunks.z; z++)
                {
                    Vector3Int coord = new Vector3Int(x, y, z);
                    float posX = (-(numChunks.x - 1f) / 2 + x) * chunkSize.x;
                    float posY = (-(numChunks.y - 1f) / 2 + y) * chunkSize.y;
                    float posZ = (-(numChunks.z - 1f) / 2 + z) * chunkSize.z;
                    Vector3 centre = new Vector3(posX, posY, posZ);

                    GameObject meshHolder = new GameObject($"Chunk ({x}, {y}, {z})");
                    meshHolder.transform.parent = transform;
                    meshHolder.layer = gameObject.layer;

                    // 我們需要傳遞 numPointsPerAxis 到 Chunk 構造函數
                    Chunk chunk = new Chunk(coord, centre, chunkSize, numPointsPerAxis, meshHolder);
                    chunk.SetMaterial(material);
                    chunks[i] = chunk;
                    i++;
                }
            }
        }
    }


    public void Terraform(Vector3 point, float weight, float radius)
    {
        // 1. 獲取完整的三維貼圖尺寸
        Vector3Int editTextureSize = new Vector3Int(rawDensityTexture.width, rawDensityTexture.height, rawDensityTexture.volumeDepth);

        // 2. 分別計算每個軸上一個像素對應的世界尺寸
        Vector3 editPixelWorldSize = new Vector3(
            boundsSize.x / editTextureSize.x,
            boundsSize.y / editTextureSize.y,
            boundsSize.z / editTextureSize.z
        );

        // 3. 計算世界半徑在像素空間中的大小。為簡化，我們以 Y 軸為基準
        int editRadius = Mathf.CeilToInt(radius / editPixelWorldSize.y);

        // 4. 分量式地計算正規化座標 (0-1 範圍)
        float tx = Mathf.Clamp01((point.x + boundsSize.x / 2) / boundsSize.x);
        float ty = Mathf.Clamp01((point.y + boundsSize.y / 2) / boundsSize.y);
        float tz = Mathf.Clamp01((point.z + boundsSize.z / 2) / boundsSize.z);

        // 5. 分量式地計算最終的編輯中心點像素座標
        int editX = Mathf.RoundToInt(tx * (editTextureSize.x - 1));
        int editY = Mathf.RoundToInt(ty * (editTextureSize.y - 1));
        int editZ = Mathf.RoundToInt(tz * (editTextureSize.z - 1));

        editCompute.SetFloat("weight", weight);
        editCompute.SetFloat("deltaTime", Time.deltaTime);
        editCompute.SetInts("brushCentre", editX, editY, editZ);
        editCompute.SetInt("brushRadius", editRadius);

        // editCompute 的 textureSize 已經在 InitTextures 中設定好了
        ComputeHelper.Dispatch(editCompute, editTextureSize.x, editTextureSize.y, editTextureSize.z);

        if (blurMap)
        {
            // blurCompute 的 textureSize 已經在 InitTextures 中設定好了
            blurCompute.SetInts("brushCentre", editX - blurRadius - editRadius, editY - blurRadius - editRadius, editZ - blurRadius - editRadius);
            blurCompute.SetInt("blurRadius", blurRadius);
            blurCompute.SetInt("brushRadius", editRadius);
            int k = (editRadius + blurRadius) * 2;
            ComputeHelper.Dispatch(blurCompute, k, k, k);
        }

        // 更新受影響的區塊
        // 注意: worldRadius 的計算也應基於 Y 軸，與 editRadius 保持一致
        float worldRadius = (editRadius + 1 + ((blurMap) ? blurRadius : 0)) * editPixelWorldSize.y;
        for (int i = 0; i < chunks.Length; i++)
        {
            Chunk chunk = chunks[i];
            if (MathUtility.SphereIntersectsBox(point, worldRadius, chunk.centre, chunk.size))
            {
                chunk.terra = true;
                GenerateChunk(chunk);
            }
        }
    }

    void Create3DTexture(ref RenderTexture texture, Vector3Int size, string name)
	{
		//
		var format = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
		if (texture == null || !texture.IsCreated() || texture.width != size.x || texture.height != size.y || texture.volumeDepth != size.z || texture.graphicsFormat != format)
		{
			//Debug.Log ("Create tex: update noise: " + updateNoise);
			if (texture != null)
			{
				texture.Release();
			}
			const int numBitsInDepthBuffer = 0;
            texture = new RenderTexture(size.x, size.y, numBitsInDepthBuffer);
            texture.graphicsFormat = format;
            texture.volumeDepth = size.z;
            texture.enableRandomWrite = true;
			texture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;


			texture.Create();
		}
		texture.wrapMode = TextureWrapMode.Repeat;
		texture.filterMode = FilterMode.Bilinear;
		texture.name = name;
	}

    void RunRandomWalkers()
    {
        if (!generateCaves) return;

         var originalRandomState = Random.state;
         Random.InitState(caveSeed);

        // 獲取 3D 貼圖的尺寸
        Vector3Int textureSize = new Vector3Int(rawDensityTexture.width, rawDensityTexture.height, rawDensityTexture.volumeDepth);
        float pixelToWorldRatio = boundsSize.y / textureSize.y;

        // 設定 editCompute 的固定參數
        editCompute.SetTexture(0, "EditTexture", rawDensityTexture);
        editCompute.SetFloat("deltaTime", 1.0f); // 我們直接控制，所以 deltaTime 設為 1

        if (generateMainCaveSystem)
        {
            // ========== 新邏輯：生成主洞穴和向上路徑 ==========

            // --- 步驟 1: 在底部中心挖一個大球體 ---
            int mainCaveRadiusInPixels = Mathf.CeilToInt(mainCaveRadius / pixelToWorldRatio);
            Vector3Int mainCaveCenter = new Vector3Int(textureSize.x / 2, 0, textureSize.z / 2); // 中間偏下方

            editCompute.SetFloat("weight", maindigStrength); // 挖掘強度
            editCompute.SetInt("brushRadius", mainCaveRadiusInPixels);
            editCompute.SetInts("brushCentre", mainCaveCenter.x, mainCaveCenter.y, mainCaveCenter.z);

            ComputeHelper.Dispatch(editCompute, textureSize.x, textureSize.y, textureSize.z);
        }

        editCompute.SetFloat("weight", digStrength); // 挖掘強度
        int editRadius = Mathf.CeilToInt(tunnelRadius / (boundsSize.y / textureSize.y));
        editCompute.SetInt("brushRadius", Mathf.CeilToInt(editRadius));


        for (int i = 0; i < numWalkers; i++)
        {
            // 隨機選擇一個起始點 (遠離邊界)
            Vector3 currentPos = new Vector3(
                Random.Range(10, textureSize.x - 10),
                Random.Range(10, textureSize.y - 10),
                Random.Range(10, textureSize.z - 10)
            );

            Vector3 currentDir = Vector3.zero;

            for (int step = 0; step < walkLength; step++)
            {
                // 隨機改變方向
                if (Random.value < 0.25f || currentDir == Vector3Int.zero)
                {
                    Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f));
                    randomDir += currentDir * movement;
                    currentDir = randomDir.normalized;
                }

                // 移動挖掘者
                currentPos = currentPos + currentDir * editRadius/2;

                editCompute.SetFloat("weight", digStrength); // 挖掘強度

                //反彈
                int border = editRadius / 2; // 設定邊界寬度

                // 檢查 X 軸
                if (currentPos.x < border)
                {
                    currentPos.x = border; // 將位置拉回邊界內
                    currentDir.x *= -1;    // X 方向反轉
                }
                else if (currentPos.x > textureSize.x - 1- border)
                {
                    currentPos.x = textureSize.x - border;
                    currentDir.x *= -1;
                }

                // 檢查 Y 軸
                if (currentPos.y < border)
                {
                    currentPos.y = border;
                    currentDir.y *= -1;    // Y 方向反轉
                }
                else if (currentPos.y > textureSize.y - 1 - border)
                {
                    currentPos.y = textureSize.y - border;
                    currentDir.y *= -1;
                    editCompute.SetFloat("weight", digStrength*100); // 挖掘強度
                }

                // 檢查 Z 軸
                if (currentPos.z < border)
                {
                    currentPos.z = border;
                    currentDir.z *= -1;    // Z 方向反轉
                }
                else if (currentPos.z > textureSize.z - 1 - border)
                {
                    currentPos.z = textureSize.z - border;
                    currentDir.z *= -1;
                }

                // 設定筆刷中心並執行挖掘
                Vector3Int brushCentreInt = new Vector3Int(
                Mathf.RoundToInt(currentPos.x),
                Mathf.RoundToInt(currentPos.y),
                Mathf.RoundToInt(currentPos.z)
            );
                editCompute.SetInts("brushCentre", brushCentreInt.x, brushCentreInt.y, brushCentreInt.z);

                //// 計算執行緒組大小，確保涵蓋整個筆刷範圍
                //int dispatchSize = Mathf.CeilToInt(tunnelRadius * 2) / 8 + 1;
                ComputeHelper.Dispatch(editCompute, textureSize.x, textureSize.y, textureSize.z);
            }
        }
         Random.state = originalRandomState;
    }



}
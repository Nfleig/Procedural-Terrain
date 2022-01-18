using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

public class WorldGenerator : MonoBehaviour
{
    public GameObject chunkObject;
    public ComputeShader biomeShader;
    public ComputeShader terrainShader;
    public int xSize = 16;
    public int zSize = 16;
    public MapSettings heightMapSettings;
    public MapSettings temperatureMapSettings;
    public MapSettings moistureMapSettings;
    public MapSettings biomeBlendMapSettings;
    public MapSettings jitterMapSettings;

    private float step;
    public int chunkResolution;
    public float chunkSize;
    public int chunkGenBatchSize;
    private float scale;
    public float depth;
    private bool isGenerating = true;

    //Test seed: -1207784884
    public int seed;
    public bool randomSeed;
    public bool drawBiomes;
    public bool useBiomeBlending;
    public bool useBiomeHeights;
    private bool useJitteredPointDictionary;
    public bool flatMap;
    public bool drawJitteredPoints;
    public int GPUCurveResolution;
    public Biome[] biomes;
    public float biomeBlendRange;
    private System.Random prng;
    public Texture2D heightCurveTexture;
    public Texture2D biomeGradientTexture;
    public Texture2D biomeCurveTexture;
    public AnimationCurve biomeHeightSmoothingCurve;

    Queue<MapThreadInfo> mapDataThreadInfoQueue = new Queue<MapThreadInfo>();
    Queue<ChunkGenerator> chunkQueue = new Queue<ChunkGenerator>();

    public Dictionary<Vector2, Chunk> chunkDictionary = new Dictionary<Vector2, Chunk>();

    [Serializable]
    public class MapSettings
    {
        public float scale;
        public AnimationCurve curve;
        public int octaves;
        public float persistance;
        public float lacunarity;
        public Vector2 offset;
        public Octave[] octaveArray;

        public MapSettings(float scale, AnimationCurve curve, int octaves, float persistance, float lacunarity, Vector2 offset, System.Random prng)
        {
            this.scale = scale;
            this.curve = curve;
            this.octaves = octaves;
            this.persistance = persistance;
            this.lacunarity = lacunarity;
            this.offset = offset;
            octaveArray = GenerateOctaves(octaves, persistance, lacunarity, prng);
        }

        public void GenerateOctaveArray(System.Random prng)
        {
            octaveArray = GenerateOctaves(octaves, persistance, lacunarity, prng);
        }

        public Octave[] getOctaveArray()
        {
            return octaveArray;
        }
    }
    
    [Serializable]
    public struct Octave
    {
        public float frequency;
        public Vector2 offset;
        public float amplitude;

        public Octave(float frequency, Vector2 offset, float amplitude)
        {
            this.frequency = frequency;
            this.offset = offset;
            this.amplitude = amplitude;
        }
    }
    struct Output
    {
        public int chunkBiome;
        public float averageElevation;

        public Output(int chunkBiome, float averageElevation)
        {
            this.chunkBiome = chunkBiome;
            this.averageElevation = averageElevation;
        }
    }

    [Serializable]
    public struct Biome
    {
        public String name;
        public float moisture;
        public float temperature;
        public Gradient color;
        public AnimationCurve heightCurve;

        public Biome(String name, float moisture, float temperature, Gradient color, AnimationCurve heightCurve)
        {
            this.name = name;
            this.moisture = moisture;
            this.temperature = temperature;
            this.color = color;
            this.heightCurve = heightCurve;
        }
    }

    public struct BiomeResult
    {
        public Color color;
        public float[,] noiseMap;

        public BiomeResult(float[,] noiseMap, Color color)
        {
            this.noiseMap = noiseMap;
            this.color = color;
        }
    }

    public struct MapData
    {
        public readonly float[,] heightMap;
        public readonly Color[] colorMap;
        public readonly ChunkGenerator chunk;

        public MapData(float[,] heightMap, Color[] colorMap, ChunkGenerator chunk)
        {
            this.heightMap = heightMap;
            this.colorMap = colorMap;
            this.chunk = chunk;
        }
    }

    public struct MapThreadInfo
    {
        public readonly Action<MapData> callback;
        public readonly MapData data;
        
        public MapThreadInfo(Action<MapData> callback, MapData data)
        {
            this.callback = callback;
            this.data = data;
        }
    }

    public void Awake()
    {
        //CreateWorld();
        calculatedPointsPerChunk.Clear();
        calculatedBiomes.Clear();
        allPoints.Clear();
        Array.Sort(biomes, CompareTemperatures);
        DeleteWorld();
        scale = chunkSize * 2 / (float)chunkResolution;
        //print(adjustedNoiseScale);
        step = chunkSize;
        if (randomSeed)
        {
            seed = (int)(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
        }
        prng = new System.Random(seed);
        heightMapSettings.GenerateOctaveArray(prng);
        temperatureMapSettings.GenerateOctaveArray(prng);
        moistureMapSettings.GenerateOctaveArray(prng);
        biomeBlendMapSettings.GenerateOctaveArray(prng);
        jitterMapSettings.GenerateOctaveArray(prng);
        InitializeTerrainShader();
        ChunkGenerator.depth = depth;
        if (flatMap)
        {
            ChunkGenerator.depth = 0;
        }
        

    }

    public void Update()
    {
        if (mapDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < mapDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo threadInfo = mapDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.data);
            }
        }
        if (isGenerating)
        {
            int i = 0;
            while (i < chunkGenBatchSize && chunkQueue.Count > 0)
            {
                ChunkGenerator chunk = chunkQueue.Dequeue();
                CalculateChunk(chunk);
                i++;
            }
        }
        isGenerating = chunkQueue.Count > 0;
    }

    public bool IsGenerating()
    {
        return isGenerating;
    }

    public static Octave[] GenerateOctaves(int octaves, float persistance, float lacunarity, System.Random prng)
    {
        float frequency = 1;
        float amplitude = 1;
        Octave[] octaveArray = new Octave[octaves];
        for (int i = 0; i < octaves; i++)
        {
            Vector2 octaveOffset = new Vector2(prng.Next(-1000, 1000), prng.Next(-1000, 1000));
            octaveArray[i] = new Octave(frequency, octaveOffset, amplitude);
            frequency *= lacunarity;
            amplitude *= persistance;
        }
        return octaveArray;
    }

    void DeleteWorld()
    {
        ChunkGenerator[] allChunks = (ChunkGenerator[])GameObject.FindObjectsOfType(typeof(ChunkGenerator));
        foreach (ChunkGenerator chunk in allChunks)
        {
            DestroyImmediate(chunk.gameObject);
        }
        chunkQueue.Clear();
    }

    public void GenerateChunk(MapData mapData)
    {
        ChunkGenerator newChunk = mapData.chunk.GetComponent<ChunkGenerator>();
        //newChunk.GenerateTerrain(mapData.heightMap, chunkResolution, chunkResolution, scale, depth, mapData.colorMap);
        newChunk.InitializeTerrain(chunkResolution, chunkResolution, scale);
    }

    public Chunk GPUGenerateChunk(int x, int y)
    {
        ChunkGenerator newChunk = Instantiate(chunkObject, new Vector3(x * step, 0, y * step), Quaternion.identity).GetComponent<ChunkGenerator>();
        newChunk.InitializeTerrain(chunkResolution, chunkResolution, scale);

        newChunk.GetComponent<Chunk>().position = new Vector2(x, y);
        chunkQueue.Enqueue(newChunk);

        return newChunk.GetComponent<Chunk>();
    }

    void CalculateChunk(ChunkGenerator chunk)
    {
        Vector2 position = chunk.GetComponent<Chunk>().position;
        int x = (int)position.x;
        int y = (int)position.y;
        int heightMapKernel = terrainShader.FindKernel("HeightMap");
        terrainShader.SetTexture(heightMapKernel, "HeightMapTexture", chunk.heightMapTexture);
        terrainShader.SetTexture(heightMapKernel, "HeightMapTexture", chunk.heightMapTexture);
        terrainShader.SetTexture(heightMapKernel, "ColorMapTexture", chunk.colorMapTexture);
        terrainShader.SetFloats("ChunkPosition", x, y);
        JitterPoints(x, y);
        terrainShader.Dispatch(heightMapKernel, chunk.heightMapTexture.width / 5, chunk.heightMapTexture.width / 5, 1);
        chunk.DeformMesh();
    }

    public Texture2D BakeMapCurves()
    {
        Texture2D curveTexture = new Texture2D(GPUCurveResolution, 6);
        for (int i = 0; i < curveTexture.width; i++)
        {
            float curveIndex = (float)i / (float)curveTexture.width;
            float curveValue = heightMapSettings.curve.Evaluate(curveIndex);
            curveTexture.SetPixel(i, 0, new Color(curveValue, curveValue, curveValue));

            curveValue = temperatureMapSettings.curve.Evaluate(curveIndex);
            curveTexture.SetPixel(i, 1, new Color(curveValue, curveValue, curveValue));

            curveValue = moistureMapSettings.curve.Evaluate(curveIndex);
            curveTexture.SetPixel(i, 2, new Color(curveValue, curveValue, curveValue));

            curveValue = biomeBlendMapSettings.curve.Evaluate(curveIndex);
            curveTexture.SetPixel(i, 3, new Color(curveValue, curveValue, curveValue));

            curveValue = jitterMapSettings.curve.Evaluate(curveIndex);
            curveTexture.SetPixel(i, 4, new Color(curveValue, curveValue, curveValue));

            curveValue = biomeHeightSmoothingCurve.Evaluate(curveIndex);
            curveTexture.SetPixel(i, 5, new Color(curveValue, curveValue, curveValue));
        }
        curveTexture.Apply();
        return curveTexture;
    }
    public Texture2D BakeBiomeCurves()
    {
        Texture2D curveTexture = new Texture2D(GPUCurveResolution, biomes.Length);
        for (int y = 0; y < biomes.Length; y++)
        {
            for (int x = 0; x < curveTexture.width; x++)
            {
                float curveValue = biomes[y].heightCurve.Evaluate((float)x / (float)curveTexture.width);
                curveTexture.SetPixel(x, y, new Color(curveValue, curveValue, curveValue));
                //print(heightCurveTexture.GetPixel(i, 0));
            }
        }
        curveTexture.Apply();
        return curveTexture;
    }

    public Texture2D BakeBiomes()
    {
        Texture2D curveTexture = new Texture2D(GPUCurveResolution, biomes.Length);
        for (int y = 0; y < biomes.Length; y++)
        {
            for (int x = 0; x < curveTexture.width; x++)
            {
                Color gradientValue = biomes[y].color.Evaluate((float)x / (float)curveTexture.width);
                curveTexture.SetPixel(x, y, gradientValue);
                //print(heightCurveTexture.GetPixel(i, 0));
            }
        }
        curveTexture.Apply();
        return curveTexture;
    }

    public void GenerateChunk(MapData mapData, Texture2D texture)
    {
        ChunkGenerator newChunk = mapData.chunk.GetComponent<ChunkGenerator>();
        newChunk.GenerateTerrain(texture);
    }

    public void CreateWorld()
    {
        Awake();
        /*
        GenerateOctaves(octaves);
        DeleteWorld();
        scale = chunkSize * 2 / (float)chunkResolution;
        adjustedNoiseScale = noiseScale / chunkResolution;
        seed = (int)(Random.value * 10000);
        prng = new System.Random(seed);
        */
        //print(adjustedNoiseScale);
        for (int x = 0; x < xSize; x++)
        {
            for (int z = 0; z < zSize; z++)
            {
                GPUGenerateChunk(x, z);

            }
        }
        while(chunkQueue.Count > 0)
        {
            CalculateChunk(chunkQueue.Dequeue());
        }
        DisposeBuffers();
    }

    /*
     * TODO: Make changes in mesh resolution not impact the position in the noise map
     * 
     * Huge help: https://www.redblobgames.com/maps/terrain-from-noise/
     * https://www.reddit.com/r/proceduralgeneration/comments/qgz2m0/i_need_help_pls/
     */

    public float[,] GenerateHeightMap(int xSize, int ySize, float scale, Octave[] octaveArray, AnimationCurve _heightCurve, Vector2 offset)
    {
        AnimationCurve heightCurve = new AnimationCurve(_heightCurve.keys);
        float[,] heightMap = new float[xSize, ySize];
        for (int x = 0; x < xSize; x++)
        {
            for (int y = 0; y < ySize; y++)
            {
                float noiseHeight = 0;
                float sumAmplitudes = 0;
                foreach (Octave octave in octaveArray)
                {
                    float noiseX = ((float)x + offset.x) / ((float)xSize * scale) - 0.5f;
                    float noiseY = ((float)y + offset.y) / ((float)ySize * scale) - 0.5f;
                    noiseHeight += octave.amplitude * Mathf.PerlinNoise(noiseX * octave.frequency + octave.offset.x, noiseY * octave.frequency + octave.offset.y);
                    sumAmplitudes += octave.amplitude;
                }
                //print(noiseHeight / sumAmplitudes);
                heightMap[x, y] = heightCurve.Evaluate(noiseHeight / sumAmplitudes);
                //print(heightMap[x, y]);
            }
        }
        return heightMap;
    }


    public float[,] GenerateHeightMap(MapSettings settings, Vector2 position)
    {
        AnimationCurve heightCurve = new AnimationCurve(settings.curve.keys);
        Vector2 offset = settings.offset + (position * chunkResolution);
        int mapSize = chunkResolution + 1;
        float[,] heightMap = new float[mapSize, mapSize];
        for (int x = 0; x < mapSize; x++)
        {
            for (int y = 0; y < mapSize; y++)
            {
                float noiseHeight = 0;
                float sumAmplitudes = 0;
                foreach (Octave octave in settings.getOctaveArray())
                {
                    float noiseX = ((float)x + offset.x) / ((float)mapSize * settings.scale) - 0.5f;
                    float noiseY = ((float)y + offset.y) / ((float)mapSize * settings.scale) - 0.5f;
                    noiseHeight += octave.amplitude * Mathf.PerlinNoise(noiseX * octave.frequency + octave.offset.x, noiseY * octave.frequency + octave.offset.y);
                    sumAmplitudes += octave.amplitude;
                }
                heightMap[x, y] = heightCurve.Evaluate(noiseHeight / sumAmplitudes);
            }
        }
        return heightMap;
    }

    GPUMapSettings ConvertToGPUSettings(MapSettings settings)
    {
        GPUMapSettings mapSettings = new GPUMapSettings();
        Octave[] octaves = settings.getOctaveArray();
        mapSettings.Scale = settings.scale;
        mapSettings.Offset = settings.offset;
        mapSettings.OctaveCount = octaves.Length;
        return mapSettings;
    }

    struct GPUBiome
    {
        public float temperature;
        public float moisture;

        public GPUBiome(float temperature, float moisture)
        {
            this.temperature = temperature;
            this.moisture = moisture;
        }
    }

    struct GPUMapSettings
    {
        public float Scale;
        public Vector2 Offset;
        public int OctaveCount;
    }

    //https://gamedev.stackexchange.com/questions/71014/passing-input-to-compute-shader
    void InitializeTerrainShader()
    {
        MapSettings settings = heightMapSettings;

        int kernel = terrainShader.FindKernel("HeightMap");
        int jitterKernel = terrainShader.FindKernel("JitterPoints");
        terrainShader.SetFloats("Offset", settings.offset.x, settings.offset.y);
        terrainShader.SetFloat("Scale", settings.scale);
        terrainShader.SetFloat("Size", chunkResolution);
        terrainShader.SetFloat("Resolution", chunkResolution + 2);
        terrainShader.SetFloat("CurveResolution", GPUCurveResolution);
        terrainShader.SetBool("DrawBiomes", drawBiomes);
        terrainShader.SetBool("BiomeBlending", useBiomeBlending);
        terrainShader.SetFloat("BiomeBlendRadius", biomeBlendRange);
        terrainShader.SetBool("BiomeHeights", useBiomeHeights);

        outputBuffer = new ComputeBuffer(1, sizeof(int) + sizeof(float));
        terrainShader.SetBuffer(jitterKernel, "OutputBuffer", outputBuffer);

        GPUBiome[] GPUBiomes = new GPUBiome[biomes.Length];
        for (int i = 0; i < GPUBiomes.Length; i++)
        {
            GPUBiomes[i] = new GPUBiome(biomes[i].temperature, biomes[i].moisture);
        }
        int biomeSize = sizeof(float) * 2;
        biomeBuffer = new ComputeBuffer(biomes.Length, biomeSize);
        biomeBuffer.SetData(GPUBiomes);
        terrainShader.SetBuffer(kernel, "Biomes", biomeBuffer);
        terrainShader.SetBuffer(jitterKernel, "Biomes", biomeBuffer);
        terrainShader.SetInt("BiomeLength", biomes.Length);
        GPUSetMapSettings(kernel, temperatureMapSettings, 1);
        GPUSetMapSettings(kernel, moistureMapSettings, 2);
        GPUSetMapSettings(kernel, biomeBlendMapSettings, 3);
        GPUSetMapSettings(jitterKernel, temperatureMapSettings, 1);
        GPUSetMapSettings(jitterKernel, moistureMapSettings, 2);
        GPUSetMapSettings(jitterKernel, jitterMapSettings, 4);
        GPUSetMapSettings(kernel, heightMapSettings, 0);

        GPUMapSettings[] maps = new GPUMapSettings[4];
        maps[0] = ConvertToGPUSettings(heightMapSettings);
        maps[1] = ConvertToGPUSettings(temperatureMapSettings);
        maps[2] = ConvertToGPUSettings(moistureMapSettings);
        maps[3] = ConvertToGPUSettings(biomeBlendMapSettings);
        int mapSize = sizeof(float) *  + sizeof(int);
        mapBuffer = new ComputeBuffer(4, mapSize);
        mapBuffer.SetData(maps);
        terrainShader.SetBuffer(kernel, "maps", mapBuffer);
        terrainShader.SetBuffer(jitterKernel, "maps", mapBuffer);

        heightCurveTexture = BakeMapCurves();
        terrainShader.SetTexture(kernel, "HeightCurveTexture", heightCurveTexture);
        terrainShader.SetTexture(jitterKernel, "HeightCurveTexture", heightCurveTexture);
        biomeGradientTexture = BakeBiomes();
        terrainShader.SetTexture(kernel, "BiomeGradientTexture", biomeGradientTexture);
        biomeCurveTexture = BakeBiomeCurves();
        terrainShader.SetTexture(kernel, "BiomeCurveTexture", biomeCurveTexture);
    }

    Dictionary<Vector2, Dictionary<Vector2, Vector3>> calculatedPointsPerChunk = new Dictionary<Vector2, Dictionary<Vector2, Vector3>>();
    Dictionary<Vector4, float> calculatedBiomes = new Dictionary<Vector4, float>();
    List<Vector4> allPoints = new List<Vector4>();

    int[] cosValues = { 1, 0, -1, 0, 1, -1, -1, 1};
    int[] sinValues = { 0, 1, 0, -1, 1, 1, -1, -1};

    void JitterPoints(int chunkX, int chunkY)
    {
        List<Vector3> jitteredPoints = new List<Vector3>();
        float size = (float)chunkResolution / (float)(chunkResolution + 2);
        bool hasJitteredPoints = false;
        float resolution = chunkResolution;
        float chunkRadius = (chunkResolution / 2) * Mathf.Sqrt(2);
        float boundary = (chunkRadius - (chunkResolution / 2)) / resolution;
        float pointArea = biomeBlendRange + 0.0f;
        bool[] quadrants = new bool[8];
        Dictionary<Vector2, Vector3> calculatedPointGrid;
        if (useJitteredPointDictionary)
        {
            hasJitteredPoints = calculatedPointsPerChunk.ContainsKey(new Vector2(chunkX, chunkY));
            if (hasJitteredPoints)
            {
                calculatedPointGrid = calculatedPointsPerChunk[new Vector2(chunkX, chunkY)];
                foreach (Vector3 point in calculatedPointGrid.Values)
                {
                    jitteredPoints.Add(point);
                    quadrants[0] = quadrants[0] || (point.x <= pointArea && point.y <= pointArea);
                    quadrants[1] = quadrants[1] || (point.x > pointArea && point.x < 1 - pointArea && point.y <= pointArea);
                    quadrants[2] = quadrants[2] || (point.x >= 1 - pointArea && point.y <= pointArea);
                    quadrants[3] = quadrants[3] || (point.x >= 1 - pointArea && point.y > pointArea && point.y < 1 - pointArea);
                    quadrants[4] = quadrants[4] || (point.x >= 1 - pointArea && point.y >= 1 - pointArea);
                    quadrants[5] = quadrants[5] || (point.x > pointArea && point.x < 1 - pointArea && point.y >= 1 - pointArea);
                    quadrants[6] = quadrants[6] || (point.x <= pointArea && point.y >= 1 - pointArea);
                    quadrants[7] = quadrants[7] || (point.x <= pointArea && point.x < 1 - pointArea && point.y >= 1 - pointArea);
                }
            }
        }
        for (float y = -pointArea * resolution; y <= (1 + pointArea) * resolution; y += chunkResolution / 16)
        {
            for (float x = -pointArea * resolution; x <= (1 + pointArea) * resolution; x += chunkResolution / 16)
            {
                float scaledX = (float)x / resolution;
                float scaledY = (float)y / resolution;
                if (useJitteredPointDictionary && hasJitteredPoints)
                {
                    if(quadrants[0] && (scaledX <= pointArea && scaledY <= pointArea)) { continue; }
                    if(quadrants[1] && (scaledX > pointArea && scaledX < 1 - pointArea && scaledY <= pointArea)) { continue; }
                    if(quadrants[2] && (scaledX >= 1 - pointArea && scaledY <= pointArea)) { continue; }
                    if(quadrants[3] && (scaledX >= 1 - pointArea && scaledY > pointArea && scaledY < 1 - pointArea)) { continue; }
                    if(quadrants[4] && (scaledX >= 1 - pointArea && scaledY >= 1 - pointArea)) { continue; }
                    if(quadrants[5] && (scaledX > pointArea && scaledX < 1 - pointArea && scaledY >= 1 - pointArea)) { continue; }
                    if(quadrants[6] && (scaledX <= pointArea && scaledY >= 1 - pointArea)) { continue; }
                    if(quadrants[7] && (scaledX <= pointArea && scaledX < 1 - pointArea && scaledY >= 1 - pointArea)) { continue; }
                }
                //print(chunkX + ", " + chunkY + ": " + scaledX + " " + scaledY);
                jitteredPoints.Add(new Vector3(x, y, 0.1f));
                //SampleHeightMap(jitterMapSettings, new Vector2(x, y), new Vector2(chunkX, chunkY)) * 2f * Mathf.PI
                //allPoints.Add(new Vector4(x + (chunkX * chunkSize), 1, y + (chunkY * chunkSize), chunkY));
            }
        }
        Vector3[] jitteredPointArray = jitteredPoints.ToArray();
        jitteredPointBuffer = new ComputeBuffer(jitteredPointArray.Length, sizeof(float) * 3);
        jitteredPointBuffer.SetData(jitteredPointArray);
        terrainShader.SetBuffer(terrainShader.FindKernel("JitterPoints"), "jitteredPoints", jitteredPointBuffer);
        Output[] outputArray = new Output[1];
        outputArray[0] = new Output(-2, 0);
        terrainShader.SetBuffer(terrainShader.FindKernel("JitterPoints"), "OutputBuffer", outputBuffer);
        outputBuffer.SetData(outputArray);
        terrainShader.SetInt("jitteredPointsLength", jitteredPointArray.Length);
        terrainShader.Dispatch(terrainShader.FindKernel("JitterPoints"), (int) (jitteredPointArray.Length / 10) + 1, 1, 1);
        terrainShader.SetBuffer(terrainShader.FindKernel("HeightMap"), "jitteredPoints", jitteredPointBuffer);
        terrainShader.SetBuffer(terrainShader.FindKernel("HeightMap"), "OutputBuffer", outputBuffer);

        if (drawJitteredPoints)
        {
            Vector3[] nJitteredPointArray = new Vector3[jitteredPointArray.Length];
            jitteredPointBuffer.GetData(nJitteredPointArray);
            foreach (Vector3 point in nJitteredPointArray)
            {
                allPoints.Add(new Vector4((point.x + chunkX) * chunkSize, 50, (point.y + chunkY) * chunkSize, chunkY));
            }
        }

        if (useJitteredPointDictionary)
        {
            Vector3[] newJitteredPointArray = new Vector3[jitteredPointArray.Length];
            jitteredPointBuffer.GetData(newJitteredPointArray);
            Vector2 chunkPosition = new Vector2(chunkX, chunkY);
            for (int i = 0; i < newJitteredPointArray.Length; i++)
            {
                //allPoints.Add(new Vector4((newJitteredPointArray[i].x + chunkX) * chunkSize, 1, (newJitteredPointArray[i].y + chunkY) * chunkSize, chunkY));
                if (newJitteredPointArray[i].x > pointArea && newJitteredPointArray[i].x < 1 - pointArea && newJitteredPointArray[i].y > pointArea && newJitteredPointArray[i].y < 1 - pointArea)
                {
                    continue;
                }
                for (int dir = 0; dir < 8; dir++)
                {
                    if ((cosValues[dir] == 0 || (cosValues[dir] < 0 && newJitteredPointArray[i].x <= pointArea) || (cosValues[dir] > 0 && newJitteredPointArray[i].x >= 1 - pointArea)) && (sinValues[dir] == 0 || (sinValues[dir] < 0 && newJitteredPointArray[i].y <= pointArea) || (sinValues[dir] > 0 && newJitteredPointArray[i].y >= 1 - pointArea)))
                    {
                        Vector2 newChunkPosition = new Vector2(chunkPosition.x + cosValues[dir], chunkPosition.y + sinValues[dir]);
                        Vector3 oPoint = jitteredPointArray[i];
                        Vector3 newPoint = new Vector3(newJitteredPointArray[i].x - (1 * cosValues[dir]), newJitteredPointArray[i].y - (1 * sinValues[dir]), newJitteredPointArray[i].z);
                        //Vector3 newPoint = newJitteredPointArray[i];

                        if (chunkDictionary.ContainsKey(newChunkPosition))
                        {
                            continue;
                        }

                        if (calculatedPointsPerChunk.ContainsKey(newChunkPosition))
                        {
                            Vector2 oldPoint = new Vector2(jitteredPointArray[i].x - (size * cosValues[dir]), jitteredPointArray[i].y - (size * sinValues[dir]));
                            if (!calculatedPointsPerChunk[newChunkPosition].ContainsKey(oldPoint))
                            {
                                calculatedPointsPerChunk[newChunkPosition].Add(oldPoint, newPoint);
                                //allPoints.Add(new Vector3((oldPoint.x + newChunkPosition.x) * chunkSize, 1, (oldPoint.y + newChunkPosition.y) * chunkSize));
                            }
                        }
                        else
                        {
                            Dictionary<Vector2, Vector3> calculatedPoints = new Dictionary<Vector2, Vector3>();
                            Vector2 oldPoint = new Vector2(jitteredPointArray[i].x - (size * cosValues[dir]), jitteredPointArray[i].y - (size * sinValues[dir]));
                            calculatedPoints.Add(oldPoint, newPoint);
                            calculatedPointsPerChunk.Add(newChunkPosition, calculatedPoints);
                            //allPoints.Add(new Vector3((oldPoint.x + newChunkPosition.x) * chunkSize, 1, (oldPoint.y + newChunkPosition.y) * chunkSize));

                        }
                    }
                }
            }
            calculatedPointsPerChunk.Remove(chunkPosition);
            //allPoints.Add(new Vector4((-pointArea + chunkX) * chunkSize, 1, (-pointArea + chunkY) * chunkSize, chunkY + 1));
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        foreach (Vector4 point in allPoints)
        {
            if (point.w == 0)
            {
                Gizmos.color = Color.red;
            } else
            {
                Gizmos.color = Color.blue;
            }
            Gizmos.DrawSphere(point, 1f);
        }
        
    }


    void GPUSetMapSettings(int kernel, MapSettings settings, int id)
    {
        Octave[] octaves = settings.getOctaveArray();
        terrainShader.SetInt("OctaveCount", octaves.Length);
        int vectorSize = sizeof(float) * 2;
        int totalSize = (sizeof(float) * 2) + vectorSize;
        octaveBuffer = new ComputeBuffer(octaves.Length, totalSize);
        octaveBuffer.SetData(octaves);
        switch (id)
        {
            case 0:
                terrainShader.SetBuffer(kernel, "HeightMapOctaves", octaveBuffer);
                break;

            case 1:
                terrainShader.SetBuffer(kernel, "TemperatureMapOctaves", octaveBuffer);
                break;

            case 2:
                terrainShader.SetBuffer(kernel, "MoistureMapOctaves", octaveBuffer);
                break;

            case 3:
                terrainShader.SetBuffer(kernel, "BiomeBlendMapOctaves", octaveBuffer);
                break;

            case 4:
                terrainShader.SetBuffer(kernel, "JitterMapOctaves", octaveBuffer);
                break;
        }
    }

    ComputeBuffer octaveBuffer;
    ComputeBuffer biomeBuffer;
    ComputeBuffer mapBuffer;
    ComputeBuffer jitteredPointBuffer;
    ComputeBuffer outputBuffer;
    
    void DisposeBuffers()
    {
        octaveBuffer.Dispose();
        biomeBuffer.Dispose();
        mapBuffer.Dispose();
        jitteredPointBuffer.Dispose();
        outputBuffer.Dispose();
    }

    public float SampleHeightMap(MapSettings settings, Vector2 position, Vector2 chunkPosition)
    {
        AnimationCurve heightCurve = new AnimationCurve(settings.curve.keys);
        int mapSize = chunkResolution + 1;
        while (position.x < 0)
        {
            chunkPosition.x -= 1;
            position.x += chunkResolution;
        }
        while (position.x > chunkResolution)
        {
            chunkPosition.x += 1;
            position.x -= chunkResolution;
        }
        while (position.y < 0)
        {
            chunkPosition.y -= 1;
            position.y += chunkResolution;
        }
        while (position.y > chunkResolution)
        {
            chunkPosition.y += 1;
            position.y -= chunkResolution;
        }
        Vector2 offset = settings.offset + (chunkPosition * chunkResolution);
        float height;
        float noiseHeight = 0;
        float sumAmplitudes = 0;
        foreach (Octave octave in settings.getOctaveArray())
        {
            float noiseX = (position.x + offset.x) / ((float)mapSize * settings.scale) - 0.5f;
            float noiseY = (position.y + offset.y) / ((float)mapSize * settings.scale) - 0.5f;
            noiseHeight += octave.amplitude * Mathf.PerlinNoise(noiseX * octave.frequency + octave.offset.x, noiseY * octave.frequency + octave.offset.y);
            sumAmplitudes += octave.amplitude;
        }
        height = heightCurve.Evaluate(noiseHeight / sumAmplitudes);
        return height;
    }

    /*
     * 
     * GUIDE: https://noiseposti.ng/posts/2021-03-13-Fast-Biome-Blending-Without-Squareness.html
     * https://gamedev.stackexchange.com/questions/191685/problems-blending-biomes-together-in-relation-to-moisture-temperature/191702#191702
     * 
     */

    public Biome getBiome(float temperature, float moisture)
    {
        Biome biome = biomes[0];
        for (int i = 0; i < biomes.Length; i++)
        {
            if (temperature <= biomes[i].temperature)
            {
                if (moisture <= biomes[i].moisture)
                {
                    biome = biomes[i];
                    break;
                }
            }
        }
        return biome;
    }

    public Biome getBiome(Vector2 position, Vector2 chunkPosition)
    {
        float temperature = SampleHeightMap(temperatureMapSettings, position, chunkPosition);
        float moisture = SampleHeightMap(moistureMapSettings, position, chunkPosition);
        return getBiome(temperature, moisture);
    }

    int CompareTemperatures(Biome x, Biome y)
    {
        if (x.temperature == y.temperature)
        {
            return 0;
        } else if (x.temperature > y.temperature)
        {
            return 1;
        } else
        {
            return -1;
        }
    }

    int CompareMoistures(Biome x, Biome y)
    {
        if (x.moisture == y.moisture)
        {
            return 0;
        }
        else if (x.moisture > y.moisture)
        {
            return 1;
        }
        else
        {
            return -1;
        }
    }
}

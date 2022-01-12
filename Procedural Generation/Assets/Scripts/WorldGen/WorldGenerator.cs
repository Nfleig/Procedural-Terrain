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
    public bool useJitteredPointDictionary;
    public bool useGPU;
    public int GPUCurveResolution;
    public Biome[] biomes;
    public float biomeBlendRange;
    private System.Random prng;
    float minimumHeight = Mathf.Infinity;
    float maximumHeight = -Mathf.Infinity;
    public Texture2D heightCurveTexture;
    public Texture2D biomeGradientTexture;
    public Texture2D biomeCurveTexture;

    Queue<MapThreadInfo> mapDataThreadInfoQueue = new Queue<MapThreadInfo>();
    Queue<ChunkGenerator> chunkQueue = new Queue<ChunkGenerator>();

    public Dictionary<Vector2, Chunk> chunkDictionary = new Dictionary<Vector2, Chunk>();

    [Serializable]
    public class MapSettings
    {
        public int size;
        public float scale;
        public AnimationCurve curve;
        public int octaves;
        public float persistance;
        public float lacunarity;
        public Vector2 offset;
        public Octave[] octaveArray;

        public MapSettings(int size, float scale, AnimationCurve curve, int octaves, float persistance, float lacunarity, Vector2 offset, System.Random prng)
        {
            this.size = size;
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
        scale = chunkSize * 2 / (float)heightMapSettings.size;
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

    public Chunk RequestChunk(int x, int y)
    {
        Chunk newChunk = Instantiate(chunkObject, new Vector3(x * step, 0, y * step), Quaternion.identity).GetComponent<Chunk>();
        RequestChunk(x, y, newChunk.GetComponent<ChunkGenerator>(), GenerateChunk);
        return newChunk;
    }

    public void GenerateChunk(MapData mapData)
    {
        ChunkGenerator newChunk = mapData.chunk.GetComponent<ChunkGenerator>();
        //newChunk.GenerateTerrain(mapData.heightMap, heightMapSettings.size, heightMapSettings.size, scale, depth, mapData.colorMap);
        newChunk.InitializeTerrain(heightMapSettings.size, heightMapSettings.size, scale);
    }

    public Chunk GPUGenerateChunk(int x, int y)
    {
        ChunkGenerator newChunk = Instantiate(chunkObject, new Vector3(x * step, 0, y * step), Quaternion.identity).GetComponent<ChunkGenerator>();
        newChunk.InitializeTerrain(heightMapSettings.size, heightMapSettings.size, scale);

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
        int biomeMapKernel = terrainShader.FindKernel("BiomeMap");
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
        Texture2D curveTexture = new Texture2D(GPUCurveResolution, 4);
        for (int i = 0; i < curveTexture.width; i++)
        {
            float curveIndex = (float)i / (float)curveTexture.width;
            float curveValue = heightMapSettings.curve.Evaluate((float)i / (float)curveTexture.width);
            curveTexture.SetPixel(i, 0, new Color(curveValue, curveValue, curveValue));

            curveValue = temperatureMapSettings.curve.Evaluate((float)i / (float)curveTexture.width);
            curveTexture.SetPixel(i, 1, new Color(curveValue, curveValue, curveValue));

            curveValue = moistureMapSettings.curve.Evaluate((float)i / (float)curveTexture.width);
            curveTexture.SetPixel(i, 2, new Color(curveValue, curveValue, curveValue));

            curveValue = biomeBlendMapSettings.curve.Evaluate((float)i / (float)curveTexture.width);
            curveTexture.SetPixel(i, 3, new Color(curveValue, curveValue, curveValue));
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

    void RequestChunk(int x, int y, ChunkGenerator chunk, Action<MapData> callback)
    {
        ThreadStart threadStart = delegate
        {
            MapDataThread(x, y, chunk, callback);
        };

        new Thread(threadStart).Start();
    }

    void MapDataThread(int x, int y, ChunkGenerator chunk, Action<MapData> callback)
    {

        Vector2 chunkPosition = new Vector2(x, y);
        
        float[,] heightMap = GenerateHeightMap(heightMapSettings, chunkPosition);
        float[,] temperatureMap = GenerateHeightMap(temperatureMapSettings, chunkPosition);
        float[,] moistureMap = GenerateHeightMap(moistureMapSettings, chunkPosition);
        Color[] colorMap;
        if (!useBiomeBlending)
        {
            colorMap = GenerateColorMap(heightMap, new Vector2(x, y));
        }
        else
        {
            if (IsOneBiome(temperatureMap, moistureMap))
            {
                colorMap = GenerateSingleBiomeColorMap(heightMap, chunkPosition);
            }
            else
            {
                colorMap = GenerateBiomeBlendMap(new Vector2(x, y), heightMap);
            }
        }
        MapData mapData = new MapData(heightMap, colorMap, chunk);

        lock (mapDataThreadInfoQueue)
        {
            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo(callback, mapData));
        }
    }

    
    public Chunk GenerateChunkImmediate(int x, int y)
    {
        ChunkGenerator newChunk = Instantiate(chunkObject, new Vector3(x * step, 0, y * step), Quaternion.identity).GetComponent<ChunkGenerator>();
        Vector2 chunkPosition = new Vector2(x, y);
        float[,] heightMap = GenerateHeightMap(heightMapSettings, chunkPosition);
        float[,] temperatureMap = GenerateHeightMap(temperatureMapSettings, chunkPosition);
        float[,] moistureMap = GenerateHeightMap(moistureMapSettings, chunkPosition);
        Color[] colorMap;
        if (!useBiomeBlending)
        {
            colorMap = GenerateColorMap(heightMap, new Vector2(x, y));
        }
        else
        {
            if (IsOneBiome(temperatureMap, moistureMap))
            {
                colorMap = GenerateSingleBiomeColorMap(heightMap, chunkPosition);
            } else
            {
                colorMap = GenerateBiomeBlendMap(new Vector2(x, y), heightMap);
            }
        }
        GenerateChunk(new MapData(heightMap, colorMap, newChunk));
        Chunk chunk = newChunk.GetComponent<Chunk>();
        chunk.position = new Vector2(x, y);
        return chunk;
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
        Vector2 offset = settings.offset + (position * settings.size);
        int mapSize = settings.size + 1;
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
        mapSettings.Size = settings.size;
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
        public float Size;
        public Vector2 Offset;
        public int OctaveCount;
    }

    struct Output
    {
        public int chunkBiome;

        public Output(int chunkBiome)
        {
            this.chunkBiome = chunkBiome;
        }
    }

    //https://gamedev.stackexchange.com/questions/71014/passing-input-to-compute-shader
    void InitializeTerrainShader()
    {
        MapSettings settings = heightMapSettings;

        int kernel = terrainShader.FindKernel("HeightMap");
        int jitterKernel = terrainShader.FindKernel("JitterPoints");
        terrainShader.SetFloats("Offset", settings.offset.x, settings.offset.y);
        terrainShader.SetFloat("Scale", settings.scale);
        terrainShader.SetFloat("Size", settings.size);
        terrainShader.SetFloat("Resolution", settings.size + 2);
        terrainShader.SetFloat("CurveResolution", GPUCurveResolution);
        terrainShader.SetBool("DrawBiomes", drawBiomes);
        terrainShader.SetBool("BiomeBlending", useBiomeBlending);
        terrainShader.SetFloat("BiomeBlendRadius", biomeBlendRange);
        terrainShader.SetBool("BiomeHeights", useBiomeHeights);

        outputBuffer = new ComputeBuffer(1, sizeof(int));
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
        GPUSetMapSettings(kernel, heightMapSettings, 0);

        GPUMapSettings[] maps = new GPUMapSettings[4];
        maps[0] = ConvertToGPUSettings(heightMapSettings);
        maps[1] = ConvertToGPUSettings(temperatureMapSettings);
        maps[2] = ConvertToGPUSettings(moistureMapSettings);
        maps[3] = ConvertToGPUSettings(biomeBlendMapSettings);
        int mapSize = sizeof(float) * 4 + sizeof(int);
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
    List<Vector3> allPoints = new List<Vector3>();

    int[] cosValues = { 1, 0, -1, 0, 1, -1, -1, 1};
    int[] sinValues = { 0, 1, 0, -1, 1, 1, -1, -1 };

    void JitterPoints(int chunkX, int chunkY)
    {
        List<Vector3> jitteredPoints = new List<Vector3>();
        float size = (float)heightMapSettings.size / (float)(heightMapSettings.size + 2);
        bool hasJitteredPoints = false;
        float resolution = heightMapSettings.size + 2;
        float chunkRadius = (heightMapSettings.size / 2) * Mathf.Sqrt(2);
        float boundary = (chunkRadius - (heightMapSettings.size / 2)) / resolution;
        Dictionary<Vector2, Vector3> calculatedPointGrid = new Dictionary<Vector2, Vector3>();
        if (useJitteredPointDictionary)
        {
            hasJitteredPoints = calculatedPointsPerChunk.ContainsKey(new Vector2(chunkX, chunkY));
            if (hasJitteredPoints)
            {
                calculatedPointGrid = calculatedPointsPerChunk[new Vector2(chunkX, chunkY)];
                
            }
        }
        for (int y = (heightMapSettings.size / 2) - (int) ((chunkRadius + ((biomeBlendRange + 0.05) * resolution))); y <= (heightMapSettings.size / 2) + (int)((chunkRadius + ((biomeBlendRange + 0.05) * resolution))); y += 4)
        {
            for (int x = (heightMapSettings.size / 2) - (int)((chunkRadius + ((biomeBlendRange + 0.05) * resolution))); x <= (heightMapSettings.size / 2) + (int)((chunkRadius + ((biomeBlendRange + 0.05) * resolution))); x += 4)
            {
                float scaledX = (float)(x - (2 * chunkX)) / resolution;
                float scaledY = (float)(y - (2 * chunkY)) / resolution;
                if (useJitteredPointDictionary && hasJitteredPoints)
                {
                    Vector2 position = new Vector2(scaledX, scaledY);
                    if (calculatedPointGrid.ContainsKey(position))
                    {
                        jitteredPoints.Add(calculatedPointGrid[position]);
                        print(chunkX + ", " + chunkY + ": " + scaledX + " " + scaledY);
                        allPoints.Add(new Vector3((calculatedPointGrid[position].x + chunkX) * chunkSize, 1, (calculatedPointGrid[position].y + chunkY) * chunkSize));
                        continue;
                    }
                }
                jitteredPoints.Add(new Vector3(scaledX, scaledY, (float) prng.NextDouble() * 2f * Mathf.PI));
                allPoints.Add(new Vector3((scaledX + chunkX) * chunkSize, 1, (scaledY + chunkY) * chunkSize));
            }
        }
        Vector3[] jitteredPointArray = jitteredPoints.ToArray();
        jitteredPointBuffer = new ComputeBuffer(jitteredPointArray.Length, sizeof(float) * 3);
        jitteredPointBuffer.SetData(jitteredPointArray);
        terrainShader.SetBuffer(terrainShader.FindKernel("JitterPoints"), "jitteredPoints", jitteredPointBuffer);
        Output[] outputArray = new Output[1];
        outputArray[0] = new Output(-2);
        terrainShader.SetBuffer(terrainShader.FindKernel("JitterPoints"), "OutputBuffer", outputBuffer);
        outputBuffer.SetData(outputArray);
        terrainShader.SetInt("jitteredPointsLength", jitteredPointArray.Length);
        terrainShader.Dispatch(terrainShader.FindKernel("JitterPoints"), (int) (jitteredPointArray.Length / 10) + 1, 1, 1);
        terrainShader.SetBuffer(terrainShader.FindKernel("HeightMap"), "jitteredPoints", jitteredPointBuffer);
        terrainShader.SetBuffer(terrainShader.FindKernel("HeightMap"), "OutputBuffer", outputBuffer);
        if (useJitteredPointDictionary)
        {
            Vector3[] newJitteredPointArray = new Vector3[jitteredPointArray.Length];
            jitteredPointBuffer.GetData(newJitteredPointArray);
            Vector2 chunkPosition = new Vector2(chunkX, chunkY);
            float pointArea = biomeBlendRange + 0.01f;
            for (int i = 0; i < newJitteredPointArray.Length; i++)
            {
                if (newJitteredPointArray[i].x > pointArea && newJitteredPointArray[i].x < 1 - pointArea && newJitteredPointArray[i].y > pointArea && newJitteredPointArray[i].y < 1 - pointArea)
                {
                    continue;
                }
                //allPoints.Add(new Vector3((newJitteredPointArray[i].x + chunkX) * chunkSize, 1, (newJitteredPointArray[i].y + chunkY) * chunkSize));
                for (int dir = 0; dir < 8; dir++)
                {
                    if ((cosValues[dir] == 0 || (cosValues[dir] < 0 && newJitteredPointArray[i].x <= pointArea) || (cosValues[dir] > 0 && newJitteredPointArray[i].x >= 1 - pointArea)) && (sinValues[dir] == 0 || (sinValues[dir] < 0 && newJitteredPointArray[i].y <= pointArea) || (sinValues[dir] > 0 && newJitteredPointArray[i].y >= 1 - pointArea)))
                    {
                        Vector2 newChunkPosition = new Vector2(chunkPosition.x + cosValues[dir], chunkPosition.y + sinValues[dir]);
                        Vector3 newPoint = new Vector3(newJitteredPointArray[i].x - (size * cosValues[dir]), newJitteredPointArray[i].y - (size * sinValues[dir]), newJitteredPointArray[i].z);
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
            allPoints.Add(new Vector3((0.5f + chunkX) * chunkSize, 1, (0.5f + chunkY) * chunkSize));
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        foreach (Vector3 point in allPoints)
        {
            Gizmos.DrawSphere(point, 1f);
        }
        
    }

    float Round(float number, params float[] possibleResults)
    {
        float result = number;
        float currentDifference = 10000000;
        foreach (float num in possibleResults)
        {
            float difference = Mathf.Abs(number - num);
            if (Mathf.Abs(number - num) < currentDifference)
            {
                result = num;
                currentDifference = difference;
            }
        }
        return result;
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
        int mapSize = settings.size + 1;
        while (position.x < 0)
        {
            chunkPosition.x -= 1;
            position.x += settings.size;
        }
        while (position.x > settings.size)
        {
            chunkPosition.x += 1;
            position.x -= settings.size;
        }
        while (position.y < 0)
        {
            chunkPosition.y -= 1;
            position.y += settings.size;
        }
        while (position.y > settings.size)
        {
            chunkPosition.y += 1;
            position.y -= settings.size;
        }
        Vector2 offset = settings.offset + (chunkPosition * settings.size);
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

    Color[] GenerateColorMap(float[,] heightMap, Vector2 chunkPosition)
    {
        Color[] colors = new Color[(int)Math.Pow(heightMapSettings.size + 1, 2)];

        float[] yValues = new float[colors.Length];

        int i = 0;
        for (int z = 0; z <= heightMapSettings.size; z++)
        {
            for (int x = 0; x <= heightMapSettings.size; x++)
            {
                float y = heightMap[x, z] * depth;
                maximumHeight = Mathf.Max(y, maximumHeight);
                minimumHeight = Mathf.Min(y, minimumHeight);

                yValues[i++] = y;
            }
        }
        //print(minimumHeight + " " + maximumHeight);
        i = 0;
        for (int z = 0; z <= heightMapSettings.size; z++)
        {
            for (int x = 0; x <= heightMapSettings.size; x++)
            {
                float y = Mathf.InverseLerp(minimumHeight, maximumHeight, yValues[i]);
                Color color = biomes[2].color.Evaluate(y);

                if (!drawBiomes)
                {
                    colors[i++] = color;
                    continue;
                }

                //float temperature = temperatureMap[x, z];
                float temperature = SampleHeightMap(temperatureMapSettings, new Vector2(x, z), chunkPosition);
                //float moisture = moistureMap[x, z];
                float moisture = SampleHeightMap(moistureMapSettings, new Vector2(x, z), chunkPosition);

                Biome currentBiome = getBiome(temperature, moisture);
                Biome otherBiome = new Biome();
                if (currentBiome.color != null)
                {
                    float temperatureDifference = currentBiome.temperature - temperature;
                    float moistureDifference = currentBiome.moisture - moisture;
                    float average = (temperatureDifference + moistureDifference) / 2;
                    if (temperatureDifference < biomeBlendRange)
                    {
                        Biome test = getBiome(temperature + biomeBlendRange, moisture + biomeBlendRange);
                        if (test.name != null && !test.name.Equals(currentBiome.name))
                        {
                            otherBiome = test;
                        } else
                        {
                            test = getBiome(temperature - biomeBlendRange, moisture - biomeBlendRange);
                            if (test.name != null && !test.name.Equals(currentBiome.name))
                            {
                                otherBiome = test;
                            }
                        }
                    }
                    Color biomeColor = currentBiome.color.Evaluate(y);
                    if (otherBiome.color != null)
                    {
                        Color otherColor = otherBiome.color.Evaluate(y);
                        float blendRange = Mathf.InverseLerp(-biomeBlendRange, biomeBlendRange, temperatureDifference);
                        color = Color.Lerp(color, biomeColor, blendRange);
                    }
                    else
                    {
                        color = biomeColor;
                    }
                }
                colors[i++] = color;

                //colors[i] = heightColor;

            }
        }
        //print(SampleHeightMap(temperatureMapSettings, new Vector2(0, 0), chunkPosition) == temperatureMap[0, 0]);
        return colors;
    }

    public bool IsOneBiome(float[,] temperatureMap, float[,] moistureMap)
    {
        Biome biome = getBiome(temperatureMap[0,0], moistureMap[0,0]);
        for (int y = 0; y <= heightMapSettings.size; y++)
        {
            for (int x = 0; x <= heightMapSettings.size; x++)
            {
                if (!getBiome(temperatureMap[x, y], moistureMap[x, y]).Equals(biome))
                {
                    return false;
                }
            }
        }
        return true;
    }

    public Color[] GenerateSingleBiomeColorMap(float[,] heightMap, Vector2 chunkPosition)
    {
        Color[] biomeColors = new Color[(int)Math.Pow(heightMapSettings.size + 1, 2)];
        Biome biome = getBiome(new Vector2(0, 0), chunkPosition);
        int i = 0;
        for (int y = 0; y <= heightMapSettings.size; y++)
        {
            for (int x = 0; x <= heightMapSettings.size; x++)
            {
                biomeColors[i++] = biome.color.Evaluate(heightMap[x, y]);
            }
        }
        return biomeColors;
    }

    /*
     * 
     * GUIDE: https://noiseposti.ng/posts/2021-03-13-Fast-Biome-Blending-Without-Squareness.html
     * https://gamedev.stackexchange.com/questions/191685/problems-blending-biomes-together-in-relation-to-moisture-temperature/191702#191702
     * 
     * TODO: Make this into a compute shader
     * 
     */

    public Color[] GenerateBiomeBlendMap(Vector2 chunkPosition, float[,] heightMap)
    {
        float jitterDistance = biomeBlendRange;
        float chunkRadius = Vector2.Distance(new Vector2(0, 0), new Vector2(heightMapSettings.size / 2, heightMapSettings.size / 2));
        float r = 2 + (2 * jitterDistance) + 1f + chunkRadius;
        //print(SampleHeightMap(temperatureMapSettings, new Vector2(64.1f, 64.1f), new Vector2(0, 0)) == SampleHeightMap(temperatureMapSettings, new Vector2(0.1f, 0.1f), new Vector2(1, 1)));
        float randomDirection = (float)prng.NextDouble() * 2 * Mathf.PI;
        float xPos = (heightMapSettings.size / 2 + 3) + (jitterDistance * Mathf.Cos(randomDirection));
        float yPos = (heightMapSettings.size / 2 + 3) + (jitterDistance * Mathf.Sin(randomDirection));
        Vector2 centerPoint = new Vector2(xPos, yPos);

        Color[,] biomeColors = new Color[heightMapSettings.size + 1, heightMapSettings.size + 1];
        float worldPointRadius = 10f;
        for (int x = (int)((heightMapSettings.size / 2) - chunkRadius); x <= (heightMapSettings.size / 2) + chunkRadius; x += 4)
        //for (int x = 0; x <= heightMapSettings.size; x += 2)
            {
            for (int y = (int)((heightMapSettings.size / 2) - chunkRadius); y <= (heightMapSettings.size / 2) + chunkRadius; y += 4)
            //for (int y = 0; y <= heightMapSettings.size; y += 2)
            {
                Vector2 jitterPoint;
                if (x == heightMapSettings.size / 2 + 3 && y == heightMapSettings.size / 2 + 3)
                {
                    jitterPoint = centerPoint;
                }
                else
                {
                    randomDirection = (float)prng.NextDouble() * 2 * Mathf.PI;
                    xPos = (float)x + (jitterDistance * Mathf.Cos(randomDirection));
                    yPos = (float)y + (jitterDistance * Mathf.Sin(randomDirection));
                    jitterPoint = new Vector2(xPos, yPos);
                }
                float distance = Vector2.Distance(centerPoint, jitterPoint);
                if (distance < r)
                {
                    Biome biome = getBiome(jitterPoint, chunkPosition);

                    for (int wx = (int)Mathf.Max(0, jitterPoint.x - worldPointRadius); wx < (int)Mathf.Min(heightMapSettings.size + 1, jitterPoint.x + worldPointRadius); wx++)
                    {
                        for (int wy = (int)Mathf.Max(0, jitterPoint.y - worldPointRadius); wy < (int)Mathf.Min(heightMapSettings.size + 1, jitterPoint.y + worldPointRadius); wy++)
                        {

                            if (biomeColors[wx, wy].Equals(Color.clear))
                            {
                                biomeColors[wx, wy] = biome.color.Evaluate(heightMap[wx, wy]);
                            }
                            else
                            {
                                distance = Vector2.Distance(new Vector2(wx, wy), jitterPoint);
                                distance = Mathf.InverseLerp(0, worldPointRadius, distance);
                                float biomeBlendNoise = SampleHeightMap(biomeBlendMapSettings, new Vector2(wx, wy), chunkPosition);
                                float finalWeight = (1 - distance) * biomeBlendNoise + 0.01f;
                                Color biomeColor = biome.color.Evaluate(heightMap[wx, wy]);
                                //print(biomeColors[x, y]);
                                biomeColors[wx, wy] = Color.Lerp(biomeColors[wx, wy], biomeColor, finalWeight);
                            }
                        }
                    }
                }
            }
        }
        Color[] finalColors = new Color[(int)Math.Pow(heightMapSettings.size + 1, 2)];
        int i = 0;
        for (int y = 0; y <= heightMapSettings.size; y++)
        {
            for (int x = 0; x <= heightMapSettings.size; x++)
            {
                finalColors[i] = biomeColors[x, y];
                i++;
            }
        }
        return finalColors;
    }


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

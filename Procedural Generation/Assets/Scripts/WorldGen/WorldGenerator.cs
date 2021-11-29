using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

public class WorldGenerator : MonoBehaviour
{
    public GameObject chunkObject;
    public int xSize = 16;
    public int zSize = 16;
    public MapSettings heightMapSettings;
    public MapSettings temperatureMapSettings;
    public MapSettings moistureMapSettings;
    public MapSettings biomeBlendMapSettings;

    private float step;

    public float chunkSize;
    private float scale;
    public float depth;

    public int seed;
    public bool randomSeed;
    public Gradient colorGradient;
    public Gradient testGradient;
    public bool drawBiomes;
    public bool useBiomeBlending;
    public Biome[] biomes;
    public float biomeBlendRange;
    private System.Random prng;
    float minimumHeight = Mathf.Infinity;
    float maximumHeight = -Mathf.Infinity;

    Queue<MapThreadInfo> mapDataThreadInfoQueue = new Queue<MapThreadInfo>();
    
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

        public Biome(String name, float moisture, float temperature, Gradient color)
        {
            this.name = name;
            this.moisture = moisture;
            this.temperature = temperature;
            this.color = color;
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
        Array.Sort(biomes, CompareTemperatures);
        

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
        newChunk.GenerateTerrain(mapData.heightMap, heightMapSettings.size, heightMapSettings.size, scale, depth, mapData.colorMap);
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
            if (isOneBiome(temperatureMap, moistureMap))
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

    void GenerateChunkImmediate(int x, int y)
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
            if (isOneBiome(temperatureMap, moistureMap))
            {
                colorMap = GenerateSingleBiomeColorMap(heightMap, chunkPosition);
            } else
            {
                colorMap = GenerateBiomeBlendMap(new Vector2(x, y), heightMap);
            }
        }
        GenerateChunk(new MapData(heightMap, colorMap, newChunk));
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
                GenerateChunkImmediate(x, z);

            }
        }
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

    Dictionary<Vector2, List<Vector4>> calculatedPointsPerChunk = new Dictionary<Vector2, List<Vector4>>();
    Dictionary<Vector4, Biome> calculatedBiomes = new Dictionary<Vector4, Biome>();

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
                Color color = colorGradient.Evaluate(y);

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

    public bool isOneBiome(float[,] temperatureMap, float[,] moistureMap)
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
     */

    public Color[] GenerateBiomeBlendMap(Vector2 chunkPosition, float[,] heightMap)
    {
        float jitterDistance = biomeBlendRange;
        Vector2 centerPoint = new Vector2();
        int i = 0;
        float chunkRadius = Vector2.Distance(new Vector2(0, 0), new Vector2(heightMapSettings.size / 2, heightMapSettings.size / 2));
        float r = 2 + (2 * jitterDistance) + 1f + chunkRadius;
        //print(SampleHeightMap(temperatureMapSettings, new Vector2(64.1f, 64.1f), new Vector2(0, 0)) == SampleHeightMap(temperatureMapSettings, new Vector2(0.1f, 0.1f), new Vector2(1, 1)));


        List<Vector2> jitterPoints = new List<Vector2>();
        for (int x = (int)((heightMapSettings.size / 2) - chunkRadius); x <= (heightMapSettings.size / 2) + chunkRadius; x += 4)
        //for (int x = 0; x <= heightMapSettings.size; x += 2)
            {
            for (int y = (int)((heightMapSettings.size / 2) - chunkRadius); y <= (heightMapSettings.size / 2) + chunkRadius; y += 4)
            //for (int y = 0; y <= heightMapSettings.size; y += 2)
            {
                float randomDirection = (float)prng.NextDouble() * 2 * Mathf.PI;
                float xPos = (float)x + (jitterDistance * Mathf.Cos(randomDirection));
                float yPos = (float)y + (jitterDistance * Mathf.Sin(randomDirection));
                Vector2 jitterPoint = new Vector2(xPos, yPos);
                jitterPoints.Add(jitterPoint);
                if (x == heightMapSettings.size / 2 + 3 && y == heightMapSettings.size / 2 + 3)
                {
                    centerPoint = jitterPoint;
                }
                i++;
            }
        }
        Color[,] biomeColors = new Color[heightMapSettings.size + 1, heightMapSettings.size + 1];
        float worldPointRadius = 10f;
        for (i = 0; i < jitterPoints.Count; i++)
        {
            float dx = jitterPoints[i].x - centerPoint.x;
            float dy = jitterPoints[i].y - centerPoint.y;
            float distance = Mathf.Sqrt(Mathf.Pow(dx, 2) + Mathf.Pow(dy, 2));
            if (distance < r)
            {
                
                float falloffValue = Mathf.Pow(Mathf.Max(0, Mathf.Pow(r, 2) - Mathf.Pow(dx, 2) - Mathf.Pow(dy, 2)), 2);
                float falloffWeight = Mathf.InverseLerp(0, Mathf.Pow(r, 4), falloffValue);
                Biome biome = getBiome(jitterPoints[i], chunkPosition);
                
                for (int x = (int)Mathf.Max(0, jitterPoints[i].x - worldPointRadius); x < (int)Mathf.Min(heightMapSettings.size + 1, jitterPoints[i].x + worldPointRadius); x++)
                {
                    for (int y = (int)Mathf.Max(0, jitterPoints[i].y - worldPointRadius); y < (int)Mathf.Min(heightMapSettings.size + 1, jitterPoints[i].y + worldPointRadius); y++)
                    {

                        if (biomeColors[x, y].Equals(Color.clear))
                        {
                            biomeColors[x, y] = biome.color.Evaluate(heightMap[x, y]);
                        } else
                        {
                            distance = Vector2.Distance(new Vector2(x, y), jitterPoints[i]);
                            distance = Mathf.InverseLerp(0, worldPointRadius, distance);
                            float biomeBlendNoise = SampleHeightMap(biomeBlendMapSettings, new Vector2(x, y), chunkPosition);
                            float finalWeight = (1 - distance) * biomeBlendNoise + 0.01f;
                            Color biomeColor = biome.color.Evaluate(heightMap[x, y]);
                            //print(biomeColors[x, y]);
                            biomeColors[x, y] = Color.Lerp(biomeColors[x, y], biomeColor, finalWeight);
                        }
                    }
                }
            }
        }
        Color[] finalColors = new Color[(int)Math.Pow(heightMapSettings.size + 1, 2)];
        i = 0;
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

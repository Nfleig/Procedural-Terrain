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

    public Vector2 offset;
    private float step;

    public float chunkSize;
    private float scale;
    public float depth;
    public AnimationCurve heightCurve;

    public float noiseScale;
    private float adjustedNoiseScale;
    public int chunkResolution;
    public int octaves;
    public float persistance;
    public float lacunarity;
    public int seed;
    public float frequency;
    public Octave[] heightMapArray;
    public Octave[] moistureMapArray;
    private System.Random prng;

    Queue<MapThreadInfo> mapDataThreadInfoQueue = new Queue<MapThreadInfo>();
    
    [System.Serializable]
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

    public struct MapData
    {
        public readonly float[,] heightMap;
        public readonly ChunkGenerator chunk;

        public MapData(float[,] heightMap, ChunkGenerator chunk)
        {
            this.heightMap = heightMap;
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
        scale = chunkSize * 2 / (float)chunkResolution;
        adjustedNoiseScale = noiseScale / chunkResolution;
        //print(adjustedNoiseScale);
        step = chunkSize;
        seed = (int)(UnityEngine.Random.value * 10000);
        prng = new System.Random(seed);
        heightMapArray = GenerateOctaves(octaves);
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

    Octave[] GenerateOctaves(int octaves)
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
        //float[,] newHeightMap = GenerateHeightMap(chunkResolution + 1, chunkResolution + 1, seed, noiseScale, new Vector2(offset.x + x * chunkResolution, offset.y + y * chunkResolution), octaves, persistance, lacunarity);
        RequestChunk(x, y, newChunk.GetComponent<ChunkGenerator>(), GenerateChunk);
        return newChunk;
    }

    public void GenerateChunk(MapData mapData)
    {
        ChunkGenerator newChunk = mapData.chunk.GetComponent<ChunkGenerator>();
        newChunk.GenerateTerrain(mapData.heightMap, chunkResolution, chunkResolution, scale, depth);
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
        float[,] heightMap = GenerateHeightMap(chunkResolution + 1, chunkResolution + 1, heightMapArray, heightCurve, new Vector2(offset.x + x * chunkResolution, offset.y + y * chunkResolution));
        MapData mapData = new MapData(heightMap, chunk);

        lock (mapDataThreadInfoQueue)
        {
            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo(callback, mapData));
        }
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
        step = chunkSize * 2;
        for (int x = 0; x < xSize; x++)
        {
            for (int z = 0; z < zSize; z++)
            {
                RequestChunk(x, z);

            }
        }
    }

    /*
     * TODO: Make changes in mesh resolution not impact the position in the noise map
     * 
     * Huge help: https://www.redblobgames.com/maps/terrain-from-noise/
     * https://www.reddit.com/r/proceduralgeneration/comments/qgz2m0/i_need_help_pls/
     */

    public float[,] GenerateHeightMap(int xSize, int ySize, Octave[] octaveArray, AnimationCurve _heightCurve, Vector2 offset)
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
                    float noiseX = ((float)x + offset.x) / (float)xSize - 0.5f;
                    float noiseY = ((float)y + offset.y) / (float)ySize - 0.5f;
                    noiseHeight = octave.amplitude * Mathf.PerlinNoise(noiseX * octave.frequency + octave.offset.x, noiseY * octave.frequency + octave.offset.y);
                    sumAmplitudes += octave.amplitude;
                }
                //print(noiseHeight / sumAmplitudes);
                heightMap[y, x] = heightCurve.Evaluate(noiseHeight / sumAmplitudes);
                //print(heightMap[y, x]);
            }
        }
        return heightMap;
    }
    /*

    public float[,] GenerateHeightMap(int xSize, int ySize, int seed, float scale, Vector2 offset, int octaves, float persistance, float lacunarity)
    {

        float[,] heightMap = new float[xSize, ySize];
        float maxPossibleHeight = 0;
        float minPossibleHeight = 0;

        float amplitude = 1;
        float frequency = 1;
        Vector2[] octaveOffsets = new Vector2[octaves];
        for (int i =  0; i < octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000) + offset.x;
            float offsetY = prng.Next(-100000, 100000) + offset.y;
            maxPossibleHeight += amplitude;
            amplitude *= persistance;
        }
        for (int x = 0; x < xSize; x++)
        {
            for (int y = 0; y < ySize; y++)
            {
                amplitude = 1;
                frequency = 1;
                float noiseHeight = 0;
                for (int i = 0; i < octaves; i++)
                {
                    float xCoord = (((float)x + offset.x) / frequency * scale);
                    float yCoord = (((float)y + offset.y) / frequency * scale);

                    float noise = Mathf.PerlinNoise(xCoord, yCoord) * 2 - 1;

                    noiseHeight += noise * amplitude;

                    amplitude *= persistance;
                    frequency *= lacunarity;
                    //noise *= noiseHeight;
                }
                heightMap[x, y] = noiseHeight;
            }
        }
        for (int x = 0; x < xSize; x++)
        {
            for (int y = 0; y < ySize; y++)
            {
                float normalizedHeight = (heightMap[x, y] + 1) / (2f * maxPossibleHeight / 1.75f);
                heightMap[x, y] = normalizedHeight;
            }
        }
        return heightMap;
    }
    */
}

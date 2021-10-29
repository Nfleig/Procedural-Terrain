using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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


    public void Awake()
    {
        //CreateWorld();
        DeleteWorld();
        scale = chunkSize * 2 / (float)chunkResolution;
        adjustedNoiseScale = noiseScale / chunkResolution;
        //print(adjustedNoiseScale);
        step = chunkSize * 2;
    }

    void DeleteWorld()
    {
        Object[] allChunks = GameObject.FindObjectsOfType(typeof(ChunkGenerator));
        foreach (ChunkGenerator chunk in allChunks)
        {
            DestroyImmediate(chunk.gameObject);
        }
    }

    public Chunk GenerateChunk(int x, int y)
    {
        ChunkGenerator newChunk = Instantiate(chunkObject, new Vector3(x * step, 0, y * step), Quaternion.identity).GetComponent<ChunkGenerator>();
        float[,] newHeightMap = GenerateHeightMap(chunkResolution + 1, chunkResolution + 1, (int)(Random.value * 10000), noiseScale, new Vector2(offset.x + x * chunkResolution, offset.y + y * chunkResolution), octaves, persistance, lacunarity);
        newChunk.setHeightMap(newHeightMap);
        newChunk.GenerateTerrain(chunkResolution, chunkResolution, scale, depth, heightCurve);
        return newChunk.GetComponent<Chunk>();
    }

    public void CreateWorld()
    {
        DeleteWorld();
        scale = chunkSize * 2 / (float)chunkResolution;
        adjustedNoiseScale = noiseScale / chunkResolution;
        //print(adjustedNoiseScale);
        step = chunkSize * 2;
        for (int x = 0; x < xSize; x++)
        {
            for (int z = 0; z < zSize; z++)
            {
                GenerateChunk(x, z);

            }
        }
    }

    /*
     * TODO: Make changes in mesh resolution not impact the position in the noise map
     */
    public float[,] GenerateHeightMap(int xSize, int ySize, int seed, float scale, Vector2 offset, int octaves, float persistance, float lacunarity)
    {
        System.Random prng = new System.Random(seed);

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
}

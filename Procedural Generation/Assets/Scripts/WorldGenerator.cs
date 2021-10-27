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


    public void Start()
    {
        CreateWorld();
    }

    void DeleteWorld()
    {
        Object[] allChunks = GameObject.FindObjectsOfType(typeof(ChunkGenerator));
        foreach (ChunkGenerator chunk in allChunks)
        {
            DestroyImmediate(chunk.gameObject);
        }
    }

    public void CreateWorld()
    {
        DeleteWorld();
        scale = chunkSize * 2 / (float)chunkResolution;
        adjustedNoiseScale = noiseScale / chunkResolution;
        step = chunkSize * 2;
        for (int x = 0; x < xSize; x++)
        {
            for (int z = 0; z < zSize; z++)
            {
                ChunkGenerator newChunk = Instantiate(chunkObject, new Vector3(x * step, 0, z * step), Quaternion.identity).GetComponent<ChunkGenerator>();
                float[,] newHeightMap = GenerateHeightMap(chunkResolution + 1, chunkResolution + 1, (int)(Random.value * 10000), new Vector2(offset.x + x * chunkResolution, offset.y + z * chunkResolution), octaves, persistance, lacunarity);
                newChunk.setHeightMap(newHeightMap);
                newChunk.GenerateTerrain(chunkResolution, chunkResolution, scale, depth, heightCurve);

            }
        }
    }

    /*
     * TODO: Make changes in mesh resolution not impact the position in the noise map
     */
    float[,] GenerateHeightMap(int xSize, int ySize, int seed, Vector2 offset, int octaves, float persistance, float lacunarity)
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
                    float xCoord = (((float)x + offset.x) / frequency * adjustedNoiseScale);
                    float yCoord = (((float)y + offset.y) / frequency * adjustedNoiseScale);

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

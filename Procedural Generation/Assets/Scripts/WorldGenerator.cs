using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    public GameObject chunkObject;
    private ChunkGenerator chunk;
    public int xSize = 16;
    public int zSize = 16;

    public float xOffset;
    public float yOffset;
    public float step;

    public float scale = 0.25f;

    public int chunkResolution;
    public int octaves;
    public float persistance;
    public float lacunarity;


    public void Start()
    {
        xOffset = Random.RandomRange(0, float.MaxValue);
        yOffset = Random.RandomRange(0, float.MaxValue);
        chunk = chunkObject.GetComponent<ChunkGenerator>();
        CreateWorld();
    }

    void CreateWorld()
    {
        for (int x = 0; x < xSize; x++)
        {
            for (int z = 0; z < zSize; z++)
            {
                ChunkGenerator newChunk = Instantiate(chunkObject, new Vector3(x * step, 0, z * step), Quaternion.identity).GetComponent<ChunkGenerator>();
                float[,] newHeightMap = GenerateHeightMap(chunkResolution + 1, chunkResolution + 1, xOffset + x * step + 1, yOffset + z * step + 1, octaves, persistance, lacunarity);
                newChunk.setHeightMap(newHeightMap);
                newChunk.GenerateTerrain();

            }
        }
    }
    public float GenerateWorld(int x, int y)
    {
        xOffset = Random.RandomRange(0, 999999f);
        yOffset = Random.RandomRange(0, 999999f);

        float xCoord = (float)x / scale + xOffset;
        float yCoord = (float)y / scale + yOffset;

        float noise = Mathf.PerlinNoise(xCoord, yCoord);
        return noise;
    }
    float[,] GenerateHeightMap(int xSize, int ySize, float xOffset, float yOffset, int octaves, float persistance, float lacunarity)
    {

        float[,] heightMap = new float[xSize, ySize];
        float maxHeightValue = float.MinValue;
        float minHeightValue = float.MaxValue;
        for (int x = 0; x < xSize; x++)
        {
            for (int y = 0; y < ySize; y++)
            {
                float amplitude = 1;
                float frequency = 1;
                float noiseHeight = 0;
                for (int i = 0; i < octaves; i++)
                {
                    float xCoord = ((float)x + xOffset / frequency * scale);
                    float yCoord = ((float)y + yOffset / frequency * scale);

                    float noise = Mathf.PerlinNoise(xCoord, yCoord) * 2 - 1;
                    //print(xCoord + " " + yCoord);

                    noiseHeight += noise * amplitude;

                    amplitude *= persistance;
                    frequency *= lacunarity;
                    //noise *= noiseHeight;
                }
                maxHeightValue = Mathf.Max(maxHeightValue, noiseHeight);
                minHeightValue = Mathf.Min(minHeightValue, noiseHeight);
                heightMap[x, y] = noiseHeight;
            }
        }
        for (int x = 0; x < xSize; x++)
        {
            for (int y = 0; y < ySize; y++)
            {
                heightMap[x, y] = Mathf.InverseLerp(minHeightValue, maxHeightValue, heightMap[x, y]);
            }
        }
        return heightMap;
    }
}

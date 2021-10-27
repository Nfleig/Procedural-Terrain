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

    public float scale = 0.25f;


    public void Start()
    {
        xOffset = Random.RandomRange(0, float.MaxValue);
        yOffset = Random.RandomRange(0, float.MaxValue);
        chunk = chunkObject.GetComponent<ChunkGenerator>();
        CreateWorld();
    }

    void CreateWorld()
    {
        float step = chunk.xSize * chunk.scale;
        for (int x = 0; x < xSize; x++)
        {
            for (int z = 0; z < zSize; z++)
            {
                ChunkGenerator newChunk = Instantiate(chunkObject, new Vector3(x * step, 0, z * step), Quaternion.identity).GetComponent<ChunkGenerator>();
                newChunk.xOffset = x * step;
                newChunk.yOffset = z * step;

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
}

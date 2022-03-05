using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

public class WorldGenerator : MonoBehaviour
{
    // Public properties
    public GameObject chunkObject;
    public ComputeShader biomeShader;
    public ComputeShader terrainShader;
    public int xSize = 16;
    public int zSize = 16;
    public MapSettings heightMapSettings;
    public MapSettings temperatureMapSettings;
    public MapSettings moistureMapSettings;
    public MapSettings biomeBlendMapSettings;
    public int chunkResolution;
    public MapSettings jitterMapSettings;
    public float chunkSize;
    public int chunkGenBatchSize;
    public float depth;
    private bool isGenerating = true;
    public float worldScale;
    public int seed;
    public bool randomSeed;
    public bool drawBiomes;
    public bool useBiomeBlending;
    public bool useBiomeHeights;
    public bool flatMap;
    public bool drawJitteredPoints;
    public int GPUCurveResolution;
    public Biome[] biomes;
    public float biomeBlendRange;
    public float biomeBlendLevel;
    public Texture2D heightCurveTexture;
    public Texture2D biomeGradientTexture;
    public Texture2D biomeCurveTexture;
    public Dictionary<Vector2, Chunk> chunkDictionary = new Dictionary<Vector2, Chunk>();

    // Private properties
    private float _step;
    private float _scale;
    private bool _useJitteredPointDictionary;
    private System.Random prng;
    private Queue<ChunkGenerator> chunkQueue = new Queue<ChunkGenerator>();
    private Dictionary<Vector2, Dictionary<Vector2, Vector3>> calculatedPointsPerChunk = new Dictionary<Vector2, Dictionary<Vector2, Vector3>>();
    private Dictionary<Vector4, float> calculatedBiomes = new Dictionary<Vector4, float>();
    private List<Vector4> allPoints = new List<Vector4>();
    private float[] biomeData;
    private int[] cosValues = { 1, 0, -1, 0, 1, -1, -1, 1};
    private int[] sinValues = { 0, 1, 0, -1, 1, 1, -1, -1};
    private ComputeBuffer octaveBuffer;
    private ComputeBuffer biomeBuffer;
    private ComputeBuffer mapBuffer;
    private ComputeBuffer jitteredPointBuffer;
    private ComputeBuffer outputBuffer;

    /*
     * This class stores all of the settings needed for a noise map. It is serializable so that it can be seen in the editor
     * For more information see this tutorial: https://www.redblobgames.com/maps/terrain-from-noise/
     */

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

            //Populate the octave array with the seeded random number generator

            octaveArray = GenerateOctaves(octaves, persistance, lacunarity, prng);
        }

        /*
         * This function initializes the octave array for the noise map. It needs to be called manually because the constructor isn't
         * used when the object is a public variable in the editor
         */
        public void SetOctaveArray(System.Random prng) {
            octaveArray = GenerateOctaves(octaves, persistance, lacunarity, prng);
        }

        public Octave[] getOctaveArray()
        {
            return octaveArray;
        }
    }
    
    /*
     * This struct represents an octave in the noisemap. It is serializable so that it can be seen in the editor
     * For more information see this tutorial: https://www.redblobgames.com/maps/terrain-from-noise/
     */

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

    /*
     * This structure is used to recieve output from the terrain shader
     */

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

    /*
     * This structure represents a biome
     */

    [Serializable]
    public struct Biome
    {
        public String name;
        public float moisture;      // The moisture range that the biome appears in
        public float temperature;   // The temperature range that the biome appears in
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

    /*
     * This structure holds all of the data that the terrain shader needs about biomes
     */

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

    /*
     * This structure holds all of the data that the terrain shader needs about biomes
     */

    struct GPUMapSettings
    {
        public float Scale;
        public Vector2 Offset;
        public int OctaveCount;
    }

    public void Awake()
    {
        // Clears all of the arrays and gets ready to make a new world.
        calculatedPointsPerChunk.Clear();
        calculatedBiomes.Clear();
        allPoints.Clear();
        Array.Sort(biomes, CompareBiomes);
        DeleteWorld();

        // Initializes some values for terrain generation. Might be depricated
        _scale = chunkSize * 2 / (float)chunkResolution;
        _step = chunkSize;

        // Generates a seeded random number generator.
        if (randomSeed)
        {
            seed = (int)(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
        }
        prng = new System.Random(seed);

        heightMapSettings.SetOctaveArray(prng);
        temperatureMapSettings.SetOctaveArray(prng);
        moistureMapSettings.SetOctaveArray(prng);
        biomeBlendMapSettings.SetOctaveArray(prng);
        jitterMapSettings.SetOctaveArray(prng);

        // Initializes the terrain shader.
        InitializeTerrainShader();

        // Sets the depth in the chunk generator
        ChunkGenerator.depth = depth * worldScale;
        if (flatMap)
        {
            ChunkGenerator.depth = 0;
        }
        

    }

    public void Update()
    {

        // If there are any chunks in the chunk queue, this takes a specified amount of them and computes them
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

        // Set the isGenerating flag so other objects can see if the world generator is running
        isGenerating = chunkQueue.Count > 0;
    }

    public bool IsGenerating()
    {
        return isGenerating;
    }

    /*
     * This function enerates octaves for the noise maps based on the seeded random number generator and other properties
     */
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

    /*
     * This function deletes all of the chunks in the world
     */
    void DeleteWorld()
    {
        ChunkGenerator[] allChunks = (ChunkGenerator[])GameObject.FindObjectsOfType(typeof(ChunkGenerator));
        foreach (ChunkGenerator chunk in allChunks)
        {
            DestroyImmediate(chunk.gameObject);
        }
        chunkQueue.Clear();
    }

    /*
     * This function creates a new chunk object and adds it to the chunk queue to be generated later
     */
    public Chunk GPUGenerateChunk(int x, int y)
    {
        // Initializes the chunk object
        ChunkGenerator newChunk = Instantiate(chunkObject, new Vector3(x * _step, 0, y * _step), Quaternion.identity).GetComponent<ChunkGenerator>();
        newChunk.InitializeTerrain(chunkResolution, chunkResolution, _scale);
        newChunk.GetComponent<Chunk>().position = new Vector2(x, y);

        chunkQueue.Enqueue(newChunk);

        return newChunk.GetComponent<Chunk>();
    }

    /*
     * This function uses the terrain shader to generate the terrain for a chunk
     */
    void CalculateChunk(ChunkGenerator chunk)
    {
        // Gets the chunk's position

        Vector2 position = chunk.GetComponent<Chunk>().position;
        int x = (int)position.x;
        int y = (int)position.y;

        // Prepares the terrain shader to generate the chunk
        // This is done by setting the output textures for the shader to textures in the chunk object and
        // setting the chunk position in the shader
        int heightMapKernel = terrainShader.FindKernel("HeightMap");
        terrainShader.SetTexture(heightMapKernel, "HeightMapTexture", chunk.heightMapTexture);
        terrainShader.SetTexture(heightMapKernel, "ColorMapTexture", chunk.colorMapTexture);
        terrainShader.SetFloats("ChunkPosition", x, y);

        // Calls the JitterPoints function to generate a jittered point array within the shader for the biome blending

        JitterPoints(x, y);

        // Dispatches the shader

        terrainShader.Dispatch(heightMapKernel, chunk.heightMapTexture.width / 5, chunk.heightMapTexture.width / 5, 1);

        // Reads the output from the shader once it's finished and uses that to get data about the chunk's terrain

        Output[] outputs = new Output[1];
        outputBuffer.GetData(outputs);
        chunk.biomeData = biomeData;

        // Calls a function to apply the generated terrain to the chunk mesh

        chunk.DeformMesh();
    }

    /*
     * This function bakes the curves from several different noisemaps into a texture
     * that can be used by the terrain shader
     */

    public Texture2D BakeMapCurves()
    {
        // Initialize a new texture

        Texture2D curveTexture = new Texture2D(GPUCurveResolution, 5);

        // Each curve is a row in the texture

        for (int i = 0; i < curveTexture.width; i++)
        {
            // The curve index translates the pixel coordinate in the texture to
            // a position on the curve

            float curveIndex = (float)i / (float)curveTexture.width;
            
            // Height map

            float curveValue = heightMapSettings.curve.Evaluate(curveIndex);
            curveTexture.SetPixel(i, 0, new Color(curveValue, curveValue, curveValue));

            // Temperature map
            
            curveValue = temperatureMapSettings.curve.Evaluate(curveIndex);
            curveTexture.SetPixel(i, 1, new Color(curveValue, curveValue, curveValue));

            // Moisture map

            curveValue = moistureMapSettings.curve.Evaluate(curveIndex);
            curveTexture.SetPixel(i, 2, new Color(curveValue, curveValue, curveValue));

            // Biome Blending map

            curveValue = biomeBlendMapSettings.curve.Evaluate(curveIndex);
            curveTexture.SetPixel(i, 3, new Color(curveValue, curveValue, curveValue));

            // Jittered Points map

            curveValue = jitterMapSettings.curve.Evaluate(curveIndex);
            curveTexture.SetPixel(i, 4, new Color(curveValue, curveValue, curveValue));
        }
        curveTexture.Apply();
        return curveTexture;
    }

    /*
     * This function bakes all of the height curves for the biomes into a texture that can be
     * used by the terrain shader
     */

    public Texture2D BakeBiomeCurves()
    {
        Texture2D curveTexture = new Texture2D(GPUCurveResolution, biomes.Length);
        for (int y = 0; y < biomes.Length; y++)
        {
            for (int x = 0; x < curveTexture.width; x++)
            {
                float curveValue = biomes[y].heightCurve.Evaluate((float)x / (float)curveTexture.width);
                curveTexture.SetPixel(x, y, new Color(curveValue, curveValue, curveValue));
            }
        }
        curveTexture.Apply();
        return curveTexture;
    }

    /*
     * This function bakes the biome color gradients into a texture that can be used by the
     * terrain shader
     */

    public Texture2D BakeBiomes()
    {
        Texture2D curveTexture = new Texture2D(GPUCurveResolution, biomes.Length);
        for (int y = 0; y < biomes.Length; y++)
        {
            for (int x = 0; x < curveTexture.width; x++)
            {
                Color gradientValue = biomes[y].color.Evaluate((float)x / (float)curveTexture.width);
                curveTexture.SetPixel(x, y, gradientValue);
            }
        }
        curveTexture.Apply();
        return curveTexture;
    }

    /*
     * This function generates a square of chunks within the editor
     */

    public void CreateWorld()
    {
        Awake();
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
     * This function takes the data that the terrain shader needs from a noise map
     * and puts it into a GPUMapSettings object
     */

    GPUMapSettings ConvertToGPUSettings(MapSettings settings)
    {
        GPUMapSettings mapSettings = new GPUMapSettings();
        Octave[] octaves = settings.getOctaveArray();
        mapSettings.Scale = settings.scale * worldScale;
        mapSettings.Offset = settings.offset;
        mapSettings.OctaveCount = octaves.Length;
        return mapSettings;
    }
    
    /*
     * This function initializes the terrain shader
     * Relevant information: https://gamedev.stackexchange.com/questions/71014/passing-input-to-compute-shader
     */

    void InitializeTerrainShader()
    {
        // Gets the indices of the two kernels

        int kernel = terrainShader.FindKernel("HeightMap");
        int jitterKernel = terrainShader.FindKernel("JitterPoints");

        // Sets all of the constants in the shader

        terrainShader.SetFloats("Offset", heightMapSettings.offset.x, heightMapSettings.offset.y);
        terrainShader.SetFloat("Scale", heightMapSettings.scale * worldScale);
        terrainShader.SetFloat("Size", chunkResolution);
        terrainShader.SetFloat("Resolution", chunkResolution + 2);
        terrainShader.SetFloat("CurveResolution", GPUCurveResolution);
        terrainShader.SetBool("DrawBiomes", drawBiomes);
        terrainShader.SetBool("BiomeBlending", useBiomeBlending);
        terrainShader.SetFloat("BiomeBlendRadius", biomeBlendRange);
        terrainShader.SetBool("BiomeHeights", useBiomeHeights);

        // Initializes the output buffer and passes it into the shader

        outputBuffer = new ComputeBuffer(1, sizeof(int) + sizeof(float));
        Output[] outputs = new Output[1];
        outputs[0].averageElevation = 0.5f;
        terrainShader.SetBuffer(jitterKernel, "OutputBuffer", outputBuffer);

        // Initializes a buffer with all of the biomes and passes it into the shader

        GPUBiome[] GPUBiomes = new GPUBiome[biomes.Length];
        for (int i = 0; i < GPUBiomes.Length; i++)
        {
            GPUBiomes[i] = new GPUBiome(biomes[i].temperature, biomes[i].moisture);
        }
        biomeBuffer = new ComputeBuffer(biomes.Length, sizeof(float) * 2);
        biomeBuffer.SetData(GPUBiomes);
        terrainShader.SetBuffer(kernel, "Biomes", biomeBuffer);
        terrainShader.SetBuffer(jitterKernel, "Biomes", biomeBuffer);
        terrainShader.SetInt("BiomeLength", biomes.Length);

        // Passes the octave buffers for all of the noise maps into the shader

        GPUSetMapSettings(kernel, temperatureMapSettings, 1);
        GPUSetMapSettings(kernel, moistureMapSettings, 2);
        GPUSetMapSettings(kernel, biomeBlendMapSettings, 3);
        GPUSetMapSettings(jitterKernel, temperatureMapSettings, 1);
        GPUSetMapSettings(jitterKernel, moistureMapSettings, 2);
        GPUSetMapSettings(jitterKernel, jitterMapSettings, 4);
        GPUSetMapSettings(kernel, heightMapSettings, 0);

        // Passes the noisemaps into the shader

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

        // Bakes all of the curves and gradients for the noisemaps and biomes and passes them into the shader

        heightCurveTexture = BakeMapCurves();
        biomeGradientTexture = BakeBiomes();
        biomeCurveTexture = BakeBiomeCurves();
        terrainShader.SetTexture(kernel, "HeightCurveTexture", heightCurveTexture);
        terrainShader.SetTexture(jitterKernel, "HeightCurveTexture", heightCurveTexture);
        terrainShader.SetTexture(kernel, "BiomeGradientTexture", biomeGradientTexture);
        terrainShader.SetTexture(kernel, "BiomeCurveTexture", biomeCurveTexture);
    }

    /*
     * This function calculates the jittered points that will be used for the terrain shader
     * For more information, see these resources:
     *
     * https://noiseposti.ng/posts/2021-03-13-Fast-Biome-Blending-Without-Squareness.html
     * https://gamedev.stackexchange.com/questions/191685/problems-blending-biomes-together-in-relation-to-moisture-temperature/191702#191702
     */

    void JitterPoints(int chunkX, int chunkY)
    {
        List<Vector3> jitteredPoints = new List<Vector3>();
        float size = (float)chunkResolution / (float)(chunkResolution + 2);     // The normalized size of the chunk
        bool hasJitteredPoints = false;
        float chunkRadius = (chunkResolution / 2) * Mathf.Sqrt(2);              // The radius of a circle that touches the corner of the chunk
        bool[] quadrants = new bool[8];                                         // An array to show which quadrants have points that have already been generated
        Dictionary<Vector2, Vector3> calculatedPointGrid;

        /*
         * Using the jittered point dictionary is not a supported feature yet
         * because it is extremely buggy and has way too much overhead in it's current form
         *
         * TODO: Rework the jittered point dictionary functionality
         */

        if (_useJitteredPointDictionary)
        {
            // If this chunk has an entry in the calculated points dictionary, then that means there are some points here
            // that have already been calculated by another chunk

            hasJitteredPoints = calculatedPointsPerChunk.ContainsKey(new Vector2(chunkX, chunkY));
            if (hasJitteredPoints)
            {
                // Add the points that have already been calculated in this chunk to the jittered point list
                calculatedPointGrid = calculatedPointsPerChunk[new Vector2(chunkX, chunkY)];
                foreach (Vector3 point in calculatedPointGrid.Values)
                {
                    jitteredPoints.Add(point);

                    // See which quadrent of the chunk the point was in. That area has already been calculated and shouldn't
                    // be calculated again
                    
                    quadrants[0] = quadrants[0] || (point.x <= biomeBlendRange && point.y <= biomeBlendRange);
                    quadrants[1] = quadrants[1] || (point.x > biomeBlendRange && point.x < 1 - biomeBlendRange && point.y <= biomeBlendRange);
                    quadrants[2] = quadrants[2] || (point.x >= 1 - biomeBlendRange && point.y <= biomeBlendRange);
                    quadrants[3] = quadrants[3] || (point.x >= 1 - biomeBlendRange && point.y > biomeBlendRange && point.y < 1 - biomeBlendRange);
                    quadrants[4] = quadrants[4] || (point.x >= 1 - biomeBlendRange && point.y >= 1 - biomeBlendRange);
                    quadrants[5] = quadrants[5] || (point.x > biomeBlendRange && point.x < 1 - biomeBlendRange && point.y >= 1 - biomeBlendRange);
                    quadrants[6] = quadrants[6] || (point.x <= biomeBlendRange && point.y >= 1 - biomeBlendRange);
                    quadrants[7] = quadrants[7] || (point.x <= biomeBlendRange && point.x < 1 - biomeBlendRange && point.y >= 1 - biomeBlendRange);
                }
            }
        }

        // Iterate through and generate a grid of points to pass into the shader

        for (float y = -biomeBlendRange * chunkResolution; y <= (1 + biomeBlendRange) * chunkResolution; y += chunkResolution / 16 * biomeBlendLevel)
        {
            for (float x = -biomeBlendRange * chunkResolution; x <= (1 + biomeBlendRange) * chunkResolution; x += chunkResolution / 16 * biomeBlendLevel)
            {
                if (_useJitteredPointDictionary && hasJitteredPoints)
                {
                    // If this point is in a quadrant that has already been calculated, then don't generate it again
                    float scaledX = (float)x / chunkResolution;
                    float scaledY = (float)y / chunkResolution;
                    if(quadrants[0] && (scaledX <= biomeBlendRange && scaledY <= biomeBlendRange)) { continue; }
                    if(quadrants[1] && (scaledX > biomeBlendRange && scaledX < 1 - biomeBlendRange && scaledY <= biomeBlendRange)) { continue; }
                    if(quadrants[2] && (scaledX >= 1 - biomeBlendRange && scaledY <= biomeBlendRange)) { continue; }
                    if(quadrants[3] && (scaledX >= 1 - biomeBlendRange && scaledY > biomeBlendRange && scaledY < 1 - biomeBlendRange)) { continue; }
                    if(quadrants[4] && (scaledX >= 1 - biomeBlendRange && scaledY >= 1 - biomeBlendRange)) { continue; }
                    if(quadrants[5] && (scaledX > biomeBlendRange && scaledX < 1 - biomeBlendRange && scaledY >= 1 - biomeBlendRange)) { continue; }
                    if(quadrants[6] && (scaledX <= biomeBlendRange && scaledY >= 1 - biomeBlendRange)) { continue; }
                    if(quadrants[7] && (scaledX <= biomeBlendRange && scaledX < 1 - biomeBlendRange && scaledY >= 1 - biomeBlendRange)) { continue; }
                }
                jitteredPoints.Add(new Vector3(x, y, 0.1f));
            }
        }

        // Create a jittered point buffer and pass it into the shader

        Vector3[] jitteredPointArray = jitteredPoints.ToArray();
        jitteredPointBuffer = new ComputeBuffer(jitteredPointArray.Length, sizeof(float) * 3);
        jitteredPointBuffer.SetData(jitteredPointArray);
        terrainShader.SetBuffer(terrainShader.FindKernel("JitterPoints"), "jitteredPoints", jitteredPointBuffer);

        // Create an output buffer and pass it intot the shader
        Output[] outputArray = new Output[1];
        outputArray[0] = new Output(-2, 0);
        outputBuffer.SetData(outputArray);
        terrainShader.SetBuffer(terrainShader.FindKernel("JitterPoints"), "OutputBuffer", outputBuffer);

        // Pass the length of the jittered points buffer into the shader

        terrainShader.SetInt("jitteredPointsLength", jitteredPointArray.Length);

        // Dispatch the shader and have it jitter the points
        terrainShader.Dispatch(terrainShader.FindKernel("JitterPoints"), (int) (jitteredPointArray.Length / 10) + 1, 1, 1);

        // Copy the jittered point and output buffers from the JitterPoints kernel to the HeightMap kernel

        terrainShader.SetBuffer(terrainShader.FindKernel("HeightMap"), "jitteredPoints", jitteredPointBuffer);
        terrainShader.SetBuffer(terrainShader.FindKernel("HeightMap"), "OutputBuffer", outputBuffer);

        // Get the points from the jittered point buffer and add them to the list for the Gizmo tool to use

        Vector3[] nJitteredPointArray = new Vector3[jitteredPointArray.Length];
        jitteredPointBuffer.GetData(nJitteredPointArray);
        biomeData = new float[biomes.Length];
        foreach (Vector3 point in nJitteredPointArray)
        {
            biomeData[(int)point.z] += 1/(float)nJitteredPointArray.Length;
            if (drawJitteredPoints)
            {
                allPoints.Add(new Vector4((point.x + chunkX) * chunkSize, 50, (point.y + chunkY) * chunkSize, chunkY));
            }
        }
        
        // Now to store the calculated points

        if (_useJitteredPointDictionary)
        {
            // Get the points from the jittered point buffer into a new array
            Vector3[] newJitteredPointArray = new Vector3[jitteredPointArray.Length];
            jitteredPointBuffer.GetData(newJitteredPointArray);

            Vector2 chunkPosition = new Vector2(chunkX, chunkY);

            for (int i = 0; i < newJitteredPointArray.Length; i++)
            {
                if ((newJitteredPointArray[i].x > biomeBlendRange) && (newJitteredPointArray[i].x < 1 - biomeBlendRange) && (newJitteredPointArray[i].y > biomeBlendRange)
                    && (newJitteredPointArray[i].y < 1 - biomeBlendRange)) {
                    
                    // If the point is in the center of the chunk then it will be useless to other chunks, so just continue

                    continue;
                }

                // Now we iterate over all of the different quadrants to see which adjacent chunks the point can be a part of

                for (int dir = 0; dir < 8; dir++)
                {
                    if ((cosValues[dir] == 0 || (cosValues[dir] < 0 && newJitteredPointArray[i].x <= biomeBlendRange) || (cosValues[dir] > 0 && newJitteredPointArray[i].x >= 1 - biomeBlendRange))
                        && (sinValues[dir] == 0 || (sinValues[dir] < 0 && newJitteredPointArray[i].y <= biomeBlendRange) || (sinValues[dir] > 0 && newJitteredPointArray[i].y >= 1 - biomeBlendRange))) {
                        
                        // This terrible if statement just sees if the point is part of the quadrant we are looking at.
                        // See the cosValues and sinValues table above in the private properties section.
                        // I thought it would be faster to just hardcode the values and loop through them

                        // Shift the position and chunk position over so that the point is now in the adjacent chunk

                        Vector2 newChunkPosition = new Vector2(chunkPosition.x + cosValues[dir], chunkPosition.y + sinValues[dir]);
                        Vector3 oPoint = jitteredPointArray[i];
                        Vector3 newPoint = new Vector3(newJitteredPointArray[i].x - (1 * cosValues[dir]), newJitteredPointArray[i].y - (1 * sinValues[dir]), newJitteredPointArray[i].z);

                        // Make sure the point isn't somehow already in that chunk's point dictionary

                        if (chunkDictionary.ContainsKey(newChunkPosition))
                        {
                            continue;
                        }

                        if (calculatedPointsPerChunk.ContainsKey(newChunkPosition))
                        {
                            // If the adjacent chunk's entry in the dictionary already exists then just add the point there

                            Vector2 oldPoint = new Vector2(jitteredPointArray[i].x - (size * cosValues[dir]), jitteredPointArray[i].y - (size * sinValues[dir]));
                            if (!calculatedPointsPerChunk[newChunkPosition].ContainsKey(oldPoint))
                            {
                                calculatedPointsPerChunk[newChunkPosition].Add(oldPoint, newPoint);
                            }
                        }
                        else
                        {
                            // Otherwise make the entry here and add the point

                            Dictionary<Vector2, Vector3> calculatedPoints = new Dictionary<Vector2, Vector3>();
                            Vector2 oldPoint = new Vector2(jitteredPointArray[i].x - (size * cosValues[dir]), jitteredPointArray[i].y - (size * sinValues[dir]));
                            calculatedPoints.Add(oldPoint, newPoint);
                            calculatedPointsPerChunk.Add(newChunkPosition, calculatedPoints);

                        }
                    }
                }
            }

            // Once a chunk has been generated it's entry in the dictionary can be safely removed

            calculatedPointsPerChunk.Remove(chunkPosition);
        }
    }

    // This little tool just visualizes the jittered points, and I put a little debug thing in to differentiate the chunks
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

    /*
     * This function sets the octave buffer for a noisemap in the shader
     */

    void GPUSetMapSettings(int kernel, MapSettings settings, int id)
    {
        // Initialize the octave buffer

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

        // It should be noted that Unity spews warnings when these octave buffers aren't disposed,
        // but they are needed at all times and closing them causes the game to crash, so we are at an impasse
        
        // TODO: Figure out how to close the octave buffers without crashing the game
    }

    /*
     * This function will close all of the buffers used for the program,
     * but right now if it is called while the game is running everything will break
     */
    void DisposeBuffers()
    {
        octaveBuffer.Dispose();
        biomeBuffer.Dispose();
        mapBuffer.Dispose();
        jitteredPointBuffer.Dispose();
        outputBuffer.Dispose();
    }

    /*
     * This comparer sorts the biomes in their list by temperature and moisture
     */

    int CompareBiomes(Biome x, Biome y)
    {
        if (x.temperature == y.temperature)
        {
            if (x.moisture == y.moisture)
            {
                return 0;
            } else if (x.moisture > y.moisture)
            {
                return 1;
            } else
            {
                return -1;
            }
        } else if (x.temperature > y.temperature)
        {
            return 1;
        } else
        {
            return -1;
        }
    }
}
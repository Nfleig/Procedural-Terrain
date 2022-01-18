using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float MovementSpeed;
    public float ZoomSpeed;

    public float MinZoomDistance;
    public float MaxZoomDistance;

    public float XRotateThreshold;
    public int VisibleChunks;
    public bool screenSaver;
    private static WorldGenerator worldGen;
    private static GameManager gameManager;

    private Camera camera;
    private bool isClicking;
    private bool start = false;
    private bool firstFrame = true;

    private float rotX = 0;

    // Start is called before the first frame update
    void Awake()
    {
        camera = Camera.main;
        worldGen = GameObject.FindWithTag("World Generator").GetComponent<WorldGenerator>();
        gameManager = GameObject.FindWithTag("GameController").GetComponent<GameManager>();
    }


    // Update is called once per frame
    void Update()
    {
        if (gameManager.fogOfWar)
        {
            if (firstFrame)
            {
                firstFrame = false;
                Chunk starterChunk = worldGen.GPUGenerateChunk(0, 0);
                worldGen.chunkDictionary.Add(new Vector2(0, 0), starterChunk);
                starterChunk.Settle();
            }
        }
        GenerateChunks();
        if (start || !worldGen.IsGenerating())
        {
            start = true;
        } else
        {
            return;
        }
        float xInput = Input.GetAxis("Horizontal");
        float zInput = Input.GetAxis("Vertical");
        if (Input.GetButtonDown("Fire2")){
            isClicking = true;
        }
        if(Input.GetButtonUp("Fire2")){
            isClicking = false;
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
        Vector3 dir = transform.forward * zInput + transform.right * xInput;
        if (screenSaver && start)
        {
            dir = new Vector3(1, 0, 1);
        }
        transform.position += dir * MovementSpeed * Time.deltaTime;
        if(isClicking){
            float rotY = Input.GetAxis("Mouse X") * 5;
            rotX += Input.GetAxis("Mouse Y") * 5;
            rotX = Mathf.Clamp(rotX, -XRotateThreshold, XRotateThreshold);
            //print(rotX);
            transform.eulerAngles = new Vector3(0, transform.eulerAngles.y + rotY, 0);
            camera.transform.eulerAngles = new Vector3(50 - rotX, camera.transform.eulerAngles.y, 0);
        }
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        float distance = Vector3.Distance(transform.position, camera.transform.position);
        if(!((distance < MinZoomDistance && scroll > 0f) || (distance > MaxZoomDistance && scroll < 0f))){
            camera.transform.position += camera.transform.forward * scroll * 5;
        }

    }

    int[] cosValues = { 1, 0, -1, 0, 1, -1, -1, 1 };
    int[] sinValues = { 0, 1, 0, -1, 1, 1, -1, -1 };
    public void RevealChunks(Vector2 position)
    {
        for (int dir = 0; dir < 4; dir++)
        {
            Vector2 newPosition = new Vector2(position.x + cosValues[dir], position.y + sinValues[dir]);
            if (!worldGen.chunkDictionary.ContainsKey(newPosition))
            {
                ShowChunks(newPosition, 0);
            }
        }
    }

    public void ShowChunks(Vector2 position, int depth)
    {
        ChunkGenerator newChunkGenerator;
        if (!worldGen.chunkDictionary.ContainsKey(position))
        {
            Chunk newChunk = worldGen.GPUGenerateChunk((int)position.x, (int)position.y);
            worldGen.chunkDictionary.Add(position, newChunk);
            newChunkGenerator = newChunk.GetComponent<ChunkGenerator>();
        } else
        {
            newChunkGenerator = worldGen.chunkDictionary[position].GetComponent<ChunkGenerator>();
        }
        if (depth >= 10 || (newChunkGenerator.averageElevation > 0.05f && depth != 0))
        {
            return;
        }
        for (int dir = 0; dir < 4; dir++)
        {
            Vector2 newPosition = new Vector2(position.x + cosValues[dir], position.y + sinValues[dir]);
            ShowChunks(newPosition, depth + 1);
        }
    }

    void GenerateChunks()
    {

        int chunkCoordX = Mathf.RoundToInt(transform.position.x / worldGen.chunkSize);
        int chunkCoordY = Mathf.RoundToInt(transform.position.z / worldGen.chunkSize);

        Chunk[] allChunks = (Chunk[])GameObject.FindObjectsOfType(typeof(Chunk));
        foreach (Chunk chunk in allChunks)
        {
            if (Mathf.Abs(chunk.position.x - (float) chunkCoordX) > VisibleChunks || Mathf.Abs(chunk.position.y - (float)chunkCoordY) > VisibleChunks)
            {
                chunk.Unload();
                if (screenSaver)
                {
                    worldGen.chunkDictionary.Remove(chunk.position);
                    Destroy(chunk.gameObject);
                }
            }
        }
        for (int x = -VisibleChunks; x < VisibleChunks; x++)
        {
            for (int y = -VisibleChunks; y < VisibleChunks; y++)
            {
                Vector2 chunkCoord = new Vector2(chunkCoordX + x, chunkCoordY + y);
                if (worldGen.chunkDictionary.ContainsKey(chunkCoord))
                {
                    if (!worldGen.chunkDictionary[chunkCoord].isLoaded())
                    {

                        worldGen.chunkDictionary[chunkCoord].Load();
                    }
                }
                else
                {
                    if (!gameManager.fogOfWar)
                    {
                        //print(chunkCoord);
                        Chunk newChunk = worldGen.GPUGenerateChunk((int)chunkCoord.x, (int)chunkCoord.y);
                        worldGen.chunkDictionary.Add(chunkCoord, newChunk);
                        //print(chunkCoordX + " " + chunkCoordY + " " + chunkCoord);
                    }

                }
            }
        }
    }
}

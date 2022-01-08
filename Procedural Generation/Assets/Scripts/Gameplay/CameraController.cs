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
    public WorldGenerator worldGen;

    private Camera camera;
    private bool isClicking;
    private bool start = false;

    private float rotX = 0;

    // Start is called before the first frame update
    void Awake()
    {
        camera = Camera.main;
    }


    // Update is called once per frame
    void Update()
    {
        GenerateChunks();
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
        if (start || !worldGen.IsGenerating())
        {
            start = true;
        }
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
                    worldGen.chunkDictionary[chunkCoord].Load();
                }
                else
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

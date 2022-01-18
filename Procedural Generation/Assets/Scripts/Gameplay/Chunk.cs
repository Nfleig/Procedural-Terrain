using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    public GameObject houseObject;
    private MeshRenderer renderer;
    private ChunkGenerator generator;
    private static GameManager gameManager;
    private static CameraController camera;
    public Vector2 position;
    public bool hovering = false;
    public bool selected = false;
    public bool claimed = false;
    private bool loaded;


    private void Awake()
    {
        gameManager = GameObject.FindWithTag("GameController").GetComponent<GameManager>();
        camera = GameObject.FindWithTag("Player").GetComponent<CameraController>();
        loaded = true;
    }

    // Start is called before the first frame update
    void Start()
    {
        renderer = GetComponent<MeshRenderer>();
        generator = GetComponent<ChunkGenerator>();
        //position = new Vector2(transform.position.x / 8, transform.position.y / 8);
    }

    public bool isLoaded()
    {
        return loaded;
    }

    public void Load()
    {
        renderer.enabled = true;
        loaded = true;
    }

    public void Unload()
    {
        renderer.enabled = false;
        loaded = false;
    }

    public void Settle()
    {
        claimed = true;
        SpawnHouses();
        Deselect();
        if (gameManager.fogOfWar)
        {
            camera.ShowChunks(position, 0);
        }
    }

    public void SpawnHouses()
    {
        GameObject house = Instantiate(houseObject, transform);
        house.transform.localPosition = new Vector3(64, 50, 64);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (!hovering && selected)
            {
                Deselect();
            }
        }
    }

    public void Select()
    {
        if (claimed)
        {
            transform.position = new Vector3(transform.position.x, 5, transform.position.z);
        } else
        {
            Settle();
        }
    }

    private void Deselect()
    {
        transform.position = new Vector3(transform.position.x, 0, transform.position.z);
        selected = false;
    }

    private void OnMouseDown()
    {
        selected = !selected;
        if (selected)
        {
            Select();
        } else
        {
            Deselect();
        }
    }

    private void OnMouseOver()
    {
        if (!hovering)
        {
            transform.position = new Vector3(transform.position.x, 3, transform.position.z);
            hovering = true;
        }
    }

    private void OnMouseExit()
    {
        hovering = false;
        transform.position = new Vector3(transform.position.x, 0, transform.position.z);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    public GameObject houseObject;
    public float spawnSpeed;
    private MeshRenderer renderer;
    private ChunkGenerator generator;
    private static GameManager gameManager;
    private static CameraController camera;
    private Transform _parent;
    private Animator _animator;
    public Vector2 position;
    public bool hovering;
    public bool selected;
    public bool claimed;
    public bool habitable;
    private bool loaded;
    public bool ignoreInput;


    private void Awake()
    {
        gameManager = GameObject.FindWithTag("GameController").GetComponent<GameManager>();
        camera = GameObject.FindWithTag("Player").GetComponent<CameraController>();
        _animator = GetComponent<Animator>();
        _parent = transform.parent;
        loaded = true;
    }

    // Start is called before the first frame update
    void Start()
    {
        renderer = GetComponent<MeshRenderer>();
        generator = GetComponent<ChunkGenerator>();
    }

    public ChunkGenerator GetGenerator()
    {
        return generator;
    }

    public bool isLoaded()
    {
        return loaded;
    }

    public void Load()
    {
        _animator.SetTrigger("Place");
        renderer.enabled = true;
        loaded = true;
    }

    public void Unload()
    {
        _animator.SetTrigger("Remove");
        Invoke("HideObject", 0.5f);
        loaded = false;
    }

    void HideObject() {
        renderer.enabled = false;
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
        house.transform.localPosition = new Vector3(16, 5, 16);
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
        _animator.SetBool("Hovered", hovering);
        _animator.SetBool("Selected", selected);
    }


    public void Select()
    {
        if (claimed)
        {
            // Chunk cannot be settled
        } else
        {
            //Settle();
        }
    }

    private void Deselect()
    {
        selected = false;
    }

    private void OnMouseDown()
    {
        if (ignoreInput) {
            return;
        }

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
        if (ignoreInput) {
            return;
        }

        if (!hovering)
        {
            hovering = true;
        }
    }

    private void OnMouseExit()
    {
        if (ignoreInput) {
            return;
        }
        
        hovering = false;
        selected = false;
    }

}

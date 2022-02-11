using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChunkInfoPanel : MonoBehaviour
{
    public Text infoText;
    private CanvasRenderer renderer;

    private void Awake()
    {
        renderer = GetComponent<CanvasRenderer>();
    }

    public void ShowChunkInfo(Chunk chunk)
    {

    }
}

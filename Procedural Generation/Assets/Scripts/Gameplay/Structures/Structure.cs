using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Structure : MonoBehaviour
{
    public enum StructureType { MILITARY, ECONOMIC, RESOURCE, INFRASTRUCTURE}

    public StructureType type;
    public int level;
    public float health;
}

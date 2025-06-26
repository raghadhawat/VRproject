using UnityEngine;
using System.Collections.Generic;

public class BoxNode
{
    public Vector3 Center;
    public Vector3 Size;
    public BoxNode(Vector3 center, Vector3 size)
    {
        Center = center;
        Size = size;
    }
}

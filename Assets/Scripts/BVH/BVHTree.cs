using System.Collections.Generic;
using UnityEngine;

public class BVHTree
{
    public BVHNode Root;

    public void Insert(GameObject gameObject, AABB boundingBox)
    {
        var newNode = new BVHNode(boundingBox, gameObject);
        if (Root == null)
        {
            Root = newNode;
        }
        else
        {
            Root = Insert(Root, newNode);
        }
    }

    private BVHNode Insert(BVHNode node, BVHNode newNode)
    {
        if (node.IsLeaf)
        {
            var newParent = new BVHNode(AABB.Union(node.BoundingBox, newNode.BoundingBox));
            newParent.Left = node;
            newParent.Right = newNode;
            return newParent;
        }

        float leftSurfaceArea = AABB.Union(node.Left.BoundingBox, newNode.BoundingBox).SurfaceArea();
        float rightSurfaceArea = AABB.Union(node.Right.BoundingBox, newNode.BoundingBox).SurfaceArea();

        if (leftSurfaceArea < rightSurfaceArea)
        {
            node.Left = Insert(node.Left, newNode);
        }
        else
        {
            node.Right = Insert(node.Right, newNode);
        }

        node.BoundingBox = AABB.Union(node.Left.BoundingBox, node.Right.BoundingBox);
        return node;
    }

    public List<GameObject> Query(AABB queryBox)
    {
        List<GameObject> result = new List<GameObject>();
        Query(Root, queryBox, result);
        return result;
    }

    private void Query(BVHNode node, AABB queryBox, List<GameObject> result)
    {
        if (node == null || !node.BoundingBox.Intersects(queryBox))
        {
            return;
        }

        if (node.IsLeaf)
        {
            result.Add(node.GameObject);
        }
        else
        {
            Query(node.Left, queryBox, result);
            Query(node.Right, queryBox, result);
        }
    }
}

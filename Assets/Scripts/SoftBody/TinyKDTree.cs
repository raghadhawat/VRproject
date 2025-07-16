using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Minimal 3‑D k‑d tree optimised for up‑to‑k‑nearest (k ≤ 3) queries.
/// Build cost ≈ O(N log N), query cost ≈ O(log N + k).
/// Works with Unity's Burst when placed in an asmdef that enables it.
/// </summary>
public sealed class TinyKDTree
{
    private struct Node
    {
        public int   index;   // original particle index
        public Vector3 pos;   // position in world space
        public int   left;    // index of left child in the nodes array (‑1 if none)
        public int   right;   // index of right child (‑1 if none)
    }

    private Node[] nodes; // flat array representing the tree

    /// <summary>
    /// Build from a list of (position, particleIndex) pairs.
    /// </summary>
    public TinyKDTree(List<(Vector3 pos, int index)> points)
    {
        if (points == null || points.Count == 0)
            throw new ArgumentException("Point list must be non‑empty");

        // copy to mutable list because we'll sort in‑place per recursion level
        var pts = new List<(Vector3 pos, int index)>(points);
        nodes = new Node[pts.Count];
        BuildRecursive(pts, 0, pts.Count, 0, 0);
    }

    // ───────────────────────────────────────────────────────────── private helpers ──

    private int BuildRecursive(List<(Vector3 pos, int index)> pts, int start, int end, int depth, int arrayIdx)
    {
        if (start >= end) return -1;

        int axis = depth % 3;
        int mid  = (start + end) >> 1;

        // partial sort: nth‑element would be ideal, but Unity lacks it → quick Sort slice
        pts.Sort(start, end - start, Comparer<(Vector3 pos,int index)>.Create(
            (a, b) => a.pos[axis].CompareTo(b.pos[axis])));

        // ensure capacity
        if (arrayIdx >= nodes.Length)
            Array.Resize(ref nodes, arrayIdx + 32);

        var (p, idx) = pts[mid];
        nodes[arrayIdx].pos   = p;
        nodes[arrayIdx].index = idx;

        int left  = BuildRecursive(pts, start, mid, depth + 1, arrayIdx + 1);
        int right = BuildRecursive(pts, mid + 1, end, depth + 1,
                                   left == -1 ? arrayIdx + 1 : left + SubtreeSize(left));

        nodes[arrayIdx].left  = left;
        nodes[arrayIdx].right = right;
        return arrayIdx;
    }

    private int SubtreeSize(int idx)
    {
        if (idx == -1) return 0;
        return 1 + SubtreeSize(nodes[idx].left) + SubtreeSize(nodes[idx].right);
    }

    // ─────────────────────────────────────────────── public query: radial k‑nearest ──

    /// <summary>
    /// Finds up to <paramref name="k"/> nearest neighbours within radius² of <paramref name="query"/>.
    /// Writes results to <paramref name="outBuf"/> (index, squared distance) sorted ascending.
    /// Returns the number of neighbours found (≤ k).
    /// </summary>
    public int RadialKNearest(Vector3 query, float radius2, int k,
                              Span<(int index, float dist2)> outBuf)
    {
        if (k <= 0 || k > outBuf.Length)
            throw new ArgumentException("outBuf must have length ≥ k");

        // tiny fixed‑size max‑heap stored in arrays
        Span<float> dHeap = stackalloc float[k];
        Span<int>   iHeap = stackalloc int[k];
        int count = 0;     // current heap size
        float worst = 0f;  // largest distance² currently in heap

        SearchRecursive(0, query, radius2, k, ref count, ref worst, dHeap, iHeap, 0);

        // copy to caller buffer and sort
        for (int c = 0; c < count; c++)
            outBuf[c] = (iHeap[c], dHeap[c]);

        // insertion-sort the at-most-3 items (count ≤ k ≤ 3)
        for (int a = 1; a < count; a++)
        {
            var key = outBuf[a];
            int b = a - 1;
           while (b >= 0 && outBuf[b].dist2 > key.dist2)
            {
                outBuf[b + 1] = outBuf[b];
                b--;
            }
            outBuf[b + 1] = key;
        }

        return count;
    }

    private void SearchRecursive(int n, Vector3 q, float r2, int k,
                                 ref int count, ref float worst,
                                 Span<float> dHeap, Span<int> iHeap,
                                 int depth)
    {
        if (n == -1) return;

        ref Node node = ref nodes[n];
        float d2 = (node.pos - q).sqrMagnitude;
        bool inRadius = d2 <= r2;

        // maintain max‑heap of size ≤ k with the closest distances²
        if (inRadius)
        {
            if (count < k) // heap not full: append
            {
                dHeap[count] = d2;
                iHeap[count] = node.index;
                if (d2 > worst) worst = d2;
                count++;
            }
            else if (d2 < worst) // replace current worst
            {
                int wi = 0;
                for (int h = 1; h < count; h++) if (dHeap[h] > dHeap[wi]) wi = h;
                dHeap[wi] = d2;
                iHeap[wi] = node.index;
                worst = dHeap[0];
                for (int h = 1; h < count; h++) if (dHeap[h] > worst) worst = dHeap[h];
            }
        }

        int axis = depth % 3;
        float diff = q[axis] - node.pos[axis];
        int first  = diff < 0 ? node.left : node.right;
        int second = diff < 0 ? node.right : node.left;

        SearchRecursive(first, q, r2, k, ref count, ref worst, dHeap, iHeap, depth + 1);

        // Check if we need to examine the other side of the split plane.
        if (diff * diff < r2 || count < k)
            SearchRecursive(second, q, r2, k, ref count, ref worst, dHeap, iHeap, depth + 1);
    }
}

        // surface edges
        for (int i = 0; i < meshTris.Length; i+=3)
        {
            int vertex1 = surfMap[meshTris[i]], 
                vertex2 = surfMap[meshTris[i+1]], 
                vertex3 = surfMap[meshTris[i+2]];
            AddEdge(vertex1, vertex2); 
            AddEdge(vertex2, vertex3); 
            AddEdge(vertex3, vertex1);
        }
﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DualGraph2d;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine.Audio;

[RequireComponent (typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider))]
public class MeshGenerator : MonoBehaviour {

	//public BuildController buildController;
	public Material[] materials;
	public Transform cubePrefab;
	
	//public BezierSpline roadCurveL;
	//public BezierSpline roadCurveR;
	//public float roadCurveFrequency;
	//public float roadSpread = 3f;
	//public GridPlayer gridPlayer;
    private List<Vector3> roadList = new List<Vector3>();
    public Vector3 volume;//= new Vector3(20.0f,0.0f,20.0f);
	public float rootTriMultiplier=1.0f;
	public int cellNumber= 20;
    public int lloydIterations = 3;
    public float minEdgeSize = 3f;
    public bool useSortedGeneration;//=true;
	public bool drawCells=false;
	public bool drawDeluany=false;
	public bool drawRoots=false;
	public bool drawVoronoi=false;
	public bool drawGhostEdges=false;
	public bool drawPartialGhostEdge=false;
	public bool drawCircumspheres=false;
	public Color sphereColor= Color.cyan;
	

	//public DualGraph dualGraph;
//	private float totalTime;
	private float computeTime;
	private Mesh graphMesh;

	
	public bool fillWithRandom;
	public bool fillWithPoints;

    public bool extrudeCells;
    public bool walls = true;
    
    public bool weldCells = true;
    public float weldThreshold = 10f;//how wide should minimum  ledge size be?
    public bool makeSkyscraperTraditional = true;
    public bool doBuildControl;
    public float threshold = 2f;

	public List<Vector3> yardPoints = new List<Vector3>();
 //   public List<GameObject> cellsList = new List<GameObject>();
   // public List<Mesh> meshList = new List<Mesh>();
    public List<Vector3[]> meshVerts = new List<Vector3[]>();
    public List<int[]> meshTris = new List<int[]>();

    public int density = 5;

    public List<GameObject> cells = new List<GameObject>();
    //public List<List<GameObject>> adjacentCells = new List<List<GameObject>>();//moved t0 saving on each cell with AdjacentCells class
    public int adjacentCellsCount = 0;

    public int counter = 0;
    List<Vector3> centroids = new List<Vector3>();

    float tolerance =5f;
    public List<List<List<int>>> edges = new List<List<List<int>>>();//cell number//edge number//edge numbers (create struct)

    public void Start ()
    {

        tolerance = minEdgeSize;//testing


        StartCoroutine("Lloyds");



    }

    void GeneratePoints(Vector3[] points,DualGraph dualGraph)
    {
       
        //set up cells
        dualGraph.DefineCells(points, rootTriMultiplier);
    //    computeTime = Time.realtimeSinceStartup;

        //compute
        if (useSortedGeneration)
            dualGraph.ComputeForAllSortedCells();
        else
            dualGraph.ComputeForAllCells();




        //  yield return new WaitForEndOfFrame();

    
        

       // Debug.Log("coroutine");

        
    }

    IEnumerator Lloyds()
    {
        
    
       
        //for (int x = 0; x < lloydIterations; x++)
        
        //we will keep relaxing until all our edges are at least 
        bool edgeShortEnough = false;
        //int maxCounts = 50;
        int count = 0;
        List<Vector3> borderPoints = new List<Vector3>();
        while(!edgeShortEnough && count < lloydIterations)
        {
            //  Debug.Log("LastIteration =" + count);

            //destroy old cells (if any), we will create new ones on this iteration
            for (int i = 0; i < cells.Count; i++)
            {
                //extruded cell is not parented
                if (cells[i].GetComponent<ExtrudeCell>()!= null)
                    Destroy(cells[i].GetComponent<ExtrudeCell>().extrudedCell);

                Destroy(cells[i].gameObject);
            }
            


            //Go get points from Road Curve       
            DualGraph dualGraph = new DualGraph(volume);
            cells.Clear();      

            cellNumber = (int)volume.x / density;

            Vector3[] points = new Vector3[cellNumber];
            //Debug.Log(points.Length);
            if (count > 0)
                points = new Vector3[centroids.Count];

            if(count > 0)
            {
                fillWithPoints = true;
                fillWithRandom = false;
            }

            GenSortedRandCells(ref points);


           // if (count == 1)
            {
                for (int i = 0; i < points.Length; i++)
                {
                  //  GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                  //  c.transform.position = points[i];
                  //   c.name = count.ToString() + " " + i.ToString();
                }
            }



            centroids.Clear();

            dualGraph.DefineCells(points, rootTriMultiplier);
            dualGraph.ComputeForAllSortedCells();
           // dualGraph.ComputeForAllCells();

            dualGraph.PrepareCellsForMesh();
            
            //work out centroids for next iteration 
            for (int i = 0; i < dualGraph.cells.Count; i++)
            {
                if (!dualGraph.cells[i].root && !dualGraph.cells[i].IsOpenEdge())
                {                       //use only interior until faux edges are added
                    if (dualGraph.cells[i].IsOpenEdge())
                    {
                        Debug.Log("open edge");
                    }
                    Vector3 avg = Vector3.zero;
                    for (int a = 0; a < dualGraph.cells[i].mesh.verts.Length; a++)
                    {
                        if (a != 0)
                            avg += dualGraph.cells[i].mesh.verts[a];//make temp []

                    }
                    avg /= dualGraph.cells[i].mesh.verts.Length - 1;
                    centroids.Add(avg);
                }
                else
                {
                    //saving outside border points, if we dont the graph will get smaller and smaller with each lloyd iteration
                    if (( dualGraph.cells[i].IsOpenEdge() && !dualGraph.cells[i].root))
                    {
                        centroids.Add(dualGraph.cells[i].point);
                       // GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        //c.transform.position = dualGraph.cells[i].point;
                    }
                }
            }

           

            if (count == 0)
            {

                for (int i = 0; i < centroids.Count; i++)
                {
                   // GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                   // c.transform.position = centroids[i];
                  //  c.name = "c";// x.ToString() + " " + i.ToString();
                }

            }

            bool doShortestEdge = false;
            if (doShortestEdge)
            {
                //work out if we have relaxed enough
                float shortestEdgeDistance = FindShortestEdgeDistance(dualGraph);
                if (shortestEdgeDistance >= minEdgeSize)
                {
                    Debug.Log("min edge distance reached ");
                    edgeShortEnough = true;
                }
                else
                    Debug.Log("shortest distance = " + shortestEdgeDistance);
            }

            count++;



            //cells = new List<GameObject>();
            GenerateMesh(dualGraph);

            CellMeshes();
            

          //  yield return new WaitForSeconds(.5f);

           
        }
        CalculateAdjacents();
        SetPalletes();

        AddToCells();

        /*
        AddExtrudes();
        AddAdjacents();

        CalculateAdjacents();
        Edges();
        RemoveSmallEdges();
        
        



        ReMesh();
        
        //refind adjacents
        CalculateAdjacents();
        //redo edges now we have removed some
        Edges();//and shared eges
        */
        //red do the meshes using the new edges
        // yield return new WaitForEndOfFrame();
        yield break;
    }


    float FindShortestEdgeDistance(DualGraph dualGraph)
    {
        float shortestDistance = Mathf.Infinity;

        Vector3 p0;
        Vector3 p1;

        int shortestIndexI = 0;
        int shortestIndexJa = 0;
        int shortestIndexJb = 0;
        for (int i = 0; i < dualGraph.cells.Count; i++)
        {
            if (!dualGraph.cells[i].root && !dualGraph.cells[i].IsOpenEdge())
            {
                for (int j = 0; j < dualGraph.cells[i].mesh.verts.Length; j++)
                {
                    if (j == 0)
                        continue;

                    int nextIndex = j + 1;
                    //looping but skipping 0 central point
                    if (nextIndex > dualGraph.cells[i].mesh.verts.Length - 1)
                        nextIndex = 1;

                    p0 = (dualGraph.cells[i].mesh.verts[j]);
                    p1 = (dualGraph.cells[i].mesh.verts[nextIndex]);

                    /*
                    GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    c.transform.position = p0;// dualGraph.cells[shortestIndexI].mesh.verts[shortestIndexJa];
                    c.name = i.ToString() +" "+ j.ToString();// "shortest A";

                    c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    c.transform.position = p1;//dualGraph.cells[shortestIndexI].mesh.verts[shortestIndexJb];
                    c.name = c.name = i.ToString()+" " + nextIndex.ToString();
                    */

                    float d = Vector2.Distance(p0, p1);

                    if (d < shortestDistance)
                    {
                        shortestIndexI = i;
                        shortestIndexJa = j;
                        shortestIndexJb = nextIndex;
                        shortestDistance = d;
                    }
                }
            }
        }
        /*
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.transform.position = dualGraph.cells[shortestIndexI].mesh.verts[shortestIndexJa];
        c.name = shortestIndexI.ToString() + " " + shortestIndexJa.ToString();// "shortest A";

        c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.transform.position = dualGraph.cells[shortestIndexI].mesh.verts[shortestIndexJb];
        c.name = shortestIndexI.ToString() + " " + shortestIndexJb.ToString();// 
        */
        return shortestDistance;
    }



    /// <summary>
    /// Generates random cells.
    /// </summary>
    /// <param name="p">P.</param>
    /// <summary>
    /// Generates random cells.
    /// </summary>
    /// <param name="p">P.</param>
    private void GenRandCells(ref Vector3[] p){		

		List<Vector3> tempList = new List<Vector3>();

	/*	for (int i = 0; i <gridPlayer.Path.Count;i++)
		{
			Vector3 position = gridPlayer.Path[i] - transform.position;
			position.y = 0f;
			tempList.Add(position);

		}

		for(int i=0; i<p.Length - gridPlayer.Path.Count; i++){

			Vector3 position = new Vector3(UnityEngine.Random.Range(-volume.x,volume.x),0.0f,UnityEngine.Random.Range(-volume.z,volume.z));
			tempList.Add(position);
			//p[i]= new Vector3(Random.Range(-volume.x,volume.x),0.0f,Random.Range(-volume.z,volume.z));
		}

		p = tempList.ToArray();

*/
		if(fillWithPoints)
		{
			//cellNumber = yardPoints.Count;
			
			for(int i = 0; i < yardPoints.Count; i++)
			{					
				tempList.Add(yardPoints[i]);
			}

			p = tempList.ToArray();
		}
	}

    private void AddCentroids(ref Vector3[] p,List<Vector3> centroids)
    {

        List<Vector3> tempList = new List<Vector3>();

        /*	for (int i = 0; i <gridPlayer.Path.Count;i++)
            {
                Vector3 position = gridPlayer.Path[i] - transform.position;
                position.y = 0f;
                tempList.Add(position);

            }

            for(int i=0; i<p.Length - gridPlayer.Path.Count; i++){

                Vector3 position = new Vector3(UnityEngine.Random.Range(-volume.x,volume.x),0.0f,UnityEngine.Random.Range(-volume.z,volume.z));
                tempList.Add(position);
                //p[i]= new Vector3(Random.Range(-volume.x,volume.x),0.0f,Random.Range(-volume.z,volume.z));
            }

            p = tempList.ToArray();

    */
            //cellNumber = yardPoints.Count;

            for (int i = 0; i < centroids.Count; i++)
            {
                tempList.Add(centroids[i]);
            }

            p = tempList.ToArray();
        
    }

    /// <summary>
    /// Generates random cells, sorted by x value.
    /// </summary>
    /// <param name="points">Points.</param>
    //Note about sorting: using a sorted list requires the x values to always be different
    private void GenSortedRandCells(ref Vector3[] points){
		SortedList<float, Vector3> p= new SortedList<float,Vector3>();



		//adds random values for the rest
		if(fillWithRandom)
			{
			for(int i=0; i<cellNumber - roadList.Count; i++){
				Vector3 v = new Vector3(UnityEngine.Random.Range(-volume.x,volume.x),0.0f,UnityEngine.Random.Range(-volume.z,volume.z));
				try{
					p.Add(v.x, v);

				
				}
				catch(System.ArgumentException){
					i--;
					//Debug.Log("sort conflict");
				}
			}
			p.Values.CopyTo(points,0);
		}

		if(fillWithPoints)
		{
			//cellNumber = yardPoints.Count;

			for(int i = 0; i < centroids.Count; i++)
			{
				try{
					p.Add(centroids[i].x, centroids[i]);
				}
				catch(System.ArgumentException)
				{

					Array.Resize(ref points,points.Length-1);
					cellNumber-=1;
				}
			}
			p.Values.CopyTo(points,0);
		}
	}
	/// <summary>
	/// Generates the mesh.
	/// </summary>
	void GenerateMesh(DualGraph dualGraph)
    {
    //    Debug.Log("prepare cells for mesh start");
        dualGraph.PrepareCellsForMesh();
        //yield return new WaitForEndOfFrame();
     //   Debug.Log("prepare cells for mesh end");
		if (graphMesh==null){
			graphMesh= new Mesh();
			graphMesh.name= "Graph Mesh";
		}
		else{
			//For the love of god, why are you calling this twice?!?!
			graphMesh.Clear();
		}

        meshVerts.Clear();
        meshTris.Clear();

	//	List<Vector3> vert= new List<Vector3>();
	//	List<Vector2> uvs= new List<Vector2>();
	//	List<int> tris= new List<int>();
	//	int vertCount=0;

	//	foreach(Cell c in dualGraph.cells)
     //   {
        for(int i = 0; i < dualGraph.cells.Count; i++)
        {
            //bottleneck protection
           // if(i!=0 && i % 100 == 0)
            //    yield return new WaitForEndOfFrame();




            List<Vector3> vert= new List<Vector3>();
			List<Vector2> uvs= new List<Vector2>();
			List<int> tris= new List<int>();
			int vertCount=0;
			if(!dualGraph.cells[i].root && !dualGraph.cells[i].IsOpenEdge()){						//use only interior until faux edges are added
				if(dualGraph.cells[i].IsOpenEdge()){
					Debug.Log("open edge");
				}

                for (int a = 0; a < dualGraph.cells[i].mesh.verts.Length; a++)
                {

                    //Debug.Log("in verts");
                    vert.Add(dualGraph.cells[i].mesh.verts[a] + transform.position);

                   // GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                   // c.transform.position= dualGraph.cells[i].mesh.verts[a];
                   // c.name = a.ToString();

                }
                foreach (Vector2 v in dualGraph.cells[i].mesh.uv){
					uvs.Add(v);
                   // Debug.Log("in uv");
                }

				for(int j = 2; j < dualGraph.cells[i].mesh.verts.Length; j++){
					tris.Add(vertCount);
					tris.Add(vertCount + j - 1);
					tris.Add(vertCount + j);
				}

				//finishing the loop
				tris.Add(vertCount);
				tris.Add(vertCount+ dualGraph.cells[i].mesh.verts.Length-1);
				tris.Add(vertCount+1);

				vertCount=vert.Count;
			}
			//Check for empty meshes and skip
			if (vert.Count == 0) continue;

			///Export to individual GameObject
		//	GameObject cell = new GameObject();

            
            //add mesh info to lists to crate mesh in a coroutine and drip feed in to unity
            //Mesh mesh = new Mesh();
		//	mesh.vertices = vert.ToArray();
        //    mesh.triangles = tris.ToArray();
          
            meshVerts.Add(vert.ToArray());
            meshTris.Add(tris.ToArray());       
    
            
		}

        //StartCoroutine("AddToCells");

       
    }

    void CellMeshes()
    {
        for (int i = 0; i < meshVerts.Count; i++)
        {
            if (meshVerts.Count == 0)
                continue;

            //create a game object for each cell in the mesh list
            GameObject cell = new GameObject();
            cell.transform.parent = this.gameObject.transform;
            cell.name = "Cell";
            
            // cell.tag = "Cell";


            //create a mesh from the already populated lists
            Mesh mesh = new Mesh();
            mesh.vertices = meshVerts[i];
            mesh.triangles = meshTris[i];
            mesh.RecalculateNormals();


            MeshFilter meshFilter = cell.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;


            MeshRenderer meshRenderer = cell.AddComponent<MeshRenderer>();
            meshRenderer.enabled = false;


            meshRenderer.sharedMaterial = Resources.Load("Ground") as Material;



            //bottleneck protection, build a 100 at a time
            //  if (i != 0 && i % 100 == 0)
            //       yield return new WaitForEndOfFrame();


            //master list of cells
            cells.Add(cell);


        }
    }

    void AddToCells()
    {
        GetComponent<BuildControl>().cells.Clear();

        for (int i = 0; i < cells.Count; i++)
        {
           // meshRenderer.sharedMaterial = Resources.Load("White") as Material;

            if (makeSkyscraperTraditional)
            {
                cells[i].AddComponent<TraditionalSkyscraper>();

                //add to build control list
                GetComponent<BuildControl>().cells.Add(cells[i]);
                //  cell.GetComponent<MeshRenderer>().enabled = false;


            }
        }


        //now we have found adjacents, we can scale cells
        for (int i = 0; i < cells.Count; i++)
        { //set layer here
            cells[i].layer = LayerMask.NameToLayer("Cells");
            if (extrudeCells)
            {
                ExtrudeCell ex = cells[i].AddComponent<ExtrudeCell>();
                ex.uniqueVertices = true;
                //call start straight away
                ex.Start();
            }
        }

      
        if (doBuildControl)
            GetComponent<BuildControl>().enabled = true;

        // yield break;
    }

    void CalculateAdjacents()
    {
        //work out which cells are adjacent to each cell, save in a list
        for (int i = 0; i < cells.Count; i++)
        {
            //set layer here
            cells[i].layer = LayerMask.NameToLayer("Cells");

            List<GameObject> adjacents = new List<GameObject>();

            Vector3[] thisVertices = cells[i].GetComponent<MeshFilter>().mesh.vertices;
            for (int j = 0; j < cells.Count; j++)
            {
                //don't check own cell
                if (i == j)
                    continue;

                Vector3[] otherVertices = cells[j].GetComponent<MeshFilter>().mesh.vertices;
                int matches = 0;

                for (int a = 0; a < thisVertices.Length; a++)
                {
                    for (int b = 0; b < otherVertices.Length; b++)
                    {
                        //if we have a match, add "other" cell to a list of adjacents for this cell
                        if (Vector3.Distance(thisVertices[a], otherVertices[b]) < tolerance) //opt0- think this is ok as ==
                        {
                            //adjacents.Add(cells[j]); //making so we need two points for an adjacent cell

                            //force out of the loops
                            //a = thisVertices.Length;


                            matches++;
                        }
                    }
                }

                if (matches > 1)//means if cell mathces one ponton a corner, we ignore. it has to be a solid edge
                    adjacents.Add(cells[j]);
            }

            //adjacentCells.Add(adjacents); //removing
            //add to list and save it on game object. Doing it this way allows us to hot reload, if we save it all in a list here, it won't serialize

            AdjacentCells aj = cells[i].AddComponent<AdjacentCells>();
            aj.adjacentCells = adjacents;
            
        }
    }

    void SetPalletes()
    {
        //choose a starting main colour randomly

        float hue = 0f;
        bool strictColours = false;//**
        if (strictColours)
        {
            float hueChooser = 30 * UnityEngine.Random.Range(0, 12);
            hue = (1f / 360) * hueChooser;
        }
        else if (!strictColours)
        {
            //or use this to create non standard palettes - can movd away from main colours
            hue = 1f / 360 * (UnityEngine.Random.Range(0f, 360f));
        }

        

        //randomly choose saturation - will aplly to all cells
        float satChooser = UnityEngine.Random.Range(.5f, .8f);//still playin with these//and whether top put inside loop or not -- keeping outside of loop, tints and shades are there to get different tones
        float saturation = satChooser;

        //changing value makes it look darker or light-almost liekthe lighting engine does. keeping static
        float value = 1f;// UnityEngine.Random.Range(.5f, 1f);// 0.5f;

        //steal the main material froma  primitive
        GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);        
        Material standardMaterial = tempCube.GetComponent<MeshRenderer>().material;
        standardMaterial.SetFloat("_Metallic", 0.5f);
        Destroy(tempCube);
        int tintsAndShades = UnityEngine.Random.Range(3, 7);//randomimse?
        //run through cells
        for (int i = 0; i < cells.Count; i++)
        {
          

            if (cells[i].GetComponent<PaletteInfo>() == null)
            {
                //we havent set any colours for this cell
                PaletteInfo pI = cells[i].AddComponent<PaletteInfo>();
                //make a pallete for this cell
                //randomly choose hwat type of step is used to create the harmonies(degres on colour wheel)
                int harmonyStep = 30 * UnityEngine.Random.Range(1, 5);

                List<PaletteInfo.MaterialAndShades> palette = PaletteInfo.Palette(hue, saturation, value,harmonyStep, tintsAndShades, standardMaterial);
                pI.palette = palette;

                //now for each adjacent, create a pallete from one of the harmonious materials
                List<GameObject> adjacents = cells[i].GetComponent<AdjacentCells>().adjacentCells;
                for (int j = 0; j < adjacents.Count; j++)
                {


                    PaletteInfo adjacentPI = adjacents[j].AddComponent<PaletteInfo>();

                    //choose random harmony - or cluster colours by clamping to one  - or cluster to adjacent colours in the pallet (0,1,2) //or always contrast - last half of pallete
                    int randomHarmonyIndex = UnityEngine.Random.Range(0, palette.Count/2);//1;// UnityEngine.Random.Range(1, palette.Count);//start at 1 because 0 is main colour//UnityEngine.Random.Range(palette.Count/2, palette.Count )

                    int randomTintIndex = UnityEngine.Random.Range(0, tintsAndShades);

                    Color harmonyColour = palette[randomHarmonyIndex].material.color;                  

                    //get hue from this
                    Color.RGBToHSV(harmonyColour, out hue, out saturation, out value);
                    //now make a palette
                    harmonyStep = 30 * UnityEngine.Random.Range(1, 5);
                    adjacentPI.palette = PaletteInfo.Palette(hue, saturation, value, harmonyStep, tintsAndShades, standardMaterial);

                }
            }
        }
    }
}

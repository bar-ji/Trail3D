using Godot;
using System;
using System.Collections.Generic;

[Tool]
public partial class Trail3D : Node3D
{
	[ExportGroup("Trail Properties")]
	[Export] private bool emitting;
	[Export] private float duration = 0.5f;
	[Export] private float snapshotInterval = 0.02f;
	[Export] private float width = 1;
	[Export] private Curve widthCurve;
	[Export] private Vector2 UVScale = new Vector2(1,1);
	[Export] private Material material;

	private List<TargetSnapshot> snapshotBuffer;
	private MeshInstance3D trailMesh;

	private float t;
	private float snapshotT;

	public void SetWidth(float value) => width = value;
	public void SetDuration(float value) => duration = value;
	public void SetSnapshotInterval(float value) => snapshotInterval = value;
	public void SetMaterial(Material value) => material = value;
	
	public bool IsEmitting() => emitting;
	public void StartEmitting() => emitting = true;

	public void StopEmitting()
	{
		Init();
		emitting = false;
	}
	
	private void Init()
	{
		foreach (var child in GetChildren())
		{
			child.Free();
		}
		
		trailMesh = new MeshInstance3D();
		AddChild(trailMesh);
		trailMesh.Mesh = new ImmediateMesh();
		
		if (trailMesh.Mesh is ImmediateMesh mesh)
		{
			mesh.ClearSurfaces();
		}

		GD.Print("initializing");

		trailMesh.TopLevel = true;
		t = 0;
		snapshotT = 0;
		snapshotBuffer = new List<TargetSnapshot>();
	}

	public override void _EnterTree()
	{
		Init();
	}
	

	public override void _Process(double delta)
	{
		if (!emitting)
		{
			return;
		}
		
		if (snapshotBuffer == null)
		{
			Init();
		}
		
		if (trailMesh == null)
		{
			GD.Print("[TRAIL3D] TRAIL MESH COULD NOT BE CREATED.");
			emitting = false;
			return;
		}

		float dt = (float)delta;

		//Push a snapshot and draw a trail if a snapshotInterval has elapsed since the last snapshot.
		if (snapshotT > snapshotInterval)
		{
			int count = snapshotBuffer.Count;
			if (count > 0)
			{
				//Only push a snapshot if the target has moved.
				if (snapshotBuffer[count-1].Position != GlobalPosition)
				{
					PushSnapshot();
				}

				//Remove a snapshot once it's been "alive" for duration.
				if (t - snapshotBuffer[0].Time > duration)
				{
					snapshotBuffer.RemoveAt(0);
				}
			
			}
			else //If there's nothing in the snapshotBuffer append the current position.
			{
				PushSnapshot();
			}

			DrawTrail();
			snapshotT = 0;
		}

		t += dt;
		snapshotT += dt;
	}

	//Add a snapshot to the buffer.
	private void PushSnapshot()
	{
		snapshotBuffer.Add(new TargetSnapshot(GlobalPosition, GlobalTransform.Basis, t));
	}

	private void DrawTrail()
	{
		if (trailMesh.Mesh is ImmediateMesh mesh)
		{
			mesh.ClearSurfaces();
			
			if(snapshotBuffer.Count < 2) return; //Only draw a face if there's two snapshots to draw between
			
			//Iterate through the snapshot buffer and draw all the faces.
			mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, material);
			for(int i = 1; i < snapshotBuffer.Count; i++)
			{
				DrawFace(mesh, i);
			}
			GD.Print("---");
			mesh.SurfaceEnd();	
		}
	}
	
	/*DRAW FACE METHOD:
	 - This function is responsible for drawing two triangles between two TargetSnapshots, resulting in a quad face.
	 - The Trail's are drawn using Immediate Mode geometry, the entire trail is redrawn every frame as it's insanely
	 cheap.
	 */
	
	private void DrawFace(ImmediateMesh mesh, int index)
	{
		TargetSnapshot snapshot = snapshotBuffer[index];
		TargetSnapshot previousSnapshot = snapshotBuffer[index - 1];

		float snapX = index / (float)snapshotBuffer.Count;
		float snapWidth = widthCurve.Sample(snapX);

		float prevSnapX = (index - 1) / (float)snapshotBuffer.Count;
		float prevSnapWidth = widthCurve.Sample(prevSnapX);

		Vector3 vert1 = previousSnapshot.Position + previousSnapshot.Basis.Y.Normalized() * prevSnapWidth * width;
		Vector3 vert2 = snapshot.Position + snapshot.Basis.Y.Normalized() * snapWidth * width; 
		Vector3 vert3 = previousSnapshot.Position - previousSnapshot.Basis.Y.Normalized() * prevSnapWidth * width;
		Vector3 vert4 = snapshot.Position - snapshot.Basis.Y.Normalized() * snapWidth * width;

		
		/*NORMALS:
		 - all vertices on a face point in the same direction. Afaik, the only case where you *might* want to change
		 this is if you have a really low snapshot rate, and you want to do some blending on sharp angles. However with a higher
		 snapshot rate that blending shouldn't be necessary.
		*/
		
		Vector3 normal = snapshot.Basis.Z.Normalized();
		
		/*UVs:
		 - UV's are seemless however, I need to implement some sort of triplanar mapping/similar to ensure that the UV's don't
		 stretch when the acceleration of the target isn't constant, and actually fit the faces that they are on. 
		 - If all the faces were perfect squares the current implementation would work fine, which I might add as an option so you can
		 enable a min distance approach rather than a time based one. Even still, this would be based on thickness which means a lot of
		 fine tuning would be necessary to achieve a desired result.
		 */

		float snapUVx = Mathf.Lerp(0, 1, snapX) * UVScale.X;
		float prevSnapUVx = Mathf.Lerp(0, 1, prevSnapX) * UVScale.X;
		
		float snapUVy = Mathf.Lerp(0, 1, snapWidth) * UVScale.Y;
		float prevSnapUVy = Mathf.Lerp(0, 1, prevSnapWidth) * UVScale.Y;
		
		Vector2 vert1UV = new Vector2(prevSnapUVx, 0.5f + prevSnapUVy / 2);
		Vector2 vert2UV = new Vector2(snapUVx, 0.5f + snapUVy / 2);
		Vector2 vert3UV = new Vector2(prevSnapUVx, 0.5f - prevSnapUVy / 2);
		Vector2 vert4UV = new Vector2(snapUVx, 0.5f - snapUVy / 2);

		Vector2[] tri1UVs = { vert1UV, vert2UV, vert3UV};
		Triangle tri1 = new Triangle(new[] {vert1, vert2, vert3}, new[] {normal, normal, normal}, tri1UVs, t);
		
		//This triangles vertices must start with vert4 to ensure the tri points in the same direction as tri1.
		Vector2[] tri2UVs = {vert4UV, vert3UV, vert2UV};
		Triangle tri2 = new Triangle(new[] {vert4, vert3, vert2}, new[] {normal, normal, normal}, tri2UVs, t);

		
		//It's important to load the triangles in this order to ensure that the trail doesn't self-intersect.
		for (int i = 0; i < tri1.Vertices.Length; i++)
		{
			mesh.SurfaceSetUV(tri1.UVs[i]);
			mesh.SurfaceSetNormal(tri1.Normals[i]);
			mesh.SurfaceAddVertex(tri1.Vertices[i]);
		}
		
		for (int i = 0; i < tri2.Vertices.Length; i++)
		{
			mesh.SurfaceSetUV(tri2.UVs[i]);
			mesh.SurfaceSetNormal(tri2.Normals[i]);
			mesh.SurfaceAddVertex(tri2.Vertices[i]);
		}
	}

	/*TRIANGLE STRUCT
	 - Basic triangle class that represents half of a "face" on the mesh.
	 - 2 Target Snapshots are required to draw a face, the DrawFace method draws a triangle between the currently
	 sampled position and the previous one, to ensure a continous row of triangles.
	 */
	
	public struct Triangle
	{
		public Vector3[] Vertices;
		public Vector3[] Normals;
		public Vector2[] UVs;
		public float Time { get; private set; }

		public Triangle(Vector3[] vertices, Vector3[] normals, Vector2[] uvs, float time)
		{
			Vertices = vertices;
			Normals = normals;
			UVs = uvs;
			Time = time;
		}
	}

	/*TARGET SNAPSHOT CLASS
	 - Stores the Position and Basis at a given point in time.
	 - The time is stored so we can remove this snapshot from the buffer after it's been alive for duration.
	 */
	public class TargetSnapshot
	{
		public Vector3 Position { get; private set; }
		public Basis Basis { get; private set; }
		public float Time { get; private set; }

		public TargetSnapshot(Vector3 position, Basis basis, float time)
		{
			Position = position;
			Basis = basis;
			Time = time;
		}
	}
	
}

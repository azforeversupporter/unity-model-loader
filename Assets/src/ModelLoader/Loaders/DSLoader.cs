using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Text;

namespace UnityModelLoader.ModelLoader.Loaders
{
	public class DSLoader : ILoader, IDisposable
	{
		public DSLoader ()
		{
			currentChunk = new Chunk();
			tmpChunk = new Chunk();
		}
		
		public int ObjectCount 
		{
			get { return objectCount; }
		}
		
		public List<GameObject> Load(string path) 
		{
			Setup (path);
			
			ReadChunk (currentChunk);
			
			if (currentChunk.Type != ChunkType.Main)
			{
				Debug.LogError("Not a valid 3ds file.");
				reader.Close ();
				stream.Dispose ();
				
				return null;
			}
		
			// Begin loading object with recursion
			ProcessNextChunk(currentChunk);
		
			// Normalize before returning
			foreach (var obj in gameObjects)
			{
				var mesh = GetMeshFromObject(obj);
				mesh.RecalculateNormals();
			}
		
			return gameObjects;
		}
		
		public void Dispose()
		{
			if (reader != null)
			{
				reader.Close ();
				reader = null;
			}
			
			if (stream != null)
			{
				stream.Dispose ();
				stream = null;
			}
		}
		
		/// <summary>
		/// Reads the next chunk from the 3ds file.
		/// </summary>
		/// <param name="chunk">Chunk.</param>
		private void ReadChunk(Chunk chunk)
		{
			chunk.Type = (ChunkType)reader.ReadUInt16 ();
			chunk.Length = reader.ReadInt32 ();
			
			chunk.Processed = 6;
		}
		
		/// <summary>
		/// Setup the required variables.
		/// </summary>
		/// <param name="path">The path to the file to load.</param>
		private void Setup(string path)
		{
			stream = File.OpenRead (path);
			reader = new BinaryReader(stream, Encoding.ASCII);
			
			currentChunk = new Chunk();
			tmpChunk = new Chunk();
		}
		
		private void ProcessNextChunk(Chunk prevChunk)
		{
			currentChunk = new Chunk();
			
			while (prevChunk.Processed < prevChunk.Length)
			{
				// Read the next chunk
				ReadChunk (currentChunk);
				
				switch (currentChunk.Type)
				{
					case ChunkType.Version:
						fileVersion = reader.ReadInt32 ();
						currentChunk.Processed += 4;
						
						if (fileVersion > 3)
						{
							Debug.LogWarning ("File is newer than version 3, so it may load incorrectly!");
						}
						break;
						
					case ChunkType.ObjectInfo:
						ReadChunk(tmpChunk);
						
						int meshVersion = reader.ReadInt32 ();
						tmpChunk.Processed += 4;
						
						currentChunk.Processed += tmpChunk.Processed;
						ProcessNextChunk (currentChunk);
						break;
						
					case ChunkType.Material:
						materialCount++;
						
						var material = new Material(Shader.Find("Diffuse"));
						materials.Add (material);
						
						ProcessNextMaterialChunk(currentChunk);
						break;
						
					case ChunkType.Object:
						objectCount++;
						gameObjects.Add (InitObject ());
						
						char ch;
						var name = string.Empty;
						do 
						{
							ch = reader.ReadChar ();
							name += ch;
						} while (ch != '\0');
						
						currentObject.name = name;
						currentChunk.Processed += Encoding.ASCII.GetByteCount (name);
						
						ProcessNextObjectChunk(currentObject, currentChunk);
						break;
						
					default:
						// Seek to the next chunk
						var seek = currentChunk.Length - currentChunk.Processed;
						reader.BaseStream.Seek (seek, SeekOrigin.Current);
						currentChunk.Processed += seek;
						break;
				}
				
				// Append the processed amount to the processed amount of the previous chunk
				prevChunk.Processed += currentChunk.Processed;
			}
			
			// Make the current chunk the previous chunk, because that's the way things started
			currentChunk = prevChunk;
		}
		
		private void ProcessNextObjectChunk(GameObject obj, Chunk prevChunk)
		{
			currentChunk = new Chunk();
			
			while (prevChunk.Processed < prevChunk.Length) 
			{
				// Read the next chunk
				ReadChunk (currentChunk);
				
				switch (currentChunk.Type)
				{
					// Start of a new object
					case ChunkType.ObjectMesh:
						ProcessNextObjectChunk (obj, currentChunk);
						break;
					
					case ChunkType.ObjectVertices:
						AddVerticesToObject(obj, currentChunk);
						break;
					
					case ChunkType.ObjectFaces:
						AddFacesToObject(obj, currentChunk);
						break;
					
					case ChunkType.ObjectMaterial:
						AddMaterialToObject(obj, currentChunk);
						break;
					
					case ChunkType.ObjectUv:
						AddUvCoordinatesToObject(obj, currentChunk);
						break;
					
					default:
						var seek = currentChunk.Length - currentChunk.Processed;
						reader.BaseStream.Seek (seek, SeekOrigin.Current);
						currentChunk.Processed += seek;
						break; 
				}
				
				prevChunk.Processed += currentChunk.Processed;
			}
			
			// Make the current chunk the previous chunk, because that's the way things started
			currentChunk = prevChunk;
		}
		
		private void ProcessNextMaterialChunk(Chunk prevChunk)
		{
			currentChunk = new Chunk();
			var materialName = string.Empty;
			var materialFileName = string.Empty;
			char ch;
			
			while (prevChunk.Processed < prevChunk.Length)
			{
				// Read the next chunk
				ReadChunk (currentChunk);
				
				switch (currentChunk.Type) 
				{
					case ChunkType.MaterialName:
						do
						{
							ch = reader.ReadChar ();
							materialName += ch;
						} while (ch != '\0');
						
						var material = GetCurrentlyProcessedMaterial();
						material.name = materialName;
						
						currentChunk.Processed += Encoding.ASCII.GetByteCount (materialName);
						break;
						
					// TODO: Material Map support
					case ChunkType.MaterialDiffuse:
						AddDiffuseColorToMaterial(GetCurrentlyProcessedMaterial(), currentChunk);
						break;
						
					default:
						var seek = currentChunk.Length - currentChunk.Processed;
						reader.BaseStream.Seek (seek, SeekOrigin.Current);
						currentChunk.Processed += seek;
						break;
				}
				
				prevChunk.Processed += currentChunk.Processed;
			}
			
			currentChunk = prevChunk;
		}
		
		private void AddVerticesToObject(GameObject obj, Chunk prevChunk)
		{
			var vertexCount = reader.ReadUInt16 ();
			prevChunk.Processed += 2;
			
			var vertices = new List<Vector3>();
			for (int i = 0; i < vertexCount; i++) 
			{
				var x = reader.ReadSingle ();
				var y = reader.ReadSingle ();
				var z = reader.ReadSingle ();
				
				// Swap Y and Z, because 3D Studio Max uses Z as the up axis
				vertices.Add (new Vector3(x, z, y));				
			}
			
			// Add vertices to the object
			var mesh = GetMeshFromObject (obj);
			mesh.vertices = vertices.ToArray ();
			
			// Add amount of processed bytes. Because ReadSingle reads 4 bytes, 
			// 12 bytes * vertexCount has been read.
			prevChunk.Processed += (12 * vertexCount);
		}
		
		private void AddFacesToObject(GameObject obj, Chunk prevChunk)
		{
			var faceCount = reader.ReadUInt16 ();
			prevChunk.Processed += 2;
			
			var faces = new List<int>();
			for (int i = 0; i < faceCount; i++)
			{
				for (int j = 0; j < 4; j++)
				{
					// Only use the first 3 shorts, the 4th is not of use
					var index = reader.ReadInt16 ();
					
					if (j < 3)
					{
						faces.Add (index);
					}
				}	
			}
			
			var mesh = GetMeshFromObject (obj);
			mesh.triangles = faces.ToArray ();
			
			// Add amount of processed bytes. Because ReadInt16 reads 2 bytes, 
			// 8 bytes * faceCount has been read.
			prevChunk.Processed += (8 * faceCount);
		}
		
		private void AddUvCoordinatesToObject(GameObject obj, Chunk prevChunk)
		{
			var textureVertexCount = reader.ReadInt16();
			prevChunk.Processed += 2;
			
			var vertices = new List<Vector2>();
			for (int i = 0; i < textureVertexCount; i++)
			{
				var u = reader.ReadSingle ();
				var v = reader.ReadSingle ();
				
				vertices.Add (new Vector2(u, v));
			}
			
			var mesh = GetMeshFromObject (obj);
			mesh.uv = vertices.ToArray();
			
			// Add amount of processed bytes. Because ReadSingle reads 4 bytes, 
			// 8 bytes * textureVertexCount has been read.
			prevChunk.Processed += (8 * textureVertexCount);
		}
		
		private void AddDiffuseColorToMaterial(Material material, Chunk chunk)
		{
			// Read the color chunk
			ReadChunk(tmpChunk);
			
			var rgb = reader.ReadBytes (3);
			tmpChunk.Processed += 3;
			
			// Normalize colors, because Unity uses floats between 0 and 1 
			// while 3D Studio Max uses bytes between 0 and 255
			var r = NormalizeColor (rgb[0]);
			var g = NormalizeColor (rgb[1]);
			var b = NormalizeColor (rgb[2]);
			
			material.color = new Color(r, g, b);
			
			chunk.Processed += tmpChunk.Processed;
		}
		
		private void AddMaterialToObject(GameObject obj, Chunk prevChunk)
		{
			char ch;
			var materialName = string.Empty;
			
			do 
			{
				ch = reader.ReadChar ();
				materialName += ch;
			} while (ch != '\0');
			
			prevChunk.Processed += Encoding.ASCII.GetByteCount (materialName);
			
			materialName = materialName.Replace ('\0', ' ').Trim ();
			
			var material = materials.FirstOrDefault (m => m.name == materialName);
			if (material != null)
			{
				var renderer = GetRendererFromObject (obj);
				renderer.material = material;
			}
			
			// Seek to skip shared materials
			var seek = prevChunk.Length - prevChunk.Processed;
			reader.BaseStream.Seek (seek, SeekOrigin.Current);
			prevChunk.Processed += seek;
		}
		
		private float NormalizeColor(int color) 
		{
			return (float)color / 255;
		}
		
		private GameObject InitObject() 
		{
			var obj = new GameObject();
			obj.AddComponent<MeshFilter>();
			obj.AddComponent<MeshRenderer>();
			
			return obj;
		}
		
		private Mesh GetMeshFromObject(GameObject obj)
		{
			return obj.GetComponent<MeshFilter>().mesh;
		}
		
		private Renderer GetRendererFromObject(GameObject obj)
		{
			return obj.GetComponent<MeshRenderer>().renderer;
		}
		
		private Material GetCurrentlyProcessedMaterial()
		{
			return materials.Last ();
		}
		
		private GameObject currentObject { get { return gameObjects.Last(); } }
		
		private Chunk currentChunk;
		private Chunk tmpChunk;
		private int objectCount = 0;
		private int materialCount = 0;
		private int fileVersion;
		private List<GameObject> gameObjects = new List<GameObject>();
		private List<Material> materials = new List<Material>();
		
		private FileStream stream;
		private BinaryReader reader;
	}
	
	internal class Chunk 
	{
		public Chunk ()
		{
			Length = 0;
			Processed = 0;	
		}
		
		public ChunkType Type { get; set; }
		public int Length { get; set; }
		public int Processed { get; set; }
	}
	
	internal enum ChunkType
	{
		Main = 0x4d4d,
		ObjectInfo = 0x3d3d,
		Version = 0x0002,
		EditKeyFrame = 0xB000,
		Material = 0xAFFF,
		Object = 0x4000,
		MaterialName = 0xA000,
		MaterialDiffuse = 0xA020,
		ObjectMesh = 0x4100,
		ObjectVertices = 0x4110,
		ObjectFaces = 0x4120,
		ObjectMaterial = 0x4130,
		ObjectUv = 0x4140
	}
}


using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using UnityEngine;

namespace AssetBundleLoader
{
    public static class ObjMeshLoader
    {
        static Dictionary<string, Mesh> loadedMeshes = new Dictionary<string, Mesh>();

        /// <summary>
        /// Build a Mesh from a Wavefront .obj on disk. Meshes are cached against the file they came from, so a
        /// mesh shared by a renderer and a collider, or by several prefabs, is only parsed once.
        /// </summary>
        /// <param name="filePath">Path of the .obj, already resolved to a file that exists</param>
        /// <param name="meshName">Name to give the mesh, for anything that prints it</param>
        public static Mesh Load(string filePath, string meshName)
        {
            string cacheKey = ModFilePath.CacheKey(filePath);

            if (loadedMeshes.ContainsKey(cacheKey) && loadedMeshes[cacheKey] != null)
            {
                return loadedMeshes[cacheKey];
            }

            var mesh = Parse(filePath, meshName);
            loadedMeshes[cacheKey] = mesh;

            return mesh;
        }

        /// <summary>
        /// Negating X swaps OBJ's right handed space for Unity's left handed one, which reverses which way round a
        /// face is wound, so the triangles are read back to front to keep them facing outwards.
        /// </summary>
        static Mesh Parse(string filePath, string meshName)
        {
            var positions = new List<Vector3>();
            var textureCoords = new List<Vector2>();
            var normals = new List<Vector3>();

            var vertexForFaceToken = new Dictionary<string, int>();
            var vertices = new List<Vector3>();
            var vertexTextureCoords = new List<Vector2>();
            var vertexNormals = new List<Vector3>();
            var triangles = new List<int>();
            int textureCoordsGiven = 0;
            int normalsGiven = 0;

            int VertexFor(string faceToken)
            {
                if (vertexForFaceToken.TryGetValue(faceToken, out var existing)) return existing;

                var indices = faceToken.Split('/');
                vertices.Add(positions[Index(indices[0], positions.Count)]);

                // Every vertex gets an entry in each channel, so a file that only sometimes names a UV or a normal still lines up
                bool hasTextureCoord = indices.Length > 1 && indices[1].Length > 0;
                vertexTextureCoords.Add(hasTextureCoord ? textureCoords[Index(indices[1], textureCoords.Count)] : Vector2.zero);
                if (hasTextureCoord) textureCoordsGiven++;

                bool hasNormal = indices.Length > 2 && indices[2].Length > 0;
                vertexNormals.Add(hasNormal ? normals[Index(indices[2], normals.Count)] : Vector3.zero);
                if (hasNormal) normalsGiven++;

                vertexForFaceToken[faceToken] = vertices.Count - 1;
                return vertices.Count - 1;
            }

            foreach (var line in File.ReadAllLines(filePath))
            {
                var fields = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length == 0) continue;

                switch (fields[0])
                {
                    case "v":
                        positions.Add(new Vector3(-Number(fields[1]), Number(fields[2]), Number(fields[3])));
                        break;
                    case "vt":
                        textureCoords.Add(new Vector2(Number(fields[1]), Number(fields[2])));
                        break;
                    case "vn":
                        normals.Add(new Vector3(-Number(fields[1]), Number(fields[2]), Number(fields[3])));
                        break;
                    case "f":
                        // Fanned, so a face with more corners than a triangle still comes through
                        for (int corner = 3; corner < fields.Length; corner++)
                        {
                            triangles.Add(VertexFor(fields[1]));
                            triangles.Add(VertexFor(fields[corner]));
                            triangles.Add(VertexFor(fields[corner - 1]));
                        }
                        break;
                }
            }

            var mesh = new Mesh();
            mesh.name = meshName;
            mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;

            if (vertices.Count > ushort.MaxValue)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.vertices = vertices.ToArray();
            if (textureCoordsGiven == vertices.Count) mesh.uv = vertexTextureCoords.ToArray();
            mesh.triangles = triangles.ToArray();

            if (normalsGiven == vertices.Count)
            {
                mesh.normals = vertexNormals.ToArray();
            }
            else
            {
                mesh.RecalculateNormals();
            }

            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            return mesh;
        }

        static float Number(string field)
        {
            return float.Parse(field, CultureInfo.InvariantCulture);
        }

        /// A negative index counts back from the end of what has been read so far, rather than forward from the start
        static int Index(string field, int count)
        {
            int written = int.Parse(field, CultureInfo.InvariantCulture);
            return written > 0 ? written - 1 : count + written;
        }
    }
}

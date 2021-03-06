﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ResFreeImage.UI {

    public class MeshIcon : MaskableGraphic
    {
        public enum ScalingMode {
            Stretch,
            Fit,
            NoScale
        }
        public enum SortMode {
            None,
            Normal,
            Reverse
        }

        private struct TriFace {
            public int i0;
            public int i1;
            public int i2;
            public Vector3 v0;
            public Vector3 v1;
            public Vector3 v2;
            
            public Vector3 vc;

            public void SetFace(int a, int b, int c) {
                i0 = a;
                i1 = b;
                i2 = c;
            }

            public void SetFace(int a, int b, int c, bool backface) {
                if(backface) {
                    SetFace(a, c, b);
                } else {
                    SetFace(a, b, c);
                }
            }

            public void SetVertices(Vector3 a, Vector3 b, Vector3 c) {
                v0 = a;
                v1 = b;
                v2 = c;
                vc = (v0 + v1 + v2) / 3.0f;
            }

            static public int CompareByZ(TriFace a, TriFace b) {
                if(a.vc.z == b.vc.z) {
                    return 0;
                }
                return (a.vc.z < b.vc.z) ? -1 : 1;
            }
            static public int CompareByZReverse(TriFace a, TriFace b) {
                if(a.vc.z == b.vc.z) {
                    return 0;
                }
                return (a.vc.z < b.vc.z) ? 1 : -1;
            }
        }

        public Texture2D texture;
        public Mesh sourceMesh;

        public BoundRect margin;

        public ScalingMode scalingMode = ScalingMode.Fit;
        public bool useMeshVertexColor = true;
        public bool removeBackFace = false;
        public SortMode sortTriangles = SortMode.None;

        public bool keepMeshOrigin = false;
        public bool keepMeshZ = false;

        public override Texture mainTexture {
            get {
                if (texture == null) {
                    if (material != null && material.mainTexture != null) {
                        return material.mainTexture;
                    }
                    return s_WhiteTexture;
                }
                return texture;
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh) {
            var recttransform = gameObject.transform as RectTransform;
            var rect = recttransform.rect;
            rect.xMin += margin.left;
            rect.xMax -= margin.right;
            rect.yMin += margin.bottom;
            rect.yMax -= margin.top;
            // Debug.Log("rect:" + rect.ToString() + "\ncenter:" + rect.center.ToString());

            if(sourceMesh == null) {
                PolulateQuad(vh, rect);
                return;
            }
            
            var meshsize = sourceMesh.bounds.size;

            // Calc scale
            var scale = Vector3.one;
            switch(scalingMode) {
                case ScalingMode.Stretch:
                    scale.x = rect.width / meshsize.x;
                    scale.y = rect.height / meshsize.y;
                    scale.z = Mathf.Min(scale.x, scale.y);
                    break;
                case ScalingMode.Fit:
                    scale.x = rect.width / meshsize.x;
                    scale.y = rect.height / meshsize.y;
                    scale.x = Mathf.Min(scale.x, scale.y);
                    scale.y = scale.x;
                    scale.z = scale.x;
                    break;
                case ScalingMode.NoScale:
                    scale = Vector3.one;
                    break;
            }
            
            float extraScaleZ = keepMeshZ? 1.0f : 0.0f;

            // Debug.Log("rect w:" + rect.width + ",h:" + rect.height);
            // Debug.Log("bounds:(w:" + meshsize.x + ",h:" + meshsize.y);
            // Debug.Log("scale:" + scale.ToString());
            
            // Calc offsets
            var meshcenter = keepMeshOrigin ? Vector3.zero : sourceMesh.bounds.center;

            var rectoffset = Vector3.zero;
            rectoffset.x = rect.center.x;
            rectoffset.y = rect.center.y;

            //
            vh.Clear();

            // Vertices
            var vcount = sourceMesh.vertexCount;
            UIVertex uiv = UIVertex.simpleVert;
            var uiverts = new List<UIVertex>(vcount);
            var vertposs = new List<Vector3>(vcount);

            for(int i = 0; i < vcount; i++) {
                var pos = sourceMesh.vertices[i] - meshcenter;
                pos.Scale(scale);
                pos += rectoffset;
                vertposs.Add(pos);
                
                pos.z *= extraScaleZ;
                uiv.position = pos;
                uiv.normal = sourceMesh.normals[i];
                uiv.tangent = sourceMesh.tangents[i];
                if(useMeshVertexColor) {
                    uiv.color = (i < sourceMesh.colors.Length) ? sourceMesh.colors[i] : Color.black;
                    uiv.color *= color;
                } else {
                    uiv.color = color;
                }
                uiv.uv0 = (i < sourceMesh.uv.Length) ? sourceMesh.uv[i] : Vector2.zero;
                uiv.uv1 = (i < sourceMesh.uv2.Length) ? sourceMesh.uv2[i] : Vector2.zero;
                uiv.uv2 = (i < sourceMesh.uv3.Length) ? sourceMesh.uv3[i] : Vector2.zero;
                uiv.uv3 = (i < sourceMesh.uv4.Length) ? sourceMesh.uv4[i] : Vector2.zero;
                uiverts.Add(uiv);
            }
            
            // Faces
            int tricount = sourceMesh.triangles.Length;
            var indices = new List<int>(tricount);

            if(!removeBackFace && (sortTriangles == SortMode.None)) {
                // No backface culling and z sort
                indices.AddRange(sourceMesh.triangles);

            } else {
                // Some mesh processing needed
                var faces = new List<TriFace>(tricount / 3);
                var triface = new TriFace();

                if(removeBackFace) {
                    // Backface culling
                    for(int i = 0; i < tricount; i+=3) {
                        int i0 = sourceMesh.triangles[i];
                        int i1 = sourceMesh.triangles[i + 1];
                        int i2 = sourceMesh.triangles[i + 2];
                        var v0 = vertposs[i0];
                        var v1 = vertposs[i1];
                        var v2 = vertposs[i2];
                        var v01 = v1 - v0;
                        var v02 = v2 - v0;
                        var n = Vector3.Cross(v01, v02);
                        if(n.z < 0.0f) { // Front face normal is (0,0,-1)
                            triface.SetFace(i0, i1, i2);
                            triface.SetVertices(v0, v1, v2);
                            faces.Add(triface);
                        }
                    }
                } else {
                    // Use all triangles
                    for(int i = 0; i < tricount; i+=3) {
                        triface.SetFace(
                            sourceMesh.triangles[i],
                            sourceMesh.triangles[i + 1],
                            sourceMesh.triangles[i + 2]
                        );
                        triface.SetVertices(
                            vertposs[triface.i0],
                            vertposs[triface.i1],
                            vertposs[triface.i2]
                        );
                        faces.Add(triface);
                    }
                }

                // Sorting. Camera's foward is (0,0,1)
                switch(sortTriangles) {
                    case SortMode.Normal:
                        // Descending order
                        faces.Sort(TriFace.CompareByZReverse);
                        break;
                    case SortMode.Reverse:
                        // Ascending order
                        faces.Sort(TriFace.CompareByZ);
                        break;
                    // case SortMode.None:
                    // default:
                    //     break;
                }

                // Add triangle
                foreach(var f in faces) {
                    indices.Add(f.i0);
                    indices.Add(f.i1);
                    indices.Add(f.i2);
                }
            }
            
            vh.AddUIVertexStream(uiverts, indices);
        }

        private void PolulateQuad(VertexHelper vh, Rect rect) {
            vh.Clear();
                Vector2[] verts = {
                    new Vector2(rect.xMin, rect.yMin),
                    new Vector2(rect.xMin, rect.yMax),
                    new Vector2(rect.xMax, rect.yMax),
                    new Vector2(rect.xMax, rect.yMin)
                };

                vh.Clear();

                UIVertex uiv = UIVertex.simpleVert;
                foreach(var v in verts) {
                    uiv.position = new Vector2(v.x, v.y);
                    uiv.uv0 = new Vector2((v.x - rect.xMin) / rect.width, (v.y - rect.yMin) / rect.height);
                    uiv.color = color;
                    vh.AddVert(uiv);
                }
                vh.AddTriangle(0, 1, 2);
                vh.AddTriangle(2, 3, 0);
        }
    }
}

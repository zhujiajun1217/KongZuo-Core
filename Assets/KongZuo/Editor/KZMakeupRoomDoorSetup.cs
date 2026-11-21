#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Open only the dressing-room leaf in the baked building meshes. The source
// theatre, surrounding walls and story-controlled backstage gate stay authored.
public static class KZMakeupRoomDoorSetup
{
    private const string MeshRoot="Assets/KongZuo/Art/Art01/Meshes/";

    public static void Apply(GameObject art, Transform layer, Vector3 room)
    {
        var pivot=new GameObject("MakeupDoor_Open").transform;
        pivot.SetParent(layer,false);
        float side=Mathf.Sign(room.x);
        pivot.position=new Vector3(room.x+side*.625f,0f,15.378f);
        int visuals=0,collisions=0;
        foreach(MeshFilter filter in art.GetComponentsInChildren<MeshFilter>(true))
        {
            MeshFilter authored=PrefabUtility.GetCorrespondingObjectFromSource(filter);
            Mesh source=authored==null?filter.sharedMesh:authored.sharedMesh;
            if(source==null) continue;
            string path=AssetDatabase.GetAssetPath(source);
            if(!path.Contains("05_Backstage__TIMBER.obj") && !path.Contains("05_Backstage__TIMBER_DARK.obj") && !path.Contains("05_Backstage__BRASS.obj")) continue;
            if(!Split(source,filter.transform,out Mesh closed,out Mesh leaf)) continue;
            filter.sharedMesh=Store(closed,source,"Doorway");
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            var go=new GameObject("OpenLeaf_"+source.name,typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetPositionAndRotation(filter.transform.position,filter.transform.rotation);
            go.transform.localScale=filter.transform.lossyScale;
            go.transform.SetParent(pivot,true);
            go.GetComponent<MeshFilter>().sharedMesh=Store(leaf,source,"OpenLeaf");
            go.GetComponent<MeshRenderer>().sharedMaterials=filter.GetComponent<MeshRenderer>().sharedMaterials;
            visuals++;
        }
        foreach(MeshCollider collider in art.GetComponentsInChildren<MeshCollider>(true))
        {
            MeshCollider authored=PrefabUtility.GetCorrespondingObjectFromSource(collider);
            Mesh source=authored==null?collider.sharedMesh:authored.sharedMesh;
            if(source==null || !AssetDatabase.GetAssetPath(source).Contains("99_Collision__COLLIDER.obj")) continue;
            if(!Split(source,collider.transform,out Mesh open,out Mesh leaf)) continue;
            Object.DestroyImmediate(leaf);
            collider.sharedMesh=Store(open,source,"Doorway");
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            collisions++;
        }
        if(visuals!=3 || collisions!=1)
            throw new InvalidOperationException("Makeup door geometry changed: visual parts="+visuals+", collision meshes="+collisions);
        pivot.rotation=Quaternion.Euler(0,side*170f,0);
    }

    private static bool Split(Mesh source,Transform space,out Mesh remaining,out Mesh leaf)
    {
        Vector3[] vertices=source.vertices;
        var inside=new bool[vertices.Length];
        for(int i=0;i<vertices.Length;i++)
        {
            Vector3 p=space.TransformPoint(vertices[i]);
            float x=Mathf.Abs(p.x);
            inside[i]=x>=6.87f && x<=8.13f && p.y>=.08f && p.y<=2.57f && p.z>=15.13f && p.z<=15.445f;
        }
        var keep=new List<int>[source.subMeshCount];
        var move=new List<int>[source.subMeshCount];
        int removed=0;
        for(int sub=0;sub<source.subMeshCount;sub++)
        {
            keep[sub]=new List<int>();
            move[sub]=new List<int>();
            int[] triangles=source.GetTriangles(sub);
            for(int i=0;i<triangles.Length;i+=3)
            {
                bool selected=inside[triangles[i]] && inside[triangles[i+1]] && inside[triangles[i+2]];
                var output=selected?move[sub]:keep[sub];
                output.Add(triangles[i]); output.Add(triangles[i+1]); output.Add(triangles[i+2]);
                if(selected) removed++;
            }
        }
        remaining=null; leaf=null;
        if(removed==0) return false;
        remaining=Build(source,keep);
        leaf=Build(source,move);
        return true;
    }

    private static Mesh Build(Mesh source,List<int>[] triangles)
    {
        Mesh mesh=Object.Instantiate(source);
        Vector3[] vertices=source.vertices;
        Bounds bounds=default;
        bool first=true;
        for(int sub=0;sub<triangles.Length;sub++)
        {
            mesh.SetTriangles(triangles[sub],sub,false);
            foreach(int index in triangles[sub])
            {
                if(first) { bounds=new Bounds(vertices[index],Vector3.zero); first=false; }
                else bounds.Encapsulate(vertices[index]);
            }
        }
        mesh.bounds=bounds;
        return mesh;
    }

    private static Mesh Store(Mesh mesh,Mesh source,string suffix)
    {
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long id);
        string path=MeshRoot+"MakeupDoor_"+guid+"_"+id+"_"+suffix+".asset";
        mesh.name=source.name+"_"+suffix;
        Mesh existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing==null) { AssetDatabase.CreateAsset(mesh,path); return mesh; }
        EditorUtility.CopySerialized(mesh,existing);
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(mesh);
        return existing;
    }
}
#endif

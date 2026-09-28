using System.Linq;
using UnityEditor;
using UnityEngine;
using PowerslideKartPhysics;
public static class PhoneVisualAudit {
 public static void Probe() {
  foreach(var id in KartSetup.Karts) {
   var art=AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Karts/KartVisual_{id}.prefab");
   foreach(var mf in art.GetComponentsInChildren<MeshFilter>()) if(mf.name.StartsWith("wheel")) Debug.Log($"[audit] {id}/{mf.name} mesh={mf.sharedMesh.bounds} scale={mf.transform.localScale} rot={mf.transform.localEulerAngles} pos={mf.transform.localPosition}");
   var src=AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Karts/Kart_{id}.prefab");
   var go=Object.Instantiate(src);
   foreach(var w in go.GetComponentsInChildren<KartWheel>()) {
    var tyre=w.visualWheel.Find("Tyre");var mf=tyre.GetComponent<MeshFilter>();
    Debug.Log($"[audit] {id}/{w.name} axle={w.transform.localPosition} radius={w.radius} visualRotation={w.visualWheel.localEulerAngles} tyreRotation={tyre.localEulerAngles} mesh={mf.sharedMesh.bounds} tyreWorldAxisX={tyre.right} axisY={tyre.up} axisZ={tyre.forward}");
   }
   Object.DestroyImmediate(go);
  }
  EditorApplication.Exit(0);
 }
}

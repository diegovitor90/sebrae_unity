using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
namespace SebraeLab.Editor {
 [InitializeOnLoad] public static class EnvironmentSetup {
  const string Root="Assets/SebraeLab/Environment";
  public const string ScenePath="Assets/SebraeLab/Scenes/SebraeLab_Exploracao.unity";
  static Dictionary<string,Material> mats;
  static EnvironmentSetup(){EditorApplication.delayCall+=Auto;}
  static void Auto(){if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Auto;return;}if(!File.Exists(ScenePath))Create();
   else if(!File.Exists("Logs/environment-validation.json")){Open();FinalizeEnvironment();}}
  static Material Material(string name,string texture,Color color,float metallic=0,float smooth=.15f){
   var path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(!m){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Metallic",metallic);m.SetFloat("_Glossiness",smooth);m.enableInstancing=true;
   if(texture!=null)m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sebraelab3D/"+texture+".png");return m;
  }
  static void Materials(){
   mats=new Dictionary<string,Material>();
   mats["CombinedTexture"]=Material("TrimSheet","TrimSheet_2k_albedo",Color.white);
   mats["Parede"]=Material("Paredes","PlasterWall_1k_albedo",new Color(.63f,.70f,.66f));
   mats["Chao Azulejo"]=Material("Piso_Azulejo","TileFloor_1k_albedo",Color.white);
   mats["Chao Madeira"]=Material("Piso_Madeira","WoodFloor_1k_albedo",Color.white);
   mats["Calçada"]=Material("Calcada",null,new Color(.5f,.52f,.54f));
   mats["Frame Metal"]=Material("Metal",null,new Color(.13f,.15f,.17f),.6f,.35f);
   mats["Default-Material"]=mats["Frame Metal"];
   var glass=Material("Vidro",null,new Color(.7f,.88f,.95f,.17f),0,.65f);
   glass.SetFloat("_Mode",3);glass.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);glass.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);glass.SetInt("_ZWrite",0);
   glass.EnableKeyword("_ALPHABLEND_ON");glass.DisableKeyword("_ALPHATEST_ON");glass.DisableKeyword("_ALPHAPREMULTIPLY_ON");glass.SetOverrideTag("RenderType","Transparent");glass.renderQueue=3000;mats["Vidro"]=glass;
   foreach(var m in mats.Values)EditorUtility.SetDirty(m);
  }
  static void MapMaterials(GameObject go){foreach(var r in go.GetComponentsInChildren<Renderer>(true)){
   r.sharedMaterials=r.sharedMaterials.Select(m=>m && mats.TryGetValue(m.name,out var mapped)?mapped:m).ToArray();r.receiveShadows=false;r.shadowCastingMode=ShadowCastingMode.Off;
  }}
  static GameObject Group(string name,Transform parent){var g=new GameObject(name);g.transform.SetParent(parent,false);return g;}
  [MenuItem("Sebrae Lab/Ambiente/1 - Montar ambiente")]
  public static void Create(){
   foreach(var sub in new[]{"Materials","Meshes","Prefabs"})Directory.CreateDirectory(Root+"/"+sub);AssetDatabase.Refresh();Materials();
   var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   var root=new GameObject("SebraeLab - Ambiente");var architecture=Group("01 - Arquitetura",root.transform);var furniture=Group("02 - Mobiliario",root.transform);var decor=Group("03 - Decoracao e luminarias",root.transform);var collisions=Group("04 - Colisoes e navegacao",root.transform);
   var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Sebraelab3D/Sebrae Lab.fbx"));
   foreach(var c in model.GetComponentsInChildren<Camera>(true))UnityEngine.Object.DestroyImmediate(c.gameObject);
   foreach(var l in model.GetComponentsInChildren<Light>(true))UnityEngine.Object.DestroyImmediate(l);
   // Original layout already uses meters (fridge 1.72m; doors 2.8m). Only re-center X.
   model.transform.position=new Vector3(-12.861f,0,0);
   int removed=0;
   foreach(var t in model.transform.Cast<Transform>().ToArray()){
    if(t.name.StartsWith("SM_")||t.name.StartsWith("chrome_")||!t.GetComponentInChildren<Renderer>()){UnityEngine.Object.DestroyImmediate(t.gameObject);removed++;continue;}
    var arch=t.name=="Base"||t.name=="Teto"||t.name.Contains("Entrada")||t.name=="Backroom GlassDoor"||t.name=="Cube.017";
    var decoration=t.name.Contains("Decor")||t.name.Contains("Decora")||t.name.Contains("condicionado")||t.name.StartsWith("Plane.")||t.name=="Cube.004";
    t.SetParent(arch?architecture.transform:decoration?decor.transform:furniture.transform,true);
   }
   UnityEngine.Object.DestroyImmediate(model);MapMaterials(root);
   var baseGo=architecture.transform.Find("Base").gameObject;var baseFilter=baseGo.GetComponent<MeshFilter>();
   MakeBaseColliders(baseFilter,collisions.transform);
   foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true)){
    if(mf==baseFilter)continue;
    var mc=mf.gameObject.AddComponent<MeshCollider>();mc.sharedMesh=mf.sharedMesh;mc.convex=false;mf.gameObject.isStatic=true;
   }
   foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.isStatic=true;
   PrefabUtility.SaveAsPrefabAssetAndConnect(root,Root+"/Prefabs/SebraeLab_Ambiente.prefab",InteractionMode.AutomatedAction);
   var lighting=new GameObject("Iluminacao");
   RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.85f,.88f,.93f);RenderSettings.ambientEquatorColor=new Color(.65f,.69f,.74f);RenderSettings.ambientGroundColor=new Color(.45f,.46f,.48f);RenderSettings.ambientIntensity=1;
   RenderSettings.skybox=null;RenderSettings.fog=false;
   var sun=new GameObject("Luz geral",typeof(Light));sun.transform.SetParent(lighting.transform);sun.transform.rotation=Quaternion.Euler(60,-35,0);var sl=sun.GetComponent<Light>();sl.type=LightType.Directional;sl.intensity=.65f;sl.shadows=LightShadows.None;
   var rig=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Samples/XR Interaction Toolkit/3.0.11/Starter Assets/Prefabs/XR Origin (XR Rig).prefab"));
   rig.name="Jogador VR - Recepcao";rig.transform.position=new Vector3(5.7f,0,7.1f);rig.transform.rotation=Quaternion.Euler(0,-90,0);rig.GetComponent<XROrigin>().RequestedTrackingOriginMode=XROrigin.TrackingOriginMode.Floor;
   foreach(var c in rig.GetComponentsInChildren<ControllerInputActionManager>(true)){c.smoothMotionEnabled=false;c.smoothTurnEnabled=false;}
   new GameObject("XR Interaction Manager",typeof(XRInteractionManager));
   var simulator=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Samples/XR Interaction Toolkit/3.0.11/XR Device Simulator/XR Device Simulator.prefab"));simulator.name="Simulador PC - excluido da build";simulator.tag="EditorOnly";simulator.SetActive(false);
   EditorSceneManager.SaveScene(scene,ScenePath);EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};AssetDatabase.SaveAssets();
   if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(new Vector3(0,0,0),Quaternion.Euler(65,0,0),23);
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/environment-created.txt","Environment created; removed export helpers: "+removed+"; scale 1 meter per Unity unit");
   FinalizeEnvironment();Debug.Log("SEBRAE_ENV_READY");
  }
  static void MakeBaseColliders(MeshFilter mf,Transform parent){
   var source=mf.sharedMesh;var vertices=source.vertices.Select(v=>mf.transform.TransformPoint(v)).ToArray();var walk=new List<int>();var walls=new List<int>();var tri=source.triangles;
   for(int i=0;i<tri.Length;i+=3){var a=vertices[tri[i]];var b=vertices[tri[i+1]];var c=vertices[tri[i+2]];var normal=Vector3.Cross(b-a,c-a).normalized;
    bool floor=normal.y>.8f&&Mathf.Abs(a.y)<.035f&&Mathf.Abs(b.y)<.035f&&Mathf.Abs(c.y)<.035f;
    (floor?walk:walls).AddRange(new[]{tri[i],tri[i+1],tri[i+2]});
   }
   if(walk.Count==0)throw new Exception("No walkable floor triangles found; inspect model before enabling locomotion");
   foreach(var entry in new[]{("Piso - Teletransporte",walk),("Estrutura - Colisao",walls)}){
    var mesh=new Mesh{name=entry.Item1};mesh.vertices=vertices;mesh.triangles=entry.Item2.ToArray();mesh.RecalculateBounds();
    var path=Root+"/Meshes/"+(entry.Item1=="Piso - Teletransporte"?"WalkableFloor":"ArchitectureCollision")+".asset";
    var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);
    var g=Group(entry.Item1,parent);g.AddComponent<MeshCollider>().sharedMesh=mesh;
    if(entry.Item1=="Piso - Teletransporte"){var area=g.AddComponent<TeleportationArea>();area.interactionLayers=(InteractionLayerMask)(1<<31);}
   }
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/environment-floor.txt","Floor triangles: "+walk.Count/3+"; wall triangles: "+walls.Count/3);
  }
  [MenuItem("Sebrae Lab/Ambiente/2 - Abrir ambiente")]
  public static void Open(){EditorSceneManager.OpenScene(ScenePath);}
  [MenuItem("Sebrae Lab/Ambiente/3 - Renderizar previas")]
  public static void RenderPreviews(){
   if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
   Directory.CreateDirectory("Previews");
   var roof=GameObject.Find("Teto");if(roof)roof.SetActive(false);
   Render("Previews/SebraeLab_Planta.png",new Vector3(0,32,0),Quaternion.Euler(90,0,0),true);
   if(roof)roof.SetActive(true);
   Render("Previews/SebraeLab_Interior.png",new Vector3(5.7f,1.65f,7.1f),Quaternion.Euler(0,-90,0),false);
   EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
  }

  [Serializable] class Validation {public int renderers,triangles,colliders,missingScripts,missingMaterials;public bool spawnOnFloor,spawnClear;public string[] spawnObstacles;public Vector3 spawn;}
  [MenuItem("Sebrae Lab/Ambiente/4 - Validar e finalizar ambiente")]
  public static void FinalizeEnvironment(){
   Materials();
   foreach(var path in AssetDatabase.FindAssets("t:Model",new[]{"Assets/Sebraelab3D"}).Select(AssetDatabase.GUIDToAssetPath)){
    var importer=(ModelImporter)AssetImporter.GetAtPath(path);bool changed=false;
    foreach(var m in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())if(mats.TryGetValue(m.name,out var mapped)){
     var id=new AssetImporter.SourceAssetIdentifier(typeof(Material),m.name);
     if(!importer.GetExternalObjectMap().TryGetValue(id,out var current)||current!=mapped){importer.AddRemap(id,mapped);changed=true;}
    }
    if(importer.importCameras||importer.importLights){importer.importCameras=false;importer.importLights=false;changed=true;}
    if(changed)importer.SaveAndReimport();
   }
   foreach(var path in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Sebraelab3D"}).Select(AssetDatabase.GUIDToAssetPath)){
    var importer=(TextureImporter)AssetImporter.GetAtPath(path);var platform=importer.GetPlatformTextureSettings("Android");
    if(!platform.overridden||platform.format!=TextureImporterFormat.ASTC_6x6){platform.overridden=true;platform.format=TextureImporterFormat.ASTC_6x6;platform.maxTextureSize=path.Contains("TrimSheet")?2048:1024;platform.compressionQuality=50;importer.mipmapEnabled=true;importer.SetPlatformTextureSettings(platform);importer.SaveAndReimport();}
   }
   var rig=GameObject.Find("Jogador VR - Recepcao");Physics.SyncTransforms();var pos=rig.transform.position;
   var hits=Physics.RaycastAll(pos+Vector3.up*2,Vector3.down,3).OrderBy(h=>h.distance).ToArray();
   bool floor=hits.Any(h=>h.collider.GetComponent<TeleportationArea>()&&Mathf.Abs(h.point.y-pos.y)<.04f);
   var overlaps=Physics.OverlapCapsule(pos+Vector3.up*.3f,pos+Vector3.up*1.6f,.2f,~0,QueryTriggerInteraction.Ignore).Where(c=>!c.transform.IsChildOf(rig.transform)).ToArray();
   var roots=SceneManager.GetActiveScene().GetRootGameObjects();var rs=roots.SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).ToArray();
   var v=new Validation{renderers=rs.Length,triangles=roots.SelectMany(g=>g.GetComponentsInChildren<MeshFilter>(true)).Where(f=>f.sharedMesh).Sum(f=>Enumerable.Range(0,f.sharedMesh.subMeshCount).Sum(i=>(int)f.sharedMesh.GetIndexCount(i)/3)),colliders=roots.SelectMany(g=>g.GetComponentsInChildren<Collider>(true)).Count(),missingScripts=roots.SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)),missingMaterials=rs.Sum(r=>r.sharedMaterials.Count(m=>!m)),spawn=pos,spawnOnFloor=floor,spawnClear=overlaps.Length==0,spawnObstacles=overlaps.Select(c=>c.name).ToArray()};
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/environment-validation.json",JsonUtility.ToJson(v,true));
   if(!v.spawnOnFloor||!v.spawnClear||v.missingScripts>0||v.missingMaterials>0)throw new Exception("Environment validation failed: see Logs/environment-validation.json");
   RenderPreviews();AssetDatabase.SaveAssets();Debug.Log("SEBRAE_ENV_VALIDATION_OK");
  }
  [MenuItem("Sebrae Lab/Ambiente/5 - Gerar APK do ambiente")]
  public static void BuildEnvironment(){
   VRProjectSetup.Setup();Open();FinalizeEnvironment();
   Directory.CreateDirectory("Builds/Quest");var report=BuildPipeline.BuildPlayer(new[]{ScenePath},"Builds/Quest/SebraeLab-Ambiente.apk",BuildTarget.Android,BuildOptions.Development);
   if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Environment build failed: "+report.summary.result);
  }
  static void Render(string path,Vector3 position,Quaternion rotation,bool top){
   var go=new GameObject("Preview camera",typeof(Camera));var camera=go.GetComponent<Camera>();go.transform.SetPositionAndRotation(position,rotation);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.15f,.2f);camera.nearClipPlane=.05f;camera.farClipPlane=100;camera.fieldOfView=78;
   camera.orthographic=top;camera.orthographicSize=19;
   var rt=new RenderTexture(1400,1400,24);camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1400,1400,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1400,1400),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);
  }
 }
}

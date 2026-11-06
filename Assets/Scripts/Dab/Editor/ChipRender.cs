using System;
using System.Collections.Generic;
using System.IO;
using MoodPicker.Folio;
using MoodPicker.Tokens;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace MoodPicker.Dab
{
    public static class ChipRender
    {
        public const string MaterialPath = "Assets/Materials/Orion/Chip.mat";
        const int Width = 1536, Height = 512;
        static double started;
        public static void RenderBatch()
        {
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Wait;
        }
        static void Wait()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup-started < 3) return;
            EditorApplication.update -= Wait;
            int code;
            try { code = Run(Arg("-chipSamples"),Arg("-chipOut")) ? 0 : 1; }
            catch (Exception e) { Debug.LogException(e); code=1; }
            Debug.Log("[MP] chip " + (code==0 ? "PASS" : "FAIL"));
            EditorApplication.Exit(code);
        }
        static string Arg(string name)
        {
            var args=Environment.GetCommandLineArgs();
            for (int i=0;i<args.Length-1;i++) if (args[i]==name) return args[i+1];
            throw new ArgumentException(name);
        }
        public static bool Run(string samples, string output)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("real graphics required");
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) throw new InvalidOperationException("URP required");
            Directory.CreateDirectory(output);
            var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material==null || !material.shader.isSupported || ShaderUtil.ShaderHasError(material.shader)) throw new InvalidOperationException("Chip shader unavailable");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            var owned=new List<Object>();
            try
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                RenderSettings.skybox=null; RenderSettings.ambientMode=AmbientMode.Flat;
                RenderSettings.ambientLight=Color.black; RenderSettings.ambientIntensity=0;
                RenderSettings.reflectionIntensity=0; RenderSettings.fog=false;
                var mesh=ChipMesh.Create(); owned.Add(mesh);
                var chips=new MeshRenderer[3]; var tiles=new Texture2D[3];
                var names=new[] { "yunseul", "gradient_sample", "paint_20261104_143524_004" };
                for (int i=0;i<3;i++)
                {
                    tiles[i]=new Texture2D(2,2,TextureFormat.RGBA32,false,false); owned.Add(tiles[i]);
                    tiles[i].wrapMode=TextureWrapMode.Repeat; tiles[i].filterMode=FilterMode.Bilinear;
                    if (!tiles[i].LoadImage(File.ReadAllBytes(Path.Combine(samples,names[i],"tile.png")))) throw new IOException(names[i]);
                    var go=new GameObject("Chip "+names[i]);
                    go.transform.position=new Vector3((i-1)*0.08f,0,0);
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;
                    chips[i]=go.AddComponent<MeshRenderer>(); chips[i].sharedMaterial=material;
                }
                var camera=new GameObject("ChipCamera").AddComponent<Camera>(); camera.scene=scene;
                camera.transform.SetPositionAndRotation(new Vector3(0,0,0.4f),Quaternion.LookRotation(Vector3.back,Vector3.up));
                camera.orthographic=true; camera.orthographicSize=0.045f; camera.aspect=Width/(float)Height;
                camera.nearClipPlane=0.01f; camera.farClipPlane=1;
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.clear;
                camera.allowHDR=false; camera.allowMSAA=false;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
                foreach (var go in scene.GetRootGameObjects())
                    if (go.GetComponentsInChildren<Light>(true).Length!=0) throw new InvalidOperationException("lights must be zero");
                foreach (int angle in new[] {0,30})
                {
                    for (int i=0;i<3;i++)
                    {
                        chips[i].transform.rotation=Quaternion.Euler(0,angle,0);
                        ChipMesh.Apply(chips[i],new MaterialPropertyBlock(),tiles[i],i+1,i==0);
                    }
                    var actual=Render(camera); owned.Add(actual);
                    File.WriteAllBytes(Path.Combine(output,"chip-"+angle+".png"),actual.EncodeToPNG());
                    for (int i=0;i<3;i++) ChipMesh.Apply(chips[i],new MaterialPropertyBlock(),Texture2D.whiteTexture,i+1,i==0);
                    var reference=Render(camera); owned.Add(reference);
                    File.WriteAllBytes(Path.Combine(output,"chip-"+angle+"-white.png"),reference.EncodeToPNG());
                    Verify(actual,reference,camera,chips,angle);
                }
                if (ShaderUtil.ShaderHasError(material.shader)) throw new InvalidOperationException("shader compiler error");
                Debug.Log($"[MP] chip device={SystemInfo.graphicsDeviceType} lights=0 ambient=black angles=0,30");
                return true;
            }
            finally
            {
                foreach (Object item in owned) Object.DestroyImmediate(item);
                if (setup.Length>0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }
        static Texture2D Render(Camera camera)
        {
            var rt=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB) { antiAliasing=1 };
            var old=RenderTexture.active;
            try
            {
                rt.Create(); camera.targetTexture=rt;
                var request=new UniversalRenderPipeline.SingleCameraRequest { destination=rt };
                if (!RenderPipeline.SupportsRenderRequest(camera,request)) throw new InvalidOperationException("URP render request unsupported");
                RenderPipeline.SubmitRenderRequest(camera,request);
                RenderTexture.active=rt;
                var image=new Texture2D(Width,Height,TextureFormat.RGBA32,false,false);
                image.ReadPixels(new Rect(0,0,Width,Height),0,0); image.Apply();
                return image;
            }
            finally { camera.targetTexture=null; RenderTexture.active=old; rt.Release(); Object.DestroyImmediate(rt); }
        }
        static int Pixel(Camera camera, Transform chip, Vector3 point)
        {
            var screen=camera.WorldToViewportPoint(chip.TransformPoint(point));
            int x=Mathf.Clamp((int)(screen.x*Width),0,Width-1), y=Mathf.Clamp((int)(screen.y*Height),0,Height-1);
            return y*Width+x;
        }
        static bool Near(Color32 p, Color color)
        {
            return Mathf.Abs(p.r-color.r*255)<10 && Mathf.Abs(p.g-color.g*255)<10 && Mathf.Abs(p.b-color.b*255)<10;
        }
        static void Require(bool value,string message)
        {
            if (!value) throw new InvalidOperationException("chip pixel check: "+message);
            Debug.Log("[MP] chip PASS "+message);
        }
        static void Verify(Texture2D image,Texture2D reference,Camera camera,MeshRenderer[] chips,int angle)
        {
            var pixels=image.GetPixels32(); var white=reference.GetPixels32();
            int magenta=0,partial=0,klein=0;
            var cobaltColor=OrionTokens.ToColor(OrionTokens.ColorPrintCobaltHex);
            var kleinColor=OrionTokens.ToColor(OrionTokens.ColorInkKleinHex);
            foreach (var p in pixels)
            {
                if (p.r>230 && p.g<26 && p.b>230) magenta++;
                if (p.a!=0 && p.a!=255) partial++;
                if (p.a==255 && Near(p,kleinColor)) klein++;
            }
            Require(magenta==0,"angle="+angle+" magenta=0");
            Require(partial==0,"binary alpha");
            for (int x=0;x<Width;x++) RequireBorder(pixels[x].a==0 && pixels[(Height-1)*Width+x].a==0);
            for (int y=0;y<Height;y++) RequireBorder(pixels[y*Width].a==0 && pixels[y*Width+Width-1].a==0);
            Require(klein>0,"Klein outline pixels="+klein);
            for (int i=0;i<chips.Length;i++)
            {
                var t=chips[i].transform;
                int center=Pixel(camera,t,new Vector3(0,ChipMeshBuilder.TileCenterY,ChipMeshBuilder.FrontZ));
                Require(pixels[center].a==255,"object alpha=1 chip="+i);
                int marks=0;
                foreach (int sx in new[] {-1,1}) foreach (int sy in new[] {-1,1})
                {
                    var point=new Vector3(sx*(ChipMeshBuilder.TileWidth/2+OrionTokens.StrokeLinePrimaryMeters*2),
                        ChipMeshBuilder.TileCenterY+sy*(ChipMeshBuilder.TileHeight/2+OrionTokens.StrokeLinePrimaryMeters),ChipMeshBuilder.Thickness);
                    int k=Pixel(camera,t,point);
                    if (Near(pixels[k],cobaltColor)) marks++;
                }
                Require(marks==4,"four cobalt corners chip="+i+" angle="+angle);
                if (angle==0) continue;
                int side=0,changed=0;
                for (int y=-8;y<=8;y++) for (int z=1;z<=3;z++)
                {
                    int k=Pixel(camera,t,new Vector3(-ChipMeshBuilder.TileWidth/2,ChipMeshBuilder.TileCenterY+y*ChipMeshBuilder.TileHeight/20,
                        ChipMeshBuilder.Thickness+ChipMeshBuilder.TileRaise*z/4));
                    if (pixels[k].a==255) side++;
                    if (Mathf.Abs(pixels[k].r-white[k].r)+Mathf.Abs(pixels[k].g-white[k].g)+Mathf.Abs(pixels[k].b-white[k].b)>8) changed++;
                }
                Require(side==51 && changed>=10,"4mm side texture chip="+i+" samples="+side+" textured="+changed);
            }
        }
        static void RequireBorder(bool value) { if (!value) throw new InvalidOperationException("background alpha must be zero"); }
    }
}

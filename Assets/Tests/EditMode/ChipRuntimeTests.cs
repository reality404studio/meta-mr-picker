using System.IO;
using MoodPicker.Folio;
using MoodPicker.Dab;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MoodPicker.Tests
{
    public class ChipRuntimeTests
    {
        [Test]
        public void UnityMeshAndRendererUseOneSubmeshAndOneSharedMaterial()
        {
            var mesh = ChipMesh.Create(); var go = new GameObject("ChipTest");
            try
            {
                var renderer = go.AddComponent<MeshRenderer>();
                var material = AssetDatabase.LoadAssetAtPath<Material>(ChipRender.MaterialPath);
                Assert.That(material, Is.Not.Null);
                renderer.sharedMaterial = material;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                Assert.That(mesh.subMeshCount, Is.EqualTo(1));
                Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(1));
                Assert.That(mesh.triangles.Length/3, Is.LessThanOrEqualTo(22));
                Assert.That(material.shader.name, Is.EqualTo("MoodPicker/OrionChip"));
                var block = new MaterialPropertyBlock();
                ChipMesh.Apply(renderer,block,null,12,true);
                renderer.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_ChipNumber"), Is.EqualTo(12));
                Assert.That(block.GetFloat("_HasLoop"), Is.EqualTo(1));
                Assert.That(renderer.sharedMaterial, Is.SameAs(material));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(mesh); }
        }

        [Test, Category("Gpu")]
        public void ThreeSamplesFrontAndThirtyDegreesPassPixelChecks()
        {
            Assert.That(ChipRender.Run(Path.Combine(Directory.GetCurrentDirectory(),"tools/web-orion/samples/chips"),
                Path.Combine(Directory.GetCurrentDirectory(),"Library/ChipGpuTests")), Is.True);
        }
    }
}

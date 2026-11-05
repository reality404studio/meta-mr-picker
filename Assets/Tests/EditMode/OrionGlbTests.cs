using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MoodPicker.Tests
{
    public class OrionGlbTests
    {
        const string AssetPath = "Assets/Models/ORION.glb";
        static readonly string[] Names = { "O1", "R", "I", "O2", "N" };

        [Test]
        public void OrionGlb_ImportsFiveSeparateNamedLetterChildren()
        {
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport
                | ImportAssetOptions.ForceUpdate);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath);
            Assert.That(prefab, Is.Not.Null,
                "ORION.glb must exist and import as a GameObject through glTFast.");
            var instance = Object.Instantiate(prefab);
            try
            {
                var children = instance.transform.Cast<Transform>().ToArray();
                Assert.That(children.Length, Is.EqualTo(Names.Length));
                Assert.That(children.Select(child => child.name), Is.EquivalentTo(Names));
                var meshes = instance.GetComponentsInChildren<MeshFilter>(true);
                Assert.That(meshes.Length, Is.EqualTo(Names.Length));
                Assert.That(meshes.Select(mesh => mesh.name), Is.EquivalentTo(Names));
                Assert.That(meshes.All(mesh => mesh.sharedMesh != null && mesh.sharedMesh.vertexCount > 0));
                Assert.That(meshes.Select(mesh => mesh.sharedMesh).Distinct().Count(), Is.EqualTo(Names.Length));
                Assert.That(instance.GetComponentsInChildren<MeshRenderer>(true).Length, Is.EqualTo(Names.Length));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}

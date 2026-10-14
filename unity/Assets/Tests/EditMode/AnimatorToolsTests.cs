using NUnit.Framework;
using UnityEngine;

namespace Oohra.Client.Tests
{
    /// <summary>Animace vlastních modelů: model bez Animatoru nebo bez parametrů nic nerozbije.</summary>
    public class AnimatorToolsTests
    {
        [Test]
        public void MissingAnimatorOrParameterIsIgnored()
        {
            Assert.IsFalse(AnimatorTools.Has(null, PlayerViews.SpeedParameter, AnimatorControllerParameterType.Float));
            AnimatorTools.SetFloat(null, PlayerViews.SpeedParameter, 1f);

            var go = new GameObject("Model");
            var animator = go.AddComponent<Animator>();
            Assert.IsFalse(AnimatorTools.Has(animator, PlayerViews.SleepParameter, AnimatorControllerParameterType.Bool));
            AnimatorTools.SetBool(animator, PlayerViews.SleepParameter, true);
            AnimatorTools.SetInt(animator, AnimalViews.StateParameter, 2);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void MarkedTintOnlyColorsMarkedParts()
        {
            var model = new GameObject("Postava");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.transform.SetParent(model.transform);
            body.AddComponent<SpeciesTint>();
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.transform.SetParent(model.transform);

            SpeciesTint.ApplyMarked(model, Color.red);

            var block = new MaterialPropertyBlock();
            body.GetComponent<Renderer>().GetPropertyBlock(block);
            Assert.AreEqual(Color.red, block.GetColor("_BaseColor"));
            head.GetComponent<Renderer>().GetPropertyBlock(block);
            Assert.IsTrue(block.isEmpty);
            Object.DestroyImmediate(model);
        }
    }
}

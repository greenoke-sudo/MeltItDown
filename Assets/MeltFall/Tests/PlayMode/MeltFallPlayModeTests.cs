using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeltFall.Tests
{
    /// <summary>
    /// PlayMode tests for <see cref="MeltableMaterial"/>: a matched liquid dissolves a piece
    /// down to cleared, while a wrong liquid (Ignore response) leaves integrity untouched.
    /// Data assets are built in-memory via reflection so nothing depends on authored assets.
    /// </summary>
    public sealed class MeltFallPlayModeTests
    {
        private static void SetPrivateField<T>(T target, string fieldName, object value)
        {
            FieldInfo field = typeof(T).GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "Field '" + fieldName + "' not found on " + typeof(T).Name);
            field.SetValue(target, value);
        }

        private static MaterialDefinition MakeMaterial(
            string id, float maxIntegrity, string correctLiquidId)
        {
            var material = ScriptableObject.CreateInstance<MaterialDefinition>();
            SetPrivateField(material, "id", id);
            SetPrivateField(material, "maxIntegrity", maxIntegrity);
            SetPrivateField(material, "correctLiquidId", correctLiquidId);
            SetPrivateField(material, "wrongLiquidResponse", WrongLiquidResponse.Ignore);
            SetPrivateField(material, "chipFraction", 0f);
            return material;
        }

        private static LiquidDefinition MakeLiquid(string id, string[] matches, float meltPower)
        {
            var liquid = ScriptableObject.CreateInstance<LiquidDefinition>();
            SetPrivateField(liquid, "id", id);
            SetPrivateField(liquid, "matchedMaterialIds", matches);
            SetPrivateField(liquid, "meltPower", meltPower);
            return liquid;
        }

        // Builds a fresh GameObject with a Rigidbody, a BoxCollider and a MeltableMaterial whose
        // 'def' is assigned before Awake runs. Yields one frame so Awake initializes integrity.
        private static IEnumerator SpawnPiece(MaterialDefinition def, System.Action<MeltableMaterial> ready)
        {
            // Create inactive so MeltableMaterial.Awake does NOT run until 'def' is assigned —
            // otherwise Awake reads a null def and integrity initializes to 0.
            var go = new GameObject("MeltablePiece");
            go.SetActive(false);
            go.AddComponent<Rigidbody>();
            go.AddComponent<BoxCollider>();
            var meltable = go.AddComponent<MeltableMaterial>();
            SetPrivateField(meltable, "def", def);

            // Activate → Awake runs now, with def set, so integrity = def.MaxIntegrity.
            go.SetActive(true);
            yield return null;

            ready(meltable);
        }

        [UnityTest]
        public IEnumerator MeltableMaterial_MatchedLiquid_MeltsToCleared()
        {
            MaterialDefinition sand = MakeMaterial("sand", 100f, "water");
            LiquidDefinition water = MakeLiquid("water", new[] { "sand" }, 60f);

            MeltableMaterial piece = null;
            yield return SpawnPiece(sand, m => piece = m);

            Assert.IsNotNull(piece);
            Assert.AreEqual(100f, piece.Integrity, 0.5f, "Integrity should initialize to maxIntegrity.");
            Assert.IsFalse(piece.IsCleared);

            // Track the empty signal (fires synchronously when integrity hits 0 — no time dependency,
            // so this is reliable even when the Editor is unfocused and Time.deltaTime is ~0).
            bool emptied = false;
            piece.Emptied += _ => emptied = true;

            float integrityBefore = piece.Integrity;
            piece.ApplyMelt(water, 200f); // 200 * 60 meltPower -> drives integrity to 0
            Assert.Less(piece.Integrity, integrityBefore, "Matched liquid should reduce integrity.");
            Assert.AreEqual(0f, piece.Integrity, 0.0001f, "Matched liquid should empty integrity.");
            Assert.IsTrue(emptied, "Emptied should fire when integrity reaches 0.");

            // The clear-out must have STARTED: colliders are disabled immediately so a shrinking
            // husk can never catch a falling gem (spec §7). (The visual shrink→IsCleared runs over
            // real time in Update; not asserted here because it depends on frame delta time.)
            var col = piece.GetComponent<Collider>();
            Assert.IsNotNull(col);
            Assert.IsFalse(col.enabled, "Colliders should be disabled the moment the piece empties.");

            if (piece != null)
            {
                Object.DestroyImmediate(piece.gameObject);
            }
            Object.DestroyImmediate(sand);
            Object.DestroyImmediate(water);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MeltableMaterial_WrongLiquid_IgnoreLeavesIntegrityUnchanged()
        {
            MaterialDefinition sand = MakeMaterial("sand", 100f, "water");
            // A non-matching liquid: it does not list "sand" among its matched ids.
            LiquidDefinition acid = MakeLiquid("acid", new[] { "metal" }, 60f);

            MeltableMaterial piece = null;
            yield return SpawnPiece(sand, m => piece = m);

            Assert.IsNotNull(piece);
            float integrityBefore = piece.Integrity;
            Assert.AreEqual(100f, integrityBefore, 0.5f);

            for (int i = 0; i < 5; i++)
            {
                piece.ApplyMelt(acid, 200f);
                yield return null;
            }

            // Ignore response -> integrity must not meaningfully drop.
            Assert.AreEqual(integrityBefore, piece.Integrity, 0.0001f,
                "Wrong liquid with Ignore response must not reduce integrity.");
            Assert.IsFalse(piece.IsCleared);

            if (piece != null)
            {
                Object.DestroyImmediate(piece.gameObject);
            }
            Object.DestroyImmediate(sand);
            Object.DestroyImmediate(acid);
        }
    }
}

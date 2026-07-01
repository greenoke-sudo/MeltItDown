using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MeltFall.Tests
{
    /// <summary>
    /// Pure-logic EditMode tests for the MELTFALL data + economy spine. No MonoBehaviour
    /// lifecycle is exercised here; ScriptableObject private [SerializeField] fields are set
    /// via reflection so the data behaves exactly as it would after Inspector authoring.
    /// </summary>
    public sealed class MeltFallEditModeTests
    {
        // ------------------------------------------------------------------------------------
        // Reflection helper: set a private (usually [SerializeField]) instance field by name.
        // ------------------------------------------------------------------------------------
        private static void SetPrivateField<T>(T target, string fieldName, object value)
        {
            FieldInfo field = typeof(T).GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "Field '" + fieldName + "' not found on " + typeof(T).Name);
            field.SetValue(target, value);
        }

        // ====================================================================================
        // FuelTank
        // ====================================================================================

        [Test]
        public void FuelTank_Initialize_FillsToMax()
        {
            var tank = new FuelTank();
            tank.Initialize(100f);

            Assert.AreEqual(100f, tank.Current, 0.0001f);
            Assert.AreEqual(100f, tank.Max, 0.0001f);
            Assert.IsFalse(tank.IsEmpty);
            Assert.AreEqual(1f, tank.Fraction, 0.0001f);
        }

        [Test]
        public void FuelTank_Consume_ReducesCurrentAndReportsConsumption()
        {
            var tank = new FuelTank();
            tank.Initialize(100f);

            bool consumed = tank.Consume(30f);

            Assert.IsTrue(consumed);
            Assert.AreEqual(70f, tank.Current, 0.0001f);
            Assert.IsFalse(tank.IsEmpty);
        }

        [Test]
        public void FuelTank_Consume_ClampsAtZeroAndIsEmpty()
        {
            var tank = new FuelTank();
            tank.Initialize(100f);

            bool consumed = tank.Consume(1000f);

            Assert.IsTrue(consumed);
            Assert.AreEqual(0f, tank.Current, 0.0001f);
            Assert.IsTrue(tank.IsEmpty);

            // A second consume on an empty tank does nothing.
            Assert.IsFalse(tank.Consume(10f));
            Assert.AreEqual(0f, tank.Current, 0.0001f);
        }

        [Test]
        public void FuelTank_IsLow_TrueWhenFractionAtOrBelowThreshold()
        {
            var tank = new FuelTank();
            tank.Initialize(100f);

            Assert.IsFalse(tank.IsLow(0.25f)); // Fraction 1.0 > 0.25

            tank.Consume(75f); // Fraction now 0.25
            Assert.IsTrue(tank.IsLow(0.25f)); // 0.25 <= 0.25

            tank.Consume(10f); // Fraction now 0.15
            Assert.IsTrue(tank.IsLow(0.25f));
        }

        [Test]
        public void FuelTank_ResetToStart_RestoresToMax()
        {
            var tank = new FuelTank();
            tank.Initialize(100f);
            tank.Consume(80f);
            Assert.AreEqual(20f, tank.Current, 0.0001f);

            tank.ResetToStart();

            Assert.AreEqual(100f, tank.Current, 0.0001f);
            Assert.IsFalse(tank.IsEmpty);
        }

        [Test]
        public void FuelTank_Emptied_FiresExactlyOnceWhenCrossingToZero()
        {
            var tank = new FuelTank();
            tank.Initialize(100f);

            int emptiedCount = 0;
            tank.Emptied += () => emptiedCount++;

            tank.Consume(60f);           // still fuel left
            Assert.AreEqual(0, emptiedCount);

            tank.Consume(1000f);         // crosses to zero -> fires once
            Assert.AreEqual(1, emptiedCount);

            tank.Consume(5f);            // already empty -> does not fire again
            Assert.AreEqual(1, emptiedCount);
        }

        // ====================================================================================
        // LiquidDefinition.Melts
        // ====================================================================================

        [Test]
        public void LiquidDefinition_Melts_MatchesConfiguredMaterialIds()
        {
            var liquid = ScriptableObject.CreateInstance<LiquidDefinition>();
            SetPrivateField(liquid, "id", "water");
            SetPrivateField(liquid, "matchedMaterialIds", new[] { "sand" });

            Assert.IsTrue(liquid.Melts("sand"));
            Assert.IsFalse(liquid.Melts("metal"));
            Assert.IsFalse(liquid.Melts(null));
            Assert.IsFalse(liquid.Melts(string.Empty));

            Object.DestroyImmediate(liquid);
        }

        // ====================================================================================
        // LevelDefinition.StarsForLanded
        // ====================================================================================

        [Test]
        public void LevelDefinition_StarsForLanded_MapsLandedGemsToStars()
        {
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            SetPrivateField(level, "starThreshold1", 1);
            SetPrivateField(level, "starThreshold2", 2);
            SetPrivateField(level, "starThreshold3", 3);

            Assert.AreEqual(0, level.StarsForLanded(0));
            Assert.AreEqual(1, level.StarsForLanded(1));
            Assert.AreEqual(2, level.StarsForLanded(2));
            Assert.AreEqual(3, level.StarsForLanded(3));
            Assert.AreEqual(3, level.StarsForLanded(4)); // clamp above threshold3

            Object.DestroyImmediate(level);
        }

        // ====================================================================================
        // MaterialDefinition
        // ====================================================================================

        [Test]
        public void MaterialDefinition_IgnoreResponse_HasZeroChipFraction()
        {
            var material = ScriptableObject.CreateInstance<MaterialDefinition>();
            SetPrivateField(material, "id", "sand");
            SetPrivateField(material, "maxIntegrity", 100f);
            SetPrivateField(material, "correctLiquidId", "water");
            SetPrivateField(material, "wrongLiquidResponse", WrongLiquidResponse.Ignore);
            SetPrivateField(material, "chipFraction", 0f);

            Assert.AreEqual("sand", material.Id);
            Assert.AreEqual(100f, material.MaxIntegrity, 0.0001f);
            Assert.AreEqual("water", material.CorrectLiquidId);
            Assert.AreEqual(WrongLiquidResponse.Ignore, material.WrongLiquidResponse);
            Assert.AreEqual(0f, material.ChipFraction, 0.0001f);

            Object.DestroyImmediate(material);
        }
    }
}

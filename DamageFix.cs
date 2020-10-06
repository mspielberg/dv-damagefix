using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using UnityEngine;

namespace DvMod.DamageFix
{
    public static class DamageFix
    {
        public static float GetStressDelay()
        {
            return Main.settings.stressDelay;
        }

        public static float GetStressThreshold()
        {
            return Main.settings.stressThresholdMultiplier;
        }

        public static IEnumerable<CodeInstruction> WaitTimeTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var inst in instructions)
            {
                if (inst.LoadsConstant(TrainStress.STRESS_CALCULATION_WAIT_TIME))
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DamageFix), nameof(DamageFix.GetStressDelay)));
                else
                    yield return inst;
            }
        }

        public static void PatchIterator(Harmony harmony, Type t, string name, Func<IEnumerable<CodeInstruction>, IEnumerable<CodeInstruction>> transpiler)
        {
            var assembly = t.Assembly;
            var iteratorTypeName = $"{t.Name}+<{name}>";
            Main.DebugLog($"Searching for type starting with {iteratorTypeName}");
            var iteratorType = assembly.GetTypes().First(t => t.FullName.StartsWith(iteratorTypeName));
            var moveNext = iteratorType.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic);
            harmony.Patch(moveNext, transpiler: new HarmonyMethod(transpiler.Method));
        }

        [HarmonyPatch(typeof(CarDamageModel), nameof(CarDamageModel.DamageCar))]
        public static class DamageCarPatch
        {
            public static void Postfix(CargoDamageModel __instance)
            {
                var car = TrainCar.Resolve(__instance.gameObject);
                var track = car.Bogies[0].track;
                var span = car.Bogies[0].traveller?.Span;
                var speed = car.GetForwardSpeed();
                Main.DebugLog($"{car.ID} damaged at {track?.logicTrack.ID} span {span}, speed={speed * 3.6f} km/h, time={Time.time}");
            }
        }

        [HarmonyPatch(typeof(CargoDamageModel), nameof(CargoDamageModel.ApplyNormalDamageToCargo))]
        public static class ApplyNormalDamageToCargoPatch
        {
            public static void Postfix(CargoDamageModel __instance)
            {
                var car = TrainCar.Resolve(__instance.gameObject);
                var track = car.Bogies[0].track;
                var span = car.Bogies[0].traveller?.Span;
                var speed = car.GetForwardSpeed();
                Main.DebugLog($"{car.ID} cargo damaged at {track?.logicTrack.ID} span {span}, speed={speed * 3.6f} km/h, time={Time.time}");
            }
        }

        [HarmonyPatch(typeof(TrainStress), nameof(TrainStress.DisableStressCheckForTwoSecondsCoro))]
        public static class DisableStressCheckForTwoSecondsCoroPatch
        {
            public static void Prepare(MethodBase original, Harmony harmony)
            {
                if (original != null)
                    return;
                PatchIterator(
                    harmony,
                    typeof(TrainStress),
                    nameof(TrainStress.DisableStressCheckForTwoSecondsCoro),
                    WaitTimeTranspiler);
            }
        }

        [HarmonyPatch(typeof(TrainStress), nameof(TrainStress.InitializeStressCoro))]
        public static class InitializeStressCoroPatch
        {
            public static void Prepare(MethodBase original, Harmony harmony)
            {
                if (original != null)
                    return;
                PatchIterator(
                    harmony,
                    typeof(TrainStress),
                    nameof(TrainStress.InitializeStressCoro),
                    WaitTimeTranspiler);
            }
        }

        [HarmonyPatch(typeof(TrainStress), nameof(TrainStress.FixedUpdate))]
        public static class FixedUpdatePatch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                bool first = true;
                foreach (var inst in instructions)
                {
                    if (first && inst.LoadsConstant(1f))
                    {
                        first = false;
                        yield return new CodeInstruction(
                            OpCodes.Call,
                            AccessTools.Method(typeof(DamageFix), nameof(DamageFix.GetStressThreshold)));
                    }
                    else
                    {
                        yield return inst;
                    }
                }
            }
        }
    }
}
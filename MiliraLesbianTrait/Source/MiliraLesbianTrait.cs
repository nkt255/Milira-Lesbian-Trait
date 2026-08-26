using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Verse;
using Verse.AI;

namespace MiliraLesbianTrait
{
    // =====================================================================
    // STARTUP
    // =====================================================================

    [StaticConstructorOnStartup]
    public static class ModStartup
    {
        static ModStartup()
        {
            try
            {
                Harmony harmony = new Harmony("goodabent.MiliraLesbianTrait");

                InstallPawnGeneratorPatch(harmony);
                InstallLovinPatch(harmony);
                InstallPregnancyPatches(harmony);
                InstallPregnancyGeneSetPatch(harmony);
                InstallPregnancyTabPatch(harmony);

            }
            catch (Exception ex)
            {
                Log.Error("[MiliraLesbianTrait] Ошибка установки Harmony-патчей: " + ex);
            }
        }

        private static void InstallPawnGeneratorPatch(Harmony harmony)
        {
            try
            {
                MethodInfo method = AccessTools.Method(
                    typeof(PawnGenerator),
                    "GeneratePawn",
                    new Type[] { typeof(PawnGenerationRequest) });

                if (method != null)
                {
                    harmony.Patch(
                        method,
                        postfix: new HarmonyMethod(
                            typeof(PawnGeneratorPatch),
                            nameof(PawnGeneratorPatch.Postfix)));
                }
            }
            catch (Exception ex)
            {
                Log.Error("[MiliraLesbianTrait] Ошибка PawnGenerator patch: " + ex);
            }
        }

        private static void InstallLovinPatch(Harmony harmony)
        {
            try
            {
                MethodInfo method = AccessTools.Method(
                    typeof(JobDriver_Lovin),
                    "MakeNewToils");

                if (method != null)
                {
                    harmony.Patch(
                        method,
                        postfix: new HarmonyMethod(
                            typeof(LovinPatch),
                            nameof(LovinPatch.Postfix)));
                }
            }
            catch (Exception ex)
            {
                Log.Error("[MiliraLesbianTrait] Ошибка Lovin patch: " + ex);
            }
        }

        private static void InstallPregnancyPatches(Harmony harmony)
        {
            try
            {
                Type pregnancyUtility = AccessTools.TypeByName("RimWorld.PregnancyUtility");

                if (pregnancyUtility == null)
                {
                    Log.Error("[MiliraLesbianTrait] PregnancyUtility НЕ НАЙДЕН.");
                    return;
                }

                MethodInfo[] methods = pregnancyUtility.GetMethods(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Static |
                    BindingFlags.Instance);

                foreach (MethodInfo method in methods)
                {
                    if (method.Name == "CanEverProduceChild" &&
                        method.ReturnType == typeof(AcceptanceReport) &&
                        HasAtLeastTwoPawnParameters(method))
                    {
                        harmony.Patch(
                            method,
                            postfix: new HarmonyMethod(
                                typeof(PregnancyPatch),
                                nameof(PregnancyPatch.CanEverProduceChildPostfix)));
                    }

                    // This is the method used by the pregnancy gene preview.
                    // Use __args instead of hard-coded parameter names so the
                    // patch is not dependent on the exact argument names in
                    // this RimWorld build.
                    if (method.Name == "GetInheritedGeneSet" &&
                        method.ReturnType == typeof(GeneSet) &&
                        HasAtLeastTwoPawnParameters(method))
                    {
                        harmony.Patch(
                            method,
                            postfix: new HarmonyMethod(
                                typeof(PregnancyPatch),
                                nameof(PregnancyPatch.GetInheritedGeneSetPostfix)));
                    }

                    if (method.Name == "GetInheritedGenes" &&
                        method.ReturnType == typeof(List<GeneDef>) &&
                        HasAtLeastTwoPawnParameters(method))
                    {
                        ParameterInfo[] parameters = method.GetParameters();

                        // RimWorld Biotech has used both a 2-argument wrapper
                        // and a 3-argument overload with a success flag.
                        // Patch BOTH so the vanilla pregnancy gene preview and
                        // any caller using the success overload get the same
                        // Milira GeneDef list.
                        if (parameters.Length == 2)
                        {
                            harmony.Patch(
                                method,
                                postfix: new HarmonyMethod(
                                    typeof(PregnancyPatch),
                                    nameof(PregnancyPatch.GetInheritedGenesPostfix2)));



                        }
                        else if (parameters.Length == 3)
                        {
                            harmony.Patch(
                                method,
                                postfix: new HarmonyMethod(
                                    typeof(PregnancyPatch),
                                    nameof(PregnancyPatch.GetInheritedGenesPostfix3)));



                        }
                    }

                    // v12: the birth dialog still showed "Age 200: +0%" in v9.
                    // That means the dialog's age factor is not necessarily the
                    // value returned by PregnancyUtility.GetBirthQualityFor().
                    // Install a second, broader patch below that targets the
                    // actual age-factor helper wherever the current RimWorld
                    // build keeps it.

                    // Keep a final safety net for the actual birth: even if
                    // another mod changes the inherited list between pregnancy
                    // preview and birth, Milira + Milira still gets only Milira
                    // genes.
                    if (method.Name == "ApplyBirthOutcome" &&
                        method.ReturnType == typeof(void) &&
                        HasAtLeastTwoPawnParameters(method))
                    {
                        harmony.Patch(
                            method,
                            prefix: new HarmonyMethod(
                                typeof(PregnancyPatch),
                                nameof(PregnancyPatch.ApplyBirthOutcomePrefix)));
                    }
                }

                InstallMiliraBirthAgeFactorPatches(harmony);
            }
            catch (Exception ex)
            {
                Log.Error("[MiliraLesbianTrait] Ошибка pregnancy patches: " + ex);
            }
        }

        private static void InstallMiliraBirthAgeFactorPatches(Harmony harmony)
        {
            try
            {
                int patched = 0;

                // IMPORTANT:
                // v10 searched only for type names containing "pregnan"/"birth".
                // The actual birth-quality age line is implemented by
                // RitualOutcomeComp_PawnAge, so v10 found ZERO candidates.
                //
                // We now patch the real UI/ritual component directly.

                MethodInfo countMethod = AccessTools.Method(
                    typeof(RitualOutcomeComp_PawnAge),
                    "Count");

                if (countMethod != null)
                {
                    harmony.Patch(
                        countMethod,
                        postfix: new HarmonyMethod(
                            typeof(PregnancyPatch),
                            nameof(PregnancyPatch.RitualPawnAgeCountPostfix)));

                    patched++;


                }
                else
                {
                    Log.Warning(
                        "[MiliraLesbianTrait v15] RitualOutcomeComp_PawnAge.Count НЕ НАЙДЕН.");
                }

                MethodInfo descMethod = AccessTools.Method(
                    typeof(RitualOutcomeComp_PawnAge),
                    "GetDesc");

                if (descMethod != null)
                {
                    harmony.Patch(
                        descMethod,
                        postfix: new HarmonyMethod(
                            typeof(PregnancyPatch),
                            nameof(PregnancyPatch.RitualPawnAgeGetDescPostfix)));

                    patched++;


                }
                else
                {
                    Log.Warning(
                        "[MiliraLesbianTrait v15] RitualOutcomeComp_PawnAge.GetDesc НЕ НАЙДЕН.");
                }

                MethodInfo qualityFactorMethod = AccessTools.Method(
                    typeof(RitualOutcomeComp_PawnAge),
                    "GetQualityFactor",
                    new Type[]
                    {
                        typeof(Precept_Ritual),
                        typeof(TargetInfo),
                        typeof(RitualObligation),
                        typeof(RitualRoleAssignments),
                        typeof(RitualOutcomeComp_Data)
                    });

                if (qualityFactorMethod != null)
                {
                    harmony.Patch(
                        qualityFactorMethod,
                        postfix: new HarmonyMethod(
                            typeof(PregnancyPatch),
                            nameof(PregnancyPatch.RitualPawnAgeGetQualityFactorPostfix)));

                    patched++;


                }
                else
                {
                    Log.Warning(
                        "[MiliraLesbianTrait v15] RitualOutcomeComp_PawnAge.GetQualityFactor НЕ НАЙДЕН.");
                }

                // The displayed factor and the actual birth outcome must agree.
                // Patch the real PregnancyUtility.GetBirthQualityFor method(s)
                // directly instead of guessing an "age factor" helper name.
                Type pregnancyUtility = AccessTools.TypeByName("RimWorld.PregnancyUtility");

                if (pregnancyUtility != null)
                {
                    MethodInfo[] methods = pregnancyUtility.GetMethods(
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.Static |
                        BindingFlags.Instance);

                    foreach (MethodInfo method in methods)
                    {
                        if (method.Name != "GetBirthQualityFor" ||
                            method.ReturnType != typeof(float) ||
                            !HasAtLeastOnePawnParameter(method))
                            continue;

                        try
                        {
                            harmony.Patch(
                                method,
                                postfix: new HarmonyMethod(
                                    typeof(PregnancyPatch),
                                    nameof(PregnancyPatch.GetBirthQualityForMiliraPostfix)));

                            patched++;




                        }
                        catch (Exception ex)
                        {
                            Log.Warning(
                                "[MiliraLesbianTrait v15] Не удалось пропатчить GetBirthQualityFor " +
                                method + ": " + ex.Message);
                        }
                    }
                }



            }
            catch (Exception ex)
            {
                Log.Error(
                    "[MiliraLesbianTrait v15] Ошибка установки birth-quality patches: " + ex);
            }
        }

        private static void InstallPregnancyTabPatch(Harmony harmony)
        {
            try
            {
                Type pregnancyType = AccessTools.TypeByName("RimWorld.Hediff_Pregnant");
                if (pregnancyType != null)
                {
                    MethodInfo postAdd = AccessTools.Method(pregnancyType, "PostAdd");
                    if (postAdd != null)
                    {
                        harmony.Patch(postAdd, postfix: new HarmonyMethod(typeof(PregnancyPatch), nameof(PregnancyPatch.HediffPregnantPostAddPostfix)));

                    }
                }
                Type tabType = AccessTools.TypeByName("RimWorld.ITab_GenesPregnancy");
                if (tabType != null)
                {
                    MethodInfo fillTab = AccessTools.Method(tabType, "FillTab");
                    if (fillTab != null)
                    {
                        harmony.Patch(fillTab, prefix: new HarmonyMethod(typeof(PregnancyPatch), nameof(PregnancyPatch.GenesPregnancyFillTabPrefix)));


                        MethodInfo pawnGetter = AccessTools.PropertyGetter(tabType, "PawnToShowInfoAbout");
                        if (pawnGetter != null)
                        {
                            harmony.Patch(pawnGetter, postfix: new HarmonyMethod(typeof(PregnancyPatch), nameof(PregnancyPatch.GenesPregnancyPawnGetterPostfix)));

                        }
                        else Log.Warning("[MiliraLesbianTrait v15] ITab_GenesPregnancy.PawnToShowInfoAbout getter NOT FOUND");

                        MethodInfo visibleGetter = AccessTools.PropertyGetter(tabType, "IsVisible");
                        if (visibleGetter != null)
                        {
                            harmony.Patch(visibleGetter, postfix: new HarmonyMethod(typeof(PregnancyPatch), nameof(PregnancyPatch.PregnancyTabVisiblePostfix)));

                        }
                        else Log.Warning("[MiliraLesbianTrait v15] ITab_GenesPregnancy.IsVisible getter NOT FOUND");
                    }
                }

                MethodInfo getGizmos = AccessTools.Method(typeof(HediffWithParents), "GetGizmos");
                if (getGizmos != null)
                {
                    harmony.Patch(getGizmos, prefix: new HarmonyMethod(typeof(PregnancyPatch), nameof(PregnancyPatch.HediffWithParentsGetGizmosPrefix)));

                }
            }
            catch (Exception ex) { Log.Error("[MiliraLesbianTrait v15] Ошибка установки pregnancy-tab patches: " + ex); }
        }

        private static void InstallPregnancyGeneSetPatch(Harmony harmony)
        {
            try
            {
                MethodInfo method = AccessTools.Method(
                    typeof(HediffWithParents),
                    "SetParents",
                    new Type[] { typeof(Pawn), typeof(Pawn), typeof(GeneSet) });

                if (method != null)
                {
                    harmony.Patch(
                        method,
                        prefix: new HarmonyMethod(
                            typeof(PregnancyPatch),
                            nameof(PregnancyPatch.SetParentsPrefix)),
                        postfix: new HarmonyMethod(
                            typeof(PregnancyPatch),
                            nameof(PregnancyPatch.SetParentsPostfix)));



                }
                else
                {
                    Log.Warning(
                        "[MiliraLesbianTrait v15] HediffWithParents.SetParents(Pawn, Pawn, GeneSet) НЕ НАЙДЕН.");
                }
            }
            catch (Exception ex)
            {
                Log.Error("[MiliraLesbianTrait] Ошибка HediffWithParents patch: " + ex);
            }
        }

        private static bool HasAtLeastOnePawnParameter(MethodInfo method)
        {
            foreach (ParameterInfo p in method.GetParameters())
            {
                if (typeof(Pawn).IsAssignableFrom(p.ParameterType))
                    return true;
            }

            return false;
        }

        private static bool HasAtLeastTwoPawnParameters(MethodInfo method)
        {
            int count = 0;

            foreach (ParameterInfo p in method.GetParameters())
            {
                if (typeof(Pawn).IsAssignableFrom(p.ParameterType))
                    count++;
            }

            return count >= 2;
        }
    }


    // =====================================================================
    // MILIRA UTILITY
    // =====================================================================

    public static class MiliraUtility
    {
        public static bool IsMilira(Pawn pawn)
        {
            return pawn != null &&
                   pawn.genes != null &&
                   pawn.genes.Xenotype != null &&
                   pawn.genes.Xenotype.defName == "MiliraXenotype";
        }

        public static bool IsMiliraPair(Pawn first, Pawn second)
        {
            return IsMilira(first) && IsMilira(second);
        }

        public static bool HasMiliraParent(Pawn first, Pawn second)
        {
            return IsMilira(first) || IsMilira(second);
        }

        public static XenotypeDef GetMiliraXenotype(Pawn first, Pawn second)
        {
            if (first != null &&
                first.genes != null &&
                first.genes.Xenotype != null &&
                first.genes.Xenotype.defName == "MiliraXenotype")
            {
                return first.genes.Xenotype;
            }

            if (second != null &&
                second.genes != null &&
                second.genes.Xenotype != null &&
                second.genes.Xenotype.defName == "MiliraXenotype")
            {
                return second.genes.Xenotype;
            }

            return DefDatabase<XenotypeDef>.GetNamedSilentFail("MiliraXenotype");
        }

        public static GeneSet MakeMiliraGeneSet(Pawn first, Pawn second)
        {
            XenotypeDef xenotype = GetMiliraXenotype(first, second);

            if (xenotype == null || xenotype.genes == null)
                return null;

            GeneSet result = new GeneSet();

            foreach (GeneDef gene in xenotype.genes)
            {
                if (gene != null &&
                    !result.GenesListForReading.Contains(gene))
                {
                    result.AddGene(gene);
                }
            }

            return result;
        }

        public static List<GeneDef> MakeMiliraGeneList(Pawn first, Pawn second)
        {
            XenotypeDef xenotype = GetMiliraXenotype(first, second);

            if (xenotype == null || xenotype.genes == null)
                return null;

            List<GeneDef> result = new List<GeneDef>();

            foreach (GeneDef gene in xenotype.genes)
            {
                if (gene != null && !result.Contains(gene))
                    result.Add(gene);
            }

            return result;
        }

        private static readonly Dictionary<int, int> processedLovinPairs =
            new Dictionary<int, int>();

        public static bool IsPregnant(Pawn pawn)
        {
            return pawn != null &&
                   pawn.health != null &&
                   pawn.health.hediffSet != null &&
                   pawn.health.hediffSet.HasHediff(HediffDefOf.Pregnant);
        }

        public static bool TryMarkLovinProcessed(Pawn first, Pawn second)
        {
            if (first == null || second == null)
                return false;

            int a = first.thingIDNumber;
            int b = second.thingIDNumber;
            int low = Math.Min(a, b);
            int high = Math.Max(a, b);
            int key = (low * 397) ^ high;
            int tick = Find.TickManager.TicksGame;

            int oldTick;
            if (processedLovinPairs.TryGetValue(key, out oldTick) && oldTick == tick)
                return false;

            processedLovinPairs[key] = tick;

            if (processedLovinPairs.Count > 256)
            {
                List<int> stale = new List<int>();
                foreach (KeyValuePair<int, int> pair in processedLovinPairs)
                {
                    if (pair.Value < tick - 600)
                        stale.Add(pair.Key);
                }
                foreach (int staleKey in stale)
                    processedLovinPairs.Remove(staleKey);
            }

            return true;
        }
    }


    // =====================================================================
    // PAWN GENERATOR
    // =====================================================================

    public static class PawnGeneratorPatch
    {
        public static void Postfix(Pawn __result)
        {
            try
            {
                if (__result == null ||
                    __result.RaceProps == null ||
                    !__result.RaceProps.Humanlike ||
                    !MiliraUtility.IsMilira(__result) ||
                    __result.story == null ||
                    __result.story.traits == null)
                    return;

                if (!__result.story.traits.HasTrait(TraitDefOf.Gay))
                {
                    __result.story.traits.GainTrait(
                        new Trait(TraitDefOf.Gay, 0));
                }
            }
            catch (Exception ex)
            {
                Log.Error("[MiliraLesbianTrait] Ошибка PawnGeneratorPatch: " + ex);
            }
        }
    }


    // =====================================================================
    // LOVIN
    // =====================================================================

    public static class LovinPatch
    {
        public static void Postfix(
            JobDriver_Lovin __instance,
            ref IEnumerable<Toil> __result)
        {
            try
            {
                if (__instance == null)
                    return;

                Pawn pawn = __instance.pawn;

                if (!MiliraUtility.IsMilira(pawn))
                    return;

                Job job = pawn.CurJob;

                if (job == null)
                    return;

                Pawn partner = job.targetA.Thing as Pawn;

                if (!MiliraUtility.IsMilira(partner))
                    return;

                __result = AddMiliraReproductionToil(__result, pawn);
            }
            catch (Exception ex)
            {
                Log.Error("[MiliraLesbianTrait] Ошибка LovinPatch: " + ex);
            }
        }

        private static IEnumerable<Toil> AddMiliraReproductionToil(
            IEnumerable<Toil> original,
            Pawn pawn)
        {
            foreach (Toil toil in original)
                yield return toil;

            yield return Toils_General.Do(delegate
            {
                try
                {
                    Job job = pawn.CurJob;

                    if (job == null)
                        return;

                    Pawn partner = job.targetA.Thing as Pawn;

                    if (!MiliraUtility.IsMiliraPair(pawn, partner))
                        return;

                    if (!MiliraUtility.TryMarkLovinProcessed(pawn, partner))
                        return;

                    // Milira + Milira gets TWO independent pregnancy rolls:
                    // one for each woman. This removes the old 50/50 "choose
                    // one mother" step, which could repeatedly favor only one
                    // partner.
                    //
                    // PawnUtility.Mated() uses the vanilla 50% pregnancy roll
                    // for the female argument. Calling it once per woman gives
                    // each woman her own independent 50% chance.
                    if (!MiliraUtility.IsPregnant(pawn))
                        PawnUtility.Mated(partner, pawn);

                    if (!MiliraUtility.IsPregnant(partner))
                        PawnUtility.Mated(pawn, partner);
                }
                catch (Exception ex)
                {
                    Log.Error(
                        "[MiliraLesbianTrait] Ошибка Milira reproduction Toil: " +
                        ex);
                }
            });
        }
    }


    // =====================================================================
    // PREGNANCY
    // =====================================================================

    public static class PregnancyPatch
    {
        private static bool birthQualityPatchReported;

        public static void ReportBirthQualityPatchHit(object[] args, float resultBefore, float resultAfter)
        {
            if (birthQualityPatchReported)
                return;

            Pawn mother = null;

            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    Pawn pawn = args[i] as Pawn;
                    if (pawn != null)
                    {
                        mother = pawn;
                        break;
                    }
                }
            }

            if (mother != null && MiliraUtility.IsMilira(mother))
            {
                birthQualityPatchReported = true;




            }
        }

        // ---------------------------------------------------------------
        // Milira + Milira compatibility
        // ---------------------------------------------------------------

        public static void CanEverProduceChildPostfix(
            Pawn first,
            Pawn second,
            ref AcceptanceReport __result)
        {
            if (MiliraUtility.IsMiliraPair(first, second))
                __result = AcceptanceReport.WasAccepted;
        }

        // ---------------------------------------------------------------
        // Pregnancy gene preview
        // ---------------------------------------------------------------
        // The previous version relied on parameter names ("father"/"mother").
        // That is fragile when the game's compiled signature differs.
        // Here we inspect __args and take the two Pawn arguments directly.

        public static void GetInheritedGeneSetPostfix(
            Pawn father,
            Pawn mother,
            ref GeneSet __result)
        {
            if (!MiliraUtility.HasMiliraParent(father, mother))
                return;

            GeneSet miliraGenes =
                MiliraUtility.MakeMiliraGeneSet(father, mother);

            if (miliraGenes != null)
            {
                __result = miliraGenes;




            }
        }

        // Vanilla Biotech uses this overload for the inherited-gene list.
        public static void GetInheritedGenesPostfix2(
            Pawn father,
            Pawn mother,
            ref List<GeneDef> __result)
        {
            if (!MiliraUtility.HasMiliraParent(father, mother))
                return;

            List<GeneDef> miliraGenes =
                MiliraUtility.MakeMiliraGeneList(father, mother);

            if (miliraGenes != null)
            {
                __result = miliraGenes;




            }
        }

        // Some Biotech builds also expose a 3-argument overload with a
        // success flag. Force both the returned list and success=true.
        public static void GetInheritedGenesPostfix3(
            Pawn father,
            Pawn mother,
            ref List<GeneDef> __result,
            ref bool success)
        {
            if (!MiliraUtility.HasMiliraParent(father, mother))
                return;

            List<GeneDef> miliraGenes =
                MiliraUtility.MakeMiliraGeneList(father, mother);

            if (miliraGenes != null)
            {
                __result = miliraGenes;
                success = true;




            }
        }

        private static bool TryGetMiliraPair(
            object[] args,
            out Pawn first,
            out Pawn second)
        {
            first = null;
            second = null;

            if (args == null)
                return false;

            for (int i = 0; i < args.Length; i++)
            {
                Pawn pawn = args[i] as Pawn;

                if (pawn == null)
                    continue;

                if (first == null)
                {
                    first = pawn;
                }
                else if (pawn != first)
                {
                    second = pawn;
                    break;
                }
            }

            return MiliraUtility.IsMiliraPair(first, second);
        }

        // ---------------------------------------------------------------
        // v9 pregnancy gene preview: patch the ACTUAL geneSet stored on the
        // pregnancy hediff.
        //
        // This is the important part for the "Inspect baby genes..." window:
        // that window reads HediffWithParents.geneSet, not a fresh call to
        // PregnancyUtility.GetInheritedGeneSet().
        // ---------------------------------------------------------------
        // v13: patch the actual Biotech pregnancy hediff and refresh it before the vanilla gene tab draws.
        public static void HediffPregnantPostAddPostfix(Hediff __instance)
        {
            try { PatchPregnancyHediffGeneSet(__instance); }
            catch (Exception ex) { Log.Error("[MiliraLesbianTrait v15] Hediff_Pregnant.PostAdd error: " + ex); }
        }

        public static void GenesPregnancyFillTabPrefix(object __instance)
        {
            try
            {

                LogTabDiagnostics(__instance);
                Pawn pawn = FindPawnInObject(__instance);
                Pawn selected = Find.Selector?.SingleSelectedThing as Pawn;

                if (pawn == null) pawn = selected;
                if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
                { Log.Warning("[MiliraLesbianTrait v15] FillTab: NO PAWN/HEALTH FOUND"); return; }
                int total = 0;
                foreach (Hediff h in pawn.health.hediffSet.hediffs)
                {
                    if (h is HediffWithParents hp)
                    {
                        total++;

                        bool changed = PatchPregnancyHediffGeneSet(h);

                    }
                }

            }
            catch (Exception ex) { Log.Error("[MiliraLesbianTrait v15] FillTab diagnostic error: " + ex); }
        }

        public static void GenesPregnancyPawnGetterPostfix(object __instance, ref Pawn __result)
        {
        }

        public static void PregnancyTabVisiblePostfix(object __instance, ref bool __result)
        {
            try { }
            catch (Exception ex) { Log.Error("[MiliraLesbianTrait v15] IsVisible diagnostic error: " + ex); }
        }

        private static void LogTabDiagnostics(object obj)
        {
        }

        private static bool PatchPregnancyHediffGeneSet(Hediff hediff)
        {
            if (hediff == null) return false;
            Type type = hediff.GetType();
            if (type.FullName != "RimWorld.Hediff_Pregnant" && !type.Name.Contains("Hediff_Pregnant")) return false;
            Pawn mother = GetPawnField(type, hediff, "mother");
            Pawn father = GetPawnField(type, hediff, "father");
            if (!MiliraUtility.HasMiliraParent(mother, father)) return false;
            GeneSet milira = MiliraUtility.MakeMiliraGeneSet(mother, father);
            if (milira == null) return false;
            FieldInfo geneField = FindField(type, "geneSet");
            if (geneField == null) { Log.Warning("[MiliraLesbianTrait v15] Hediff_Pregnant geneSet field НЕ НАЙДЕН."); return false; }
            GeneSet old = geneField.GetValue(hediff) as GeneSet;
            geneField.SetValue(hediff, milira);

            return true;
        }

        public static void HediffWithParentsGetGizmosPrefix(HediffWithParents __instance)
        {
            try
            {
                if (__instance == null) return;
                PatchPregnancyHediffGeneSet(__instance);
            }
            catch (Exception ex)
            {
                Log.Error("[MiliraLesbianTrait v15] HediffWithParents.GetGizmos error: " + ex);
            }
        }

        private static Pawn FindPawnInObject(object obj)
        {
            if (obj == null) return null;
            Type t = obj.GetType();
            while (t != null)
            {
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    try { Pawn pawn = f.GetValue(obj) as Pawn; if (pawn != null) return pawn; } catch { }
                foreach (PropertyInfo prop in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!typeof(Pawn).IsAssignableFrom(prop.PropertyType) || !prop.CanRead) continue;
                    try { Pawn pawn = prop.GetValue(obj, null) as Pawn; if (pawn != null) return pawn; } catch { }
                }
                t = t.BaseType;
            }
            return null;
        }

        private static Pawn GetPawnField(Type type, object obj, string name)
        {
            FieldInfo f = FindField(type, name);
            return f == null ? null : f.GetValue(obj) as Pawn;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            while (type != null)
            {
                FieldInfo f = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f != null) return f;
                type = type.BaseType;
            }
            return null;
        }

        [HarmonyPriority(Priority.Last)]
        public static void SetParentsPrefix(
            Pawn mother,
            Pawn father,
            ref GeneSet geneSet)
        {
            if (!MiliraUtility.HasMiliraParent(mother, father))
                return;

            GeneSet miliraGenes =
                MiliraUtility.MakeMiliraGeneSet(mother, father);

            if (miliraGenes != null)
            {
                // Change the actual argument BEFORE vanilla stores it in
                // HediffWithParents.geneSet.
                geneSet = miliraGenes;




            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void SetParentsPostfix(
            HediffWithParents __instance,
            Pawn mother,
            Pawn father,
            GeneSet geneSet)
        {
            if (__instance == null || !MiliraUtility.HasMiliraParent(mother, father))
                return;

            GeneSet miliraGenes =
                MiliraUtility.MakeMiliraGeneSet(mother, father);

            if (miliraGenes != null)
            {
                // Final safety net: the pregnancy hediff itself must contain
                // the Milira GeneSet because ITab_GenesPregnancy reads this
                // stored GeneSet.
                __instance.geneSet = miliraGenes;




            }
        }

        // ---------------------------------------------------------------
        // v9 birth quality: patch the final value, never the pawn's age.
        // ---------------------------------------------------------------
        // RimWorld's normal curve gives a mother aged 20-30 a +50 percentage
        // point birth-quality bonus. Milira can be 200+ chronological years
        // old, so the vanilla curve gives them 0%.
        //
        // v9 does NOT touch Pawn_AgeTracker. The real age remains 200 and all
        // normal age-dependent systems continue to see 200. We only add the
        // missing +0.50 to the value returned by GetBirthQualityFor().
        //
        // The mother is extracted from __args instead of relying on a parameter
        // named "mother". This is important because the compiled method
        // signature can use a different parameter name in another build.
        // v12: force the age contribution itself to +50% for Milira.
        // This is intentionally separate from the total birth-quality value.
        // ===============================================================
        // v12: ACTUAL birth-quality age factor used by the birth ritual UI.
        //
        // The real RimWorld implementation is RitualOutcomeComp_PawnAge.
        // We do NOT change the pawn's real age. We only make the curve evaluate
        // at human prime age 25 for Milira, which is exactly +50%.
        // ===============================================================

        public static void RitualPawnAgeCountPostfix(
            ref float __result,
            LordJob_Ritual ritual,
            RitualOutcomeComp_PawnAge __instance)
        {
            if (__instance == null || __instance.roleId != "mother")
                return;

            Pawn mother = ritual != null
                ? ritual.PawnWithRole("mother")
                : null;

            if (!MiliraUtility.IsMilira(mother))
                return;

            // Keep the displayed real biological age.
            __result = mother.ageTracker.AgeBiologicalYearsFloat;




        }

        public static void RitualPawnAgeGetDescPostfix(
            ref string __result,
            LordJob_Ritual ritual,
            RitualOutcomeComp_PawnAge __instance,
            string ___label)
        {
            if (__instance == null || __instance.roleId != "mother")
                return;

            Pawn mother = ritual != null
                ? ritual.PawnWithRole("mother")
                : null;

            if (!MiliraUtility.IsMilira(mother))
                return;

            // Human prime age 25 sits on the +50% plateau.
            float quality = 0.50f;
            string sign = "+";

            __result =
                ___label.CapitalizeFirst().Formatted(
                    mother.Named("PAWN")) +
                ": " +
                "OutcomeBonusDesc_QualitySingleOffset".Translate(
                    sign + quality.ToStringPercent()) +
                ".";




        }

        public static void RitualPawnAgeGetQualityFactorPostfix(
            ref QualityFactor __result,
            RitualRoleAssignments assignments,
            RitualOutcomeComp_PawnAge __instance,
            string ___label)
        {
            if (__instance == null || __instance.roleId != "mother")
                return;

            Pawn mother = assignments != null
                ? assignments.FirstAssignedPawn("mother")
                : null;

            if (!MiliraUtility.IsMilira(mother))
                return;

            if (__result == null)
                __result = new QualityFactor();

            __result.label =
                ___label.Formatted(mother.Named("PAWN"));

            __result.quality = 0.50f;
            __result.positive = true;

            // Show actual age, not the fake 25 used for the curve.
            __result.count =
                mother.ageTracker.AgeBiologicalYearsFloat.ToString("0");

            __result.qualityChange =
                "+50%";







        }

        // ===============================================================
        // v12: actual birth quality.
        //
        // The vanilla birth-quality curve gives Milira age 200 a 0% age
        // contribution. We add the missing +0.50 to the final value.
        // This is separate from the UI patch above so the real birth outcome
        // and the displayed factor both use the same rule.
        // ===============================================================

        public static void GetBirthQualityForMiliraPostfix(
            object[] __args,
            ref float __result)
        {
            Pawn mother = FindMiliraPawn(__args);

            if (mother == null)
                return;

            float before = __result;

            // Add exactly the missing +50 percentage points.
            // Do not touch other factors (doctor, room, bed, loved one).
            __result = Math.Min(__result + 0.50f, 1.0f);







        }

        private static Pawn FindMiliraPawn(object[] args)
        {
            if (args == null)
                return null;

            for (int i = 0; i < args.Length; i++)
            {
                Pawn pawn = args[i] as Pawn;
                if (pawn != null && MiliraUtility.IsMilira(pawn))
                    return pawn;
            }

            return null;
        }

        private static Pawn FindMiliraPawnInObject(object obj)
        {
            if (obj == null)
                return null;

            Type type = obj.GetType();

            while (type != null)
            {
                FieldInfo[] fields = type.GetFields(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance |
                    BindingFlags.DeclaredOnly);

                for (int i = 0; i < fields.Length; i++)
                {
                    if (!typeof(Pawn).IsAssignableFrom(fields[i].FieldType))
                        continue;

                    Pawn pawn = fields[i].GetValue(obj) as Pawn;
                    if (pawn != null && MiliraUtility.IsMilira(pawn))
                        return pawn;
                }

                type = type.BaseType;
            }

            return null;
        }

        public static void GetBirthQualityForPostfixV9(
            object[] __args,
            ref float __result)
        {
            Pawn mother = null;

            if (__args != null)
            {
                for (int i = 0; i < __args.Length; i++)
                {
                    Pawn pawn = __args[i] as Pawn;
                    if (pawn != null)
                    {
                        mother = pawn;
                        break;
                    }
                }
            }

            if (mother == null || !MiliraUtility.IsMilira(mother))
                return;

            // Vanilla age contribution is at most +50%. If another mod has
            // already supplied the full/near-full bonus, do not double it.
            float resultBefore = __result;

            if (__result < 0.5f)
                __result = Math.Min(__result + 0.5f, 1f);

            ReportBirthQualityPatchHit(__args, resultBefore, __result);
        }

        // ---------------------------------------------------------------
        // Final birth gene list
        // ---------------------------------------------------------------
        // ApplyBirthOutcome gets the inherited gene list immediately before
        // the baby is generated. Replace it here as a final guarantee.

        [HarmonyPriority(Priority.Last)]
        public static void ApplyBirthOutcomePrefix(
            Pawn geneticMother,
            Pawn father,
            ref List<GeneDef> genes)
        {
            if (!MiliraUtility.IsMiliraPair(geneticMother, father))
                return;

            List<GeneDef> miliraGenes =
                MiliraUtility.MakeMiliraGeneList(
                    geneticMother,
                    father);

            if (miliraGenes != null)
                genes = miliraGenes;
        }
    }

}
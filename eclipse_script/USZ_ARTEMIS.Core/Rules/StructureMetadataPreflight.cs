using System;
using System.Collections.Generic;
using System.Linq;

namespace USZ_ARTEMIS.Core.Rules
{
    public sealed class StructureMetadataSnapshot
    {
        public StructureMetadataSnapshot(
            string id,
            string volumeType,
            string structureCodeScheme,
            string structureCode,
            bool isApproved,
            bool isEmpty)
        {
            Id = id;
            VolumeType = volumeType;
            StructureCodeScheme = structureCodeScheme;
            StructureCode = structureCode;
            IsApproved = isApproved;
            IsEmpty = isEmpty;
        }

        public string Id { get; }
        public string VolumeType { get; }
        public string StructureCodeScheme { get; }
        public string StructureCode { get; }
        public bool IsApproved { get; }
        public bool IsEmpty { get; }
    }

    public sealed class RuleStructureUse
    {
        public RuleStructureUse(
            IEnumerable<string> inputIds,
            IEnumerable<string> outputIds,
            IEnumerable<string> geometryRequiredInputIds = null)
        {
            InputIds = (inputIds ?? Enumerable.Empty<string>()).ToList();
            OutputIds = (outputIds ?? Enumerable.Empty<string>()).ToList();
            GeometryRequiredInputIds =
                (geometryRequiredInputIds ?? Enumerable.Empty<string>()).ToList();
        }

        public IReadOnlyList<string> InputIds { get; }
        public IReadOnlyList<string> OutputIds { get; }
        public IReadOnlyList<string> GeometryRequiredInputIds { get; }
    }

    public enum StructureMetadataPreparationKind
    {
        CreateFromReference,
        CreateEmptyInputFromReference,
        SynchronizeExisting,
        CreateTemporary
    }

    public sealed class StructureMetadataPreparation
    {
        public StructureMetadataPreparation(
            string structureId,
            StructureMetadataPreparationKind kind,
            string expectedVolumeType)
        {
            StructureId = structureId;
            Kind = kind;
            ExpectedVolumeType = expectedVolumeType;
        }

        public string StructureId { get; }
        public StructureMetadataPreparationKind Kind { get; }
        public string ExpectedVolumeType { get; }
    }

    public sealed class StructureMetadataPreflightResult
    {
        internal StructureMetadataPreflightResult(
            IList<StructureMetadataPreparation> preparations,
            IList<string> errors,
            IList<string> warnings)
        {
            Preparations = new List<StructureMetadataPreparation>(preparations);
            Errors = new List<string>(errors);
            Warnings = new List<string>(warnings);
        }

        public IReadOnlyList<StructureMetadataPreparation> Preparations { get; }
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public bool CanApply => Errors.Count == 0;
    }

    public static class StructureMetadataPreflight
    {
        public static StructureMetadataPreflightResult Evaluate(
            IEnumerable<StructureMetadataSnapshot> referenceStructures,
            IEnumerable<StructureMetadataSnapshot> targetStructures,
            IEnumerable<RuleStructureUse> orderedRuleUses,
            IEnumerable<string> structureIdsRemovedBeforeRules,
            IEnumerable<string> temporaryOutputIds)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var preparations = new List<StructureMetadataPreparation>();

            var references = BuildLookup(referenceStructures, "base", errors);
            var targets = BuildLookup(targetStructures, "destination", errors);
            var removedIds = BuildIdSet(structureIdsRemovedBeforeRules);
            var temporaryIds = BuildIdSet(temporaryOutputIds);
            var availableIds = new HashSet<string>(
                targets.Keys.Where(id => !removedIds.Contains(id)),
                StringComparer.OrdinalIgnoreCase);
            var preparedStructureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var ruleUses =
                (orderedRuleUses ?? Enumerable.Empty<RuleStructureUse>()).ToList();
            var generatedIds = BuildIdSet(
                ruleUses.SelectMany(ruleUse => ruleUse.OutputIds));
            var producedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            int ruleNumber = 0;
            foreach (var ruleUse in ruleUses)
            {
                ruleNumber++;
                var geometryRequiredIds = BuildIdSet(
                    ruleUse.GeometryRequiredInputIds);

                foreach (string inputId in ruleUse.InputIds.Where(IsUsableId))
                {
                    string displayId = inputId.Trim();
                    string normalizedInputId = NormalizeId(inputId);
                    if (availableIds.Contains(normalizedInputId))
                    {
                        if (geometryRequiredIds.Contains(normalizedInputId) &&
                            !producedIds.Contains(normalizedInputId) &&
                            targets.TryGetValue(
                                normalizedInputId,
                                out StructureMetadataSnapshot target) &&
                            target.IsEmpty &&
                            IsSafetyCriticalMarginInputType(target.VolumeType))
                        {
                            errors.Add(
                                $"Rule {ruleNumber}: required margin input structure " +
                                $"'{displayId}' is empty. A {target.VolumeType?.Trim()} structure " +
                                "used for margin generation must contain geometry.");
                        }

                        continue;
                    }

                    PlanMissingInputPreparation(
                        ruleNumber,
                        displayId,
                        normalizedInputId,
                        references,
                        targets,
                        removedIds,
                        generatedIds,
                        preparedStructureIds,
                        preparations,
                        errors,
                        warnings);

                    if (preparedStructureIds.Contains(normalizedInputId))
                    {
                        availableIds.Add(normalizedInputId);
                    }
                }

                foreach (string outputId in ruleUse.OutputIds.Where(IsUsableId))
                {
                    string displayId = outputId.Trim();
                    string normalizedOutputId = NormalizeId(outputId);

                    if (preparedStructureIds.Add(normalizedOutputId))
                    {
                        PlanOutputPreparation(
                            displayId,
                            normalizedOutputId,
                            references,
                            targets,
                            removedIds,
                            temporaryIds,
                            preparations,
                            errors,
                            warnings);
                    }

                    availableIds.Add(normalizedOutputId);
                    producedIds.Add(normalizedOutputId);
                }
            }

            return new StructureMetadataPreflightResult(preparations, errors, warnings);
        }

        private static void PlanMissingInputPreparation(
            int ruleNumber,
            string displayId,
            string normalizedInputId,
            IReadOnlyDictionary<string, StructureMetadataSnapshot> references,
            IReadOnlyDictionary<string, StructureMetadataSnapshot> targets,
            ISet<string> removedIds,
            ISet<string> generatedIds,
            ISet<string> preparedStructureIds,
            ICollection<StructureMetadataPreparation> preparations,
            ICollection<string> errors,
            ICollection<string> warnings)
        {
            if (targets.ContainsKey(normalizedInputId) &&
                removedIds.Contains(normalizedInputId))
            {
                errors.Add(
                    $"Rule {ruleNumber}: required input structure '{displayId}' is removed before " +
                    "the rules run and is not produced by an earlier rule.");
                return;
            }

            if (generatedIds.Contains(normalizedInputId))
            {
                errors.Add(
                    $"Rule {ruleNumber}: required rule-generated input structure '{displayId}' " +
                    "is not produced by an earlier rule.");
                return;
            }

            if (!references.TryGetValue(
                    normalizedInputId,
                    out StructureMetadataSnapshot reference))
            {
                errors.Add(
                    $"Rule {ruleNumber}: required input structure '{displayId}' is missing from " +
                    "the destination structure set and has no matching structure in the base plan.");
                return;
            }

            if (string.IsNullOrWhiteSpace(reference.VolumeType))
            {
                errors.Add(
                    $"Rule {ruleNumber}: base-plan structure '{reference.Id}' has no Volume Type " +
                    "and cannot be used to create an empty destination input.");
                return;
            }

            if (IsSafetyCriticalMarginInputType(reference.VolumeType))
            {
                errors.Add(
                    $"Rule {ruleNumber}: required {reference.VolumeType.Trim()} input structure " +
                    $"'{displayId}' is missing from the destination structure set. Target and " +
                    "external structures are not created empty automatically.");
                return;
            }

            if (!preparedStructureIds.Add(normalizedInputId))
            {
                return;
            }

            preparations.Add(
                new StructureMetadataPreparation(
                    displayId,
                    StructureMetadataPreparationKind.CreateEmptyInputFromReference,
                    reference.VolumeType));
            warnings.Add(
                $"Destination input structure '{displayId}' was missing and was created empty " +
                "using metadata from the base plan.");
        }

        private static bool StructureCodesEqual(
            StructureMetadataSnapshot left,
            StructureMetadataSnapshot right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            return string.Equals(
                       left.StructureCodeScheme,
                       right.StructureCodeScheme,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       left.StructureCode,
                       right.StructureCode,
                       StringComparison.Ordinal);
        }

        public static bool IsSafetyCriticalMarginInputType(string volumeType)
        {
            if (string.IsNullOrWhiteSpace(volumeType))
            {
                return false;
            }

            switch (volumeType.Trim().ToUpperInvariant())
            {
                case "GTV":
                case "CTV":
                case "PTV":
                case "ITV":
                case "TREATED_VOLUME":
                case "IRRAD_VOLUME":
                case "EXTERNAL":
                    return true;
                default:
                    return false;
            }
        }

        private static void PlanOutputPreparation(
            string displayId,
            string normalizedOutputId,
            IReadOnlyDictionary<string, StructureMetadataSnapshot> references,
            IReadOnlyDictionary<string, StructureMetadataSnapshot> targets,
            ISet<string> removedIds,
            ISet<string> temporaryIds,
            ICollection<StructureMetadataPreparation> preparations,
            ICollection<string> errors,
            ICollection<string> warnings)
        {
            bool targetWillExist =
                targets.TryGetValue(normalizedOutputId, out StructureMetadataSnapshot target) &&
                !removedIds.Contains(normalizedOutputId);

            if (!references.TryGetValue(
                    normalizedOutputId,
                    out StructureMetadataSnapshot reference))
            {
                if (!temporaryIds.Contains(normalizedOutputId))
                {
                    errors.Add(
                        $"Rule output '{displayId}' has no matching structure in the base plan. " +
                        "Its Volume Type and Structure Code cannot be determined.");
                    return;
                }

                if (!targetWillExist)
                {
                    preparations.Add(
                        new StructureMetadataPreparation(
                            displayId,
                            StructureMetadataPreparationKind.CreateTemporary,
                            "CONTROL"));
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(reference.VolumeType))
            {
                errors.Add(
                    $"Base-plan structure '{reference.Id}' has no Volume Type and cannot be " +
                    "used to create or validate the destination structure.");
                return;
            }

            if (!targetWillExist)
            {
                preparations.Add(
                    new StructureMetadataPreparation(
                        displayId,
                        StructureMetadataPreparationKind.CreateFromReference,
                        reference.VolumeType));
                return;
            }

            if (target.IsApproved)
            {
                errors.Add(
                    $"Destination structure '{target.Id}' is approved and cannot be safely " +
                    "updated by its rule.");
            }

            if (!string.Equals(
                    reference.VolumeType,
                    target.VolumeType,
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"Volume Type mismatch for '{displayId}': base='{reference.VolumeType}', " +
                    $"destination='{target.VolumeType}'. ESAPI cannot change this value in place.");
            }

            if (!StructureCodesEqual(reference, target))
            {
                preparations.Add(
                    new StructureMetadataPreparation(
                        displayId,
                        StructureMetadataPreparationKind.SynchronizeExisting,
                        reference.VolumeType));
            }
        }

        private static Dictionary<string, StructureMetadataSnapshot> BuildLookup(
            IEnumerable<StructureMetadataSnapshot> structures,
            string label,
            ICollection<string> errors)
        {
            var lookup = new Dictionary<string, StructureMetadataSnapshot>(
                StringComparer.OrdinalIgnoreCase);

            foreach (StructureMetadataSnapshot structure in
                     structures ?? Enumerable.Empty<StructureMetadataSnapshot>())
            {
                if (structure == null || !IsUsableId(structure.Id))
                {
                    continue;
                }

                string normalizedId = NormalizeId(structure.Id);
                if (lookup.ContainsKey(normalizedId))
                {
                    errors.Add(
                        $"The {label} structure set contains more than one structure matching " +
                        $"ID '{structure.Id.Trim()}' after trimming and case-insensitive comparison.");
                    continue;
                }

                lookup.Add(normalizedId, structure);
            }

            return lookup;
        }

        private static HashSet<string> BuildIdSet(IEnumerable<string> ids)
        {
            return new HashSet<string>(
                (ids ?? Enumerable.Empty<string>())
                    .Where(IsUsableId)
                    .Select(NormalizeId),
                StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsUsableId(string id)
        {
            return !string.IsNullOrWhiteSpace(id);
        }

        private static string NormalizeId(string id)
        {
            return id.Trim();
        }
    }
}

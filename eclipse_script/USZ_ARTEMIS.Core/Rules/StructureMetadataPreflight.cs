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
            bool isApproved)
        {
            Id = id;
            VolumeType = volumeType;
            StructureCodeScheme = structureCodeScheme;
            StructureCode = structureCode;
            IsApproved = isApproved;
        }

        public string Id { get; }
        public string VolumeType { get; }
        public string StructureCodeScheme { get; }
        public string StructureCode { get; }
        public bool IsApproved { get; }
    }

    public sealed class RuleStructureUse
    {
        public RuleStructureUse(IEnumerable<string> inputIds, IEnumerable<string> outputIds)
        {
            InputIds = (inputIds ?? Enumerable.Empty<string>()).ToList();
            OutputIds = (outputIds ?? Enumerable.Empty<string>()).ToList();
        }

        public IReadOnlyList<string> InputIds { get; }
        public IReadOnlyList<string> OutputIds { get; }
    }

    public enum StructureMetadataPreparationKind
    {
        CreateFromReference,
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
            var preparedOutputIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            int ruleNumber = 0;
            foreach (var ruleUse in orderedRuleUses ?? Enumerable.Empty<RuleStructureUse>())
            {
                ruleNumber++;

                foreach (string inputId in ruleUse.InputIds.Where(IsUsableId))
                {
                    string normalizedInputId = NormalizeId(inputId);
                    if (!availableIds.Contains(normalizedInputId))
                    {
                        errors.Add(
                            $"Rule {ruleNumber}: required input structure '{inputId.Trim()}' " +
                            "will not be available when the rule runs.");
                    }
                }

                foreach (string outputId in ruleUse.OutputIds.Where(IsUsableId))
                {
                    string displayId = outputId.Trim();
                    string normalizedOutputId = NormalizeId(outputId);

                    if (preparedOutputIds.Add(normalizedOutputId))
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
                }
            }

            return new StructureMetadataPreflightResult(preparations, errors, warnings);
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

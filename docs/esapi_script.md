# Eclipse Script: Isocenter During Plan Copy

The **Copy original plan to new structure set** button copies the selected base plan
onto the image associated with the selected destination structure set. ARTEMIS
calls `Course.CopyPlanSetup(originalPlan, newStructureSet, null)` for this step.
The target image must be registered with the base plan's primary image, as
specified by the ESAPI 17.0 documentation for this overload.

## How the isocenter is propagated

ESAPI performs the copy between the registered images and determines the
isocenter positions of the copied beams. ARTEMIS does not calculate a
registration transform, select a particular registration, or place the
isocenter at the center of a destination target structure. The call passes
`null` for copy diagnostics, so this workflow does not display any diagnostics
from that operation.

If a treatment machine is selected, ARTEMIS replaces the copied beams for that
machine. It reads each copied beam's `IsocenterPosition` and passes that same
position when creating its replacement beam. The subsequent fit to the current
PTV union plus 5 mm changes jaws and arc apertures; it does not reposition the
isocenter.

The structure-set overload's ESAPI 17.0 documentation requires image
registration but does not specify how it chooses among multiple registrations
or describe the exact coordinate mapping. Verify the resulting beam isocenter
positions in Eclipse, particularly when multiple registrations exist. The
production target is ESAPI 18.1; confirm the copy behavior against its installed
Online Help and assemblies on the licensed Windows workstation.

## References

- Implementation: [`StartPage.PlanCopy.cs`](../esapi_script/USZ_ARTEMIS/UserControls/StartPage.PlanCopy.cs)
- ESAPI 17.0: [`Course.CopyPlanSetup`](../reference/esapi-docs-17.0/docs/VMS.TPS.Common.Model.API.Course.md)
- ESAPI 18.1 guide scope: [`reference/esapi-guide-18.1/README.md`](../reference/esapi-guide-18.1/README.md)

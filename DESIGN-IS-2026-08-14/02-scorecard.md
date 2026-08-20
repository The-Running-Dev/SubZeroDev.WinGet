# Dieter Rams scorecard

1. Good design is innovative — Score: 1/3
   Evidence: The site uses the stock Docusaurus classic information pattern with no project-owned visual or onboarding layer ([E1](01-evidence.md#e1---standard-inherited-presentation)).
   Justification: It imitates a familiar documentation pattern with only content variation, matching the score-1 anchor.

2. Good design makes a product useful — Score: 1/3
   Evidence: The task content exists, but the first install command precedes the source/authentication setup it currently needs and the homepage does not elevate one primary action ([E2](01-evidence.md#e2---primary-task-and-onboarding-order)).
   Justification: A new adopter must recover from an avoidable installation detour, so the task is supported but not direct.

3. Good design is aesthetic — Score: 1/3
   Evidence: The inherited visual system is coherent, but has 20 observed spacing values, 22 color values, one failing syntax token, and a visible integration error ([E4](01-evidence.md#e4---visual-system), [E6](01-evidence.md#e6---chrome-attention-and-failure-residue)).
   Justification: The combined inconsistencies exceed the score-2 allowance and include a jarring broken-state residue.

4. Good design makes a product understandable — Score: 1/3
   Evidence: Multiple labels do not match their destinations or semantics, generic `Read` repeats ten times, and several first-use terms are unexplained ([E5](01-evidence.md#e5---copy-clarity-and-behavior-mismatches)).
   Justification: More than three controls or labels need interpretation, and jargon is present, matching the score-1 anchor.

5. Good design is unobtrusive — Score: 2/3
   Evidence: Neutral chrome and zero idle animation keep the shell quiet, though mixed-audience homepage content and a visible Giscus error compete with the primary path ([E6](01-evidence.md#e6---chrome-attention-and-failure-residue)).
   Justification: The chrome remains visible but usually quiet; its content hierarchy, rather than decoration, causes the distraction.

6. Good design is honest — Score: 1/3
   Evidence: Important limitations are disclosed and no dark patterns exist, but multiple absolute claims and link-label mismatches remain ([E5](01-evidence.md#e5---copy-clarity-and-behavior-mismatches)).
   Justification: Two or more inflations trigger score 1 under the supplied rubric even without deceptive intent.

7. Good design is long-lasting — Score: 3/3
   Evidence: The neutral typography, restrained palette, and conventional documentation structure contain no obvious dated trend markers ([E9](01-evidence.md#e9---long-lasting-qualities)).
   Justification: The visual language should remain legible as current three years from now, despite a separate implementation-drift risk from the unpinned base image.

8. Good design is thorough down to the last detail — Score: 1/3
   Evidence: Empty, loading, and disabled states are missing; error treatment is rough; copy success is visual but not observably announced; focus is present ([E7](01-evidence.md#e7---states-and-accessibility)).
   Justification: Three required states are missing and two present states are incomplete, so the worst representative evidence fits score 1.

9. Good design is environmentally friendly — Score: 1/3
   Evidence: Initial JS is 794,156 compressed bytes, with 28 top-level requests, while idle animation is zero and dark/reduced-motion support exists ([E8](01-evidence.md#e8---weight-and-friction)).
   Justification: The 500 KB-2 MB bundle anchor forces score 1; motion and theme handling prevent an even worse result but cannot satisfy the under-500-KB score-2 threshold.

10. Good design is as little design as possible — Score: 0/3
    Evidence: The homepage has 23 repeated-destination patterns, 36 duplicate instances, 79 focus targets, and substantial content duplication with Getting Started ([E3](01-evidence.md#e3---structural-counts-and-duplication)).
    Justification: The page is dominated by duplicated affordances and repeated onboarding content, matching the score-0 anchor.

## Total: 12/30

Scoring used the lower score when evidence fell between anchors and the worst representative instance rather than the average.

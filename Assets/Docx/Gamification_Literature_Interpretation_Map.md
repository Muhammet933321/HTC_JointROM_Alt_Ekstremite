# Gamification Literature Interpretation Map

LastUpdated: 2026-04-23
Audience: Human reviewers and any future LLM working on this repository
Scope: Literature used to justify or limit the lower-extremity XR screening/gamification system
Status: Decision log, not a full systematic review

## Purpose

This file records:
- which papers were actually used as evidence,
- what each paper supports,
- what each paper does NOT support,
- what was criticized or weakened by the paper itself or by our reading of it,
- how those findings should be interpreted inside this project.

This is intentionally written so that another LLM can read it later and avoid repeating the same reasoning mistakes.

## Global Interpretation Rules

1. This project should be described as a movement-risk screening system, not a diagnosis system.
2. Stronger claims require stronger evidence. Prospective cohorts and systematic reviews outrank current-concepts or narrative reviews.
3. A literature-backed task is not automatically a literature-backed metric. The task and the measured variable must both match.
4. Published cutoffs should not be copied across sport, sex, age, or test variants unless the paper directly validates that transfer.
5. Y-Balance style findings do NOT justify using our anterior-reach-only implementation as a stand-alone injury predictor.
6. LESS literature does NOT justify claiming that our system performs a full LESS score, because our code does not score the full LESS rubric.
7. High knee abduction moment, EMG activation patterns, and full trunk-control lab measures are not directly measured by the current code. They can only be used as mechanistic support, not as direct output labels.
8. Single-task interpretation is weak. The defensible position is combined pattern screening across valgus, flexion, balance, sway, reach, and asymmetry proxies.
9. If implementation and literature diverge, implementation defines what the product actually measures, and literature defines what claims are safe.

## Current Measurement Reality In This Repo

Current lower-limb gamification code directly or approximately measures:
- knee valgus proxy,
- knee flexion,
- pelvis sway RMS / sway velocity,
- symmetry proxy,
- anterior reach percentage proxy.

Current lower-limb gamification code does NOT directly measure:
- ACL injury probability,
- knee abduction moment (KAM),
- full LESS item score,
- EMG muscle activation,
- cutting mechanics,
- true gait spatiotemporal metrics,
- direct trunk displacement angles in the current lean task pipeline.

## Evidence Records

### Record 1

PMID: 18539658
Title: Non-contact ACL injuries in female athletes: an International Olympic Committee current concepts statement
Year: 2008
Type: Review / consensus-style current concepts statement
Population focus: Young athletes, especially female athletes in sports such as basketball and team handball

What it supports:
- Dynamic valgus and landing mechanics matter in ACL injury mechanisms.
- Prevention programs that emphasize landing softly, more knee and hip flexion, two-foot landing when possible, and avoiding dynamic valgus are sensible.
- Hip/trunk neuromuscular and proprioceptive training can reduce injury risk.

What it weakens or pushes back on:
- It does not support passive, single-metric screening claims.
- It does not say that one simple field test alone can diagnose future ACL injury.

How we use it in this project:
- Supports the inclusion of LandingScreen as a clinically intuitive movement-quality task.
- Supports using knee-over-toe alignment and flexed landing as coaching targets.

What it does NOT justify in this repo:
- It does not validate our exact thresholds.
- It does not validate direct ACL risk labeling from our system.
- It does not validate Y-Balance or single-leg squat as stand-alone predictors.

Interpretation strength for this project: Moderate

### Record 2

PMID: 17468378
Title: Deficits in neuromuscular control of the trunk predict knee injury risk: a prospective biomechanical-epidemiologic study
Year: 2007
Type: Prospective cohort study
Population focus: 277 collegiate athletes

What it supports:
- Trunk control matters for knee, ligament, and ACL injury risk.
- Lateral trunk displacement was the strongest predictor among the trunk variables in that study.
- Core stability variables can contribute meaningful injury-risk information, especially in female athletes.

What it weakens or pushes back on:
- It weakens a purely knee-only view of injury risk.
- It implies that proximal control matters and that lower-limb-only interpretation can miss relevant context.

How we use it in this project:
- Supports keeping trunk/lean style tasks in the protocol as supportive context.
- Supports future expansion toward direct trunk-angle or trunk-displacement measurement.

What it does NOT justify in this repo:
- Our current lean tasks are only indirect because the current code does not directly compute trunk displacement in the same way as the study.
- This paper does not validate our present lean-task output as an injury classifier.

Interpretation strength for this project: Moderate for concept, Low-to-Moderate for current implementation match

### Record 3

PMID: 20595554
Title: Development and validation of a clinic-based prediction tool to identify female athletes at high risk for anterior cruciate ligament injury
Year: 2010
Type: Validation study / clinic-based prediction model
Population focus: Female athletes (basketball, soccer, volleyball)

What it supports:
- Clinic-style surrogate measures can approximate high KAM status.
- Knee valgus motion and knee flexion range are relevant screening variables.
- Simple tools can be used to route high-risk athletes toward neuromuscular training.

What it weakens or pushes back on:
- It weakens the idea that only a biomechanics lab can screen useful landing risk factors.
- It still centers on predicting high KAM status, not direct injury diagnosis.

How we use it in this project:
- Supports using valgus and flexion as the core observable variables in LandingScreen and squat-style tasks.
- Supports the general clinic-friendly design philosophy of the gamification system.

What it does NOT justify in this repo:
- Our code does not measure KAM.
- Our code does not reproduce the exact clinic algorithm from the paper.
- The study is female-athlete specific, so broad generalization across all users should be cautious.

Interpretation strength for this project: Moderate

### Record 4

PMID: 26542164
Title: Sex Differences in Landing Biomechanics and Postural Stability During Adolescence: A Systematic Review with Meta-Analyses
Year: 2016
Type: Systematic review with meta-analyses
Population focus: Adolescent athletes

What it supports:
- With maturation, adolescent females show increased knee valgus during landing tasks.
- Adolescence is an important period for neuromuscular and landing-pattern divergence.

What it weakens or pushes back on:
- It explicitly reports low overall methodological quality of included studies.
- It reports no consensus on sex differences in postural stability.

How we use it in this project:
- Supports placing landing mechanics near the center of a youth screening battery.
- Supports caution when using simple balance measures as strong stand-alone evidence.

What it does NOT justify in this repo:
- It does not justify strong balance-only claims.
- It does not justify a universal sex-based rule inside the software.

Interpretation strength for this project: Moderate for landing rationale, Low for strong balance claims

### Record 5

PMID: 28658071
Title: A Review of Field-Based Assessments of Neuromuscular Control and Their Utility in Male Youth Soccer Players
Year: 2019
Type: Review
Population focus: Male youth soccer players

What it supports:
- Field-based neuromuscular screening is relevant in youth sport settings.
- Trunk dominance, ligament dominance, leg dominance, and reduced dynamic stability are useful conceptual categories.
- The review specifically says LESS is the only method validated in male youth soccer players at that time.
- It notes Y-Balance anterior asymmetry may be promising but needs more support in soccer.

What it weakens or pushes back on:
- It pushes back against overclaiming from field tests in male youth soccer because validation is limited.
- It weakens direct transfer of evidence from adult or female cohorts into male youth soccer.

How we use it in this project:
- Supports the overall use of field-style screening tasks.
- Supports using Y-Balance style reach as supportive information rather than sole classification.
- Supports keeping landing mechanics stronger than Y-Balance when discussing evidence strength.

What it does NOT justify in this repo:
- It does not justify claiming that our Y-Balance implementation is strongly validated in all youth populations.
- It does not justify calling our landing task a validated LESS replacement.

Interpretation strength for this project: Moderate

### Record 6

PMID: 32951976
Title: Factors influencing the Landing Error Scoring System: Systematic review with meta-analysis
Year: 2021
Type: Systematic review with meta-analysis
Population focus: Mixed athletic populations across included studies

What it supports:
- Females tend to show higher LESS scores than males.
- Previous ACL injury is associated with higher LESS scores.
- Neuromuscular training programs of at least six weeks can improve LESS scores.

What it weakens or pushes back on:
- The paper explicitly states the evidence quality is very low by GRADE.
- It weakens overconfidence in LESS as a precise universal decision tool.

How we use it in this project:
- Supports landing-quality training and re-testing logic.
- Supports treating landing movement quality as modifiable.

What it does NOT justify in this repo:
- We do not compute LESS.
- We should not claim equivalence between our LandingScreen and a validated LESS workflow.
- We should not present landing scores as high-certainty evidence.

Interpretation strength for this project: Low-to-Moderate

### Record 7

PMID: 32362482
Title: Can the Y balance test identify those at risk of contact or non-contact lower extremity injury in adolescent and collegiate Gaelic games?
Year: 2020
Type: Prospective cohort study
Population focus: 636 male adolescent and collegiate Gaelic footballers and hurlers

What it supports:
- Y-Balance can provide normative and descriptive balance information.
- Y-Balance may be useful as a preliminary screen to identify some athletes who are likely not at risk.

What it weakens or pushes back on:
- It explicitly says Y-Balance as a sole screening method is questionable.
- It explicitly says generalizing published cutoffs from other sports is not supported.

How we use it in this project:
- Strong reason not to use Y-Balance-style outputs as a stand-alone injury-risk label.
- Strong reason not to import cutoffs from other papers into our project without local validation.

What it does NOT justify in this repo:
- It does not justify our current anterior-reach threshold as a universal injury cutoff.
- It does not justify one-test screening decisions.

Interpretation strength for this project: High as a cautionary paper

### Record 8

PMID: 34801389
Title: The association between Y-balance test scores, injury, and physical performance in elite adolescent Australian footballers
Year: 2022
Type: Prospective cohort study
Population focus: 257 elite adolescent male Australian football athletes

What it supports:
- In isolation, mYBT was not useful for identifying injury risk.
- Some asymmetry findings may interact with performance context, especially agility.
- Y-Balance style results have small relationships with some physical performance measures.

What it weakens or pushes back on:
- It directly weakens the idea that isolated mYBT metrics are sufficient for injury-risk identification.
- It pushes back against simplistic one-variable prediction claims.

How we use it in this project:
- Supports keeping ModifiedYBalanceAnterior as a supporting task, not a primary single-task classifier.
- Supports interpreting asymmetry or reach deficits in context with the rest of the battery.

What it does NOT justify in this repo:
- It does not justify using our simplified anterior-only version as a strong injury predictor.
- It does not justify ignoring sport-specific context.

Interpretation strength for this project: High as a cautionary paper, Low for stand-alone predictive justification

### Record 9

PMID: 39160505
Title: Muscle activation in the lower limb muscles in individuals with dynamic knee valgus during single-leg and overhead squats: a meta-analysis study
Year: 2024
Type: Systematic review / meta-analysis
Population focus: Individuals with dynamic knee valgus; only four papers and 130 participants in total

What it supports:
- Single-leg squat and related squat patterns are biomechanically meaningful contexts for dynamic knee valgus.
- DKV is associated with altered muscle-activation patterns and likely compensatory strategies.
- Squat-based tasks are clinically relevant for identifying movement-control deficits.

What it weakens or pushes back on:
- This is not a prospective injury-prediction study.
- The included evidence base is small.
- The paper supports mechanism more than direct screening accuracy.

How we use it in this project:
- Supports including SingleLegSquat_R and SingleLegSquat_L as movement-quality tasks.
- Supports targeted exercise or coaching recommendations when valgus patterns appear in squat tasks.

What it does NOT justify in this repo:
- It does not justify direct injury prediction from our squat task alone.
- It does not justify EMG-based claims because our system does not record EMG.

Interpretation strength for this project: Moderate for mechanistic support, Low for predictive claims

### Record 10

PMID: 41635594
Title: Current Concepts in Hip and Core Assessment to Reduce the Risk of ACL Injury
Year: 2026
Type: Current concepts review / narrative synthesis
Population focus: Broad ACL prevention and rehabilitation literature

What it supports:
- Hip and trunk dysfunction are modifiable contributors to lower-limb mechanics tied to ACL risk.
- Single-leg step-down, Y-Balance, modified Star Excursion, hop symmetry, and strength assessment are repeatedly discussed as useful functional assessment tools.
- Multi-component hip/core programs may reduce injury burden in youth cohorts.

What it weakens or pushes back on:
- It is a current-concepts review, not a new validation study.
- It explicitly says trunk-specific evaluation tools and dosing parameters still need refinement.

How we use it in this project:
- Useful as a synthesis paper for explaining why trunk, hip, reach, and single-leg control all belong in one battery.
- Useful for explaining why the software should suggest training or follow-up, not make a diagnosis.

What it does NOT justify in this repo:
- It does not validate our exact implementation.
- It does not override stronger caution from prospective Y-Balance studies.

Interpretation strength for this project: Moderate as synthesis, Low as stand-alone proof

## Task-to-Evidence Mapping

### LandingScreen

Primary support:
- PMID 18539658
- PMID 20595554
- PMID 26542164
- PMID 32951976

Safe claim:
- Landing mechanics, valgus control, and landing flexion are clinically meaningful movement-quality signals and reasonable screening targets.

Unsafe claim:
- Our LandingScreen equals LESS.
- Our LandingScreen diagnoses ACL injury risk.

### ModifiedYBalanceAnterior_R / ModifiedYBalanceAnterior_L

Primary support:
- PMID 28658071
- PMID 32362482
- PMID 34801389
- PMID 41635594

Safe claim:
- Anterior reach asymmetry or reduced reach can be used as supportive dynamic balance information in a broader screening battery.

Unsafe claim:
- Anterior reach alone predicts future injury.
- Published cutoffs transfer directly to this project.
- Our simplified reach proxy equals full Y-Balance clinical validity.

### SingleLegSquat_R / SingleLegSquat_L

Primary support:
- PMID 39160505
- PMID 41635594
- PMID 20595554 (indirectly through valgus/flexion clinic logic)

Safe claim:
- Single-leg squat is a meaningful task for observing dynamic valgus and lower-limb control patterns.

Unsafe claim:
- Single-leg squat alone predicts ACL or lower-extremity injury in this exact implementation.

### LeanRight / LeanLeft / LeanForward

Primary support:
- PMID 17468378
- PMID 41635594

Safe claim:
- Trunk-control challenges belong in a comprehensive movement screening concept.

Unsafe claim:
- The current lean implementation directly reproduces published trunk-risk measures.

### WalkSimulation

Current evidence status:
- Weak for the current implementation.

Why weak:
- The code does not yet measure true gait variables such as step width, cadence, stance time, swing time, foot progression, or pelvic drop.

Safe claim:
- Exploratory movement observation only.

Unsafe claim:
- Evidence-based gait injury screening.

## Explicit Exclusions From This Research Set

These PubMed pages were open during browsing but are unrelated to this project and must NOT be cited in future reasoning:

- PMID 17656778: Co-immunoprecipitation of protein complexes
- PMID 18550854: Differential use of SCL/TAL-1 DNA-binding domain in developmental hematopoiesis
- PMID 19680096: Histological changes of an injectable rhBMP-2/calcium phosphate cement in vertebroplasty of rhesus monkey
- PMID 22622521: Overview of LASSO-related penalized regression methods for quantitative trait mapping and genomic selection
- PMID 23821427: Magnetic resonance spectroscopy and molecular studies in ornithine transcarbamylase deficiency
- PMID 29430093: Grape seed extract: An innovation in remineralization

Reason for exclusion:
- They are unrelated search/browser artifacts and have no relevance to lower-extremity sports screening, ACL risk, landing mechanics, Y-Balance, squat control, or XR biomechanics.

## Search Pages That Were Discovery Context Only

These were browsing/search context pages, not direct evidence sources:
- Y-Balance test injury risk adolescent athletes
- Landing Error Scoring System injury risk review
- adolescent female athletes ACL risk review valgus
- gait asymmetry lower extremity injury risk review athletes
- adolescent gait variability injury risk review
- single leg squat review injury risk valgus adolescent
- high knee abduction moment clinic-based prediction female athletes

Rule:
- Future LLMs should cite the actual article records above, not the search pages.

## Safe Summary For Future Use

If another LLM needs a one-paragraph evidence summary for this repository, use this:

The strongest defensible position for this project is that it screens for lower-extremity movement-risk patterns using field-style tasks that are inspired by landing, balance, trunk-control, and single-leg-control literature. Landing and valgus/flexion observations are the strongest practical anchors. Y-Balance-style reach should be treated as supportive rather than stand-alone because prospective adolescent studies question its solo predictive value and reject simple cut-off transfer across sports. Single-leg squat is clinically meaningful for observing dynamic knee valgus patterns but is better supported as a mechanistic and movement-quality task than as a direct injury predictor. Trunk-control literature supports adding proximal-control context, but the current implementation does not yet reproduce direct trunk metrics from the classic prospective studies. Therefore the system should be described as a screening and exercise-guidance tool, not a diagnostic engine.
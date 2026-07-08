# Spec: Serverless IT Project Auto-Grading Platform

**Date:** 2026-07-08
**Status:** Draft

---

## Problem Statement

Lecturers spend significant time grading IT project documents and architecture diagrams for subjects such as SWD and SWR. This platform reduces grading effort by extracting structured evidence from submitted Word and Draw.io files, applying subject-specific rubrics, and generating AI-suggested scores and comments for lecturer approval.

---

## User Stories

- **[P1]** As a lecturer, I want to create/select a subject and upload that subject's rubric template so that grading follows the correct criteria for each course.
  Accepted when: a lecturer can upload a rubric `.docx`, the system validates required rubric-table columns, and the rubric is stored as normalized criteria with max scores.

- **[P1]** As a student, I want to upload my Word document and Draw.io diagram through a web interface so that my assignment can enter the grading workflow.
  Accepted when: the student can upload one `.docx` and one `.drawio` file for an assignment, both files are stored in Supabase Storage, and a submission record is created.

- **[P1]** As the system, I want to extract text sections from Word and diagram structure from Draw.io so that grading can use both document content and architecture relationships.
  Accepted when: the backend stores extracted document sections, diagram entities, diagram relationships, and extraction warnings for each submission.

- **[P1]** As a lecturer, I want AI to score each rubric criterion separately with comments and deduction reasons so that I can review concrete grading evidence.
  Accepted when: each criterion has `max_score`, `suggested_score`, `deductions`, `evidence`, `comment`, and a confidence/status value.

- **[P1]** As a lecturer, I want to edit AI-suggested scores and comments before publishing so that the official grade remains under human control.
  Accepted when: final scores are saved separately from AI suggestions, lecturer edits are persisted, and students only see published final results.

- **[P2]** As a lecturer, I want a split review screen showing the submitted content beside the AI grading result so that I can verify scores quickly.
  Accepted when: the review page displays extracted document/diagram evidence on one side and editable criterion scores/comments on the other.

- **[P2]** As an admin or lecturer, I want to see extraction and AI-processing errors so that invalid submissions can be fixed or reprocessed.
  Accepted when: failed or partial grading jobs show actionable error states and can be retried.

- **[P3]** _(out of scope - noted for future)_ Batch grading of entire classes, plagiarism detection, LMS integration, and automatic publishing without lecturer approval.

---

## Functional Requirements

1. FR-01: The web app must support authenticated lecturer and student roles.
2. FR-02: Lecturers must be able to create or select a subject before uploading a rubric template.
3. FR-03: The first supported rubric upload format must be `.docx`; the system must extract standardized rubric tables and normalize them into JSON criteria for grading.
4. FR-04: A rubric criterion row must include at minimum: criterion/question identifier, title, description, max score, grading guidance, and deduction rules or notes.
5. FR-05: Students must be able to upload exactly one `.docx` report and one `.drawio` diagram per submission attempt for the MVP.
6. FR-06: Uploaded files must be stored in Supabase Storage with metadata linked to subject, assignment, student, and submission.
7. FR-07: A backend job must extract structured text from `.docx` files according to known template sections.
8. FR-08: A backend job must parse Draw.io XML and extract nodes/entities, labels, connectors, and relationships.
9. FR-09: The grading job must combine rubric criteria, extracted document text, extracted diagram structure, and extraction warnings into a structured AI request.
10. FR-10: The AI response must be parsed into criterion-level grading records with suggested scores, comments, evidence, deductions, and confidence/status.
11. FR-11: AI-suggested scores must never overwrite lecturer-approved final scores.
12. FR-12: Lecturers must be able to edit each criterion score/comment and save a final grade.
13. FR-13: Students must only see results after the lecturer publishes the final grading result.
14. FR-14: The system must store audit data for file uploads, extraction results, AI model responses, lecturer edits, and publish events.
15. FR-15: The system must support re-running extraction and AI grading for a submission before final publication.

---

## Non-Functional Requirements

- Performance: For a single submission with one `.docx` file up to 20 MB and one `.drawio` file up to 10 MB, extraction plus AI grading should complete in under 5 minutes for at least 90% of submissions.
- Security: Students must only access their own submissions and published results; lecturers must only access subjects/assignments they are authorized to grade.
- Availability: The web app should remain usable for upload and review if AI grading is delayed; grading jobs may continue asynchronously.
- Reliability: All grading states must be explicit: `uploaded`, `extracting`, `extracted`, `grading`, `graded`, `reviewed`, `published`, or `failed`.
- Explainability: Every AI-suggested criterion score must include at least one evidence reference or an explicit reason why evidence was missing.
- Data retention: Original uploaded files, extracted artifacts, AI results, and final lecturer decisions must be retained for at least one academic term. [NEEDS CLARIFICATION: exact school retention policy]

---

## Success Criteria

- [ ] End-to-end grading: 1 lecturer can upload a rubric, 1 student can submit `.docx` + `.drawio`, and the system produces criterion-level AI suggestions without manual backend intervention.
- [ ] Human approval: 100% of student-visible grades require lecturer publication in MVP.
- [ ] Rubric structure: 100% of AI scores are bounded by each criterion's configured max score.
- [ ] Traceability: 100% of final criterion scores store both the AI suggestion and the lecturer-approved final value.
- [ ] Review efficiency: A lecturer can review and publish a graded submission in under 10 minutes after AI grading completes.
- [ ] Error visibility: 100% of failed extraction or grading jobs expose a visible failure reason in the lecturer interface.

---

## Out of Scope

- Fully autonomous publishing of grades without lecturer review.
- Plagiarism detection.
- LMS integration with systems such as Canvas, Moodle, or Google Classroom.
- Batch upload and batch grading for entire classes.
- Support for arbitrary document structures outside approved templates.
- Grading non-IT assignments unrelated to structured documents and architecture diagrams.

---

## Assumptions

- Supabase will be used for Auth, Postgres, Storage, and Edge Functions.
- React will be used for the web interface.
- OpenRouter will be used to access LLMs such as DeepSeek or Llama-family models.
- `.docx` and `.drawio` are the required submission formats for MVP.
- Rubric `.docx` files can follow a constrained table format defined by the system.
- AI is an assistant for scoring and feedback, while lecturers remain the final authority.

---

## [NEEDS CLARIFICATION]

- [ ] Exact MVP subject list: start with one subject such as SWD, one subject such as SWR, or both.
- [ ] Exact rubric `.docx` table schema and whether each row represents a criterion, sub-criterion, or question.
- [ ] Required school audit/data-retention policy for submitted files, AI outputs, and final grading decisions.

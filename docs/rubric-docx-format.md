# Rubric DOCX Format

Lecturers upload one Word `.docx` rubric template per subject or assignment.

The MVP parser expects the rubric to contain at least one table. The first valid table must have a header row and one row per criterion.

## Required Columns

| Column | Purpose | Example |
| --- | --- | --- |
| `Ma tieu chi` | Stable criterion or question code | `C1` |
| `Noi dung can cham` | Short criterion title | `Architecture consistency` |
| `Mo ta` | Detailed requirement | `Diagram components match the written design` |
| `Diem toi da` | Maximum score for this criterion | `4` |

## Optional Columns

| Column | Purpose | Example |
| --- | --- | --- |
| `Muc dat` | Guidance for awarding points | `Full score if all required services and relations exist` |
| `Loi tru diem` | Common deductions | `Missing relationship: -1` |

## Rules

- Keep one criterion per row.
- Use numeric max scores.
- Do not merge cells in the rubric table.
- Keep criterion codes stable across rubric versions when possible.
- Put explanatory notes outside the rubric table if they should not become criteria.

## Normalized Output

The parser converts each row into:

```json
{
  "criterionCode": "C1",
  "title": "Architecture consistency",
  "description": "Diagram components match the written design",
  "maxScore": 4,
  "gradingGuidance": "Full score if all required services and relations exist",
  "deductionNotes": "Missing relationship: -1",
  "displayOrder": 0
}
```

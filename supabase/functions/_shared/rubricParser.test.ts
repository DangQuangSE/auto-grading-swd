import { describe, expect, it } from "vitest";
import { parseRubricFromDocx } from "./rubricParser";

describe("parseRubricFromDocx", () => {
  it("normalizes rubric table rows into criteria", () => {
    const parsed = parseRubricFromDocx({
      sections: [],
      warnings: [],
      tables: [
        [
          ["Ma tieu chi", "Noi dung can cham", "Mo ta", "Diem toi da", "Muc dat", "Loi tru diem"],
          ["C1", "Architecture", "Diagram matches requirements", "4", "Clear components", "Missing relation -1"],
        ],
      ],
    });

    expect(parsed.criteria).toEqual([
      {
        criterionCode: "C1",
        title: "Architecture",
        description: "Diagram matches requirements",
        maxScore: 4,
        gradingGuidance: "Clear components",
        deductionNotes: "Missing relation -1",
        displayOrder: 0,
      },
    ]);
  });

  it("throws when required columns are missing", () => {
    expect(() =>
      parseRubricFromDocx({
        sections: [],
        warnings: [],
        tables: [[["Name", "Points", "Note", "Other"], ["A", "1", "", ""]]],
      }),
    ).toThrow(/missing required columns/i);
  });
});

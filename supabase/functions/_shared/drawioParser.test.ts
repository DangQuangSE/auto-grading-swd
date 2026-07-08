import { describe, expect, it } from "vitest";
import { parseDrawioXml } from "./drawioParser";

describe("parseDrawioXml", () => {
  it("extracts labeled entities and relationships", () => {
    const parsed = parseDrawioXml(`
      <mxfile>
        <diagram>
          <mxGraphModel>
            <root>
              <mxCell id="1" vertex="1" value="React Web" style="rounded=1;" />
              <mxCell id="2" vertex="1" value="Supabase" style="shape=database;" />
              <mxCell id="3" edge="1" source="1" target="2" value="stores files" />
            </root>
          </mxGraphModel>
        </diagram>
      </mxfile>
    `);

    expect(parsed.entities).toEqual([
      { id: "1", label: "React Web", type: "rounded=1" },
      { id: "2", label: "Supabase", type: "shape=database" },
    ]);
    expect(parsed.relationships).toEqual([
      { id: "3", sourceId: "1", targetId: "2", label: "stores files" },
    ]);
  });

  it("warns when the diagram is empty", () => {
    const parsed = parseDrawioXml("<mxfile />");
    expect(parsed.warnings).toContain("No labeled entities were extracted from the Draw.io diagram.");
    expect(parsed.warnings).toContain("No relationships/connectors were extracted from the Draw.io diagram.");
  });
});

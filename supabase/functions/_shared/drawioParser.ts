export type DrawioEntity = {
  id: string;
  label: string;
  type?: string;
};

export type DrawioRelationship = {
  id: string;
  sourceId?: string;
  targetId?: string;
  label?: string;
};

export type ParsedDrawio = {
  entities: DrawioEntity[];
  relationships: DrawioRelationship[];
  warnings: string[];
};

function decodeXml(value: string) {
  return value
    .replace(/&amp;/g, "&")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'");
}

function getAttribute(tag: string, name: string) {
  const match = tag.match(new RegExp(`${name}="([^"]*)"`));
  return match ? decodeXml(match[1]) : undefined;
}

export function parseDrawioXml(xml: string): ParsedDrawio {
  const warnings: string[] = [];
  const cells = Array.from(xml.matchAll(/<mxCell\b[^>]*>/g)).map((match) => match[0]);
  const entities: DrawioEntity[] = [];
  const relationships: DrawioRelationship[] = [];

  for (const cell of cells) {
    const id = getAttribute(cell, "id");
    if (!id) {
      continue;
    }

    const value = getAttribute(cell, "value")?.replace(/<[^>]+>/g, "").trim() ?? "";
    const sourceId = getAttribute(cell, "source");
    const targetId = getAttribute(cell, "target");
    const isEdge = getAttribute(cell, "edge") === "1";
    const isVertex = getAttribute(cell, "vertex") === "1";

    if (isEdge || sourceId || targetId) {
      relationships.push({
        id,
        sourceId,
        targetId,
        label: value || undefined,
      });
      continue;
    }

    if (isVertex && value) {
      entities.push({
        id,
        label: value,
        type: getAttribute(cell, "style")?.split(";")[0],
      });
    }
  }

  if (entities.length === 0) {
    warnings.push("No labeled entities were extracted from the Draw.io diagram.");
  }

  if (relationships.length === 0) {
    warnings.push("No relationships/connectors were extracted from the Draw.io diagram.");
  }

  return { entities, relationships, warnings };
}

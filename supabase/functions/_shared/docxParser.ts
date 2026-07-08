import { strFromU8, unzipSync } from "npm:fflate@0.8.2";

export type ParsedDocxSection = {
  heading: string;
  text: string;
  order: number;
};

export type ParsedDocx = {
  sections: ParsedDocxSection[];
  tables: string[][][];
  warnings: string[];
};

const WORD_DOCUMENT_PATH = "word/document.xml";

function textContent(xml: string, tagName: string) {
  const matches = Array.from(xml.matchAll(new RegExp(`<${tagName}[^>]*>(.*?)</${tagName}>`, "gs")));
  return matches
    .map((match) => match[1].replace(/<[^>]+>/g, ""))
    .map(decodeXml)
    .join("");
}

function decodeXml(value: string) {
  return value
    .replace(/&amp;/g, "&")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'");
}

function splitBlocks(xml: string, tagName: string) {
  return Array.from(xml.matchAll(new RegExp(`<w:${tagName}\\b[^>]*>(.*?)</w:${tagName}>`, "gs"))).map(
    (match) => match[1],
  );
}

function parseParagraphs(documentXml: string) {
  return splitBlocks(documentXml, "p")
    .map((paragraph) => textContent(paragraph, "w:t").trim())
    .filter(Boolean);
}

function parseTables(documentXml: string) {
  return splitBlocks(documentXml, "tbl").map((table) =>
    splitBlocks(table, "tr").map((row) =>
      splitBlocks(row, "tc").map((cell) => textContent(cell, "w:t").replace(/\s+/g, " ").trim()),
    ),
  );
}

function paragraphsToSections(paragraphs: string[]) {
  const sections: ParsedDocxSection[] = [];
  let current: ParsedDocxSection | null = null;

  for (const paragraph of paragraphs) {
    const looksLikeHeading =
      paragraph.length <= 90 &&
      (/^\d+(\.\d+)*\s+/.test(paragraph) || /^[A-Z][A-Za-z0-9 /&-]{2,}$/.test(paragraph));

    if (!current || looksLikeHeading) {
      current = {
        heading: paragraph,
        text: "",
        order: sections.length,
      };
      sections.push(current);
      continue;
    }

    current.text = [current.text, paragraph].filter(Boolean).join("\n\n");
  }

  return sections;
}

export async function parseDocx(buffer: ArrayBuffer): Promise<ParsedDocx> {
  const warnings: string[] = [];
  const entries = unzipSync(new Uint8Array(buffer));
  const documentEntry = entries[WORD_DOCUMENT_PATH];

  if (!documentEntry) {
    throw new Error("Invalid .docx file: word/document.xml was not found.");
  }

  const documentXml = strFromU8(documentEntry);
  const paragraphs = parseParagraphs(documentXml);
  const tables = parseTables(documentXml);

  if (paragraphs.length === 0) {
    warnings.push("No paragraph text was extracted from the Word document.");
  }

  if (tables.length === 0) {
    warnings.push("No tables were found in the Word document.");
  }

  return {
    sections: paragraphsToSections(paragraphs),
    tables,
    warnings,
  };
}

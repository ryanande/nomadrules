import { createHash } from 'crypto';

export interface DiffResult {
  hasChanged: boolean;
  currentHash: string;
  previousHash: string | null;
  changeRatio: number;
}

const CHANGE_THRESHOLD = 0.01;

export function computeHash(content: string): string {
  return createHash('sha256').update(content).digest('hex');
}

export function normalizeContent(html: string): string {
  return html
    .replace(/<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>/gi, '')
    .replace(/<style\b[^<]*(?:(?!<\/style>)<[^<]*)*<\/style>/gi, '')
    .replace(/<!--[\s\S]*?-->/g, '')
    .replace(/<[^>]+>/g, ' ')
    .replace(/&amp;/g, '&').replace(/&lt;/g, '<').replace(/&gt;/g, '>')
    .replace(/&nbsp;/g, ' ').replace(/&quot;/g, '"').replace(/&#\d+;/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

export function detectDiff(
  currentContent: string,
  previousContent: string | null,
  previousHash: string | null
): DiffResult {
  const currentHash = computeHash(currentContent);

  if (previousContent === null || previousHash === null) {
    return { hasChanged: false, currentHash, previousHash: null, changeRatio: 0 };
  }
  if (currentHash === previousHash) {
    return { hasChanged: false, currentHash, previousHash, changeRatio: 0 };
  }

  const currentTokens  = new Set(currentContent.split(/\s+/));
  const previousTokens = new Set(previousContent.split(/\s+/));
  const added   = [...currentTokens].filter(t => !previousTokens.has(t)).length;
  const removed = [...previousTokens].filter(t => !currentTokens.has(t)).length;
  const total   = Math.max(currentTokens.size, previousTokens.size, 1);
  const changeRatio = (added + removed) / (2 * total);

  return { hasChanged: changeRatio >= CHANGE_THRESHOLD, currentHash, previousHash, changeRatio };
}

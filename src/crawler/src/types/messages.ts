export interface LawChangeDetected {
  messageType: 'LawChangeDetected';
  messageId: string;
  sourceId: string;
  rawContent: string;
  contentHash: string;
  previousHash: string | null;
  url: string;
  state?: string;
  category: string;
  detectedAt: string;
}

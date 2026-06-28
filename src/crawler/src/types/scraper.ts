export type ScraperStrategy = 'html-diff' | 'rss' | 'pdf' | 'api';
export type LawCategory = 'insurance' | 'tax' | 'dmv' | 'voting' | 'business';

export interface ScrapeResult {
  sourceId: string;
  content: string;
  contentHash: string;
  url: string;
  scrapedAt: Date;
  strategy: ScraperStrategy;
}

export interface ISourceScraper {
  readonly sourceId: string;
  readonly schedule: string;
  readonly category: LawCategory;
  readonly state?: string;
  scrape(): Promise<ScrapeResult>;
}

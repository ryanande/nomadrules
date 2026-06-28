import axios from 'axios';
import type { ISourceScraper, LawCategory, ScrapeResult } from '../types/scraper.js';
import { computeHash } from '../core/diffEngine.js';
import { logger } from '../core/logger.js';

interface FRDocument {
  document_number: string;
  title: string;
  abstract: string | null;
  publication_date: string;
  html_url: string;
}

export class FederalRegisterScraper implements ISourceScraper {
  readonly sourceId = 'federal-register-health-insurance';
  readonly schedule = '0 4 * * 1';
  readonly category: LawCategory = 'insurance';
  readonly state = undefined;

  private readonly apiBase = 'https://www.federalregister.gov/api/v1/documents.json';
  private readonly agencies = [
    'health-and-human-services-department',
    'centers-for-medicare-medicaid-services',
    'internal-revenue-service',
  ];

  async scrape(): Promise<ScrapeResult> {
    logger.info('Fetching Federal Register documents', { sourceId: this.sourceId });
    const params = new URLSearchParams();
    this.agencies.forEach(a => params.append('conditions[agencies][]', a));
    params.set('conditions[type][]', 'Rule');
    params.set('conditions[publication_date][gte]', this.daysAgo(14));
    ['document_number','title','abstract','publication_date','html_url'].forEach(f => params.append('fields[]', f));
    params.set('per_page', '20');
    params.set('order', 'newest');

    const response = await axios.get<{ results: FRDocument[] }>(`${this.apiBase}?${params}`, {
      timeout: 15_000,
      headers: { 'User-Agent': 'NomadRules-Crawler/1.0' },
    });

    const content = response.data.results
      .map(d => [d.publication_date, d.document_number, d.title, d.abstract ?? '', d.html_url].join(' | '))
      .join('\n');

    return { sourceId: this.sourceId, content, contentHash: computeHash(content), url: this.apiBase, scrapedAt: new Date(), strategy: 'api' };
  }

  private daysAgo(n: number): string {
    const d = new Date();
    d.setDate(d.getDate() - n);
    return d.toISOString().split('T')[0]!;
  }
}

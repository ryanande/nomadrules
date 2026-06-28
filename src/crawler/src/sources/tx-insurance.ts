import type { ScrapeResult } from '../types/scraper.js';
import { BaseScraper } from './base.js';
import { logger } from '../core/logger.js';

export class TxInsuranceBulletinsScraper extends BaseScraper {
  readonly sourceId = 'tx-insurance-bulletins';
  readonly schedule = '0 3 * * *';
  readonly category = 'insurance' as const;
  readonly state = 'TX';

  private readonly url = 'https://www.tdi.texas.gov/bulletins/index.html';
  private readonly contentSelector = 'main, #main-content, .content-wrapper';

  async scrape(): Promise<ScrapeResult> {
    logger.info('Scraping TX insurance bulletins', { sourceId: this.sourceId });
    const { page, context } = await this.getPage(this.url);
    try {
      return this.buildResult(await this.extractContent(page, this.contentSelector), this.url, 'html-diff');
    } finally {
      await this.cleanup(context);
    }
  }
}

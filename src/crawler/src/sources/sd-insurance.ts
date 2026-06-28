import type { ScrapeResult } from '../types/scraper.js';
import { BaseScraper } from './base.js';
import { logger } from '../core/logger.js';

export class SdInsuranceBulletinsScraper extends BaseScraper {
  readonly sourceId = 'sd-insurance-bulletins';
  readonly schedule = '0 2 * * *';
  readonly category = 'insurance' as const;
  readonly state = 'SD';

  private readonly url = 'https://dlr.sd.gov/insurance/bulletins.aspx';
  private readonly contentSelector = '#dnn_ctr407_HtmlModule_lblContent';

  async scrape(): Promise<ScrapeResult> {
    logger.info('Scraping SD insurance bulletins', { sourceId: this.sourceId });
    const { page, context } = await this.getPage(this.url);
    try {
      return this.buildResult(await this.extractContent(page, this.contentSelector), this.url, 'html-diff');
    } finally {
      await this.cleanup(context);
    }
  }
}

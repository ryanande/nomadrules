import { Browser, BrowserContext, Page, chromium } from 'playwright';
import type { ISourceScraper, LawCategory, ScrapeResult } from '../types/scraper.js';
import { normalizeContent, computeHash } from '../core/diffEngine.js';
import { logger } from '../core/logger.js';

export abstract class BaseScraper implements ISourceScraper {
  abstract readonly sourceId: string;
  abstract readonly schedule: string;
  abstract readonly category: LawCategory;
  readonly state?: string;

  private browser: Browser | null = null;

  protected async getPage(url: string): Promise<{ page: Page; context: BrowserContext }> {
    // --no-sandbox: Chromium's own sandbox needs Linux capabilities the pod's
    // securityContext (runAsNonRoot, all capabilities dropped — see
    // infra/helm/templates/crawler-cronjob.yaml) deliberately doesn't grant.
    // Container-level isolation (non-root user, dropped caps, no privilege
    // escalation) is the substitute sandbox boundary here, same trade-off
    // Playwright's own container/CI docs recommend.
    if (!this.browser) this.browser = await chromium.launch({ headless: true, args: ['--no-sandbox'] });
    const context = await this.browser.newContext({
      userAgent: 'NomadRules-Crawler/1.0 (regulatory monitoring; contact info@nomadrules.com)',
    });
    const page = await context.newPage();
    await page.route('**/*', (route) => {
      if (['image', 'media', 'font'].includes(route.request().resourceType())) {
        void route.abort();
      } else {
        void route.continue();
      }
    });
    logger.debug('Navigating', { url });
    await page.goto(url, { waitUntil: 'networkidle', timeout: 30_000 });
    return { page, context };
  }

  protected async extractContent(page: Page, selector?: string): Promise<string> {
    let html: string;
    if (selector) {
      const el = await page.$(selector);
      html = el ? await el.innerHTML() : await page.content();
      if (!el) logger.warn('Selector not found, falling back to full page', { selector });
    } else {
      html = await page.content();
    }
    return normalizeContent(html);
  }

  protected async cleanup(context: BrowserContext): Promise<void> {
    await context.close();
  }

  protected buildResult(content: string, url: string, strategy: ScrapeResult['strategy']): ScrapeResult {
    return { sourceId: this.sourceId, content, contentHash: computeHash(content), url, scrapedAt: new Date(), strategy };
  }

  abstract scrape(): Promise<ScrapeResult>;
}

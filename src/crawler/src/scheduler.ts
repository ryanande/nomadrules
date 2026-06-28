import { randomUUID } from 'crypto';
import cron from 'node-cron';
import type { ISourceScraper } from './types/scraper.js';
import type { LawChangeDetected } from './types/messages.js';
import { SnapshotStore } from './core/snapshotStore.js';
import { createPublisher, type IMessagePublisher } from './core/messageBus.js';
import { detectDiff } from './core/diffEngine.js';
import { logger } from './core/logger.js';

export class CrawlerScheduler {
  private scrapers: ISourceScraper[] = [];
  private snapshotStore: SnapshotStore;
  private publisher: IMessagePublisher;
  private tasks: cron.ScheduledTask[] = [];

  constructor() {
    const connStr = process.env.AZURE_STORAGE_CONNECTION_STRING ?? 'UseDevelopmentStorage=true';
    this.snapshotStore = new SnapshotStore(connStr);
    this.publisher = createPublisher();
  }

  register(scraper: ISourceScraper): this {
    this.scrapers.push(scraper);
    return this;
  }

  async init(): Promise<void> {
    await this.snapshotStore.init();
    logger.info('Scheduler initialised', { scrapers: this.scrapers.map(s => s.sourceId) });
  }

  start(): void {
    for (const scraper of this.scrapers) {
      const task = cron.schedule(scraper.schedule, () => { void this.runScraper(scraper); });
      this.tasks.push(task);
      logger.info('Scraper scheduled', { sourceId: scraper.sourceId, schedule: scraper.schedule });
    }
  }

  async runAll(): Promise<void> {
    await Promise.allSettled(this.scrapers.map(s => this.runScraper(s)));
  }

  async runOne(sourceId: string): Promise<void> {
    const scraper = this.scrapers.find(s => s.sourceId === sourceId);
    if (!scraper) throw new Error(`No scraper registered for sourceId: ${sourceId}`);
    await this.runScraper(scraper);
  }

  private async runScraper(scraper: ISourceScraper): Promise<void> {
    const startedAt = Date.now();
    logger.info('Scraper started', { sourceId: scraper.sourceId });
    try {
      const result   = await scraper.scrape();
      const snapshot = await this.snapshotStore.getSnapshot(scraper.sourceId);
      const diff     = detectDiff(result.content, snapshot?.content ?? null, snapshot?.hash ?? null);

      logger.info('Diff computed', {
        sourceId: scraper.sourceId, hasChanged: diff.hasChanged,
        changeRatio: diff.changeRatio.toFixed(4), durationMs: Date.now() - startedAt,
      });

      await this.snapshotStore.saveSnapshot(scraper.sourceId, result.content, result.contentHash);

      if (!diff.hasChanged) return;

      const detectedAt = result.scrapedAt.toISOString();
      if (snapshot) await this.snapshotStore.archiveSnapshot(scraper.sourceId, snapshot.content, snapshot.hash, detectedAt);

      const message: LawChangeDetected = {
        messageType: 'LawChangeDetected', messageId: randomUUID(),
        sourceId: scraper.sourceId, rawContent: result.content,
        contentHash: result.contentHash, previousHash: diff.previousHash,
        url: result.url, state: scraper.state, category: scraper.category, detectedAt,
      };
      await this.publisher.publish(message);
    } catch (err) {
      logger.error('Scraper failed', { sourceId: scraper.sourceId, error: String(err) });
    }
  }

  async shutdown(): Promise<void> {
    this.tasks.forEach(t => t.stop());
    await this.publisher.close();
    logger.info('Scheduler shut down');
  }
}

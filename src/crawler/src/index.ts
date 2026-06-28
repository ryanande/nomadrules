import 'dotenv/config';
import { CrawlerScheduler } from './scheduler.js';
import { SdInsuranceBulletinsScraper } from './sources/sd-insurance.js';
import { TxInsuranceBulletinsScraper } from './sources/tx-insurance.js';
import { FederalRegisterScraper } from './sources/federal-register.js';
import { logger } from './core/logger.js';

async function main(): Promise<void> {
  const args   = process.argv.slice(2);
  const runNow = args.includes('--run-now');
  const runOne = args.find(a => a.startsWith('--source='))?.split('=')[1];

  const scheduler = new CrawlerScheduler();
  await scheduler.init();

  scheduler
    .register(new SdInsuranceBulletinsScraper())
    .register(new TxInsuranceBulletinsScraper())
    .register(new FederalRegisterScraper());

  if (runOne) {
    logger.info(`Running single scraper: ${runOne}`);
    await scheduler.runOne(runOne);
    await scheduler.shutdown();
    return;
  }

  if (runNow) {
    logger.info('Running all scrapers immediately');
    await scheduler.runAll();
    await scheduler.shutdown();
    return;
  }

  scheduler.start();
  logger.info('Crawler scheduler running. Ctrl+C to stop.');

  const shutdown = async (): Promise<void> => {
    await scheduler.shutdown();
    process.exit(0);
  };
  process.on('SIGINT',  () => void shutdown());
  process.on('SIGTERM', () => void shutdown());
}

main().catch(err => { console.error('Fatal:', err); process.exit(1); });

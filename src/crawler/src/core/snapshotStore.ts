import { BlobServiceClient, ContainerClient } from '@azure/storage-blob';
import { logger } from './logger.js';

const CONTAINER_NAME = 'crawler-snapshots';

interface Snapshot { content: string; hash: string; savedAt: string; }

export class SnapshotStore {
  private containerClient: ContainerClient;

  constructor(connectionString: string) {
    const blobServiceClient = BlobServiceClient.fromConnectionString(connectionString);
    this.containerClient = blobServiceClient.getContainerClient(CONTAINER_NAME);
  }

  async init(): Promise<void> {
    await this.containerClient.createIfNotExists();
    logger.info('SnapshotStore ready', { container: CONTAINER_NAME });
  }

  async getSnapshot(sourceId: string): Promise<Snapshot | null> {
    const blobClient = this.containerClient.getBlobClient(`${sourceId}/latest.json`);
    try {
      if (!(await blobClient.exists())) return null;
      const download = await blobClient.download();
      const body = await streamToString(download.readableStreamBody!);
      return JSON.parse(body) as Snapshot;
    } catch (err) {
      logger.warn('Could not read snapshot', { sourceId, error: String(err) });
      return null;
    }
  }

  async saveSnapshot(sourceId: string, content: string, hash: string): Promise<void> {
    const blob = this.containerClient.getBlockBlobClient(`${sourceId}/latest.json`);
    const data = JSON.stringify({ content, hash, savedAt: new Date().toISOString() });
    await blob.upload(data, Buffer.byteLength(data), {
      blobHTTPHeaders: { blobContentType: 'application/json' },
    });
  }

  async archiveSnapshot(sourceId: string, content: string, hash: string, detectedAt: string): Promise<void> {
    const key = detectedAt.replace(/[:.]/g, '-');
    const blob = this.containerClient.getBlockBlobClient(`${sourceId}/archive/${key}.json`);
    const data = JSON.stringify({ content, hash, detectedAt });
    await blob.upload(data, Buffer.byteLength(data), {
      blobHTTPHeaders: { blobContentType: 'application/json' },
    });
  }
}

async function streamToString(readable: NodeJS.ReadableStream): Promise<string> {
  const chunks: Buffer[] = [];
  for await (const chunk of readable) {
    chunks.push(Buffer.isBuffer(chunk) ? chunk : Buffer.from(String(chunk)));
  }
  return Buffer.concat(chunks).toString('utf-8');
}

import { ServiceBusClient, ServiceBusSender } from '@azure/service-bus';
import { mkdir, writeFile } from 'fs/promises';
import { join } from 'path';
import type { LawChangeDetected } from '../types/messages.js';
import { logger } from './logger.js';

export interface IMessagePublisher {
  publish(message: LawChangeDetected): Promise<void>;
  close(): Promise<void>;
}

export class ServiceBusPublisher implements IMessagePublisher {
  private client: ServiceBusClient;
  private sender: ServiceBusSender;

  constructor(connectionString: string, queueName: string) {
    this.client = new ServiceBusClient(connectionString);
    this.sender = this.client.createSender(queueName);
  }

  async publish(message: LawChangeDetected): Promise<void> {
    await this.sender.sendMessages({
      body: message,
      contentType: 'application/json',
      subject: message.messageType,
      messageId: message.messageId,
    });
    logger.info('Published to Service Bus', { messageId: message.messageId, sourceId: message.sourceId });
  }

  async close(): Promise<void> {
    await this.sender.close();
    await this.client.close();
  }
}

export class LocalFilePublisher implements IMessagePublisher {
  constructor(private queueDir = './local-queue') {}

  async publish(message: LawChangeDetected): Promise<void> {
    await mkdir(this.queueDir, { recursive: true });
    const filename = join(this.queueDir, `${message.messageId}.json`);
    await writeFile(filename, JSON.stringify(message, null, 2), 'utf-8');
    logger.info('Message written to local queue', { file: filename, sourceId: message.sourceId });
  }

  async close(): Promise<void> {}
}

export function createPublisher(): IMessagePublisher {
  const transport = process.env.TRANSPORT ?? 'local';
  if (transport === 'servicebus') {
    const connectionString = process.env.AZURE_SERVICE_BUS_CONNECTION_STRING;
    const queueName = process.env.AZURE_SERVICE_BUS_QUEUE ?? 'law-changes';
    if (!connectionString) throw new Error('AZURE_SERVICE_BUS_CONNECTION_STRING is required when TRANSPORT=servicebus');
    return new ServiceBusPublisher(connectionString, queueName);
  }
  return new LocalFilePublisher(process.env.LOCAL_QUEUE_DIR ?? './local-queue');
}

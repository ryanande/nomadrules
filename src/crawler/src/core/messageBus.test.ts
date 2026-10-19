import { afterEach, describe, expect, it } from 'vitest';
import { LocalFilePublisher, createPublisher } from './messageBus.js';

describe('createPublisher', () => {
  const saved = { ...process.env };
  afterEach(() => {
    process.env = { ...saved };
  });

  it('defaults to the local file queue', () => {
    delete process.env.TRANSPORT;
    expect(createPublisher()).toBeInstanceOf(LocalFilePublisher);
  });

  it('rejects an unknown transport instead of silently falling back to local', () => {
    process.env.TRANSPORT = 'rabbitmq';
    expect(() => createPublisher()).toThrow(/Unknown TRANSPORT 'rabbitmq'/);
  });

  it.each(['toString', 'constructor', '__proto__'])('rejects inherited Object keys such as %s', (name) => {
    process.env.TRANSPORT = name;
    expect(() => createPublisher()).toThrow(/Unknown TRANSPORT/);
  });

  it('requires a connection string for servicebus', () => {
    process.env.TRANSPORT = 'servicebus';
    delete process.env.AZURE_SERVICE_BUS_CONNECTION_STRING;
    expect(() => createPublisher()).toThrow(/AZURE_SERVICE_BUS_CONNECTION_STRING/);
  });
});

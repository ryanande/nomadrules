export interface Logger {
  info(message: string, meta?: Record<string, unknown>): void;
  warn(message: string, meta?: Record<string, unknown>): void;
  error(message: string, meta?: Record<string, unknown>): void;
  debug(message: string, meta?: Record<string, unknown>): void;
}

function formatLog(level: string, message: string, meta?: Record<string, unknown>): string {
  return JSON.stringify({ timestamp: new Date().toISOString(), level, message, ...meta });
}

export const logger: Logger = {
  info:  (msg, meta) => console.log(formatLog('INFO', msg, meta)),
  warn:  (msg, meta) => console.warn(formatLog('WARN', msg, meta)),
  error: (msg, meta) => console.error(formatLog('ERROR', msg, meta)),
  debug: (msg, meta) => {
    if (process.env.LOG_LEVEL === 'debug') console.log(formatLog('DEBUG', msg, meta));
  },
};

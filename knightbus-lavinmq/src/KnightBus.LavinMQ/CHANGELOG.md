# Changelog

## 1.0.0

- Initial release of the LavinMQ transport (AMQP 0-9-1, built on `RabbitMQ.Client`).
- Commands, events (fanout exchange with one queue per subscription), and dead-lettering
  via LavinMQ's native `x-dead-letter-exchange` and `x-delivery-limit`.
- Scheduling through LavinMQ's built-in `x-delayed-message` exchange.
- `KnightBus.LavinMQ.Management` provides `IQueueManager` (list/peek/dead-letter handling).
- Works against LavinMQ, RabbitMQ and CloudAMQP; LavinMQ is the tested target.

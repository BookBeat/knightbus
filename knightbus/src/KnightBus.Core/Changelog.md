# KnightBus.Core Changelog
# 15.0.0
* Throw if etag differs when updating blob saga data

# 14.0.0
* Removed ConsoleWriter

## 8.4.0
* Added GetMapping for IMessage, to get IMessageMapper instance

## 8.3.0
* Added ISagaDuplicateDetected<> that can be used to handle the duplicated message before it is completed.  
    It can e.g. be used to re-schedule the message later on before it is deleted
